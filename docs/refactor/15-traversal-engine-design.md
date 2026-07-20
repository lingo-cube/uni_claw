# TraversalEngine — 统一遍历引擎设计（修订版）

> 将 PlanCompiler、StepOrchestrator、SimulationRunner 整合为单一入口，
> 对齐 Python `GraphTraversalEngine`。仿真和真实执行共用同一接口。
> 修订日期: 2026-07-06
> 原始设计: 2026-07-05 (docs/refactor/15-traversal-engine-design.md)

---

## 1. 现状问题

当前遍历相关组件分散在 3 个 namespace，调用方需要理解 5+ 个类型才能运行：

```csharp
// 当前: 调用方需要自己拼装
var fixture = ...;
var registry = new SimpleNodeRegistry();
registry.Register(leaf1);
registry.Register(leaf2);
var root = new TraversalNode(...);
var runner = new SimulationRunner(fixture, root, registry);
var result = runner.Run();
```

而 Python 只需要：

```python
engine = GraphTraversalEngine(plan, vision_service=v, action_executor=a)
result = engine.run()
```

### 已存在的组件

| 组件 | 文件 | 状态 |
|------|------|------|
| `TraversalPlan` | `Graph/Models/TraversalPlan.cs` | ✅ 存在 |
| `PlanCompiler` | `Graph/Models/PlanCompiler.cs` | ✅ 存在（IntentSlots→Plan） |
| `TraversalFSM` | `StateMachine/TraversalFSM.cs` | ✅ 存在 |
| `TraversalRuntimeContext` | `StateMachine/TraversalRuntimeContext.cs` | ✅ 存在 |
| `StepOrchestrator` | `Traversal/StepOrchestrator.cs` | ✅ 存在 |
| `TraversalEngine` helpers | `Traversal/TraversalEngine.cs` | ⚠️ 只有子组件（DynamicChildManager 等），无统一入口 |
| `SimulationRunner` | `Simulation/SimulationRunner.cs` | ✅ 存在（仿真专用，不含 Plan 编译） |
| `IVisionProvider` | `StateMachine/StepContext.cs` | ✅ 存在 |
| `IActionExecutor` | `Traversal/IGraphTraversalEngine.cs` | ✅ 存在 |
| `IGraphTraversalEngine` (完整版) | `Traversal/IGraphTraversalEngine.cs` | ✅ 存在（8 成员 async 接口） |
| `IGraphTraversalEngine` (空 stub) | `StateMachine/TraversalState.cs:152-155` | ⚠️ 空接口，避免循环依赖的临时方案 → **D-14 待清理** |
| `TraceCoordinator` | `Traversal/TraversalEngine.cs` | ✅ 存在 |
| `ITraceRecorder` | `Observability/ITraceRecorder.cs` | ✅ 存在 |

### 关键缺失

1. **无统一入口** — 没有像 Python `GraphTraversalEngine` 的类
2. **Plan→node-tree 编译** — `PlanCompiler` 只做 IntentSlots→Plan，不做 Plan→节点树展开
3. **Trace 输出** — Runner 不记录结构化 trace（SimulationResult 没有 Trace 字段）
4. **仿真 vs 生产** — 当前 Runner 只能仿真（mock 服务内部创建）
5. **IGraphTraversalEngine 双定义** — 空 stub (StateMachine) + 完整版 (Traversal)，导致 HasUnvisitedChildren 无法接收引擎实例

---

## 2. 目标

```
TraversalEngine(plan, vision, action, config?, traceRecorder?)
  │
  ├── 实现 IGraphTraversalEngine (Traversal namespace)
  ├── 构造器完成初始化（编译 Plan → 节点树 + 注册 + 组装内部组件）
  ├── RunAsync() 核心循环 — StepOrchestrator.ExecuteStep() + trace + 协调 GlobalFSM
  ├── Run() 同步便利包装 — 仿真测试用
  └── 返回 TraversalResult (统一 Result 类型)
```

### Goals

- **单一入口** — `new TraversalEngine(plan, vision, action).Run()` 或 `.RunAsync()`
- **Plan 驱动** — 输入 `TraversalPlan`，自动编译节点树并注册
- **仿真/生产统一** — 注入 mock → 仿真；注入真实 → 生产。引擎不变
- **Trace 记录** — 输出完整 trace（FSM 状态序列、操作、页面变化）
- **SimulationRunner 合并** — Runner 不再作为独立 public API，逻辑迁移入 TraversalEngine
- **IGraphTraversalEngine stub 清理** — 删除 StateMachine 空 stub (D-14 解决)
- **Result/Config 统一** — SimulationResult → TraversalResult, SimulationConfig → TraversalEngineConfig

### Non-Goals

