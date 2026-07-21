# Trace Collection Completion — C-8/C-9/C-10 联合设计

> Date: 2026-07-21
> Status: Draft
> 位置: 填补 C-7 (JSONL 存储 27/27) 后的采集端空洞

## 1. Summary

C-7 完成 JSONL 存储后端，但生产端只有 ~50% 组件往管道写数据。当前 P1 空洞: ContainerHandler 零 trace、PageTransition 从未调用、3 SpanType 未激活 (PopupHandling/DfsBacktrack/CacheOp)、GlobalFSM Context=null。P2 空洞: DurationMs 永远 0、PageId 永远空、Metadata 永远不填。

3 份设计文档按用途分组:

| 变更 | 用途 | 估计 |
|------|------|------|
| **C-9**: Handler Lifecycle Trace | 优化分析 — handler 频率/类型/完成模式 | 3 天, 9 tests, 5 files |
| **C-10**: Operation Timing Trace | 性能优化 — 步骤耗时/DFS回溯/AI延迟 | 2 天, 12 tests, 4 files |
| **C-8**: State Flow Trace | 重放 — 双 FSM + 页面导航 + 页面级索引 | 2 天, 9 tests, 3 files |

**实施顺序**: C-9 → C-10 → C-8

## 2. 新增类型

### 接口: IHandlerTraceWriter

```csharp
namespace UniClaw.Core.Observability;

/// <summary>
/// Handler 生命周期 trace 接口 — ISP 分离，与 ITraceCoordinator 独立。
/// 供 PopupHandler/ContainerHandler/ErrorHandler 和 DfsBacktrack 插入点使用。
/// </summary>
public interface IHandlerTraceWriter
{
    /// <summary>
    /// 记录 handler 生命周期事件，底层委托 ITraceRecorder.RecordExecutionAsync。
    /// action: 描述字符串 ("handle_popup", "dfs_backtrack", 等)
    /// spanType: 区分 handler 类型 (PopupHandling/ContainerHandling/ErrorHandling/DfsBacktrack)
    /// metadata: handler 特有字段，见各 handler 映射表
    /// </summary>
    Task RecordHandlerLifecycleAsync(
        string action,
        SpanType spanType,
        Dictionary<string, object>? metadata = null);
}
```

- ISP 分离于 ITraceCoordinator (18 方法已偏大)
- 命名空间: `UniClaw.Core.Observability` (D-17 方向, Observability 被 SM+Traversal 消费)
- 实现: `HandlerTraceWriter` 委托 `ITraceRecorder.RecordExecutionAsync`

### Attribute: TraceHandlerAttribute

```csharp
namespace UniClaw.Core.Observability;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceHandlerAttribute : Attribute
{
    public SpanType SpanType { get; }
    public string Action { get; }
    public TraceHandlerAttribute(SpanType spanType, string action)
    {
        SpanType = spanType;
        Action = action;
    }
}
```

- C-9 阶段: 只作文档化标注，不跑逻辑
- Phase 3+: 源生成器扫描 `[TraceHandler]` 自动注入 trace 调用

### 辅助类: TraceMetadata

```csharp
namespace UniClaw.Core.Observability;

/// <summary>
/// 构建 handler metadata Dictionary 的链式辅助。
/// 统一 null skip、enum → string、key 拼写一致性。
/// </summary>
public static class TraceMetadata
{
    public static Builder Build() => new();

    public sealed class Builder
    {
        private readonly Dictionary<string, object> _dict = new();

        public Builder Add(string key, object? value)
        {
            if (value != null) _dict[key] = value;
            return this;
        }
        public Builder Add(string key, string? value)
        {
            if (value != null) _dict[key] = value;
            return this;
        }
        public Builder Add<T>(string key, T? value) where T : struct, Enum
        {
            if (value.HasValue) _dict[key] = value.Value.ToString();
            return this;
        }
        public Dictionary<string, object> ToDict() => _dict;
    }
}
```

### 记录扩展: AICallRecord.Metadata

```csharp
// 当前 (3 构造参数 + 3 optional)
public sealed record class AICallRecord(
    string Capability,
    string ProviderId,
    bool Success,
    double LatencyMs,
    TraceContext? Context = null,
    int? Tokens = null,
    Dictionary<string, object>? Metadata = null,   // ← 新增, 默认 null
    DateTimeOffset Timestamp = default);
```

