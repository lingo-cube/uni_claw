# 异步化 + 滑动坐标可配置 重构设计

> 日期: 2026-07-15
> 状态: Draft (待评审)
> 分支: feature/refactor
> 相关代码: `src/UniClaw.Core/StateMachine/TraversalFSM.cs`、`src/UniClaw.Core/Traversal/StepOrchestrator.cs`、`src/UniClaw.Core/Traversal/TraversalEngine.cs`、`src/UniClaw.Core/Simulation/Scroll/`
> 相关文档: `docs/system/layers/traversal.md`、`docs/system/constitution/constraints.md`、`docs/refactor/2026-07-14-scroll-as-action-refactor-design.md`

---

## 1. 背景与问题

基于 2026-07-14 审计结论（架构正确，但距真机运行还需补齐），本次重构解决其中两个问题：

### 问题 1: 同步阻塞异步（15 站点，3 文件）

遍历管道的热路径全部同步但内部调异步 I/O：

```
TraversalEngine.RunAsync() [async]
  → _orchestrator.ExecuteStep() [sync]
    → FSM.Step() [sync]
      → HandleExecute() [sync] → DispatchAsync().GetAwaiter().GetResult()
      → HandleResultVerify() [sync] → AnalyzeCurrentPageAsync().GetAwaiter().GetResult()
    → TryHandleScroll() [sync] → SwipeAsync().GetAwaiter().GetResult()
```

24 处 `.GetAwaiter().GetResult()` 分布在 3 个文件。真机 ADB 截图需要 0.5-5s，同步阻塞会死锁线程池。

根因：所有 I/O 接口（`IActionExecutor`、`IVisionProvider`）初始设计就是 async（`Task<bool>`、`Task<PageAnalysis?>`），但上层的 FSM 和 Orchestrator 没有跟上，全部是同步方法。

### 问题 2: 滑动坐标硬编码

`StepOrchestrator.cs:284-288` 五个 `const` 值：始终从 `(0.5, 0.7)` 滑到 `(0.5, 0.3)`，300ms。不同 App 的滚动区域位置/大小/方向完全不同，硬编码在真机上会导致滑不到滚动区域。

根因：坐标是 v1 默认值，注释明确标注"滚动区域坐标未来可从 PageAnalysis 精确推导（见设计 §13 延后项）"。现在需要把这个延后项补上，但用更务实的方案——可配置参数，而非 AI 推导。

## 2. 核心洞察

**异步化**：不需要引入新机制。`IActionExecutor` 和 `IVisionProvider` 的所有方法已经是 `Task` 返回。只需让上层 FSM / Orchestrator 穿透 async/await，消除中间层的 `.GetAwaiter().GetResult()`。改动是机械性的签名变更，零逻辑变更。

**滑动坐标**：不需要 AI 推导。95% 的场景用默认值就够了。把 `const` 提升为 `record class` 配置对象，引擎级默认 + 页面级覆盖（通过 `IVisionProvider` 暴露），mock 和真机都能按需适配。

## 3. 目标与成功标准

- 消除全部 24 处 `.GetAwaiter().GetResult()`，热路径全链路 async/await
- 接口 `IGraphTraversalEngine` / `IActionExecutor` / `IVisionProvider` 签名不变
- 滑动坐标从 `const` 变为可配置，默认值保持 `(0.5, 0.7) → (0.5, 0.3), 300ms`，零行为变更
- Mock 能按页面适配不同滚动区域配置
- 所有现有测试通过，~50 测试签名从 `void` → `async Task`
- 宪章约束 C-4（FSM 独立性）不受影响——只改签名不改状态空间
- 现有 baseline 测试的 `NumericAnchor` 值不变（行为等价）

## 4. Part 1: 异步化设计

### 4.1 调用链改动

