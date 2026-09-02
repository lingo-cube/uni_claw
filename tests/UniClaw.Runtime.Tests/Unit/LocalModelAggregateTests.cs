using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Stage C1 task 3.1 capability coverage: the per-Node immutable LocalModel
/// aggregate enters ContainerRuntimeV2State through the single reducer seam —
/// append-only evidence, active/archived layering, whole replacement, stale
/// revision returning the exact prior state, and no dangling references
/// (container-runtime-v2-evidence-model, spec: canonical-world
/// "LocalModel 是唯一 container-local canonical world owner").
/// </summary>
public sealed class LocalModelAggregateTests
{
    private static readonly ContainerNodeRef Node = new("node:1");
    private static readonly ElementBounds FullBounds = new(0f, 0f, 1f, 1f);

    private static ContainerRuntimeV2State AcceptedState()
    {
        var slice = new ContainerSlice(
            new ContainerSliceRef("slice:1"),
            new SemanticEvidenceRevision(1),
            observationRef: "observation:1",
            viewportBounds: FullBounds,
            spatialRegionRefs: [new SpatialRegionRef("primary")],
            occurrenceRefs: [new ViewportOccurrenceRef("occ:1")],
            fastAssessmentRefs: [new FastAssessmentRef("fast:1")],
            stabilityEvidenceRef: new StabilityEvidenceRef("stability:1"));

        var region = new SpatialRegion(
            "primary",
            SpatialRegionKind.ScrollableContent,
            FullBounds,
            ParticipatesInScroll: true,
            ParticipatesInCoverage: true,
            ParticipatesInGrounding: true);

        var occurrence = new Occurrence(
            new ViewportOccurrenceRef("occ:1"),
            new ContainerSliceRef("slice:1"),
            VisualPrimitiveKind.Text,
            new ElementBounds(0.1f, 0.2f, 0.9f, 0.3f),
            new OccurrenceRegionBinding(
                new ViewportOccurrenceRef("occ:1"),
                new SpatialRegionRef("primary"),
                OverlapRatio: 1d,
                Ambiguous: false),
            "vision:occ:1");

        var assessment = new FastAssessment(
            new FastAssessmentRef("fast:1"),
            new ContainerSliceRef("slice:1"),
            [new ViewportOccurrenceRef("occ:1")],
            FastStructureHint.ListItem,
            FastMemberRoleHint.Primary,
            FastAffordanceHint.Navigate,
            "fast:test");

        var preparation = ContainerRuntimeV2Reducer.PrepareAcceptedEvidence(
            ContainerRuntimeV2State.Empty,
            new SliceAcceptanceCommit(slice, [region], [occurrence], [assessment]));

        Assert.True(preparation.CanCommit, preparation.RejectionReason);

        var node = new ContainerGraphNode(Node, "settings");
        return new ContainerRuntimeV2State(
            new ContainerGraphSnapshot(nodes: [node]),
            evidenceRevision: preparation.State.EvidenceRevision,
            slices: preparation.State.Slices,
            spatialRegions: preparation.State.SpatialRegions,
            occurrences: preparation.State.Occurrences,
            fastAssessments: preparation.State.FastAssessments);
    }

    private static LocalModelAppendInput Append(
        ContainerRuntimeV2State state,
        SemanticEvidenceRevision? revision = null,
        ImmutableArray<ContainerSliceRef>? slices = null,
        ImmutableArray<ViewportOccurrenceRef>? occurrences = null,
        ImmutableArray<ContainerSliceRef>? archiveSlices = null,
        ImmutableArray<ViewportOccurrenceRef>? archiveOccurrences = null,
        ImmutableArray<FastAssessmentRef>? assessments = null,
        ContainerNodeRef? node = null)
        => new(
            node ?? Node,
            revision ?? new SemanticEvidenceRevision(state.EvidenceRevision.Value + 1),
            slices,
            occurrences,
            archiveSlices,
            archiveOccurrences,
            assessments);

    [Fact]
    public void AppendCreatesModelWithActiveLayerAndPreservesPriorState()
    {
        var previous = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            previous,
            Append(previous,
                slices: [new ContainerSliceRef("slice:1")],
                occurrences: [new ViewportOccurrenceRef("occ:1")],
                assessments: [new FastAssessmentRef("fast:1")]));

