using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.World;

/// <summary>Identifies the owner-supplied obligation being reevaluated.</summary>
public enum ContainerObligationContextKind
{
    /// <summary>The owner intended a traversal child, but the evidence differs.</summary>
    TraversalMisclick,
    /// <summary>The owner intended a directed entry branch, but the evidence differs.</summary>
    DirectedEntryWrongBranch,
}

/// <summary>Opaque reference to owner context; its value is not semantic authority.</summary>
public readonly record struct ContainerObligationContextRef
{
    /// <summary>Creates an opaque owner context reference.</summary>
    public ContainerObligationContextRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque owner context value.</summary>
    public string Value { get; }
}

/// <summary>
/// Immutable owner-supplied context for obligation reevaluation. Its intended
/// semantic and implication kind come from the owner, not from Slow evidence.
/// </summary>
public sealed record ContainerObligationContext
{
    /// <summary>Creates a typed owner context for one reevaluation.</summary>
    public ContainerObligationContext(
        ContainerObligationContextRef contextRef,
        ContainerObligationContextKind kind,
        string intendedSemantic,
        string? runRef = null,
        string? observationRef = null,
        SemanticEvidenceRevision? evidenceRevision = null,
        TransitionOccurrenceRef? transitionOccurrenceRef = null,
        string? triggerOccurrenceRef = null,
        ContainerNodeRef? parentNodeRef = null,
        string? parentSemanticPage = null,
        ContainerNodeRef? destinationNodeRef = null,
        ContainerSliceRef? currentSliceRef = null,
        long? attributedCompletionObservationSequence = null)
    {
        if (string.IsNullOrWhiteSpace(contextRef.Value))
            throw new ArgumentException("An opaque obligation context reference is required.", nameof(contextRef));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(intendedSemantic);
        ContextRef = contextRef;
        Kind = kind;
        IntendedSemantic = intendedSemantic;
        RunRef = Normalize(runRef);
        ObservationRef = Normalize(observationRef);
        EvidenceRevision = evidenceRevision;
        TransitionOccurrenceRef = transitionOccurrenceRef;
        TriggerOccurrenceRef = Normalize(triggerOccurrenceRef);
        ParentNodeRef = parentNodeRef;
        ParentSemanticPage = Normalize(parentSemanticPage);
        DestinationNodeRef = destinationNodeRef;
        CurrentSliceRef = currentSliceRef;
        AttributedCompletionObservationSequence = attributedCompletionObservationSequence;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Gets the opaque owner context reference.</summary>
    public ContainerObligationContextRef ContextRef { get; }
    /// <summary>Gets the owner-supplied obligation implication.</summary>
    public ContainerObligationContextKind Kind { get; }
    /// <summary>Gets the owner-supplied intended semantic.</summary>
    public string IntendedSemantic { get; }
    /// <summary>Gets the opaque Run reference, when the owner supplied one.</summary>
    public string? RunRef { get; }
    /// <summary>Gets the exact owner-bound observation reference.</summary>
    public string? ObservationRef { get; }
    /// <summary>Gets the exact owner-bound evidence revision.</summary>
    public SemanticEvidenceRevision? EvidenceRevision { get; }
    /// <summary>Gets the exact owner-bound transition occurrence reference.</summary>
    public TransitionOccurrenceRef? TransitionOccurrenceRef { get; }
    /// <summary>Gets the exact owner-bound trigger occurrence reference.</summary>
    public string? TriggerOccurrenceRef { get; }
    /// <summary>Gets the exact owner-bound parent node reference.</summary>
    public ContainerNodeRef? ParentNodeRef { get; }
    /// <summary>Gets the progress scope key supplied by the owner.</summary>
    public string? ParentSemanticPage { get; }
    /// <summary>Gets the exact owner-bound destination node reference.</summary>
    public ContainerNodeRef? DestinationNodeRef { get; }
    /// <summary>Gets the exact owner-bound current Slice reference.</summary>
    public ContainerSliceRef? CurrentSliceRef { get; }
    /// <summary>Gets the exact completion observation attribution, when any.</summary>
    public long? AttributedCompletionObservationSequence { get; }
    /// <summary>Gets whether the owner supplied the complete exact event binding.</summary>
    public bool HasExactEventBinding
        => !string.IsNullOrWhiteSpace(RunRef)
           && !string.IsNullOrWhiteSpace(ObservationRef)
           && EvidenceRevision is not null
           && TransitionOccurrenceRef is not null
           && !string.IsNullOrWhiteSpace(TriggerOccurrenceRef)
           && ParentNodeRef is not null
           && !string.IsNullOrWhiteSpace(ParentSemanticPage)
           && DestinationNodeRef is not null
           && CurrentSliceRef is not null;
}

/// <summary>
/// Immutable semantic correction evidence bound to one current Slow
/// assessment. It carries actual assessment semantics only; it is not a world
/// fact, an obligation mutation, or an execution command.
/// </summary>
public sealed record ContainerSemanticCorrectionFact
{
    /// <summary>Creates a fully assessment-bound correction fact.</summary>
    internal ContainerSemanticCorrectionFact(
        SlowContainerSemanticAssessmentKind assessmentKind,
        string observationRef,
        SemanticEvidenceRevision evidenceRevision,
        ContainerNodeRef nodeRef,
        ContainerNodeRef sourceNodeRef,
        string triggerOccurrenceRef,
        TransitionOccurrenceRef transitionOccurrenceRef,
        string? actualTriggerSemantic,
        string? observedContainerSemantic,
        string? correctedIdentityCandidate,
        string? relationSemantic)
    {
        if (assessmentKind is not (SlowContainerSemanticAssessmentKind.Challenge
            or SlowContainerSemanticAssessmentKind.Correct))
            throw new ArgumentOutOfRangeException(nameof(assessmentKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(observationRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerOccurrenceRef);
        AssessmentKind = assessmentKind;
        ObservationRef = observationRef;
        EvidenceRevision = evidenceRevision;
        NodeRef = nodeRef;
        SourceNodeRef = sourceNodeRef;
        TriggerOccurrenceRef = triggerOccurrenceRef;
        TransitionOccurrenceRef = transitionOccurrenceRef;
        ActualTriggerSemantic = Normalize(actualTriggerSemantic);
        ObservedContainerSemantic = Normalize(observedContainerSemantic);
        CorrectedIdentityCandidate = Normalize(correctedIdentityCandidate);
        RelationSemantic = Normalize(relationSemantic);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Gets the Slow assessment kind that produced this evidence.</summary>
    public SlowContainerSemanticAssessmentKind AssessmentKind { get; }
    /// <summary>Gets the exact observation reference.</summary>
    public string ObservationRef { get; }
    /// <summary>Gets the exact assessment revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the observed destination node reference.</summary>
    public ContainerNodeRef NodeRef { get; }
    /// <summary>Gets the observed source node reference.</summary>
    public ContainerNodeRef SourceNodeRef { get; }
    /// <summary>Gets the exact trigger occurrence reference.</summary>
    public string TriggerOccurrenceRef { get; }
    /// <summary>Gets the exact transition occurrence reference.</summary>
    public TransitionOccurrenceRef TransitionOccurrenceRef { get; }
    /// <summary>Gets Slow's actual trigger semantic, when supplied.</summary>
    public string? ActualTriggerSemantic { get; }
    /// <summary>Gets Slow's observed Container semantic, when supplied.</summary>
    public string? ObservedContainerSemantic { get; }
    /// <summary>Gets Slow's corrected identity candidate, when supplied.</summary>
    public string? CorrectedIdentityCandidate { get; }
    /// <summary>Gets Slow's relation semantic, when supplied.</summary>
    public string? RelationSemantic { get; }
    /// <summary>Gets whether an owning obligation layer must reevaluate this evidence.</summary>
    public bool RequiresOwnerReevaluation => true;
    /// <summary>Gets whether any owner decision effect was applied.</summary>
    public bool HasAppliedObligationMutation => false;
    /// <summary>Gets whether an execution action was emitted.</summary>
    public bool HasAction => false;
    /// <summary>Gets whether a recovery effect was emitted.</summary>
    public bool HasRecovery => false;
    /// <summary>Gets whether a completion effect was emitted.</summary>
    public bool HasCompletion => false;
}

/// <summary>
/// Read-only input for owner-side obligation reevaluation. Pending and
/// observed values are candidates; ownership remains outside this projection.
/// </summary>
public sealed record ContainerObligationReevaluationInput
{
    /// <summary>Creates an immutable owner reevaluation input.</summary>
    public ContainerObligationReevaluationInput(
        ContainerSemanticCorrectionFact correction,
        ContainerObligationContext ownerContext)
    {
        ArgumentNullException.ThrowIfNull(correction);
        ArgumentNullException.ThrowIfNull(ownerContext);
        Correction = correction;
        OwnerContext = ownerContext;
    }

    /// <summary>Gets the exact correction evidence.</summary>
    public ContainerSemanticCorrectionFact Correction { get; }
    /// <summary>Gets the explicit owner context that supplied intended meaning.</summary>
    public ContainerObligationContext OwnerContext { get; }
    /// <summary>Gets the owner-supplied intended item as a pending candidate.</summary>
    public string IntendedPendingCandidate => OwnerContext.IntendedSemantic;
    /// <summary>
    /// Gets the assessment-supplied observed item candidate. Challenge evidence
    /// deliberately has no observed visited candidate.
    /// </summary>
    public string? ObservedActualCandidate
        => Correction.AssessmentKind != SlowContainerSemanticAssessmentKind.Correct
            ? null
            : OwnerContext.Kind == ContainerObligationContextKind.TraversalMisclick
                ? Correction.ActualTriggerSemantic
                : Correction.CorrectedIdentityCandidate ?? Correction.ObservedContainerSemantic;
    /// <summary>Gets whether the owner must reevaluate the obligation.</summary>
    public bool RequiresOwnerReevaluation => true;
    /// <summary>Gets whether this value changed owner-managed obligation state.</summary>
    public bool HasAppliedObligationMutation => false;
    /// <summary>Gets whether this value emitted an execution action.</summary>
    public bool HasAction => false;
    /// <summary>Gets whether this value emitted a recovery effect.</summary>
    public bool HasRecovery => false;
    /// <summary>Gets whether this value emitted a completion effect.</summary>
    public bool HasCompletion => false;
    /// <summary>Gets whether the owner must separately authorize the next decision.</summary>
    public bool RequiresSeparateOwnerAuthorization
        => OwnerContext.Kind == ContainerObligationContextKind.DirectedEntryWrongBranch;
}

/// <summary>Immutable confirmation evidence for one execution-path node.</summary>
public sealed record ContainerPathConfirmation
{
    /// <summary>Creates path confirmation evidence.</summary>
    public ContainerPathConfirmation(
        string observationRef,
        SemanticEvidenceRevision evidenceRevision,
        ContainerNodeRef nodeRef,
        bool isSufficientlyConfirmed,
        bool isCorrectExecutionPath,
        bool isOffPath = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observationRef);
        ObservationRef = observationRef;
        EvidenceRevision = evidenceRevision;
        NodeRef = nodeRef;
        IsSufficientlyConfirmed = isSufficientlyConfirmed;
        IsCorrectExecutionPath = isCorrectExecutionPath;
        IsOffPath = isOffPath;
    }

    /// <summary>Gets the exact observation reference.</summary>
    public string ObservationRef { get; }
    /// <summary>Gets the confirmation revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the confirmed path node reference.</summary>
    public ContainerNodeRef NodeRef { get; }
    /// <summary>Gets whether the node has sufficient confirmation.</summary>
    public bool IsSufficientlyConfirmed { get; }
    /// <summary>Gets whether the node belongs to the correct execution path.</summary>
    public bool IsCorrectExecutionPath { get; }
    /// <summary>Gets whether the node is explicitly off-path.</summary>
    public bool IsOffPath { get; }
}

/// <summary>
/// Immutable, explicitly ordered execution-path confirmation evidence.
/// NEW_SYMBOL_JUSTIFICATION: an ordered path wrapper is required to make
/// checkpoint selection depend on caller-declared execution order rather than
/// silently treating arbitrary enumeration order as runtime authority.
/// </summary>
public sealed record ContainerExecutionPath
{
    /// <summary>Creates an immutable path from confirmations in execution order.</summary>
    public ContainerExecutionPath(IEnumerable<ContainerPathConfirmation> orderedConfirmations)
    {
        ArgumentNullException.ThrowIfNull(orderedConfirmations);
        var confirmations = orderedConfirmations.ToImmutableArray();
        if (confirmations.Any(confirmation => confirmation is null))
            throw new ArgumentException("Execution path cannot contain null confirmation evidence.", nameof(orderedConfirmations));
        Confirmations = confirmations;
    }

    /// <summary>Gets confirmation evidence in caller-declared execution order.</summary>
    public ImmutableArray<ContainerPathConfirmation> Confirmations { get; }
}

/// <summary>Immutable derived proposal for a resumable execution-path node.</summary>
public sealed record ContainerCheckpointProposal
{
    /// <summary>Creates a checkpoint proposal from sufficient path evidence.</summary>
    internal ContainerCheckpointProposal(
        string observationRef,
        SemanticEvidenceRevision evidenceRevision,
        ContainerNodeRef nodeRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observationRef);
        ObservationRef = observationRef;
        EvidenceRevision = evidenceRevision;
        NodeRef = nodeRef;
    }

    /// <summary>Gets the evidence observation that supports the proposal.</summary>
    public string ObservationRef { get; }
    /// <summary>Gets the proposal revision.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the proposed confirmed path node.</summary>
    public ContainerNodeRef NodeRef { get; }
}

/// <summary>Pure projections for correction facts and path proposals.</summary>
public static class ContainerSemanticCorrectionProjector
{
    /// <summary>
    /// Produces current correction evidence only from a current, available Slow
    /// challenge or correction with all required occurrence references.
    /// </summary>
    /// <param name="consumption">Slow derived consumption view.</param>
    /// <returns>Assessment-bound correction evidence, or null when unavailable or incomplete.</returns>
    public static ContainerSemanticCorrectionFact? TryCreateCorrection(
        SlowContainerSemanticConsumption consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        if (consumption.Availability != SlowContainerSemanticAvailability.Available
            || !consumption.IsCurrent
            || consumption.Assessment is not { } assessment
            || assessment.Kind is not (SlowContainerSemanticAssessmentKind.Challenge
                or SlowContainerSemanticAssessmentKind.Correct)
            || assessment.NodeRef is not { } nodeRef
            || assessment.SourceNodeRef is not { } sourceNodeRef
            || string.IsNullOrWhiteSpace(assessment.TriggerOccurrenceRef)
            || assessment.TransitionOccurrenceRef is not { } transitionOccurrenceRef
            || (assessment.Kind == SlowContainerSemanticAssessmentKind.Correct
                && string.IsNullOrWhiteSpace(assessment.CorrectedIdentityCandidate)
                && string.IsNullOrWhiteSpace(assessment.ContainerSemantic)
                && string.IsNullOrWhiteSpace(assessment.TriggerSemantic)))
        {
            return null;
        }

        return new ContainerSemanticCorrectionFact(
            assessment.Kind,
            assessment.ObservationRef,
            assessment.EvidenceRevision,
            nodeRef,
            sourceNodeRef,
            assessment.TriggerOccurrenceRef,
            transitionOccurrenceRef,
            assessment.TriggerSemantic,
            assessment.ContainerSemantic,
            assessment.CorrectedIdentityCandidate,
            assessment.RelationSemantic);
    }

