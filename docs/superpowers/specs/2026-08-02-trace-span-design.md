# Trace Span 可观测性设计

> 状态: draft | 日期: 2026-08-02

## 1. 动机

当前 trace 是扁平 event list，缺少父子关系和结构化耗时，无法支持：
- 运行时提前终止判定（条目数够了 + 列表到底 → 停）
- 性能瓶颈定位（哪步耗时最长？引擎在等什么？）
- 跨 run 基线对比（这个场景通常几个条目？p95 步数？）

## 2. 目标

1. 引入 Span 模型建立 trace 父子关系树
2. 从树中可重建条目树 + FSM 状态流
3. 支持实时分析和离线基线
4. 低侵入——引擎逻辑不改，Phase 1 手动埋点（~10 行），Phase 2 迁移到 `[TraceSpan]` source generator

## 3. Span Schema

### 3.1 数据结构

```csharp
public sealed record class TraceSpan(
    string SpanId,
    string? ParentSpanId,
    string SpanType,             // 见 §3.3 枚举
    string SpanName,             // 人类可读标识
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    string Status,               // "ok" | "error" | "deny" | "skip"
    TraceContext? Context,       // stepNumber, depth, nodeId, traceId
    Dictionary<string, object>? Attributes = null)
{
    public double DurationMs =>
        EndTime.HasValue ? (EndTime.Value - StartTime).TotalMilliseconds : 0;
}
```

### 3.2 record_type

新增 `"span"` 到 trace JSONL。与已有 `execution` / `state_transition` / `page_transition` / `ai_call` 并行存在。

```json
{"record_type":"span","spanId":"...","parentSpanId":"...","spanType":"entry.observed",...}
```

### 3.3 spanType 分类

#### 引擎层

| spanType | 父 Span | 生命周期 | attributes | 触发位置 |
|----------|---------|---------|------------|---------|
| `engine.run` | null (root) | 长（整个 run） | — | `TraversalEngine.RunAsync` 外层 |
| `engine.step` | `engine.run` | 长（单步） | `step.number`, `step.wall_ms`, `step.ai_ms`, `step.action_ms`, `step.idle_ms` | `RunAsync` 循环内 |

#### 条目层

| spanType | 父 Span | 生命周期 | attributes | 触发位置 |
|----------|---------|---------|------------|---------|
| `entry.generate` | `engine.step` | 中 | `entry.parent_node`, `entry.fingerprint`, `entry.match_count`, `entry.ignored_count`, `gen.*_ms` | `DynamicChildManager.Generate` |
| `entry.observed` | `entry.generate` | 瞬时 | `entry.name`, `entry.type`, `entry.parent`, `entry.node_id`, `entry.match_rule`, `entry.index` | `Generate` 每个 matchResult |
| `entry.ignored` | `entry.generate` | 瞬时 | `entry.name`, `entry.reason` | `Generate` dedup 命中 |
| `entry.visited` | `engine.step` | 中 | `entry.name`, `entry.node_id`, `entry.step`, `entry.depth` | `InterceptionHandler` push child |
| `entry.skipped` | `entry.visited` | 瞬时 | `entry.name`, `entry.rule_id`, `entry.reason` | `SafeActionExecutor` deny |
| `entry.action` | `entry.visited` | 长 | `entry.name`, `action.type`, `action.result`, `action.*_ms` | 操作完成确认点 |

#### 操作层

| spanType | 父 Span | 生命周期 | attributes | 触发位置 |
|----------|---------|---------|------------|---------|
| `action.click` | `entry.visited` | 长 | `action.type`, `action.result`, `action.adb_ms`, `action.stabilize_ms` | ADB tap |
| `action.scroll` | `entry.visited` 或 `engine.step` | 长 | `action.type`, `action.adb_ms`, `action.page_analysis_ms` | ADB swipe |
| `action.back` | `engine.step` | 长 | `action.type`, `action.adb_ms`, `action.stabilize_ms` | ADB keyevent |
| `action.launch` | `engine.run` | 长 | `action.type`, `action.adb_ms` | ADB am start |
| `action.wait` | `engine.step` | 长 | `action.type`, `action.wait_ms` | Task.Delay |

#### AI 层

