using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Deterministic evidence and stateful async tests for the Slow seam.</summary>
public sealed class SlowContainerSemanticAdvisorTests
{
    [Fact]
    public async Task DisabledModeDoesNotInvokeAdvisor()
    {
        var advisor = new FakeSlowAdvisor();
        var result = await ConsumeAsync(
            SlowContainerSemanticMode.Disabled,
            advisor,
            Request(7),
            new SemanticEvidenceRevision(7));

        Assert.Equal(SlowContainerSemanticAvailability.Disabled, result.Availability);
        Assert.Null(result.Assessment);
        Assert.Equal(0, advisor.CallCount);
        Assert.True(result.IsAdvisoryOnly);
        Assert.False(result.HasRuntimeEffect);
    }

    [Fact]
    public async Task ShadowChallengeIsVisibleAndHasNoRuntimeEffect()
    {
        var request = Request(7, node: "node:settings", source: "node:desktop", trigger: "trigger:1", transition: "occ:1");
        var advisor = new FakeSlowAdvisor(_ => Assessment(
            request,
            SlowContainerSemanticAssessmentKind.Challenge,
            SlowContainerSceneKind.WrongChild));

        var result = await ConsumeAsync(
            SlowContainerSemanticMode.Shadow,
            advisor,
            request,
            new SemanticEvidenceRevision(7));

        Assert.Equal(SlowContainerSemanticAvailability.Available, result.Availability);
        Assert.Equal(SlowContainerSemanticAssessmentKind.Challenge, result.Assessment!.Kind);
        Assert.True(result.IsCurrent);
        Assert.False(result.IsStale);
        Assert.True(result.IsAdvisoryOnly);
        Assert.False(result.HasRuntimeEffect);
        Assert.False(result.ConflictsWithFast);
        Assert.Equal(1, advisor.CallCount);
    }

    [Fact]
    public async Task AsyncAdvisoryCanReturnAfterFastAndExposesConflictOnly()
    {
        var request = Request(8, node: "node:settings", source: "node:desktop", trigger: "trigger:1", transition: "occ:1");
        var fast = new FastContainerAssessment(
            FastContainerResolutionKind.NEW_CONTAINER,
            new SemanticEvidenceRevision(8),
            new ContainerNodeRef("node:desktop"),
            new ContainerNodeRef("node:settings"),
            "Settings",
            null,
            true,
            true,
            true,
            false,
            false);
        request = new SlowContainerSemanticRequest(
            request.ObservationRef,
            request.EvidenceRevision,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef,
            fast);
        var completion = new TaskCompletionSource<SlowContainerSemanticAssessment>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var advisor = new FakeSlowAdvisor(_ => completion.Task);

        var pending = SlowContainerSemanticConsumer.AcquireAsync(
            SlowContainerSemanticMode.AsyncAdvisory,
            advisor,
            request);
        Assert.False(pending.IsCompleted);

        completion.SetResult(Assessment(
            request,
            SlowContainerSemanticAssessmentKind.Challenge,
            SlowContainerSceneKind.Overlay));
        var result = SlowContainerSemanticConsumer.Project(
            await pending,
            new SemanticEvidenceRevision(8));

        Assert.Equal(SlowContainerSemanticAvailability.Available, result.Availability);
        Assert.True(result.ConflictsWithFast);
        Assert.True(result.IsAdvisoryOnly);
        Assert.False(result.HasRuntimeEffect);
    }

    [Theory]
    [InlineData(SlowContainerSemanticAssessmentKind.Confirm, SlowContainerSceneKind.Normal)]
    [InlineData(SlowContainerSemanticAssessmentKind.Correct, SlowContainerSceneKind.WrongChild)]
    [InlineData(SlowContainerSemanticAssessmentKind.Insufficient, SlowContainerSceneKind.Advertisement)]
    [InlineData(SlowContainerSemanticAssessmentKind.Insufficient, SlowContainerSceneKind.Transient)]
    [InlineData(SlowContainerSemanticAssessmentKind.Insufficient, SlowContainerSceneKind.Overlay)]
    [InlineData(SlowContainerSemanticAssessmentKind.Insufficient, SlowContainerSceneKind.OffPath)]
    public async Task SameRevisionAssessmentKindsAndScenesRemainVisible(
        SlowContainerSemanticAssessmentKind kind,
        SlowContainerSceneKind scene)
    {
        var request = Request(10, node: "node:child", source: "node:root", trigger: "trigger:x", transition: "occ:x");
        var advisor = new FakeSlowAdvisor(_ => Assessment(request, kind, scene));

        var result = await ConsumeAsync(
            SlowContainerSemanticMode.Shadow,
            advisor,
            request,
            new SemanticEvidenceRevision(10));

        Assert.Equal(SlowContainerSemanticAvailability.Available, result.Availability);
        Assert.Equal(kind, result.Assessment!.Kind);
        Assert.Equal(scene, result.Assessment.Scene);
        Assert.NotNull(result.Assessment.ContainerSemantic);
        Assert.NotNull(result.Assessment.TriggerSemantic);
        Assert.NotNull(result.Assessment.RelationSemantic);
        Assert.Equal(
            scene is SlowContainerSceneKind.Advertisement
                or SlowContainerSceneKind.Transient
                or SlowContainerSceneKind.Loading
                or SlowContainerSceneKind.Overlay
                or SlowContainerSceneKind.OffPath
                ? SlowContainerEvidenceUsefulness.NotUseful
                : SlowContainerEvidenceUsefulness.Useful,
            result.Assessment.EvidenceUsefulness);
        Assert.Equal(
            kind is SlowContainerSemanticAssessmentKind.Challenge or SlowContainerSemanticAssessmentKind.Correct,
            result.Assessment.HasMismatch);
        Assert.True(result.IsCurrent);
        Assert.False(result.HasRuntimeEffect);
    }

