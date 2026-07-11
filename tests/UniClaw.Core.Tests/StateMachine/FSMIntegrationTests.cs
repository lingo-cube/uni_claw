using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// FSM Integration Tests — complete traversal cycle through all 8 states.
/// Verifies that all 4 implemented handlers produce correct transitions
/// and that trace is populated correctly.
/// </summary>
public class FSMIntegrationTests
{
    /// <summary>
    /// Creates a TraversalNode for testing.
    /// </summary>
    private static TraversalNode CreateNode(string id, Operation operation,
        NodeType nodeType = NodeType.LeafAction)
        => new(id, id, nodeType, operation,
            new ChildrenStrategy(ChildrenStrategyType.None));

    /// <summary>
    /// Creates a StepContext with all dependencies for integration testing.
    /// </summary>
    private static (StepContext stepCtx, InMemoryTraceStorage storage) CreateFullStepContext(
        TraversalRuntimeContext ctx, TraversalFSM fsm)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var vision = new MockVisionProvider();
        vision.NextResult = new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray.Create(
                new MenuItem("btn1", new Coordinate(0.5, 0.5))));
        var stepCtx = new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Vision: vision,
            Action: null!,
            ChildMgr: null!,
            NodeRegistry: null!,
            Trace: trace,
            SnapshotMgr: null!,
            Stack: null!);
        return (stepCtx, storage);
    }

    [Fact(DisplayName = "FSM集成: 8状态完整遍历路径 NodeSelect→PreconditionCheck→Execute→ResultVerify→Branch")]
    public void FSM_Integration_FullCycle_AllHandlersImplemented()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var node = CreateNode("root", new Operation(OperationType.NoAction), NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);
        ctx.SetCurrentPageAnalysis(new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray.Create(
                new MenuItem("btn1", new Coordinate(0.5, 0.5)))));

        var fsm = new TraversalFSM(ctx);

        // Step 1: NodeSelect → PreconditionCheck (stack has node)
        var (stepCtx, _) = CreateFullStepContext(ctx, fsm);
        var step1 = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.PreconditionCheck, step1);

        // Step 2: PreconditionCheck → Execute (assume pass)
        stepCtx = CreateFullStepContext(ctx, fsm).stepCtx;
        var step2 = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.Execute, step2);

        // Step 3: Execute → ResultVerify (NoAction → skip execution)
        stepCtx = CreateFullStepContext(ctx, fsm).stepCtx;
        var step3 = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.ResultVerify, step3);

        // Step 4: ResultVerify → Branch (page changed or all retries fail → Branch)
        stepCtx = CreateFullStepContext(ctx, fsm).stepCtx;
        var step4 = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.Branch, step4);
    }

    [Fact(DisplayName = "FSM集成: ErrorHandling路径 — 各策略映射")]
    public void FSM_Integration_ErrorHandling_StrategyMapping()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var node = CreateNode("root", new Operation(OperationType.NoAction), NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);
        ctx.LastError = new Exception("network error");

        var fsm = new TraversalFSM(ctx);
        // Drive to ErrorHandling via valid path:
        // NodeSelect → PreconditionCheck → Execute → ErrorHandling
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);
        fsm.TransitionTo(TraversalState.ErrorHandling); // Execute → ErrorHandling

        // Test Continue strategy → NodeSelect
        var handler = new ErrorHandler(
            classify: _ => ErrorType.Unknown,
            selectStrategy: (_, _) => ErrorStrategy.Continue,
            execute: (_, _) => new ErrorRecoveryResult(ErrorStrategy.Continue, RecoveryOutcome.Success, 0));
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var stepCtx = new StepContext(
            Context: ctx, StateMachine: fsm, Vision: new MockVisionProvider(),
            Action: null!, ChildMgr: null!, NodeRegistry: null!,
            Trace: trace, SnapshotMgr: null!, Stack: null!,
            ErrorHandler: handler);

        var result = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.NodeSelect, result); // Continue → NodeSelect

        // Verify trace recorded
        var executions = storage.GetExecutions();
        Assert.Contains(executions, e => e.Action == "Continue→NodeSelect");
    }

    [Fact(DisplayName = "FSM集成: PopupHandling路径 — dismiss成功→ResultVerify")]
    public void FSM_Integration_PopupHandling_Cycle()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var node = CreateNode("root", new Operation(OperationType.NoAction), NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);

        var fsm = new TraversalFSM(ctx);
        // Drive to PopupHandling via valid path:
        // NodeSelect → PreconditionCheck → Execute → ResultVerify → PopupHandling
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);
        fsm.TransitionTo(TraversalState.ResultVerify);
        fsm.TransitionTo(TraversalState.PopupHandling); // ResultVerify → PopupHandling

        // Popup dismissed successfully → ResultVerify
        var executor = new PopupActionExecutor(
            permissionHook: _ => new PopupHandlingResult(true, "auto_close", "Popup dismissed"));
        var handler = new PopupHandler(executor);

        // Set popup page analysis on context
        var popupItems = ImmutableArray.Create(
            new MenuItem("Allow access", new Coordinate(0.5, 0.5), MenuItemType.Button));
        ctx.SetCurrentPageAnalysis(new PageAnalysis(
            Direction.Left, Direction.Top, Items: popupItems, IsPopup: true));

        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var stepCtx = new StepContext(
            Context: ctx, StateMachine: fsm, Vision: new MockVisionProvider(),
            Action: null!, ChildMgr: null!, NodeRegistry: null!,
            Trace: trace, SnapshotMgr: null!, Stack: null!,
            PopupHandler: handler);

        var result = fsm.Step(stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);

        // Verify trace recorded state transition
        var transitions = storage.GetTransitions();
        Assert.Contains(transitions, t => t.FromState == "PopupHandling" && t.ToState == "ResultVerify");
    }

    [Fact(DisplayName = "FSM集成: Trace在各handler transition正确填充")]
    public void FSM_Integration_TracePopulatedCorrectly()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var node = CreateNode("root", new Operation(OperationType.NoAction), NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);
        ctx.SetCurrentPageAnalysis(new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray.Create(
                new MenuItem("btn1", new Coordinate(0.5, 0.5)))));

        var fsm = new TraversalFSM(ctx);

        // Use a single storage for all steps to accumulate trace
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var vision = new MockVisionProvider();
        vision.NextResult = new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray.Create(
                new MenuItem("btn1", new Coordinate(0.5, 0.5))));
        var stepCtx = new StepContext(
            Context: ctx, StateMachine: fsm, Vision: vision,
            Action: null!, ChildMgr: null!, NodeRegistry: null!,
            Trace: trace, SnapshotMgr: null!, Stack: null!);

        // Step 1: NodeSelect → PreconditionCheck (NodeSelect handler: stack has node → PreconditionCheck)
        fsm.Step(stepCtx);
        Assert.Equal(TraversalState.PreconditionCheck, fsm.CurrentState);

        // Step 2: PreconditionCheck → Execute (trace: precondition_assume_pass)
        fsm.Step(stepCtx);
        Assert.Equal(TraversalState.Execute, fsm.CurrentState);

        // Verify precondition_assume_pass was recorded in executions
        Assert.NotEmpty(storage.GetExecutions());
        Assert.Contains(storage.GetExecutions(), e => e.Action == "precondition_assume_pass");
    }

    [Fact(DisplayName = "FSM集成: HandleFrameComplete不需要增强(D5 — minimal实现正确)")]
    public void FSM_Integration_HandleFrameComplete_MinimalCorrect()
    {
        // D5: HandleFrameComplete minimal implementation is correct.
        // Stack pop + frame teardown is in StepOrchestrator, not in FSM handler.
        // Handler only decides transition: NodeSelect (continue) or ErrorHandling (error).
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = new TraversalFSM(ctx);

        // Drive to FrameComplete via valid path:
        // NodeSelect → Branch → FrameComplete (requires depth > 1)
        var node = CreateNode("root", new Operation(OperationType.NoAction), NodeType.Container);
        ctx.CurrentFrame = node;
        ctx.NodeStack.Push(node);

        fsm.TransitionTo(TraversalState.Branch);
        fsm.TransitionTo(TraversalState.FrameComplete); // Branch → FrameComplete

        var result = fsm.Step();
        Assert.Equal(TraversalState.NodeSelect, result);

        // Verify: handler returns NodeSelect (correct minimal behavior)
        // Stack pop is handled by StepOrchestrator step 10, not by FSM handler
    }
}
