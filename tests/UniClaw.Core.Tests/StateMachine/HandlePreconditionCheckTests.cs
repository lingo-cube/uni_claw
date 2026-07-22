using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// HandlePreconditionCheck handler tests.
/// D1: Assume pass with explicit trace logging.
/// </summary>
public class HandlePreconditionCheckTests
{
    /// <summary>
    /// Helper: drives FSM to PreconditionCheck state.
    /// NodeSelect → PreconditionCheck (stack has node)
    /// </summary>
    private static TraversalFSM DriveToPreconditionCheck(TraversalRuntimeContext ctx)
    {
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.SetCurrentFrame(node);
        ctx.NodeStack.Push(node);
        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.PreconditionCheck); // NodeSelect → PreconditionCheck
        return fsm;
    }

    /// <summary>
    /// Creates a StepContext with an active TraceCoordinator (for verifying trace recording).
    /// </summary>
    private static (StepContext stepCtx, InMemoryTraceStorage storage) CreateStepContextWithTrace(
        TraversalRuntimeContext ctx, TraversalFSM fsm)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var stepCtx = new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Vision: new MockVisionProvider(),
            ScreenState: new DefaultScreenStateProvider(),
            Action: null!,
            ChildMgr: null!,
            NodeRegistry: null!,
            Trace: trace,
            SnapshotMgr: null!,
            Stack: null!);
        return (stepCtx, storage);
    }

    [Fact(DisplayName = "前置检查: assume pass → Execute transition")]
    public async Task PreconditionCheck_AssumePass_GoesToExecute()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToPreconditionCheck(ctx);

        var result = await fsm.StepAsync();

        Assert.Equal(TraversalState.Execute, result);
    }

    [Fact(DisplayName = "前置检查: assume pass → trace decision recorded")]
    public async Task PreconditionCheck_AssumePass_TraceDecisionRecorded()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToPreconditionCheck(ctx);
        var (stepCtx, storage) = CreateStepContextWithTrace(ctx, fsm);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.Execute, result);

        // Verify trace decision was recorded
        var executions = storage.GetExecutions();
        Assert.NotEmpty(executions);
        Assert.Contains(executions, e => e.Action == "precondition_assume_pass");
    }

    [Fact(DisplayName = "前置检查: 无StepContext → stub回退仍返回Execute")]
    public async Task PreconditionCheck_NoStepContext_StillReturnsExecute()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToPreconditionCheck(ctx);

        // Step() without StepContext — no trace but still returns Execute
        var result = await fsm.StepAsync();

        Assert.Equal(TraversalState.Execute, result);
    }
}
