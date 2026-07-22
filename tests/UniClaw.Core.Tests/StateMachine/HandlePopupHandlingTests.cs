using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// HandlePopupHandling handler tests.
/// D4: PopupHandler pipeline delegation + FSM transition mapping.
/// </summary>
public class HandlePopupHandlingTests
{
    /// <summary>
    /// Helper: drives FSM to PopupHandling state.
    /// This requires a valid path through the transition matrix.
    /// </summary>
    private static TraversalFSM DriveToPopupHandling(TraversalRuntimeContext ctx)
    {
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.SetCurrentFrame(node);
        ctx.NodeStack.Push(node);
        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.PreconditionCheck);  // NodeSelect → PreconditionCheck
        fsm.TransitionTo(TraversalState.Execute);             // PreconditionCheck → Execute
        fsm.TransitionTo(TraversalState.ResultVerify);        // Execute → ResultVerify
        fsm.TransitionTo(TraversalState.PopupHandling);       // ResultVerify → PopupHandling
        return fsm;
    }

    /// <summary>
    /// Creates a StepContext with a custom PopupHandler and active TraceCoordinator.
    /// </summary>
    private static (StepContext stepCtx, InMemoryTraceStorage storage) CreateStepContextWithPopupHandler(
        TraversalRuntimeContext ctx, TraversalFSM fsm, PopupHandler popupHandler)
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
            Stack: null!,
            PopupHandler: popupHandler);
        return (stepCtx, storage);
    }

    /// <summary>
    /// Creates a PopupHandler with a custom executor that returns a specific result.
    /// </summary>
    private static PopupHandler CreatePopupHandlerWithResult(PopupHandlingResult result)
    {
        var executor = new PopupActionExecutor(
            permissionHook: _ => result,
            errorHook: _ => result,
            adHook: _ => result,
            dialogHook: _ => result,
            unknownHook: _ => result);
        return new PopupHandler(executor);
    }

    /// <summary>
    /// Sets up context with a popup-like PageAnalysis.
    /// </summary>
    private static void SetupPopupPageAnalysis(TraversalRuntimeContext ctx)
    {
        var items = ImmutableArray.Create(
            new MenuItem("Allow access", new Coordinate(0.5, 0.5), MenuItemType.Button),
            new MenuItem("ok", new Coordinate(0.7, 0.5), MenuItemType.Button),
            new MenuItem("Deny", new Coordinate(0.3, 0.5), MenuItemType.Button));
        var popupAnalysis = new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: items,
            IsPopup: true);
        ctx.SetCurrentPageAnalysis(popupAnalysis);
    }

    [Fact(DisplayName = "弹窗处理: PopupHandler返回Success=true → ResultVerify")]
    public async Task PopupHandling_Success_GoesToResultVerify()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctx);
        var fsm = DriveToPopupHandling(ctx);
        var handler = CreatePopupHandlerWithResult(new PopupHandlingResult(true, "auto_close", "Popup dismissed"));
        var (stepCtx, _) = CreateStepContextWithPopupHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.ResultVerify, result);
    }

    [Fact(DisplayName = "弹窗处理: PopupHandler返回Success=false → ErrorHandling")]
    public async Task PopupHandling_Failure_GoesToErrorHandling()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctx);
        var fsm = DriveToPopupHandling(ctx);
        var handler = CreatePopupHandlerWithResult(new PopupHandlingResult(false, "back_fallback", "No dismiss target"));
        var (stepCtx, _) = CreateStepContextWithPopupHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.ErrorHandling, result);
    }

    [Fact(DisplayName = "弹窗处理: PopupClassifier识别Permission弹窗 → dismiss策略")]
    public async Task PopupHandling_PermissionPopup_DismissStrategy()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctx);
        var fsm = DriveToPopupHandling(ctx);
        // Permission popup with "ok" available button → AutoClose
        var executor = new PopupActionExecutor(
            permissionHook: ctx2 =>
            {
                // Verify that the classification identified Permission type
                Assert.Equal(PopupType.Permission, ctx2.Classification.PopupType);
                Assert.Equal(DismissStrategy.AutoClose, ctx2.Classification.DismissStrategy);
                return new PopupHandlingResult(true, "auto_close", "Clicked ok for permission popup");
            });
        var handler = new PopupHandler(executor);
        var (stepCtx, _) = CreateStepContextWithPopupHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.ResultVerify, result);
    }

    [Fact(DisplayName = "弹窗处理: HandlerLifecycle trace记录")]
    public async Task PopupHandling_TraceTransitionsRecorded()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctx);
        var fsm = DriveToPopupHandling(ctx);
        var handler = CreatePopupHandlerWithResult(new PopupHandlingResult(true, "auto_close", "Popup dismissed"));
        var (stepCtx, storage) = CreateStepContextWithPopupHandler(ctx, fsm, handler);

        // Add HandlerTrace for lifecycle trace verification
        var recorder = new InMemoryTraceRecorder(storage);
        stepCtx = stepCtx with { HandlerTrace = new HandlerTraceWriter(recorder) };

        await fsm.StepAsync(stepCtx);

        // Verify HandlerLifecycle trace recorded instead of old StateTransition + Decision
        var executions = storage.GetExecutions();
        Assert.Contains(executions, e => e.Action == "handle_popup" && e.SpanType == SpanType.PopupHandling);
    }

    [Fact(DisplayName = "弹窗处理: 无StepContext → stub回退返回ResultVerify")]
    public async Task PopupHandling_NoStepContext_StubFallbackResultVerify()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToPopupHandling(ctx);

        var result = await fsm.StepAsync(); // No StepContext → stub fallback

        Assert.Equal(TraversalState.ResultVerify, result);
    }
}
