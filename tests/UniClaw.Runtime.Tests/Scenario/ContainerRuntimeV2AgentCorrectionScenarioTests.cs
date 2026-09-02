using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Recovery;
using UniClaw.Runtime.Startup;
using UniClaw.Runtime.Traversal;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Integration proof for Runtime V2 correction consumption by Agent.</summary>
public sealed class ContainerRuntimeV2AgentCorrectionScenarioTests
{
    [Fact]
    public async Task CompositionThenAgentConsumerRetractsCAndPreservesABAndCompletedD()
    {
        var agent = NewAgent();
        SetProgress(agent, Progress(completedD: true));
        var projection = await BuildProjection();
        var result = agent.ConsumeContainerSemanticCorrection(projection, projection.State);
        Assert.True(result.Accepted);
        var progress = result.ProgressSnapshot["Parent"];
        Assert.DoesNotContain("C", progress.CompletedSiblingEvidence.Keys);
        Assert.Equal(18, progress.CompletedSiblingEvidence["D"]);
        Assert.Equal(1, progress.CompletedSiblingEvidence["A"]);
        Assert.Equal(2, progress.CompletedSiblingEvidence["B"]);
    }

    [Fact]
    public async Task CompositionThenAgentConsumerDoesNotAddUncompletedObservedD()
    {
        var agent = NewAgent();
        SetProgress(agent, Progress(completedD: false));
        var projection = await BuildProjection();
        var result = agent.ConsumeContainerSemanticCorrection(projection, projection.State);
        Assert.True(result.Accepted);
        var progress = result.ProgressSnapshot["Parent"];
        Assert.DoesNotContain("D", progress.CompletedSiblingEvidence.Keys);
        Assert.DoesNotContain("Observed", progress.CompletedSiblingEvidence.Keys);
    }

    private static async Task<ContainerRuntimeV2ReadProjection> BuildProjection()
    {
        var revision = new SemanticEvidenceRevision(17);
        var source = new ContainerNodeRef("node:parent");
        var destination = new ContainerNodeRef("node:wrong");
        var occurrenceRef = new TransitionOccurrenceRef("occ:17");
        var slice = new ContainerSliceRef("slice:17");
        var occurrence = new ContainerTransitionOccurrence(
            occurrenceRef, "observation:17", revision, ContainerTransitionBoundary.NEW_CONTAINER, true,
            source, "trigger:17", destination);
        var owner = new ContainerObligationContext(
            new ContainerObligationContextRef("obligation:17"),
            ContainerObligationContextKind.TraversalMisclick,
            "C", "run:17", "observation:17", revision, occurrenceRef, "trigger:17",
            source, "Parent", destination, slice, 17);
        var context = new ContainerRuntimeV2EvidenceContext(
            "run:17", "observation:17", 17, revision, occurrenceRef, "trigger:17",
            source, destination, slice, owner);
        var input = new ContainerRuntimeV2LifecycleInput(
            ContainerRuntimeV2State.Empty,
            context,
            new ContainerRuntimeV2ReductionInput(
                occurrence,
                [new ContainerGraphNode(source), new ContainerGraphNode(destination)]),
            new FastContainerResolutionRequest(
                revision, slice, 17, FastActionPriorKind.MAY_ENTER, source, destination,
                independentBoundarySupport: true, triggerDestinationSemanticMatch: true),
            SlowContainerSemanticMode.Shadow,
            new SlowContainerSemanticRequest(
                "observation:17", revision, destination, source, "trigger:17", occurrenceRef),
            new ScenarioAdvisor());
        var result = await ContainerRuntimeV2.ComposeAsync(input);
        Assert.True(result.Accepted, result.RejectionReason);
        return result.ReadProjection!;
    }

    private static ImmutableDictionary<string, BranchProgressEvidence> Progress(bool completedD)
    {
        var completed = ImmutableDictionary<string, long>.Empty.Add("A", 1).Add("B", 2).Add("C", 17);
        if (completedD)
            completed = completed.Add("D", 18);
        return ImmutableDictionary<string, BranchProgressEvidence>.Empty.Add(
            "Parent",
            new BranchProgressEvidence(
                "Parent",
                ImmutableDictionary<string, long>.Empty.Add("A", 1).Add("B", 2).Add("C", 3).Add("D", 4),
                completed,
                ImmutableDictionary<string, long>.Empty.Add("A", 5).Add("B", 6).Add("C", 7).Add("D", 8)));
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

    private sealed class ScenarioAdvisor : ISlowContainerSemanticAdvisor
    {
        public Task<SlowContainerSemanticAssessment> AssessAsync(
            SlowContainerSemanticRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SlowContainerSemanticAssessment(
                request.ObservationRef, request.EvidenceRevision,
                SlowContainerSemanticAssessmentKind.Correct,
                SlowContainerSceneKind.WrongChild,
                request.NodeRef, request.SourceNodeRef,
                request.TriggerOccurrenceRef, request.TransitionOccurrenceRef,
                correctedIdentityCandidate: "D", triggerSemantic: "D"));
    }

    private sealed class NoopEnvironment : IEnvironment
    {
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Observation(ImmutableArray<ObservedElement>.Empty, "test.app", 1));

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "noop", "noop"));
    }
}
