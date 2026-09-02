using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;
using AdmittedSemanticEvidence = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Deterministic coverage for the stateless V2 composition seam.</summary>
public sealed class ContainerRuntimeV2CompositionTests
{
    [Fact]
    public async Task ComposeProducesOneCorrelatedReadProjection()
    {
        var input = Input(SlowContainerSemanticMode.Shadow, new TestAdvisor());

        var result = await ContainerRuntimeV2.ComposeAsync(input);

        Assert.True(result.Accepted, result.RejectionReason);
        Assert.NotNull(result.ReadProjection);
        Assert.Equal(input.EvidenceContext.ObservationRef, result.ReadProjection!.EvidenceContext.ObservationRef);
        Assert.Equal(input.EvidenceContext.EvidenceRevision, result.ReadProjection.State.EvidenceRevision);
        Assert.Equal(FastContainerResolutionKind.NEW_CONTAINER, result.ReadProjection.FastAssessment.Resolution);
        Assert.Equal(SlowContainerSemanticAvailability.Available, result.ReadProjection.SlowConsumption.Availability);
        Assert.NotNull(result.ReadProjection.Correction);
        Assert.Equal("Observed", result.ReadProjection.Correction!.ActualTriggerSemantic);
    }

    [Fact]
    public async Task ComposeRejectsBindingMismatchWithoutPartialState()
    {
        var input = Input(SlowContainerSemanticMode.Disabled, null, new SemanticEvidenceRevision(99));

        var result = await ContainerRuntimeV2.ComposeAsync(input);

        Assert.False(result.Accepted);
        Assert.Same(input.PreviousState, result.State);
        Assert.Null(result.ReadProjection);
    }

    [Fact]
    public async Task DisabledSlowDoesNotInvokeAdvisorAndLeavesCorrectionAbsent()
    {
        var advisor = new TestAdvisor();
        var result = await ContainerRuntimeV2.ComposeAsync(Input(SlowContainerSemanticMode.Disabled, advisor));

        Assert.True(result.Accepted, result.RejectionReason);
        Assert.False(advisor.Invoked);
        Assert.Equal(SlowContainerSemanticAvailability.Disabled, result.ReadProjection!.SlowConsumption.Availability);
        Assert.Null(result.ReadProjection.Correction);
    }

    [Fact]
    public async Task SameRevisionSlowConfirmSemanticCandidateTakesPrecedenceOverFast()
    {
        var result = await ContainerRuntimeV2.ComposeAsync(
            Input(SlowContainerSemanticMode.Shadow, new TestAdvisor(SlowContainerSemanticAssessmentKind.Confirm, "Confirmed")));

        Assert.True(result.Accepted, result.RejectionReason);
        Assert.Equal(ContainerRuntimeV2SemanticTrustSource.Slow, result.ReadProjection!.SemanticTrust.Source);
        Assert.Equal("Confirmed", result.ReadProjection.SemanticTrust.SemanticCandidate);
        Assert.True(result.ReadProjection.SemanticTrust.IsCurrent);
    }

    [Fact]
    public async Task ProducedFastAssessmentIsBoundIntoSlowAndSlowDRemainsCurrentWinner()
    {
        var advisor = new TestAdvisor(SlowContainerSemanticAssessmentKind.Correct, "D");
        var result = await ContainerRuntimeV2.ComposeAsync(
            Input(SlowContainerSemanticMode.Shadow, advisor, fastCandidate: "C"));

        Assert.True(result.Accepted, result.RejectionReason);
        Assert.True(result.ReadProjection!.SlowConsumption.ConflictsWithFast);
        Assert.Equal(ContainerRuntimeV2SemanticTrustSource.Slow, result.ReadProjection.SemanticTrust.Source);
        Assert.Equal("D", result.ReadProjection.SemanticTrust.SemanticCandidate);
        Assert.Equal("D", result.ReadProjection.Correction!.ActualTriggerSemantic);
        Assert.Equal("C", result.ReadProjection.FastAssessment.IdentityCandidate);
        Assert.Equal("C", advisor.FastCandidateSeen);
    }

