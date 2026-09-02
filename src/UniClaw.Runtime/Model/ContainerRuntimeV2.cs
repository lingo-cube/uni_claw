using System.Collections.Immutable;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.Model;

// NEW_SYMBOL_JUSTIFICATION: Container.SemanticPageName requires a known
// semantic identity and therefore cannot own an unresolved working node.
// Existing ContainerTransition combines expectation-shaped dispositions with
// execution evidence and cannot own the occurrence-only contract.  The
// downstream RunExecutionGraph is an execution read projection and cannot own
// this Runtime evidence model or its pure replacement seam.

/// <summary>
/// Opaque, Run-local reference.  A reference identifies an evidence record; it
/// is never a semantic identity, a route, or an authorization token.
/// </summary>
public readonly record struct ContainerNodeRef
{
    /// <summary>Creates a node reference from a non-empty Run-local value.</summary>
    public ContainerNodeRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque reference value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque reference value.</summary>
    public override string ToString() => Value;
}

/// <summary>Opaque Run-local reference to one evidence-backed relation.</summary>
public readonly record struct ContainerRelationRef
{
    /// <summary>Creates a relation reference from a non-empty Run-local value.</summary>
    public ContainerRelationRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque reference value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque reference value.</summary>
    public override string ToString() => Value;
}

/// <summary>Opaque Run-local reference to one physical transition occurrence.</summary>
public readonly record struct TransitionOccurrenceRef
{
    /// <summary>Creates an occurrence reference from a non-empty Run-local value.</summary>
    public TransitionOccurrenceRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque reference value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque reference value.</summary>
    public override string ToString() => Value;
}

/// <summary>Opaque Run-local reference to one fresh visible Slice.</summary>
public readonly record struct ContainerSliceRef
{
    /// <summary>Creates a Slice reference from a non-empty Run-local value.</summary>
    public ContainerSliceRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque reference value.</summary>
    public string Value { get; }
    /// <summary>Returns the opaque reference value.</summary>
    public override string ToString() => Value;
}

