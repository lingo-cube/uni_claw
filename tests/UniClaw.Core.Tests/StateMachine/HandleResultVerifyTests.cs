using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// HandleResultVerify handler tests.
/// D2: 3-round retry + vision correction + popup detection.
/// </summary>
public class HandleResultVerifyTests
{
    /// <summary>
    /// Helper: drives FSM to ResultVerify state.
    /// NodeSelect → PreconditionCheck → Execute → ResultVerify
    /// </summary>
    private static TraversalFSM DriveToResultVerify(TraversalRuntimeContext ctx)
    {
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.SetCurrentFrame(node);
        ctx.NodeStack.Push(node);
        var fsm = new TraversalFSM(ctx);
        fsm.TransitionTo(TraversalState.PreconditionCheck);  // NodeSelect → PreconditionCheck
        fsm.TransitionTo(TraversalState.Execute);             // PreconditionCheck → Execute
        fsm.CurrentState = TraversalState.ResultVerify;       // Execute → ResultVerify (bypass Step)
        return fsm;
    }

    /// <summary>
    /// Creates a basic PageAnalysis for fingerprint comparison.
    /// </summary>
    private static PageAnalysis CreatePageAnalysis(string[] itemNames)
    {
        var items = itemNames.Select(name =>
            new MenuItem(name, new Coordinate(0.5, 0.5))).ToImmutableArray();
        return new PageAnalysis(Direction.Left, Direction.Top, Items: items);
    }

    /// <summary>
    /// Creates a PageAnalysis with popup keywords in item names.
    /// </summary>
    private static PageAnalysis CreatePopupPageAnalysis(string popupKeyword, bool isPopup = false)
    {
        var items = ImmutableArray.Create(
            new MenuItem(popupKeyword, new Coordinate(0.5, 0.5)),
            new MenuItem("normal_item", new Coordinate(0.3, 0.3)));
        return new PageAnalysis(Direction.Left, Direction.Top, Items: items, IsPopup: isPopup);
    }

