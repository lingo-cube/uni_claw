using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: ContainerSlice previously retained only a slice
// reference and revision. Stage B2 requires accepted visual occurrences and
// Fast structural hints to be immutable evidence records before LocalModel
// canonicalization exists. These records own no mutable state and grant no
// identity, action, progress, graph, coverage, or completion authority.

/// <summary>Opaque reference to the evidence that qualified a stable Slice.</summary>
public readonly record struct StabilityEvidenceRef
{
    /// <summary>Creates a non-empty evidence reference.</summary>
    public StabilityEvidenceRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque value.</summary>
    public override string ToString() => Value;
}

/// <summary>Opaque reference to one accepted Fast structural assessment.</summary>
public readonly record struct FastAssessmentRef
{
    /// <summary>Creates a non-empty assessment reference.</summary>
    public FastAssessmentRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque value.</summary>
    public override string ToString() => Value;
}

/// <summary>Opaque reference to auxiliary structured evidence.</summary>
public readonly record struct StructuredEvidenceRef
{
    /// <summary>Creates a non-empty evidence reference.</summary>
    public StructuredEvidenceRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque value.</summary>
    public override string ToString() => Value;
}

/// <summary>
/// V1 candidate visual primitive taxonomy. The enum values are intentionally
/// not contract-frozen; the contract freezes compositional modeling only.
/// </summary>
public enum VisualPrimitiveKind
{
    /// <summary>Reliable visual occurrence whose primitive is unresolved.</summary>
    Unknown,
    /// <summary>Text-shaped visual primitive.</summary>
    Text,
    /// <summary>Icon-shaped visual primitive.</summary>
    Icon,
    /// <summary>Toggle-shaped visual primitive.</summary>
    Toggle,
    /// <summary>Image-shaped visual primitive.</summary>
    Image,
    /// <summary>Divider or separator visual primitive.</summary>
    Divider,
    /// <summary>Container-like visual grouping primitive.</summary>
    Group,
}

/// <summary>V1 candidate structural hint taxonomy from the Fast path.</summary>
public enum FastStructureHint
{
    /// <summary>No reliable structural hypothesis.</summary>
    Unknown,
    /// <summary>Candidates may form a list item.</summary>
    ListItem,
    /// <summary>Candidates may form a group.</summary>
    Group,
    /// <summary>Candidate may be static content.</summary>
    StaticContent,
}

/// <summary>V1 candidate member-role hint taxonomy from the Fast path.</summary>
public enum FastMemberRoleHint
{
    /// <summary>No reliable role hypothesis.</summary>
    Unknown,
    /// <summary>Primary member hypothesis.</summary>
    Primary,
    /// <summary>Secondary member hypothesis.</summary>
    Secondary,
    /// <summary>Decorative member hypothesis.</summary>
    Decoration,
}

/// <summary>V1 candidate affordance hint taxonomy from the Fast path.</summary>
public enum FastAffordanceHint
{
    /// <summary>No reliable affordance hypothesis.</summary>
    Unknown,
    /// <summary>Non-interactive hypothesis.</summary>
    None,
    /// <summary>Navigation hypothesis.</summary>
    Navigate,
    /// <summary>Toggle hypothesis.</summary>
    Toggle,
    /// <summary>Generic action hypothesis.</summary>
    Invoke,
}

/// <summary>Claim-specific raw state hints corroborated by structured evidence.</summary>
public sealed record OccurrenceStateHints(
    bool? Clickable,
    bool? Checkable,
    bool? Checked,
    bool? Enabled,
    bool? Focusable)
{
    /// <summary>Empty hint set used when no structured correspondence exists.</summary>
    public static OccurrenceStateHints Empty { get; } = new(null, null, null, null, null);
}