/// <summary>Monotonic revision of accepted fresh evidence within one Run.</summary>
public readonly record struct SemanticEvidenceRevision : IComparable<SemanticEvidenceRevision>
{
    /// <summary>Creates a non-negative evidence revision.</summary>
    public SemanticEvidenceRevision(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    /// <summary>Gets the numeric revision.</summary>
    public long Value { get; }
    /// <summary>Compares this revision with another revision.</summary>
    public int CompareTo(SemanticEvidenceRevision other) => Value.CompareTo(other.Value);
    /// <summary>Returns the invariant numeric revision text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Lifecycle evidence for a working node; this slice has one non-trusted stage.</summary>
public enum ContainerNodeLifecycleStage
{
    /// <summary>The working node exists without a trusted semantic identity.</summary>
    INITIALIZED,
}

/// <summary>Evidence interpretation of one observed boundary.</summary>
public enum ContainerTransitionBoundary
{
    /// <summary>Evidence supports continuity in the same Container.</summary>
    SAME_CONTAINER,
    /// <summary>Evidence supports an independent Container.</summary>
    NEW_CONTAINER,
    /// <summary>Evidence may describe a transient or intermediate view.</summary>
    TRANSIENT,
    /// <summary>Evidence does not disambiguate the boundary.</summary>
    AMBIGUOUS,
    /// <summary>Evidence places the occurrence outside the expected path.</summary>
    OFF_PATH,
    /// <summary>Evidence crosses an external boundary.</summary>
    EXTERNAL,
    /// <summary>Evidence is not sufficient to classify the boundary.</summary>
    UNRESOLVED,
}

/// <summary>Whether a supplied relation record is eligible as normal relation evidence.</summary>
public enum ContainerRelationEligibility
{
    /// <summary>The occurrence is retained as evidence without a normal relation.</summary>
    NOT_ELIGIBLE,
    /// <summary>The occurrence may support the supplied normal relation.</summary>
    ELIGIBLE,
}

/// <summary>
/// Derived, evidence-only interpretation of one historical Graph relation
/// against an optional fresh occurrence.  This is not relation lifecycle
/// state and does not rewrite the recorded Graph.
/// NEW_SYMBOL_JUSTIFICATION: the immutable Graph snapshot has no stored
/// assessment state; this small enum is required to express a pure derived
/// view without introducing a maturity owner.
/// </summary>
public enum ContainerGraphRelationAssessmentKind
{
    /// <summary>The recorded relation remains supported by its evidence.</summary>
    SUPPORTED,
    /// <summary>Fresh, eligible evidence challenges the recorded destination.</summary>
    CHALLENGED,
    /// <summary>The supplied fresh occurrence is not eligible to alter relations.</summary>
    NOT_ELIGIBLE,
}

/// <summary>
/// Immutable derived assessment for one evidence-backed Graph relation.
/// Assessment values are recomputed from immutable inputs and are never stored
/// as mutable Graph truth.
/// NEW_SYMBOL_JUSTIFICATION: no existing relation value exposes a revision-
/// bound challenge view, and adding one would conflate historical evidence
/// with its derived interpretation.
/// </summary>
public sealed record ContainerGraphRelationAssessment
{
    /// <summary>
    /// Creates a derived relation assessment bound to the supplied evidence
    /// revision.
    /// </summary>
    public ContainerGraphRelationAssessment(
        ContainerRelationRef relationRef,
        ContainerGraphRelationAssessmentKind kind,
        SemanticEvidenceRevision historicalEvidenceRevision,
        SemanticEvidenceRevision assessmentRevision,
        TransitionOccurrenceRef? freshOccurrenceRef = null,
        ContainerNodeRef? observedDestinationNodeRef = null)
    {
        RelationRef = relationRef;
        Kind = kind;
        HistoricalEvidenceRevision = historicalEvidenceRevision;
        AssessmentRevision = assessmentRevision;
        FreshOccurrenceRef = freshOccurrenceRef;
        ObservedDestinationNodeRef = observedDestinationNodeRef;
    }

    /// <summary>Gets the relation whose evidence was assessed.</summary>
    public ContainerRelationRef RelationRef { get; }
    /// <summary>Gets the derived evidence interpretation.</summary>
    public ContainerGraphRelationAssessmentKind Kind { get; }
    /// <summary>Gets the newest revision among the relation's support evidence.</summary>
    public SemanticEvidenceRevision HistoricalEvidenceRevision { get; }
    /// <summary>Gets the revision at which this derived view was evaluated.</summary>
    public SemanticEvidenceRevision AssessmentRevision { get; }
    /// <summary>Gets the optional fresh occurrence used for this assessment.</summary>
    public TransitionOccurrenceRef? FreshOccurrenceRef { get; }
    /// <summary>Gets the optional fresh destination observed by the candidate.</summary>
    public ContainerNodeRef? ObservedDestinationNodeRef { get; }
}

/// <summary>
/// Immutable working Container evidence.  Semantic identity is optional so an
/// independent first frame can be represented without fabricating identity.
/// </summary>
public sealed record ContainerGraphNode
{
    /// <summary>Creates immutable working-node evidence and copies supplied collections.</summary>
    public ContainerGraphNode(
        ContainerNodeRef nodeRef,
        string? semanticIdentityCandidate = null,
        ContainerNodeLifecycleStage lifecycleStage = ContainerNodeLifecycleStage.INITIALIZED,
        IEnumerable<ContainerSliceRef>? sliceRefs = null,
        IEnumerable<string>? evidenceRefs = null)
    {
        NodeRef = nodeRef;
        SemanticIdentityCandidate = string.IsNullOrWhiteSpace(semanticIdentityCandidate)
            ? null
            : semanticIdentityCandidate;
        LifecycleStage = lifecycleStage;
        SliceRefs = Copy(sliceRefs);
        EvidenceRefs = Copy(evidenceRefs);
    }

    /// <summary>Gets the opaque node reference.</summary>
    public ContainerNodeRef NodeRef { get; }
    /// <summary>Gets the optional semantic identity candidate.</summary>
    public string? SemanticIdentityCandidate { get; }
    /// <summary>Gets the node lifecycle evidence stage.</summary>
    public ContainerNodeLifecycleStage LifecycleStage { get; }
    /// <summary>Gets immutable Slice references associated with this node.</summary>
    public ImmutableArray<ContainerSliceRef> SliceRefs { get; }
    /// <summary>Gets immutable evidence references associated with this node.</summary>
    public ImmutableArray<string> EvidenceRefs { get; }

    private static ImmutableArray<T> Copy<T>(IEnumerable<T>? values)
        => values is null ? ImmutableArray<T>.Empty : values.ToImmutableArray();
}

/// <summary>Immutable fresh visible window; geometry and labels remain evidence only.</summary>
public sealed record ContainerSlice
{
    /// <summary>Creates an immutable fresh visible Slice record.</summary>
    public ContainerSlice(
        ContainerSliceRef sliceRef,
        SemanticEvidenceRevision evidenceRevision,
        IEnumerable<string>? evidenceRefs = null,
        string? observationRef = null,
        ElementBounds? viewportBounds = null,
        IEnumerable<SpatialRegionRef>? spatialRegionRefs = null,
        IEnumerable<ViewportOccurrenceRef>? occurrenceRefs = null,
        IEnumerable<FastAssessmentRef>? fastAssessmentRefs = null,
        StabilityEvidenceRef? stabilityEvidenceRef = null)
    {
        SliceRef = sliceRef;
        EvidenceRevision = evidenceRevision;
        EvidenceRefs = evidenceRefs is null
            ? ImmutableArray<string>.Empty
            : evidenceRefs.ToImmutableArray();
        ObservationRef = string.IsNullOrWhiteSpace(observationRef) ? null : observationRef;
        ViewportBounds = viewportBounds;
        SpatialRegionRefs = Copy(spatialRegionRefs);
        OccurrenceRefs = Copy(occurrenceRefs);
        FastAssessmentRefs = Copy(fastAssessmentRefs);
        StabilityEvidenceRef = stabilityEvidenceRef;
    }

    /// <summary>Gets the opaque Slice reference.</summary>
    public ContainerSliceRef SliceRef { get; }
    /// <summary>Gets the accepted evidence revision for this Slice.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets immutable evidence references for this Slice.</summary>
    public ImmutableArray<string> EvidenceRefs { get; }
    /// <summary>Gets the exact fresh Observation reference accepted as this Slice.</summary>
    public string? ObservationRef { get; }
    /// <summary>Gets viewport geometry for this accepted Slice.</summary>
    public ElementBounds? ViewportBounds { get; }
    /// <summary>Gets the Slice-local spatial region references.</summary>
    public ImmutableArray<SpatialRegionRef> SpatialRegionRefs { get; }
    /// <summary>Gets accepted visual occurrence references.</summary>
    public ImmutableArray<ViewportOccurrenceRef> OccurrenceRefs { get; }
    /// <summary>Gets Fast assessment references atomically bound to this Slice.</summary>
    public ImmutableArray<FastAssessmentRef> FastAssessmentRefs { get; }
    /// <summary>Gets the evidence that qualified this viewport as stable and fresh.</summary>
    public StabilityEvidenceRef? StabilityEvidenceRef { get; }
    /// <summary>
    /// Gets whether all Stage B2 acceptance fields are present. Legacy R8
    /// slices may remain thin until they are produced through RuntimeAcceptance.
    /// </summary>
    public bool IsAcceptedEvidenceComplete
        => ObservationRef is not null
            && ViewportBounds is { IsValid: true }
            && !SpatialRegionRefs.IsDefaultOrEmpty
            && StabilityEvidenceRef is not null;

    private static ImmutableArray<T> Copy<T>(IEnumerable<T>? values)
        => values is null ? ImmutableArray<T>.Empty : values.ToImmutableArray();
}

/// <summary>
/// Entry-relative context.  Source is the actual entry evidence, not a parent
/// and not a topology edge that can be reversed to authorize return.
/// </summary>
public sealed record ContainerEntryContext
{
    /// <summary>Creates entry-relative source and occurrence evidence.</summary>
    public ContainerEntryContext(
        ContainerNodeRef sourceNodeRef,
        TransitionOccurrenceRef entryTransitionOccurrenceRef,
        ContainerRelationRef? entryRelationRef = null)
    {
        SourceNodeRef = sourceNodeRef;
        EntryTransitionOccurrenceRef = entryTransitionOccurrenceRef;
        EntryRelationRef = entryRelationRef;
    }

    /// <summary>Gets the actual source node for this entry.</summary>
    public ContainerNodeRef SourceNodeRef { get; }
    /// <summary>Gets the occurrence that established this entry.</summary>
    public TransitionOccurrenceRef EntryTransitionOccurrenceRef { get; }
    /// <summary>Gets optional relation evidence for this entry.</summary>
    public ContainerRelationRef? EntryRelationRef { get; }
}

/// <summary>
/// Thin current physical working-location projection.  It intentionally has
/// no LocalModel, identity truth, history, obligation, planning, recovery or
/// completion state.
/// </summary>
public sealed record CurrentContainer
{
    /// <summary>Creates the thin current physical working-location projection.</summary>
    public CurrentContainer(
        ContainerNodeRef nodeRef,
        ContainerSliceRef currentSliceRef,
        ContainerEntryContext? entryContext = null)
    {
        NodeRef = nodeRef;
        CurrentSliceRef = currentSliceRef;
        EntryContext = entryContext;
    }

    /// <summary>Gets the current working node reference.</summary>
    public ContainerNodeRef NodeRef { get; }
    /// <summary>Gets the fresh current Slice reference.</summary>
    public ContainerSliceRef CurrentSliceRef { get; }
    /// <summary>Gets optional path-relative entry context.</summary>
    public ContainerEntryContext? EntryContext { get; }
}

/// <summary>
/// Immutable evidence of what physically occurred after fresh observation.
/// It is distinct from action expectation, Graph relation and identity trust.
/// </summary>
public sealed record ContainerTransitionOccurrence
{
    /// <summary>Creates immutable evidence for one physical transition occurrence.</summary>
    public ContainerTransitionOccurrence(
        TransitionOccurrenceRef occurrenceRef,
        string freshObservationRef,
        SemanticEvidenceRevision evidenceRevision,
        ContainerTransitionBoundary boundary,
        bool isCompleted,
        ContainerNodeRef? sourceNodeRef = null,
        string? triggerOccurrenceRef = null,
        ContainerNodeRef? destinationNodeRef = null,
        IEnumerable<string>? evidenceRefs = null,
        string? entryAffordanceEvidenceRef = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(freshObservationRef);
        OccurrenceRef = occurrenceRef;
        FreshObservationRef = freshObservationRef;
        EvidenceRevision = evidenceRevision;
        Boundary = boundary;
        IsCompleted = isCompleted;
        SourceNodeRef = sourceNodeRef;
        TriggerOccurrenceRef = string.IsNullOrWhiteSpace(triggerOccurrenceRef)
            ? null
            : triggerOccurrenceRef;
        DestinationNodeRef = destinationNodeRef;
        EntryAffordanceEvidenceRef = string.IsNullOrWhiteSpace(entryAffordanceEvidenceRef)
            ? null
            : entryAffordanceEvidenceRef;
        EvidenceRefs = evidenceRefs is null
            ? ImmutableArray<string>.Empty
            : evidenceRefs.ToImmutableArray();
    }

    /// <summary>Gets the opaque occurrence reference.</summary>
    public TransitionOccurrenceRef OccurrenceRef { get; }
    /// <summary>Gets the optional observed source node.</summary>
    public ContainerNodeRef? SourceNodeRef { get; }
    /// <summary>Gets the optional per-transition trigger occurrence evidence.</summary>
    public string? TriggerOccurrenceRef { get; }
    /// <summary>Gets the optional observed destination node.</summary>
    public ContainerNodeRef? DestinationNodeRef { get; }
    /// <summary>
    /// Opaque evidence reference for the entry affordance. It is independent
    /// from each occurrence's trigger reference and is not display text.
    /// NEW_SYMBOL_JUSTIFICATION: the existing trigger occurrence is a
    /// per-transition record, while relation support needs an independently
    /// comparable affordance-evidence handle.
    /// </summary>
    public string? EntryAffordanceEvidenceRef { get; }
    /// <summary>Gets the opaque fresh observation reference.</summary>
    public string FreshObservationRef { get; }
    /// <summary>Gets the accepted evidence revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the evidence boundary interpretation.</summary>
    public ContainerTransitionBoundary Boundary { get; }
    /// <summary>Gets whether the physical occurrence was sufficiently observed.</summary>
    public bool IsCompleted { get; }
    /// <summary>Gets immutable supporting evidence references.</summary>
    public ImmutableArray<string> EvidenceRefs { get; }
}

/// <summary>Evidence-backed relation; RelationRef is independent of trigger display text.</summary>
public sealed record ContainerGraphRelation
{
    /// <summary>Creates immutable evidence for one trigger-bearing relation.</summary>
    public ContainerGraphRelation(
        ContainerRelationRef relationRef,
        ContainerNodeRef sourceNodeRef,
        ContainerNodeRef destinationNodeRef,
        string entryAffordanceEvidenceRef,
        IEnumerable<TransitionOccurrenceRef>? supportingOccurrences = null,
        IEnumerable<string>? evidenceRefs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAffordanceEvidenceRef);
        RelationRef = relationRef;
        SourceNodeRef = sourceNodeRef;
        DestinationNodeRef = destinationNodeRef;
        EntryAffordanceEvidenceRef = entryAffordanceEvidenceRef;
        SupportingOccurrences = Copy(supportingOccurrences);
        EvidenceRefs = Copy(evidenceRefs);
    }

    /// <summary>Gets the opaque relation reference.</summary>
    public ContainerRelationRef RelationRef { get; }
    /// <summary>Gets the relation source node.</summary>
    public ContainerNodeRef SourceNodeRef { get; }
    /// <summary>Opaque entry-affordance evidence; it is not a trigger occurrence or display text.</summary>
    public string EntryAffordanceEvidenceRef { get; }
    /// <summary>Gets the relation destination node.</summary>
    public ContainerNodeRef DestinationNodeRef { get; }
    /// <summary>Gets immutable occurrence references supporting this relation.</summary>
    public ImmutableArray<TransitionOccurrenceRef> SupportingOccurrences { get; }
    /// <summary>Gets immutable evidence references supporting this relation.</summary>
    public ImmutableArray<string> EvidenceRefs { get; }

    internal ContainerGraphRelation Support(ContainerTransitionOccurrence occurrence)
    {
        var occurrenceRefs = SupportingOccurrences.Contains(occurrence.OccurrenceRef)
            ? SupportingOccurrences
            : SupportingOccurrences.Add(occurrence.OccurrenceRef);
        var evidence = occurrence.EvidenceRefs.IsDefaultOrEmpty
            ? EvidenceRefs
            : EvidenceRefs.AddRange(occurrence.EvidenceRefs);
        return new ContainerGraphRelation(
            RelationRef,
            SourceNodeRef,
            DestinationNodeRef,
            EntryAffordanceEvidenceRef,
            occurrenceRefs,
            evidence.Distinct());
    }

    private static ImmutableArray<T> Copy<T>(IEnumerable<T>? values)
        => values is null ? ImmutableArray<T>.Empty : values.ToImmutableArray();
}

/// <summary>Immutable evidence-only Graph snapshot; it has no current slot or route API.</summary>
public sealed record ContainerGraphSnapshot
{
    /// <summary>Creates an immutable evidence-only Graph snapshot.</summary>
    public ContainerGraphSnapshot(
        IEnumerable<ContainerGraphNode>? nodes = null,
        IEnumerable<ContainerGraphRelation>? relations = null,
        IEnumerable<TransitionOccurrenceRef>? occurrenceRefs = null)
    {
        Nodes = Copy(nodes);
        Relations = Copy(relations);
        OccurrenceRefs = Copy(occurrenceRefs);
    }

    /// <summary>Gets immutable Graph nodes.</summary>
    public ImmutableArray<ContainerGraphNode> Nodes { get; }
    /// <summary>Gets immutable evidence-backed relations.</summary>
    public ImmutableArray<ContainerGraphRelation> Relations { get; }
    /// <summary>Gets immutable occurrence references recorded by the Graph.</summary>
    public ImmutableArray<TransitionOccurrenceRef> OccurrenceRefs { get; }

    private static ImmutableArray<T> Copy<T>(IEnumerable<T>? values)
        => values is null ? ImmutableArray<T>.Empty : values.ToImmutableArray();
}

/// <summary>Aggregate immutable state for the first V2 model slice.</summary>
public sealed record ContainerRuntimeV2State
{
    /// <summary>Creates an immutable aggregate snapshot from supplied evidence.</summary>
    public ContainerRuntimeV2State(
        ContainerGraphSnapshot? graph = null,
        CurrentContainer? currentContainer = null,
        IEnumerable<ContainerTransitionOccurrence>? transitionOccurrences = null,
        SemanticEvidenceRevision? evidenceRevision = null,
        IEnumerable<ContainerSlice>? slices = null,
        IEnumerable<SpatialRegion>? spatialRegions = null,
        IEnumerable<Occurrence>? occurrences = null,
        IEnumerable<FastAssessment>? fastAssessments = null,
        IEnumerable<UnmatchedStructuredEvidence>? unmatchedAuxiliaryEvidence = null,
        IEnumerable<NodeLocalModel>? localModels = null)
    {
        Graph = graph ?? new ContainerGraphSnapshot();
        CurrentContainer = currentContainer;
        TransitionOccurrences = transitionOccurrences is null
            ? ImmutableArray<ContainerTransitionOccurrence>.Empty
            : transitionOccurrences.ToImmutableArray();
        EvidenceRevision = evidenceRevision ?? new SemanticEvidenceRevision(0);
        Slices = Copy(slices);
        SpatialRegions = Copy(spatialRegions);
        Occurrences = Copy(occurrences);
        FastAssessments = Copy(fastAssessments);
        UnmatchedAuxiliaryEvidence = Copy(unmatchedAuxiliaryEvidence);
        LocalModels = Copy(localModels);
    }

    /// <summary>Gets an empty initial aggregate snapshot.</summary>
    public static ContainerRuntimeV2State Empty { get; } = new();
    /// <summary>Gets the immutable evidence-only Graph snapshot.</summary>
    public ContainerGraphSnapshot Graph { get; }
    /// <summary>Gets the optional current physical working-location projection.</summary>
    public CurrentContainer? CurrentContainer { get; }
    /// <summary>Gets immutable transition occurrence evidence.</summary>
    public ImmutableArray<ContainerTransitionOccurrence> TransitionOccurrences { get; }
    /// <summary>Gets the latest accepted evidence revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets accepted stable Slice evidence.</summary>
    public ImmutableArray<ContainerSlice> Slices { get; }
    /// <summary>Gets accepted Slice-local region evidence.</summary>
    public ImmutableArray<SpatialRegion> SpatialRegions { get; }
    /// <summary>Gets accepted primary viewport visual occurrences.</summary>
    public ImmutableArray<Occurrence> Occurrences { get; }
    /// <summary>Gets immutable Fast structural hints bound during acceptance.</summary>
    public ImmutableArray<FastAssessment> FastAssessments { get; }
    /// <summary>Gets structured evidence retained without visual correspondence.</summary>
    public ImmutableArray<UnmatchedStructuredEvidence> UnmatchedAuxiliaryEvidence { get; }
    /// <summary>
    /// Gets the per-Node immutable LocalModel aggregates — the single
    /// container-local canonical world owner seam (NET_NEW_MUTABLE_TRUTH = +1,
    /// centralized; whole-replacement only). One model per Graph node.
    /// </summary>
    public ImmutableArray<NodeLocalModel> LocalModels { get; }

    private static ImmutableArray<T> Copy<T>(IEnumerable<T>? values)
        => values is null ? ImmutableArray<T>.Empty : values.ToImmutableArray();
}

/// <summary>
/// Explicit candidate for one synchronous replacement.  The caller supplies
/// already-correlated evidence; this type performs no observation or action.
/// </summary>
public sealed record ContainerRuntimeV2ReductionInput
{
    /// <summary>Creates one already-correlated candidate state replacement.</summary>
    public ContainerRuntimeV2ReductionInput(
        ContainerTransitionOccurrence occurrence,
        IEnumerable<ContainerGraphNode>? nodesToAdd = null,
        CurrentContainer? currentContainer = null,
        ContainerGraphRelation? relation = null,
        ContainerRelationEligibility relationEligibility = ContainerRelationEligibility.NOT_ELIGIBLE)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        Occurrence = occurrence;
        NodesToAdd = nodesToAdd is null ? ImmutableArray<ContainerGraphNode>.Empty : nodesToAdd.ToImmutableArray();
        CurrentContainer = currentContainer;
        Relation = relation;
        RelationEligibility = relationEligibility;
    }

    /// <summary>Gets the transition occurrence to append.</summary>
    public ContainerTransitionOccurrence Occurrence { get; }
    /// <summary>Gets immutable working nodes proposed for addition.</summary>
    public ImmutableArray<ContainerGraphNode> NodesToAdd { get; }
    /// <summary>Gets the optional current-location replacement.</summary>
    public CurrentContainer? CurrentContainer { get; }
    /// <summary>Gets the optional relation evidence to support.</summary>
    public ContainerGraphRelation? Relation { get; }
    /// <summary>Gets the non-authoritative relation eligibility assessment.</summary>
    public ContainerRelationEligibility RelationEligibility { get; }
}

