# Design: trace-parent-linkage

## Context

`trace-span-helpers`（已归档）落地了 `TraceSpanScope`/`RecordEventAsync` 与 `SpanTreeEquivalenceTests` S1–S5 快照闸门（AC1），span 记录脚手架已清零。遗留三个缺口：

1. **父链缺口**：`ai.call`/`ai.analyze` 是孤儿根。D-134 P3 注释称"PageAnalyzer 跨层无通道"，但核实后 `AnalyzeCurrentPageAsync` 的 4 个调用点（`TraversalEngine.cs:290`、`InterceptionHandler.cs:217/479`、`TraversalFSM.cs:368/385`）全部位于 engine.step 上下文内，`CurrentEngineStepSpanId` 通道可用（`entry.visited` 已在用）。
2. **字段无目录**：span 属性键名是散落手写字符串（"ai.provider_id"、"action.adb_ms"…），无防漂移约束。
3. **字段无分级**：`TraceLevel`（None/Basic/Detailed/Full，对齐 Python，枚举值不可新增）只控制事件开关（`ShouldRecordEntryAttempt`/`ShouldRecordVisionCall`），同一 span 不能按级别裁剪字段。

约束（guard 冻结）：`ITraceRecorder` 9 声明方法（扩展可 additive）、`ITraceCoordinator` 27 public 成员（internal 可加）、`SpanType` enum 11 值、`SpanTypes` 18 常量、`IPageAnalyzer` 签名（§12-B"签名零改动"注释）、AC3 白名单（同步 passthrough 接缝）。

## Goals / Non-Goals

**Goals**
- `ai.call`/`ai.analyze` 父链挂到 `engine.step`；非引擎入口保留孤儿（决策已确认）。
- 全 span 属性键名集中为 `TraceFields` 常量目录。
- 同 span 按 `TraceLevel` 分级记录字段（Basic=核心、Detailed+=扩展），缺省行为与现状全量记录一致。
- S4 快照重冻结 + 新增 S6 父链场景；S1–S3/S5 不变。

**Non-Goals**
- 不新增 `SpanTypes` 成员、不改 `SpanType` enum。
- 不改 `entry.observed`/`entry.ignored` 同步 passthrough（`IDynamicChildManager.Generate` 同步 guard）。
- 不做 `[TraceSpan]` source generator（独立 deferred change；本 change 的 `TraceFields`/分级描述符成为其输入）。
- 不消除 `analyze.completion`/`analyze.error_loop` 的无父状态（分析器独立层，语义独立）。

## Decisions

### D1: 父链形态 — 注入 `ITraceContextProvider`（B），非接口签名参数（A）

```csharp
public interface ITraceContextProvider
{
    /// <summary>当前最内层 engine.step span id；非引擎上下文为 null。</summary>
    string? CurrentSpanId { get; }
}
```

- `PageAnalyzer` 构造注入（`UniBrainFactory` 接线，Provider 可选——null 时保留现状孤儿行为）。
- `ai.call` scope 的 `parentSpanId` 改为 `_traceContext?.CurrentSpanId`（运行时表达式）。
- **4 个调用点零改动**——这是选 B 而非 A 的决定性理由（A 改 `IPageAnalyzer` 签名属宪章级，且 4 调用点都要改）。

**apply 时修订（2026-08-03 裁决）**：原方案"`TraceCoordinator` 实现（`CurrentSpanId => _currentEngineStepSpanId`）"在生产走不通——引擎在 `Initialize()` 内部**自建** per-engine coordinator（`TraversalEngine.cs:117`，带 `_ctx.TraceId`/`ctx`），step id 只进该实例；Host 组合根另建 `new TraceCoordinator(recorder)`（`HostCommands.cs:1041`，traceId=null → `Active=false`）注入 PageAnalyzer，两个实例互不相通 → 生产 ai.call 仍为孤儿。经用户裁决改为 **AsyncLocal 通道**：

```csharp
public sealed class EngineStepSpanContext : ITraceContextProvider
{
    public static EngineStepSpanContext Instance { get; } = new();
    private static readonly AsyncLocal<string?> _current = new();
    public string? CurrentSpanId => _current.Value;
    internal void Set(string? spanId) => _current.Value = spanId;
    internal void Reset() => _current.Value = null;
}
```