- Phase 3-A future-ready: `["adb_operation"]="tap"`, `["adb_latency_ms"]=150`, `["chain_step"]="screenshot"`
- 零行为变化: default=null, 现有调用方无需改

## 3. C-9: Handler Lifecycle Trace

### 数据流

```
PopupHandler.HandlePopup
  → [TraceHandler] (标注, 不运行)
  → HandlerTraceWriter.RecordHandlerLifecycleAsync("handle_popup", SpanType.PopupHandling, metadata)
    → ITraceRecorder.RecordExecutionAsync(ExecutionRecord{ SpanType=PopupHandling, Metadata=... })
```

### Handler 映射表

| Handler | SpanType | metadata 字段 | 源头 |
|---------|----------|---------------|------|
| PopupHandler | PopupHandling | `popup_type`, `dismiss_strategy`, `dismiss_target`, `urgency`, `blocking_type`, `handling_success`, `handling_action` | PopupClassification + PopupHandlingResult |
| ContainerHandler | ContainerHandling | `completion_reason`, `fallback_action`, `container_success`, `elapsed_ms`, `depth`, `total_children`, `visited_child_count` | CompletionContext + ContainerActionResult |
| ErrorHandler | ErrorHandling | `classified_error_type`, `strategy`, `outcome`, `backoff_delay_seconds`, `consecutive_errors`, `can_backtrack`, `can_skip`, `stack_depth`, `error_policy` | ErrorClassificationContext + ErrorRecoveryResult |

### 现有 trace 替换

| Handler | 当前 | 目标 |
|---------|------|------|
| PopupHandler | 2 calls (StateTransition + Decision) | 1 call RecordHandlerLifecycleAsync |
| ContainerHandler | 0 calls | 1 call RecordHandlerLifecycleAsync |
| ErrorHandler | 2 calls (Decision + ErrorSpan) | 1 call RecordHandlerLifecycleAsync + **保留** RecordErrorSpanAsync |

ErrorHandler 保留两处是因为: RecordErrorSpanAsync 写的是 ErrorRecord (独立的 record 类型)，而 RecordHandlerLifecycleAsync 写的是 ExecutionRecord — 两者正交。

### Trace 注入点: 编排层, 非 handler 内部

`IHandlerTraceWriter` 不在 handler 内部调用 (handler 是纯管道原则)。注入点位于**编排层**:

```
StepOrchestrator (或 InterceptionHandler)
  → 调用 handler (如 HandleContainer)
  → handler 返回扩展后的 result (含 CompletionReason, TotalChildren 等)
  → 编排层从 result 提取 metadata
  → 调用 IHandlerTraceWriter.RecordHandlerLifecycleAsync
```

handler 自身不感知 trace，只需返回扩展后的 result 类型。编排层做 metadata 提取 + trace 写入。

### DecideFrameCompletion async 签名

```csharp
// 当前 (sync)
private (bool frameCompleted, bool childPushed, TraversalState nextState) DecideFrameCompletion(
    StepContext ctx, ITraversalNode currentFrame, bool canContinue);

// 目标 (async)
private async Task<(bool frameCompleted, bool childPushed, TraversalState nextState)> DecideFrameCompletionAsync(
    StepContext ctx, ITraversalNode currentFrame, bool canContinue);
```

波及调用方:
- `OnDynamicMatchNodeSelect` (已 async) → 改 await
- `OnFrameComplete` (当前 sync) → 变 async → StepOrchestrator 调用方改 await

### 必要的签名变更

| 变更 | 原因 | 影响 |
|------|------|------|
| `DecideFrameCompletion` sync → async | 内部需 await RecordHandlerLifecycleAsync | `OnFrameComplete` 同步变 async, StepOrchestrator 改 await |
| `OnFrameComplete` sync → async | 同上 | 调用方改 await |
| ContainerHandler 构造路径传入 IHandlerTraceWriter | Decouple handler 与 trace | StepContext 加 #18 |

### PopupHandlingResult 扩展

```csharp
// 当前
public sealed record class PopupHandlingResult(bool Success, string Action, string Description);

// 目标: 加可选分类字段 (向后兼容, default null)
public sealed record class PopupHandlingResult(
    bool Success,
    string Action,
    string Description,
    PopupClassification? Classification = null);  // ← 新增
```

### ContainerActionResult 扩展