/// <summary>
/// Explicit candidate for one LocalModel append/archival whole-replacement.
/// The caller supplies already-accepted evidence references; the reducer
/// validates every reference against state before building the next state.
/// Archival moves a reference from the active layer to the archived layer
/// (relocation anchors retained, never deleted).
/// </summary>
public sealed record LocalModelAppendInput
{
    /// <summary>Creates one append/archival candidate for a Node's LocalModel.</summary>
    public LocalModelAppendInput(
        ContainerNodeRef nodeRef,
        SemanticEvidenceRevision evidenceRevision,
        IEnumerable<ContainerSliceRef>? slicesToActivate = null,
        IEnumerable<ViewportOccurrenceRef>? occurrencesToActivate = null,
        IEnumerable<ContainerSliceRef>? sliceRefsToArchive = null,
        IEnumerable<ViewportOccurrenceRef>? occurrenceRefsToArchive = null,
        IEnumerable<FastAssessmentRef>? fastAssessmentRefs = null,
        IEnumerable<TransitionOccurrenceRef>? transitionOccurrenceRefs = null)
    {
        NodeRef = nodeRef;
        EvidenceRevision = evidenceRevision;
        SlicesToActivate = slicesToActivate?.ToImmutableArray() ?? ImmutableArray<ContainerSliceRef>.Empty;
        OccurrencesToActivate = occurrencesToActivate?.ToImmutableArray() ?? ImmutableArray<ViewportOccurrenceRef>.Empty;
        SliceRefsToArchive = sliceRefsToArchive?.ToImmutableArray() ?? ImmutableArray<ContainerSliceRef>.Empty;
        OccurrenceRefsToArchive = occurrenceRefsToArchive?.ToImmutableArray() ?? ImmutableArray<ViewportOccurrenceRef>.Empty;
        FastAssessmentRefs = fastAssessmentRefs?.ToImmutableArray() ?? ImmutableArray<FastAssessmentRef>.Empty;
        TransitionOccurrenceRefs = transitionOccurrenceRefs?.ToImmutableArray() ?? ImmutableArray<TransitionOccurrenceRef>.Empty;
    }

    /// <summary>Gets the owning Graph node reference.</summary>
    public ContainerNodeRef NodeRef { get; }
    /// <summary>Gets the strictly-advancing candidate revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets accepted Slice references to add to the active layer.</summary>
    public ImmutableArray<ContainerSliceRef> SlicesToActivate { get; }
    /// <summary>Gets accepted occurrence references to add to the active layer.</summary>
    public ImmutableArray<ViewportOccurrenceRef> OccurrencesToActivate { get; }
    /// <summary>Gets active-layer Slice references to move into the archived layer.</summary>
    public ImmutableArray<ContainerSliceRef> SliceRefsToArchive { get; }
    /// <summary>Gets active-layer occurrence references to move into the archived layer.</summary>
    public ImmutableArray<ViewportOccurrenceRef> OccurrenceRefsToArchive { get; }
    /// <summary>Gets accepted Fast assessment references to bind.</summary>
    public ImmutableArray<FastAssessmentRef> FastAssessmentRefs { get; }
    /// <summary>Gets accepted transition occurrence references to bind.</summary>
    public ImmutableArray<TransitionOccurrenceRef> TransitionOccurrenceRefs { get; }
}

/// <summary>Accepted immutable replacement or explicit no-commit rejection.</summary>
public sealed record ContainerRuntimeV2Preparation
{
    /// <summary>Creates an immutable acceptance or rejection result.</summary>
    private ContainerRuntimeV2Preparation(
        bool canCommit,
        ContainerRuntimeV2State state,
        string? rejectionReason)
    {
        CanCommit = canCommit;
        State = state;
        RejectionReason = rejectionReason;
    }

    /// <summary>Gets whether the candidate can be committed.</summary>
    public bool CanCommit { get; }
    /// <summary>Gets the accepted next state or the unchanged prior state.</summary>
    public ContainerRuntimeV2State State { get; }
    /// <summary>Gets the explicit rejection reason, when rejected.</summary>
    public string? RejectionReason { get; }

    internal static ContainerRuntimeV2Preparation Accepted(ContainerRuntimeV2State state)
        => new(true, state, null);

    internal static ContainerRuntimeV2Preparation Rejected(ContainerRuntimeV2State previous, string reason)
        => new(false, previous, reason);
}