- 不改变 `TraversalFSM` / `StepOrchestrator` / `TraversalRuntimeContext` 的内部实现
- 不改变 `IVisionProvider` / `IActionExecutor` 接口定义
- 不实现 `BehaviorValidator` / `ProblemDetector`
- 不实现 Scroll 仿真
- 不实现 GlobalFSM 具体类（Phase 3）
- 不实现 PauseAsync/ResumeAsync 的完整逻辑（Phase 3 stub → P4-B2 已完成: TaskCompletionSource gate + 前置校验 + B1 hook）

---

## 3. 类型定义

### 3.1 TraversalResult

**替换旧版** `Traversal/IGraphTraversalEngine.cs` 中的 `TraversalResult`。
旧版使用 `HashSet<string>` + `List<Dictionary<string,object>>`（违反 P-5）。
新版统一 SimulationResult 的字段 + 新增结构化 trace。

```csharp
namespace UniClaw.Core.Traversal;

/// <summary>引擎执行结果（统一 SimulationResult + TraversalResult）</summary>
public sealed record class TraversalResult(
    bool Success,
    string CompletionReason,        // "all_visited" | "max_steps" | "error" | "anti_loop"
    int TotalSteps,
    double ElapsedSeconds,
    ImmutableArray<ActionRecord> ActionHistory,
    ImmutableArray<string> VisitedPages,
    ImmutableArray<TraceRecord> Trace,
    string? TraceId,
    TraversalState FinalState,       // FSM 终态
    Exception? Error = null)
{
    public static class Reasons
    {
        public const string AllVisited = "all_visited";
        public const string MaxSteps = "max_steps";
        public const string Error = "error";
        public const string AntiLoop = "anti_loop";
        public const string Cancelled = "cancelled";  // CancellationToken / StopAsync 触发
    }
}
```

**旧版删除**: `IGraphTraversalEngine.cs` 中原有的 `TraversalResult(GlobalState Status, ...)` record 被替换。
`IGraphTraversalEngine` 接口的 `RunAsync()` 返回类型更新为新 `TraversalResult`。

**SimulationResult 删除**: `Simulation/SimulationResult.cs` 整个文件删除。所有消费者迁移到 `TraversalResult`。

### 3.2 TraceRecord

```csharp
namespace UniClaw.Core.Traversal;

/// <summary>单步 trace 记录</summary>
public sealed record class TraceRecord(
    int StepNumber,
    TraversalState FromState,
    TraversalState ToState,
    string? CurrentNodeId,
    string? CurrentPageId,
    string? ActionExecuted,
    bool ActionSuccess,
    bool ChildPushed,
    bool FrameCompleted);
```

### 3.3 TraversalEngineConfig

**合并 SimulationConfig**。`SimulateDelayMs` → `DelayPerStepMs`（通用化命名）。

```csharp
namespace UniClaw.Core.Traversal;

public sealed record class TraversalEngineConfig
{
    public int MaxSteps { get; init; } = 1000;
    public int MaxDepth { get; init; } = 10;
    public bool ThrowOnError { get; init; } = false;
    public bool TraceEnabled { get; init; } = true;
    public int DelayPerStepMs { get; init; } = 0;   // 仿真: 模拟延迟; 生产: 等待 UI 稳定
}
```

**SimulationConfig 删除**: `Simulation/SimulationConfig.cs` 整个文件删除。

---

## 4. D-14 解决 — IGraphTraversalEngine stub 清理

### 4.1 问题

两个同名接口 `IGraphTraversalEngine`:
- **空 stub** @ `UniClaw.Core.StateMachine` (TraversalState.cs:152-155) — 无方法，仅为 HasUnvisitedChildren 参数类型避免循环依赖
- **完整版** @ `UniClaw.Core.Traversal` (IGraphTraversalEngine.cs:41-90) — 8 成员 async 接口

空 stub 导致 `HasUnvisitedChildren(IGraphTraversalEngine?)` 参数永远传 null（无实现），方法成为死代码。

### 4.2 解决方案

**删除空 stub，承认 StateMachine→Traversal 向上引用**（与 D-17 一致：Observability 也是允许的向上引用）。

| 变更 | 文件 | 说明 |
|------|------|------|
| 删除空 stub | `TraversalState.cs` 删除 152-155 行 | `public interface IGraphTraversalEngine {}` |
| 参数类型替换 | `TraversalFSM.cs` | `HasUnvisitedChildren` 参数 → `UniClaw.Core.Traversal.IGraphTraversalEngine` |
| 参数类型替换 | `TraversalState.cs` ITraversalStateMachine | `HasUnvisitedChildren` 参数同上 |
| Guard test 更新 | `ArchitectureGuardTests.cs` | whitelist: StateMachine→Traversal + StateMachine→Observability 为允许向上引用 |
| 实现 | `TraversalEngine.cs` | `TraversalEngine : IGraphTraversalEngine` |