/// <summary>
/// Immutable accepted primary viewport visual occurrence. Structured evidence
/// may corroborate this record but can never create one. ScreenBounds is fresh
/// visual geometry; RegionRelativeBounds is correlation-only and never action
/// grounding authority.
/// </summary>
public sealed record Occurrence
{
    /// <summary>Creates accepted visual occurrence evidence.</summary>
    public Occurrence(
        ViewportOccurrenceRef occurrenceRef,
        ContainerSliceRef sliceRef,
        VisualPrimitiveKind primitiveKind,
        ElementBounds screenBounds,
        OccurrenceRegionBinding regionBinding,
        string rawEvidenceRef,
        ElementBounds? regionRelativeBounds = null,
        OccurrenceStateHints? stateHints = null,
        IEnumerable<StructuredEvidenceRef>? corroborationRefs = null,
        string? stabilizerHint = null,
        bool edgeClipped = false)
    {
        // Explicit value check: record-struct refs can arrive as default(T)
        // bypassing their constructors.
        if (string.IsNullOrWhiteSpace(occurrenceRef.Value))
            throw new ArgumentException("Occurrence reference must be non-empty.", nameof(occurrenceRef));
        ArgumentNullException.ThrowIfNull(screenBounds);
        ArgumentNullException.ThrowIfNull(regionBinding);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawEvidenceRef);
        if (!Enum.IsDefined(primitiveKind))
            throw new ArgumentOutOfRangeException(nameof(primitiveKind));
        if (!screenBounds.IsValid)
            throw new ArgumentException("Screen bounds must be valid normalized visual evidence.", nameof(screenBounds));
        if (!regionBinding.IsValid || regionBinding.OccurrenceRef != occurrenceRef)
            throw new ArgumentException("Region binding must be valid and refer to this occurrence.", nameof(regionBinding));
        if (regionRelativeBounds is not null && (!regionRelativeBounds.IsValid || regionBinding.Ambiguous))
            throw new ArgumentException("Region-relative bounds require an unambiguous valid binding.", nameof(regionRelativeBounds));

        OccurrenceRef = occurrenceRef;
        SliceRef = sliceRef;
        PrimitiveKind = primitiveKind;
        ScreenBounds = screenBounds;
        RegionBinding = regionBinding;
        RegionRelativeBounds = regionRelativeBounds;
        RawEvidenceRef = rawEvidenceRef;
        StateHints = stateHints ?? OccurrenceStateHints.Empty;
        CorroborationRefs = corroborationRefs is null
            ? ImmutableArray<StructuredEvidenceRef>.Empty
            : corroborationRefs.Distinct().ToImmutableArray();
        StabilizerHint = string.IsNullOrWhiteSpace(stabilizerHint) ? null : stabilizerHint;
        EdgeClipped = edgeClipped;
    }

    /// <summary>Gets the Run-local occurrence reference.</summary>
    public ViewportOccurrenceRef OccurrenceRef { get; }
    /// <summary>Gets the owning accepted Slice reference.</summary>
    public ContainerSliceRef SliceRef { get; }
    /// <summary>Gets the visual primitive hint.</summary>
    public VisualPrimitiveKind PrimitiveKind { get; }
    /// <summary>Gets fresh visual bounds in the full viewport frame.</summary>
    public ElementBounds ScreenBounds { get; }
    /// <summary>Gets spatial association evidence.</summary>
    public OccurrenceRegionBinding RegionBinding { get; }
    /// <summary>Gets optional correlation-only bounds within the primary region.</summary>
    public ElementBounds? RegionRelativeBounds { get; }
    /// <summary>Gets the primary visual evidence reference.</summary>
    public string RawEvidenceRef { get; }
    /// <summary>Gets structured state hints, if corroborated.</summary>
    public OccurrenceStateHints StateHints { get; }
    /// <summary>Gets auxiliary evidence references deterministically matched to the visual occurrence.</summary>
    public ImmutableArray<StructuredEvidenceRef> CorroborationRefs { get; }
    /// <summary>Gets the optional non-authoritative perception stabilizer hint.</summary>
    public string? StabilizerHint { get; }
    /// <summary>Gets whether visual geometry touches the viewport edge.</summary>
    public bool EdgeClipped { get; }
}

/// <summary>
/// Structured evidence without a deterministic visual correspondence. It is
/// retained as auxiliary evidence and grants no occurrence, grounding,
/// identity, coverage, graph, progress, or completion authority.
/// </summary>
public sealed record UnmatchedStructuredEvidence(
    StructuredEvidenceRef EvidenceRef,
    ContainerSliceRef SliceRef,
    StructuredElementEvidence Evidence);