/// <summary>
/// Pure queries over the immutable Graph and occurrence snapshots.  The query
/// returns a derived view only; it does not record evidence or alter current
/// location.  NEW_SYMBOL_JUSTIFICATION: the existing snapshot and reducer own
/// immutable Graph evidence and replacement, while no existing read service
/// owns a non-authoritative, revision-bound assessment projection.  A static
/// query reuses those contracts without creating a reader or recorder service.
/// </summary>
public static class ContainerGraphQuery
{
    /// <summary>
    /// Projects every recorded relation into a revision-bound assessment.
    /// </summary>
    /// <param name="state">Immutable Runtime state to inspect.</param>
    /// <param name="freshOccurrence">Optional fresh occurrence to compare.</param>
    /// <param name="relationEligibility">
    /// Explicit assessment input controlling whether the fresh occurrence may
    /// challenge normal relation evidence.
    /// </param>
    /// <returns>Immutable derived assessments in Graph relation order.</returns>
    public static ImmutableArray<ContainerGraphRelationAssessment> ProjectRelationAssessments(
        ContainerRuntimeV2State state,
        ContainerTransitionOccurrence? freshOccurrence = null,
        ContainerRelationEligibility relationEligibility = ContainerRelationEligibility.NOT_ELIGIBLE)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(relationEligibility))
            throw new ArgumentOutOfRangeException(nameof(relationEligibility));

        var assessments = ImmutableArray.CreateBuilder<ContainerGraphRelationAssessment>(
            state.Graph.Relations.Length);
        foreach (var relation in state.Graph.Relations)
        {
            assessments.Add(AssessRelation(state, relation.RelationRef, freshOccurrence, relationEligibility));
        }

        return assessments.MoveToImmutable();
    }

    /// <summary>
    /// Derives one relation assessment from historical support and optional
    /// fresh evidence without mutating either input snapshot.
    /// </summary>
    /// <param name="state">Immutable Runtime state containing relation evidence.</param>
    /// <param name="relationRef">Reference to a relation already recorded in the Graph.</param>
    /// <param name="freshOccurrence">Optional fresh occurrence to compare.</param>
    /// <param name="relationEligibility">
    /// Explicit assessment input for the fresh occurrence.
    /// </param>
    /// <returns>A derived, immutable relation assessment.</returns>
    public static ContainerGraphRelationAssessment AssessRelation(
        ContainerRuntimeV2State state,
        ContainerRelationRef relationRef,
        ContainerTransitionOccurrence? freshOccurrence = null,
        ContainerRelationEligibility relationEligibility = ContainerRelationEligibility.NOT_ELIGIBLE)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(relationEligibility))
            throw new ArgumentOutOfRangeException(nameof(relationEligibility));

        var relation = FindRecordedRelation(state, relationRef);

        var historicalRevision = FindHistoricalSupportRevision(state, relation);
        var assessmentRevision = freshOccurrence?.EvidenceRevision ?? state.EvidenceRevision;
        if (freshOccurrence is not null && !IsAcceptedOccurrence(state, freshOccurrence))
        {
            throw new ArgumentException(
                "fresh occurrence is not an exact match for accepted state evidence",
                nameof(freshOccurrence));
        }

        var kind = freshOccurrence is null
            ? ContainerGraphRelationAssessmentKind.SUPPORTED
            : relationEligibility != ContainerRelationEligibility.ELIGIBLE
                ? ContainerGraphRelationAssessmentKind.NOT_ELIGIBLE
                : IsFreshConflict(relation, historicalRevision, freshOccurrence)
                    ? ContainerGraphRelationAssessmentKind.CHALLENGED
                    : ContainerGraphRelationAssessmentKind.SUPPORTED;

        return new ContainerGraphRelationAssessment(
            relation.RelationRef,
            kind,
            historicalRevision,
            assessmentRevision,
            freshOccurrence?.OccurrenceRef,
            freshOccurrence?.DestinationNodeRef);
    }

    private static ContainerGraphRelation FindRecordedRelation(
        ContainerRuntimeV2State state,
        ContainerRelationRef relationRef)
    {
        ContainerGraphRelation? recorded = null;
        foreach (var relation in state.Graph.Relations)
        {
            if (relation.RelationRef != relationRef)
                continue;
            if (recorded is not null)
            {
                throw new ArgumentException(
                    "relation reference has conflicting recorded Graph entries",
                    nameof(relationRef));
            }

            recorded = relation;
        }

        return recorded
            ?? throw new ArgumentException(
                "relation reference is not recorded in the Graph",
                nameof(relationRef));
    }

    private static bool IsAcceptedOccurrence(
        ContainerRuntimeV2State state,
        ContainerTransitionOccurrence candidate)
    {
        foreach (var accepted in state.TransitionOccurrences)
        {
            if (accepted.OccurrenceRef != candidate.OccurrenceRef)
                continue;

            return accepted.FreshObservationRef == candidate.FreshObservationRef
                && accepted.EvidenceRevision == candidate.EvidenceRevision
                && accepted.Boundary == candidate.Boundary
                && accepted.IsCompleted == candidate.IsCompleted
                && accepted.SourceNodeRef == candidate.SourceNodeRef
                && accepted.TriggerOccurrenceRef == candidate.TriggerOccurrenceRef
                && accepted.DestinationNodeRef == candidate.DestinationNodeRef
                && accepted.EntryAffordanceEvidenceRef == candidate.EntryAffordanceEvidenceRef
                && accepted.EvidenceRefs.SequenceEqual(candidate.EvidenceRefs);
        }

        return false;
    }

    private static SemanticEvidenceRevision FindHistoricalSupportRevision(
        ContainerRuntimeV2State state,
        ContainerGraphRelation relation)
    {
        var newest = new SemanticEvidenceRevision(0);
        foreach (var occurrence in state.TransitionOccurrences)
        {
            if (!relation.SupportingOccurrences.Contains(occurrence.OccurrenceRef)
                || occurrence.EvidenceRevision.Value <= newest.Value)
            {
                continue;
            }

            newest = occurrence.EvidenceRevision;
        }

        return newest;
    }

    private static bool IsFreshConflict(
        ContainerGraphRelation relation,
        SemanticEvidenceRevision historicalRevision,
        ContainerTransitionOccurrence freshOccurrence)
        => freshOccurrence.IsCompleted
            && freshOccurrence.Boundary == ContainerTransitionBoundary.NEW_CONTAINER
            && freshOccurrence.SourceNodeRef is { } source
            && source == relation.SourceNodeRef
            && freshOccurrence.DestinationNodeRef is { } destination
            && destination != relation.DestinationNodeRef
            && freshOccurrence.EntryAffordanceEvidenceRef is { } affordance
            && string.Equals(
                affordance,
                relation.EntryAffordanceEvidenceRef,
                StringComparison.Ordinal)
            && historicalRevision.Value > 0
            && freshOccurrence.EvidenceRevision.Value > historicalRevision.Value;
}

/// <summary>
/// Pure deterministic state replacement seam.  Validation completes before a
/// next immutable snapshot is constructed; rejection returns the exact prior
/// state reference, proving atomic zero-commit behavior.
/// </summary>
public static class ContainerRuntimeV2Reducer
{
    /// <summary>Prepares an immutable reduction result through the pure reducer.</summary>
    public static ContainerRuntimeV2Preparation Reduce(
        ContainerRuntimeV2State previous,
        ContainerRuntimeV2ReductionInput? input)
        => Prepare(previous, input);

    /// <summary>Validates a candidate and builds one immutable state replacement.</summary>
    public static ContainerRuntimeV2Preparation Prepare(
        ContainerRuntimeV2State previous,
        ContainerRuntimeV2ReductionInput? input)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (input is null)
            return ContainerRuntimeV2Preparation.Rejected(previous, "candidate input is unavailable");

        var occurrence = input.Occurrence;
        if (!IsValid(occurrence.OccurrenceRef)
            || !IsValid(occurrence.FreshObservationRef)
            || !Enum.IsDefined(occurrence.Boundary))
            return Reject(previous, "occurrence reference, observation, or boundary is invalid");
        if (occurrence.SourceNodeRef is { } sourceRef && !IsValid(sourceRef))
            return Reject(previous, "occurrence source reference is invalid");
        if (occurrence.DestinationNodeRef is { } destinationRef && !IsValid(destinationRef))
            return Reject(previous, "occurrence destination reference is invalid");
        if (occurrence.TriggerOccurrenceRef is { } triggerRef
            && string.IsNullOrWhiteSpace(triggerRef))
            return Reject(previous, "occurrence trigger reference is invalid");
        if (occurrence.EvidenceRevision.Value <= previous.EvidenceRevision.Value)
            return Reject(previous, "evidence revision is stale or already committed");
        if (previous.TransitionOccurrences.Any(existing => existing.OccurrenceRef == occurrence.OccurrenceRef))
            return Reject(previous, "duplicate transition occurrence reference");
        if (input.NodesToAdd.Any(node => node is null))
            return Reject(previous, "node candidate is unavailable");

        var existingNodes = previous.Graph.Nodes.ToDictionary(node => node.NodeRef);
        foreach (var node in input.NodesToAdd)
        {
            if (node is null || !IsValid(node.NodeRef) || !Enum.IsDefined(node.LifecycleStage))
                return Reject(previous, "node candidate is invalid");
            if (!existingNodes.TryAdd(node.NodeRef, node))
                return Reject(previous, "duplicate or conflicting node reference");
        }

        if (occurrence.SourceNodeRef is { } source && !existingNodes.ContainsKey(source))
            return Reject(previous, "occurrence source reference is unknown");
        if (occurrence.DestinationNodeRef is { } destination && !existingNodes.ContainsKey(destination))
            return Reject(previous, "occurrence destination reference is unknown");

        if (input.CurrentContainer is { } current)
        {
            if (!occurrence.IsCompleted || occurrence.DestinationNodeRef is not { } completedDestination)
                return Reject(previous, "current replacement requires a completed occurrence with a destination");
            if (!IsValid(current.NodeRef) || !IsValid(current.CurrentSliceRef))
                return Reject(previous, "current node or slice reference is invalid");
            if (!existingNodes.ContainsKey(current.NodeRef))
                return Reject(previous, "current node reference is unknown");
            if (current.NodeRef != completedDestination)
            {
                return Reject(previous, "current node and occurrence destination disagree");
            }

            if (current.EntryContext is { } entry)
            {
                if (!IsValid(entry.SourceNodeRef)
                    || !IsValid(entry.EntryTransitionOccurrenceRef))
                    return Reject(previous, "entry context reference is invalid");
                if (!existingNodes.ContainsKey(entry.SourceNodeRef))
                    return Reject(previous, "entry source reference is unknown");
                var preservesExistingEntry = previous.CurrentContainer?.NodeRef == current.NodeRef
                    && previous.CurrentContainer.EntryContext == entry;
                var restoresRecordedEntry = previous.TransitionOccurrences.Any(accepted =>
                    accepted.OccurrenceRef == entry.EntryTransitionOccurrenceRef
                    && accepted.DestinationNodeRef == current.NodeRef);
                if (!preservesExistingEntry && !restoresRecordedEntry)
                {
                    if (entry.EntryTransitionOccurrenceRef != occurrence.OccurrenceRef)
                        return Reject(previous, "entry occurrence does not match candidate occurrence");
                    if (occurrence.SourceNodeRef is not { } entrySource
                        || entry.SourceNodeRef != entrySource)
                        return Reject(previous, "entry source and occurrence source disagree");
                }
            }
        }

        var relations = previous.Graph.Relations.ToBuilder();
        if (!Enum.IsDefined(input.RelationEligibility))
            return Reject(previous, "relation eligibility is invalid");
        if (input.RelationEligibility == ContainerRelationEligibility.ELIGIBLE
            && (input.Relation is null || !occurrence.IsCompleted))
            return Reject(previous, "eligible relation support requires a relation and completed occurrence");

        if (input.Relation is { } suppliedRelation
            && (!IsValid(suppliedRelation.RelationRef)
                || !IsValid(suppliedRelation.SourceNodeRef)
                || !IsValid(suppliedRelation.DestinationNodeRef)))
        {
            return Reject(previous, "relation reference or endpoint is invalid");
        }