| spanType | 父 Span | 生命周期 | attributes | 触发位置 |
|----------|---------|---------|------------|---------|
| `ai.call` | `engine.step` 或 `entry.action` | 长 | `ai.capability`, `ai.provider_id`, `ai.latency_ms`, `ai.tokens`, `ai.success`, `ai.model`, `ai.mode` | HTTP round-trip |
| `ai.analyze` | `ai.call` | 长 | `ai.page_fingerprint`, `ai.item_count`, `ai.retry_count` | PageAnalysis 完成 |

#### 分析层

| spanType | 父 Span | 生命周期 | attributes | 触发位置 |
|----------|---------|---------|------------|---------|
| `analyze.completion` | `engine.step` | 瞬时 | `analyze.judge`, `analyze.verdict`, `analyze.confidence`, `analyze.items_visited`, `analyze.items_total`, `analyze.end_of_list`, `analyze.reason` | `CompletionAnalyzer` |
| `analyze.error_loop` | `engine.step` | 瞬时 | `analyze.judge`, `analyze.verdict`, `analyze.consecutive_skips`, `analyze.reason` | `ErrorAnalyzer` |
| `analyze.tree` | `engine.run` | 瞬时 | `analyze.total_nodes` | 事后 `BaselineBuilder` |

### 3.4 条目层差异矩阵

| 维度 | entry.observed | entry.visited | entry.skipped | entry.action |
|------|:---:|:---:|:---:|:---:|
| 含义 | 引擎看到了 | 选中了 | 选中但被拒 | 操作完成了 |
| 父 Span | entry.generate | engine.step | entry.visited | entry.visited |
| 一定有 ADB？ | ❌ | ❌ | ❌ | ✅ |
| 一定有子 Span？ | ❌ | ✅ | ❌ | ❌ |
| entry.name | 匹配到的 item 名 | 同 observed | 同 visited | 同 visited |

### 3.5 父子关系树

```
engine.run
├── action.launch
│
├── engine.step (step 1-3: observe)
│   ├── entry.generate
│   │   ├── entry.observed (Network & internet)
│   │   ├── entry.observed (Connected devices)
│   │   ├── entry.observed (Apps)
│   │   └── ...
│   └── ...
│
├── engine.step (step 4: click Network)
│   ├── entry.generate
│   │   └── entry.observed (同上, dedup → entry.ignored)
│   ├── entry.visited (Network & internet)
│   │   ├── action.click
│   │   │   ├── ai.analyze           ← AI 分析新页面
│   │   │   └── ...
│   │   └── entry.action
│   └── analyze.completion {verdict:"continue"}
│
├── engine.step (step 5: skip dangerous)
│   ├── entry.visited (Reset options)
│   │   └── entry.skipped             ← 安全拒绝，无 action.click
│   └── ...
│
├── engine.step (scroll)
│   ├── entry.generate
│   │   ├── entry.ignored (已知条目, dedup)
│   │   └── entry.observed (新条目: Battery, Storage, ...)
│   └── action.scroll
│
└── engine.step (step 34: end detected)
    ├── entry.generate
    │   └── entry.ignored (全部见过)
    └── analyze.completion {verdict:"terminate", confidence:0.95}
```

### 3.6 耗时规则

| 规则 | 说明 |
|------|------|
| `total_ms` == span interval | 一致性约束 |
| 子 span interval 之和 ≤ 父 interval | 差值 = overhead/idle |
| 所有 `*_ms` 属性用整数（毫秒） | 避免精度问题 |
| `*_ms` 可选 | 未记录 = unknown |

## 4. 接口设计

### 4.1 ITraceRecorder 扩展

```csharp
// ITraceRecorder 新增 2 方法
string StartSpan(
    string spanType,
    string? parentSpanId = null,
    Dictionary<string, object>? attributes = null,
    CancellationToken cancellationToken = default);

void EndSpan(
    string spanId,
    string status = "ok",
    Dictionary<string, object>? attributes = null,
    CancellationToken cancellationToken = default);
```

### 4.2 ITraceQuery（新增，继承 ITraceService）