    /// <summary>Projects assessment evidence and owner context into read-only obligation input.</summary>
    public static ContainerObligationReevaluationInput? ProjectObligationInput(
        ContainerSemanticCorrectionFact? correction,
        ContainerObligationContext? ownerContext)
    {
        if (correction is null || ownerContext is null)
            return null;
        return new ContainerObligationReevaluationInput(correction, ownerContext);
    }

    /// <summary>
    /// Selects the last sufficiently confirmed node in an explicitly ordered
    /// current execution path for the exact current revision. No eligible node
    /// produces no proposal.
    /// </summary>
    /// <param name="path">Caller-declared execution-path evidence in order.</param>
    /// <param name="currentEvidenceRevision">The currently accepted revision.</param>
    /// <returns>A derived proposal, or null when no path node qualifies.</returns>
    public static ContainerCheckpointProposal? ProjectCheckpoint(
        ContainerExecutionPath path,
        SemanticEvidenceRevision currentEvidenceRevision)
    {
        ArgumentNullException.ThrowIfNull(path);
        ContainerPathConfirmation? selected = null;
        foreach (var confirmation in path.Confirmations)
        {
            ArgumentNullException.ThrowIfNull(confirmation);
            if (!confirmation.IsSufficientlyConfirmed
                || !confirmation.IsCorrectExecutionPath
                || confirmation.IsOffPath
                || confirmation.EvidenceRevision != currentEvidenceRevision)
            {
                continue;
            }

            selected = confirmation;
        }

        return selected is null
            ? null
            : new ContainerCheckpointProposal(
                selected.ObservationRef,
                selected.EvidenceRevision,
                selected.NodeRef);
    }
}