```
TraversalEngine.RunAsync()
  │  await _vision.AnalyzeCurrentPageAsync(ct)   ← 已有 await
  │  await _orchestrator.ExecuteStepAsync()       ← 改: 原来同步
  │
  └─ StepOrchestrator.ExecuteStepAsync()
       │  await ctx.StateMachine.StepAsync(ctx)   ← 改: 原来同步
       │  await ctx.Action.PressBackAsync()       ← 改: 原来是 .GetAwaiter()
       │  await TryHandleScrollAsync()             ← 改: 原来同步
       │
       └─ TraversalFSM.StepAsync()
            │  await DispatchHandlerAsync()         ← 改: 原来同步
            │
            ├─ HandleExecuteAsync()                ← 改: await DispatchAsync ×2
            ├─ HandleResultVerifyAsync()           ← 改: await AnalyzeCurrentPageAsync ×2
            ├─ HandleNodeSelectAsync()             ← 签名改, 逻辑不变
            ├─ HandlePreconditionCheckAsync()      ← 签名改, 逻辑不变
            ├─ HandleBranchAsync()                 ← 签名改, 逻辑不变
            ├─ HandleFrameCompleteAsync()          ← 签名改, 逻辑不变
            ├─ HandleErrorHandlingAsync()          ← 签名改, 逻辑不变
            └─ HandlePopupHandlingAsync()          ← 签名改, 逻辑不变
```

实际有 await 的只有 `HandleExecuteAsync` (DispatchAsync) 和 `HandleResultVerifyAsync` (AnalyzeCurrentPageAsync) 以及 `TryHandleScrollAsync` (SwipeAsync + AnalyzeCurrentPageAsync)。其他 6 个 Handler 和 `TryHandleNavigation` 是纯同步逻辑，只改返回类型为 `Task<TraversalState>`。

### 4.2 TraversalFSM.Step() → StepAsync()

```csharp
// 之前
public TraversalState Step(StepContext? ctx)
{
    var fromState = CurrentState;
    TraversalState nextState;
    try
    {
        _currentStepContext = ctx;
        nextState = DispatchHandler(fromState);
    }
    catch (Exception ex)
    {
        RuntimeContext.SetLastError(ex);
        RuntimeContext.IncrementConsecutiveErrors();
        nextState = TraversalState.ErrorHandling;
    }
    finally { _currentStepContext = null; }
    TransitionTo(nextState);
    return nextState;
}

// 之后
public async Task<TraversalState> StepAsync(StepContext? ctx)
{
    var fromState = CurrentState;
    TraversalState nextState;
    try
    {
        _currentStepContext = ctx;
        nextState = await DispatchHandlerAsync(fromState);
    }
    catch (Exception ex)
    {
        RuntimeContext.SetLastError(ex);
        RuntimeContext.IncrementConsecutiveErrors();
        nextState = TraversalState.ErrorHandling;
    }
    finally { _currentStepContext = null; }
    TransitionTo(nextState);
    return nextState;
}
```

### 4.3 8 Handler → async

```csharp
// Dispatch 表
private async Task<TraversalState> DispatchHandlerAsync(TraversalState fromState)
{
    return fromState switch
    {
        TraversalState.NodeSelect => await HandleNodeSelectAsync(),
        TraversalState.PreconditionCheck => await HandlePreconditionCheckAsync(),
        TraversalState.Execute => await HandleExecuteAsync(),
        TraversalState.ResultVerify => await HandleResultVerifyAsync(),
        TraversalState.Branch => await HandleBranchAsync(),
        TraversalState.FrameComplete => await HandleFrameCompleteAsync(),
        TraversalState.ErrorHandling => await HandleErrorHandlingAsync(),
        TraversalState.PopupHandling => await HandlePopupHandlingAsync(),
        _ => TraversalState.ErrorHandling
    };
}
```

`HandleExecuteAsync` 和 `HandleResultVerifyAsync` 内部把 `.GetAwaiter().GetResult()` 换为 `await`。其余 6 个 Handler 只在方法签名加 `async Task<TraversalState>`，内部逻辑不变。

### 4.4 StepOrchestrator

```csharp
// 之前
public StepResult ExecuteStep(StepContext ctx) { ... }
internal static bool TryHandleScroll(StepContext ctx, ...) { ... }

// 之后
public async Task<StepResult> ExecuteStepAsync(StepContext ctx) { ... }
internal static async Task<bool> TryHandleScrollAsync(StepContext ctx, ...) { ... }
```

`TryHandleNavigation` 保持同步——纯指纹比较 + 栈操作，无 I/O。

