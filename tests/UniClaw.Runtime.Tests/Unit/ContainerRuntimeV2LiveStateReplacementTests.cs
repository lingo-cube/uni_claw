using System.Reflection;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>Deterministic checks for the Agent V2 live-state owner replacement.</summary>
public sealed class ContainerRuntimeV2LiveStateReplacementTests
{
    /// <summary>Independent Unknown evidence creates a working node instead of reusing an Unknown identity.</summary>
    [Fact]
    public void IndependentUnknownBoundaryDoesNotReuseUnknownNode()
    {
        var first = Occ("unknown:1", 1, ContainerTransitionBoundary.NEW_CONTAINER, "source", "unknown-a");
        var firstState = Reduce(
            ContainerRuntimeV2State.Empty,
            first,
            new ContainerGraphNode(new ContainerNodeRef("source"), "Source"),
            new ContainerGraphNode(new ContainerNodeRef("unknown-a")),
            Current(first, "unknown-a"));

        var second = Occ("unknown:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "unknown-a", "unknown-b");
        var secondState = Reduce(
            firstState.State,
            second,
            new ContainerGraphNode(new ContainerNodeRef("unknown-b")),
            Current(second, "unknown-b"));

        Assert.True(secondState.CanCommit, secondState.RejectionReason);
        Assert.Equal(3, secondState.State.Graph.Nodes.Length);
        Assert.Contains(secondState.State.Graph.Nodes, node => node.NodeRef == new ContainerNodeRef("unknown-b"));
        Assert.Equal(new ContainerNodeRef("unknown-b"), secondState.State.CurrentContainer!.NodeRef);
    }

    /// <summary>Strong same-container continuity can preserve one unresolved working node.</summary>
    [Fact]
    public void SameContainerUnknownContinuityPreservesWorkingNodeReference()
    {
        var first = Occ("same:1", 1, ContainerTransitionBoundary.SAME_CONTAINER, "source", "working");
        var firstState = Reduce(
            ContainerRuntimeV2State.Empty,
            first,
            new ContainerGraphNode(new ContainerNodeRef("source"), "Source"),
            new ContainerGraphNode(new ContainerNodeRef("working")),
            Current(first, "working"));
        var second = Occ("same:2", 2, ContainerTransitionBoundary.SAME_CONTAINER, "working", "working");
        var secondState = Reduce(firstState.State, second, Current(second, "working"));

        Assert.True(secondState.CanCommit, secondState.RejectionReason);
        Assert.Equal(new ContainerNodeRef("working"), secondState.State.CurrentContainer!.NodeRef);
        Assert.Single(secondState.State.Graph.Nodes, node => node.NodeRef == new ContainerNodeRef("working"));
    }

    /// <summary>Rejected stale candidates preserve the exact previous V2 snapshot.</summary>
    [Fact]
    public void StaleCandidatePreservesAllV2OwnersWithoutCommit()
    {
        var accepted = Occ("accepted", 4, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b");
        var committed = Reduce(
            ContainerRuntimeV2State.Empty,
            accepted,
            new ContainerGraphNode(new ContainerNodeRef("a"), "A"),
            new ContainerGraphNode(new ContainerNodeRef("b"), "B"),
            Current(accepted, "b"));
        var stale = Occ("stale", 4, ContainerTransitionBoundary.NEW_CONTAINER, "b", "c");
        var rejected = Reduce(
            committed.State,
            stale,
            new ContainerGraphNode(new ContainerNodeRef("c")),
            Current(stale, "c"));

        Assert.False(rejected.CanCommit);
        Assert.Same(committed.State, rejected.State);
        Assert.Equal(4, rejected.State.EvidenceRevision.Value);
        Assert.Single(rejected.State.TransitionOccurrences);
        Assert.DoesNotContain(rejected.State.Graph.Nodes, node => node.NodeRef == new ContainerNodeRef("c"));
    }

    /// <summary>Observed current location remains independent from execution obligations.</summary>
    [Fact]
    public void ObservedCurrentDoesNotInventExecutionObligation()
    {
        var occurrence = Occ("r5", 1, ContainerTransitionBoundary.NEW_CONTAINER, "a", "observed-d");
        var result = Reduce(
            ContainerRuntimeV2State.Empty,
            occurrence,
            new ContainerGraphNode(new ContainerNodeRef("a"), "A"),
            new ContainerGraphNode(new ContainerNodeRef("observed-d"), "D"),
            Current(occurrence, "observed-d"));

        Assert.True(result.CanCommit, result.RejectionReason);
        Assert.Equal(new ContainerNodeRef("observed-d"), result.State.CurrentContainer!.NodeRef);
        Assert.DoesNotContain(result.State.GetType().GetProperties(), property =>
            property.Name.Contains("Obligation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Off-path occurrence remains readable but does not create a normal relation.</summary>
    [Fact]
    public void OffPathOccurrenceHasNoNormalRelation()
    {
        var occurrence = Occ("off-path", 1, ContainerTransitionBoundary.OFF_PATH, "a", "d");
        var result = Reduce(
            ContainerRuntimeV2State.Empty,
            occurrence,
            new ContainerGraphNode(new ContainerNodeRef("a"), "A"),
            new ContainerGraphNode(new ContainerNodeRef("d"), "D"),
            Current(occurrence, "d"));

        Assert.True(result.CanCommit, result.RejectionReason);
        Assert.Empty(result.State.Graph.Relations);
        Assert.Single(result.State.TransitionOccurrences, item => item.Boundary == ContainerTransitionBoundary.OFF_PATH);
    }

    /// <summary>Proves the compatibility belief follows the accepted V2 snapshot.</summary>
    [Fact]
    public async Task AgentBeliefProjectsFromTheSoleV2StateSlot()
    {
        var harness = UniClaw.Runtime.Tests.Scenario.ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var stateField = Assert.Single(
            typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(ContainerRuntimeV2State)));
        var state = Assert.IsType<ContainerRuntimeV2State>(stateField.GetValue(harness.Agent));
        var belief = harness.Agent.Belief;
        Assert.NotNull(belief);
        Assert.Equal(state.EvidenceRevision.Value, belief.SourceObservationSequence);
        Assert.Equal(
            state.Graph.Nodes.Single(node => node.NodeRef == state.CurrentContainer!.NodeRef)
                .SemanticIdentityCandidate,
            belief.SemanticPage);
        Assert.DoesNotContain(
            typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => string.Equals(field.Name, "_belief", StringComparison.Ordinal));
    }

    /// <summary>Proves an accepted occurrence is retained with the projected current evidence.</summary>
    [Fact]
    public async Task AcceptedRunRetainsImmutableOccurrenceAndCurrentProjection()
    {
        var harness = UniClaw.Runtime.Tests.Scenario.ScenarioHarness.Create("same-text");

        await harness.RunAsync();

        var stateField = Assert.Single(
            typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(ContainerRuntimeV2State)));
        var state = Assert.IsType<ContainerRuntimeV2State>(stateField.GetValue(harness.Agent));
        Assert.NotNull(state.CurrentContainer);
        Assert.NotEmpty(state.TransitionOccurrences);
        Assert.Equal(state.EvidenceRevision, state.TransitionOccurrences[^1].EvidenceRevision);
        Assert.Equal(
            state.TransitionOccurrences[^1].DestinationNodeRef,
            state.CurrentContainer!.NodeRef);
    }

    private static ContainerRuntimeV2Preparation Reduce(
        ContainerRuntimeV2State previous,
        ContainerTransitionOccurrence occurrence,
        params object[] values)
    {
        var nodes = values.OfType<ContainerGraphNode>().ToArray();
        var current = values.OfType<CurrentContainer>().SingleOrDefault();
        return ContainerRuntimeV2Reducer.Prepare(
            previous,
            new ContainerRuntimeV2ReductionInput(occurrence, nodes, current));
    }

    private static ContainerTransitionOccurrence Occ(
        string id,
        long revision,
        ContainerTransitionBoundary boundary,
        string source,
        string destination)
        => new(
            new TransitionOccurrenceRef(id),
            "observation:" + revision,
            new SemanticEvidenceRevision(revision),
            boundary,
            true,
            new ContainerNodeRef(source),
            "trigger:" + id,
            new ContainerNodeRef(destination),
            ["evidence:" + id]);

    private static CurrentContainer Current(ContainerTransitionOccurrence occurrence, string node)
        => new(
            new ContainerNodeRef(node),
            new ContainerSliceRef("slice:" + occurrence.OccurrenceRef.Value),
            new ContainerEntryContext(
                occurrence.SourceNodeRef ?? new ContainerNodeRef("source"),
                occurrence.OccurrenceRef));
}