    [Fact]
    public async Task StartReturnsFastBeforeSlowAndCompleteMarksOlderSlowStale()
    {
        var advisor = new GatedAdvisor();
        var input = Input(
            SlowContainerSemanticMode.Shadow,
            advisor,
            checkpointPath: new ContainerExecutionPath(new[]
            {
                new ContainerPathConfirmation(
                    "observation:1", new SemanticEvidenceRevision(1),
                    new ContainerNodeRef("node:destination"), true, true),
            }));

        var started = ContainerRuntimeV2.Start(input);

        Assert.True(started.Accepted, started.RejectionReason);
        Assert.False(started.SlowAcquisition!.IsCompleted);
        Assert.NotNull(started.Checkpoint);
        var newerOccurrence = new ContainerTransitionOccurrence(
            new TransitionOccurrenceRef("occ:2"), "observation:2", new SemanticEvidenceRevision(2),
            ContainerTransitionBoundary.SAME_CONTAINER, true,
            new ContainerNodeRef("node:source"), "trigger:2", new ContainerNodeRef("node:destination"));
        var newer = ContainerRuntimeV2Reducer.Prepare(
            started.State,
            new ContainerRuntimeV2ReductionInput(newerOccurrence)).State;
        advisor.Complete();

        var completed = ContainerRuntimeV2.CompleteSlow(
            started,
            await started.SlowAcquisition,
            newer);

        Assert.True(completed.Accepted, completed.RejectionReason);
        Assert.Same(newer, completed.State);
        Assert.Equal(SlowContainerSemanticAvailability.Stale, completed.ReadProjection!.SlowConsumption.Availability);
        Assert.Null(completed.ReadProjection.Correction);
        Assert.Null(completed.ReadProjection.Checkpoint);
        Assert.False(completed.ReadProjection.SemanticTrust.IsCurrent);
        Assert.All(completed.ReadProjection.RelationAssessments,
            assessment => Assert.Equal(new SemanticEvidenceRevision(2), assessment.AssessmentRevision));
    }