        Assert.True(preparation.CanCommit, preparation.RejectionReason);
        var model = Assert.Single(preparation.State.LocalModels);
        Assert.Equal(Node, model.NodeRef);
        Assert.Contains(new ContainerSliceRef("slice:1"), model.ActiveSliceRefs);
        Assert.Contains(new ViewportOccurrenceRef("occ:1"), model.ActiveOccurrenceRefs);
        Assert.Contains(new FastAssessmentRef("fast:1"), model.FastAssessmentRefs);
        // Whole replacement: the prior state is untouched.
        Assert.NotSame(previous, preparation.State);
        Assert.Empty(previous.LocalModels);
        Assert.True(model.IsValid);
    }

    [Fact]
    public void AppendingAlreadyLayeredReferenceIsRejectedWithExactPriorState()
    {
        var state = AcceptedState();
        var first = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, slices: [new ContainerSliceRef("slice:1")]));
        Assert.True(first.CanCommit, first.RejectionReason);

        var second = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            first.State,
            Append(first.State, slices: [new ContainerSliceRef("slice:1")]));

        Assert.False(second.CanCommit);
        Assert.Same(first.State, second.State);
    }

    [Fact]
    public void ArchivalMovesActiveToArchivedAndRetainsRelocationAnchor()
    {
        var state = AcceptedState();
        var activated = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state,
                slices: [new ContainerSliceRef("slice:1")],
                occurrences: [new ViewportOccurrenceRef("occ:1")]));
        Assert.True(activated.CanCommit, activated.RejectionReason);

        var archived = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            activated.State,
            Append(activated.State,
                archiveSlices: [new ContainerSliceRef("slice:1")],
                archiveOccurrences: [new ViewportOccurrenceRef("occ:1")]));

        Assert.True(archived.CanCommit, archived.RejectionReason);
        var model = Assert.Single(archived.State.LocalModels);
        Assert.DoesNotContain(new ContainerSliceRef("slice:1"), model.ActiveSliceRefs);
        Assert.Contains(new ContainerSliceRef("slice:1"), model.ArchivedSliceRefs);
        Assert.DoesNotContain(new ViewportOccurrenceRef("occ:1"), model.ActiveOccurrenceRefs);
        Assert.Contains(new ViewportOccurrenceRef("occ:1"), model.ArchivedOccurrenceRefs);
        Assert.True(model.IsValid);
    }

    [Fact]
    public void ArchivingUnknownActiveReferenceIsRejected()
    {
        var state = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, archiveSlices: [new ContainerSliceRef("slice:1")]));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
    }

    [Fact]
    public void StaleRevisionReturnsExactPriorState()
    {
        var state = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, revision: new SemanticEvidenceRevision(state.EvidenceRevision.Value)));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
        Assert.Contains("stale", preparation.RejectionReason, StringComparison.Ordinal);
    }

    [Fact]
    public void DanglingOccurrenceReferenceIsRejected()
    {
        var state = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, slices: [new ContainerSliceRef("slice:1")],
                occurrences: [new ViewportOccurrenceRef("occ:missing")]));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
    }

    [Fact]
    public void OccurrenceOfSliceOutsideTheModelIsRejected()
    {
        var state = AcceptedState();

        // occ:1 belongs to slice:1 which is NOT being activated here.
        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, occurrences: [new ViewportOccurrenceRef("occ:1")]));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
    }

    [Fact]
    public void AssessmentOfSliceOutsideTheModelIsRejected()
    {
        var state = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, assessments: [new FastAssessmentRef("fast:1")]));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
    }

    [Fact]
    public void UnknownGraphNodeIsRejected()
    {
        var state = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state,
                node: new ContainerNodeRef("node:unknown"),
                slices: [new ContainerSliceRef("slice:1")]));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
    }

    [Fact]
    public void ActivateAndArchiveInTheSameCommitIsRejected()
    {
        var state = AcceptedState();

        var preparation = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state,
                slices: [new ContainerSliceRef("slice:1")],
                archiveSlices: [new ContainerSliceRef("slice:1")]));

        Assert.False(preparation.CanCommit);
        Assert.Same(state, preparation.State);
    }

    [Fact]
    public void SecondAppendReplacesTheSingleModelInsteadOfDuplicating()
    {
        var state = AcceptedState();
        var first = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            state,
            Append(state, slices: [new ContainerSliceRef("slice:1")]));
        Assert.True(first.CanCommit, first.RejectionReason);

        var second = ContainerRuntimeV2Reducer.PrepareLocalModelAppend(
            first.State,
            Append(first.State, occurrences: [new ViewportOccurrenceRef("occ:1")]));

        Assert.True(second.CanCommit, second.RejectionReason);
        var model = Assert.Single(second.State.LocalModels);
        Assert.Contains(new ContainerSliceRef("slice:1"), model.ActiveSliceRefs);
        Assert.Contains(new ViewportOccurrenceRef("occ:1"), model.ActiveOccurrenceRefs);
    }

    [Fact]
    public void CoverageSkeletonIsFailClosedUntilProducersExist()
    {
        var model = new NodeLocalModel(Node);

        // Empty projection sets are NOT exhausted: no completion authority
        // leaks from the 3.1 skeleton (task 4.4/4.5 own the real judgement).
        Assert.False(model.ContainerCoverage.Exhausted);
        Assert.Empty(model.RegionCoverageProjections);
    }
}
