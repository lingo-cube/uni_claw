using System.Collections.Immutable;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Recovery;
using UniClaw.Runtime.Startup;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Verifies the single Agent-owned correction consumer boundary.</summary>
public sealed class AgentSemanticCorrectionConsumerTests
{
    [Fact]
    public async Task TraversalCorrectionRetractsOnlyExactCompletionAttribution()
    {
        var agent = NewAgent();
        var progress = Progress();
        SetProgress(agent, progress);
        var projection = await BuildProjection();

        var result = agent.ConsumeContainerSemanticCorrection(projection, projection.State);

        Assert.True(result.Accepted);
        Assert.Equal(projection.PendingCorrectionRef, result.CorrectionConsumptionRef);
        Assert.True(result.HasAppliedObligationMutation);
        Assert.False(result.HasAction);
        Assert.False(result.HasRecovery);
        Assert.False(result.HasGoalEvidenceMutation);
        Assert.False(result.HasCompletion);
        Assert.False(result.IsIdempotentNoChange);
        var updated = Assert.Single(result.ProgressSnapshot).Value;
        Assert.DoesNotContain("C", updated.CompletedSiblingEvidence.Keys);
        Assert.Equal(18, updated.CompletedSiblingEvidence["D"]);
        Assert.Contains("D", updated.AuthorizedSiblingEvidence.Keys);
    }

    [Fact]
    public async Task DuplicatePendingCorrectionIsIdempotentAndDoesNotAddObservedBranch()
    {
        var agent = NewAgent();
        var pending = new BranchProgressEvidence(
            "Parent",
            ImmutableDictionary<string, long>.Empty.Add("C", 1).Add("D", 2),
            ImmutableDictionary<string, long>.Empty,
            ImmutableDictionary<string, long>.Empty.Add("C", 3).Add("D", 4));
        SetProgress(agent, ImmutableDictionary<string, BranchProgressEvidence>.Empty.Add("Parent", pending));
        var projection = await BuildProjection();

        var result = agent.ConsumeContainerSemanticCorrection(projection, projection.State);

        Assert.True(result.Accepted);
        Assert.True(result.IsIdempotentNoChange);
        Assert.False(result.HasAppliedObligationMutation);
        Assert.DoesNotContain("Observed", result.ProgressSnapshot["Parent"].CompletedSiblingEvidence.Keys);
    }

    [Fact]
    public async Task DirectedWrongBranchIsReadOnlyAndRequiresSeparateOwnerDecision()
    {
        var agent = NewAgent();
        SetProgress(agent, ImmutableDictionary<string, BranchProgressEvidence>.Empty);
        var projection = await BuildProjection(ContainerObligationContextKind.DirectedEntryWrongBranch);

        var result = agent.ConsumeContainerSemanticCorrection(
            projection,
            projection.State);

        Assert.True(result.Accepted);
        Assert.True(result.RequiresSeparateOwnerAuthorization);
        Assert.False(result.HasAppliedObligationMutation);
        Assert.Equal("C", result.IntendedPendingCandidate);
        Assert.Equal("Observed", result.ObservedActualCandidate);
        Assert.Empty(result.ProgressSnapshot);
    }

    [Fact]
    public async Task ForgedOccurrenceBindingFailsClosedWithoutMutation()
    {
        var agent = NewAgent();
        var progress = Progress();
        SetProgress(agent, progress);
        var projection = await BuildProjection();

        var result = agent.ConsumeContainerSemanticCorrection(
            projection,
            ContainerRuntimeV2State.Empty);

        Assert.False(result.Accepted);
        Assert.Equal(progress["Parent"], result.ProgressSnapshot["Parent"]);
    }

    [Fact]
    public async Task ActualIntendedSemanticDoesNotRetractCompletion()
    {
        var agent = NewAgent();
        var progress = Progress();
        SetProgress(agent, progress);
        var projection = await BuildProjection(observedSemantic: "C");

        var result = agent.ConsumeContainerSemanticCorrection(projection, projection.State);

        Assert.True(result.Accepted);
        Assert.True(result.IsIdempotentNoChange);
        Assert.Equal(progress["Parent"], result.ProgressSnapshot["Parent"]);
    }