/// <summary>
/// Immutable lowest-tier Fast structural hypothesis bound to accepted visual
/// occurrences. It is an input hint only, never a LogicalItem or obligation.
/// </summary>
public sealed record FastAssessment
{
    /// <summary>Creates an immutable assessment.</summary>
    public FastAssessment(
        FastAssessmentRef assessmentRef,
        ContainerSliceRef sliceRef,
        IEnumerable<ViewportOccurrenceRef> targetOccurrenceRefs,
        FastStructureHint structureHint,
        FastMemberRoleHint memberRoleHint,
        FastAffordanceHint affordanceHint,
        string source)
    {
        ArgumentNullException.ThrowIfNull(targetOccurrenceRefs);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!Enum.IsDefined(structureHint))
            throw new ArgumentOutOfRangeException(nameof(structureHint));
        if (!Enum.IsDefined(memberRoleHint))
            throw new ArgumentOutOfRangeException(nameof(memberRoleHint));
        if (!Enum.IsDefined(affordanceHint))
            throw new ArgumentOutOfRangeException(nameof(affordanceHint));

        AssessmentRef = assessmentRef;
        SliceRef = sliceRef;
        TargetOccurrenceRefs = targetOccurrenceRefs.Distinct().ToImmutableArray();
        StructureHint = structureHint;
        MemberRoleHint = memberRoleHint;
        AffordanceHint = affordanceHint;
        Source = source;
    }

    /// <summary>Gets the assessment reference.</summary>
    public FastAssessmentRef AssessmentRef { get; }
    /// <summary>Gets the owning Slice reference.</summary>
    public ContainerSliceRef SliceRef { get; }
    /// <summary>Gets the accepted occurrence targets.</summary>
    public ImmutableArray<ViewportOccurrenceRef> TargetOccurrenceRefs { get; }
    /// <summary>Gets the structure hint.</summary>
    public FastStructureHint StructureHint { get; }
    /// <summary>Gets the member-role hint.</summary>
    public FastMemberRoleHint MemberRoleHint { get; }
    /// <summary>Gets the affordance hint.</summary>
    public FastAffordanceHint AffordanceHint { get; }
    /// <summary>Gets the Fast source marker.</summary>
    public string Source { get; }
}

/// <summary>
/// One validated atomic accepted-evidence commit. All collections are copied;
/// the reducer validates every cross-reference before replacing Runtime state.
/// </summary>
public sealed record SliceAcceptanceCommit
{
    /// <summary>Creates the immutable candidate commit.</summary>
    public SliceAcceptanceCommit(
        ContainerSlice slice,
        IEnumerable<SpatialRegion> spatialRegions,
        IEnumerable<Occurrence> occurrences,
        IEnumerable<FastAssessment> fastAssessments,
        IEnumerable<UnmatchedStructuredEvidence>? unmatchedAuxiliaryEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(spatialRegions);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(fastAssessments);
        Slice = slice;
        SpatialRegions = spatialRegions.ToImmutableArray();
        Occurrences = occurrences.ToImmutableArray();
        FastAssessments = fastAssessments.ToImmutableArray();
        UnmatchedAuxiliaryEvidence = unmatchedAuxiliaryEvidence is null
            ? ImmutableArray<UnmatchedStructuredEvidence>.Empty
            : unmatchedAuxiliaryEvidence.ToImmutableArray();
    }

    /// <summary>Gets the accepted Slice.</summary>
    public ContainerSlice Slice { get; }
    /// <summary>Gets the Slice-local regions.</summary>
    public ImmutableArray<SpatialRegion> SpatialRegions { get; }
    /// <summary>Gets accepted visual occurrences.</summary>
    public ImmutableArray<Occurrence> Occurrences { get; }
    /// <summary>Gets bound Fast hint assessments.</summary>
    public ImmutableArray<FastAssessment> FastAssessments { get; }
    /// <summary>Gets retained structured evidence without a visual correspondence.</summary>
    public ImmutableArray<UnmatchedStructuredEvidence> UnmatchedAuxiliaryEvidence { get; }
}
