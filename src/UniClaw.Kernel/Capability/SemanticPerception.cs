using System.Collections.Frozen;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.Capability;

/// <summary>Non-authoritative semantic support; never an effect authorization.</summary>
public enum PerceptionDisposition { Supported, Partial, Unknown, Conflicted, Unsupported, Unaligned, Unavailable }
/// <summary>Observed relationship, separate from the requested destination.</summary>
public enum SemanticPageRelation { Unknown, SamePage, DifferentPage }
/// <summary>Intent comparison; not an observation or a successful operation receipt.</summary>
public enum SemanticExpectedMatch { Unknown, Match, Mismatch, Conflicted }

/// <summary>A normalized capture-local item, not a canonical World identity.</summary>
public sealed record SemanticLayoutItem(string Id, string Kind, string? Text, UiBounds Bounds, double? Confidence = null);

/// <summary>Bounded normalized observations; page labels are caller-supplied previous-page context only, forbidden on Current.</summary>
public sealed record SemanticObservationContext(
    string CaptureId, string ObservationCycleId, string SessionCorrelation,
    DateTimeOffset CaptureTime, CoordinateSpace Space,
    IReadOnlyList<SemanticLayoutItem> Items, IReadOnlyList<string> EvidenceIds,
    bool IsFresh = true, string? RevisionId = null, string? PageName = null,
    string? PagePurpose = null, string? PageKey = null, IReadOnlySet<string>? SourceLeafEvidenceIds = null)
{
    public IReadOnlyList<SemanticLayoutItem> Items { get; } = Array.AsReadOnly((Items ?? []).ToArray());
    public IReadOnlyList<string> EvidenceIds { get; } = Array.AsReadOnly((EvidenceIds ?? []).ToArray());
    public IReadOnlySet<string> SourceLeafEvidenceIds { get; } =
        (SourceLeafEvidenceIds ?? (EvidenceIds ?? []).ToHashSet(StringComparer.Ordinal)).ToFrozenSet(StringComparer.Ordinal);
}

/// <summary>Caller-owned semantic catalogue entry; Key is a class, never a ContainerId.</summary>
public sealed record SemanticPageCandidate(string Key, string Name, string Purpose);

/// <summary>Recorded operation and its explicitly sourced element description.</summary>
public sealed record SemanticActionContext(
    string Kind, string ElementType, string ElementText, string SourceCaptureId,
    string DescriptionSource, string? SourceOccurrenceId = null,
    int? X = null, int? Y = null, string? OperationId = null, string? ReceiptId = null,
    string? ExecutionStatus = null);

/// <summary>Caller intent only; it must never enter P22 or become an observation value.</summary>
public sealed record SemanticEvaluationIntent(string ExpectedPageKey, string Description);

/// <summary>Semantic Perception executable payload; distinct from the frozen legacy Slow request.</summary>
public sealed record SemanticPerceptionRequest(
    string RequestId, CapabilityCorrelation Correlation, SemanticObservationContext Current,
    IReadOnlyList<SemanticPageCandidate> Candidates, SemanticObservationContext? Previous = null,
    SemanticActionContext? Action = null, SemanticEvaluationIntent? Intent = null, TimeSpan? Budget = null)
{
    public IReadOnlyList<SemanticPageCandidate> Candidates { get; } = Array.AsReadOnly((Candidates ?? []).ToArray());
}

/// <summary>Shared assessment vocabulary; confidence is diagnostic, not calibrated trust.</summary>
public sealed record PerceptionAssessment(
    PerceptionDisposition Disposition, string Coverage, string Uncertainty,
    string AssociationQuality, string SourcePath, double? ProviderConfidence = null,
    string? Diagnostic = null);

/// <summary>Meaning proposed from supplied observations; World remains the identity owner.</summary>
public sealed record SemanticObservationProposal(
    string PageKey, string PageName, string PagePurpose, string? ObservedTitle,
    IReadOnlyList<string> BasisEvidenceIds, PerceptionAssessment Assessment)
{
    public IReadOnlyList<string> BasisEvidenceIds { get; } = Array.AsReadOnly((BasisEvidenceIds ?? []).ToArray());
}

/// <summary>Normalized value projected through P2; deliberately excludes expectation and operation success.</summary>
public sealed record SemanticPageClaim(
    string PageKey, string PageName, string PagePurpose, string? ObservedTitle,
    string CaptureId, string ObservationCycleId, string SessionCorrelation);

/// <summary>Execution, observation, observed change and intent comparison are independent dimensions.</summary>
public sealed record SemanticPerceptionResult(
    string RequestId, string CaptureId, string ObservationCycleId, CapabilityStatus Status,
    SemanticObservationProposal? Observation, SemanticPageRelation Relation,
    SemanticExpectedMatch ExpectedMatch, PerceptionAssessment Assessment)
{
    /// <summary>Construct an observation from a caller catalogue and an actually supplied title.</summary>
    public static SemanticPerceptionResult Observed(SemanticPerceptionRequest request, string pageKey,
        string? observedTitle = null, SemanticPageRelation relation = SemanticPageRelation.Unknown,
        SemanticExpectedMatch expectedMatch = SemanticExpectedMatch.Unknown,
        double? confidence = null, string source = "semantic-realization")
    {
        var page = request.Candidates.Single(c => c.Key == pageKey);
        var basis = request.Current.EvidenceIds.Concat(request.Previous?.EvidenceIds ?? [])
            .Distinct(StringComparer.Ordinal).ToArray();
        var assessment = new PerceptionAssessment(PerceptionDisposition.Supported,
            "CompleteWithinDeclaredSurface", "Bounded", "Unassociated", source, confidence);
        return new(request.RequestId, request.Current.CaptureId, request.Current.ObservationCycleId,
            CapabilityStatus.Completed, new(page.Key, page.Name, page.Purpose, observedTitle, basis, assessment),
            relation, expectedMatch, assessment);
    }

    /// <summary>No observation is produced for unavailable/invalid/unknown input or execution.</summary>
    public static SemanticPerceptionResult Empty(SemanticPerceptionRequest request,
        CapabilityStatus status, PerceptionDisposition disposition, string diagnostic) =>
        new(request.RequestId, request.Current?.CaptureId ?? "", request.Current?.ObservationCycleId ?? "",
            status, null, SemanticPageRelation.Unknown, SemanticExpectedMatch.Unknown,
            new(disposition, "Unknown", "Material", "Unassociated", "semantic-perception", Diagnostic: diagnostic));
}

/// <summary>Provider-neutral supply seam; provider adapters do not implement the L1 capability.</summary>
public delegate ValueTask<SemanticPerceptionResult> SemanticPerceptionExecutor(
    SemanticPerceptionRequest request, CancellationToken cancellationToken);