    [Fact]
    public void WrongTransitionReferenceFailsClosedAtStart()
    {
        var result = ContainerRuntimeV2.Start(
            Input(SlowContainerSemanticMode.Disabled, null, contextOccurrenceRef: new TransitionOccurrenceRef("occ:wrong")));

        Assert.False(result.Accepted);
        Assert.Contains("bound", result.RejectionReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WrongTriggerOccurrenceReferenceFailsClosedAtStart()
    {
        var result = ContainerRuntimeV2.Start(
            Input(SlowContainerSemanticMode.Disabled, null, contextTrigger: "trigger:wrong"));

        Assert.False(result.Accepted);
        Assert.Contains("bound", result.RejectionReason!, StringComparison.OrdinalIgnoreCase);
    }

    private static ContainerRuntimeV2LifecycleInput Input(
        SlowContainerSemanticMode mode,
        ISlowContainerSemanticAdvisor? advisor,
        SemanticEvidenceRevision? fastRevision = null,
        ContainerExecutionPath? checkpointPath = null,
        TransitionOccurrenceRef? contextOccurrenceRef = null,
        string? contextTrigger = null,
        string? fastCandidate = null)
    {
        var revision = new SemanticEvidenceRevision(1);
        var source = new ContainerNodeRef("node:source");
        var destination = new ContainerNodeRef("node:destination");
        var occurrenceRef = new TransitionOccurrenceRef("occ:1");
        var trigger = "trigger:1";
        var slice = new ContainerSliceRef("slice:1");
        var occurrence = new ContainerTransitionOccurrence(
            occurrenceRef, "observation:1", revision, ContainerTransitionBoundary.NEW_CONTAINER, true,
            source, trigger, destination, ["evidence:1"], "affordance:1");
        var relation = new ContainerGraphRelation(
            new ContainerRelationRef("relation:1"), source, destination, "affordance:1");
        var reduction = new ContainerRuntimeV2ReductionInput(
            occurrence,
            [new ContainerGraphNode(source, "Source"), new ContainerGraphNode(destination)],
            new CurrentContainer(destination, slice, new ContainerEntryContext(source, occurrenceRef)),
            relation,
            ContainerRelationEligibility.ELIGIBLE);
        var validated = fastCandidate is null
            ? null
            : new ValidatedSemanticEvidenceResult(
                [new AdmittedSemanticEvidence(
                    "evidence:fast", "v1", "FAST", SemanticEvidenceKind.ContainerIdentity,
                    fastCandidate, 0.95, SemanticEvidenceScope.CurrentObservation, 1, DateTimeOffset.UtcNow)],
                ImmutableArray<AdmittedSemanticEvidence>.Empty,
                ImmutableArray<SemanticEvidenceRejection>.Empty,
                [new SemanticEvidenceWeight("evidence:fast", 0.95)]);
        var fast = new FastContainerResolutionRequest(
            fastRevision ?? revision, slice, 1, FastActionPriorKind.MAY_ENTER, source, destination,
            independentBoundarySupport: true, triggerDestinationSemanticMatch: true,
            validatedSemanticEvidence: validated);
        var slowRequest = new SlowContainerSemanticRequest(
            "observation:1", revision, destination, source, trigger, occurrenceRef);
        var context = new ContainerRuntimeV2EvidenceContext(
            "run:1", "observation:1", 1, revision, contextOccurrenceRef ?? occurrenceRef,
            contextTrigger ?? trigger, source, destination, slice);
        return new ContainerRuntimeV2LifecycleInput(
            ContainerRuntimeV2State.Empty, context, reduction, fast, mode, slowRequest, advisor, checkpointPath);
    }

    private sealed class TestAdvisor : ISlowContainerSemanticAdvisor
    {
        private readonly SlowContainerSemanticAssessmentKind _kind;
        private readonly string _semantic;
        public string? FastCandidateSeen { get; private set; }

        public TestAdvisor(
            SlowContainerSemanticAssessmentKind kind = SlowContainerSemanticAssessmentKind.Correct,
            string semantic = "Observed")
        {
            _kind = kind;
            _semantic = semantic;
        }

        public bool Invoked { get; private set; }

        public Task<SlowContainerSemanticAssessment> AssessAsync(
            SlowContainerSemanticRequest request,
            CancellationToken cancellationToken = default)
        {
            Invoked = true;
            FastCandidateSeen = request.FastAssessment?.IdentityCandidate;
            return Task.FromResult(new SlowContainerSemanticAssessment(
                request.ObservationRef, request.EvidenceRevision,
                _kind,
                SlowContainerSceneKind.WrongChild,
                request.NodeRef, request.SourceNodeRef, request.TriggerOccurrenceRef,
                request.TransitionOccurrenceRef, _semantic, details: "bounded test evidence",
                containerSemantic: _semantic + "Page", triggerSemantic: _semantic));
        }
    }

    private sealed class GatedAdvisor : ISlowContainerSemanticAdvisor
    {
        private readonly TaskCompletionSource<SlowContainerSemanticAssessment> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private SlowContainerSemanticRequest? _request;

        public Task<SlowContainerSemanticAssessment> AssessAsync(
            SlowContainerSemanticRequest request,
            CancellationToken cancellationToken = default)
        {
            _request = request;
            return _completion.Task;
        }

        public void Complete()
        {
            var request = _request ?? throw new InvalidOperationException("Slow request was not started.");
            _completion.SetResult(new SlowContainerSemanticAssessment(
                request.ObservationRef, request.EvidenceRevision,
                SlowContainerSemanticAssessmentKind.Correct,
                SlowContainerSceneKind.WrongChild,
                request.NodeRef, request.SourceNodeRef, request.TriggerOccurrenceRef,
                request.TransitionOccurrenceRef, "Observed", triggerSemantic: "Observed"));
        }
    }
}
