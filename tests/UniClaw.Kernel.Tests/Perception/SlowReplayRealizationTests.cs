using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.Fusion;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;
using UniClaw.Kernel.Capability;

namespace UniClaw.Kernel.Tests.Perception;

public sealed class SlowReplayRealizationTests
{
    private static readonly DateTimeOffset CaptureTime = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TextRequest_UsesBoundedContext_AndReplayProposal()
    {
        var request = Request(LogicalProfileId.Text);
        var replay = new DeterministicSlowRealization();
        replay.SetResponse(request.RequestId, new SlowReplayResponse(
            SlowExecutionStatus.Succeeded,
            new[] { DeterministicSlowRealization.Proposal(request, "ui.dialog", "visible") },
            SlowSemanticDisposition.Supported));

        var result = replay.Execute(request);

        Assert.Equal(SlowExecutionStatus.Succeeded, result.Status);
        Assert.Single(result.Proposals);
        Assert.Equal(SlowSemanticDisposition.Supported, result.SemanticDisposition);
    }

    [Fact]
    public void VisualRequest_WithoutRawArtifact_IsRejected()
    {
        var result = new DeterministicSlowRealization().Execute(Request(LogicalProfileId.Visual) with { RawArtifact = null });
        Assert.Equal(SlowExecutionStatus.InvalidInput, result.Status);
        Assert.Empty(result.Proposals);
    }

    [Theory]
    [InlineData((int)SlowExecutionStatus.Timeout)]
    [InlineData((int)SlowExecutionStatus.Cancelled)]
    [InlineData((int)SlowExecutionStatus.ModelUnavailable)]
    [InlineData((int)SlowExecutionStatus.UnsupportedCapability)]
    [InlineData((int)SlowExecutionStatus.MalformedResponse)]
    [InlineData((int)SlowExecutionStatus.SchemaFailure)]
    [InlineData((int)SlowExecutionStatus.InfrastructureFailure)]
    [InlineData((int)SlowExecutionStatus.InvalidInput)]
    public void FailureStatuses_ProduceZeroProposals(int rawStatus)
    {
        var status = (SlowExecutionStatus)rawStatus;
        var request = Request(LogicalProfileId.Text);
        var replay = new DeterministicSlowRealization();
        replay.SetResponse(request.RequestId, new SlowReplayResponse(status,
            new[] { DeterministicSlowRealization.Proposal(request, "ui.dialog", "forged") }));
        Assert.Empty(replay.Execute(request).Proposals);
    }

    [Fact]
    public void ContextInsufficient_ProducesZeroProposals()
    {
        var valid = Request(LogicalProfileId.Text);
        var request = valid with
        {
            Context = EvidenceContext.ContextInsufficient(valid.RequiredClaim)
        };
        var result = new DeterministicSlowRealization().Execute(request);
        Assert.Equal(SlowExecutionStatus.ContextInsufficient, result.Status);
        Assert.Empty(result.Proposals);
    }

    [Fact]
    public void Partial_AdmitsOnlyValidProposals()
    {
        var request = Request(LogicalProfileId.Text);
        var valid = DeterministicSlowRealization.Proposal(request, "ui.dialog", "visible");
        var malformed = new ObservationProposal(
            new ObservationClaim("ui.dialog", "bad"), IngressKind.Observation,
            ObservationContext.External, null);
        var replay = new DeterministicSlowRealization();
        replay.SetResponse(request.RequestId, new SlowReplayResponse(
            SlowExecutionStatus.Partial, new[] { valid, malformed }));
        var result = replay.Execute(request);
        Assert.Equal(SlowExecutionStatus.Partial, result.Status);
        Assert.Single(result.Proposals);
    }

    [Fact]
    public void SucceededEmpty_IsNotAbsence()
    {
        var result = new DeterministicSlowRealization().Execute(Request(LogicalProfileId.Text));
        Assert.Equal(SlowExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Proposals);
    }

    [Fact]
    public void TransportRetry_UsesOneSemanticInvocation()
    {
        var request = Request(LogicalProfileId.Text);
        var replay = new DeterministicSlowRealization();
        var result = replay.ExecuteWithTransportRetries(request, new[]
        {
            new SlowReplayResponse(SlowExecutionStatus.InfrastructureFailure, Diagnostic: "transport-retry"),
            new SlowReplayResponse(SlowExecutionStatus.Succeeded,
                new[] { DeterministicSlowRealization.Proposal(request, "ui.dialog", "visible") }),
        });
        Assert.Equal(1, replay.SemanticInvocationCount);
        Assert.Single(result.Proposals);
    }

    [Fact]
    public void EffectCriticalZeroBudget_TimesOutWithoutEffectPath()
    {
        var orchestration = new SlowPerceptionOrchestrator();
        var outcome = orchestration.Execute(Request(LogicalProfileId.Text), effectCritical: true,
            boundedWait: TimeSpan.Zero);
        Assert.Equal(SlowExecutionStatus.Timeout, outcome.Result.Status);
        Assert.False(outcome.EffectAuthorizationAllowed);
        Assert.Equal(0, orchestration.Realization.SemanticInvocationCount);
    }