        if (input.Relation is { } relation
            && input.RelationEligibility == ContainerRelationEligibility.ELIGIBLE)
        {
            if (!existingNodes.ContainsKey(relation.SourceNodeRef)
                || !existingNodes.ContainsKey(relation.DestinationNodeRef))
            {
                return Reject(previous, "relation source or destination reference is unknown");
            }
            if (occurrence.SourceNodeRef is not { } occurrenceSource
                || occurrence.DestinationNodeRef is not { } occurrenceDestination
                || relation.SourceNodeRef != occurrenceSource
                || relation.DestinationNodeRef != occurrenceDestination)
            {
                return Reject(previous, "relation and occurrence endpoints disagree");
            }
            if (occurrence.EntryAffordanceEvidenceRef is { } occurrenceAffordance
                && !string.Equals(
                    relation.EntryAffordanceEvidenceRef,
                    occurrenceAffordance,
                    StringComparison.Ordinal))
            {
                return Reject(previous, "relation and occurrence entry-affordance evidence disagree");
            }

            var relationIndex = -1;
            for (var index = 0; index < relations.Count; index++)
            {
                if (relations[index].RelationRef == relation.RelationRef)
                {
                    relationIndex = index;
                    break;
                }
            }

            if (relationIndex >= 0)
            {
                var existing = relations[relationIndex];
                if (existing.SourceNodeRef != relation.SourceNodeRef
                    || existing.DestinationNodeRef != relation.DestinationNodeRef
                    || !string.Equals(existing.EntryAffordanceEvidenceRef, relation.EntryAffordanceEvidenceRef, StringComparison.Ordinal))
                {
                    return Reject(previous, "relation reference conflicts with existing relation");
                }
                relations[relationIndex] = existing.Support(occurrence);
            }
            else
            {
                relations.Add(relation.Support(occurrence));
            }
        }

        if (input.CurrentContainer?.EntryContext?.EntryRelationRef is { } entryRelationRef)
        {
            if (!IsValid(entryRelationRef)
                || !relations.Any(relation => relation.RelationRef == entryRelationRef))
                return Reject(previous, "entry relation reference is unknown");
        }

        var nextGraph = new ContainerGraphSnapshot(
            existingNodes.Values,
            relations,
            previous.Graph.OccurrenceRefs.Add(occurrence.OccurrenceRef));
        var nextOccurrences = previous.TransitionOccurrences.Add(occurrence);
        var next = new ContainerRuntimeV2State(
            nextGraph,
            input.CurrentContainer ?? previous.CurrentContainer,
            nextOccurrences,
            occurrence.EvidenceRevision,
            previous.Slices,
            previous.SpatialRegions,
            previous.Occurrences,
            previous.FastAssessments,
            previous.UnmatchedAuxiliaryEvidence);
        return ContainerRuntimeV2Preparation.Accepted(next);
    }

    /// <summary>
    /// Validates and prepares one atomic Slice + Occurrence[] +
    /// FastAssessment[] replacement through the existing V2 state seam.
    /// Rejection returns the exact prior state reference.
    /// </summary>
    public static ContainerRuntimeV2Preparation PrepareAcceptedEvidence(
        ContainerRuntimeV2State previous,
        SliceAcceptanceCommit? commit)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (commit is null)
            return Reject(previous, "accepted evidence commit is unavailable");

        var slice = commit.Slice;
        if (!IsValid(slice.SliceRef)
            || slice.EvidenceRevision.Value <= previous.EvidenceRevision.Value)
            return Reject(previous, "accepted Slice revision is stale or its reference is invalid");
        if (!slice.IsAcceptedEvidenceComplete)
            return Reject(previous, "accepted Slice is missing observation, viewport, regions, or stability evidence");
        if (previous.Slices.Any(existing => existing.SliceRef == slice.SliceRef))
            return Reject(previous, "duplicate accepted Slice reference");

        if (commit.SpatialRegions.Any(region => region is null)
            || HasDuplicates(commit.SpatialRegions.Select(region => region.RegionRef))
            || commit.SpatialRegions.Any(region => !region.IsValid)
            || !SameSet(slice.SpatialRegionRefs, commit.SpatialRegions.Select(region => region.RegionRef)))
            return Reject(previous, "Slice spatial region references are invalid or dangling");

        if (commit.Occurrences.Any(occurrence => occurrence is null)
            || HasDuplicates(commit.Occurrences.Select(occurrence => occurrence.OccurrenceRef))
            || commit.Occurrences.Any(occurrence => occurrence.SliceRef != slice.SliceRef
                || !slice.SpatialRegionRefs.Contains(occurrence.RegionBinding.PrimarySpatialRegionRef ?? default)
                    && !occurrence.RegionBinding.Ambiguous)
            || commit.Occurrences.Any(occurrence => previous.Occurrences.Any(existing => existing.OccurrenceRef == occurrence.OccurrenceRef))
            || !SameSet(slice.OccurrenceRefs, commit.Occurrences.Select(occurrence => occurrence.OccurrenceRef)))
            return Reject(previous, "Slice occurrence references are invalid or dangling");

        var acceptedOccurrenceRefs = commit.Occurrences
            .Select(occurrence => occurrence.OccurrenceRef)
            .ToHashSet();
        if (commit.FastAssessments.Any(assessment => assessment is null)
            || HasDuplicates(commit.FastAssessments.Select(assessment => assessment.AssessmentRef))
            || commit.FastAssessments.Any(assessment => assessment.SliceRef != slice.SliceRef
                || assessment.TargetOccurrenceRefs.IsDefaultOrEmpty
                || assessment.TargetOccurrenceRefs.Any(target => !acceptedOccurrenceRefs.Contains(target)))
            || commit.FastAssessments.Any(assessment => previous.FastAssessments.Any(existing => existing.AssessmentRef == assessment.AssessmentRef))
            || !SameSet(slice.FastAssessmentRefs, commit.FastAssessments.Select(assessment => assessment.AssessmentRef)))
            return Reject(previous, "Slice Fast assessment references are invalid or dangling");

        if (commit.UnmatchedAuxiliaryEvidence.Any(auxiliary => auxiliary is null)
            || commit.UnmatchedAuxiliaryEvidence.Any(auxiliary => auxiliary.SliceRef != slice.SliceRef)
            || HasDuplicates(commit.UnmatchedAuxiliaryEvidence.Select(auxiliary => auxiliary.EvidenceRef)))
            return Reject(previous, "unmatched auxiliary evidence is invalid");

        var next = new ContainerRuntimeV2State(
            previous.Graph,
            previous.CurrentContainer,
            previous.TransitionOccurrences,
            slice.EvidenceRevision,
            previous.Slices.Add(slice),
            previous.SpatialRegions.AddRange(commit.SpatialRegions),
            previous.Occurrences.AddRange(commit.Occurrences),
            previous.FastAssessments.AddRange(commit.FastAssessments),
            previous.UnmatchedAuxiliaryEvidence.AddRange(commit.UnmatchedAuxiliaryEvidence));
        return ContainerRuntimeV2Preparation.Accepted(next);
    }

    /// <summary>
    /// Validates and prepares one LocalModel append/archival whole-replacement
    /// through the existing V2 state seam. All referenced evidence must already
    /// be accepted in state (no dangling references); archival moves active →
    /// archived without deletion; the candidate revision must strictly advance.
    /// Rejection returns the exact prior state reference.
    /// </summary>
    public static ContainerRuntimeV2Preparation PrepareLocalModelAppend(
        ContainerRuntimeV2State previous,
        LocalModelAppendInput? input)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (input is null)
            return Reject(previous, "local model append input is unavailable");
        if (!IsValid(input.NodeRef))
            return Reject(previous, "local model node reference is invalid");
        if (!previous.Graph.Nodes.Any(node => node.NodeRef == input.NodeRef))
            return Reject(previous, "local model node reference is not recorded in the Graph");
        if (input.EvidenceRevision.Value <= previous.EvidenceRevision.Value)
            return Reject(previous, "local model append revision is stale or already committed");

        var model = previous.LocalModels.FirstOrDefault(existing => existing.NodeRef == input.NodeRef);
        var hasModel = model is not null;

        var activeSlices = hasModel ? model!.ActiveSliceRefs : ImmutableArray<ContainerSliceRef>.Empty;
        var archivedSlices = hasModel ? model!.ArchivedSliceRefs : ImmutableArray<ContainerSliceRef>.Empty;
        var activeOccurrences = hasModel ? model!.ActiveOccurrenceRefs : ImmutableArray<ViewportOccurrenceRef>.Empty;
        var archivedOccurrences = hasModel ? model!.ArchivedOccurrenceRefs : ImmutableArray<ViewportOccurrenceRef>.Empty;
        var boundAssessments = hasModel ? model!.FastAssessmentRefs : ImmutableArray<FastAssessmentRef>.Empty;
        var boundTransitions = hasModel ? model!.TransitionOccurrenceRefs : ImmutableArray<TransitionOccurrenceRef>.Empty;

        // Activation: references must exist in accepted state and be new to both layers.
        if (input.SlicesToActivate.Any(sliceRef => !previous.Slices.Any(slice => slice.SliceRef == sliceRef))
            || input.SlicesToActivate.Any(sliceRef => activeSlices.Contains(sliceRef) || archivedSlices.Contains(sliceRef)))
            return Reject(previous, "slices to activate are unknown, dangling, or already layered");
        if (input.OccurrencesToActivate.Any(occurrenceRef => !previous.Occurrences.Any(occurrence => occurrence.OccurrenceRef == occurrenceRef))
            || input.OccurrencesToActivate.Any(occurrenceRef => activeOccurrences.Contains(occurrenceRef) || archivedOccurrences.Contains(occurrenceRef)))
            return Reject(previous, "occurrences to activate are unknown, dangling, or already layered");

        var modelSlicesAfterActivation = activeSlices.AddRange(input.SlicesToActivate);
        var allModelSlices = modelSlicesAfterActivation.AddRange(archivedSlices);
        if (input.OccurrencesToActivate.Any(occurrenceRef =>
                previous.Occurrences
                    .Where(occurrence => occurrence.OccurrenceRef == occurrenceRef)
                    .Any(occurrence => !allModelSlices.Contains(occurrence.SliceRef))))
            return Reject(previous, "occurrences to activate belong to slices outside this LocalModel");

        // Archival: moves active → archived; the model and the active refs must exist.
        if (input.SliceRefsToArchive.Any(sliceRef => !activeSlices.Contains(sliceRef)))
            return Reject(previous, "slices to archive are not in the active layer");
        if (input.OccurrenceRefsToArchive.Any(occurrenceRef => !activeOccurrences.Contains(occurrenceRef)))
            return Reject(previous, "occurrences to archive are not in the active layer");
        if (input.SlicesToActivate.Any(sliceRef => input.SliceRefsToArchive.Contains(sliceRef))
            || input.OccurrencesToActivate.Any(occurrenceRef => input.OccurrenceRefsToArchive.Contains(occurrenceRef)))
            return Reject(previous, "a reference cannot be activated and archived in one commit");

        // Assessments/transitions: must exist in accepted state, belong to model slices, and be new.
        if (input.FastAssessmentRefs.Any(assessmentRef => !previous.FastAssessments.Any(assessment => assessment.AssessmentRef == assessmentRef))
            || input.FastAssessmentRefs.Any(assessmentRef => boundAssessments.Contains(assessmentRef)))
            return Reject(previous, "fast assessment references are unknown, dangling, or already bound");
        if (input.FastAssessmentRefs.Any(assessmentRef =>
                previous.FastAssessments
                    .Where(assessment => assessment.AssessmentRef == assessmentRef)
                    .Any(assessment => !allModelSlices.Contains(assessment.SliceRef))))
            return Reject(previous, "fast assessment references belong to slices outside this LocalModel");
        if (input.TransitionOccurrenceRefs.Any(transitionRef => !previous.TransitionOccurrences.Any(occurrence => occurrence.OccurrenceRef == transitionRef))
            || input.TransitionOccurrenceRefs.Any(transitionRef => boundTransitions.Contains(transitionRef)))
            return Reject(previous, "transition occurrence references are unknown, dangling, or already bound");

        var nextModel = new NodeLocalModel(
            input.NodeRef,
            activeSliceRefs: modelSlicesAfterActivation.RemoveAll(input.SliceRefsToArchive.Contains),
            archivedSliceRefs: archivedSlices.AddRange(input.SliceRefsToArchive.Distinct()),
            activeOccurrenceRefs: activeOccurrences
                .AddRange(input.OccurrencesToActivate)
                .RemoveAll(input.OccurrenceRefsToArchive.Contains),
            archivedOccurrenceRefs: archivedOccurrences.AddRange(input.OccurrenceRefsToArchive.Distinct()),
            fastAssessmentRefs: boundAssessments.AddRange(input.FastAssessmentRefs),
            transitionOccurrenceRefs: boundTransitions.AddRange(input.TransitionOccurrenceRefs),
            canonicalProjection: hasModel ? model!.CanonicalProjection : null,
            regionCoverageProjections: hasModel ? model!.RegionCoverageProjections : null);
        if (!nextModel.IsValid)
            return Reject(previous, "resulting local model violates layering invariants");

        var nextLocalModels = previous.LocalModels
            .RemoveAll(existing => existing.NodeRef == input.NodeRef)
            .Add(nextModel);

        var next = new ContainerRuntimeV2State(
            previous.Graph,
            previous.CurrentContainer,
            previous.TransitionOccurrences,
            input.EvidenceRevision,
            previous.Slices,
            previous.SpatialRegions,
            previous.Occurrences,
            previous.FastAssessments,
            previous.UnmatchedAuxiliaryEvidence,
            nextLocalModels);
        return ContainerRuntimeV2Preparation.Accepted(next);
    }

    private static ContainerRuntimeV2Preparation Reject(ContainerRuntimeV2State previous, string reason)
        => ContainerRuntimeV2Preparation.Rejected(previous, reason);

    private static bool IsValid(ContainerNodeRef value)
        => !string.IsNullOrWhiteSpace(value.Value);

    private static bool IsValid(ContainerRelationRef value)
        => !string.IsNullOrWhiteSpace(value.Value);

    private static bool IsValid(TransitionOccurrenceRef value)
        => !string.IsNullOrWhiteSpace(value.Value);

    private static bool IsValid(ContainerSliceRef value)
        => !string.IsNullOrWhiteSpace(value.Value);

    private static bool HasDuplicates<T>(IEnumerable<T> values)
        where T : notnull
    {
        var seen = new HashSet<T>();
        return values.Any(value => !seen.Add(value));
    }

    private static bool SameSet<T>(IEnumerable<T> left, IEnumerable<T> right)
        where T : notnull
        => left.ToHashSet().SetEquals(right);

    private static bool IsValid(string value)
        => !string.IsNullOrWhiteSpace(value);
}