```csharp
public interface ITraceQuery : ITraceService
{
    TraceSpan? GetRootSpan();
    IReadOnlyList<TraceSpan> GetSpansByType(string spanType);
    IReadOnlyList<TraceSpan> GetChildSpans(string parentSpanId);
    TraceSpan? GetSpan(string spanId);
    IReadOnlyList<TraceSpan> GetAllSpans();
}
```

`ITraceService` 12 方法保留不变。`ITraceQuery` 是纯增量。`InMemoryTraceService` 直接实现 `ITraceQuery`。

### 4.3 消费关系

```
VerificationAnalyzer (已有) → ITraceQuery
CompletionAnalyzer  (新)    → ITraceQuery
ErrorAnalyzer       (新)    → ITraceQuery
BaselineBuilder     (新)    → ITraceQuery
```

## 5. Phase 1 埋点（手动）

### 5.1 文件改动

| 文件 | 埋点 | spanType |
|------|------|----------|
| `TraversalEngine.cs:RunAsync` | 循环外层 + 每步 | `engine.run` + `engine.step` |
| `TraversalEngine.cs:Generate` | Generate 整体 + 每个 item | `entry.generate` / `entry.observed` / `entry.ignored` |
| `InterceptionHandler.cs:OnBranch` | push child 后 | `entry.visited` |
| `SafeActionExecutor.cs:DecideAsync` | deny 分支 | `entry.skipped` |

### 5.2 代码示例

```csharp
// TraversalEngine.RunAsync
var runSpanId = _traceRecorder?.StartSpan("engine.run");
for (int i = 0; i < _config.MaxSteps; i++)
{
    var stepSpanId = _traceRecorder?.StartSpan("engine.step", runSpanId);
    // ... existing loop body ...
    _traceRecorder?.EndSpan(stepSpanId);
}
_traceRecorder?.EndSpan(runSpanId);

// DynamicChildManager.Generate
var genSpanId = _traceRecorder?.StartSpan("entry.generate", _currentStepSpanId,
    new() { ["entry.parent_node"] = node.NodeId, ["entry.fingerprint"] = fingerprint });
// ... foreach matchResult ...
    // dedup hit:
    _traceRecorder?.StartSpan("entry.ignored", genSpanId, new() { ["entry.name"] = itemText });
    // new item:
    _traceRecorder?.StartSpan("entry.observed", genSpanId, new() { ... });
// ... end foreach ...
_traceRecorder?.EndSpan(genSpanId, "ok", new() { ["entry.match_count"] = ... });
```

### 5.3 依赖注入

`DynamicChildManager` 需要 `ITraceRecorder`（目前已有 `ITraceCoordinator`）。`InterceptionHandler` 通过 `StepContext.Trace` 写入（已有通路）。

### 5.4 已有 trace 兼容

Span 写入到 `InMemoryTraceStorage._spans` 列表，与已有 `_executions` / `_transitions` / `_pageTransitions` / `_aiCalls` / `_errors` 并列存储。现有消费者完全不受影响。

## 6. Phase 2 [TraceSpan] Source Generator

### 6.1 目标

消除手写 `StartSpan`/`EndSpan`，用 attribute 声明式埋点。Source generator 编译期生成 wrapper 代码。

### 6.2 Attribute 定义

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceSpanAttribute : Attribute
{
    public string SpanType { get; }
    public string? SpanName { get; set; }
    public string? ParentField { get; set; }       // 父 Span 字段名
    public string RecorderField { get; set; } = "_traceRecorder";
    public string[]? Attributes { get; set; }       // "key:expr" 对
}
```

### 6.3 使用示例

```csharp
// ADB 操作
[TraceSpan("action.click", Attributes = ["action.type:click"])]
protected override Task<bool> TapAsync(double x, double y) { ... }

// AI 调用
[TraceSpan("ai.call", Attributes = [
    "ai.capability:capability",
    "ai.provider_id:providerId",
    "ai.latency_ms:latencyMs"
])]
Task<ModelResponse> SendAsync(string capability, string providerId, ...) { ... }

// 引擎方法
[TraceSpan("entry.generate",
    ParentField = "_currentStepSpanId",
    Attributes = ["entry.parent_node:node.NodeId"])]
