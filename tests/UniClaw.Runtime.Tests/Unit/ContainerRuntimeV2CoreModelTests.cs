using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class ContainerRuntimeV2CoreModelTests
{
    [Fact]
    public void WorkingUnprovenNodeCanCompletePhysicalOccurrence()
    {
        var occurrence = Occ("occ:1", 1, ContainerTransitionBoundary.NEW_CONTAINER, "desktop", "working");
        var result = Reduce(
            ContainerRuntimeV2State.Empty,
            occurrence,
            Node("desktop"),
            Node("working"),
            Current(occurrence, "working"));

        Assert.True(result.CanCommit);
        var node = Assert.Single(result.State.Graph.Nodes, n => n.NodeRef == new ContainerNodeRef("working"));
        Assert.Null(node.SemanticIdentityCandidate);
        Assert.Equal(ContainerNodeLifecycleStage.INITIALIZED, node.LifecycleStage);
        Assert.True(Assert.Single(result.State.TransitionOccurrences).IsCompleted);
        Assert.Equal(new ContainerNodeRef("working"), result.State.CurrentContainer!.NodeRef);
    }

    [Fact]
    public void CurrentLocationIsSeparateFromPendingObligationAndTrust()
    {
        var occurrence = Occ("r5:settings", 1, ContainerTransitionBoundary.NEW_CONTAINER, "display", "settings");
        var result = Reduce(
            ContainerRuntimeV2State.Empty,
            occurrence,
            Node("display"),
            Node("settings"),
            Current(occurrence, "settings"));

        Assert.True(result.CanCommit);
        Assert.Equal(new ContainerNodeRef("settings"), result.State.CurrentContainer!.NodeRef);
        Assert.DoesNotContain(result.State.GetType().GetProperties(), property =>
            property.Name.Contains("Obligation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.State.GetType().GetProperties(), property =>
            property.Name.Contains("Trust", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameDestinationThroughDesktopAndSearchPreservesDistinctRelationsAndEntries()
    {
        var first = Occ("occ:desktop", 1, ContainerTransitionBoundary.NEW_CONTAINER, "desktop", "settings", "open-settings");
        var firstState = Reduce(
            ContainerRuntimeV2State.Empty,
            first,
            Node("desktop"),
            Node("settings"),
            Current(first, "settings"),
            Relation("relation:desktop", "desktop", "settings", "affordance:desktop-settings"));

        var second = Occ("occ:search", 2, ContainerTransitionBoundary.NEW_CONTAINER, "search", "settings", "open-settings");
        var secondState = Reduce(
            firstState.State,
            second,
            Current(second, "settings"),
            Node("search"),
            Relation("relation:search", "search", "settings", "affordance:search-settings"));

        Assert.True(firstState.CanCommit);
        Assert.True(secondState.CanCommit);
        Assert.Single(secondState.State.Graph.Nodes, node => node.NodeRef == new ContainerNodeRef("settings"));
        Assert.Equal(2, secondState.State.Graph.Relations.Length);
        Assert.NotEqual(
            secondState.State.Graph.Relations[0].RelationRef,
            secondState.State.Graph.Relations[1].RelationRef);
        Assert.NotEqual(
            secondState.State.Graph.Relations[0].EntryAffordanceEvidenceRef,
            secondState.State.Graph.Relations[1].EntryAffordanceEvidenceRef);
        Assert.Equal(first.TriggerOccurrenceRef, second.TriggerOccurrenceRef);
        Assert.Equal(new ContainerNodeRef("search"), secondState.State.CurrentContainer!.EntryContext!.SourceNodeRef);
        Assert.Equal(new TransitionOccurrenceRef("occ:search"),
            secondState.State.CurrentContainer.EntryContext.EntryTransitionOccurrenceRef);
        var assessments = ContainerGraphQuery.ProjectRelationAssessments(secondState.State);
        Assert.Collection(
            assessments,
            assessment => Assert.Equal(ContainerGraphRelationAssessmentKind.SUPPORTED, assessment.Kind),
            assessment => Assert.Equal(ContainerGraphRelationAssessmentKind.SUPPORTED, assessment.Kind));
    }

    [Fact]
    public void RepeatedOccurrencesMaySupportOneRelationWithoutCollapsingOccurrences()
    {
        var first = Occ("occ:1", 1, ContainerTransitionBoundary.NEW_CONTAINER, "desktop", "settings", "trigger-occ:1", "affordance:desktop-settings");
        var relation = Relation("relation:desktop-settings", "desktop", "settings", "affordance:desktop-settings");
        var firstState = Reduce(ContainerRuntimeV2State.Empty, first, Node("desktop"), Node("settings"), Current(first, "settings"), relation);
        var second = Occ("occ:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "desktop", "settings", "trigger-occ:2", "affordance:desktop-settings");
        var secondState = Reduce(firstState.State, second, Current(second, "settings"), relation);

        Assert.True(secondState.CanCommit);
        Assert.Single(secondState.State.Graph.Relations);
        Assert.Equal(2, secondState.State.Graph.Relations[0].SupportingOccurrences.Length);
        Assert.Equal(2, secondState.State.TransitionOccurrences.Length);
    }

    [Fact]
    public void FreshCompletedOccurrenceChallengesHistoricalRelationWithoutRewritingGraph()
    {
        var historical = Occ(
            "occ:settings",
            1,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "settings",
            "same-display-text",
            "affordance:desktop-settings");
        var historicalState = Reduce(
            ContainerRuntimeV2State.Empty,
            historical,
            Node("desktop"),
            Node("settings"),
            Current(historical, "settings"),
            Relation("relation:desktop-settings", "desktop", "settings", "affordance:desktop-settings"));

        var fresh = Occ(
            "occ:other",
            2,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "other",
            "same-display-text",
            "affordance:desktop-settings");
        var next = Reduce(
            historicalState.State,
            fresh,
            Node("other"),
            Current(fresh, "other"));

        var relation = Assert.Single(next.State.Graph.Relations);
        var assessment = Assert.Single(ContainerGraphQuery.ProjectRelationAssessments(
            next.State,
            fresh,
            ContainerRelationEligibility.ELIGIBLE));

        Assert.True(next.CanCommit);
        Assert.Equal(new ContainerNodeRef("other"), next.State.CurrentContainer!.NodeRef);
        Assert.Equal(new ContainerNodeRef("settings"), relation.DestinationNodeRef);
        Assert.Equal(ContainerGraphRelationAssessmentKind.CHALLENGED, assessment.Kind);
        Assert.Equal(new TransitionOccurrenceRef("occ:other"), assessment.FreshOccurrenceRef);
        Assert.Equal(new ContainerNodeRef("other"), assessment.ObservedDestinationNodeRef);
        Assert.Single(next.State.Graph.Relations);
    }

    [Theory]
    [InlineData("missing-affordance", false)]
    [InlineData("incomplete", true)]
    public void FreshConflictRequiresAffordanceAndCompletedOccurrence(string caseName, bool hasAffordance)
    {
        var historical = Occ(
            "occ:historical-" + caseName,
            1,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "settings",
            affordance: "affordance:desktop-settings");
        var historicalState = Reduce(
            ContainerRuntimeV2State.Empty,
            historical,
            Node("desktop"),
            Node("settings"),
            Current(historical, "settings"),
            Relation("relation:desktop-settings-" + caseName, "desktop", "settings", "affordance:desktop-settings"));

        var fresh = Occ(
            "occ:fresh-" + caseName,
            2,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "other",
            affordance: hasAffordance ? "affordance:desktop-settings" : null,
            isCompleted: caseName != "incomplete");
        var next = Reduce(historicalState.State, fresh, Node("other"));
        var assessment = Assert.Single(ContainerGraphQuery.ProjectRelationAssessments(
            next.State,
            fresh,
            ContainerRelationEligibility.ELIGIBLE));

        Assert.True(next.CanCommit);
        Assert.Equal(ContainerGraphRelationAssessmentKind.SUPPORTED, assessment.Kind);
    }

    [Fact]
    public void UnacceptedFreshOccurrenceCannotChallengeHistoricalRelation()
    {
        var historical = Occ(
            "occ:historical-unaccepted",
            1,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "settings",
            affordance: "affordance:desktop-settings");
        var committed = Reduce(
            ContainerRuntimeV2State.Empty,
            historical,
            Node("desktop"),
            Node("settings"),
            Current(historical, "settings"),
            Relation("relation:desktop-settings-unaccepted", "desktop", "settings", "affordance:desktop-settings"));
        var unaccepted = Occ(
            "occ:unaccepted",
            2,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "other",
            affordance: "affordance:desktop-settings");
        var unacceptedException = Assert.Throws<ArgumentException>(() =>
            ContainerGraphQuery.ProjectRelationAssessments(
                committed.State,
                unaccepted,
                ContainerRelationEligibility.ELIGIBLE));

        var conflictingSameReference = Occ(
            "occ:historical-unaccepted",
            2,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "other",
            affordance: "affordance:desktop-settings");
        var conflictingException = Assert.Throws<ArgumentException>(() =>
            ContainerGraphQuery.ProjectRelationAssessments(
                committed.State,
                conflictingSameReference,
                ContainerRelationEligibility.ELIGIBLE));

        Assert.Contains("accepted state evidence", unacceptedException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accepted state evidence", conflictingException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(committed.State.Graph.Relations);
        Assert.Single(committed.State.TransitionOccurrences);
    }

    [Fact]
    public void UnrecordedRelationReferenceCannotBeAssessed()
    {
        var occurrence = Occ("occ:recorded", 1, ContainerTransitionBoundary.NEW_CONTAINER, "desktop", "settings");
        var committed = Reduce(
            ContainerRuntimeV2State.Empty,
            occurrence,
            Node("desktop"),
            Node("settings"),
            Current(occurrence, "settings"));

        var unrecorded = new ContainerRelationRef("relation:not-recorded");

        Assert.Throws<ArgumentException>(() => ContainerGraphQuery.AssessRelation(
            committed.State,
            unrecorded,
            occurrence,
            ContainerRelationEligibility.ELIGIBLE));
    }

    [Theory]
    [InlineData(ContainerTransitionBoundary.OFF_PATH)]
    [InlineData(ContainerTransitionBoundary.TRANSIENT)]
    public void NotEligibleAbnormalOccurrenceRemainsReadableAndQueryDoesNotMutate(
        ContainerTransitionBoundary boundary)
    {
        var historical = Occ(
            "occ:normal",
            1,
            ContainerTransitionBoundary.NEW_CONTAINER,
            "desktop",
            "settings",
            affordance: "affordance:desktop-settings");
        var historicalState = Reduce(
            ContainerRuntimeV2State.Empty,
            historical,
            Node("desktop"),
            Node("settings"),
            Current(historical, "settings"),
            Relation("relation:desktop-settings", "desktop", "settings", "affordance:desktop-settings"));

        var offPath = Occ(
            "occ:abnormal-query-" + boundary,
            2,
            boundary,
            "desktop",
            "launcher",
            affordance: "affordance:desktop-settings");
        var next = Reduce(
            historicalState.State,
            offPath,
            Node("launcher"),
            Current(offPath, "launcher"),
            ContainerRelationEligibility.NOT_ELIGIBLE);
        var priorState = next.State;
        var priorGraph = priorState.Graph;
        var priorOccurrences = priorState.TransitionOccurrences;
        var assessments = ContainerGraphQuery.ProjectRelationAssessments(
            priorState,
            offPath,
            ContainerRelationEligibility.NOT_ELIGIBLE);

        Assert.True(next.CanCommit);
        Assert.Contains(priorState.TransitionOccurrences, occurrence => occurrence.OccurrenceRef == offPath.OccurrenceRef);
        Assert.Single(priorState.Graph.Relations);
        Assert.Single(assessments);
        Assert.Equal(ContainerGraphRelationAssessmentKind.NOT_ELIGIBLE, assessments[0].Kind);
        Assert.Same(priorGraph, priorState.Graph);
        Assert.Equal(priorOccurrences, priorState.TransitionOccurrences);
        Assert.Single(priorState.Graph.Relations);
        Assert.Equal(2, priorState.TransitionOccurrences.Length);
    }

    [Fact]
    public void OffPathOccurrenceIsRetainedWithoutNormalRelation()
    {
        var occurrence = Occ("occ:off-path", 1, ContainerTransitionBoundary.OFF_PATH, "desktop", "launcher", "open-settings");
        var result = Reduce(
            ContainerRuntimeV2State.Empty,
            occurrence,
            Node("desktop"),
            Node("launcher"),
            Current(occurrence, "launcher"),
            Relation("relation:wrong", "desktop", "launcher", "open-settings"),
            ContainerRelationEligibility.NOT_ELIGIBLE);

        Assert.True(result.CanCommit);
        Assert.Single(result.State.TransitionOccurrences);
        Assert.Empty(result.State.Graph.Relations);
    }

    [Fact]
    public void StaleRevisionRejectsAtomicallyAndReturnsPriorStateReference()
    {
        var accepted = Occ("occ:new", 5, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b");
        var committed = Reduce(ContainerRuntimeV2State.Empty, accepted, Node("a"), Node("b"), Current(accepted, "b"));
        var stale = Occ("occ:stale", 4, ContainerTransitionBoundary.NEW_CONTAINER, "b", "c");
        var rejected = Reduce(committed.State, stale, Node("c"), Current(stale, "c"));

        Assert.True(committed.CanCommit);
        Assert.False(rejected.CanCommit);
        Assert.Contains("stale", rejected.RejectionReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Same(committed.State, rejected.State);
        Assert.Single(rejected.State.TransitionOccurrences);
        Assert.DoesNotContain(rejected.State.Graph.Nodes, node => node.NodeRef == new ContainerNodeRef("c"));
    }

    [Theory]
    [InlineData("unknown source")]
    [InlineData("entry mismatch")]
    [InlineData("destination mismatch")]
    [InlineData("relation mismatch")]
    [InlineData("duplicate occurrence")]
    public void InvalidStructuralCandidateRejectsWithoutPartialCommit(string invalidCase)
    {
        var first = Occ("occ:1", 1, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b");
        var committed = Reduce(ContainerRuntimeV2State.Empty, first, Node("a"), Node("b"), Current(first, "b"));
        ContainerRuntimeV2ReductionInput input;

        switch (invalidCase)
        {
            case "unknown source":
                input = new(Occ("occ:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "missing", "b"), currentContainer: Current(first, "b"));
                break;
            case "entry mismatch":
                input = new(Occ("occ:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "b", "c"),
                    [Node("c")], Current(first, "c"));
                break;
            case "destination mismatch":
                input = new(Occ("occ:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "a", "c"),
                    [Node("c")], Current(Occ("occ:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "a", "c"), "b"));
                break;
            case "relation mismatch":
                var occurrence = Occ("occ:2", 2, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b", "trigger-occ:2", "affordance:expected");
                input = new(occurrence, relation: Relation("relation:bad", "a", "b", "different"),
                    relationEligibility: ContainerRelationEligibility.ELIGIBLE);
                break;
            default:
                input = new(first, currentContainer: Current(first, "b"));
                break;
        }

        var rejected = ContainerRuntimeV2Reducer.Prepare(committed.State, input);
        Assert.False(rejected.CanCommit);
        Assert.False(string.IsNullOrWhiteSpace(rejected.RejectionReason));
        Assert.Same(committed.State, rejected.State);
    }

    [Fact]
    public void CurrentReplacementRequiresCompletedOccurrenceWithDestination()
    {
        var incomplete = Occ("occ:incomplete", 1, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b", isCompleted: false);
        var incompleteResult = Reduce(
            ContainerRuntimeV2State.Empty,
            incomplete,
            Node("a"),
            Node("b"),
            Current(incomplete, "b"));
        Assert.False(incompleteResult.CanCommit);
        Assert.Same(ContainerRuntimeV2State.Empty, incompleteResult.State);

        var noDestination = new ContainerTransitionOccurrence(
            new TransitionOccurrenceRef("occ:no-destination"),
            "observation:no-destination",
            new SemanticEvidenceRevision(1),
            ContainerTransitionBoundary.UNRESOLVED,
            isCompleted: true,
            sourceNodeRef: new ContainerNodeRef("a"));
        var noDestinationResult = Reduce(
            ContainerRuntimeV2State.Empty,
            noDestination,
            Node("a"),
            Current(new ContainerTransitionOccurrence(
                new TransitionOccurrenceRef("occ:no-destination"),
                "observation:no-destination",
                new SemanticEvidenceRevision(1),
                ContainerTransitionBoundary.UNRESOLVED,
                isCompleted: true,
                sourceNodeRef: new ContainerNodeRef("a")), "a"));
        Assert.False(noDestinationResult.CanCommit);
        Assert.Same(ContainerRuntimeV2State.Empty, noDestinationResult.State);
    }

    [Fact]
    public void EligibleRelationRequiresCompletedOccurrenceAndRelation()
    {
        var occurrence = Occ("occ:eligible", 1, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b");
        var missingRelation = ContainerRuntimeV2Reducer.Prepare(
            ContainerRuntimeV2State.Empty,
            new ContainerRuntimeV2ReductionInput(
                occurrence,
                [Node("a"), Node("b")],
                relationEligibility: ContainerRelationEligibility.ELIGIBLE));
        Assert.False(missingRelation.CanCommit);
        Assert.Same(ContainerRuntimeV2State.Empty, missingRelation.State);

        var incomplete = Occ("occ:eligible-incomplete", 1, ContainerTransitionBoundary.NEW_CONTAINER, "a", "b", isCompleted: false);
        var incompleteRelation = ContainerRuntimeV2Reducer.Prepare(
            ContainerRuntimeV2State.Empty,
            new ContainerRuntimeV2ReductionInput(
                incomplete,
                [Node("a"), Node("b")],
                relation: Relation("relation:eligible", "a", "b", "affordance:eligible"),
                relationEligibility: ContainerRelationEligibility.ELIGIBLE));
        Assert.False(incompleteRelation.CanCommit);
        Assert.Same(ContainerRuntimeV2State.Empty, incompleteRelation.State);
    }

    private static ContainerRuntimeV2Preparation Reduce(
        ContainerRuntimeV2State previous,
        ContainerTransitionOccurrence occurrence,
        params object[] values)
    {
        var nodes = values.OfType<ContainerGraphNode>().ToArray();
        var current = values.OfType<CurrentContainer>().SingleOrDefault();
        var relation = values.OfType<ContainerGraphRelation>().SingleOrDefault();
        var eligibility = values.OfType<ContainerRelationEligibility>().Any()
            ? values.OfType<ContainerRelationEligibility>().Single()
            : relation is null
                ? ContainerRelationEligibility.NOT_ELIGIBLE
                : ContainerRelationEligibility.ELIGIBLE;
        return ContainerRuntimeV2Reducer.Reduce(
            previous,
            new ContainerRuntimeV2ReductionInput(occurrence, nodes, current, relation, eligibility));
    }

    private static ContainerGraphNode Node(string value, string? identity = null)
        => new(new ContainerNodeRef(value), identity);

    private static CurrentContainer Current(ContainerTransitionOccurrence occurrence, string node)
        => new(
            new ContainerNodeRef(node),
            new ContainerSliceRef("slice:" + occurrence.FreshObservationRef),
            new ContainerEntryContext(
                occurrence.SourceNodeRef ?? new ContainerNodeRef("unknown-source"),
                occurrence.OccurrenceRef));

    private static ContainerTransitionOccurrence Occ(
        string id,
        long revision,
        ContainerTransitionBoundary boundary,
        string source,
        string destination,
        string trigger = "trigger",
        string? affordance = null,
        bool isCompleted = true)
        => new(
            new TransitionOccurrenceRef(id),
            "observation:" + id,
            new SemanticEvidenceRevision(revision),
            boundary,
            isCompleted,
            new ContainerNodeRef(source),
            trigger,
            new ContainerNodeRef(destination),
            ["evidence:" + id],
            affordance);

    private static ContainerGraphRelation Relation(
        string id,
        string source,
        string destination,
        string trigger)
        => new(
            new ContainerRelationRef(id),
            new ContainerNodeRef(source),
            new ContainerNodeRef(destination),
            trigger);
}