/// <summary>
/// Exact evidence context shared by one stateless V2 lifecycle. Values are
/// independently supplied references; none is inferred from an opaque string.
/// </summary>
public sealed record ContainerRuntimeV2EvidenceContext
{
    /// <summary>Creates one exact lifecycle evidence context.</summary>
    public ContainerRuntimeV2EvidenceContext(
        string runRef,
        string observationRef,
        long freshObservationSequence,
        SemanticEvidenceRevision evidenceRevision,
        TransitionOccurrenceRef transitionOccurrenceRef,
        string triggerOccurrenceRef,
        ContainerNodeRef sourceNodeRef,
        ContainerNodeRef destinationNodeRef,
        ContainerSliceRef currentSliceRef,
        ContainerObligationContext? ownerContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationRef);
        ArgumentOutOfRangeException.ThrowIfNegative(freshObservationSequence);
        if (string.IsNullOrWhiteSpace(transitionOccurrenceRef.Value))
            throw new ArgumentException("Transition occurrence reference is required.", nameof(transitionOccurrenceRef));
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerOccurrenceRef);
        if (string.IsNullOrWhiteSpace(sourceNodeRef.Value))
            throw new ArgumentException("Source node reference is required.", nameof(sourceNodeRef));
        if (string.IsNullOrWhiteSpace(destinationNodeRef.Value))
            throw new ArgumentException("Destination node reference is required.", nameof(destinationNodeRef));
        if (string.IsNullOrWhiteSpace(currentSliceRef.Value))
            throw new ArgumentException("Current Slice reference is required.", nameof(currentSliceRef));
        RunRef = runRef;
        ObservationRef = observationRef;
        FreshObservationSequence = freshObservationSequence;
        EvidenceRevision = evidenceRevision;
        TransitionOccurrenceRef = transitionOccurrenceRef;
        TriggerOccurrenceRef = triggerOccurrenceRef;
        SourceNodeRef = sourceNodeRef;
        DestinationNodeRef = destinationNodeRef;
        CurrentSliceRef = currentSliceRef;
        OwnerContext = ownerContext;
    }

    /// <summary>Gets the opaque Run reference.</summary>
    public string RunRef { get; }
    /// <summary>Gets the exact fresh observation reference.</summary>
    public string ObservationRef { get; }
    /// <summary>Gets the explicit fresh observation sequence.</summary>
    public long FreshObservationSequence { get; }
    /// <summary>Gets the accepted evidence revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the exact transition occurrence reference.</summary>
    public TransitionOccurrenceRef TransitionOccurrenceRef { get; }
    /// <summary>Gets the exact trigger occurrence reference.</summary>
    public string TriggerOccurrenceRef { get; }
    /// <summary>Gets the exact source node reference.</summary>
    public ContainerNodeRef SourceNodeRef { get; }
    /// <summary>Gets the exact destination node reference.</summary>
    public ContainerNodeRef DestinationNodeRef { get; }
    /// <summary>Gets the exact current Slice reference.</summary>
    public ContainerSliceRef CurrentSliceRef { get; }
    /// <summary>Gets optional owner obligation context.</summary>
    public ContainerObligationContext? OwnerContext { get; }
}

/// <summary>
/// Immutable input to the one stateless V2 composition lifecycle.
/// NEW_SYMBOL_JUSTIFICATION: no existing type correlates reducer, Fast, Slow,
/// correction, checkpoint and read projections. Extending the reducer would
/// mix structural replacement with asynchronous assessment; extending Slow
/// would give its evidence port composition responsibility.
/// </summary>
public sealed record ContainerRuntimeV2LifecycleInput
{
    /// <summary>Creates one fully correlated lifecycle input.</summary>
    public ContainerRuntimeV2LifecycleInput(
        ContainerRuntimeV2State previousState,
        ContainerRuntimeV2EvidenceContext evidenceContext,
        ContainerRuntimeV2ReductionInput reductionInput,
        FastContainerResolutionRequest fastRequest,
        SlowContainerSemanticMode slowMode,
        SlowContainerSemanticRequest slowRequest,
        ISlowContainerSemanticAdvisor? slowAdvisor = null,
        ContainerExecutionPath? checkpointPath = null)
    {
        ArgumentNullException.ThrowIfNull(previousState);
        ArgumentNullException.ThrowIfNull(evidenceContext);
        ArgumentNullException.ThrowIfNull(reductionInput);
        ArgumentNullException.ThrowIfNull(fastRequest);
        ArgumentNullException.ThrowIfNull(slowRequest);
        if (!Enum.IsDefined(slowMode))
            throw new ArgumentOutOfRangeException(nameof(slowMode));
        PreviousState = previousState;
        EvidenceContext = evidenceContext;
        ReductionInput = reductionInput;
        FastRequest = fastRequest;
        SlowMode = slowMode;
        SlowRequest = slowRequest;
        SlowAdvisor = slowAdvisor;
        CheckpointPath = checkpointPath;
    }

    /// <summary>Gets the prior immutable V2 state.</summary>
    public ContainerRuntimeV2State PreviousState { get; }
    /// <summary>Gets the exact shared lifecycle evidence context.</summary>
    public ContainerRuntimeV2EvidenceContext EvidenceContext { get; }
    /// <summary>Gets the candidate immutable reduction input.</summary>
    public ContainerRuntimeV2ReductionInput ReductionInput { get; }
    /// <summary>Gets the correlated Fast request.</summary>
    public FastContainerResolutionRequest FastRequest { get; }
    /// <summary>Gets the selected Slow consumption mode.</summary>
    public SlowContainerSemanticMode SlowMode { get; }
    /// <summary>Gets the correlated Slow request.</summary>
    public SlowContainerSemanticRequest SlowRequest { get; }
    /// <summary>Gets the optional advisory assessment source.</summary>
    public ISlowContainerSemanticAdvisor? SlowAdvisor { get; }
    /// <summary>Gets optional explicitly ordered checkpoint evidence.</summary>
    public ContainerExecutionPath? CheckpointPath { get; }
}

