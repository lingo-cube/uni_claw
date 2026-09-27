using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;

namespace UniClaw.Kernel.Perception.Fusion;

public enum FusionDisposition { Supported, Conflicted, Unknown, Unsupported, Unaligned, Malformed }
public enum TemporalAlignmentDisposition { Aligned, AlignedWithCoverageLimit, Unaligned, TemporalUnknown }
public enum AssociationDisposition { Unique, ManyToOne, OneToMany, Ambiguous, Unassociated }
public enum CoverageDisposition { CompleteWithinDeclaredSurface, Partial, Unknown, SourceUnavailable }
public enum ConflictDisposition { None, OverruledSource, Unresolved }
public enum FusionSourceKind { Hierarchy, Visual, Ocr }
public enum LineageDisposition { Valid, MalformedLineage }

public sealed record FusionCapture(string CaptureId, string SessionCorrelation, string? ObservationCycleId,
    DateTimeOffset CaptureTimestamp, string? FrameId = null, bool KnownMutationMarker = false,
    CoordinateSpace? Space = null)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(CaptureId) && !string.IsNullOrWhiteSpace(SessionCorrelation);
}

public sealed record FusionSourceEvidence(string EvidenceId, FusionSourceKind SourceKind, string Subject, string Field,
    string Value, FusionCapture Capture, CoverageDisposition Coverage = CoverageDisposition.CompleteWithinDeclaredSurface,
    bool CapabilityDeclared = true, bool ValueObserved = true)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(EvidenceId) && !string.IsNullOrWhiteSpace(Subject)
        && !string.IsNullOrWhiteSpace(Field) && Capture is { IsValid: true };
}

public sealed record EvidenceNode(string EvidenceId, IReadOnlyList<string> ParentEvidenceIds,
    IReadOnlyList<string>? DeclaredTransitiveEvidenceBasis = null, bool IsSourceEvidence = false);

public sealed record TemporalAlignmentResult(TemporalAlignmentDisposition Disposition, string Reason,
    IReadOnlyList<string> CaptureIds)
{
    public bool EligibleForJointFusion => Disposition is TemporalAlignmentDisposition.Aligned
        or TemporalAlignmentDisposition.AlignedWithCoverageLimit;
}

public sealed record AssociationCandidate(string HierarchyOccurrenceId, IReadOnlyList<string> VisualOccurrenceIds,
    IReadOnlyList<string> OcrTokenIds, AssociationDisposition Disposition,
    IReadOnlyDictionary<string, string>? Features = null);

public sealed record FieldAuthorityDecision(FusionDisposition Disposition, FusionSourceKind? AuthoritySource,
    ConflictDisposition Conflict, string Reason);

public sealed record LineageValidationResult(LineageDisposition Disposition, IReadOnlySet<string> TransitiveEvidenceBasis,
    string? Error = null)
{
    public bool IsValid => Disposition == LineageDisposition.Valid;
}

public sealed record FusionRequest(string Subject, string Field, IReadOnlyList<FusionSourceEvidence> Sources,
    AssociationCandidate Association, TimeSpan AlignmentWindow, IReadOnlyDictionary<string, EvidenceNode>? EvidenceGraph = null,
    string FusionRule = "per-011-authority-v1", string FusionRuleVersion = "1");

public sealed record DerivedObservationProposal(ObservationClaim Claim, FusionDisposition Disposition,
    TemporalAlignmentResult Alignment, AssociationDisposition Association, CoverageDisposition Coverage,
    IReadOnlyList<string> SourceEvidenceIds, IReadOnlyList<string> DerivedFromEvidenceIds,
    IReadOnlySet<string> TransitiveEvidenceBasis, string FusionRule, string FusionRuleVersion,
    FusionSourceKind? AuthoritySource, ConflictDisposition Conflict, IReadOnlyList<string> Limitations, bool IsAdmissible)
{
    public DateTimeOffset CaptureTimestamp { get; init; }

    public ObservationProposal ToObservationProposal(ObservationContext context = ObservationContext.External)
    {
        if (!IsAdmissible || Disposition is FusionDisposition.Malformed or FusionDisposition.Unaligned)
            throw new InvalidOperationException("fusion proposal is not admissible for P2 projection");
        var lineage = new[] { $"fusion:{FusionRule}:{FusionRuleVersion}", "typed-lineage:v1" };
        return new ObservationProposal(Claim, IngressKind.Observation, context,
            new Provenance("per-011.fusion",
                CaptureTimestamp == default ? DateTimeOffset.UnixEpoch.AddTicks(1) : CaptureTimestamp,
                $"scope:{Claim.Subject}", lineage))
        {
            DerivedFromEvidenceIds = DerivedFromEvidenceIds,
            TransitiveEvidenceBasis = TransitiveEvidenceBasis,
            FusionRule = FusionRule,
            FusionVersion = FusionRuleVersion,
        };
    }
}