void Generate(TraversalNode node, ITraversalContext context) { ... }
```

### 6.4 迁移

Phase 2 的 source generator 生成的代码与 Phase 1 手写代码行为完全等价。迁移时删除手写代码，添加 attribute，重新编译即可。

## 7. 分析器

### 7.1 设计原则（方案 C）

```
┌──────────────┐      读 Span       ┌──────────────┐
│  ITraceQuery │ ←─────────────── │   Analyzer   │
│  (引擎写入)   │                   │  (Host 判定)  │
└──────────────┘                   └──────┬───────┘
                                         │ 写判定 Span
                                  ┌──────▼───────┐
                                  │  ITraceRecorder │
                                  │  (判定记录)    │
                                  └──────┬───────┘
                                         │ 跨 run 聚合
                                  ┌──────▼───────┐
                                  │   Baseline    │
                                  │   逐渐完善阈值  │
                                  └──────────────┘
```

- 引擎只写发生了什么（Span），不关心"完成"概念
- Analyzer 是唯一知道"什么叫完成"的地方
- Analyzer 写 `analyze.completion` span 回 trace，形成审计记录
- 离线从判定记录聚合基线，阈值从硬编码进化到数据驱动

### 7.2 接口

```csharp
// Core
public interface ICompletionAnalyzer
{
    Task<CompletionVerdict?> EvaluateAsync(ITraceQuery trace, CancellationToken ct);
}

public sealed record CompletionVerdict(
    bool ShouldTerminate,
    string Reason,
    double Confidence  // 0.0-1.0，由判定逻辑确定
);
```

### 7.3 CompletionVerdict 置信度定义

| Confidence | 条件 | 含义 |
|-----------|------|------|
| 1.0 | `pending == 0 && endOfList` | 确定——所有条目已处理且列表到底 |
| 0.9 | `visited >= p95 && endOfList` | 强烈——已超过 95% 历史 run 的条目数 |
| 0.7 | `visited >= p50 && endOfList` | 可能——达到历史中位数 |
| 0.5 | `p50 < visited < p95` 无 endOfList | 弱——接近中位数但无明确信号 |
| 0.0 | `visited < p50` 无 endOfList | 不终止——远未达到预期 |

### 7.4 判定边界

#### 7.4.1 决策状态机

```
                    ┌──────────┐
                    │  Observe │ ← 初始，每 500ms 轮询
                    └────┬─────┘
                         │
              ┌──────────┼──────────┐
              ▼          ▼          ▼
           Halt      Recommend     Continue
        (conf≥0.9)   (0.7-0.9)    (<0.7)
              │          │           │
              ▼          ▼           │
         直接 cancel  写入判定       │
                     通知调用方      │
                        │           │
                   ┌────▼────┐      │
                   │ 决策回调 │     │
                   └────┬────┘      │
                    ┌───┴───┐       │
                    ▼       ▼       │
                 确认     拒绝      │
                    │       │       │
                    ▼       ▼       │
                  cancel   继续 ────┘
```

#### 7.4.2 判定等级

| 等级 | 条件 | 动作 | 说明 |
|------|------|------|------|
| **Halt** | `pending == 0 && endOfList` | 立即终止 | 穷尽，无需外部判断 |
| **Terminate** | `visited ≥ p95` | 立即终止 | 统计足够（≥95% 历史 run） |
| **Recommend** | `p50 ≤ visited < p95 && endOfList` | 建议终止 | 需外部确认（首次达标、新场景） |
| **Warn** | `visited ≥ p95 * 1.5` 或连续 5 次 skipped | 警告终止 | 异常保护，防资源浪费 |
| **Observe** | 其他 | 继续观察 | 不满足任何终止条件 |

#### 7.4.3 置信度 ↔ 动作映射

| 置信度 | 触发条件 | 基线依赖 | 动作 |
|--------|---------|---------|------|
| 1.0 | `pending == 0 && endOfList` | 否 | Halt |
| 0.9 | `visited ≥ p95 && endOfList` | 是（≥10 条） | Terminate |
| 0.8 | `visited ≥ p95` 无 endOfList | 是（≥10 条） | Terminate（弱信号，统计够） |
| 0.7 | `p50 ≤ visited < p95 && endOfList` | 是（≥10 条） | Recommend |
| 0.5 | `p50 ≤ visited < p95` 无 endOfList | 是（≥10 条） | Observe |
| 0.3 | `visited < p50` 有 endOfList | 否 | Observe（可能异常） |
| 0.0 | `visited == 0` 或无基线 | 否 | Observe |

#### 7.4.4 冷启动（基线不足 10 条）

```
基线 < 10 条时：
  - 仅 Halt 生效（pending==0 && endOfList → 直接终止）
  - Terminate / Recommend 不触发（阈值不可靠）
  - Warn 仍生效（异常保护不依赖基线）
  - 所有判定写入 Observe span，积累数据