### 4.3 C-5 约束说明

C-5 原文："Graph→StateMachine 是唯一允许方向"。但 system-orchestration.md (D-17) 已承认：
- StateMachine→Traversal 是真实依赖（FSM 需要 VisitedChildren 判断）
- StateMachine→Observability 是真实依赖（TraceCoordinator span 记录）

这些向上引用**不视为设计缺陷**（D-17）。Guard test 更新不是放宽约束，而是**显式声明已承认的例外**。

---

## 5. TraversalEngine 实现

### 5.1 构造函数

```csharp
namespace UniClaw.Core.Traversal;

public sealed class TraversalEngine : IGraphTraversalEngine
{
    private readonly TraversalPlan _plan;
    private readonly IVisionProvider _vision;
    private readonly IActionExecutor _action;
    private readonly TraversalEngineConfig _config;
    private readonly ITraceRecorder? _traceRecorder;

    // --- 内部组件 (构造器创建) ---
    private TraversalRuntimeContext _ctx;
    private TraversalFSM _fsm;
    private StepContext _stepCtx;
    private StepOrchestrator _orchestrator;

    // --- IGraphTraversalEngine 属性 ---
    public TraversalPlan Plan => _plan;
    public ITraversalContext Context => _ctx;   // 返回只读接口 (P-3)
    public GlobalState CurrentState => _ctx.GlobalState;

    public TraversalEngine(
        TraversalPlan plan,
        IVisionProvider vision,
        IActionExecutor action,
        TraversalEngineConfig? config = null,
        ITraceRecorder? traceRecorder = null)
    {
        _plan = plan;
        _vision = vision;
        _action = action;
        _config = config ?? new TraversalEngineConfig();
        _traceRecorder = traceRecorder;

        Initialize();
    }
}
```

**设计选择**: 构造器调用 `Initialize()` — fail-fast 模式。C# 构造器抛异常是正常模式。
Log-and-Continue 适用于运行时执行方法（RunAsync），不适用于构造器。

**类型选择**: `TraversalEngine` 是 `sealed class`（不是 record），因为有 4 个可变内部字段
（`_ctx`、`_fsm`、`_stepCtx`、`_orchestrator`）。这与 P-5 对 TraversalRuntimeContext 的例外一致。

### 5.2 Initialize — 编译 Plan → 内部状态

```csharp
private void Initialize()
{
    // 1. 设置 GlobalState = Initializing
    _ctx = new TraversalRuntimeContext(
        traceId: $"engine-{Guid.NewGuid():N}"[..12],
        maxDepth: _config.MaxDepth);
    _ctx.GlobalState = GlobalState.Initializing;

    // 2. 编译 Plan → 节点注册表 + 根节点
    var (rootNode, registry) = CompilePlan();

    // 3. 推入根节点到栈
    _ctx.NodeStack.Push(rootNode);
    _ctx.CurrentFrame = rootNode;
    if (_plan.CompletionPolicy != null)
        _ctx.SetCompletionPolicy(_plan.CompletionPolicy);

    // 4. 创建 FSM
    _fsm = new TraversalFSM(_ctx);

    // 5. 组装 StepContext
    _stepCtx = new StepContext(
        Context: _ctx,
        StateMachine: _fsm,
        Vision: _vision,
        Action: _action,
        ChildMgr: new DynamicChildManager(registry),
        NodeRegistry: registry,
        Trace: new TraceCoordinator(_traceRecorder, _ctx.TraceId),
        SnapshotMgr: new PageSnapshotManager(),
        Stack: new NodeStackAdapter(_ctx, registry));

    // 6. 创建 Orchestrator
    _orchestrator = new StepOrchestrator();

    // 7. GlobalState → Traversing (初始化完成)
    _ctx.GlobalState = GlobalState.Traversing;
}
```

### 5.3 CompilePlan — TraversalPlan → 节点树

**修正**: 原设计引用 `_plan.DynamicNodes`（不存在），且 fallback 逻辑不可靠。
TraversalPlan 只有 `StaticNodes` 属性。

