using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: Stage C1 task 3.1 (container-runtime-v2-evidence-model,
// spec: canonical-world "LocalModel 是唯一 container-local canonical world owner")
// requires the per-Node immutable aggregation seam. No existing type owns
// container-local accumulated evidence: ContainerRuntimeV2State holds flat
// append-only evidence collections; Container (page-local class) is the legacy
// mutable owner scheduled for staged migration (task 2.8, dependency-deferred).
// This aggregate is the NET_NEW_MUTABLE_TRUTH = +1 centralized owner from the
// Repository Mapping: immutable whole-replacement through the existing
// ContainerRuntimeV2Reducer seam, existing SemanticEvidenceRevision, no second
// reducer, no second revision, no mutable cache, no live handle, no dual-write
// owner. Canonical/coverage projections are READ-ONLY SKELETONS at 3.1:
// LogicalItem (3.2), SemanticReconciler (3.3) and the coverage consumers
// (4.4/4.5) do not exist yet and MUST NOT be pre-created here.

/// <summary>
/// Read-only skeleton of the canonical projection slot. Content (LogicalItem
/// references) is filled by task 3.2/3.3; at 3.1 this records only the
/// revision the (empty) projection was computed at. It is a derived snapshot:
/// it grants no identity, obligation, action, progress, or completion
/// authority, and it is recomputed by whole replacement — never mutated.
/// </summary>
/// <param name="Revision">The evidence revision this projection snapshot reflects.</param>
public sealed record CanonicalProjection(SemanticEvidenceRevision Revision);

/// <summary>
/// Read-only skeleton of one region's coverage projection. Evidence refs and
/// exhaustion are supplied by producers that do not exist at 3.1 (Stage E
/// cutover, task 4.4); until then projections carry empty evidence and null
/// exhaustion. COVERAGE IS SPATIAL_REGION_SCOPED BEFORE CONTAINER AGGREGATION;
/// this projection is evidence, never completion truth.
/// </summary>
/// <param name="RegionRef">The Slice-local region this projection describes.</param>
/// <param name="CoverageEvidenceRefs">Evidence references supporting the coverage state.</param>
/// <param name="Exhaustion">Optional exhaustion state; null = not yet assessable at this skeleton stage.</param>
public sealed record RegionCoverageProjection(
    SpatialRegionRef RegionRef,
    IEnumerable<string>? CoverageEvidenceRefs = null,
    bool? Exhaustion = null)
{
    /// <summary>Gets immutable coverage evidence references.</summary>
    public ImmutableArray<string> EvidenceRefs { get; }
        = CoverageEvidenceRefs?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
}

/// <summary>
/// Read-only aggregate over one Node's region coverage projections. The
/// derived exhaustion view folds participating regions only — producers
/// include solely regions where participatesInCoverage = true (task 4.4).
/// Derived view only: never completion authority (ContainerLocalComplete needs
/// three independent conditions, task 4.5).
/// </summary>
public sealed record ContainerCoverageProjection
{
    /// <summary>Creates the aggregate from the Node's region projections.</summary>
    public ContainerCoverageProjection(IEnumerable<RegionCoverageProjection>? regionProjections = null)
    {
        Regions = regionProjections?.ToImmutableArray() ?? ImmutableArray<RegionCoverageProjection>.Empty;
    }

    /// <summary>Gets the aggregated region projections (participating regions only, by producer contract).</summary>
    public ImmutableArray<RegionCoverageProjection> Regions { get; }

    /// <summary>
    /// Derived view: true only when at least one participating region is
    /// projected AND every participating region reports exhaustion. Empty
    /// projection sets are NOT exhausted (fail-closed).
    /// </summary>
    public bool Exhausted => !Regions.IsDefaultOrEmpty
        && Regions.All(region => region.Exhaustion == true);
}

/// <summary>
/// Immutable per-Node LocalModel: the single container-local canonical world
/// owner's aggregation state. Evidence references are layered ACTIVE (correlation
/// search space) and ARCHIVED (superseded evidence retained as relocation
/// anchors; excluded from correlation search, never deleted). Underlying
/// evidence collections in ContainerRuntimeV2State remain append-only; layer
/// membership changes only via whole-replacement commits. This aggregate owns
/// no Agent plan, action authorization, GoalEvidence, current physical
/// authority, cross-run item identity, or historical-bounds click authority.
/// </summary>
public sealed record NodeLocalModel
{
    /// <summary>Creates the immutable per-Node aggregate.</summary>
    public NodeLocalModel(
        ContainerNodeRef nodeRef,
        IEnumerable<ContainerSliceRef>? activeSliceRefs = null,
        IEnumerable<ContainerSliceRef>? archivedSliceRefs = null,
        IEnumerable<ViewportOccurrenceRef>? activeOccurrenceRefs = null,
        IEnumerable<ViewportOccurrenceRef>? archivedOccurrenceRefs = null,
        IEnumerable<FastAssessmentRef>? fastAssessmentRefs = null,
        IEnumerable<TransitionOccurrenceRef>? transitionOccurrenceRefs = null,
        CanonicalProjection? canonicalProjection = null,
        IEnumerable<RegionCoverageProjection>? regionCoverageProjections = null)
    {
        NodeRef = nodeRef;
        ActiveSliceRefs = activeSliceRefs?.ToImmutableArray() ?? ImmutableArray<ContainerSliceRef>.Empty;
        ArchivedSliceRefs = archivedSliceRefs?.ToImmutableArray() ?? ImmutableArray<ContainerSliceRef>.Empty;
        ActiveOccurrenceRefs = activeOccurrenceRefs?.ToImmutableArray() ?? ImmutableArray<ViewportOccurrenceRef>.Empty;
        ArchivedOccurrenceRefs = archivedOccurrenceRefs?.ToImmutableArray() ?? ImmutableArray<ViewportOccurrenceRef>.Empty;
        FastAssessmentRefs = fastAssessmentRefs?.ToImmutableArray() ?? ImmutableArray<FastAssessmentRef>.Empty;
        TransitionOccurrenceRefs = transitionOccurrenceRefs?.ToImmutableArray() ?? ImmutableArray<TransitionOccurrenceRef>.Empty;
        CanonicalProjection = canonicalProjection ?? new CanonicalProjection(new SemanticEvidenceRevision(0));
        RegionCoverageProjections = regionCoverageProjections?.ToImmutableArray() ?? ImmutableArray<RegionCoverageProjection>.Empty;
    }

    /// <summary>Gets the owning Graph node reference.</summary>
    public ContainerNodeRef NodeRef { get; }
    /// <summary>Gets active layer Slice references (correlation search space).</summary>
    public ImmutableArray<ContainerSliceRef> ActiveSliceRefs { get; }
    /// <summary>Gets archived layer Slice references (relocation anchors; excluded from correlation search).</summary>
    public ImmutableArray<ContainerSliceRef> ArchivedSliceRefs { get; }
    /// <summary>Gets active layer occurrence references.</summary>
    public ImmutableArray<ViewportOccurrenceRef> ActiveOccurrenceRefs { get; }
    /// <summary>Gets archived layer occurrence references (relocation anchors).</summary>
    public ImmutableArray<ViewportOccurrenceRef> ArchivedOccurrenceRefs { get; }
    /// <summary>Gets bound Fast hint assessment references (lowest-tier evidence).</summary>
    public ImmutableArray<FastAssessmentRef> FastAssessmentRefs { get; }
    /// <summary>Gets interaction/transition evidence references.</summary>
    public ImmutableArray<TransitionOccurrenceRef> TransitionOccurrenceRefs { get; }
    /// <summary>Gets the read-only canonical projection skeleton (content arrives with 3.2/3.3).</summary>
    public CanonicalProjection CanonicalProjection { get; }
    /// <summary>Gets the read-only region coverage projection skeletons.</summary>
    public ImmutableArray<RegionCoverageProjection> RegionCoverageProjections { get; }

    /// <summary>Derived read-only aggregate over the region coverage skeletons.</summary>
    public ContainerCoverageProjection ContainerCoverage => new(RegionCoverageProjections);

    /// <summary>
    /// Contract invariants: active/archived layers are disjoint per kind and
    /// contain no duplicates (append-only dedupe across both layers).
    /// </summary>
    public bool IsValid
        => Distinct(ActiveSliceRefs) && Distinct(ArchivedSliceRefs)
           && Disjoint(ActiveSliceRefs, ArchivedSliceRefs)
           && Distinct(ActiveOccurrenceRefs) && Distinct(ArchivedOccurrenceRefs)
           && Disjoint(ActiveOccurrenceRefs, ArchivedOccurrenceRefs)
           && Distinct(FastAssessmentRefs)
           && Distinct(TransitionOccurrenceRefs);

    private static bool Distinct<T>(ImmutableArray<T> values)
        => values.Distinct().Count() == values.Length;

    private static bool Disjoint<T>(ImmutableArray<T> left, ImmutableArray<T> right)
        => !left.Any(right.Contains);
}