基线 ≥ 10 条后，动态阈值自动启用。
Confidence 随积累 run 数增加而自然提升。
```

#### 7.4.5 边界条件

| 边缘情况 | 行为 |
|---------|------|
| 基线文件不存在 | 降级为纯 Observe，仅 Halt 生效 |
| 基线文件损坏 | 同上，日志告警 |
| 同一 run 多次 Recommend | 降级：第一次 Recommend，第二次直接 Terminate（防骚扰） |
| `observed` 突增（> p95 × 2） | 可能是 UI 改版，标异常不触发 Terminate，上报 |
| Cancel 后引擎状态 | `cts.Cancel()` → 引擎 `OperationCanceledException` → 正常退出 |
| Monitor crash | 引擎不受影响，继续走到 MaxSteps 或 Exhaustive |
| 不同场景 ID | 各自独立基线文件，互不干扰 |

### 7.5 判定逻辑

```
EnumerateCompletionAnalyzer.EvaluateAsync(traceQuery):

  observed = GetSpansByType("entry.observed").Count
  visited  = GetSpansByType("entry.visited").Count
  skipped  = GetSpansByType("entry.skipped").Count
  pending  = observed - visited - skipped

  endSteps  = GetSpansByType("engine.step")
                .Where(s.Attributes["step.end_reached"] == true)

  loadedBaseline = 尝试加载 artifacts/baselines/<scenarioId>.jsonl

  // Rule 1: 穷尽
  if pending <= 0 && endSteps.Any()
     → terminate, reason:"exhausted", confidence:1.0

  // Rule 2: 基线匹配（需已有 ≥ 10 条历史记录）
  if loadedBaseline != null:
      if visited >= p50 && endSteps.Any()
         → terminate, reason:"baseline_p50", confidence:0.7
      if visited >= p95 && endSteps.Any()
         → terminate, reason:"baseline_p95", confidence:0.9

  // Rule 3: 异常保护
  if visited >= p95 * 1.5
     → terminate, reason:"abnormal_excess", confidence:0.95

  // Default: continue
  → continue, reason:$"pending_{pending}_items", confidence:visited/max(observed,1)

  // 无论终止与否，写入判定 span
  traceRecorder.StartSpan("analyze.completion", currentStepSpanId, attributes:{...})
```

### 7.6 ErrorAnalyzer

```
ErrorLoopAnalyzer.EvaluateAsync(traceQuery):

  recentSkipped = 最近 N 个 engine.step 中的 entry.skipped 数量
  recentVisited = 最近 N 个 engine.step 中的 entry.visited 数量

  // 连续 5 步全是 skipped，没有 visited
  if recentSkipped >= 5 && recentVisited == 0
     → terminate, reason:"stuck_in_error_loop", confidence:0.9

  // 同一页面上 skipped 比例超过 80%
  if recentSkipped > recentVisited * 4
     → terminate, reason:"skip_rate_too_high", confidence:0.7
```

### 7.7 CompletionMonitor（调度器）

```csharp
public sealed class CompletionMonitor : IDisposable
{
    private readonly ICompletionAnalyzer[] _analyzers;
    private readonly ITraceQuery _trace;
    private readonly ITraceRecorder _recorder;
    private readonly CancellationTokenSource _cts;
    private readonly int _pollIntervalMs;       // 默认 500ms

    public async Task StartAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(_pollIntervalMs, _cts.Token);