```csharp
private (TraversalNode root, DictionaryNodeRegistry registry) CompilePlan()
{
    var registry = new DictionaryNodeRegistry();

    // 注册所有 StaticNodes
    if (_plan.StaticNodes != null)
    {
        foreach (var (id, node) in _plan.StaticNodes)
            registry.Register(node);
    }

    // Root node: 优先使用 plan 中已有的 RootNode
    // 如果 RootNode 为 null, 构建 minimal root from EntryApp
    var root = _plan.RootNode ?? BuildDefaultRoot(_plan.EntryApp);

    // 确保 root 自身也在注册表中（如果 StaticNodes 不包含 root ID）
    if (registry.GetNode(root.NodeId) == null)
        registry.Register(root);

    return (root, registry);
}

/// <summary>Plan 无 RootNode 时，构建 minimal root (NoAction + Static children from StaticNodes)</summary>
private TraversalNode BuildDefaultRoot(string entryApp)
{
    var childIds = _plan.StaticNodes?.Keys.ToList() ?? new List<string>();

    return new TraversalNode(
        NodeId: $"{entryApp}_root",
        Name: $"Root of {entryApp}",
        NodeType: NodeType.Container,
        Operation: new Operation(OperationType.NoAction),
        ChildrenStrategy: new ChildrenStrategy(
            ChildrenStrategyType.Static,
            StaticChildren: childIds),
        Precondition: null,
        ErrorPolicy: null,
        ExitCondition: null,
        Meta: null);
}
```

**注意**: `BuildDefaultRoot` 使用 `StaticNodes.Keys` 作为 root 的 children。
这在大多数 plan 中是正确的（StaticNodes 的 key 通常是 root 的直接子节点 ID），
但对于嵌套 plan（child 也是 container，有自己的 StaticChildren），
root 的 `StaticChildren` 应只包含直接子节点 ID，不含嵌套的孙子节点。
PlanCompiler 在 `BuildRootNode()` 中已经正确处理了这种情况。

### 5.4 RunAsync — 核心循环

**设计选择**: `RunAsync()` 为 primary API（实现 IGraphTraversalEngine），
`Run()` 为同步便利包装。

```csharp
public async Task<TraversalResult> RunAsync(CancellationToken ct = default)
{
    var stopwatch = Stopwatch.StartNew();
    var traceRecords = _config.TraceEnabled ? new List<TraceRecord>() : null;
    var visitedPages = new List<string>();
    var fromState = _fsm.CurrentState;

    try
    {
        for (int i = 0; i < _config.MaxSteps; i++)
        {
            ct.ThrowIfCancellationRequested();

            // 仿真延迟 (DelayPerStepMs)
            if (_config.DelayPerStepMs > 0)
                await Task.Delay(_config.DelayPerStepMs, ct);

            var stepResult = _orchestrator.ExecuteStep(_stepCtx);

            // Leaf 执行后自动 pop（同 SimulationRunner 的修复）
            if (stepResult.NextState == TraversalState.ResultVerify
                && _ctx.NodeStack.Depth > 1
                && _ctx.CurrentFrame?.ChildrenStrategy.Type == ChildrenStrategyType.None)
                _ctx.NodeStack.Pop();

            _ctx.CurrentFrame = _ctx.NodeStack.Peek()?.Node;

            if (stepResult.ChildPushed
                && _fsm.CanTransitionTo(TraversalState.NodeSelect))
                _fsm.TransitionTo(TraversalState.NodeSelect);

            // 记录 trace
            if (_config.TraceEnabled && traceRecords != null)
            {
                traceRecords.Add(new TraceRecord(
                    StepNumber: i + 1,
                    FromState: fromState,
                    ToState: stepResult.NextState,
                    CurrentNodeId: _ctx.CurrentFrame?.NodeId,
                    CurrentPageId: GetCurrentPageId(),
                    ActionExecuted: GetLastAction(),
                    ActionSuccess: GetLastActionSuccess(),
                    ChildPushed: stepResult.ChildPushed,
                    FrameCompleted: stepResult.FrameCompleted));
            }

            RecordPageVisit(visitedPages);

            // 终止条件
            if (stepResult.FrameCompleted && _ctx.NodeStack.Depth <= 1)
                return Done(TraversalResult.Reasons.AllVisited, i + 1,
                    stopwatch, traceRecords, visitedPages);
            if (stepResult.AntiLoopTriggered)
                return Done(TraversalResult.Reasons.AntiLoop, i + 1,
                    stopwatch, traceRecords, visitedPages);

            fromState = _fsm.CurrentState;
        }

        return Done(TraversalResult.Reasons.MaxSteps, _config.MaxSteps,
            stopwatch, traceRecords, visitedPages);
    }
    catch (OperationCanceledException)
    {
        // CancellationToken 触发 — 用户主动停止
        return Done(TraversalResult.Reasons.Cancelled, _ctx.StepCount,
            stopwatch, traceRecords, visitedPages);
    }
    catch (Exception ex)
    {
        // Log-and-Continue: 不抛出，返回 Error result
        _ctx.GlobalState = GlobalState.Error;
        return Done(TraversalResult.Reasons.Error, _ctx.StepCount,
            stopwatch, traceRecords, visitedPages, ex);
    }
    finally
    {
        // Trace session 结束（Log-and-Continue: 吞掉 EndSessionAsync 异常）
        try
        {
            if (_traceRecorder != null)
                await _traceRecorder.EndSessionAsync();
        }
        catch { /* swallow — 不影响结果返回 */ }
        stopwatch.Stop();
    }
}

/// <summary>同步便利包装 — 仿真测试用。
/// ⚠️ 在 ASP.NET / UI SynchronizationContext 线程上下文可能死锁。
/// 仅用于 CLI / 测试环境（无 SynchronizationContext 的线程）。</summary>
public TraversalResult Run()
    => RunAsync().GetAwaiter().GetResult();
```

