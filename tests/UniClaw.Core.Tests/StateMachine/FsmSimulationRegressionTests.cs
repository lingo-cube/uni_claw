using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// Deterministic regression tests for FSM flow bugs discovered during
/// 60+ real-device integration runs.  All tests use
/// <see cref="FsmSimulationHarness"/> — no emulator, no AI, &lt;1ms each.
/// </summary>
public sealed class FsmSimulationRegressionTests
{
    [Fact(DisplayName = "FSM 回归: ErrorHandling 循环闸门 — 5 次失败触发 PressBack")]
    public async Task ErrorHandling_FiveFailuresOnSubPage_PressBackAndFrameComplete()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.NodeStack.Push(new TestTraversalNode("child", "sub-item", NodeType.LeafAction));
        ctx.NodeStack.Push(new TestTraversalNode("root", "root", NodeType.Container));
        ctx.SetCurrentFrame(ctx.NodeStack.Peek()!.Node);
        ctx.SetLastError(new InvalidOperationException("safety deny"));

        var analyzerCalls = 0;
        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.ErrorHandling);
        var handler = FsmSimulationHarness.StrategyForcingHandler(ErrorStrategy.Backtrack);
        var action = FsmSimulationHarness.FakeAction(returns: true);
        // Success cycle: every AnalyzeCurrentPageAsync returns a fresh page →
        // verification_passed on first check (resets ConsecutiveErrors).
        var successAnalyzer = new CallbackPageAnalyzer(() =>
            FsmSimulationHarness.Page($"success_page_{Interlocked.Increment(ref analyzerCalls)}"));
        var (stepCtx, storage) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, action: action, errorHandler: handler, pageAnalyzer: successAnalyzer);

        // Interleaved deny/success pattern (the scenario the page-item gate
        // exists for): each failed item increments NodeFailedItems (distinct
        // frame per iteration), each verified success resets ConsecutiveErrors
        // so the consecutive gate (≥3) never fires before the item gate (≥5).
        for (var i = 0; i < 5; i++)
        {
            ctx.SetCurrentFrame(new TestTraversalNode($"item_{i}", $"item_{i}", NodeType.LeafAction));
            FsmSimulationHarness.ReenterErrorHandling(fsm);
            await fsm.StepAsync(stepCtx);
            if (fsm.CurrentState == TraversalState.FrameComplete)
                break;

            // Successful sibling item (legal path from NodeSelect):
            // PreconditionCheck → Execute → ResultVerify → verification_passed.
            fsm.TransitionTo(TraversalState.PreconditionCheck);
            fsm.TransitionTo(TraversalState.Execute);
            fsm.TransitionTo(TraversalState.ResultVerify);
            await fsm.StepAsync(stepCtx);
        }

        Assert.Equal(TraversalState.FrameComplete, fsm.CurrentState);
        Assert.Contains(storage.GetExecutions(),
            d => d.Action == "error_recovery_page_item_limit_5");
    }

    [Fact(DisplayName = "FSM 回归: Backtrack 不重置连续错误计数")]
    public async Task ErrorHandling_ThreeBacktracks_ConsecutiveErrorsIsThree()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.NodeStack.Push(new TestTraversalNode("child", "sub-item", NodeType.LeafAction));
        ctx.NodeStack.Push(new TestTraversalNode("root", "root", NodeType.Container));
        ctx.SetCurrentFrame(ctx.NodeStack.Peek()!.Node);
        ctx.SetLastError(new InvalidOperationException("deny"));

        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.ErrorHandling);
        var handler = FsmSimulationHarness.StrategyForcingHandler(ErrorStrategy.Backtrack);
        var action = FsmSimulationHarness.FakeAction(returns: true);
        var (stepCtx, storage) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, action: action, errorHandler: handler);

        // Two Backtracks: counter accumulates (Backtrack must NOT reset it).
        for (var i = 0; i < 2; i++)
        {
            FsmSimulationHarness.ReenterErrorHandling(fsm);
            await fsm.StepAsync(stepCtx);
        }
        Assert.Equal(2, ctx.ConsecutiveErrors);

        // Third error: consecutive gate fires → PressBack → FrameComplete.
        FsmSimulationHarness.ReenterErrorHandling(fsm);
        var result = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.FrameComplete, result);
        Assert.Contains(storage.GetExecutions(),
            d => d.Action == "error_recovery_press_back");
    }

    [Fact(DisplayName = "FSM 回归: ResultVerify 弹窗检测单次重试")]
    public async Task ResultVerify_PopupDetectedSingleRetry_GoesToPopupHandling()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(FsmSimulationHarness.Page("item_a"));
        ctx.SetCurrentFrame(new TestTraversalNode("root", "root", NodeType.Container));

        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.ResultVerify);
        var callCount = 0;
        var popupAnalyzer = new CallbackPageAnalyzer(() =>
        {
            callCount++;
            return callCount switch
            {
                1 => FsmSimulationHarness.Page("item_a"),
                2 => FsmSimulationHarness.PopupPage("Allow access"),
                _ => null,
            };
        });
        var (stepCtx, _) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, pageAnalyzer: popupAnalyzer);

        var result = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.PopupHandling, result);
        Assert.Equal(2, callCount);
    }

    [Fact(DisplayName = "FSM 回归: ResultVerify 无变化后直接 Branch")]
    public async Task ResultVerify_NoChangeNoPopup_ReturnsBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(FsmSimulationHarness.Page("item_a"));
        ctx.SetCurrentFrame(new TestTraversalNode("root", "root", NodeType.Container));

        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.ResultVerify);
        var unchangedAnalyzer = new CallbackPageAnalyzer(() =>
            FsmSimulationHarness.Page("item_a"));
        var (stepCtx, _) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, pageAnalyzer: unchangedAnalyzer);

        var result = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.Branch, result);
    }

    [Fact(DisplayName = "FSM 回归: Execute 成功 action → ResultVerify")]
    public async Task Execute_SuccessfulAction_GoesToResultVerify()
    {
        var ctx = new TraversalRuntimeContext("test-trace");

        // DriveTo first, then swap the harness stub node for a real
        // TraversalNode carrying a Click operation so Execute dispatches
        // through OperationDispatcher → TapAsync.
        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.Execute);
        ctx.NodeStack.Pop();
        var node = new TraversalNode(
            "root", "root", NodeType.Container,
            new Operation(OperationType.Click,
                new Target(TargetType.Coordinate, new Coordinate(0.5, 0.5))),
            new ChildrenStrategy(ChildrenStrategyType.None));
        ctx.SetCurrentFrame(node);
        ctx.NodeStack.Push(node);
        ctx.SetCurrentPageAnalysis(FsmSimulationHarness.Page("About phone"));

        var action = new FakeActionRecorder(returns: true);
        var (stepCtx, _) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, action: action);

        var result = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.ResultVerify, result);
        Assert.Single(action.GetHistory());
        Assert.True(action.GetHistory()[0].Success);
    }

    [Fact(DisplayName = "FSM 回归: PreconditionChecker 门禁 → ErrorHandling")]
    public async Task PreconditionCheck_CheckerReturnsFalse_GoesToErrorHandling()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.NodeStack.Push(new TestTraversalNode("root", "root", NodeType.Container));
        ctx.SetCurrentFrame(ctx.NodeStack.Peek()!.Node);

        var fsm = FsmSimulationHarness.DriveTo(ctx, TraversalState.PreconditionCheck);
        var checker = new FailingPreconditionChecker();
        var (stepCtx, _) = FsmSimulationHarness.CreateStepContext(
            ctx, fsm, preconditionChecker: checker);

        var result = await fsm.StepAsync(stepCtx);
        Assert.Equal(TraversalState.ErrorHandling, result);
    }

    [Fact(DisplayName = "FSM 回归: AI 空响应不重试 — IsTransient 返回 false")]
    public void PageAnalyzer_IsTransient_EmptyResponse_ReturnsFalse()
    {
        var ex = new DomainValidationException(
            "Content", "",
            "analyze_visual model returned empty response — structural failure, will not retry.");

        var analyzer = new PageAnalyzer(
            new FailingModelProvider(),
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FakeScreenCapture());

        var method = typeof(PageAnalyzer).GetMethod("IsTransient",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var isTransient = (bool)method!.Invoke(analyzer, [ex])!;
        Assert.False(isTransient);
    }

    private sealed class CallbackPageAnalyzer : IPageAnalyzer
    {
        private readonly Func<PageAnalysis?> _cb;
        public CallbackPageAnalyzer(Func<PageAnalysis?> cb) => _cb = cb;
        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default) =>
            Task.FromResult(_cb());
        public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default) =>
            Task.FromResult<AppEntryPoint?>(null);
        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis p, string e, string? n = null, CancellationToken ct = default) =>
            Task.FromResult(new PageTypeVerification(true, 1.0));
    }

    private sealed class FailingPreconditionChecker : IPreconditionChecker
    {
        public Task<bool> CheckAsync(TraversalRuntimeContext context, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeActionRecorder : IActionExecutor
    {
        private readonly bool _returns;
        private readonly List<ActionRecord> _history = [];
        public FakeActionRecorder(bool returns) => _returns = returns;
        public Task<bool> TapAsync(double x, double y, CancellationToken ct = default) =>
            Record(new ActionRecord("click", DateTimeOffset.UtcNow, new Dictionary<string, object> { ["x"] = x, ["y"] = y }, _returns));
        public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int d, CancellationToken ct = default) =>
            Record(new ActionRecord("scroll", DateTimeOffset.UtcNow, new Dictionary<string, object> { ["sx"] = sx, ["sy"] = sy, ["ex"] = ex, ["ey"] = ey }, _returns));
        public Task<bool> PressBackAsync(CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<bool> InputTextAsync(string t, CancellationToken ct = default) =>
            Task.FromResult(_returns);
        public Task<bool> LongPressAsync(double x, double y, int d, CancellationToken ct = default) =>
            Task.FromResult(_returns);
        public Task WaitAsync(int ms, CancellationToken ct = default) =>
            Task.CompletedTask;
        public List<ActionRecord> GetHistory() => _history;
        private Task<bool> Record(ActionRecord r) { _history.Add(r); return Task.FromResult(_returns); }
    }

    private sealed class FailingModelProvider : IModelProvider
    {
        public string ProviderId => "failing";
        public Task<ModelResponse> CompleteTextAsync(ModelRequest r, CancellationToken ct = default) =>
            Task.FromResult(new ModelResponse("", ProviderId, "text", 0, 0, 0));
        public Task<ModelResponse> CompleteVisionAsync(ModelRequest r, byte[] d, CancellationToken ct = default) =>
            Task.FromResult(new ModelResponse("", ProviderId, "vision", 0, 0, 0));
        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest r, byte[] d, CancellationToken ct = default) =>
            CompleteVisionAsync(r, d, ct);
    }

    private sealed class FakeScreenCapture : IScreenCapture
    {
        public Task<byte[]> CaptureAsync(CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public Task<RawScreenBuffer> CaptureRawAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Raw capture not supported in test fake");
    }
}