    [Fact]
    public async Task FastCompletesBeforeSlowAndOlderResultBecomesStaleAfterFreshRevision()
    {
        var request = Request(11, node: "node:child", source: "node:root", trigger: "trigger:x", transition: "occ:x");
        var completion = new TaskCompletionSource<SlowContainerSemanticAssessment>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var advisor = new FakeSlowAdvisor(_ => completion.Task);
        var pending = SlowContainerSemanticConsumer.AcquireAsync(
            SlowContainerSemanticMode.AsyncAdvisory,
            advisor,
            request);

        Assert.False(pending.IsCompleted);
        // Fast may finish while Slow is pending. The latest revision advances
        // while acquisition is pending and is read only during projection.
        var latestRevision = new SemanticEvidenceRevision(11);
        latestRevision = new SemanticEvidenceRevision(12);

        completion.SetResult(Assessment(
            request,
            SlowContainerSemanticAssessmentKind.Confirm,
            SlowContainerSceneKind.Normal));
        var invocation = await pending;
        var result = SlowContainerSemanticConsumer.Project(invocation, latestRevision);

        Assert.Equal(SlowContainerSemanticAvailability.Stale, result.Availability);
        Assert.True(result.IsStale);
        Assert.False(result.IsCurrent);
        Assert.NotNull(result.Assessment);
        Assert.False(result.HasRuntimeEffect);
    }