**⚠️ 死锁风险**: `GetAwaiter().GetResult()` 在有 `SynchronizationContext` 的线程
（ASP.NET、WinForms、WPF）上会死锁，因为 async 方法在等待 continuation 时需要回到原线程，
而原线程被 `GetResult()` 阻塞。在 CLI / 测试 / 后台线程环境中没有此问题。

### 5.5 IGraphTraversalEngine 方法实现

Phase 2.3 只需要 `RunAsync()`。其余方法 stub 实现（不检查前置条件，Phase 3 完善）：

```csharp
// InitializeAsync — 构造器已完成初始化，此方法为 no-op validation
public Task InitializeAsync(CancellationToken ct = default)
    => Task.CompletedTask;  // 构造器已初始化

// PauseAsync — Phase 3 完整实现（应检查 GlobalState == Traversing 才允许 pause）
public Task PauseAsync(CancellationToken ct = default)
{
    _ctx.GlobalState = GlobalState.Paused;
    return Task.CompletedTask;
}

// ResumeAsync — Phase 3 完整实现（应检查 GlobalState == Paused 才允许 resume）
public Task ResumeAsync(CancellationToken ct = default)
{
    _ctx.GlobalState = GlobalState.Traversing;
    return Task.CompletedTask;
}

// StopAsync — 设置 Terminated
public Task StopAsync(CancellationToken ct = default)
{
    _ctx.GlobalState = GlobalState.Terminated;
    return Task.CompletedTask;
}

// GetStateAsync — 返回当前 GlobalState
public Task<GlobalState> GetStateAsync(CancellationToken ct = default)
    => Task.FromResult(_ctx.GlobalState);
```

### 5.6 Done helper

```csharp
private TraversalResult Done(string reason, int steps, Stopwatch sw,
    List<TraceRecord>? trace, List<string> pages, Exception? error = null)
{
    // GlobalState 设置
    _ctx.GlobalState = reason is TraversalResult.Reasons.AllVisited
        or TraversalResult.Reasons.AntiLoop
        ? GlobalState.Completed
        : reason is TraversalResult.Reasons.Cancelled
            ? GlobalState.Terminated
            : GlobalState.Error;

    return new TraversalResult(
        Success: reason is TraversalResult.Reasons.AllVisited
                     or TraversalResult.Reasons.AntiLoop,
        CompletionReason: reason,
        TotalSteps: steps,
        ElapsedSeconds: sw.Elapsed.TotalSeconds,
        ActionHistory: _action.GetHistory().ToImmutableArray(),
        VisitedPages: pages.ToImmutableArray(),
        Trace: trace?.ToImmutableArray() ?? ImmutableArray<TraceRecord>.Empty,
        TraceId: _ctx.TraceId,
        FinalState: _fsm.CurrentState,
        Error: error);
}
```

### 5.7 Helper 方法

```csharp
private string? GetCurrentPageId()
    => _ctx.CurrentPageAnalysis?.PageId;

private string? GetLastAction()
    => _ctx.ActionHistory.LastOrDefault()?.Action;

private bool GetLastActionSuccess()
    => _ctx.ActionHistory.LastOrDefault()?.Success ?? false;

private void RecordPageVisit(List<string> pages)
{
    var currentPage = GetCurrentPageId();
    if (currentPage != null && !pages.Contains(currentPage))
        pages.Add(currentPage);
}
```

---

## 6. GlobalFSM 协调

TraversalEngine 作为统一入口，通过 `ITraversalContext.GlobalState` 读写协调两个独立 FSM。
**不共享基础设施** (C-4, P-7)。

| 生命周期点 | GlobalState 设置 | 说明 |
|-----------|-----------------|------|
| 构造器 Initialize() 开始 | `Initializing` | 编译 Plan + 组装组件 |
| Initialize() 完成 | `Traversing` | 开始遍历循环 |
| 循环正常运行 | `Traversing` (保持) | StepOrchestrator 逐步执行 |
| 正常完成 (AllVisited/AntiLoop) | `Completed` | 不可逆终态 |
| 错误捕获 | `Error` | → Recovering → Initializing → Traversing 恢复路径 (Phase 3) |
| 用户 PauseAsync | `Paused` | 可恢复到 Traversing |
| 用户 StopAsync | `Terminated` | 不可逆终态 |
| CancellationToken | `Terminated` | 不可逆终态 (Done() 映射 Reasons.Cancelled) |

