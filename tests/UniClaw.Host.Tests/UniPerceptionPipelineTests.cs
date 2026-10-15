using UniClaw.Host;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>PER-019 — UniPerception 异步流水的确定性合同：发射即时返回、
/// 晚到 IsLate 投影、路由变更诚实丢弃、超时零投影、预算发射时计。</summary>
public sealed class UniPerceptionPipelineTests
{
    private static SlowConsultationRequest Request(string id = "slow-p-1") => new(
        id, "settings-screen", "ui.screen.route", "route", RequiresRawArtifact: false,
        "buyer-1", "slow-trigger:SemanticUnclear", "capture-1", DateTimeOffset.UtcNow,
        "cycle-1", Array.Empty<string>());

    private static UniPerceptionFetchResult Ok(params (string, string)[] proposals) =>
        new(SlowConsultationStatus.Succeeded, true, proposals, "Supported", null);

    [Fact]
    public void Dispatch_ReturnsImmediately_CycleNeverBlocks()
    {
        var pipeline = new UniPerceptionPipeline(
            (req, corr, ct) => Task.Delay(TimeSpan.FromSeconds(30), ct)
                .ContinueWith(_ => Ok(("x", "y")), TaskScheduler.Default)
                .ContinueWith<UniPerceptionFetchResult>(t => t.Result), maxRequestsPerRun: 4);
        var started = DateTimeOffset.UtcNow;

        var token = pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear",
            fetchBound: TimeSpan.FromSeconds(30));

