using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

public sealed class TextFastBasisTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private static FusionCapture Capture() =>
        new("capture-1", "session-1", "cycle-1", CaptureTime);

    private static FastTextBasis Basis(
        string captureId = "capture-1",
        string session = "session-1",
        string cycle = "cycle-1",
        bool providerAvailable = true,
        bool fresh = true,
        IReadOnlyList<string>? yolo = null,
        IReadOnlyList<string>? ocr = null) => new(
            captureId, session, cycle,
            yolo ?? new[] { "text_block" },
            ocr ?? new[] { "Settings" },
            CaptureTime, providerAvailable, fresh);

    [Fact]
    public void SameCaptureCycleSession_IsEligible()
    {
        var result = SlowTextGate.Evaluate(Capture(), Basis());

        Assert.True(result.Eligible);
        Assert.Equal(SlowTextGateStatus.Eligible, result.Status);
        Assert.Same(result.Basis, result.Basis);
    }

    [Fact]
    public void EmptyDetections_AreEligibleAndRemainDistinctFromMissing()
    {
        var result = SlowTextGate.Evaluate(Capture(), Basis(yolo: Array.Empty<string>(), ocr: Array.Empty<string>()));

        Assert.True(result.Eligible);
        Assert.NotNull(result.Basis);
        Assert.False(result.Basis!.HasDetection);
        Assert.Equal(SlowTextGateStatus.Eligible, result.Status);
    }

    [Fact]
    public void MissingStaleMisalignedAndUnavailable_AreDistinctDiagnostics()
    {
        Assert.Equal(SlowTextGateStatus.MissingFast,
            SlowTextGate.Evaluate(Capture(), null).Status);
        Assert.Equal(SlowTextGateStatus.StaleFast,
            SlowTextGate.Evaluate(Capture(), Basis(fresh: false)).Status);
        Assert.Equal(SlowTextGateStatus.MisalignedFast,
            SlowTextGate.Evaluate(Capture(), Basis(session: "other-session")).Status);
        Assert.Equal(SlowTextGateStatus.ProviderUnavailable,
            SlowTextGate.Evaluate(Capture(), Basis(providerAvailable: false)).Status);
    }

    [Fact]
    public void SlowConsultation_RejectsTextWithoutFastBasis()
    {
        var replay = new DeterministicSlowRealization(SlowReplayProfiles.CreateDefault());
        var consultation = new SlowConsultation(new SlowPerceptionOrchestrator(
            new EphemeralAttemptLedger(), replay));
        var claim = new RequiredClaim("screen", "route");
        var request = new SlowConsultationRequest(
            "request-1", "settings-screen", claim.Subject, claim.Field, false,
            "host.settings-coverage", "slow-trigger:SemanticUnclear", "capture-1",
            CaptureTime, "cycle-1", Array.Empty<string>());
        var kernel = new UniKernel(new EvidenceLedger(),
            new WorldModel(new HashSet<string> { "screen" }),
            UniClaw.Kernel.Trace.DisabledRunTrace.Instance);

        var outcome = consultation.Consult(request, kernel);

        Assert.Equal(SlowConsultationStatus.Rejected, outcome.Status);
        Assert.Contains("basis missing", outcome.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(0, replay.SemanticInvocationCount);
    }

    [Fact]
    public void OpenCodeSerialization_CarriesBoundedFastBasis()
    {
        var claim = new RequiredClaim("screen", "route");
        var capture = Capture();
        var context = EvidenceContextBuilder.Build(claim,
            semantic: new SemanticReasoningContext(claim, Array.Empty<string>()));
        var request = new SlowPerceptionRequest(
            "request-1", new SlowAttemptKey("settings-screen", "cycle-1", claim, LogicalProfileId.Text),
            claim, LogicalProfileId.Text, "host.settings-coverage", "slow-trigger:SemanticUnclear",
            capture, context, FastBasis: Basis());
        using var realization = new OpenCodeSlowRealization(
            new SlowModelManagement(), OpenCodeSlowRealizationOptions.Local());

        var json = realization.SerializeContext(request);
        using var document = JsonDocument.Parse(json);
        var basis = document.RootElement.GetProperty("fastBasis");

        Assert.Equal("capture-1", basis.GetProperty("captureId").GetString());
        Assert.Equal("session-1", basis.GetProperty("sessionCorrelation").GetString());
        Assert.Equal("text_block", basis.GetProperty("yoloDetections")[0].GetString());
        Assert.Equal("Settings", basis.GetProperty("ocrTokens")[0].GetString());
    }
}