    /// <summary>
    /// Creates a StepContext with an active TraceCoordinator and configurable MockVisionProvider.
    /// </summary>
    private static (StepContext stepCtx, InMemoryTraceStorage storage, MockVisionProvider vision) CreateStepContextWithTrace(
        TraversalRuntimeContext ctx, TraversalFSM fsm)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var vision = new MockVisionProvider();
        var snapshotMgr = new PageSnapshotManager();
        var stepCtx = new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Vision: vision,
            Action: null!,
            ChildMgr: null!,
            NodeRegistry: null!,
            Trace: trace,
            SnapshotMgr: snapshotMgr,
            Stack: null!);
        return (stepCtx, storage, vision);
    }

    /// <summary>
    /// Creates a StepContext with a sequential vision provider and active TraceCoordinator.
    /// </summary>
    private static (StepContext stepCtx, InMemoryTraceStorage storage) CreateStepContextWithSequentialVision(
        TraversalRuntimeContext ctx, TraversalFSM fsm, SequentialVisionProvider seqVision)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var snapshotMgr = new PageSnapshotManager();
        var stepCtx = new StepContext(
            Context: ctx, StateMachine: fsm, Vision: seqVision,
            Action: null!, ChildMgr: null!, NodeRegistry: null!,
            Trace: trace, SnapshotMgr: snapshotMgr, Stack: null!);
        return (stepCtx, storage);
    }

    [Fact(DisplayName = "结果验证: 首次检查通过 → Branch")]
    public async Task ResultVerify_FirstCheckPass_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        // Set "before" analysis (different from "after")
        ctx.SetCurrentPageAnalysis(CreatePageAnalysis(["item_a", "item_b"]));
        var fsm = DriveToResultVerify(ctx);
        var (stepCtx, _, vision) = CreateStepContextWithTrace(ctx, fsm);

        // Set "after" analysis with different items → HasChanged = true
        vision.NextResult = CreatePageAnalysis(["item_c", "item_d"]);

        var result = await fsm.StepAsync(stepCtx);

        Assert.Equal(TraversalState.Branch, result);
    }

    [Fact(DisplayName = "结果验证: 第2轮重试成功 → Branch")]
    public async Task ResultVerify_RetryRound2Succeeds_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(CreatePageAnalysis(["item_a"]));
        var fsm = DriveToResultVerify(ctx);

        // First vision call returns same (no change), second returns different
        var sameAnalysis = CreatePageAnalysis(["item_a"]);
        var changedAnalysis = CreatePageAnalysis(["item_b"]);
        var callSequence = new Queue<PageAnalysis?>();
        callSequence.Enqueue(sameAnalysis);      // initial check → no change
        callSequence.Enqueue(sameAnalysis);      // round 1 → no change
        callSequence.Enqueue(changedAnalysis);   // round 2 → change detected

        var seqVision = new SequentialVisionProvider(callSequence);
        var (seqStepCtx, _) = CreateStepContextWithSequentialVision(ctx, fsm, seqVision);

        var result = await fsm.StepAsync(seqStepCtx);

        Assert.Equal(TraversalState.Branch, result);
    }

    [Fact(DisplayName = "结果验证: 3轮重试全失败 → Branch(继续遍历不阻塞)")]
    public async Task ResultVerify_3RoundsFail_GoesToBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(CreatePageAnalysis(["item_a"]));
        var fsm = DriveToResultVerify(ctx);
        var (stepCtx, _, vision) = CreateStepContextWithTrace(ctx, fsm);

        // All calls return the same analysis → no change ever
        vision.NextResult = CreatePageAnalysis(["item_a"]);

        var result = await fsm.StepAsync(stepCtx);

        // After 3 rounds of retry failure → still Branch (don't block traversal)
        Assert.Equal(TraversalState.Branch, result);
    }

    [Fact(DisplayName = "结果验证: 第1轮检测到弹窗(PageAnalysis.IsPopup) → PopupHandling")]
    public async Task ResultVerify_PopupDetectedRound1_GoesToPopupHandling()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(CreatePageAnalysis(["item_a"]));
        var fsm = DriveToResultVerify(ctx);

        // Use sequential vision: initial check shows no change, then popup page (IsPopup=true)
        var callSequence = new Queue<PageAnalysis?>();
        callSequence.Enqueue(CreatePageAnalysis(["item_a"]));                // initial check → no change
        callSequence.Enqueue(CreatePopupPageAnalysis("Allow access", isPopup: true)); // round 1 → IsPopup=true

        var seqVision = new SequentialVisionProvider(callSequence);
        var (seqStepCtx, _) = CreateStepContextWithSequentialVision(ctx, fsm, seqVision);

        var result = await fsm.StepAsync(seqStepCtx);

        Assert.Equal(TraversalState.PopupHandling, result);
    }

    [Fact(DisplayName = "结果验证: 第2轮检测到弹窗(IsPopup=true) → PopupHandling")]
    public async Task ResultVerify_PopupDetectedRound2_GoesToPopupHandling()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(CreatePageAnalysis(["item_a"]));
        var fsm = DriveToResultVerify(ctx);

        // Sequential: initial no change, round 1 no change, round 2 IsPopup=true
        var callSequence = new Queue<PageAnalysis?>();
        callSequence.Enqueue(CreatePageAnalysis(["item_a"]));           // initial check → no change
        callSequence.Enqueue(CreatePageAnalysis(["item_a"]));           // round 1 → no change, no popup
        callSequence.Enqueue(CreatePopupPageAnalysis("Allow access", isPopup: true)); // round 2 → IsPopup=true

        var seqVision = new SequentialVisionProvider(callSequence);
        var (seqStepCtx, _) = CreateStepContextWithSequentialVision(ctx, fsm, seqVision);

        var result = await fsm.StepAsync(seqStepCtx);

        Assert.Equal(TraversalState.PopupHandling, result);
    }

    [Fact(DisplayName = "结果验证: trace decisions记录")]
    public async Task ResultVerify_TraceDecisionsRecorded()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        ctx.SetCurrentPageAnalysis(CreatePageAnalysis(["item_a"]));
        var fsm = DriveToResultVerify(ctx);
        var (stepCtx, storage, vision) = CreateStepContextWithTrace(ctx, fsm);

        // Set vision to return different items → first check passes
        vision.NextResult = CreatePageAnalysis(["item_b"]);

        await fsm.StepAsync(stepCtx);

        var executions = storage.GetExecutions();
        Assert.Contains(executions, e => e.Action == "verification_passed_first_check");
    }

    [Fact(DisplayName = "结果验证: 无StepContext → stub回退返回Branch")]
    public async Task ResultVerify_NoStepContext_StubFallbackBranch()
    {
        var ctx = new TraversalRuntimeContext("test-trace");
        var fsm = DriveToResultVerify(ctx);

        var result = await fsm.StepAsync(); // No StepContext → stub fallback

        Assert.Equal(TraversalState.Branch, result);
    }
}

/// <summary>
/// Sequential vision provider — returns PageAnalysis results from a queue.
/// Used for testing retry logic where different calls need different results.
/// </summary>
internal sealed class SequentialVisionProvider : IVisionProvider
{
    private readonly Queue<PageAnalysis?> _results;

    public SequentialVisionProvider(Queue<PageAnalysis?> results)
    {
        _results = results;
    }

    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : null);
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => Task.FromResult<AppEntryPoint?>(new AppEntryPoint(0.5, 0.5));
}
