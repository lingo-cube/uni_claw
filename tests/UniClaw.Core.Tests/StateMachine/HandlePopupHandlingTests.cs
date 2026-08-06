using System.Collections.Immutable;
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
            Brain: new UniBrainService(new MockVisionProvider(), new MockTraversalAdvisor(), new MockTextUnderstanding()),
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

    // ── P1 (ANR 弹窗专项): PopupType.Anr 检测 + Wait 按钮策略 ──

    [Fact(DisplayName = "P1a: PopupDetector 识别 'Settings isn't responding' → PopupType.Anr (最高优先级)")]
    public void AnrText_Detect_ReturnsAnr()
    {
        var detector = new PopupDetector();

        Assert.Equal(PopupType.Anr, detector.Detect("Settings isn't responding"));
        Assert.Equal(PopupType.Anr, detector.Detect("Settings is not responding"));
        Assert.Equal(PopupType.Anr, detector.Detect("Launcher keeps stopping"));
        // 列表项文本不误报
        Assert.Equal(PopupType.Unknown, detector.Detect("Network & internet"));
        Assert.Equal(PopupType.Unknown, detector.Detect("T-Mobile"));
    }

    [Fact(DisplayName = "P1b: ANR 弹窗有 Wait 按钮 → AutoClose 点 Wait; 无按钮 → Back (等效 wait)")]
    public void AnrPopup_Classify_Strategy()
    {
        var classifier = new PopupClassifier();

        var withWait = classifier.Classify("Settings isn't responding", ["Close app", "Wait"]);
        Assert.Equal(PopupType.Anr, withWait.PopupType);
        Assert.Equal("wait", withWait.DismissTarget);
        Assert.Equal(DismissStrategy.AutoClose, withWait.DismissStrategy);
        Assert.Equal(UrgencyLevel.High, withWait.Urgency);
        Assert.Equal(BlockingType.Modal, withWait.BlockingType);

        var noWait = classifier.Classify("Settings isn't responding", ["Close app"]);
        Assert.Equal(DismissStrategy.Back, noWait.DismissStrategy); // 无 Wait → Back (等效 wait, 不杀应用)
    }

    [Fact(DisplayName = "P1c: ANR 弹窗执行 → 点 Wait 恢复应用 (不触发 Error auto-close)")]
    public void AnrPopup_Execute_ClicksWait()
    {
        var executor = new PopupActionExecutor();
        var classification = new PopupClassification(
            PopupType.Anr, "wait", DismissStrategy.AutoClose, UrgencyLevel.High, BlockingType.Modal);

        var result = executor.Execute(PopupType.Anr, new PopupContext(
            classification, new TraversalRuntimeContext("test-trace")));

        Assert.True(result.Success);
        Assert.Equal("auto_close", result.Action);
    }

    // ── fsm-matrix-hardening: 弹窗失败 → LastError (设计 §2.5) ──

    [Fact(DisplayName = "错误处理: 弹窗关闭失败 → LastError 带安全消息 (ErrorClassifier 无碰撞)")]
    public async Task PopupHandling_Failure_SetsLastError()
    {
        // 子用例 4a — 有 Classification → "Popup dismiss failed: dismiss_action=<action>"
        var ctx = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctx);
        var fsm = DriveToPopupHandling(ctx);
        var classification = new PopupClassification(
            PopupType.Dialog, "ok", DismissStrategy.AutoClose, UrgencyLevel.Medium, BlockingType.Modal);
        var handler = CreatePopupHandlerWithResult(
            new PopupHandlingResult(false, "Back", "No dismiss target", classification));
        var (stepCtx, _) = CreateStepContextWithPopupHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.ErrorHandling, result);
        var error = Assert.IsType<InvalidOperationException>(ctx.LastError);
        Assert.Equal("Popup dismiss failed: dismiss_action=Back", error.Message);

        // 子用例 4b — Classification 为 null → 回退消息 "Popup dismiss failed: action=<action>"
        var ctxB = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctxB);
        var fsmB = DriveToPopupHandling(ctxB);
        var handlerB = CreatePopupHandlerWithResult(
            new PopupHandlingResult(false, "Back", "No dismiss target", null));
        var (stepCtxB, _) = CreateStepContextWithPopupHandler(ctxB, fsmB, handlerB);

        var resultB = await fsmB.StepAsync(stepCtxB);

        Assert.Equal(TraversalState.ErrorHandling, resultB);
        var errorB = Assert.IsType<InvalidOperationException>(ctxB.LastError);
        Assert.Equal("Popup dismiss failed: action=Back", errorB.Message);

        // 消息不得含 ErrorClassifier 碰撞子串 (大小写不敏感)
        var forbidden = new[] { "Permission", "Error", "Timeout", "Ad", "Dialog", "Anr" };
        foreach (var sub in forbidden)
        {
            Assert.DoesNotContain(sub, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sub, errorB.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "错误处理: 弹窗关闭失败 → OnErrorAsync hook 触发前置条件成立 (FSM 级)")]
    public async Task PopupHandling_Failure_TriggersOnErrorAsyncHook()
    {
        // 引擎级 OnErrorAsync 在 stepResult.NextState == ErrorHandling && _ctx.LastError != null
        // 时触发 (TraversalEngine.StepAsync); 引擎级弹窗路径因 StatefulMockVisionService
        // 硬编码 IsPopup=false 无法在单测构造, 此处验证 FSM 级前置条件:
        // 失败关闭后 NextState==ErrorHandling 且 LastError 为正确异常类型。
        var ctx = new TraversalRuntimeContext("test-trace");
        SetupPopupPageAnalysis(ctx);
        var fsm = DriveToPopupHandling(ctx);
        var handler = CreatePopupHandlerWithResult(new PopupHandlingResult(false, "Back", "No dismiss target"));
        var (stepCtx, _) = CreateStepContextWithPopupHandler(ctx, fsm, handler);

        var result = await fsm.StepAsync(stepCtx);

        // FSM 级前置条件: 返回 ErrorHandling + LastError 已设置
        Assert.Equal(TraversalState.ErrorHandling, result);
        Assert.IsType<InvalidOperationException>(ctx.LastError);
        Assert.NotNull(ctx.LastError!.Message);
    }
}