            foreach (var analyzer in _analyzers)
            {
                var verdict = await analyzer.EvaluateAsync(_trace, _cts.Token);
                if (verdict is null) continue;

                // 写入判定 span（无论终止与否）
                _recorder.StartSpan("analyze.completion", ...,
                    attributes: new() {
                        ["analyze.verdict"] = verdict.Reason,
                        ["analyze.confidence"] = verdict.Confidence
                    });

                switch (verdict.Confidence)
                {
                    case >= 0.9f:  // Halt / Terminate → 直接终止
                        _recorder.StartSpan("analyze.run_terminated", ...);
                        await _cts.CancelAsync();
                        return;

                    case >= 0.7f:  // Recommend → 回调通知，由调用方决定
                        var confirmed = await _onRecommend?.Invoke(verdict);
                        if (confirmed == true) { await _cts.CancelAsync(); return; }
                        break;

                    default:  // Observe / Warn → 继续观察
                        break;
                }
            }
        }
    }

    public void Stop() => _cts.Cancel();
    public void Dispose() => _cts.Dispose();
}

// _onRecommend 回调签名：Func<CompletionVerdict, Task<bool?>>
//   true  → 确认终止
//   false → 拒绝，继续观察
//   null  → 无回调，Recommend 降级为 Observe
```

### 7.8 组装（Host）

```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
var baseline = BaselineBuilder.Load("artifacts/baselines/enumerate-settings-safely.jsonl");

var analyzers = new ICompletionAnalyzer[]
{
    new EnumerateCompletionAnalyzer(traceQuery, baseline),
    new ErrorLoopAnalyzer(traceQuery)
};
using var monitor = new CompletionMonitor(analyzers, traceQuery, traceRecorder, cts, 500);

_ = monitor.StartAsync();                           // 后台 500ms 轮询
var result = await engine.RunAsync(cts.Token);      // cts.Cancel() → 引擎退出
monitor.Stop();
```

## 8. BaselineBuilder（离线）

### 8.1 数据来源

每次 run 结束，读取该 run 的 span：

```
entry.observed 数量   → itemsObserved
entry.visited 数量    → itemsVisited
entry.skipped 数量    → itemsSkipped
engine.step 数量      → stepsUsed
action.scroll 数量    → scrollCount
是否有 end_reached    → endOfListDetected
引擎最终状态           → success
ai.call 耗时统计      → aiLatencyP50, aiLatencyP95
```

### 8.2 基线格式

```jsonl
{"scenarioId":"enumerate-settings-safely","timestamp":"2026-08-02T11:00:00Z","itemsObserved":18,"itemsVisited":14,"itemsSkipped":2,"stepsUsed":87,"scrollCount":8,"endOfListDetected":true,"success":true,"aiLatencyP50":4500,"aiLatencyP95":8200}
{"scenarioId":"enumerate-settings-safely","timestamp":"2026-08-02T12:30:00Z","itemsObserved":16,"itemsVisited":13,"itemsSkipped":1,"stepsUsed":92,"scrollCount":9,"endOfListDetected":true,"success":true,"aiLatencyP50":4300,"aiLatencyP95":7900}
```

存入 `artifacts/baselines/<scenarioId>.jsonl`，每个场景一个文件。

### 8.3 阈值计算

```
基线记录 ≥ 10 条后：
  计算 p50_items, p95_items
  计算 p50_steps, p95_steps
  计算 p50_ai_ms, p95_ai_ms

CompletionAnalyzer 加载基线后：
  confidence = 从 §7.4.3 表查表
  阈值从硬编码 (p50=14) 自动演化到数据驱动
```

### 8.4 文件

| 文件 | 内容 |
|------|------|
| `Core/Observability/ICompletionAnalyzer.cs` | 接口 + `CompletionVerdict` |
| `Host/Analysis/EnumerateCompletionAnalyzer.cs` | enumerate 判定 |
| `Host/Analysis/ErrorLoopAnalyzer.cs` | 错误循环检测 |
| `Host/Analysis/CompletionMonitor.cs` | 调度器 |
| `Host/Analysis/BaselineBuilder.cs` | 离线基线构建 |
| `artifacts/baselines/<scenarioId>.jsonl` | 基线数据 |

### 8.5 数据闭环

```
Run 1 ─→ trace spans ─→ BaselineBuilder ─→ baseline.jsonl (1 条, 不可用)
Run 2 ─→ trace spans ─→ BaselineBuilder ─→ baseline.jsonl (2 条, 不可用)
...
Run 10 ─→ trace spans ─→ BaselineBuilder ─→ baseline.jsonl (10 条, p50=14, p95=18)
                                            │
