# TraversalEngine — 统一遍历引擎设计

> 将 PlanCompiler、StepOrchestrator、SimulationRunner 整合为单一入口，
> 对齐 Python `GraphTraversalEngine`。仿真和真实执行共用同一接口。
> 日期: 2026-07-05

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
| `IVisionProvider` / `IActionExecutor` | — | ✅ 存在 |
| `TraceCoordinator` | `Traversal/TraversalEngine.cs` | ✅ 存在 |

### 关键缺失

1. **无统一入口** — 没有像 Python `GraphTraversalEngine` 的类
2. **Plan→node-tree 编译** — `PlanCompiler` 只做 IntentSlots→Plan，不做 Plan→节点树展开
3. **Trace 输出** — Runner 不记录 trace
4. **仿真 vs 生产** — 当前 Runner 只能仿真（mock 服务）

---

## 2. 目标

```
TraversalEngine(plan, vision, action, trace?)
  │
  ├── 内部构造 Context + FSM
  ├── 编译 Plan → 节点树 (PlanAdapter 内部逻辑)
  ├── 循环调用 StepOrchestrator.ExecuteStep(ctx)
  ├── 记录 trace
  └── 返回 TraversalResult
```

### Goals

- **单一入口** — `new TraversalEngine(plan, vision, action).Run()`
- **Plan 驱动** — 输入 `TraversalPlan`，自动编译节点树并注册
- **仿真/生产统一** — 注入 mock → 仿真；注入真实 → 生产。引擎不变
- **Trace 记录** — 输出完整 trace（FSM 状态序列、操作、页面变化）
- **SimulationRunner 合并** — Runner 不再作为独立 public API，成为引擎内部组件

### Non-Goals

- 不改变 `TraversalFSM` / `StepOrchestrator` / `TraversalRuntimeContext`
- 不改变 `IVisionProvider` / `IActionExecutor` 接口
- 不实现 `BehaviorValidator` / `ProblemDetector`
- 不实现 Scroll 仿真

---

## 3. 类型定义

### 3.1 TraversalResult

```csharp
namespace UniClaw.Core.Traversal;

/// <summary>引擎执行结果</summary>
public sealed record class TraversalResult(
    bool Success,
    string CompletionReason,        // "all_visited" | "max_steps" | "error" | "anti_loop"
    int TotalSteps,
    double ElapsedSeconds,
    ImmutableArray<ActionRecord> ActionHistory,
    ImmutableArray<string> VisitedPages,
    ImmutableArray<TraceRecord> Trace,      // NEW: 完整 trace
    TraversalState FinalState,
    Exception? Error = null)
{
    public static class Reasons { ... }  // 同 SimulationResult.Reasons
}

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

### 3.2 TraversalEngineConfig

```csharp
public sealed record class TraversalEngineConfig
{
    public int MaxSteps { get; init; } = 1000;
    public int MaxDepth { get; init; } = 10;
    public bool ThrowOnError { get; init; } = false;
    public bool TraceEnabled { get; init; } = true;  // NEW: 控制 trace 记录
}
```

---

## 4. TraversalEngine 实现

### 4.1 构造函数

```csharp
namespace UniClaw.Core.Traversal;

public sealed class TraversalEngine
{
    private readonly TraversalPlan _plan;
    private readonly IVisionProvider _vision;
    private readonly IActionExecutor _action;
    private readonly TraversalEngineConfig _config;
    private readonly ITraceRecorder? _traceRecorder;  // 可选: 持久化 trace