**注意**: 当前不创建 `GlobalFSM` 具体类（Phase 3）。
GlobalState 的转换遵循 fsm-design.md 的迁移矩阵约束：
- Completed 和 Terminated 是终态，无出口转换
- Error → 必须经 Recovering → Initializing → Traversing 恢复（Phase 3 实现）
- Paused → 只能到 Traversing 或 Terminated

---

## 7. SimulationRunner 合并

```diff
- SimulationRunner 作为 public API
+ SimulationRunner 逻辑完全迁移入 TraversalEngine
+ SimulationRunner.cs 删除 (不再是 internal，不再存在)
+ SimulationResult.cs 删除 (合并入 TraversalResult)
+ SimulationConfig.cs 删除 (合并入 TraversalEngineConfig)
```

### 7.1 逻辑对照

SimulationRunner.Run() 中的逻辑与 TraversalEngine.RunAsync() 逐项对照：

| SimulationRunner 逻辑 | TraversalEngine 对应 | 变化 |
|----------------------|---------------------|------|
| `StateFixture fixture` 构造参数 | 移除 — StatefulMockVisionService/ActionExecutor 通过构造参数注入 | **外部化** |
| `new StatefulMockVisionService(fixture)` 内部创建 | 调用方自行创建，通过 `vision` 参数注入 | **外部化** |
| `new StatefulMockActionExecutor(vision)` 内部创建 | 调用方自行创建，通过 `action` 参数注入 | **外部化** |
| leaf-pop-after-ResultVerify | 直接在 RunAsync() 中实现 | **保持** |
| child-push → NodeSelect transition | 直接在 RunAsync() 中实现 | **保持** |
| page-visit tracking | 直接在 RunAsync() 中实现 | **保持** |
| frame-completion pop | 直接在 RunAsync() 中实现 | **保持** |
| AllVisited/AntiLoop/MaxSteps 终止 | 直接在 RunAsync() 中实现 | **保持 + Done() helper** |
| trace recording | RunAsync() 中 TraceRecord 记录 (新) | **新增** |
| GlobalFSM 协调 | ctx.GlobalState 管理 (新) | **新增** |

### 7.2 现有 SimulationRunner 的不保留部分

| 项目 | 原因 |
|------|------|
| `StateFixture` 构造参数 | 仿真依赖外部化，TraversalEngine 不直接依赖 StateFixture |
| `Vision` / `Action` public 属性 | TraversalEngine 的 `_vision` / `_action` 是 private，通过 Context 间接暴露 |
| `Context` public 属性 (SimulationRunner) | → `TraversalEngine.Context` (ITraversalContext 接口) |
| `CurrentState` public 属性 | → `TraversalEngine.CurrentState` (GlobalState) |

---

## 8. 使用方式

### 8.1 仿真模式

```csharp
// 对齐 Python: engine = GraphTraversalEngine(plan, vision_service=v, action_executor=a)
var fixture = new StateFixture("settings_app");
var vision = new StatefulMockVisionService(fixture);
var action = new StatefulMockActionExecutor(vision);

var plan = new TraversalPlan(
    entryApp: "settings.app",
    rootNode: root,
    staticNodes: nodes,
    completionPolicy: ...);

var engine = new TraversalEngine(
    plan,
    vision: vision,
    action: action,
    config: new TraversalEngineConfig { TraceEnabled = true });

// 同步便利 — 仿真测试用
var result = engine.Run();

// 结果
Assert.True(result.Success);
Assert.NotEmpty(result.Trace);          // trace 记录可用
foreach (var record in result.Trace)
    Console.WriteLine($"{record.StepNumber}: {record.FromState}→{record.ToState}");
```

### 8.2 仿真模式 (async)

```csharp
// 完整 async API — 实现IGraphTraversalEngine
var result = await engine.RunAsync();

// 支持 CancellationToken
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var result = await engine.RunAsync(cts.Token);
```

### 8.3 生产模式（ADB + AI，Phase 3）

```csharp
var engine = new TraversalEngine(
    plan,
    vision: new RealAdbVisionProvider(),   // Phase 3
    action: new RealAdbActionExecutor(),   // Phase 3
    traceRecorder: new FileStorage());

var result = await engine.RunAsync();
```

### 8.4 对比: Python → C#

```python
# Python
plan = TraversalPlan(entry_app="test", root_node=root, static_nodes=nodes)
engine = GraphTraversalEngine(plan, vision_service=v, action_executor=a)
result = engine.run()
print(result.completion_reason, len(result.trace))
```

