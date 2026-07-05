# Phase 2.3-sim-runner — SimulationRunner 设计

> 在 Phase 2.3-sim (StateFixture + StatefulMockVision + StatefulMockAction) 基础上，
> 新增自动化仿真驱动层，复用真实 StepOrchestrator 完成端到端遍历。
> 日期: 2026-07-05

---

## 1. 动机

### 1.1 当前状态

Phase 2.3-sim 已交付核心 3 组件（StateFixture、StatefulMockVisionService、StatefulMockActionExecutor），E2E 测试已证明 3 级页面遍历可行（`ThreeLevelTraversal_HomeToWifiAndBack`，489 tests pass）。

但 E2E 测试代码严重重复：

```csharp
// 每次执行一个节点都要手动写：
ctx.NodeStack.Push(settingsNode);
ctx.CurrentFrame = settingsNode;
ctx.AddVisitedChild("root", "btn_settings");
fsm.TransitionTo(TraversalState.Branch);
fsm.TransitionTo(TraversalState.NodeSelect);
fsm.TransitionTo(TraversalState.PreconditionCheck);
fsm.TransitionTo(TraversalState.Execute);
result = fsm.Step(stepCtx);
```

这些中间步骤（push child、mark visited、FSM 状态推进）在 Python 中由 `SimulationRunner` 自动完成。

### 1.2 Python 对照

```python
# Python: 一行启动仿真
runner = SimulationRunner(virtual_pages=data, plan=plan)
result = runner.run()
# result.visited_tree, result.executed_actions, result.trace, ...
```

Python `SimulationRunner`（362 行）的核心循环：

```python
while not self._is_complete():
    self.engine.step()  # GraphTraversalEngine 内部调用 FSM.step() + 中间逻辑
```

C# 中 `StepOrchestrator.ExecuteStep(ctx)` 已经封装了所有中间逻辑——包括 FSM 状态推进、BRANCH 拦截、子节点发现、visited 标记。Runner 只需循环调用。

---

## 2. 架构

### 2.1 位置

```
src/UniClaw.Core/Simulation/
├── StateFixture.cs                    ← Phase 2.3-sim ✅
├── StateFixtureBuilder.cs             ← Phase 2.3-sim ✅
├── StatefulMockVisionService.cs       ← Phase 2.3-sim ✅
├── StatefulMockActionExecutor.cs      ← Phase 2.3-sim ✅
├── SimpleNodeRegistry.cs              ← Phase 2.3-sim ✅
├── SimulationRunner.cs                ← NEW (本文)
├── SimulationConfig.cs                ← NEW
└── SimulationResult.cs                ← NEW
```

### 2.2 依赖关系

```
SimulationRunner
  ├── StepOrchestrator          ← 真实（14-step 编排）
  │     ├── TraversalFSM        ← 真实（8 handler）
  │     ├── TraversalRuntimeContext ← 真实（30 可变状态）
  │     ├── DynamicChildManager ← 真实（子节点发现）
  │     ├── NodeStackAdapter    ← 真实
  │     └── TraceCoordinator    ← 真实（active=false 时 no-op）
  │
  ├── StatefulMockVisionService ← mock（: IVisionProvider）
  └── StatefulMockActionExecutor ← mock（: IActionExecutor）
```

**核心原则**：引擎是真实的，只有 I/O 层被 mock。和 Python 一致。

### 2.3 数据流

```
SimulationRunner.Run()
  │
  └── while (!done)
        │
        └── StepOrchestrator.ExecuteStep(ctx)
              │
              ├── Step 3:  ctx.StateMachine.Step(ctx)
              │     └── DispatchHandler → HandleExecute / HandleBranch / ...
              │           ├── _currentStepContext.Vision ← StatefulMockVisionService
              │           └── _currentStepContext.Action ← StatefulMockActionExecutor
              │
              ├── Step 8:  BRANCH → ChildMgr.GetNextUnvisitedChild() → Stack.Push(child)
              ├── Step 9:  NODE_SELECT + DYNAMIC_MATCH → anti-loop
              ├── Step 10: FRAME_COMPLETE override
              ├── Step 12: MarkNodeVisited
              └── Step 13: Invalidate dynamic children cache
```

