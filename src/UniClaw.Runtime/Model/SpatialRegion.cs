namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: no existing Runtime type expresses viewport-local
// spatial partitioning with independent scroll/coverage/grounding participation
// flags. ContainerSlice is a thin evidence reference and ElementBounds is bare
// geometry; neither can own the spatial-association boundary that
// OccurrenceRegionBinding and SliceRelation consume (Stage B1,
// container-runtime-v2-evidence-model, spec: evidence-foundation).

/// <summary>
/// Opaque Slice-local reference to one SpatialRegion. A reference identifies a
/// region within exactly one owning Slice; it is never a cross-Slice identity,
/// a run-global identity, or an occurrence identity.
/// </summary>
public readonly record struct SpatialRegionRef
{
    /// <summary>Creates a region reference from a non-empty Slice-local value.</summary>
    public SpatialRegionRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque reference value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque reference value.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// Spatial partition kind for one region of an accepted stable Slice.
/// V1 CANDIDATE TAXONOMY — NOT CONTRACT-FROZEN: values may be merged, renamed,
/// removed, or extended as real Settings/IVI buyers are proven. The frozen
/// contract is the compositional separation (region kind × participation
/// flags), not this value set.
/// </summary>
public enum SpatialRegionKind
{
    /// <summary>Primary scrollable content area.</summary>
    ScrollableContent,
    /// <summary>Fixed chrome (status bar, toolbar) — static across scrolls.</summary>
    FixedChrome,
    /// <summary>Overlay/dialog layer above content.</summary>
    Overlay,
    /// <summary>Persistent control bar (e.g. IVI climate bar).</summary>
    PersistentControlBar,
    /// <summary>Independent non-scrolling panel (e.g. IVI nav sidebar).</summary>
    Panel,
    /// <summary>Partition kind not yet classifiable.</summary>
    Unknown,
}

/// <summary>
/// Immutable spatial partition of one accepted stable Slice. A region is a
/// SPATIAL ASSOCIATION BOUNDARY: it defines which viewport area participates
/// in scroll correlation, coverage accumulation, and action grounding. The
/// three participation flags are independent by contract (a fixed-chrome Back
/// control does not scroll or accumulate coverage yet remains groundable; a
/// horizontal pager region accumulates coverage without vertical scroll
/// participation).
/// REGION_BINDING != OWNERSHIP and REGION_BINDING != OCCURRENCE_IDENTITY:
/// occurrences belong to their Slice; a region never owns occurrences and
/// never confers identity. Bounds are spatial evidence only.
/// </summary>
/// <param name="RegionRef">Slice-local opaque region reference.</param>
/// <param name="Kind">Spatial partition kind (V1 candidate taxonomy).</param>
/// <param name="Bounds">Region bounds in the Slice viewport frame (normalized [0,1]×[0,1], same canonical frame as ElementBounds).</param>
/// <param name="ParticipatesInScroll">Whether the region participates in scroll motion correlation / consensus.</param>
/// <param name="ParticipatesInCoverage">Whether the region participates in coverage accumulation before container aggregation.</param>
/// <param name="ParticipatesInGrounding">Whether occurrences in this region may participate in action grounding.</param>
public sealed record SpatialRegion(
    SpatialRegionRef RegionRef,
    SpatialRegionKind Kind,
    ElementBounds Bounds,
    bool ParticipatesInScroll,
    bool ParticipatesInCoverage,
    bool ParticipatesInGrounding)
{
    /// <summary>Creates the immutable region with contract validation.</summary>
    public SpatialRegion(
        string regionRef,
        SpatialRegionKind Kind,
        ElementBounds Bounds,
        bool ParticipatesInScroll,
        bool ParticipatesInCoverage,
        bool ParticipatesInGrounding)
        : this(
            new SpatialRegionRef(regionRef),
            Kind,
            Bounds,
            ParticipatesInScroll,
            ParticipatesInCoverage,
            ParticipatesInGrounding)
    {
    }

    /// <summary>Validates contract invariants: non-empty ref, defined kind, well-formed bounds.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(RegionRef.Value)
           && Enum.IsDefined(Kind)
           && Bounds is not null
           && Bounds.IsValid;
}
