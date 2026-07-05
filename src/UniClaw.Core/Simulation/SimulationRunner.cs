using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation;

/// <summary>
/// SimulationRunner — 自动化仿真驱动层。
/// 复用真实 StepOrchestrator + TraversalFSM + TraversalRuntimeContext，
/// 仅注入 StatefulMockVisionService + StatefulMockActionExecutor 替代真实 I/O。
/// Run() 循环调用 StepOrchestrator.ExecuteStep(ctx) 直到终止条件满足。
/// </summary>
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

        var fsm = new TraversalFSM(_ctx);

        // 组装 StepContext
        _stepCtx = new StepContext(
            Context: _ctx,
            StateMachine: fsm,
            Vision: _vision,
            Action: _action,
            ChildMgr: new DynamicChildManager(nodeRegistry),
            NodeRegistry: nodeRegistry,
            Trace: new TraceCoordinator(),
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
                var stepResult = _orchestrator.ExecuteStep(_stepCtx);

                // After a leaf executes (Execute → ResultVerify), pop the leaf
                // so BRANCH interception in the next step runs with parent as CurrentFrame
                if (stepResult.NextState == TraversalState.ResultVerify
                    && _ctx.NodeStack.Depth > 1
                    && _ctx.CurrentFrame?.ChildrenStrategy.Type == ChildrenStrategyType.None)
                {
                    _ctx.NodeStack.Pop();
                }

                // StepOrchestrator doesn't update CurrentFrame — sync from stack top
                _ctx.CurrentFrame = _ctx.NodeStack.Peek()?.Node;

                // BRANCH interception pushed a child — force NodeSelect so the
                // child goes through Execute (not straight to FrameComplete)
                if (stepResult.ChildPushed
                    && _stepCtx.StateMachine.CanTransitionTo(TraversalState.NodeSelect))
                {
                    _stepCtx.StateMachine.TransitionTo(TraversalState.NodeSelect);
                }

                RecordPageVisit();

                // Frame completed at depth > 1: pop back to parent
                if (stepResult.FrameCompleted && _ctx.NodeStack.Depth > 1)
                    _ctx.NodeStack.Pop();

                if (_config.SimulateDelayMs > 0)
                    Thread.Sleep(_config.SimulateDelayMs);

                // Terminate: all nodes visited, back at root
                if (stepResult.FrameCompleted && _ctx.NodeStack.Depth <= 1)
                    return Done(SimulationResult.Reasons.AllVisited, i + 1);

                // Terminate: anti-loop triggered
                if (stepResult.AntiLoopTriggered)
                    return Done(SimulationResult.Reasons.AntiLoop, i + 1);
            }

            return Done(SimulationResult.Reasons.MaxSteps, _config.MaxSteps);
        }
        catch (Exception ex) when (!_config.ThrowOnError)
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
            Success: reason is SimulationResult.Reasons.AllVisited or SimulationResult.Reasons.AntiLoop,
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