---

## 3. 类型定义

### 3.1 SimulationConfig

```csharp
namespace UniClaw.Core.Simulation;

/// <summary>仿真运行配置</summary>
public sealed record class SimulationConfig
{
    /// <summary>最大步数（安全上限，防止死循环）</summary>
    public int MaxSteps { get; init; } = 1000;

    /// <summary>栈最大深度</summary>
    public int MaxDepth { get; init; } = 10;

    /// <summary>true = handler 异常立即中断; false = 记录后继续（走 ErrorHandling 路径）</summary>
    public bool ThrowOnError { get; init; } = false;

    /// <summary>仿真延时（毫秒），用于模拟真实操作延迟。0 = 无延时</summary>
    public int SimulateDelayMs { get; init; } = 0;
}
```

### 3.2 SimulationResult

```csharp
/// <summary>仿真运行结果</summary>
public sealed record class SimulationResult(
    bool Success,
    string CompletionReason,
    int TotalSteps,
    double ElapsedSeconds,
    ImmutableArray<ActionRecord> ActionHistory,
    ImmutableArray<string> VisitedPages,
    TraversalState FinalState,
    Exception? Error = null)
{
    /// <summary>完成原因枚举值</summary>
    public static class Reasons
    {
        public const string AllVisited = "all_visited";
        public const string MaxSteps = "max_steps";
        public const string Error = "error";
        public const string AntiLoop = "anti_loop";
    }
}
```

---

## 4. SimulationRunner 实现

```csharp
public sealed class SimulationRunner
{
    private readonly SimulationConfig _config;
    private readonly StepOrchestrator _orchestrator;
    private readonly StepContext _stepCtx;
    private readonly TraversalRuntimeContext _ctx;
    private readonly StatefulMockVisionService _vision;
    private readonly StatefulMockActionExecutor _action;
    private readonly Stopwatch _stopwatch = new();
    private readonly List<string> _visitedPages = new();

    public SimulationRunner(
        StateFixture fixture,
        TraversalNode rootNode,
        SimpleNodeRegistry nodeRegistry,
        SimulationConfig? config = null)
    {
        _config = config ?? new SimulationConfig();

        // Mock 服务
        _vision = new StatefulMockVisionService(fixture);
        _action = new StatefulMockActionExecutor(_vision);

        // 真实 Context + FSM
        _ctx = new TraversalRuntimeContext(
            traceId: $"sim-{Guid.NewGuid():N}"[..12],
            maxDepth: _config.MaxDepth);
        _ctx.NodeStack.Push(rootNode);
        _ctx.CurrentFrame = rootNode;
        // 注册 root 自身的子节点到 VisitedChildren（初始为空集）
        // DynamicChildManager 在 Step 8 查询时会用到

        var fsm = new TraversalFSM(_ctx);

        // 组装 StepContext
        _stepCtx = new StepContext(
            Context: _ctx,
            StateMachine: fsm,
            Vision: _vision,
            Action: _action,
            ChildMgr: new DynamicChildManager(),
            NodeRegistry: nodeRegistry,
            Trace: new TraceCoordinator(),     // active=false → no-op
            SnapshotMgr: new PageSnapshotManager(),
            Stack: new NodeStackAdapter(_ctx, nodeRegistry));

        _orchestrator = new StepOrchestrator();
    }

    public SimulationResult Run()
    {
        _stopwatch.Start();

        try
        {
            for (int i = 0; i < _config.MaxSteps; i++)
            {
                // StepOrchestrator 自动:
                //   Step 3: fsm.Step(ctx)  → handlers 执行
                //   Step 8: BRANCH → push child
                //   Step 9: NODE_SELECT → anti-loop
                //   Step 10: FRAME_COMPLETE → override
                //   Step 12: MarkNodeVisited
                //   Step 14: 记录 step end
                var stepResult = _orchestrator.ExecuteStep(_stepCtx);

                // 记录页面变化
                RecordPageVisit();

                // 模拟延时
                if (_config.SimulateDelayMs > 0)
                    Thread.Sleep(_config.SimulateDelayMs);

                // 终止条件: 栈为空 + FrameCompleted
                if (stepResult.FrameCompleted && _ctx.NodeStack.Depth <= 1)
                    return Done(SimulationResult.Reasons.AllVisited, i + 1);

                // 终止条件: anti-loop 触发
                if (stepResult.AntiLoopTriggered)
                    return Done(SimulationResult.Reasons.AntiLoop, i + 1);
            }

            return Done(SimulationResult.Reasons.MaxSteps, _config.MaxSteps);
        }
        catch (Exception ex)
        {
            return Done(SimulationResult.Reasons.Error, _ctx.StepCount, ex);
        }
    }

    private void RecordPageVisit()
    {
        var page = _vision.CurrentPageId;
        if (_visitedPages.Count == 0 || _visitedPages[^1] != page)
            _visitedPages.Add(page);
    }

    private SimulationResult Done(string reason, int steps, Exception? error = null)
    {
        _stopwatch.Stop();
        return new SimulationResult(
            Success: error == null,
            CompletionReason: reason,
            TotalSteps: steps,
            ElapsedSeconds: _stopwatch.Elapsed.TotalSeconds,
            ActionHistory: _action.GetHistory().ToImmutableArray(),
            VisitedPages: _visitedPages.ToImmutableArray(),
            FinalState: _stepCtx.StateMachine.CurrentState,
            Error: error);
    }

    // ── 公开属性（测试断言用）──

    public StatefulMockVisionService Vision => _vision;
    public StatefulMockActionExecutor Action => _action;
    public TraversalRuntimeContext Context => _ctx;
    public TraversalState CurrentState => _stepCtx.StateMachine.CurrentState;
}
```