/// <summary>Identifies the immutable semantic source selected for a read view.</summary>
public enum ContainerRuntimeV2SemanticTrustSource
{
    /// <summary>No current semantic candidate is available.</summary>
    None,
    /// <summary>The candidate is derived from Fast working evidence.</summary>
    Fast,
    /// <summary>The candidate is derived from current Slow evidence.</summary>
    Slow,
}

/// <summary>Derived, authority-free semantic and trust view for one lifecycle.</summary>
public sealed record ContainerRuntimeV2SemanticTrustView
{
    /// <summary>Creates one immutable derived semantic view.</summary>
    internal ContainerRuntimeV2SemanticTrustView(
        SemanticEvidenceRevision evidenceRevision,
        ContainerRuntimeV2SemanticTrustSource source,
        string? semanticCandidate,
        bool isCurrent,
        bool conflictsWithFast)
    {
        EvidenceRevision = evidenceRevision;
        Source = source;
        SemanticCandidate = string.IsNullOrWhiteSpace(semanticCandidate) ? null : semanticCandidate;
        IsCurrent = isCurrent;
        ConflictsWithFast = conflictsWithFast;
    }

    /// <summary>Gets the revision used for this derived view.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the immutable source assessment.</summary>
    public ContainerRuntimeV2SemanticTrustSource Source { get; }
    /// <summary>Gets the working semantic candidate, when available.</summary>
    public string? SemanticCandidate { get; }
    /// <summary>Gets whether the candidate is current for the supplied state.</summary>
    public bool IsCurrent { get; }
    /// <summary>Gets whether current Slow evidence conflicts with Fast.</summary>
    public bool ConflictsWithFast { get; }
    /// <summary>Gets whether this view carries no authority effect.</summary>
    public bool IsAuthorityFree => true;
    /// <summary>Gets whether this view carries no runtime effect.</summary>
    public bool HasRuntimeEffect => false;
}

/// <summary>Unified immutable read projection from one V2 lifecycle.</summary>
public sealed record ContainerRuntimeV2ReadProjection
{
    /// <summary>Creates one unified authority-free read projection.</summary>
    internal ContainerRuntimeV2ReadProjection(
        ContainerRuntimeV2EvidenceContext evidenceContext,
        ContainerRuntimeV2State state,
        ImmutableArray<ContainerGraphRelationAssessment> relationAssessments,
        FastContainerAssessment fastAssessment,
        SlowContainerSemanticConsumption slowConsumption,
        ContainerRuntimeV2SemanticTrustView semanticTrust,
        ContainerSemanticCorrectionFact? correction,
        ContainerObligationReevaluationInput? obligationInput,
        ContainerCheckpointProposal? checkpoint)
    {
        EvidenceContext = evidenceContext;
        State = state;
        RelationAssessments = relationAssessments;
        FastAssessment = fastAssessment;
        SlowConsumption = slowConsumption;
        SemanticTrust = semanticTrust;
        Correction = correction;
        ObligationInput = obligationInput;
        Checkpoint = checkpoint;
    }

    /// <summary>Gets the shared exact evidence context.</summary>
    public ContainerRuntimeV2EvidenceContext EvidenceContext { get; }
    /// <summary>Gets the immutable state current when this view was consumed.</summary>
    public ContainerRuntimeV2State State { get; }
    /// <summary>Gets derived Graph relation assessments.</summary>
    public ImmutableArray<ContainerGraphRelationAssessment> RelationAssessments { get; }
    /// <summary>Gets the derived Fast assessment.</summary>
    public FastContainerAssessment FastAssessment { get; }
    /// <summary>Gets the derived Slow consumption.</summary>
    public SlowContainerSemanticConsumption SlowConsumption { get; }
    /// <summary>Gets the derived semantic/trust view.</summary>
    public ContainerRuntimeV2SemanticTrustView SemanticTrust { get; }
    /// <summary>Gets the optional current correction evidence.</summary>
    public ContainerSemanticCorrectionFact? Correction { get; }
    /// <summary>Gets the optional owner reevaluation input.</summary>
    public ContainerObligationReevaluationInput? ObligationInput { get; }
    /// <summary>Gets the optional derived checkpoint proposal.</summary>
    public ContainerCheckpointProposal? Checkpoint { get; }
    /// <summary>Gets a pure correction reference derived from occurrence and revision.</summary>
    public string? CorrectionRef
        => Correction is null
            ? null
            : $"correction:{Correction.TransitionOccurrenceRef.Value}@{Correction.EvidenceRevision.Value}";
    /// <summary>Gets the immutable consumption reference for the same correction.</summary>
    public string? PendingCorrectionRef => CorrectionRef;
}

/// <summary>Immutable result of the non-blocking structural V2 start.</summary>
public sealed record ContainerRuntimeV2StartedResult
{
    /// <summary>Creates a started lifecycle result.</summary>
    internal ContainerRuntimeV2StartedResult(
        bool accepted,
        ContainerRuntimeV2LifecycleInput input,
        ContainerRuntimeV2State state,
        FastContainerAssessment? fastAssessment,
        ImmutableArray<ContainerGraphRelationAssessment> relationAssessments,
        ContainerCheckpointProposal? checkpoint,
        Task<SlowContainerSemanticInvocation>? slowAcquisition,
        SlowContainerSemanticRequest slowRequest,
        string? rejectionReason)
    {
        Accepted = accepted;
        Input = input;
        State = state;
        FastAssessment = fastAssessment;
        RelationAssessments = relationAssessments;
        Checkpoint = checkpoint;
        SlowAcquisition = slowAcquisition;
        SlowRequest = slowRequest;
        RejectionReason = rejectionReason;
    }

    /// <summary>Gets whether structural preparation and Fast evaluation succeeded.</summary>
    public bool Accepted { get; }
    /// <summary>Gets the immutable lifecycle input retained for exact completion binding.</summary>
    public ContainerRuntimeV2LifecycleInput Input { get; }
    /// <summary>Gets the structurally accepted state.</summary>
    public ContainerRuntimeV2State State { get; }
    /// <summary>Gets the completed Fast assessment, when accepted.</summary>
    public FastContainerAssessment? FastAssessment { get; }
    /// <summary>Gets relation assessments produced before Slow completion.</summary>
    public ImmutableArray<ContainerGraphRelationAssessment> RelationAssessments { get; }
    /// <summary>Gets the checkpoint projection produced before Slow completion.</summary>
    public ContainerCheckpointProposal? Checkpoint { get; }
    /// <summary>Gets the pending or completed raw Slow acquisition.</summary>
    public Task<SlowContainerSemanticInvocation>? SlowAcquisition { get; }
    /// <summary>Gets the actual Slow request carrying the produced Fast assessment.</summary>
    public SlowContainerSemanticRequest SlowRequest { get; }
    /// <summary>Gets the explicit fail-closed start reason.</summary>
    public string? RejectionReason { get; }
}

/// <summary>Immutable result of one V2 lifecycle attempt.</summary>
public sealed record ContainerRuntimeV2LifecycleResult
{
    /// <summary>Creates an immutable lifecycle result.</summary>
    internal ContainerRuntimeV2LifecycleResult(
        bool accepted,
        ContainerRuntimeV2State state,
        ContainerRuntimeV2ReadProjection? readProjection,
        string? rejectionReason)
    {
        Accepted = accepted;
        State = state;
        ReadProjection = readProjection;
        RejectionReason = rejectionReason;
    }

    /// <summary>Gets whether the complete lifecycle was accepted.</summary>
    public bool Accepted { get; }
    /// <summary>Gets the accepted current state, or the supplied unchanged state.</summary>
    public ContainerRuntimeV2State State { get; }
    /// <summary>Gets the unified read projection when accepted.</summary>
    public ContainerRuntimeV2ReadProjection? ReadProjection { get; }
    /// <summary>Gets the explicit fail-closed reason, when rejected.</summary>
    public string? RejectionReason { get; }
}

/// <summary>
/// Stateless composition facade for the immutable V2 evidence lifecycle.
/// It owns no state and grants no action, recovery, progress or Goal authority.
/// </summary>
public static class ContainerRuntimeV2
{
    /// <summary>
    /// Starts structural reduction, Fast assessment, Graph projection and
    /// checkpoint projection without waiting for Slow acquisition.
    /// </summary>
    /// <param name="input">Fully correlated immutable lifecycle input.</param>
    /// <param name="cancellationToken">Cancellation supplied to Slow acquisition.</param>
    /// <returns>A structural result carrying the pending Slow task.</returns>
    public static ContainerRuntimeV2StartedResult Start(
        ContainerRuntimeV2LifecycleInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bindingFailure = ValidateBindings(input);
        if (bindingFailure is not null)
            return StartedRejected(input, bindingFailure);

        var preparation = ContainerRuntimeV2Reducer.Prepare(input.PreviousState, input.ReductionInput);
        if (!preparation.CanCommit)
            return StartedRejected(input, preparation.RejectionReason ?? "V2 reduction rejected");

        var fastAssessment = FastContainerResolver.Resolve(input.FastRequest);
        if (!MatchesFast(input.EvidenceContext, input.FastRequest, fastAssessment))
            return StartedRejected(input, "Fast assessment is not bound to lifecycle evidence");

        var relationAssessments = ContainerGraphQuery.ProjectRelationAssessments(
            preparation.State,
            input.ReductionInput.Occurrence,
            input.ReductionInput.RelationEligibility);
        var checkpoint = input.CheckpointPath is null
            ? null
            : ContainerSemanticCorrectionProjector.ProjectCheckpoint(
                input.CheckpointPath,
                preparation.State.EvidenceRevision);
        var acquisitionRequest = new SlowContainerSemanticRequest(
            input.SlowRequest.ObservationRef,
            input.SlowRequest.EvidenceRevision,
            input.SlowRequest.NodeRef,
            input.SlowRequest.SourceNodeRef,
            input.SlowRequest.TriggerOccurrenceRef,
            input.SlowRequest.TransitionOccurrenceRef,
            fastAssessment);
        var slowAcquisition = SlowContainerSemanticConsumer.AcquireAsync(
            input.SlowMode,
            input.SlowAdvisor,
            acquisitionRequest,
            cancellationToken);
        return new ContainerRuntimeV2StartedResult(
            true,
            input,
            preparation.State,
            fastAssessment,
            relationAssessments,
            checkpoint,
            slowAcquisition,
            acquisitionRequest,
            null);
    }

