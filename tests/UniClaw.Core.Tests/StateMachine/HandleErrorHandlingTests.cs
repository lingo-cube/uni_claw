using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// HandleErrorHandling handler tests.
/// D3: 5-strategy RecoveryExecutor delegation + consecutive error tracking.
/// </summary>
public class HandleErrorHandlingTests
{
    /// <summary>
    /// Helper: drives FSM to ErrorHandling state.
    /// NodeSelect → PreconditionCheck → Execute → ErrorHandling
    /// </summary>
    private static TraversalFSM DriveToErrorHandling(TraversalRuntimeContext ctx)
    {
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);
        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.PreconditionCheck);  // NodeSelect → PreconditionCheck
        fsm.TransitionTo(TraversalState.Execute);             // PreconditionCheck → Execute
        fsm.TransitionTo(TraversalState.ErrorHandling);       // Execute → ErrorHandling
        return fsm;
    }

    /// <summary>
    /// Creates a StepContext with a custom ErrorHandler and active TraceCoordinator.
    /// </summary>
    private static (StepContext stepCtx, InMemoryTraceStorage storage) CreateStepContextWithErrorHandler(
        TraversalRuntimeContext ctx, TraversalFSM fsm, ErrorHandler errorHandler)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var stepCtx = new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Vision: new MockVisionProvider(),
            Action: null!,
            ChildMgr: null!,
            NodeRegistry: null!,
            Trace: trace,
            SnapshotMgr: null!,
            Stack: null!,
            ErrorHandler: errorHandler);
        return (stepCtx, storage);
    }

    /// <summary>
    /// Creates an ErrorHandler that always returns a specific strategy.
    /// </summary>
    private static ErrorHandler CreateStrategyForcingHandler(ErrorStrategy strategy, RecoveryOutcome outcome)
    {
        return new ErrorHandler(
            classify: _ => ErrorType.Unknown,
            selectStrategy: (_, _) => strategy,
            execute: (_, _) => new ErrorRecoveryResult(strategy, outcome, 0));
    }

    [Fact(DisplayName = "错误处理: Retry策略 → Execute + 连续错误递增")]
    public void ErrorHandling_Retry_GoesToExecute_IncrementConsecutive()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("timeout error");
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        Assert.Equal(0, ctx.ConsecutiveErrors);
        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.Execute, result);
        Assert.Equal(1, ctx.ConsecutiveErrors); // Incremented on Retry
    }

    [Fact(DisplayName = "错误处理: Backtrack策略 → NodeSelect + 连续错误重置")]
    public void ErrorHandling_Backtrack_GoesToNodeSelect_ResetConsecutive()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("element not found");
        ctx.IncrementConsecutiveErrors(); // Set to 1
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Backtrack, RecoveryOutcome.Success);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        Assert.Equal(1, ctx.ConsecutiveErrors);
        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.NodeSelect, result);
        Assert.Equal(0, ctx.ConsecutiveErrors); // Reset on non-Retry
    }

    [Fact(DisplayName = "错误处理: Skip策略 → Branch")]
    public void ErrorHandling_Skip_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("ui element error");
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Skip, RecoveryOutcome.Success);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.Branch, result);
        Assert.Equal(0, ctx.ConsecutiveErrors); // Reset on non-Retry
    }

    [Fact(DisplayName = "错误处理: Continue策略 → NodeSelect")]
    public void ErrorHandling_Continue_GoesToNodeSelect()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("unknown error");
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Continue, RecoveryOutcome.Success);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.NodeSelect, result);
        Assert.Equal(0, ctx.ConsecutiveErrors); // Reset on non-Retry
    }

    [Fact(DisplayName = "错误处理: Abort策略 → FrameComplete")]
    public void ErrorHandling_Abort_GoesToFrameComplete()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("app crash");
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Abort, RecoveryOutcome.Failure);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.FrameComplete, result);
        Assert.Equal(0, ctx.ConsecutiveErrors); // Reset on non-Retry
    }

    [Fact(DisplayName = "错误处理: RecoveryExecutor异常兜底 → Abort")]
    public void ErrorHandling_RecoveryExecutorFallback_Abort()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("test error");
        var fsm = DriveToErrorHandling(ctx);
        // Pipeline-level fallback: classify throws → Abort + Failure
        var handler = new ErrorHandler(
            classify: _ => throw new InvalidOperationException("Intentional test failure from classifier"),
            selectStrategy: new ErrorStrategySelector().SelectStrategy,
            execute: new RecoveryExecutor().Execute);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = fsm.Step(stepCtx);

        Assert.Equal(TraversalState.FrameComplete, result); // Abort → FrameComplete
    }

    [Fact(DisplayName = "错误处理: trace decisions记录每个策略")]
    public void ErrorHandling_TraceRecordedOnEachStrategy()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.LastError = new Exception("timeout error");
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var (stepCtx, storage) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        fsm.Step(stepCtx);

        // Verify trace decisions recorded
        var executions = storage.GetExecutions();
        Assert.Contains(executions, e => e.Action == "Retry→Execute");

        var errors = storage.GetErrors();
        Assert.NotEmpty(errors);
    }

    [Fact(DisplayName = "错误处理: 无StepContext → stub回退返回NodeSelect")]
    public void ErrorHandling_NoStepContext_StubFallbackNodeSelect()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToErrorHandling(ctx);

        var result = fsm.Step(); // No StepContext → stub fallback

        Assert.Equal(TraversalState.NodeSelect, result);
    }
}