---

## 5. 使用示例

### 5.1 3 级页面遍历（替代当前 E2E 测试的手动代码）

```csharp
[Fact]
public void ThreeLevelTraversal_WithRunner()
{
    // 1. 构建 fixture
    var fixture = new StateFixtureBuilder()
        .Page("home", p => p
            .Name("HomeScreen")
            .Button("btn_settings", "Settings", 0.5, 0.9))
        .Page("settings", p => p
            .Name("SettingsScreen")
            .Button("btn_wifi", "Wi-Fi Settings", 0.5, 0.5)
            .BackButton("btn_back_s", 0.05, 0.05))
        .Page("wifi", p => p
            .Name("WiFiScreen")
            .Switch("sw_enable", "Enable Wi-Fi", 0.8, 0.3)
            .BackButton("btn_back_w", 0.05, 0.05))
        .Transition(t => t.Click("btn_settings").From("home").To("settings"))
        .Transition(t => t.Click("btn_wifi").From("settings").To("wifi"))
        .Transition(t => t.Click("btn_back_s").From("settings").To("home"))
        .Transition(t => t.Click("btn_back_w").From("wifi").To("settings"))
        .Build();

    // 2. 注册节点
    var registry = new SimpleNodeRegistry();
    registry.Register(Leaf("btn_settings", ClickAt(0.5, 0.9)));
    registry.Register(Leaf("btn_wifi", ClickAt(0.5, 0.5)));
    registry.Register(Leaf("btn_back_s", OperationType.Back));
    registry.Register(Leaf("btn_back_w", OperationType.Back));

    // 3. 根节点
    var root = new TraversalNode("root", "Root", NodeType.Container,
        new Operation(OperationType.NoAction),
        new ChildrenStrategy(ChildrenStrategyType.Static,
            StaticChildren: new List<string> { "btn_settings", "btn_wifi", "btn_back_s", "btn_back_w" }));

    // 4. 运行
    var runner = new SimulationRunner(fixture, root, registry);
    var result = runner.Run();

    // 5. 验证
    Assert.True(result.Success);
    Assert.Equal("all_visited", result.CompletionReason);
    Assert.Equal(new[] { "home", "settings", "wifi", "settings", "home" }, result.VisitedPages);
    Assert.True(result.ActionHistory.Length >= 4);
}
```

### 5.2 对比：手动驱动 vs Runner