```csharp
// C#
var plan = new TraversalPlan(entryApp: "test", rootNode: root, staticNodes: nodes);
var engine = new TraversalEngine(plan, vision: v, action: a);
var result = engine.Run();
Console.WriteLine($"{result.CompletionReason}, {result.Trace.Length}");
```

### 8.5 测试迁移

现有 Simulation E2E tests 迁移到 TraversalEngine 构造：

```csharp
// BEFORE (SimulationE2ETests.cs):
var fixture = new StateFixture("settings_app");
var registry = new SimpleNodeRegistry();
registry.Register(wifiPage);
var root = new TraversalNode(...);
var runner = new SimulationRunner(fixture, root, registry);
var result = runner.Run();

// AFTER:
var fixture = new StateFixture("settings_app");
var vision = new StatefulMockVisionService(fixture);
var action = new StatefulMockActionExecutor(vision);

// 构建 TraversalPlan — 需要把手动注册的节点转为 Dictionary<string, TraversalNode>
var nodes = new Dictionary<string, TraversalNode>
{
    { wifiPage.NodeId, wifiPage },
    // ... 其他手动注册的节点
};

var plan = new TraversalPlan(
    entryApp: "settings.app",
    rootNode: root,
    staticNodes: nodes,
    entryPolicy: new EntryPolicy(EntryStrategy.DirectEntry),
    planName: "test_plan",
    planId: "test-001",
    completionPolicy: ...);

var engine = new TraversalEngine(plan, vision, action);
var result = engine.Run();  // 同步便利 — 测试逻辑不变
```

**迁移要点**: 测试不再手动构造 `SimpleNodeRegistry` 并逐个 `Register(node)`。
改为直接构建 `Dictionary<string, TraversalNode>` 传给 `TraversalPlan.staticNodes`。
`DictionaryNodeRegistry`（原 `SimpleNodeRegistry`，移到 Traversal namespace）仅供 TraversalEngine.CompilePlan() 和 DynamicChildManager 内部使用，测试不直接引用。

---

## 9. Trace 记录格式

```json
{
  "stepNumber": 1,
  "fromState": "NodeSelect",
  "toState": "PreconditionCheck",
  "currentNodeId": "root",
  "currentPageId": "home",
  "actionExecuted": null,
  "actionSuccess": false,
  "childPushed": false,
  "frameCompleted": false
}
```

Trace 的用途:
- 调试：回放每一步发生了什么
- BehaviorValidator: 对比实际 trace 与期望行为 (Phase 2.4)
- ProblemDetector: 检测循环/重复/异常 (Phase 2.4)
- Dashboard 可视化: 状态转换图、操作时间线

Trace 与 ITraceRecorder 的关系:
- `TraceRecord[]` 是 TraversalResult 的内存 trace — 每步记录
- `ITraceRecorder` 是外部持久化（文件、数据库）— 通过 TraceCoordinator span 记录
- 两者独立：TraceRecord 不依赖 ITraceRecorder，ITraceRecorder 不消费 TraceRecord

---

## 10. 文件清单

### 新增文件

| # | 文件 | 说明 | 行数 |
|---|------|------|------|
| 1 | `src/UniClaw.Core/Traversal/TraversalEngine.cs` | 统一引擎类（替换同名文件中的 helper 集） | ~120 |
| 2 | `src/UniClaw.Core/Traversal/TraversalResult.cs` | 统一 Result + TraceRecord record | ~40 |
| 3 | `src/UniClaw.Core/Traversal/TraversalEngineConfig.cs` | 统一 Config record (合并 SimulationConfig) | ~15 |

### 删除文件

| # | 文件 | 原因 |
|---|------|------|
| 4 | `src/UniClaw.Core/Simulation/SimulationRunner.cs` | 逻辑迁移入 TraversalEngine |
| 5 | `src/UniClaw.Core/Simulation/SimulationResult.cs` | 合并入 TraversalResult |
| 6 | `src/UniClaw.Core/Simulation/SimulationConfig.cs` | 合并入 TraversalEngineConfig |

### 修改文件