### 4.5 TraversalEngine

- `Run()` 同步包装器 → **删除**
- `RunAsync()` 第 195 行：`_orchestrator.ExecuteStep()` → `await _orchestrator.ExecuteStepAsync()`

### 4.6 TraceCoordinator

`LogAndContinue(Action)` → `LogAndContinue(Func<Task>)`:

```csharp
// 之前
private void LogAndContinue(Action action)
{
    if (!Active) return;
    try { action(); }
    catch (Exception ex) { Console.WriteLine($"[TraceCoordinator Warning] ..."); }
}

// 之后
private async Task LogAndContinueAsync(Func<Task> func)
{
    if (!Active) return;
    try { await func(); }
    catch (Exception ex) { Console.WriteLine($"[TraceCoordinator Warning] ..."); }
}
```

15 个 `Record*` 方法改为 `async Task`，内部的 `_recorder.Record*Async(...).GetAwaiter().GetResult()` 换为 `await`。调用方在 `ExecuteStepAsync` 中自然 `await` 无需额外适配。

### 4.7 不改的部分

- `OperationDispatcher.DispatchAsync` — 已经是正确 async，不变
- `IActionExecutor` / `IVisionProvider` / `IGraphTraversalEngine` — 接口签名不变
- C-4（FSM 独立性）— 只改返回类型，状态空间和迁移矩阵不变

## 5. Part 2: 滑动坐标可配置设计

### 5.1 新增类型

```csharp
// 新文件: src/UniClaw.Core/Traversal/ScrollSwipeConfig.cs
namespace UniClaw.Core.Traversal;

/// <summary>
/// 滚动滑动配置 — 归一化坐标 (0-1) + 持续时间。
/// 默认值 = 当前硬编码常量 (0.5, 0.7) → (0.5, 0.3), 300ms。
/// </summary>
public sealed record class ScrollSwipeConfig(
    double StartX = 0.5,
    double StartY = 0.7,
    double EndX = 0.5,
    double EndY = 0.3,
    int DurationMs = 300);
```

### 5.2 配置层级

```
引擎级默认 (TraversalEngineConfig.ScrollSwipe)
  → 透传到 StepContext.ScrollSwipe  ← TryHandleScrollAsync 的回退值
  → IVisionProvider.GetScrollSwipeConfig()?  ← 页面级覆盖（优先）
```

`TryHandleScrollAsync` 合并逻辑：

```csharp
var cfg = ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe;
await ctx.Action.SwipeAsync(cfg.StartX, cfg.StartY, cfg.EndX, cfg.EndY, cfg.DurationMs);
```

### 5.3 IVisionProvider 加 virtual 方法

```csharp
// IVisionProvider (StepContext.cs) — 不改接口契约，加 virtual 默认实现
virtual ScrollSwipeConfig? GetScrollSwipeConfig() => null;
```

默认返回 null，表示"用引擎配置"。Mock 或真机实现按需覆写。

### 5.4 TraversalEngineConfig

```csharp
public sealed record class TraversalEngineConfig
{
    public int MaxSteps { get; init; } = 1000;
    public int MaxDepth { get; init; } = 10;
    public bool ThrowOnError { get; init; } = false;
    public bool TraceEnabled { get; init; } = true;
    public int DelayPerStepMs { get; init; } = 0;
    public ScrollSwipeConfig ScrollSwipe { get; init; } = new();  // ← 新增
}
```

### 5.5 StepContext 透传

```csharp
public sealed record class StepContext(
    TraversalRuntimeContext Context,
    TraversalFSM StateMachine,
    IVisionProvider Vision,
    IActionExecutor Action,
    IDynamicChildManager ChildMgr,
    INodeRegistry NodeRegistry,
    ITraceCoordinator Trace,
    IPageSnapshotManager SnapshotMgr,
    INodeStackAdapter Stack,
    ErrorHandler? ErrorHandler = null,
    PopupHandler? PopupHandler = null,
    string? LastKnownPath = null,
    string? LastRecordedPath = null,
    string? LastRecordedAction = null,
    ScrollSwipeConfig ScrollSwipe = null!);  // ← 新增, TraversalEngine.RunAsync 构造时填充
```

