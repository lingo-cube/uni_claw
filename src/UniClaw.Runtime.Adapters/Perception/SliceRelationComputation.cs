using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Perception;

// NEW_SYMBOL_JUSTIFICATION: Stage B1 buys the adapter-side computation port
// for pairwise SliceRelation evidence (container-runtime-v2-evidence-model,
// spec: evidence-foundation "SliceRelation 为 region-bound 空间证据"). The
// Runtime core keeps zero external dependencies (ArchitectureGuard), so CV
// computation (anchor matching / registration / consensus) lives behind this
// adapter capability port; Runtime only consumes the produced evidence.

/// <summary>Region geometry of one Slice supplied to relation computation.</summary>
/// <param name="RegionRef">Slice-local region reference.</param>
/// <param name="Bounds">Region bounds in the Slice viewport frame (normalized).</param>
public sealed record SliceRegionGeometry(SpatialRegionRef RegionRef, ElementBounds Bounds);

/// <summary>
/// Immutable input for one pairwise SliceRelation computation. Carries visual
/// evidence references (large pixel payloads stay at the artifact location),
/// both Slices' region geometry, and the optional scroll action prior.
/// The prior is evidence only — it never becomes world truth.
/// </summary>
/// <param name="FromVisualEvidenceRef">Visual evidence reference of the From-Slice.</param>
/// <param name="ToVisualEvidenceRef">Visual evidence reference of the To-Slice.</param>
/// <param name="FromRegions">From-Slice region geometry.</param>
/// <param name="ToRegions">To-Slice region geometry.</param>
/// <param name="ScrollActionPrior">Optional gesture prior for the motion that produced the To-Slice.</param>
public sealed record SliceRelationComputationInput(
    string FromVisualEvidenceRef,
    string ToVisualEvidenceRef,
    ImmutableArray<SliceRegionGeometry> FromRegions,
    ImmutableArray<SliceRegionGeometry> ToRegions,
    SpatialTranslation? ScrollActionPrior = null)
{
    /// <summary>Validates input invariants: non-empty evidence refs and region geometry on both sides.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(FromVisualEvidenceRef)
           && !string.IsNullOrWhiteSpace(ToVisualEvidenceRef)
           && !FromRegions.IsDefaultOrEmpty
           && !ToRegions.IsDefaultOrEmpty;
}

/// <summary>
/// Adapter capability port producing pairwise region-bound spatial relation
/// evidence for two accepted Slices. Implementations MUST mark the evidence
/// channels they used (SpatialEvidenceChannel provenance) and quantify
/// translation uncertainty. Produced evidence is correlation / coverage /
/// relocation evidence only: SLICE_ALIGNMENT != ITEM_IDENTITY, no action
/// grounding, no scroll authorization. Not a Runtime semantic port.
/// </summary>
public interface ISliceRelationSource
{
    /// <summary>Computes the pairwise SliceRelation evidence for two accepted Slices.</summary>
    /// <param name="fromSliceRef">Earlier accepted Slice reference.</param>
    /// <param name="toSliceRef">Later accepted Slice reference.</param>
    /// <param name="input">Computation input (visual evidence refs + region geometry + optional prior).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Region-bound SliceRelation evidence with channel provenance and quantified uncertainty.</returns>
    Task<SliceRelation> ComputeAsync(
        ContainerSliceRef fromSliceRef,
        ContainerSliceRef toSliceRef,
        SliceRelationComputationInput input,
        CancellationToken cancellationToken);
}