| | 当前 E2E 测试 | 使用 Runner |
|---|---|---|
| 代码行数 | ~120 行 | ~30 行 |
| 手动 TransitionTo | 12 处 | 0 |
| 手动 NodeStack.Push | 4 处 | 0 |
| 手动 AddVisitedChild | 2 处 | 0 |
| 手动 DriveToExecuteAndStep | 每节点 1 次 | 0 |
| 可读性 | 淹没在状态管理里 | 声明式：fixture → runner → result |

---

## 6. 终止条件

| 条件 | CompletionReason | Success |
|------|-----------------|---------|
| `FrameCompleted` + 栈深度 ≤ 1 | `all_visited` | true |
| `AntiLoopTriggered` | `anti_loop` | true |
| 达到 `MaxSteps` | `max_steps` | false |
| 未捕获异常 | `error` | false |

**终止判断为什么是 `FrameCompleted + 栈深度 ≤ 1`？**
- `FrameCompleted` 表示当前子树的遍历已完成（HandleBranch 返回 FrameComplete 或 StepOrchestrator 触发 force-complete）
- 栈深度 ≤ 1 表示只剩根节点（root 本身也已完成）
- 两者同时满足 = 整棵遍历树完成

---

## 7. 与 Python 的差异

| 项目 | Python SimulationRunner | C# SimulationRunner |
|------|------------------------|---------------------|
| 行数 | 362 | ~150 |
| 引擎 | `GraphTraversalEngine` | `StepOrchestrator.ExecuteStep()` |
| 终止判断 | engine 内部判断 | `FrameCompleted + 栈深度 ≤ 1` |
| Plan 层 | 传入 `TraversalPlan`（支持修改） | 传入 `rootNode`（Plan→Node 由外部处理） |
| Trace | 真实 `TraceRecorder + MemoryStorage` | `TraceCoordinator(active=false)` no-op |
| 结果 | `SimulationResult` + `StructuredResult` | `SimulationResult`（合并） |
| Stateful 服务 | 构造器二选一（static/stateful） | 固定 stateful（`StatefulMockVisionService`） |

---

## 8. 不包含的内容

- **Plan 层集成**：Runner 接收 `TraversalNode`，不是 `TraversalPlan`。Plan → Node 树转换由 `PlanCompiler`（已存在）在外部完成
- **页面起始状态覆盖**：不支持 `set_path_context`（Python 的 path 注入）。fixture 始终从 `InitialPage` 开始
- **Scroll 仿真**：`SimulateDelayMs` 只模拟时间延迟，不模拟滚动行为
- **并发运行**：单线程，不支持并行仿真

---

## 9. 文件清单

### 新增文件

| # | 文件 | 说明 | 估算行数 |
|---|------|------|---------|
| 1 | `src/UniClaw.Core/Simulation/SimulationConfig.cs` | 配置 record | 20 |
| 2 | `src/UniClaw.Core/Simulation/SimulationResult.cs` | 结果 record | 25 |
| 3 | `src/UniClaw.Core/Simulation/SimulationRunner.cs` | 编排类 | 110 |

### 修改文件

| # | 文件 | 变更 | 影响 |
|---|------|------|------|
| 4 | `tests/.../Simulation/SimulationE2ETests.cs` | 用 Runner 重写现有 3 个测试，新增 1-2 个 Runner 测试 | 减少 ~80 行手动代码 |

### 文档更新

| # | 文件 | 变更 |
|---|------|------|
| 5 | `docs/system/layers/simulation.md` | 新增 SimulationRunner / SimulationConfig / SimulationResult 到类型清单 |

**总计**: ~155 行生产代码 + 测试重构（净减少 ~40 行）。

---

## 10. 与后续 Phase 的关系

```
Phase 2.3-sim ✅         StateFixture + StatefulMock* 服务
Phase 2.3-sim-runner     本文 — 自动化仿真驱动
     ↓
Phase 2.3c               HandleErrorHandling + HandlePopupHandling
                         └── Runner 可直接验证 error/popup handler 行为
     ↓
Phase 2.3b               HandleResultVerify + HandlePreconditionCheck
                         └── Runner 可验证视觉验证 + 前置检查的完整循环
```
