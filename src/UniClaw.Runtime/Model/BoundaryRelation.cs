namespace UniClaw.Runtime.Model;

/// <summary>
/// EXTERNAL BOUNDARY relation (EBD). Represents an AUTHORIZED crossing from a
/// Runtime-owned foreground into a DIFFERENT (external) foreground package.
/// The Parent keeps this as a child RELATION (RelationKind=ExternalBoundary)
/// but the external destination NEVER becomes a RecursiveChild — it receives no
/// recursive traversal authority, is not put in RequiredChildren, and is not
/// inventoried. Minimal immutable provenance/evidence facts only (no full
/// Observation snapshot embedded).
/// </summary>
public sealed record BoundaryRelation
{
    /// <summary>Creates an authorized external-boundary relation.</summary>
    /// <param name="parentContainerIdentity">Identity of the owning Runtime container.</param>
    /// <param name="sourceOccurrenceReference">Reference to the source occurrence.</param>
    /// <param name="preActionForeground">Foreground before the crossing.</param>
    /// <param name="externalForeground">External foreground reached by the crossing.</param>
    /// <param name="expectedReturnParent">Foreground expected after returning.</param>
    /// <param name="sourceObservationSequence">Observation sequence that supplied the evidence.</param>
    public BoundaryRelation(
        string parentContainerIdentity,
        string sourceOccurrenceReference,
        string preActionForeground,
        string externalForeground,
        string expectedReturnParent,
        long sourceObservationSequence)
    {
        ParentContainerIdentity = parentContainerIdentity;
        SourceOccurrenceReference = sourceOccurrenceReference;
        PreActionForeground = preActionForeground;
        ExternalForeground = externalForeground;
        ExpectedReturnParent = expectedReturnParent;
        SourceObservationSequence = sourceObservationSequence;
    }

    /// <summary>Gets the owning Runtime container identity.</summary>
    public string ParentContainerIdentity { get; }
    /// <summary>Gets the source occurrence reference.</summary>
    public string SourceOccurrenceReference { get; }
    /// <summary>Gets the pre-action foreground.</summary>
    public string PreActionForeground { get; }
    /// <summary>Gets the external foreground.</summary>
    public string ExternalForeground { get; }
    /// <summary>Gets the expected return parent.</summary>
    public string ExpectedReturnParent { get; }
    /// <summary>Gets the source observation sequence.</summary>
    public long SourceObservationSequence { get; }

    /// <summary>Stable relation kind identifier.</summary>
    public const string RelationKind = "ExternalBoundary";
}

/// <summary>Pending external-boundary obligation: an authorized crossing whose
/// return disposition has NOT yet been verified.</summary>
public sealed record BoundaryObligation
{
    /// <summary>Creates a pending obligation for the supplied relation.</summary>
    /// <param name="relation">The authorized boundary relation.</param>
    public BoundaryObligation(BoundaryRelation relation)
    {
        Relation = relation;
        State = BoundaryObligationState.Pending;
    }
    /// <summary>Gets the relation awaiting disposition.</summary>
    public BoundaryRelation Relation { get; }
    /// <summary>Gets the required verified disposition.</summary>
    public string RequiredDisposition => "RETURNED_TO_PARENT";
    /// <summary>Gets the current obligation state.</summary>
    public BoundaryObligationState State { get; init; } = BoundaryObligationState.Pending;
    /// <summary>Returns a copy marked as verified.</summary>
    public BoundaryObligation WithVerified() => new(Relation) { State = BoundaryObligationState.Verified };
}

/// <summary>A VERIFIED external-boundary disposition. Written ONLY from fresh
/// world evidence (exact-parent return + parent continuity + parent frozen-epoch
/// consistency). The SystemBack dispatch receipt is NEVER the truth.</summary>
public sealed record VerifiedBoundaryDisposition
{
    /// <summary>Creates a verified external-boundary disposition.</summary>
    /// <param name="relation">The original boundary relation.</param>
    /// <param name="returnedParentIdentity">Identity of the verified return parent.</param>
    /// <param name="evidenceSequence">Observation sequence supplying the evidence.</param>
    public VerifiedBoundaryDisposition(
        BoundaryRelation relation, string returnedParentIdentity, long evidenceSequence)
    {
        Relation = relation;
        ReturnedParentIdentity = returnedParentIdentity;
        EvidenceSequence = evidenceSequence;
    }
    /// <summary>Gets the original boundary relation.</summary>
    public BoundaryRelation Relation { get; }
    /// <summary>Gets the stable disposition identifier.</summary>
    public string Disposition => "RETURNED_TO_PARENT";
    /// <summary>Gets the verified returned-parent identity.</summary>
    public string ReturnedParentIdentity { get; }
    /// <summary>Gets the evidence observation sequence.</summary>
    public long EvidenceSequence { get; }
}

/// <summary>State of an external-boundary return obligation.</summary>
public enum BoundaryObligationState
{
    /// <summary>Return has not yet been verified.</summary>
    Pending = 0,
    /// <summary>Return has been verified from fresh world evidence.</summary>
    Verified = 1,
}
