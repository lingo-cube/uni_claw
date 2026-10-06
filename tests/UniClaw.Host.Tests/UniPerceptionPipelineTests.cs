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
        Assert.Equal("L|1|Supported", token);
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
}