- 引擎在 step scope 开启处 `EngineStepSpanContext.Instance.Set(stepScope.SpanId)`、`EndEngineStepSpan` helper 内 `Reset()`（悬挂错误路径不处理，下一次 Set 自然覆盖）。
- `HostCommands.CreateUniBrain` 注入 `EngineStepSpanContext.Instance`；`TraceCoordinator` 的 `ITraceContextProvider` 实现移除（生产不再使用）。
- AsyncLocal 按 async flow 隔离，多引擎并行安全；S4/S6 fixture 走生产同款通道。

备选：C（无 parent 则跳过记录）被否决——已确认保留孤儿，非引擎入口观测不丢失，S4 快照差异最小。

### D2: 字段目录 `TraceFields` — 静态常量类

- 全键名集中为 `public const string`（§5 清单全量）。
- **常量值不变**（JSONL 持久化字段、下游消费兼容），仅引用方式变化。
- 新增目录完整性测试（键名非空、`layer.` 命名空间）。
- 成为未来 generator 的 TSG002 字段校验输入。

### D3: 字段分级 — 描述符 + helper 层过滤

```csharp
public static class TraceSpanFields
{
    public static readonly SpanFieldProfile AiCall = new(
        Basic: new[] { "ai.success", "ai.mode", "ai.capability" },
        Extended: new[] { "ai.provider_id", "ai.model", "ai.tokens", "ai.latency_ms" });
    // 每 spanType 一个
}
```

- helper additive 演进：`BeginSpanAsync(..., SpanFieldProfile? profile, TraceLevel level)` 或等效——记录时按 level 过滤 Extended 键。
- `level` 来源：`EntryConfig.TraceLevel`（引擎配置，缺省 Detailed→现状全量行为，向后兼容）。
- 分级划分草案（apply 时按 §4 验收逐键核对）：

| 级别 | 字段 |
|---|---|
| Basic | `ai.success`, `ai.mode`, `ai.capability`, `action.result`, `action.type`, `entry.name`, `analyze.observed/visited/skipped/pending/end_reached/rule`, `error.reason` |
| Detailed+ | `ai.provider_id`, `ai.model`, `ai.tokens`, `ai.latency_ms`, `ai.item_count`, `ai.retry_count`, `action.adb_ms`, `action.wait_ms`, `entry.node_id/step/depth/rule_id/match_count/ignored_count`, `analyze.p50/p95/cold_start/abnormal_spike`, `error.consecutive_steps` |

**M0 apply 修订**：实际键名全集为 **45 键**（TraceFields 目录，代码权威）。除上表外另有：`poll.verdict/confidence/action/escalated/callback_outcome`（CompletionMonitor）、`error.skipped/error.visited`（ErrorLoopAnalyzer Rule 2）、`entry.parent_node/fingerprint/parent/match_rule/index`（observed/ignored 实发键）。M2 分级须覆盖全部 45 键，poll.*/error.skipped/error.visited 建议归 Detailed+。

### D4: 快照闸门更新

- S4 重冻结：`ai.call` parent 从 null 变为 `engine.step` span id（场景构造注入 provider 或经引擎入口）。
- 新增 S6：完整父链 `engine.run → engine.step → ai.call → ai.analyze`，含重试路径（`ai.retry_count` 断言）。
- S1–S3/S5：**必须 unchanged**——字段键名替换为常量不改变键值（快照不受影响）；分级缺省 Detailed 与现状全量一致。

## Risks / Trade-offs

- **[Risk] provider 接线遗漏** → 某个 PageAnalyzer 实例未注入 provider，ai.* 变回孤儿但无编译错误。Mitigation: S6 断言父链 + S4 断言非引擎入口孤儿形态，双向覆盖。
- **[Risk] 分级过滤引入行为差异** → 缺省级别选错导致快照击穿。Mitigation: 缺省 = Detailed（现状等价），分级测试显式断言"Detailed 与全量一致"。
- **[Risk] TraceFields 常量值漂移** → 常量名对常量值、常量值对 JSONL 字段是双重契约。Mitigation: 目录完整性测试断言值非空 + `layer.` 命名空间；值一经归档不可改（写入 design 约束）。
- **[Risk] 快照重冻结掩盖回归** → S4 重冻结时若 S1–S3/S5 同步漂移则难以归因。Mitigation: apply 顺序先字段键替换（零行为变化，S1–S5 应全绿）→ 再父链（只 S4 变）→ 再分级（缺省零变），每层独立验证。