### 5.6 StepOrchestrator.TryHandleScrollAsync

```csharp
// 之前 — 5 个 const 硬编码
private const double ScrollSwipeStartX = 0.5;
// ... (删除全部 5 个 const)

ctx.Action.SwipeAsync(
    ScrollSwipeStartX, ScrollSwipeStartY,
    ScrollSwipeEndX, ScrollSwipeEndY,
    ScrollSwipeDurationMs).GetAwaiter().GetResult();

// 之后 — 读配置
var cfg = ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe;
await ctx.Action.SwipeAsync(cfg.StartX, cfg.StartY, cfg.EndX, cfg.EndY, cfg.DurationMs);
```

## 6. Mock 适配

### 6.1 SimulatedScreen

```csharp
// 新增
private readonly Dictionary<string, ScrollSwipeConfig> _scrollSwipeConfigs = new();

public ScrollSwipeConfig? GetScrollSwipeConfig(string pageId)
    => _scrollSwipeConfigs.TryGetValue(pageId, out var cfg) ? cfg : null;

// WithScrollablePage 加可选参数
public SimulatedScreen WithScrollablePage(
    string pageId,
    PagedItemGenerator source,
    ScrollSwipeConfig? scrollSwipe = null)
{
    _scrollablePages[pageId] = new ScrollablePageState(source, ...);
    if (scrollSwipe != null)
        _scrollSwipeConfigs[pageId] = scrollSwipe;
    return this;
}
```

### 6.2 ScrollableMockVisionService

```csharp
// 覆写 virtual 方法
public override ScrollSwipeConfig? GetScrollSwipeConfig()
    => _screen.GetScrollSwipeConfig(_screen.CurrentPageId);
```

### 6.3 测试用法示例

```csharp
// 默认场景 — 不传配置，走引擎默认 (0.5, 0.7) → (0.5, 0.3)
var screen = new SimulatedScreen(fixture)
    .WithScrollablePage("network_list", new PagedItemGenerator(25, 5));

// 底部弹窗列表 — 页面级覆盖
var screen = new SimulatedScreen(fixture)
    .WithScrollablePage("bottom_sheet_list", new PagedItemGenerator(20, 5),
        scrollSwipe: new ScrollSwipeConfig(
            StartX: 0.5, StartY: 0.85,
            EndX: 0.5, EndY: 0.55,
            DurationMs: 200));
```

## 7. 测试影响

### 7.1 签名变更（~50 测试方法）

```csharp
// 之前
[Fact]
public void SomeTest()
{
    var result = engine.Run();
    Assert.True(result.Success);
}

// 之后
[Fact]
public async Task SomeTest()
{
    var result = await engine.RunAsync();
    Assert.True(result.Success);
}
```

### 7.2 行为不变验证

- `TraversalEngine.Run()` 删除 → 所有调用方改为 `await engine.RunAsync()`
- 默认 `ScrollSwipeConfig` 值 = 当前硬编码值 → 行为零差异
- Baseline 测试 `NumericAnchor` 不重新标定
- 所有 575 现有测试通过

## 8. 不改的部分

- 不新增 enum 值（宪章 C-1/C-2/C-7/C-8）
- 不改变 FSM 迁移矩阵（宪章 C-4）
- 不修改 `IActionExecutor` / `IVisionProvider` / `IGraphTraversalEngine` 接口契约
- 不修改 `PageAnalysis` / Domain 层（宪章 C-3）
- `TryHandleNavigation` 保持同步——无 I/O，纯指纹比较
- `ChildrenStrategy` 不加 `ScrollSwipeConfig` 字段——节点级覆盖延后到真机需要时再加

## 9. 真机接入影响

异步化后，真机实现的 `IActionExecutor` 和 `IVisionProvider` 可以真正 async，不再被上层同步阻塞：

```
Mock:   TapAsync(0ms) → AnalyzeCurrentPageAsync(0ms) → 即刻下一步
Real:   await TapAsync(50-200ms) → await AnalyzeCurrentPageAsync(500ms-5s) → 不阻塞线程池
```

滑动坐标可配置后，真机设备只需要在构造时传 `ScrollSwipeConfig`，引擎不需要知道坐标来源。