    [Theory]
    [InlineData("wrong-run", "slice:1")]
    [InlineData("run:1", "slice:wrong")]
    public async Task OwnerBindingMismatchFailsClosed(string runRef, string sliceRef)
    {
        var agent = NewAgent();
        var progress = Progress();
        SetProgress(agent, progress);
        var projection = await BuildProjection(runRef: runRef, sliceRef: sliceRef);

        var result = agent.ConsumeContainerSemanticCorrection(projection, projection.State);

        Assert.False(result.Accepted);
        Assert.Equal(progress["Parent"], result.ProgressSnapshot["Parent"]);
    }

    [Fact]
    public async Task HistoricalCorrectionCanRetractExactO17AttributionWhileKeepingO23State()
    {
        var agent = NewAgent();
        SetProgress(agent, Progress());
        var projection = await BuildProjection();
        var current = ContainerRuntimeV2Reducer.Prepare(
            projection.State,
            new ContainerRuntimeV2ReductionInput(
                new ContainerTransitionOccurrence(
                    new TransitionOccurrenceRef("occ:2"),
                    "observation:2",
                    new SemanticEvidenceRevision(2),
                    ContainerTransitionBoundary.SAME_CONTAINER,
                    true,
                    new ContainerNodeRef("node:parent"),
                    "trigger:2",
                    new ContainerNodeRef("node:child")),
                currentContainer: new CurrentContainer(
                    new ContainerNodeRef("node:child"),
                    new ContainerSliceRef("slice:2")))).State;
        var currentRevision = current.EvidenceRevision;
        var currentContainer = current.CurrentContainer;

        var result = agent.ConsumeContainerSemanticCorrection(projection, current);

        Assert.True(result.Accepted);
        Assert.Same(currentContainer, current.CurrentContainer);
        Assert.Equal(currentRevision, current.EvidenceRevision);
        Assert.DoesNotContain("C", result.ProgressSnapshot["Parent"].CompletedSiblingEvidence.Keys);
    }

    private static ImmutableDictionary<string, BranchProgressEvidence> Progress()
    {
        var value = new BranchProgressEvidence(
            "Parent",
            ImmutableDictionary<string, long>.Empty.Add("C", 1).Add("D", 2),
            ImmutableDictionary<string, long>.Empty.Add("C", 17).Add("D", 18),
            ImmutableDictionary<string, long>.Empty.Add("C", 3).Add("D", 4));
        return ImmutableDictionary<string, BranchProgressEvidence>.Empty.Add("Parent", value);
    }

    private static ContainerObligationContext Context(
        ContainerObligationContextKind kind = ContainerObligationContextKind.TraversalMisclick,
        string trigger = "trigger:1")
        => new(
            new ContainerObligationContextRef("obligation:1"),
            kind,
            "C",
            "run:1",
            "observation:1",
            new SemanticEvidenceRevision(1),
            new TransitionOccurrenceRef("occ:1"),
            trigger,
            new ContainerNodeRef("node:parent"),
            "Parent",
            new ContainerNodeRef("node:child"),
            new ContainerSliceRef("slice:1"),
            17);

    private static ContainerRuntimeV2State AcceptedState()
    {
        var occurrence = new ContainerTransitionOccurrence(
            new TransitionOccurrenceRef("occ:1"),
            "observation:1",
            new SemanticEvidenceRevision(1),
            ContainerTransitionBoundary.NEW_CONTAINER,
            true,
            new ContainerNodeRef("node:parent"),
            "trigger:1",
            new ContainerNodeRef("node:child"));
        var reduction = new ContainerRuntimeV2ReductionInput(
            occurrence,
            [new ContainerGraphNode(new ContainerNodeRef("node:parent")), new ContainerGraphNode(new ContainerNodeRef("node:child"))]);
        return ContainerRuntimeV2Reducer.Prepare(ContainerRuntimeV2State.Empty, reduction).State;
    }