    /// <summary>
    /// Completes Slow consumption against the immutable state current at
    /// consumption time. Older Slow results remain readable but cannot produce
    /// current correction or trust.
    /// </summary>
    /// <param name="started">The result returned by <see cref="Start"/>.</param>
    /// <param name="invocation">The raw acquisition carried by the started result.</param>
    /// <param name="currentState">The latest immutable accepted state.</param>
    /// <returns>A current read projection or a fail-closed result.</returns>
    public static ContainerRuntimeV2LifecycleResult CompleteSlow(
        ContainerRuntimeV2StartedResult started,
        SlowContainerSemanticInvocation invocation,
        ContainerRuntimeV2State currentState)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(currentState);
        if (!started.Accepted)
            return Rejected(currentState, started.RejectionReason ?? "V2 start rejected");
        if (!MatchesSlowRequest(started.SlowRequest, invocation.Request))
            return Rejected(currentState, "Slow invocation is not bound to the started request");
        if (!HasExactOccurrence(currentState, started.Input.EvidenceContext))
            return Rejected(currentState, "current state does not contain the exact lifecycle occurrence");
        if (currentState.EvidenceRevision.CompareTo(started.State.EvidenceRevision) < 0)
            return Rejected(currentState, "current state is older than the started structural state");

        var slowConsumption = SlowContainerSemanticConsumer.Project(
            invocation,
            currentState.EvidenceRevision);
        if (slowConsumption.Availability == SlowContainerSemanticAvailability.Rejected)
            return Rejected(currentState, slowConsumption.RejectionReason ?? "Slow assessment rejected");
        if (!MatchesSlow(started.Input.EvidenceContext, started.Input.SlowRequest, slowConsumption))
            return Rejected(currentState, "Slow consumption is not bound to lifecycle evidence");

        var correction = ContainerSemanticCorrectionProjector.TryCreateCorrection(slowConsumption);
        var obligationInput = ContainerSemanticCorrectionProjector.ProjectObligationInput(
            correction,
            started.Input.EvidenceContext.OwnerContext);
        var semanticTrust = DeriveSemanticTrust(
            started.FastAssessment!,
            slowConsumption,
            currentState.EvidenceRevision);
        var relationAssessments = ContainerGraphQuery.ProjectRelationAssessments(currentState);
        var checkpoint = started.Input.CheckpointPath is null
            ? null
            : ContainerSemanticCorrectionProjector.ProjectCheckpoint(
                started.Input.CheckpointPath,
                currentState.EvidenceRevision);
        var projection = new ContainerRuntimeV2ReadProjection(
            started.Input.EvidenceContext,
            currentState,
            relationAssessments,
            started.FastAssessment!,
            slowConsumption,
            semanticTrust,
            correction,
            obligationInput,
            checkpoint);
        return new ContainerRuntimeV2LifecycleResult(true, currentState, projection, null);
    }

    /// <summary>
    /// Completes a production Disabled-Slow lifecycle synchronously after the
    /// stateless facade has created its already-completed no-op acquisition.
    /// </summary>
    /// <param name="started">The accepted result returned by <see cref="Start"/>.</param>
    /// <param name="currentState">The immutable state to project.</param>
    /// <returns>The completed authority-free read projection.</returns>
    /// <remarks>
    /// NEW_SYMBOL_JUSTIFICATION: Agent production replacement needs a
    /// non-blocking Disabled-Slow seam; reusing <see cref="CompleteSlow"/>
    /// would force Agent to consume a Task directly and make waiting visible
    /// in the owner. This bounded facade helper rejects every non-Disabled
    /// or advisor-backed input and owns no state.
    /// </remarks>
    public static ContainerRuntimeV2LifecycleResult CompleteDisabled(
        ContainerRuntimeV2StartedResult started,
        ContainerRuntimeV2State currentState)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(currentState);
        if (started.Input.SlowMode != SlowContainerSemanticMode.Disabled
            || started.Input.SlowAdvisor is not null
            || started.SlowAcquisition is null
            || !started.SlowAcquisition.IsCompletedSuccessfully)
        {
            return Rejected(currentState, "Disabled completion requires a completed no-advisor Slow acquisition");
        }

        return CompleteSlow(started, started.SlowAcquisition.Result, currentState);
    }

    /// <summary>Convenience complete lifecycle that waits only at the caller boundary.</summary>
    public static async Task<ContainerRuntimeV2LifecycleResult> ComposeAsync(
        ContainerRuntimeV2LifecycleInput input,
        CancellationToken cancellationToken = default)
    {
        var started = Start(input, cancellationToken);
        if (!started.Accepted)
            return Rejected(input.PreviousState, started.RejectionReason ?? "V2 start rejected");
        var invocation = await started.SlowAcquisition!.ConfigureAwait(false);
        return CompleteSlow(started, invocation, started.State);
    }

    private static ContainerRuntimeV2StartedResult StartedRejected(
        ContainerRuntimeV2LifecycleInput input,
        string reason)
        => new(false, input, input.PreviousState, null, ImmutableArray<ContainerGraphRelationAssessment>.Empty, null, null, input.SlowRequest, reason);

    private static ContainerRuntimeV2LifecycleResult Rejected(
        ContainerRuntimeV2State state,
        string reason)
        => new(false, state, null, reason);

    private static ContainerRuntimeV2SemanticTrustView DeriveSemanticTrust(
        FastContainerAssessment fast,
        SlowContainerSemanticConsumption slow,
        SemanticEvidenceRevision currentRevision)
    {
        if (slow.IsCurrent
            && slow.Assessment is { } assessment
            && assessment.Kind is SlowContainerSemanticAssessmentKind.Confirm
                or SlowContainerSemanticAssessmentKind.Correct
                or SlowContainerSemanticAssessmentKind.Challenge)
        {
            var slowCandidate = assessment.CorrectedIdentityCandidate
                ?? assessment.ContainerSemantic
                ?? assessment.TriggerSemantic;
            return new ContainerRuntimeV2SemanticTrustView(
                assessment.EvidenceRevision,
                ContainerRuntimeV2SemanticTrustSource.Slow,
                slowCandidate,
                true,
                slow.ConflictsWithFast);
        }

        var fastCurrent = fast.EvidenceRevision == currentRevision;
        return new ContainerRuntimeV2SemanticTrustView(
            fast.EvidenceRevision,
            fast.SemanticSupport
                ? ContainerRuntimeV2SemanticTrustSource.Fast
                : ContainerRuntimeV2SemanticTrustSource.None,
            fast.IdentityCandidate,
            fastCurrent,
            false);
    }

    private static string? ValidateBindings(ContainerRuntimeV2LifecycleInput input)
    {
        var context = input.EvidenceContext;
        var occurrence = input.ReductionInput.Occurrence;
        if (occurrence.OccurrenceRef != context.TransitionOccurrenceRef
            || occurrence.FreshObservationRef != context.ObservationRef
            || occurrence.EvidenceRevision != context.EvidenceRevision
            || occurrence.TriggerOccurrenceRef != context.TriggerOccurrenceRef
            || occurrence.SourceNodeRef != context.SourceNodeRef
            || occurrence.DestinationNodeRef != context.DestinationNodeRef)
            return "reduction occurrence is not bound to lifecycle evidence";
        var current = input.ReductionInput.CurrentContainer;
        if (current is not null
            && (current.NodeRef != context.DestinationNodeRef
                || current.CurrentSliceRef != context.CurrentSliceRef))
            return "current replacement is not bound to lifecycle evidence";
        var fast = input.FastRequest;
        if (fast.EvidenceRevision != context.EvidenceRevision
            || fast.FreshObservationSequence != context.FreshObservationSequence
            || fast.FreshSliceRef != context.CurrentSliceRef
            || fast.CurrentNodeRef != context.SourceNodeRef
            || fast.CandidateNodeRef != context.DestinationNodeRef)
            return "Fast request is not bound to lifecycle evidence";
        var slow = input.SlowRequest;
        if (slow.ObservationRef != context.ObservationRef
            || slow.EvidenceRevision != context.EvidenceRevision
            || slow.NodeRef != context.DestinationNodeRef
            || slow.SourceNodeRef != context.SourceNodeRef
            || !string.Equals(slow.TriggerOccurrenceRef, context.TriggerOccurrenceRef, StringComparison.Ordinal)
            || slow.TransitionOccurrenceRef != context.TransitionOccurrenceRef)
            return "Slow request is not bound to lifecycle evidence";
        return null;
    }

    private static bool HasExactOccurrence(
        ContainerRuntimeV2State state,
        ContainerRuntimeV2EvidenceContext context)
        => state.TransitionOccurrences.Any(occurrence =>
            occurrence.OccurrenceRef == context.TransitionOccurrenceRef
            && occurrence.FreshObservationRef == context.ObservationRef
            && occurrence.EvidenceRevision == context.EvidenceRevision
            && occurrence.TriggerOccurrenceRef == context.TriggerOccurrenceRef
            && occurrence.SourceNodeRef == context.SourceNodeRef
            && occurrence.DestinationNodeRef == context.DestinationNodeRef);

    private static bool MatchesFast(
        ContainerRuntimeV2EvidenceContext context,
        FastContainerResolutionRequest request,
        FastContainerAssessment assessment)
        => assessment.EvidenceRevision == context.EvidenceRevision
           && assessment.CurrentNodeRef == request.CurrentNodeRef
           && assessment.CandidateNodeRef == request.CandidateNodeRef;

    private static bool MatchesSlowRequest(
        SlowContainerSemanticRequest expected,
        SlowContainerSemanticRequest actual)
        => expected.ObservationRef == actual.ObservationRef
           && expected.EvidenceRevision == actual.EvidenceRevision
           && expected.NodeRef == actual.NodeRef
           && expected.SourceNodeRef == actual.SourceNodeRef
           && expected.TransitionOccurrenceRef == actual.TransitionOccurrenceRef
           && string.Equals(expected.TriggerOccurrenceRef, actual.TriggerOccurrenceRef, StringComparison.Ordinal);

    private static bool MatchesSlow(
        ContainerRuntimeV2EvidenceContext context,
        SlowContainerSemanticRequest request,
        SlowContainerSemanticConsumption consumption)
    {
        var assessment = consumption.Assessment;
        return assessment is null
            || (assessment.ObservationRef == context.ObservationRef
                && assessment.EvidenceRevision == context.EvidenceRevision
                && assessment.NodeRef == request.NodeRef
                && assessment.SourceNodeRef == request.SourceNodeRef
                && string.Equals(assessment.TriggerOccurrenceRef, context.TriggerOccurrenceRef, StringComparison.Ordinal)
                && assessment.TransitionOccurrenceRef == context.TransitionOccurrenceRef);
    }
}