    [Fact]
    public void DuplicateAttempt_IsRejected_AndTerminalCannotRetry()
    {
        var orchestration = new SlowPerceptionOrchestrator();
        var first = orchestration.Execute(Request(LogicalProfileId.Text));
        var second = orchestration.Execute(Request(LogicalProfileId.Text));
        Assert.NotEqual(SlowWaitDisposition.DuplicateAttempt, first.WaitDisposition);
        Assert.Equal(SlowWaitDisposition.DuplicateAttempt, second.WaitDisposition);
        Assert.Equal(1, orchestration.Realization.SemanticInvocationCount);
    }

    [Fact]
    public void SlowResult_EntersP2_AndRequestIdDoesNotBecomeEvidenceId()
    {
        var request = Request(LogicalProfileId.Text);
        var proposal = DeterministicSlowRealization.Proposal(request, "ui.dialog", "visible");
        var result = new SlowResult(request.RequestId, request.AttemptKey,
            SlowExecutionStatus.Succeeded, new[] { proposal }, request.Capture,
            request.Binding, SlowSemanticDisposition.Supported);
        var ledger = new EvidenceLedger();
        var kernel = new UniKernel(ledger,
            new WorldModel(new HashSet<string> { "ui.dialog" }), DisabledRunTrace.Instance);

        var admission = SlowResultProjector.Project(result, kernel);

        Assert.True(admission.Accepted);
        var evidence = Assert.Single(ledger.CanonicalRecords.Values);
        Assert.DoesNotContain(request.RequestId, evidence.EvidenceId, StringComparison.Ordinal);
    }

    [Fact]
    public void LateResult_CanEnterP2_ButCannotAuthorizeExpiredEffect()
    {
        var request = Request(LogicalProfileId.Text);
        var result = new SlowOrchestrationResult(
            new SlowResult(request.RequestId, request.AttemptKey, SlowExecutionStatus.Succeeded,
                new[] { DeterministicSlowRealization.Proposal(request, "ui.dialog", "visible") },
                request.Capture, request.Binding, SlowSemanticDisposition.Supported),
            IsLate: true);
        var kernel = new UniKernel(new EvidenceLedger(),
            new WorldModel(new HashSet<string> { "ui.dialog" }), DisabledRunTrace.Instance);

        var admission = SlowResultProjector.Project(result.Result, kernel);

        Assert.True(admission.Accepted);
        Assert.False(result.EffectAuthorizationAllowed);
    }

    [Fact]
    public void UnknownProfile_IsExplicitRoutingUnavailable()
    {
        var management = SlowReplayProfiles.CreateDefault();
        var resolution = management.Resolve(new LogicalProfileId("slow.unknown"));
        Assert.Equal(ModelRoutingStatus.RoutingUnavailable, resolution.Status);
        Assert.Contains("ROUTING_UNAVAILABLE", resolution.Diagnostic);
    }

    [Fact]
    public void UnhealthyBinding_IsUnavailableWithoutSilentFallback()
    {
        var profile = LogicalProfileId.Text;
        var management = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(profile, "replay", "model", Available: true, Health: false),
        });
        var resolution = management.Resolve(profile);
        Assert.Equal(ModelRoutingStatus.RoutingUnavailable, resolution.Status);
        Assert.Null(resolution.Binding);
    }

    [Fact]
    public void ExplicitFallback_IsRecordedOnResolvedSnapshot()
    {
        var profile = LogicalProfileId.Text;
        var management = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(profile, "replay", "primary", Available: false),
        });
        var fallback = new ModelBindingSnapshot(profile, "replay", "fallback", Available: true);
        var resolution = management.Resolve(profile, fallback);
        Assert.True(resolution.IsResolved);
        Assert.Equal("primary", resolution.Binding!.FallbackFrom);
    }

    [Fact]
    public void DeepEscalation_MapsToSlowRouteHintOnly()
    {
        Assert.Equal(ObservationDepth.Focused,
            SlowEscalationRoute.ToObservationDepth(EscalationAction.FocusedRescan));
        Assert.Equal(ObservationDepth.Slow,
            SlowEscalationRoute.ToObservationDepth(EscalationAction.DeepPerception));
    }

    private static SlowPerceptionRequest Request(LogicalProfileId profile)
    {
        var claim = new RequiredClaim("ui.dialog", "visible");
        var capture = new FusionCapture("capture-1", "run-1", "cycle-1", CaptureTime);
        var context = EvidenceContextBuilder.Build(claim, budget: 4);
        var raw = profile == LogicalProfileId.Visual ? new RawArtifactRef("artifact-1") : null;
        return new SlowPerceptionRequest("request-1",
            new SlowAttemptKey("dialog-1", "cycle-1", claim, profile), claim, profile,
            "control", "required claim insufficient", capture, context, raw,
            new ModelBindingSnapshot(profile, "replay", "model", Available: true, Experimental: true));
    }
}
