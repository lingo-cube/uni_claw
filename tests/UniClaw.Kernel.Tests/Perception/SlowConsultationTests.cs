using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// AGT-009 — 公开 Slow 咨询缝（Host 买方）的确定性契约测试。语义（ADR-0030）：
/// 结果只经 SlowResultProjector → P2 进账；NotConfigured/Timeout 零投影；
/// 晚到结果可投影但 IsLate=true；本缝结构上不存在 Effect 授权路径。
/// </summary>
public sealed class SlowConsultationTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static SlowConsultationRequest Request(
        string requestId = "req-1",
        bool requiresRawArtifact = false,
        byte[]? rawArtifact = null) => new(
        RequestId: requestId,
        Target: "settings-screen",
        ClaimSubject: "ui.overlay.popup",
        ClaimField: "state",
        RequiresRawArtifact: requiresRawArtifact,
        BuyerRef: "host.settings-coverage",
        Reason: "slow-trigger:NoXml",
        CaptureId: "capture-1",
        CaptureTimestamp: CaptureTime,
        ObservationCycleId: "cycle-1",
        EvidenceIds: Array.Empty<string>(),
        RawArtifact: rawArtifact,
        FastBasis: requiresRawArtifact ? null : new FastTextBasis(
            "capture-1", "host.settings-coverage", "cycle-1",
            new[] { "button" }, new[] { "Settings" }, CaptureTime));

    private static UniKernel Kernel(EvidenceLedger? ledger = null, string subject = "ui.overlay.popup") =>
        new(ledger ?? new EvidenceLedger(), new WorldModel(new HashSet<string> { subject }), DisabledRunTrace.Instance);

    private static SlowConsultation ReplayConsultation(out DeterministicSlowRealization replay)
    {
        replay = new DeterministicSlowRealization(SlowReplayProfiles.CreateDefault());
        return new SlowConsultation(new SlowPerceptionOrchestrator(
            new EphemeralAttemptLedger(), replay));
    }

    /// <summary>与 seam 内部映射逐字段对齐的内部请求重建（测试专用对齐桩）。</summary>
    private static SlowPerceptionRequest InternalRequest(SlowConsultationRequest request)
    {
        var profile = request.RequiresRawArtifact && request.RawArtifact is not null
            ? LogicalProfileId.Visual
            : LogicalProfileId.Text;
        var claim = new RequiredClaim(request.ClaimSubject, request.ClaimField);
        var capture = new FusionCapture(request.CaptureId, request.BuyerRef, request.ObservationCycleId, CaptureTime);
        var context = EvidenceContextBuilder.Build(claim,
            semantic: new SemanticReasoningContext(claim, request.EvidenceIds, Reason: request.Reason));
        var raw = profile == LogicalProfileId.Visual ? new RawArtifactRef(request.CaptureId) : null;
        var binding = SlowReplayProfiles.CreateDefault().Resolve(profile).Binding;
        return new SlowPerceptionRequest(request.RequestId,
            new SlowAttemptKey(request.Target, request.ObservationCycleId, claim, profile),
            claim, profile, request.BuyerRef, request.Reason, capture, context, raw, binding);
    }

    [Fact]
    public void UnresolvableBinding_ReturnsNotConfigured_WithZeroProposals()
    {
        var consultation = new SlowConsultation(new SlowPerceptionOrchestrator(
            new EphemeralAttemptLedger(),
            new DeterministicSlowRealization(new SlowModelManagement())));
        var ledger = new EvidenceLedger();

        var outcome = consultation.Consult(Request(), Kernel());

        Assert.Equal(SlowConsultationStatus.NotConfigured, outcome.Status);
        Assert.False(outcome.Admitted);
        Assert.Equal(0, outcome.ProjectedProposals);
        Assert.Empty(ledger.CanonicalRecords);
    }

    [Fact]
    public void Success_ProjectsThroughP2_IntoLedger_AndNoEffectPath()
    {
        var consultation = ReplayConsultation(out var replay);
        var request = Request();
        var internalRequest = InternalRequest(request);
        replay.SetResponse(request.RequestId, new SlowReplayResponse(
            SlowExecutionStatus.Succeeded,
            new[] { DeterministicSlowRealization.Proposal(internalRequest, "ui.overlay.popup", "present") },
            SlowSemanticDisposition.Supported));
        var ledger = new EvidenceLedger();
        var kernel = Kernel(ledger);

        var outcome = consultation.Consult(request, kernel);

        Assert.Equal(SlowConsultationStatus.Succeeded, outcome.Status);
        Assert.True(outcome.Admitted, outcome.Diagnostic);
        Assert.Equal(1, outcome.ProjectedProposals);
        Assert.False(outcome.IsLate);
        var evidence = Assert.Single(ledger.CanonicalRecords.Values);
        Assert.Equal("ui.overlay.popup", evidence.Claim.Subject);
        Assert.Equal("present", evidence.Claim.Value);
        // 结构性保证：seam 全链零 Effect。
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void Timeout_ReturnsTimedOut_WithZeroProjections()
    {
        var consultation = ReplayConsultation(out var replay);
        var request = Request();
        replay.SetResponse(request.RequestId, new SlowReplayResponse(
            SlowExecutionStatus.Timeout, Diagnostic: "bounded wait exhausted"));
        var ledger = new EvidenceLedger();

        var outcome = consultation.Consult(Request(), Kernel(), boundedWait: TimeSpan.FromMilliseconds(1));

        Assert.Equal(SlowConsultationStatus.TimedOut, outcome.Status);
        Assert.False(outcome.Admitted);
        Assert.Equal(0, outcome.ProjectedProposals);
        Assert.Empty(ledger.CanonicalRecords);
    }

    [Fact]
    public void LateResult_IsProjected_WithIsLate_AndStillNoEffect()
    {
        var consultation = ReplayConsultation(out var replay);
        var request = Request();
        var internalRequest = InternalRequest(request);
        var result = new SlowResult(request.RequestId, internalRequest.AttemptKey,
            SlowExecutionStatus.Succeeded,
            new[] { DeterministicSlowRealization.Proposal(internalRequest, "ui.overlay.popup", "present") },
            internalRequest.Capture, internalRequest.Binding, SlowSemanticDisposition.Supported);
        var kernel = Kernel();

        var outcome = consultation.AcceptLate(result, kernel);

        Assert.Equal(SlowConsultationStatus.Succeeded, outcome.Status);
        Assert.True(outcome.Admitted);
        Assert.True(outcome.IsLate);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void InvalidRequest_ReturnsInvalid_WithoutConsultingRealization()
    {
        var consultation = ReplayConsultation(out var replay);

        var outcome = consultation.Consult(Request(requestId: " "), Kernel());

        Assert.Equal(SlowConsultationStatus.Invalid, outcome.Status);
        Assert.Equal(0, outcome.ProjectedProposals);
        Assert.Equal(0, replay.SemanticInvocationCount);
    }

    [Fact]
    public void FailedRealization_ReturnsRejected_WithZeroProjections()
    {
        var consultation = ReplayConsultation(out var replay);
        var request = Request();
        replay.SetResponse(request.RequestId, new SlowReplayResponse(
            SlowExecutionStatus.MalformedResponse, Diagnostic: "bad payload"));
        var ledger = new EvidenceLedger();

        var outcome = consultation.Consult(request, Kernel());

        Assert.Equal(SlowConsultationStatus.Rejected, outcome.Status);
        Assert.Equal(0, outcome.ProjectedProposals);
        Assert.Empty(ledger.CanonicalRecords);
    }
}