```csharp
// 当前
public sealed record class ContainerActionResult(FallbackAction Action, bool Success, string Description);

// 目标: 加完成指标 (向后兼容)
public sealed record class ContainerActionResult(
    FallbackAction Action,
    bool Success,
    string Description,
    CompletionReason? CompletionReason = null,
    int? TotalChildren = null,
    int? VisitedChildCount = null,
    int? Depth = null);
```

## 4. C-10: Operation Timing Trace

### DurationMs — TraceCoordinator 自有 Stopwatch

```csharp
// TraceCoordinator 新增字段
private readonly Stopwatch _stepStopwatch = new();

// RecordStepStartAsync 中
_stepStopwatch.Restart();

// RecordStepEndAsync 中
_stepStopwatch.Stop();
// DurationMs = _stepStopwatch.Elapsed.TotalMilliseconds
```

### DfsBacktrack — 3 个插入点

| backtrack_reason | 插入点 | 文件 | 条件 |
|-----------------|--------|------|------|
| `leaf_execution_complete` | Leaf → Pop 后 | TraversalEngine.RunAsync (行 254-258) | stepResult.NextState==ResultVerify && Depth>1 && ChildrenStrategy=None |
| `pop_only_parent_frame_matches` | Pop-only 分支 | InterceptionHandler.OnDynamicMatchNodeSelect | 指纹匹配, Pop-only |
| `press_back_parent_frame_differs` | PressBack+Pop 分支 | InterceptionHandler.OnDynamicMatchNodeSelect | 指纹不匹配, PressBack+Pop |

### AICallRecord 扩展

```csharp
// RecordAICallSpanAsync 加可选 metadata 参数
Task RecordAICallSpanAsync(
    string capability,
    string providerId,
    bool success,
    double latencyMs,
    int? tokens = null,
    Dictionary<string, object>? metadata = null);   // ← 新增
```

## 5. C-8: State Flow Trace

### GlobalFSM Context 修复

当前 `RegisterGlobalFsmTraceCallbacks` 传 `Context: null`。目标: closure 捕获 engine 上下文:

```csharp
// RegisterGlobalFsmTraceCallbacks 内部
_ = _traceRecorder.RecordTransitionAsync(new StateTransition(
    FromState: args.FromState.ToString(),
    ToState: args.ToState.ToString(),
    Context: new TraceContext(
        NodeId: _ctx.CurrentFrame?.NodeId,
        StepSpanId: null,               // 事件在步骤循环间发生
        StepNumber: _ctx.StepCount,
        TraceId: _ctx.TraceId),
    FsmType: "GlobalFSM",
    Timestamp: args.Timestamp,
    Reason: args.Reason));
```

- StepSpanId=null: 事件在步骤循环间发生，不属于任何 step span
- ForceState 不触发回调 — 恢复路径不产生 trace (已有正确语义)

### GlobalFSM 状态覆盖扩展

当前注册 4 states: `Completed, Error, Traversing, Idle`。扩展为全部 8 states:

```csharp
foreach (var state in Enum.GetValues<GlobalState>())
{
    // 排除 terminal states (无出转换)
    if (state is GlobalState.Completed or GlobalState.Terminated) continue;
    fsm.RegisterStateCallback(state, args => { ... });
}
```

### PageTransition — 2 个插入点

**插入点 (a):** RunAsync 循环替换启发式比较

```csharp
// RunAsync for 循环内, 旧的 TraceRecord 启发式替换为正式调用
var currentPageId = GetCurrentPageId();
if (lastPageId != null && lastPageId != currentPageId)
{
    await _trace.RecordPageTransitionAsync(lastPageId, currentPageId, "navigation");
}
```

**插入点 (b):** InterceptionHandler PressBack+Pop

```csharp
// OnDynamicMatchNodeSelect -> PressBack+Pop 分支 (指纹不匹配)
var parentNodeId = _ctx.NodeStack.Peek()?.Node?.NodeId;
if (parentNodeId != null)
{
    await _trace.RecordPageTransitionAsync(currentFrame.NodeId, parentNodeId, "press_back");
}
```

### PageId 填充

8 个 ITraceCoordinator Record 方法中，只在 ExecutionRecord 相关方法填充 PageId:

```csharp
// RecordActionExecutionAsync (typed overload)
var pageId = _ctx?.CurrentFrame?.NodeId;
// ... 构建 ExecutionRecord 时设置 PageId = pageId
```

StateTransition/ErrorRecord/PageTransition/AICallRecord → 不加 PageId (TraceContext 4 字段规则)。

## 6. 文件变更清单

