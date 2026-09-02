namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: no existing type expresses the spatial association
// between a viewport occurrence candidate and Slice-local SpatialRegions.
// ObjectBinding is observation-local element binding evidence with different
// semantics (superseded at Stage B2); SpatialRegion itself is a partition and
// cannot own the association. Stage B1, container-runtime-v2-evidence-model,
// spec: evidence-foundation (SpatialRegion 与 OccurrenceRegionBinding).

/// <summary>
/// Opaque reference to one accepted viewport occurrence. Introduced at Stage B1
/// as the handle carried by OccurrenceRegionBinding; the accepted visual
/// Occurrence evidence record itself materializes at Stage B2. The reference is
/// never an identity, an authorization token, or a cross-run value.
/// </summary>
public readonly record struct ViewportOccurrenceRef
{
    /// <summary>Creates an occurrence reference from a non-empty Run-local value.</summary>
    public ViewportOccurrenceRef(string value)
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
/// Immutable spatial association between one viewport occurrence and the
/// SpatialRegions of its owning Slice (V1 rule: max-overlap dominance with a
/// tunable threshold). This binding is SPATIAL ASSOCIATION ONLY:
/// REGION_BINDING != OWNERSHIP (occurrences belong to their Slice / LocalModel
/// evidence model) and REGION_BINDING != OCCURRENCE_IDENTITY. When ambiguous,
/// the occurrence's ScreenBounds remain valid evidence while region-relative
/// coordinates carry no authoritative correlation value.
/// </summary>
/// <param name="OccurrenceRef">The bound viewport occurrence reference.</param>
/// <param name="PrimarySpatialRegionRef">Dominant region reference; null when ambiguous or unbound.</param>
/// <param name="OverlapRatio">Overlap fraction (intersection area / occurrence area) of the best candidate region.</param>
/// <param name="Ambiguous">True when no dominant region exists; region-relative coordinates are non-authoritative.</param>
public sealed record OccurrenceRegionBinding(
    ViewportOccurrenceRef OccurrenceRef,
    SpatialRegionRef? PrimarySpatialRegionRef,
    double OverlapRatio,
    bool Ambiguous)
{
    /// <summary>Validates contract invariants: non-empty occurrence ref, ratio within [0,1], ambiguous bindings carry no primary region.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(OccurrenceRef.Value)
           && OverlapRatio >= 0d && OverlapRatio <= 1d
           && (!Ambiguous || PrimarySpatialRegionRef is null);
}

/// <summary>
/// Stateless pure assessor for occurrence-to-region spatial association.
/// Computes overlap of one occurrence's viewport bounds against the owning
/// Slice's regions and applies the V1 dominance rule:
/// 1) overlap ratio = intersection area / occurrence area (0 for disjoint);
/// 2) a region dominates when its ratio reaches the threshold AND is strictly
///    greater than every other region's ratio;
/// 3) otherwise the binding is ambiguous (primary = null, best ratio retained
///    as evidence).
/// Produces derived evidence only; it grants no ownership, identity, grounding
/// or coverage authority.
/// </summary>
public static class SpatialRegionBinding
{
    /// <summary>Default V1 dominance threshold (tunable policy evidence, not a frozen constant).</summary>
    public const float DefaultDominantOverlapThreshold = 0.5f;

    /// <summary>
    /// Assesses the dominant region association for one occurrence's bounds.
    /// </summary>
    /// <param name="occurrenceBounds">Occurrence bounds in the Slice viewport frame (normalized).</param>
    /// <param name="regions">The owning Slice's spatial regions.</param>
    /// <param name="dominantOverlapThreshold">Minimum best-overlap ratio required for dominance.</param>
    /// <returns>Assessment carrying the optional primary region reference, the best overlap ratio, and the ambiguity flag.</returns>
    public static (SpatialRegionRef? PrimarySpatialRegionRef, double OverlapRatio, bool Ambiguous) Assess(
        ElementBounds occurrenceBounds,
 IReadOnlyList<SpatialRegion> regions,
        float dominantOverlapThreshold = DefaultDominantOverlapThreshold)
    {
        ArgumentNullException.ThrowIfNull(occurrenceBounds);
        ArgumentNullException.ThrowIfNull(regions);

        var occurrenceArea = (double)occurrenceBounds.Width * occurrenceBounds.Height;
        if (occurrenceArea <= 0d || regions.Count == 0)
        {
            return (null, 0d, Ambiguous: true);
        }

        SpatialRegionRef? primary = null;
        var bestRatio = 0d;
        var tie = false;
        foreach (var region in regions)
        {
            var ratio = IntersectionArea(occurrenceBounds, region.Bounds) / occurrenceArea;
            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                primary = region.RegionRef;
                tie = false;
            }
            else if (ratio == bestRatio && ratio > 0d && primary is not null)
            {
                tie = true;
            }
        }

        var ambiguous = tie || bestRatio < dominantOverlapThreshold;
        return ambiguous ? (null, bestRatio, Ambiguous: true) : (primary, bestRatio, Ambiguous: false);
    }

    private static double IntersectionArea(ElementBounds occurrence, ElementBounds region)
    {
        var width = Math.Min(occurrence.X2, region.X2) - Math.Max(occurrence.X1, region.X1);
        var height = Math.Min(occurrence.Y2, region.Y2) - Math.Max(occurrence.Y1, region.Y1);
        return width > 0f && height > 0f ? (double)width * height : 0d;
    }
}