## Migration Plan

按层落地，每层独立绿（对齐 trace-span-helpers 惯例）：

1. **M0** — `TraceFields` 目录（全键名常量）+ 目录完整性测试；业务代码键名替换为常量（零行为变化，S1–S5 必须全绿——行为等价证明）。
2. **M1** — 父链打通：`ITraceContextProvider` + `TraceCoordinator` 实现 + `PageAnalyzer` 注入 + `ai.call` parent 运行时表达式；S4 重冻结 + 新增 S6。
3. **M2** — 字段分级：`SpanFieldProfile` 描述符 + helper level 参数 + 按级别过滤 + 分级测试（缺省 Detailed 等价证明）。
4. **M3** — 验收矩阵（AC1–AC7 全绿）+ 归档 spec 更新。

回滚：每层独立 revert；M0/M2 为 additive（可留），M1 回滚即恢复孤儿（快照还原）。

## Open Questions

- 无（决策点已确认：保留孤儿、合并三子项、B 形态、`EntryConfig.TraceLevel` 为 level 来源）。

## Acceptance Criteria

### AC1 — 快照闸门
`SpanTreeEquivalenceTests` 全绿：S4 重冻结（`ai.call` parent = `engine.step`）、S6 新增（父链含重试）、**S1–S3 逐字节 unchanged**。**S5 例外（apply 时经用户裁决重冻结）**：并行 change `local-vision-provider` R-12（`MaxEmptyScrollRetries=1` 滚动空差分重试）合法改变引擎滚动轮次，S5 由 53 行重冻结为 70 行（R-12 行为，归因已记录；3 次确定性复现 + 移除 2.7 Set/Reset 对照实验证明与本 change 无关）。M0 键名替换后 S1–S5 曾全绿，先于 M1/M2 验证。

### AC2 — Oracle 零改动
`TraceSpanTests`/`TraceSpanTreeTests`/`HandlerTraceWriterTests`/`InMemoryTraceRecorderTests`/`ArchitectureGuardTests`/`PageAnalyzerTests`/Traversal 7 文件/`SafetyGateTests`/`ErrorLoopAnalyzerTests`/`EnumerateCompletionAnalyzerTests`/`CompletionMonitorTests`/`BaselineTests` 零 diff 且全绿。

### AC3 — 无新脚手架
`grep -rn 'StartSpanAsync\|EndSpanAsync' src/` 命中仅限 `ITraceRecorderExtensions`/`TraceSpanScope`、TraversalEngine passthrough、recorder 实现——本 change 不新增业务命中。

### AC4 — 目录与枚举冻结
`SpanType` enum 11 值（guard `SpanType_Has11Values` 绿）、`TraceFields` 目录完整性测试绿（全 45 键名在目录中、值非空、`layer.` 命名空间）。`SpanTypes` 目录本 change 未增删；apply 时并行 change `local-vision-provider` 新增 4 常量（`ai.yolo/ocr/fusion/scroll`，任务 6.6/10.1），总数 18→22，无 guard 断言计数（ArchitectureGuardTests 零引用），非破坏性——仅记录。

### AC5 — 基线计数
全量 Core/Host 通过数全绿（最终 Core 1083/2、Host 143/7）。归因增量：M0 +4（TraceFieldsTests）、M2 +11（SpanFieldLevelsTests）于 Core，M1 +2（S6、NonEngineEntry）于 Host；其余 Core 1049→1083 差额（+19）来自并行 change `local-vision-provider` 的测试（ScrollLoopTermination 改写、LocalVision 测试新增），非本 change 产物。**新增测试只加不改，oracle 零 diff**。

### AC6 — 分级缺省兼容
缺省（Detailed）级别记录的字段集与 change 前全量记录一致（分级测试显式断言）；Basic 级别记录核心字段、不记录 Detailed+ 字段。

### AC7 — 父链双向覆盖
S6（引擎入口 → `ai.call` parent = `engine.step`）与 S4/新增测试（非引擎入口 → `ai.call` 仍为根，保留孤儿）同时绿。