| 文件 | C-8 | C-9 | C-10 | 说明 |
|------|-----|-----|------|------|
| `Observability/IHandlerTraceWriter.cs` | | ✅ | | 新接口 |
| `Observability/HandlerTraceWriter.cs` | | ✅ | | 新实现 |
| `Observability/ITraceCoordinator.cs` | | | ✅ | RecordAICallSpanAsync + metadata 参数 |
| `Observability/TraceCoordinator.cs` | ✅ | | ✅ | Stopwatch + PageId + GlobalFSM Context |
| `Observability/ITraceRecorder.cs` | | | ✅ | AICallRecord.Metadata |
| `Observability/TraceMetadata.cs` | | ✅ | ✅ | 新辅助类 |
| `Observability/TraceHandlerAttribute.cs` | | ✅ | | 新 Attribute |
| `Traversal/TraversalEngine.cs` | ✅ | | ✅ | GlobalFSM callbacks + RunAsync PageTransition + DfsBacktrack |
| `Traversal/InterceptionHandler.cs` | ✅ | ✅ | ✅ | PageTransition(b) + DecideFrameCompletion async + DfsBacktrack |
| `StateMachine/PopupHandler.cs` | | ✅ | | PopupHandlingResult 扩展 + trace 注入 |
| `StateMachine/ContainerHandler.cs` | | ✅ | | ContainerActionResult 扩展 + trace 注入 |
| `StateMachine/ErrorHandler.cs` | | ✅ | | ErrorHandler trace + ErrorRecoveryResult |
| `StateMachine/StepContext.cs` | | ✅ | | 可选的 IHandlerTraceWriter |

## 7. 测试策略

### C-9 测试 (9 tests)

| 测试 | 覆盖 |
|------|------|
| PopupHandler lifecycle trace | metadata 字段正确 |
| ContainerHandler lifecycle trace | completion_reason, total_children 等 |
| ErrorHandler lifecycle trace | 双写 (RecordHandlerLifecycleAsync + RecordErrorSpanAsync) |
| DecideFrameCompletionAsync | sync→async 不改变行为 |
| OnFrameComplete async | async 调用链 |
| TraceMetadata.Build | 链式 API 正确, null skip |
| TraceHandlerAttribute | 属性值正确 |
| PopupHandlingResult 扩展 | Classification 向后兼容 |
| ContainerActionResult 扩展 | CompletionReason 向后兼容 |

### C-10 测试 (12 tests)

| 测试 | 覆盖 |
|------|------|
| RecordStepStart/End DurationMs | Stopwatch 精度, 非零 |
| DfsBacktrack leaf_execution_complete | Pop 后 trace 触发 |
| DfsBacktrack pop_only | 指纹匹配分支 |
| DfsBacktrack press_back | 指纹不匹配分支 |
| AICallRecord.Metadata | 新增字段 round-trip |
| RecordAICallSpanAsync metadata 参数 | 传递正确 |
| AICallRecord 现有调用方 | 零行为变化 (default null) |

### C-8 测试 (9 tests)

| 测试 | 覆盖 |
|------|------|
| GlobalFSM Context | 非 null, NodeId/StepNumber 正确 |
| GlobalFSM 全状态覆盖 | 8 states 全部注册 |
| PageTransition (a) RunAsync | 页面指纹变化触发 |
| PageTransition (b) PressBack | PressBack+Pop 触发 |
| PageId 填充 | ExecutionRecord.PageId 非空 |
| PageId 边界 | TransitionRecord/ErrorRecord 不加 PageId |
| ForceState 不 trace | 恢复路径无记录 |
| 回归: 在 full traversal baseline 确认新 SpanType 出现 | sim trace.jsonl 含 PopupHandling/ContainerHandling/DfsBacktrack |
| ArchitectureGuard: SpanType 11 值锁定 | 未变 |

## 8. 不在范围内

- ❌ TraceContext VisitSpanId/ParentSpanId (Phase 3 通用关联扩展)
- ❌ CacheOp SpanType 激活 (无连接组件)
- ❌ Handler 内部步骤级 trace (detect→classify→preserve→handle→restore→validate 太细)
- ❌ Handler 直接注入 ITraceCoordinator (违反 handler 纯管道原则)
- ❌ ADB/Vision 操作计时 (Phase 3-A, 通过 AICallRecord.Metadata 预留)
- ❌ SpanType enum 值增删 (11 值锁定, 需 constitution change flow)
- ❌ Replay 执行器组件 (Phase 3 功能)