    private static async Task<ContainerRuntimeV2ReadProjection> BuildProjection(
        ContainerObligationContextKind kind = ContainerObligationContextKind.TraversalMisclick,
        string observedSemantic = "Observed",
        string runRef = "run:1",
        string sliceRef = "slice:1")
    {
        var revision = new SemanticEvidenceRevision(1);
        var source = new ContainerNodeRef("node:parent");
        var destination = new ContainerNodeRef("node:child");
        var occurrenceRef = new TransitionOccurrenceRef("occ:1");
        var occurrence = new ContainerTransitionOccurrence(
            occurrenceRef, "observation:1", revision, ContainerTransitionBoundary.NEW_CONTAINER, true,
            source, "trigger:1", destination);
        var reduction = new ContainerRuntimeV2ReductionInput(
            occurrence,
            [new ContainerGraphNode(source), new ContainerGraphNode(destination)]);
        var request = new SlowContainerSemanticRequest(
            "observation:1", revision, destination, source, "trigger:1", occurrenceRef);
        var owner = Context(kind, "trigger:1");
        var context = new ContainerRuntimeV2EvidenceContext(
            runRef, "observation:1", 1, revision, occurrenceRef, "trigger:1", source, destination,
            new ContainerSliceRef(sliceRef), owner);
        var fast = new FastContainerResolutionRequest(
            revision, new ContainerSliceRef(sliceRef), 1, FastActionPriorKind.MAY_ENTER,
            source, destination, independentBoundarySupport: true, triggerDestinationSemanticMatch: true);
        var input = new ContainerRuntimeV2LifecycleInput(
            ContainerRuntimeV2State.Empty, context, reduction, fast,
            SlowContainerSemanticMode.Shadow, request,
            new TestAdvisor(observedSemantic));
        var result = await ContainerRuntimeV2.ComposeAsync(input);
        Assert.True(result.Accepted, result.RejectionReason);
        return result.ReadProjection!;
    }

    private static ContainerSemanticCorrectionFact Correction(ContainerRuntimeV2State state)
    {
        var request = new SlowContainerSemanticRequest(
            "observation:1",
            new SemanticEvidenceRevision(1),
            new ContainerNodeRef("node:child"),
            new ContainerNodeRef("node:parent"),
            "trigger:1",
            new TransitionOccurrenceRef("occ:1"));
        var assessment = new SlowContainerSemanticAssessment(
            request.ObservationRef,
            request.EvidenceRevision,
            SlowContainerSemanticAssessmentKind.Correct,
            SlowContainerSceneKind.WrongChild,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef,
            correctedIdentityCandidate: "Observed",
            triggerSemantic: "Observed");
        var consumption = new SlowContainerSemanticConsumption(
            SlowContainerSemanticMode.Shadow,
            SlowContainerSemanticAvailability.Available,
            assessment,
            true,
            false,
            true);
        return ContainerSemanticCorrectionProjector.TryCreateCorrection(consumption)!;
    }

    private static RuntimeAgent NewAgent()
    {
        var environment = new NoopEnvironment();
        Func<Observation, string?> resolver = _ => "Root";
        return new RuntimeAgent(
            new RuntimeStartup(environment, "test.app", resolver),
            new RuntimeTraversal(environment),
            _ => Task.FromResult(new Observation(ImmutableArray<ObservedElement>.Empty, "test.app", 1)),
            resolver,
            _ => new RuntimeContainer("Root", _ => true, (_, _, _) => new TraversalStepResult.Failed("unused")),
            new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true));
    }

    private static void SetProgress(RuntimeAgent agent, ImmutableDictionary<string, BranchProgressEvidence> progress)
        => typeof(RuntimeAgent).GetField("_branchProgress", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(agent, progress);

    private sealed class TestAdvisor : ISlowContainerSemanticAdvisor
    {
        private readonly string _observedSemantic;

        public TestAdvisor(string observedSemantic = "Observed")
            => _observedSemantic = observedSemantic;

        public Task<SlowContainerSemanticAssessment> AssessAsync(
            SlowContainerSemanticRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SlowContainerSemanticAssessment(
                request.ObservationRef,
                request.EvidenceRevision,
                SlowContainerSemanticAssessmentKind.Correct,
                SlowContainerSceneKind.WrongChild,
                request.NodeRef,
                request.SourceNodeRef,
                request.TriggerOccurrenceRef,
                request.TransitionOccurrenceRef,
                correctedIdentityCandidate: _observedSemantic,
                triggerSemantic: _observedSemantic));
    }

    private sealed class NoopEnvironment : IEnvironment
    {
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Observation(ImmutableArray<ObservedElement>.Empty, "test.app", 1));

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "noop", "noop"));
    }
}
