using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: Stage C1 task 3.2 (container-runtime-v2-evidence-model,
// spec: canonical-world "LogicalItem 为 LocalModel-scoped canonical 逻辑对象")
// requires the immutable canonical logical-object record. No existing type owns
// this composition: FastAssessment/FastStructureHint are lowest-tier hints;
// CanonicalObservationOccurrence is source-neutral perception evidence; this
// change's Occurrence is accepted visual evidence. This model owns no producer:
// the SemanticReconciler (task 3.3) creates LogicalItems; no commit seam, second
// owner, identity registry, merge/split/reclassification logic, or cross-Slice
// correlation decision is introduced here.

/// <summary>
/// Opaque reference scoped to the owning NodeLocalModel lifecycle. It carries
/// no cross-node and no cross-run identity semantics.
/// </summary>
public readonly record struct LogicalItemRef
{
    /// <summary>Creates a non-empty item reference.</summary>
    public LogicalItemRef(string value)
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
/// V1 candidate logical structure taxonomy (NOT CONTRACT-FROZEN; the frozen
/// contract is the compositional model only). Unknown is an explicit
/// unresolved state — it MUST NOT be coerced into StaticContent or any other
/// kind by this model or its consumers' defaults.
/// </summary>
public enum LogicalStructure
{
    /// <summary>Structure not yet resolved; SemanticResolved must be false.</summary>
    Unknown,
    /// <summary>List-row style item.</summary>
    ListItem,
    /// <summary>Tile/card style item.</summary>
    Tile,
    /// <summary>Standalone button.</summary>
    Button,
    /// <summary>Embedded control (non-button).</summary>
    Control,
    /// <summary>Text/selection input.</summary>
    Input,
    /// <summary>Range/slider control.</summary>
    Range,
    /// <summary>Tab selector.</summary>
    Tab,
    /// <summary>Static, non-interactive content (section titles, labels).</summary>
    StaticContent,
    /// <summary>Composition group over sibling items (hierarchy DEFERRED).</summary>
    Group,
}

/// <summary>
/// V1 candidate action affordance taxonomy (NOT CONTRACT-FROZEN). Absence of
/// an action affordance is represented by a NULL PrimaryAffordance — there is
/// deliberately no None member, so "no action semantics" can never be confused
/// with "unknown action semantics" (Unknown) or defaulted silently.
/// </summary>
public enum LogicalAffordanceKind
{
    /// <summary>Affordance not yet resolved.</summary>
    Unknown,
    /// <summary>Navigates to another container.</summary>
    Navigate,
    /// <summary>Invokes an action in place.</summary>
    Invoke,
    /// <summary>Toggles a boolean state.</summary>
    Toggle,
    /// <summary>Selects among options.</summary>
    Select,
    /// <summary>Edits content.</summary>
    Edit,
    /// <summary>Adjusts a value/range.</summary>
    Adjust,
    /// <summary>Expands/discloses nested content.</summary>
    Expand,
    /// <summary>Dismisses overlay content.</summary>
    Dismiss,
}

/// <summary>V1 candidate member-role taxonomy for occurrence membership (NOT CONTRACT-FROZEN).</summary>
public enum LogicalMemberRole
{
    /// <summary>Role not yet resolved.</summary>
    Unknown,
    /// <summary>Primary content member.</summary>
    Primary,
    /// <summary>Secondary/supporting content member.</summary>
    Secondary,
    /// <summary>Value display member.</summary>
    Value,
    /// <summary>State indicator member.</summary>
    StateIndicator,
    /// <summary>Leading visual member.</summary>
    LeadingVisual,
    /// <summary>Trailing visual member.</summary>
    TrailingVisual,
    /// <summary>Embedded control member.</summary>
    Control,
    /// <summary>Disclosure affordance member.</summary>
    Disclosure,
}

/// <summary>
/// Immutable canonical logical state PROJECTION of one item (not evidence:
/// the supporting evidence lives in the LocalModel evidence/assessment/
/// reconciliation-decision chain). Bool dimensions are tri-state (null =
/// unknown); value/unit/mode cover IVI range semantics. The projection is not
/// a grounding, action, or completion input by itself.
/// </summary>
public sealed record LogicalItemState(
    bool? Enabled = null,
    bool? Selected = null,
    bool? Checked = null,
    bool? Expanded = null,
    string? Value = null,
    string? Unit = null,
    string? Mode = null)
{
    /// <summary>Empty state used when no state evidence exists.</summary>
    public static LogicalItemState Empty { get; } = new();
}

/// <summary>
/// Immutable evidence-backed membership of one accepted visual occurrence in
/// one LogicalItem. The explicit evidence reference is MANDATORY: membership is
/// never inferred from equal text, equal destination, adjacency, or shared
/// container — those inferences belong to the claim-specific evidence policy
/// (task 3.3), which must still record its decision through this explicit
/// evidence channel.
/// </summary>
/// <param name="OccurrenceRef">The accepted visual occurrence member.</param>
/// <param name="Role">The member role (V1 candidate taxonomy).</param>
/// <param name="EvidenceRef">Explicit evidence reference backing this membership decision.</param>
public sealed record LogicalMembership(
    ViewportOccurrenceRef OccurrenceRef,
    LogicalMemberRole Role,
    string EvidenceRef)
{
    /// <summary>Creates the membership with mandatory explicit evidence.</summary>
    public LogicalMembership(
        string occurrenceRef,
        LogicalMemberRole role,
        string evidenceRef)
        : this(new ViewportOccurrenceRef(occurrenceRef), role, evidenceRef)
    {
    }

    /// <summary>Validates invariants: defined role and non-empty explicit evidence.</summary>
    public bool IsValid
        => Enum.IsDefined(Role) && !string.IsNullOrWhiteSpace(EvidenceRef);
}

/// <summary>
/// Immutable canonical logical object of one Node's LocalModel lifecycle.
/// Compositional semantics: Structure × single optional primary affordance ×
/// membership roles × state. Identity is LocalModel-scoped only — never
/// cross-run, never text-derived, never destination-derived. This record owns
/// NO grounding geometry, no live handle, no mutable collection, no historical
/// grounding result, and no Agent obligation/authorization semantics:
/// SEMANTIC_AFFORDANCE != AGENT_ADMISSION; a resolved item is NOT currently
/// groundable, authorized, obligation-satisfied, coverage-exhausted, or
/// container-complete by virtue of SemanticResolved.
/// </summary>
public sealed record LogicalItem
{
    /// <summary>Creates the immutable canonical logical object (fail-closed).</summary>
    public LogicalItem(
        LogicalItemRef itemRef,
        LogicalStructure structure,
        LogicalAffordanceKind? primaryAffordance,
        LogicalItemState? state = null,
        IEnumerable<LogicalMembership>? memberships = null,
        IEnumerable<ContainerSliceRef>? anchorSliceRefs = null,
        bool semanticResolved = false)
    {
        // Explicit value check: record-struct refs can arrive as default(T)
        // bypassing their constructors, and ThrowIfNull is meaningless on a
        // non-boxed struct.
        if (string.IsNullOrWhiteSpace(itemRef.Value))
            throw new ArgumentException("LogicalItemRef must be non-empty.", nameof(itemRef));
        if (!Enum.IsDefined(structure))
            throw new ArgumentOutOfRangeException(nameof(structure));
        if (primaryAffordance is { } affordance && !Enum.IsDefined(affordance))
            throw new ArgumentOutOfRangeException(nameof(primaryAffordance));

        ItemRef = itemRef;
        // No coercion: Unknown stays Unknown; a missing affordance stays null.
        Structure = structure;
        PrimaryAffordance = primaryAffordance;
        State = state ?? LogicalItemState.Empty;
        var memberList = memberships?.ToImmutableArray() ?? ImmutableArray<LogicalMembership>.Empty;
        if (memberList.Any(member => member is null || !member.IsValid || string.IsNullOrWhiteSpace(member.OccurrenceRef.Value))
            || memberList.Select(member => member!.OccurrenceRef).Distinct().Count() != memberList.Length)
            throw new ArgumentException("memberships must be valid, use non-default occurrence references, and reference distinct occurrences.", nameof(memberships));
        Memberships = memberList;
        var anchors = anchorSliceRefs?.ToImmutableArray() ?? ImmutableArray<ContainerSliceRef>.Empty;
        if (anchors.Distinct().Count() != anchors.Length)
            throw new ArgumentException("anchor slice references must be distinct.", nameof(anchorSliceRefs));
        AnchorSliceRefs = anchors;

        if (semanticResolved)
        {
            if (structure == LogicalStructure.Unknown)
                throw new ArgumentException("an unresolved structure can never carry a resolved semantic claim.", nameof(semanticResolved));
            if (!AffordanceDetermined(primaryAffordance))
                throw new ArgumentException("a resolved claim requires a determined affordance (null = determined none; Unknown = unresolved).", nameof(semanticResolved));
            if (Memberships.IsDefaultOrEmpty)
                throw new ArgumentException("a resolved claim requires at least one evidence-backed membership.", nameof(semanticResolved));
        }

        SemanticResolved = semanticResolved;
    }

    /// <summary>Gets the LocalModel-scoped item reference.</summary>
    public LogicalItemRef ItemRef { get; }
    /// <summary>Gets the logical structure (Unknown = explicitly unresolved).</summary>
    public LogicalStructure Structure { get; }
    /// <summary>
    /// Gets the single optional primary action affordance. Null = no action
    /// semantics established (legitimate for StaticContent/Group); Unknown =
    /// unresolved. At most one primary affordance by construction.
    /// </summary>
    public LogicalAffordanceKind? PrimaryAffordance { get; }
    /// <summary>Gets immutable logical state evidence.</summary>
    public LogicalItemState State { get; }
    /// <summary>Gets immutable evidence-backed occurrence memberships.</summary>
    public ImmutableArray<LogicalMembership> Memberships { get; }
    /// <summary>
    /// Gets immutable anchor slice references retained for relocation. Anchors
    /// are historical supporting slices ONLY: they are not currently-visible,
    /// not currently-groundable, and never an action-bounds source; grounding
    /// always requires a fresh occurrence.
    /// </summary>
    public ImmutableArray<ContainerSliceRef> AnchorSliceRefs { get; }
    /// <summary>
    /// Gets whether the canonical semantic claim has satisfied its evidence
    /// policy. This flag is a SEMANTIC claim state only — it is never
    /// CurrentlyGroundable, Authorized, ObligationSatisfied, CoverageExhausted,
    /// or ContainerComplete.
    /// </summary>
    public bool SemanticResolved { get; }

    /// <summary>
    /// Derived structural view: whether the affordance dimension is determined.
    /// Orthogonal to Structure by contract — determination looks ONLY at the
    /// affordance value itself (null = determined NONE, a definite value =
    /// determined, Unknown = unresolved). Structure→Affordance compatibility
    /// rules (e.g. "StaticContent must have no affordance", "Button implies
    /// Invoke") deliberately do NOT exist in this base model; they belong to
    /// the claim-specific EvidencePolicy (task 3.3), so e.g. a resolved
    /// LIST_ITEM with null affordance (a definite, non-operable list row) is
    /// representable.
    /// </summary>
    public bool IsAffordanceDetermined => AffordanceDetermined(PrimaryAffordance);

    private static bool AffordanceDetermined(LogicalAffordanceKind? affordance)
        => affordance != LogicalAffordanceKind.Unknown;
}

/// <summary>
/// Stateless integrity validator for LogicalItem references against a
/// NodeLocalModel's evidence layers. Pure derived check — it creates no
/// commit path, no second owner, and no reconciliation decision (task 3.3).
/// Anchors are validated across active AND archived layers: archived anchors
/// are retained relocation evidence, never deleted.
/// </summary>
public static class LogicalItemIntegrity
{
    /// <summary>
    /// Validates that every membership occurrence and anchor slice reference
    /// resolves inside the model's evidence layers.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <param name="model">The owning Node's LocalModel.</param>
    /// <returns>True when all references resolve; otherwise false with collected violations.</returns>
    public static bool ReferencesResolve(
        LogicalItem item,
        NodeLocalModel model,
        out IReadOnlyList<string> violations)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(model);
        var found = new List<string>();

        var occurrenceRefs = model.ActiveOccurrenceRefs.Concat(model.ArchivedOccurrenceRefs).ToHashSet();
        foreach (var member in item.Memberships)
        {
            if (!occurrenceRefs.Contains(member.OccurrenceRef))
                found.Add($"membership occurrence '{member.OccurrenceRef}' is dangling");
        }

        var sliceRefs = model.ActiveSliceRefs.Concat(model.ArchivedSliceRefs).ToHashSet();
        foreach (var anchor in item.AnchorSliceRefs)
        {
            if (!sliceRefs.Contains(anchor))
                found.Add($"anchor slice '{anchor}' is dangling");
        }

        violations = found;
        return found.Count == 0;
    }
}