    // --- 内部组件 (构造器创建) ---
    private TraversalRuntimeContext _ctx;
    private TraversalFSM _fsm;
    private StepContext _stepCtx;
    private StepOrchestrator _orchestrator;

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

### 4.2 Initialize — 编译 Plan → 内部状态

```csharp
private void Initialize()
{
    // 1. 编译 Plan → 节点注册表 + 根节点
    var (rootNode, registry) = CompilePlan();

    // 2. 创建 Context
    _ctx = new TraversalRuntimeContext(
        traceId: $"engine-{Guid.NewGuid():N}"[..12],
        maxDepth: _config.MaxDepth);
    _ctx.NodeStack.Push(rootNode);
    _ctx.CurrentFrame = rootNode;
    _ctx.SetCompletionPolicy(_plan.CompletionPolicy);

    // 3. 创建 FSM
    _fsm = new TraversalFSM(_ctx);

    // 4. 组装 StepContext
    _stepCtx = new StepContext(
        Context: _ctx,
        StateMachine: _fsm,
        Vision: _vision,
        Action: _action,
        ChildMgr: new DynamicChildManager(registry),
        NodeRegistry: registry,
        Trace: new TraceCoordinator(
            _traceRecorder, _ctx.TraceId),
        SnapshotMgr: new PageSnapshotManager(),
        Stack: new NodeStackAdapter(_ctx, registry));

    _orchestrator = new StepOrchestrator();
}
```

### 4.3 CompilePlan — TraversalPlan → 节点树

```csharp
private (TraversalNode root, SimpleNodeRegistry registry) CompilePlan()
{
    var registry = new SimpleNodeRegistry();

    // 注册所有静态节点
    if (_plan.StaticNodes != null)
    {
        foreach (var (id, node) in _plan.StaticNodes)
            registry.Register(node);
    }

    // 注册 dynamicNodes 如果存在
    if (_plan.DynamicNodes != null)
    {
        foreach (var (id, node) in _plan.DynamicNodes)
            registry.Register(node);
    }

    var root = CompileRootNode();
    return (root, registry);
}

private TraversalNode CompileRootNode()
{
    var planRoot = _plan.RootNode;
    // plan 的 root 可能缺少 childrenStrategy 或 operation
    // 如果 planRoot 没有 StaticChildren，从 _plan.StaticNodes 的 key 推断
    var children = planRoot.StaticChildren.Count > 0
        ? planRoot.StaticChildren
        : (_plan.StaticNodes?.Keys.ToList() ?? new());

    return new TraversalNode(
        NodeId: planRoot.NodeId,
        Name: planRoot.Name,
        NodeType: planRoot.NodeType,
        Operation: planRoot.Operation ?? new Operation(OperationType.NoAction),
        ChildrenStrategy: planRoot.ChildrenStrategy.Type != ChildrenStrategyType.None
            ? planRoot.ChildrenStrategy
            : new ChildrenStrategy(ChildrenStrategyType.Static,
                StaticChildren: children),
        Precondition: planRoot.Precondition,
        ErrorPolicy: planRoot.ErrorPolicy,
        ExitCondition: planRoot.ExitCondition,
        Meta: planRoot.Meta
    );
}
```

### 4.4 Run

```csharp
public TraversalResult Run()
{
    var stopwatch = Stopwatch.StartNew();
    var traceRecords = new List<TraceRecord>();
    var visitedPages = new List<string>();
    var fromState = _fsm.CurrentState;

    try
    {
        for (int i = 0; i < _config.MaxSteps; i++)
        {
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
            if (_config.TraceEnabled)
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
                return Done(SimulationResult.Reasons.AllVisited, i + 1,
                    stopwatch, traceRecords, visitedPages);
            if (stepResult.AntiLoopTriggered)
                return Done(SimulationResult.Reasons.AntiLoop, i + 1,
                    stopwatch, traceRecords, visitedPages);

            fromState = _fsm.CurrentState;
        }

        return Done(SimulationResult.Reasons.MaxSteps, _config.MaxSteps,
            stopwatch, traceRecords, visitedPages);
    }
    catch (Exception ex)
    {
        return Done(SimulationResult.Reasons.Error, _ctx.StepCount,
            stopwatch, traceRecords, visitedPages, ex);
    }
}
```

### 4.5 Done helper

```csharp
private TraversalResult Done(string reason, int steps, Stopwatch sw,
    List<TraceRecord> trace, List<string> pages, Exception? error = null)
{
    sw.Stop();
    _traceRecorder?.Flush();

    return new TraversalResult(
        Success: reason is SimulationResult.Reasons.AllVisited
                      or SimulationResult.Reasons.AntiLoop,
        CompletionReason: reason,
        TotalSteps: steps,
        ElapsedSeconds: sw.Elapsed.TotalSeconds,
        ActionHistory: _action.GetHistory().ToImmutableArray(),
        VisitedPages: pages.ToImmutableArray(),
        Trace: trace.ToImmutableArray(),
        FinalState: _fsm.CurrentState,
        Error: error);
}
```

---

## 5. 使用方式

### 5.1 仿真模式

```csharp
// 等价于 Python 仿真测试
var plan = new TraversalPlan(
    entryApp: "settings.app",
    rootNode: root,
    staticNodes: nodes,
    completionPolicy: ...);

var engine = new TraversalEngine(
    plan,
    vision: new StatefulMockVisionService(fixture),
    action: new StatefulMockActionExecutor(vision),
    config: new TraversalEngineConfig { TraceEnabled = true });

var result = engine.Run();

// 结果
Assert.True(result.Success);
Assert.NotEmpty(result.Trace);          // trace 记录可用
foreach (var record in result.Trace)
    Console.WriteLine($"{record.StepNumber}: {record.FromState}→{record.ToState}");
```

### 5.2 生产模式（ADB + AI，未来）

```csharp
var engine = new TraversalEngine(
    plan,
    vision: new RealAdbVisionProvider(),   // Phase 3
    action: new RealAdbActionExecutor(),   // Phase 3
    traceRecorder: new FileStorage());

var result = engine.Run();
```

### 5.3 对比: Python → C#

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

---

## 6. SimulationRunner 的去留

```diff
- SimulationRunner 作为 public API
+ TraversalEngine 作为统一 public API
+ SimulationRunner 降级为 TraversalEngine 的内部实现细节
```

现有测试中的 `SimulationRunner` 构造替换为 `TraversalEngine` 构造，行为不变。

```csharp
// 之前
var runner = new SimulationRunner(fixture, root, registry);
var result = runner.Run();

// 之后（等价）
var plan = new TraversalPlan(entryApp: "test", rootNode: root, staticNodes: regDict);
var engine = new TraversalEngine(plan, vision, action);
var result = engine.Run();
```

`SimulationRunner` 的代码不删除，移到 `internal` 或作为 `TraversalEngine` 的内部嵌套类。

---

## 7. Trace 记录格式

```json
// TraceRecord 序列化示例
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
- BehaviorValidator: 对比实际 trace 与期望行为 (future)
- ProblemDetector: 检测循环/重复/异常 (future)
- Dashboard 可视化: 状态转换图、操作时间线

---

## 8. 文件清单

### 新增文件

| # | 文件 | 说明 | 行数 |
|---|------|------|------|
| 1 | `src/UniClaw.Core/Traversal/TraversalEngine.cs` | 统一引擎类（替换同名文件中的 helper 集的占位） | ~200 |
| 2 | `src/UniClaw.Core/Traversal/TraversalResult.cs` | 结果 + TraceRecord record | ~50 |
| 3 | `src/UniClaw.Core/Traversal/TraversalEngineConfig.cs` | 配置 record | ~15 |

### 删除/降级

| # | 文件 | 变更 |
|---|------|------|
| 4 | `src/UniClaw.Core/Simulation/SimulationRunner.cs` | 降级为 internal，被 TraversalEngine 替代 |
| 5 | `src/UniClaw.Core/Simulation/SimulationConfig.cs` | 保留？合并到 TraversalEngineConfig |
| 6 | `src/UniClaw.Core/Simulation/SimulationResult.cs` | 保留？Result 类型统一为 TraversalResult |

### 修改文件

| # | 文件 | 变更 |
|---|------|------|
| 7 | `tests/.../Simulation/SimulationE2ETests.cs` | 替换 SimulationRunner → TraversalEngine |
| 8 | `docs/system/layers/traversal.md` | 新增 TraversalEngine 到类型清单 |
| 9 | `docs/system/layers/simulation.md` | SimulationRunner 降级标注 |

---

## 9. 与 Python 对照

| 能力 | Python GraphTraversalEngine | C# TraversalEngine |
|------|---------------------------|-------------------|
| 输入 | `TraversalPlan` | `TraversalPlan` |
| 服务注入 | 构造参数 | 构造参数 |
| 内部循环 | `step()` 循环 | `StepOrchestrator.ExecuteStep()` 循环 |
| 终止条件 | `_is_complete()` | `FrameCompleted + depth ≤ 1` |
| Trace 记录 | `TraceRecorder` → `FileStorage` | `TraceRecord[]` 内存 + 可选 `ITraceRecorder` |
| 节点编译 | Plan 已在外部编译好 | 内部 `CompilePlan()` |

---

## 10. 后续路线

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
