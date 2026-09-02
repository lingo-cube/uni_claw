using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: no existing type expresses pairwise spatial
// continuity evidence between accepted Slices. ContainerTransitionOccurrence
// records physical transition evidence, ViewportExplorationEvidence is an
// Observation history, and ContainerSlice is a thin reference; none can own
// region-bound translation/overlap/continuity evidence. Stage B1,
// container-runtime-v2-evidence-model, spec: evidence-foundation
// (SliceRelation 为 region-bound 空间证据).

/// <summary>Immutable viewport-relative translation delta (normalized viewport units; +Y = downward).</summary>
/// <param name="Dx">Horizontal displacement between the two Slices' region frames.</param>
/// <param name="Dy">Vertical displacement between the two Slices' region frames.</param>
public sealed record SpatialTranslation(float Dx, float Dy);

/// <summary>Evidence channel that contributed to one region relation. Channels are provenance marks only, not authority.</summary>
public enum SpatialEvidenceChannel
{
    /// <summary>Common text/icon/structure anchor displacement consensus.</summary>
    OccurrenceAnchorMatching,
    /// <summary>Pixel/feature registration (e.g. phase correlation) over the region.</summary>
    PixelRegistration,
    /// <summary>Robust clustering of motion candidates (median/RANSAC; sticky dy≈0 as separate cluster).</summary>
    RobustConsensus,
    /// <summary>Scroll action prior from the gesture that produced the target Slice.</summary>
    ScrollActionPrior,
}

/// <summary>Derived confidence band for one region relation; consumers gate on the band, never treat estimates as exact coordinates.</summary>
public enum SpatialRelationConfidenceBand
{
    /// <summary>High-confidence estimate usable as strong correlation evidence.</summary>
    High,
    /// <summary>Medium-confidence estimate; downstream weighting applies.</summary>
    Medium,
    /// <summary>Low-confidence estimate; relocation hints only with mandatory re-grounding.</summary>
    Low,
}

/// <summary>Quantified translation uncertainty: numeric error bound (viewport units) plus the derived confidence band.</summary>
/// <param name="ErrorBound">Quantified displacement error bound.</param>
/// <param name="Band">Derived confidence band consumed by policy gates.</param>
public sealed record SpatialRelationUncertainty(float ErrorBound, SpatialRelationConfidenceBand Band);

/// <summary>Spatial continuity interpretation of one region relation.</summary>
public enum RegionContinuity
{
    /// <summary>Effective overlap establishes continuous coverage across the two Slices.</summary>
    Continuous,
    /// <summary>Large displacement with insufficient overlap: an uncovered spatial interval exists.</summary>
    Gap,
    /// <summary>Continuity not assessable from available evidence.</summary>
    Unknown,
}

/// <summary>
/// Immutable region-bound spatial relation between one region of the From-Slice
/// and the corresponding region of the To-Slice. Spatial evidence only:
/// SLICE_ALIGNMENT != ITEM_IDENTITY; estimated positions are never action
/// grounding; this record never authorizes scrolling.
/// </summary>
/// <param name="FromSpatialRegionRef">Region reference within the From-Slice.</param>
/// <param name="ToSpatialRegionRef">Corresponding region reference within the To-Slice.</param>
/// <param name="Translation">Estimated dominant translation of the region content.</param>
/// <param name="Uncertainty">Quantified uncertainty plus derived confidence band.</param>
/// <param name="Overlap">Effective overlap fraction [0,1] of the region across the two Slices.</param>
/// <param name="Continuity">Continuity interpretation (Continuous / Gap / Unknown).</param>
/// <param name="EvidenceChannels">Provenance marks of contributing evidence channels.</param>
public sealed record RegionRelation(
    SpatialRegionRef FromSpatialRegionRef,
    SpatialRegionRef ToSpatialRegionRef,
    SpatialTranslation Translation,
    SpatialRelationUncertainty Uncertainty,
    float Overlap,
    RegionContinuity Continuity,
    ImmutableArray<SpatialEvidenceChannel> EvidenceChannels)
{
    /// <summary>Validates contract invariants: defined refs, overlap within [0,1], defined continuity.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(FromSpatialRegionRef.Value)
           && !string.IsNullOrWhiteSpace(ToSpatialRegionRef.Value)
           && Overlap >= 0f && Overlap <= 1f
           && Enum.IsDefined(Continuity);
}

/// <summary>
/// Immutable pairwise spatial relation evidence between two accepted stable
/// Slices, scoped per SpatialRegion. Region correspondence stays internal to
/// this pairwise evidence (positional matching within From/To slices); it does
/// not introduce cross-Slice region identity. V1 producers emit exactly one
/// Primary region relation; the model itself permits 1..N. Consumed as
/// correlation / coverage / relocation evidence only — never item identity,
/// never action grounding, never scroll authorization, never completion truth.
/// </summary>
/// <param name="FromSliceRef">Earlier accepted Slice reference.</param>
/// <param name="ToSliceRef">Later accepted Slice reference.</param>
/// <param name="RegionRelations">Per-region relations (V1: exactly one Primary relation).</param>
public sealed record SliceRelation(
    ContainerSliceRef FromSliceRef,
    ContainerSliceRef ToSliceRef,
    IEnumerable<RegionRelation>? RegionRelations = null)
{
    /// <summary>Gets immutable per-region relations.</summary>
    public ImmutableArray<RegionRelation> Regions { get; }
        = RegionRelations?.ToImmutableArray() ?? ImmutableArray<RegionRelation>.Empty;

    /// <summary>Validates contract invariants: at least one valid region relation; all region relations valid.</summary>
    public bool IsValid
        => !Regions.IsDefaultOrEmpty
           && Regions.All(relation => relation is not null && relation.IsValid);

    /// <summary>
    /// Derived evidence view: true when any region relation reports an
    /// uncovered spatial interval. Evidence only — coverage decisions remain
    /// with the region-scoped coverage consumer boundary (Stage C2/E).
    /// </summary>
    public bool IndicatesUncoveredGap => Regions.Any(r => r.Continuity == RegionContinuity.Gap);
}