        var elapsed = DateTimeOffset.UtcNow - started;
        Assert.StartsWith("SemanticUnclear|Dispatched", token);
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"dispatch blocked {elapsed}");
        Assert.Equal(1, pipeline.InFlight);
    }

    [Fact]
    public async Task Poll_LandsLateResult_WithProjectorAndDisposition()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");
        Assert.Empty(pipeline.Poll((r, res) => "landed", "android.settings")); // 未完成不结算

        tcs.SetResult(Ok(("ui.screen.route", "Unknown")));
        var tokens = pipeline.Poll(
            (request, result) => $"L|{result.Proposals.Count}|{result.SemanticDisposition}",
            "android.settings");

        var token = Assert.Single(tokens);
        Assert.StartsWith("L|1|Supported|ms=", token); // AGT-019 M3：令牌带 ms 延迟分量
        Assert.Equal(0, pipeline.InFlight);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Poll_RouteChanged_DropsHonestly_Unaligned()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");
        tcs.SetResult(Ok(("ui.screen.route", "X")));

        var tokens = pipeline.Poll((r, res) => "should-not-land", "android.settings|rk1:Internet");

        var token = Assert.Single(tokens);
        Assert.Contains("Dropped|unaligned-route:android.settings->", token);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Poll_Timeout_ZeroProjection_HonestToken()
    {
        var pipeline = new UniPerceptionPipeline(
            (req, corr, ct) => Task.FromCanceled<UniPerceptionFetchResult>(ct), maxRequestsPerRun: 4);
        // fetch 直接取消（等价超时）；用可完成包装避免竞态
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "route", "NoXml");
        tcs.SetResult(new UniPerceptionFetchResult(
            SlowConsultationStatus.TimedOut, false, Array.Empty<(string, string)>(), null, "bounded-wait-exhausted"));

        var tokens = pipeline.Poll((r, res) => "should-not-land", "route");
        var token = Assert.Single(tokens);
        Assert.Contains("NoXml|TimedOut|zero-projection", token);
        await Task.CompletedTask;
    }

    [Fact]
    public void Budget_CountsAtDispatch_SkipsWhenExhausted()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 1);

        var first = pipeline.Dispatch(Request("a"), "corr", "route", "NoXml");
        var second = pipeline.Dispatch(Request("b"), "corr", "route", "NoXml");

        Assert.StartsWith("NoXml|Dispatched", first);
        Assert.StartsWith("NoXml|Skipped|budget-exhausted", second);
        Assert.Equal(1, pipeline.Dispatched);
    }

    [Fact]
    public void LandProjector_WiresThroughKernel_ProducerIsUniPerception()
    {
        // feed 的 LandSlow 语义端到端：经 kernel.Process 落 canonical evidence。
        var ledger = new EvidenceLedger();
        var kernel = new UniKernel(ledger,
            new Kernel.World.WorldModel(new HashSet<string> { "ui.screen.route" }),
            Kernel.Trace.DisabledRunTrace.Instance);
        var request = Request();
        var result = Ok(("ui.screen.route", "Unknown"));

        var projected = 0;
        foreach (var (subject, value) in result.Proposals)
        {
            kernel.Process(new ObservationProposal(
                new ObservationClaim(subject, value), IngressKind.Observation,
                ObservationContext.External,
                new Provenance("uni.perception", request.CaptureTimestamp,
                    $"scope:slow:{request.ObservationCycleId}",
                    new[] { "real-model", "capture:" + request.CaptureId, "late" })));
            projected++;
        }

        Assert.Equal(1, projected);
        var evidence = Assert.Single(ledger.CanonicalRecords.Values);
        Assert.Equal("uni.perception", evidence.Provenance?.Producer);
    }

    // ---- AGT-019 M3：slow 令牌延迟分量（P6 可观测性闭合）-------------------

    [Fact]
    public void Dispatch_TokenCarriesLatencyComponent()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);

        var token = pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");

        Assert.Matches(@"^SemanticUnclear\|Dispatched\|in-flight=1\|ms=\d+$", token);
    }

    [Fact]
    public async Task Settled_DropTokenCarriesLatencyComponent()
    {
        var droppedTcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => droppedTcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");
        droppedTcs.SetResult(Ok(("ui.screen.route", "X")));

        var dropped = Assert.Single(pipeline.Poll((_, _) => "should-not-land",
            "android.settings|rk1:Internet"));
        Assert.Matches(@"^SemanticUnclear\|Dropped\|unaligned-route:.*\|ms=\d+$", dropped);
        await Task.CompletedTask;
    }

    // ---- AGT-019 M3：材料不足的有界同步等待 ---------------------------------

    [Fact]
    public void WaitAndPoll_LandsResultWithinBudget_WhenFetchCompletes()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");
        _ = Task.Delay(50).ContinueWith(_ => tcs.TrySetResult(Ok(("ui.screen.route", "Unknown"))));

        var tokens = pipeline.WaitAndPoll(
            (_, result) => $"L|{result.Proposals.Count}",
            "android.settings",
            budget: TimeSpan.FromSeconds(5));

        var token = Assert.Single(tokens);
        Assert.StartsWith("L|1|ms=", token);
        Assert.Equal(0, pipeline.InFlight);
    }

    [Fact]
    public void WaitAndPoll_RespectsBudget_KeepsPendingForLaterCycles()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var tokens = pipeline.WaitAndPoll((_, _) => "should-not-land", "android.settings",
            budget: TimeSpan.FromMilliseconds(200));
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);

        Assert.Empty(tokens); // 未完成不结算：跨周期晚到语义不变
        Assert.Equal(1, pipeline.InFlight);
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(180), $"wait short-circuited: {elapsed}");
        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"budget ignored: {elapsed}");
    }

    [Fact]
    public void WaitAndPoll_WaitCappedByRemainingFetchBound_CycleBudget()
    {
        // fetch 界 500ms（发射时 CTS 起）；budget 30s ≫ 界剩余量 → 等待被
        // 周期剩余预算封顶（≤ fetch 界），不得等满 budget。
        var neverTcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => neverTcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "route", "NoXml",
            fetchBound: TimeSpan.FromMilliseconds(500));

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var tokens = pipeline.WaitAndPoll((_, _) => "should-not-land", "route",
            budget: TimeSpan.FromSeconds(30));
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);

        Assert.Empty(tokens);
        Assert.True(elapsed < TimeSpan.FromSeconds(10),
            $"wait exceeded remaining cycle budget: {elapsed}");
    }

    [Fact]
    public async Task WaitAndPoll_KeepsUnalignedRouteDropGate_FullyInForce()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "SemanticUnclear");
        _ = Task.Delay(50).ContinueWith(_ => tcs.TrySetResult(Ok(("ui.screen.route", "Unknown"))));

        var tokens = pipeline.WaitAndPoll((_, _) => "should-not-land",
            "android.settings|rk1:Internet", budget: TimeSpan.FromSeconds(5));

        var token = Assert.Single(tokens);
        Assert.Contains("|Dropped|unaligned-route:android.settings->", token);
        Assert.Matches(@"\|ms=\d+$", token);
        await Task.CompletedTask;
    }

    // ---- AGT-019 M3：机械触发谓词（既有 per-cycle trace 字段）---------------

    [Theory]
    [InlineData(false, 3, true)]   // hierarchyAvailable=false → 材料不足
    [InlineData(true, 0, true)]    // proposalCount=0 → 材料不足
    [InlineData(true, 5, false)]   // 层级在场且非空 → 不等待
    [InlineData(false, 0, true)]
    public void MaterialInsufficiency_TriggerPredicate_UsesExistingTraceFields(
        bool hierarchyAvailable, int proposalCount, bool expected)
    {
        Assert.Equal(expected, SettingsTraversalLiveFeed.MaterialInsufficiency(
            hierarchyAvailable, proposalCount));
    }

    // ---- AGT-019 M2③/B5：截图三道闸（Q3 裁决：判断级永不入账）--------------

    [Fact]
    public async Task JudgmentLevelSlowResult_NeverLands_DroppedEvenWhenRouteAligned()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "ScreenshotRead",
            judgmentLevel: true);
        _ = Task.Delay(50).ContinueWith(_ => tcs.TrySetResult(Ok(("ui.screen.route", "Unknown"))));

        var landCalls = 0;
        var tokens = pipeline.WaitAndPoll(
            (_, _) => { landCalls++; return "should-never-land"; },
            "android.settings", budget: TimeSpan.FromSeconds(5));

        var token = Assert.Single(tokens);
        Assert.Contains("|Dropped|judgment-source-never-ledgered|", token);
        Assert.Matches(@"\|ms=\d+$", token);
        Assert.Equal(0, landCalls); // 入账拒收：零投影
        await Task.CompletedTask;
    }

    [Fact]
    public async Task JudgmentGate_PrecedesAlignmentCheck_InTrace()
    {
        var tcs = new TaskCompletionSource<UniPerceptionFetchResult>();
        var pipeline = new UniPerceptionPipeline((_, _, _) => tcs.Task, maxRequestsPerRun: 4);
        pipeline.Dispatch(Request(), "corr", "android.settings", "ScreenshotRead",
            judgmentLevel: true);
        _ = Task.Delay(50).ContinueWith(_ => tcs.TrySetResult(Ok(("ui.screen.route", "Unknown"))));

        // 路由也变了，但 judgment 闸优先——令牌是 judgment-source 不是 unaligned。
        var tokens = pipeline.WaitAndPoll((_, _) => "should-not-land",
            "android.settings|rk1:Internet", budget: TimeSpan.FromSeconds(5));

        var token = Assert.Single(tokens);
        Assert.Contains("judgment-source-never-ledgered", token);
        Assert.DoesNotContain("unaligned-route", token);
        await Task.CompletedTask;
    }
}