| # | 文件 | 变更 |
|---|------|------|
| 7 | `src/UniClaw.Core/Traversal/IGraphTraversalEngine.cs` | TraversalResult 形状更新；移除旧版 record |
| 8 | `src/UniClaw.Core/StateMachine/TraversalState.cs` | 删除空 IGraphTraversalEngine stub (152-155 行)；ITraversalStateMachine.HasUnvisitedChildren 参数类型更新 |
| 9 | `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | HasUnvisitedChildren 参数类型 → UniClaw.Core.Traversal.IGraphTraversalEngine；增加 using |
| 10 | `tests/.../Simulation/SimulationE2ETests.cs` | 替换 SimulationRunner → TraversalEngine 构造 |
| 11 | `tests/.../ArchitectureGuardTests.cs` | whitelist: StateMachine→Traversal+Observability 向上引用 |
| 12 | `docs/system/layers/traversal.md` | 新增 TraversalEngine 到类型清单 |
| 13 | `docs/system/layers/simulation.md` | SimulationRunner 降级标注 + 删除清单 |
| 14 | `docs/system/constitution/constraints.md` | C-5 更新: 显式承认 StateMachine→Traversal+Observability 向上引用 |
| 15 | `docs/system/decisions/log.md` | D-14 标记 resolved |

### 保留文件（Simulation namespace 内）

| # | 文件 | 说明 |
|---|------|------|
| 16 | `src/UniClaw.Core/Simulation/StateFixture.cs` | 测试 fixture，SimulationRunner 删除后仍需要 |
| 17 | `src/UniClaw.Core/Simulation/StatefulMockVisionService.cs` | 实现 IVisionProvider，测试用 |
| 18 | `src/UniClaw.Core/Simulation/StatefulMockActionExecutor.cs` | 实现 IActionExecutor，测试用 |

### 移动文件

| # | 文件 | 变更 | 原因 |
|---|------|------|------|
| 19 | `SimpleNodeRegistry.cs` | Simulation → Traversal namespace，重命名为 `DictionaryNodeRegistry` | TraversalEngine.CompilePlan() 需要使用 INodeRegistry 实现；Traversal→Simulation 依赖方向错误。DictionaryNodeRegistry 是通用字典注册表，不是仿真专用 |

---

## 11. 与 Python 对照

| 能力 | Python GraphTraversalEngine | C# TraversalEngine |
|------|---------------------------|-------------------|
| 输入 | `TraversalPlan` | `TraversalPlan` |
| 服务注入 | 构造参数 | 构造参数 (IVisionProvider + IActionExecutor) |
| 内部循环 | `step()` 循环 | `StepOrchestrator.ExecuteStep()` 循环 |
| 终止条件 | `_is_complete()` | `FrameCompleted + depth ≤ 1` |
| Trace 记录 | `TraceRecorder` → `FileStorage` | `TraceRecord[]` 内存 + 可选 `ITraceRecorder` |
| 节点编译 | Plan 已在外部编译好 | 内部 `CompilePlan()` |
| 生命周期 | `run()` 同步 | `RunAsync()` async + `Run()` 同步便利 |
| FSM 协调 | Python 无双 FSM | 通过 `ctx.GlobalState` 协调双 FSM |

---

## 12. 后续路线

```
Phase 2.3-sim ✅         StateFixture + StatefulMock*
Phase 2.3-sim-runner ✅  SimulationRunner 自动化驱动
Phase 2.3-engine         本文: TraversalEngine 统一入口
     ↓
Phase 2.3c               HandleErrorHandling + HandlePopupHandling
                         └── TraversalEngine 可验证
     ↓
Phase 2.3b               HandleResultVerify + HandlePreconditionCheck
     ↓
Phase 2.4                BehaviorValidator + ProblemDetector
                         └── 依赖 TraversalEngine 输出的 trace
```

---

## A. 约束合规检查

| 约束 | 合规 | 说明 |
|------|------|------|
| C-1: TraversalState 8 值锁定 | ✅ | RunAsync() 使用现有 8 个状态，不新增 |
| C-4: 双 FSM 独立 | ✅ | 仅通过 ctx.GlobalState 协调，不共享基础设施 |
| C-5: 依赖方向 | ⚠️→✅ | 删除 stub 后 StateMachine→Traversal 显式承认 (D-17)；Traversal 不引用 Simulation namespace (SimpleNodeRegistry 移入 Traversal) |
| C-7: GlobalState 8 值锁定 | ✅ | 使用现有 8 个状态 |
| P-1: 禁止 ToDictionary | ✅ | 不使用 |
| P-2: 视外观+行为不混合 | ✅ | 不涉及 |
| P-3: ITraversalContext 只读 | ✅ | Context 属性返回 ITraversalContext (只读接口)；引擎内部用 TraversalRuntimeContext 操作 |
| P-4: HashSet 不直接暴露为 IReadOnlySet | ✅ | TraversalResult 使用 ImmutableArray |
| P-5: sealed record class | ✅ | TraversalResult, TraceRecord, TraversalEngineConfig 都是 sealed record class |
| P-6: DomainValidationException | ✅ | 构造器校验用；运行时错误通过 TraversalResult.Error 返回 |
| P-7: TraversalFSM 不引用 GlobalFSM | ✅ | 不共享 |
| Log-and-Continue | ✅ | RunAsync() catch(Exception) → Done(Error)，永不抛出 |
| Dispatch-table pattern | ✅ | 无新 dispatch 需求；InitializeAsync 等方法遵循 ?? default 模式 |