Run 11 ─→ EnumerateCompletionAnalyzer ──────┘ 加载基线, 动态阈值
          │
          ├─ visited=14 + endOfList → terminate (confidence 0.7, 命中 p50)
          └─ 写入 analyze.completion span → 判定可审计
```

## 8. BaselineBuilder（离线）

### 8.1 数据积累

每次 run 结束，从 trace span 提取基线数据：

```json
{"scenarioId":"enumerate-settings-safely","timestamp":"...","itemsObserved":18,"itemsVisited":14,"itemsSkipped":2,"stepsUsed":87,"scrollCount":8,"endOfListDetected":true,"success":true,"aiLatencyP50":4500,"aiLatencyP95":8200}
```

存入 `artifacts/baselines/enumerate-settings.jsonl`。

### 8.2 阈值更新

```
基线文件积累 ≥ 10 条记录后：
  p50_items = 14, p95_items = 18
  p50_steps = 85, p95_steps = 120
  p50_ai_ms = 4500, p95_ai_ms = 8200

CompletionAnalyzer 运行时加载基线：
  当前 itemsVisited >= p50_items && endOfList → 判定终止 (confidence 0.8)
  当前 itemsVisited >= p95_items → 判定终止 (confidence 0.95, 即使没有 endOfList)
  当前 stepsUsed > p95_steps * 1.5 → 异常告警
```

## 9. 架构概览

```
┌─ Core ──────────────────────────────────────────────────────┐
│  TraversalEngine.RunAsync()                                  │
│    └→ engine.run / engine.step spans                         │
│                                                              │
│  DynamicChildManager.Generate()                              │
│    └→ entry.generate / entry.observed / entry.ignored spans  │
│                                                              │
│  InterceptionHandler                                         │
│    └→ entry.visited span                                     │
│                                                              │
│  ITraceRecorder ← StartSpan/EndSpan                          │
│  ITraceQuery    ← GetRootSpan/GetSpansByType/GetChildSpans   │
│                                                              │
│  InMemoryTraceService : ITraceQuery                          │
│    ├── 已有存储（executions/transitions/pages/ai/errors）     │
│    └── 新增 _spans: List<TraceSpan>                          │
└──────────────────────────────────────────────────────────────┘
                              │
┌─ Host ──────────────────────│────────────────────────────────┐
│                    ITraceQuery (注入)                         │
│                         │                                    │
│              ┌──────────┼──────────┐                         │
│              ▼          ▼          ▼                         │
│        Completion   Error      Baseline                      │
│        Analyzer     Analyzer   Builder                       │
│              │          │          │                         │
│              └──────────┴──────────┘                         │
│                         │                                    │
│                  CompletionMonitor                           │
│                    └→ cts.Cancel()                           │
│                         │                                    │
│              engine.RunAsync(cts.Token) ← 引擎提前退出        │
└──────────────────────────────────────────────────────────────┘
```

## 10. 测试

| 层级 | 测试 |
|------|------|
| Core 单元 | `TraceSpan` 序列化/反序列化 |
| Core 单元 | `InMemoryTraceService` span CRUD |
| Core 单元 | `DynamicChildManager.Generate` 写入 `entry.generate/observed/ignored` |
| Core 单元 | `InterceptionHandler` 写入 `entry.visited` |
| Host 单元 | `SafeActionExecutor` deny 写入 `entry.skipped` |
| Host 单元 | `CompletionAnalyzer` 从 mock trace 正确判定 |
| Host 单元 | `ErrorAnalyzer` 错误循环检测 |
| 集成 | 引擎完整 run → trace span 树可重建条目树 |

## 11. 风险与缓解

| 风险 | 缓解 |
|------|------|
| Span 数量大（每条目 N 个 span） | 按 spanType 过滤查询，内存中按需加载 |
| Phase 1 手写代码在 Phase 2 迁移时需删除 | 等 source generator 成熟后再迁移，阶段性 commit |
| `ITraceQuery` 继承 `ITraceService` 可能变胖 | 如果未来发现不合适的继承关系，用组合替代 |