    [Fact]
    public async Task MismatchedBindingFailsClosedButRetainsRawAssessment()
    {
        var request = Request(13, node: "node:child", source: "node:root", trigger: "trigger:x", transition: "occ:x");
        var mismatched = new SlowContainerSemanticRequest(
            "observation:other",
            request.EvidenceRevision,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            new TransitionOccurrenceRef("occ:other"));
        var advisor = new FakeSlowAdvisor(_ => Assessment(
            mismatched,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.WrongChild,
            correctedIdentity: "Other"));

        var result = await ConsumeAsync(
            SlowContainerSemanticMode.Shadow,
            advisor,
            request,
            new SemanticEvidenceRevision(13));

        Assert.Equal(SlowContainerSemanticAvailability.Rejected, result.Availability);
        Assert.NotNull(result.Assessment);
        Assert.Equal("observation:other", result.Assessment!.ObservationRef);
        Assert.False(result.IsCurrent);
        Assert.False(result.IsStale);
        Assert.False(result.ConflictsWithFast);
        Assert.False(result.HasRuntimeEffect);
        Assert.Contains("binding", result.RejectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FutureResultFailsClosed()
    {
        var request = Request(14, node: "node:child", source: "node:root", trigger: "trigger:x", transition: "occ:x");
        var future = new SlowContainerSemanticRequest(
            request.ObservationRef,
            new SemanticEvidenceRevision(15),
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef);
        var advisor = new FakeSlowAdvisor(_ => Assessment(
            future,
            SlowContainerSemanticAssessmentKind.Confirm,
            SlowContainerSceneKind.Normal));

        var result = await ConsumeAsync(
            SlowContainerSemanticMode.Shadow,
            advisor,
            request,
            new SemanticEvidenceRevision(14));

        Assert.Equal(SlowContainerSemanticAvailability.Rejected, result.Availability);
        Assert.NotNull(result.Assessment);
        Assert.Equal(new SemanticEvidenceRevision(15), result.Assessment!.EvidenceRevision);
        Assert.False(result.IsCurrent);
        Assert.False(result.IsStale);
        Assert.False(result.ConflictsWithFast);
        Assert.False(result.HasRuntimeEffect);
    }

    [Fact]
    public async Task MissingAdvisorIsUnavailableAndDoesNotCreateAssessment()
    {
        var result = await ConsumeAsync(
            SlowContainerSemanticMode.Shadow,
            null,
            Request(15),
            new SemanticEvidenceRevision(15));

        Assert.Equal(SlowContainerSemanticAvailability.Unavailable, result.Availability);
        Assert.Null(result.Assessment);
        Assert.False(result.HasRuntimeEffect);
    }

    [Fact]
    public void FastAssessmentBindingMismatchIsRejectedAtRequestConstruction()
    {
        var request = Request(15, node: "node:child", source: "node:root");
        var fast = new FastContainerAssessment(
            FastContainerResolutionKind.NEW_CONTAINER,
            new SemanticEvidenceRevision(14),
            new ContainerNodeRef("node:other-root"),
            new ContainerNodeRef("node:other-child"),
            null,
            null,
            true,
            true,
            false,
            false,
            false);

        Assert.Throws<ArgumentException>(() => new SlowContainerSemanticRequest(
            request.ObservationRef,
            request.EvidenceRevision,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef,
            fast));
    }

    [Fact]
    public void RequestAndAssessmentCarryAllApplicableEvidenceReferences()
    {
        var request = Request(16, node: "node:d", source: "node:s", trigger: "trigger:t", transition: "occ:t");
        var assessment = Assessment(request, SlowContainerSemanticAssessmentKind.Confirm, SlowContainerSceneKind.Normal);

        Assert.Equal(request.ObservationRef, assessment.ObservationRef);
        Assert.Equal(request.EvidenceRevision, assessment.EvidenceRevision);
        Assert.Equal(request.NodeRef, assessment.NodeRef);
        Assert.Equal(request.SourceNodeRef, assessment.SourceNodeRef);
        Assert.Equal(request.TriggerOccurrenceRef, assessment.TriggerOccurrenceRef);
        Assert.Equal(request.TransitionOccurrenceRef, assessment.TransitionOccurrenceRef);
    }

    private static SlowContainerSemanticRequest Request(
        long revision,
        string? node = null,
        string? source = null,
        string? trigger = null,
        string? transition = null)
        => new(
            $"observation:{revision}",
            new SemanticEvidenceRevision(revision),
            node is null ? null : new ContainerNodeRef(node),
            source is null ? null : new ContainerNodeRef(source),
            trigger,
            transition is null ? null : new TransitionOccurrenceRef(transition));

    private static async Task<SlowContainerSemanticConsumption> ConsumeAsync(
        SlowContainerSemanticMode mode,
        ISlowContainerSemanticAdvisor? advisor,
        SlowContainerSemanticRequest request,
        SemanticEvidenceRevision latestRevision)
        => SlowContainerSemanticConsumer.Project(
            await SlowContainerSemanticConsumer.AcquireAsync(mode, advisor, request),
            latestRevision);

    private static SlowContainerSemanticAssessment Assessment(
        SlowContainerSemanticRequest request,
        SlowContainerSemanticAssessmentKind kind,
        SlowContainerSceneKind scene,
        string? correctedIdentity = null)
        => new(
            request.ObservationRef,
            request.EvidenceRevision,
            kind,
            scene,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef,
            correctedIdentity,
            kind is SlowContainerSemanticAssessmentKind.Challenge or SlowContainerSemanticAssessmentKind.Correct
                ? SlowContainerSemanticDisposition.ReassessFreshEvidence
                : SlowContainerSemanticDisposition.RetainEvidence,
            details: scene.ToString(),
            containerSemantic: kind is SlowContainerSemanticAssessmentKind.Correct ? "corrected-child" : "working-child",
            triggerSemantic: "entry-trigger",
            relationSemantic: "source-to-destination",
            evidenceUsefulness: scene is SlowContainerSceneKind.Advertisement
                or SlowContainerSceneKind.Transient
                or SlowContainerSceneKind.Loading
                or SlowContainerSceneKind.Overlay
                or SlowContainerSceneKind.Unrelated
                or SlowContainerSceneKind.OffPath
                ? SlowContainerEvidenceUsefulness.NotUseful
                : SlowContainerEvidenceUsefulness.Useful);

    private sealed class FakeSlowAdvisor : ISlowContainerSemanticAdvisor
    {
        private readonly Func<SlowContainerSemanticRequest, Task<SlowContainerSemanticAssessment>> _responder;

        public FakeSlowAdvisor()
            : this((Func<SlowContainerSemanticRequest, SlowContainerSemanticAssessment>)(_ =>
                throw new InvalidOperationException("disabled mode must not invoke the advisor"))) { }

        public FakeSlowAdvisor(Func<SlowContainerSemanticRequest, SlowContainerSemanticAssessment> responder)
            : this(request => Task.FromResult(responder(request))) { }

        public FakeSlowAdvisor(Func<SlowContainerSemanticRequest, Task<SlowContainerSemanticAssessment>> responder)
            => _responder = responder;

        public int CallCount { get; private set; }

        public Task<SlowContainerSemanticAssessment> AssessAsync(
            SlowContainerSemanticRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _responder(request);
        }
    }
}
