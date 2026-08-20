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

    public string ParentContainerIdentity { get; }
    public string SourceOccurrenceReference { get; }
    public string PreActionForeground { get; }
    public string ExternalForeground { get; }
    public string ExpectedReturnParent { get; }
    public long SourceObservationSequence { get; }

    public const string RelationKind = "ExternalBoundary";
}

/// <summary>Pending external-boundary obligation: an authorized crossing whose
/// return disposition has NOT yet been verified.</summary>
public sealed record BoundaryObligation
{
    public BoundaryObligation(BoundaryRelation relation)
    {
        Relation = relation;
        State = BoundaryObligationState.Pending;
    }
    public BoundaryRelation Relation { get; }
    public string RequiredDisposition => "RETURNED_TO_PARENT";
    public BoundaryObligationState State { get; init; } = BoundaryObligationState.Pending;
    public BoundaryObligation WithVerified() => new(Relation) { State = BoundaryObligationState.Verified };
}

/// <summary>A VERIFIED external-boundary disposition. Written ONLY from fresh
/// world evidence (exact-parent return + parent continuity + parent frozen-epoch
/// consistency). The SystemBack dispatch receipt is NEVER the truth.</summary>
public sealed record VerifiedBoundaryDisposition
{
    public VerifiedBoundaryDisposition(
        BoundaryRelation relation, string returnedParentIdentity, long evidenceSequence)
    {
        Relation = relation;
        ReturnedParentIdentity = returnedParentIdentity;
        EvidenceSequence = evidenceSequence;
    }
    public BoundaryRelation Relation { get; }
    public string Disposition => "RETURNED_TO_PARENT";
    public string ReturnedParentIdentity { get; }
    public long EvidenceSequence { get; }
}

public enum BoundaryObligationState
{
    Pending = 0,
    Verified = 1,
}
