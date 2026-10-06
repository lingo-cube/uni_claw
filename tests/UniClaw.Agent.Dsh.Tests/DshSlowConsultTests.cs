using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>AGT-017 — live Slow 桥的确定性合同：有界等待强制、错误诚实映射、
/// 成功路径经 kernel.Process 公开 P2 缝投影。fake transport，零网络。</summary>
public sealed class DshSlowConsultTests
{
    private static readonly ModelConfiguration Model = new("zai-coding-cn", "glm-5.3-flash");

    private static UniKernel Kernel() =>
        new(new EvidenceLedger(), new WorldModel(new HashSet<string> { "ui.overlay.popup" }),
            DisabledRunTrace.Instance);

    private static SlowConsultationRequest Request() => new(
        "slow-test-1", "Wi-Fi", "ui.overlay.popup", "value", RequiresRawArtifact: false,
        "buyer-1", "slow-trigger:SemanticUnclear", "capture-1", DateTimeOffset.Now,
        "cycle-1", Array.Empty<string>());

    private static DshSlowConsult Bridge(DshSlowConsult.Transport transport) =>
        new(transport, Model);

    [Fact]
    public void Success_ParsesAndProjectsViaPublicP2Seam()
    {
        var ledger = new EvidenceLedger();
        var kernel = new UniKernel(ledger, new WorldModel(new HashSet<string> { "ui.overlay.popup" }), DisabledRunTrace.Instance);
        var consult = Bridge((_, _, _, _, _) => Task.FromResult(new DshSlowResponse(
            "slow-test-1",
            """{"status":"Succeeded","semanticDisposition":"Supported","proposals":[{"subject":"ui.overlay.popup","value":"present"}]}""",
            Error: null)));

        var outcome = consult.Consult(Request(), kernel, effectCritical: false, boundedWait: TimeSpan.FromSeconds(5));

        Assert.Equal(SlowConsultationStatus.Succeeded, outcome.Status);
        Assert.Equal(1, outcome.ProjectedProposals);
        var evidence = Assert.Single(ledger.CanonicalRecords.Values);
        Assert.Equal("ui.overlay.popup", evidence.Claim.Subject);
        Assert.Equal("present", evidence.Claim.Value);
    }

    [Fact]
    public void EndpointError_MapsToRejectedWithDiagnostic_ZeroProjection()
    {
        var kernel = Kernel();
        var consult = Bridge((_, _, _, _, _) => Task.FromResult(new DshSlowResponse(
            "slow-test-1", Text: null, Error: "one-in-flight")));

        var outcome = consult.Consult(Request(), kernel, false, TimeSpan.FromSeconds(5));

        Assert.Equal(SlowConsultationStatus.Rejected, outcome.Status);
        Assert.False(outcome.Admitted);
        Assert.Equal(0, outcome.ProjectedProposals);
        Assert.Contains("one-in-flight", outcome.Diagnostic);
    }

    [Fact]
    public async Task BoundedWaitExhausted_MapsToTimedOut_ZeroProjection_CycleProceeds()
    {
        var kernel = Kernel();
        var consult = Bridge(async (_, _, _, _, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new DshSlowResponse("slow-test-1", "{}", null);
        });

        var started = DateTimeOffset.UtcNow;
        var outcome = await Task.Run(() =>
            consult.Consult(Request(), kernel, false, TimeSpan.FromMilliseconds(300)));
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.Equal(SlowConsultationStatus.TimedOut, outcome.Status);
        Assert.Equal(0, outcome.ProjectedProposals);
        // 异步保证：有界等待被强制——耗时贴近界而非贴近传输的 30s。
        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"elapsed={elapsed}");
    }

    [Fact]
    public void NonJsonResponse_IsRejectedHonestly()
    {
        var kernel = Kernel();
        var consult = Bridge((_, _, _, _, _) => Task.FromResult(new DshSlowResponse(
            "slow-test-1", "the screen shows settings", null)));

        var outcome = consult.Consult(Request(), kernel, false, TimeSpan.FromSeconds(5));

        Assert.Equal(SlowConsultationStatus.Rejected, outcome.Status);
        Assert.Contains("slow-response-not-json", outcome.Diagnostic);
    }

    [Fact]
    public void PromptCarriesTargetClaimAndTrigger()
    {
        var prompt = DshSlowConsult.BuildPrompt(Request());
        Assert.Contains("Wi-Fi", prompt);
        Assert.Contains("ui.overlay.popup", prompt);
        Assert.Contains("slow-trigger:SemanticUnclear", prompt);
        Assert.Contains("proposals", prompt);
    }

    [Fact]
    public void VisualRequiredButNotConfigured_IsNotConfigured_ZeroProjection()
    {
        var kernel = Kernel();
        var request = Request() with { RequiresRawArtifact = true, RawArtifact = new byte[] { 1 } };
        var consult = Bridge((_, _, _, _, _) => Task.FromResult(new DshSlowResponse("slow-test-1", "{}", null)));

        var outcome = consult.Consult(request, kernel, false, TimeSpan.FromSeconds(5));

        Assert.Equal(SlowConsultationStatus.NotConfigured, outcome.Status);
        Assert.Contains("visual", outcome.Diagnostic);
    }
}
