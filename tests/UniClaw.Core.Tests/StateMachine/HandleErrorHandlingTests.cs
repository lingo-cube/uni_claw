using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
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
        ctx.SetCurrentFrame(node);
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
            Brain: new UniBrainService(new MockVisionProvider(), new MockTraversalAdvisor(), new MockTextUnderstanding()),
            ScreenState: new DefaultScreenStateProvider(),
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
    public async Task ErrorHandling_Retry_GoesToExecute_IncrementConsecutive()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("timeout error"));
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        Assert.Equal(0, ctx.ConsecutiveErrors);
        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.Execute, result);
        Assert.Equal(1, ctx.ConsecutiveErrors); // Incremented on Retry
    }

    [Fact(DisplayName = "错误处理: Backtrack策略 → NodeSelect + 连续错误递增")]
    public async Task ErrorHandling_Backtrack_GoesToNodeSelect_IncrementsConsecutive()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("element not found"));
        ctx.IncrementConsecutiveErrors(); // Set to 1
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Backtrack, RecoveryOutcome.Success);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        Assert.Equal(1, ctx.ConsecutiveErrors);
        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.NodeSelect, result);
        Assert.Equal(2, ctx.ConsecutiveErrors); // Incremented under ALL strategies
    }

    [Fact(DisplayName = "错误处理: Skip策略 → Branch")]
    public async Task ErrorHandling_Skip_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("ui element error"));
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Skip, RecoveryOutcome.Success);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.Branch, result);
        Assert.Equal(1, ctx.ConsecutiveErrors); // Incremented under ALL strategies
    }

    [Fact(DisplayName = "错误处理: Continue策略 → NodeSelect")]
    public async Task ErrorHandling_Continue_GoesToNodeSelect()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("unknown error"));
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Continue, RecoveryOutcome.Success);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.NodeSelect, result);
        Assert.Equal(1, ctx.ConsecutiveErrors); // Incremented under ALL strategies
    }

    [Fact(DisplayName = "错误处理: Abort策略 → FrameComplete")]
    public async Task ErrorHandling_Abort_GoesToFrameComplete()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("app crash"));
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Abort, RecoveryOutcome.Failure);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.FrameComplete, result);
        Assert.Equal(1, ctx.ConsecutiveErrors); // Incremented under ALL strategies
    }

    [Fact(DisplayName = "错误处理: RecoveryExecutor异常兜底 → Abort")]
    public async Task ErrorHandling_RecoveryExecutorFallback_Abort()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("app crash"));
        var fsm = DriveToErrorHandling(ctx);
        // Pipeline-level fallback: classify throws → Abort + Failure
        var handler = new ErrorHandler(
            classify: _ => throw new InvalidOperationException("Intentional test failure from classifier"),
            selectStrategy: new ErrorStrategySelector().SelectStrategy,
            execute: new RecoveryExecutor().Execute);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.FrameComplete, result); // Abort → FrameComplete
    }

    [Fact(DisplayName = "错误处理: HandlerLifecycle trace + ErrorRecord双写")]
    public async Task ErrorHandling_TraceRecordedOnEachStrategy()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("timeout error"));
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var (stepCtx, storage) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        // Add HandlerTrace for lifecycle trace verification
        var recorder = new InMemoryTraceRecorder(storage);
        stepCtx = stepCtx with { HandlerTrace = new HandlerTraceWriter(recorder) };

        await fsm.StepAsync(stepCtx);

        // Verify HandlerLifecycle trace recorded (replaces old RecordStateDecisionAsync)
        var executions = storage.GetExecutions();
        Assert.Contains(executions, e => e.Action == "handle_error" && e.SpanType == SpanType.ErrorHandling);

        // Verify ErrorRecord still written (orthogonal — RecordErrorSpanAsync preserved)
        var errors = storage.GetErrors();
        Assert.NotEmpty(errors);
    }

    [Fact(DisplayName = "错误处理: 无StepContext → stub回退返回NodeSelect")]
    public async Task ErrorHandling_NoStepContext_StubFallbackNodeSelect()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToErrorHandling(ctx);

        var result = await fsm.StepAsync(); // No StepContext → stub fallback

        Assert.Equal(TraversalState.NodeSelect, result);
    }

    // ── fsm-matrix-hardening: LastError 生命周期 (设计 §2.4) ──

    [Fact(DisplayName = "错误处理: 成功恢复后 LastError 清零 (3条返回路径全覆盖)")]
    public async Task ErrorHandling_SuccessfulRecovery_ClearsLastError()
    {
        // 子用例 2a — 主返回路径 (Retry → Execute)
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetLastError(new Exception("test error"));
        var fsm = DriveToErrorHandling(ctx);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var (stepCtx, _) = CreateStepContextWithErrorHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.Execute, result);
        Assert.Null(ctx.LastError); // 修复前: 残留 "test error"

        // 子用例 2b — page-item 门限路径 (NodeFailedItems=5 + depth>1 → PressBack → FrameComplete)
        var ctxB = new TraversalRuntimeContext("test-trace");
        ctxB.SetLastError(new Exception("test error"));
        var fsmB = DriveToErrorHandling(ctxB);
        ctxB.NodeStack.Push(new TestTraversalNode("child", "sub-item", NodeType.LeafAction)); // depth > 1
        for (var i = 0; i < 5; i++)
        {
            ctxB.SetCurrentFrame(new TestTraversalNode($"failed_{i}", $"failed_{i}", NodeType.LeafAction));
            ctxB.IncrementNodeFailedItems(); // 5 个不同 frame → NodeFailedItems == 5
        }
        Assert.Equal(5, ctxB.NodeFailedItems);
        var handlerB = CreateStrategyForcingHandler(ErrorStrategy.Skip, RecoveryOutcome.Success);
        var actionB = FsmSimulationHarness.FakeAction(returns: true);
        var (stepCtxB, _) = FsmSimulationHarness.CreateStepContext(
            ctxB, fsmB, action: actionB, errorHandler: handlerB);

        var resultB = await fsmB.StepAsync(stepCtxB);

        Assert.Equal(TraversalState.FrameComplete, resultB); // Skip→Branch 被 page-item gate 抢占
        Assert.Null(ctxB.LastError); // 修复前: 残留

        // 子用例 2c — consecutive 门限路径 (ConsecutiveErrors=2 + depth>1 → 递增到3 → PressBack → FrameComplete)
        var ctxC = new TraversalRuntimeContext("test-trace");
        ctxC.SetLastError(new Exception("test error"));
        ctxC.IncrementConsecutiveErrors();
        ctxC.IncrementConsecutiveErrors();
        Assert.Equal(2, ctxC.ConsecutiveErrors);
        var fsmC = DriveToErrorHandling(ctxC);
        ctxC.NodeStack.Push(new TestTraversalNode("child", "sub-item", NodeType.LeafAction)); // depth > 1
        var handlerC = CreateStrategyForcingHandler(ErrorStrategy.Backtrack, RecoveryOutcome.Success);
        var actionC = FsmSimulationHarness.FakeAction(returns: true);
        var (stepCtxC, _) = FsmSimulationHarness.CreateStepContext(
            ctxC, fsmC, action: actionC, errorHandler: handlerC);

        var resultC = await fsmC.StepAsync(stepCtxC);

        Assert.Equal(TraversalState.FrameComplete, resultC); // 递增到3 → consecutive gate 触发
        Assert.Null(ctxC.LastError); // 修复前: 残留
    }

    // ── fsm-matrix-hardening: 递增收敛 (设计 §2.3) ──

    [Fact(DisplayName = "错误处理: 完整错误周期 ConsecutiveErrors 只 +1 (Execute handler catch 路径)")]
    public async Task ErrorHandling_FullCycle_ConsecutiveErrorsIncrementsOnce()
    {
        // 覆盖 Bug #2: Execute 抛异常 → HandleExecuteAsync catch → ErrorHandling (不递增,
        // 已移除) → 下次 StepAsync HandleErrorHandlingAsync 递增到 1 → Retry → Execute。
        // 修复前: catch(+1) + handler(+1) = 2。
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.Execute);
        // 换成携带 Click 操作的 TraversalNode — 使 Execute 经 OperationDispatcher 派发到 TapAsync
        ctx.NodeStack.Pop();
        var node = new TraversalNode(
            "root", "root", NodeType.Container,
            new Operation(OperationType.Click,
                new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        ctx.SetCurrentFrame(node);
        ctx.NodeStack.Push(node);

        var action = new MockActionExecutor
        {
            NextResult = true,
            ThrowsOnNext = new TimeoutException("ADB timeout")
        };
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var (stepCtx, _) = FsmSimulationHarness.CreateStepContext(ctx, fsm, action: action, errorHandler: handler);

        Assert.Equal(0, ctx.ConsecutiveErrors);

        // 第一次: HandleExecuteAsync catch → ErrorHandling (不递增)
        var first = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.ErrorHandling, first);
        Assert.Equal(0, ctx.ConsecutiveErrors); // 修复前: 此处已 +1

        // 第二次: HandleErrorHandlingAsync → 唯一递增点 → Retry → Execute
        var second = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.Execute, second);
        Assert.Equal(1, ctx.ConsecutiveErrors); // 修复前: 2
    }

    [Fact(DisplayName = "错误处理: 完整错误周期 ConsecutiveErrors 只 +1 (StepAsync catch 异常路由路径)")]
    public async Task ErrorHandling_FullCycle_UncaughtException_IncrementsOnce()
    {
        // 变体 — 异常路由路径 (经 StepAsync catch):
        // ThrowingPreconditionChecker 抛出的 TimeoutException 无内部 try 包裹,
        // 直达 StepAsync catch → SetLastError(不递增) → ErrorHandling。
        // (Execute handler 的全部异常路径均被其内部 catch 捕获, 无法自然直达
        //  StepAsync catch; PreconditionCheck 是可达 ErrorHandling 的状态中唯一
        //  无内部 catch 的 handler。)
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.PreconditionCheck);
        var handler = CreateStrategyForcingHandler(ErrorStrategy.Retry, RecoveryOutcome.RetryScheduled);
        var checker = new ThrowingPreconditionChecker();
        var (stepCtx, _) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, errorHandler: handler, preconditionChecker: checker);

        // 第一次: StepAsync catch 路由到 ErrorHandling (不递增)
        var first = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.ErrorHandling, first);
        Assert.Equal(0, ctx.ConsecutiveErrors); // 修复前: catch 块已 +1

        // 第二次: HandleErrorHandlingAsync → 唯一递增点 → Retry → Execute
        var second = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.Execute, second);
        Assert.Equal(1, ctx.ConsecutiveErrors); // 修复前: 2
    }

    /// <summary>
    /// PreconditionChecker — CheckAsync 抛 TimeoutException (未捕获 → 经 StepAsync catch 路由)。
    /// </summary>
    private sealed class ThrowingPreconditionChecker : IPreconditionChecker
    {
        public Task<bool> CheckAsync(TraversalRuntimeContext context, CancellationToken ct = default)
            => throw new TimeoutException("ADB timeout");
    }
}
