using UniClaw.Kernel.Capability;

namespace UniClaw.Kernel.World;

/// <summary>A source-bound historical fragment. Its occurrences are never current delivery handles.</summary>
public sealed record WorldPageObservation(
    string SourceRevisionId, string CaptureEvidenceId, string? IdentityEvidenceId,
    PageCaptureObservation Capture, string? ContainerId,
    IReadOnlyList<OccurrenceFact> Occurrences, bool IsCurrentCapture, string? VisitSourceRef);

/// <summary>Read-only reference, not a current semantic assertion or an authorization.</summary>
public sealed record WorldSemanticReference(
    string EvidenceId, string CaptureEvidenceId, SemanticPageClaim Meaning,
    bool IsHistorical, bool Conflicted)
{
    public string? VerificationEvidenceId { get; init; }
}

/// <summary>World-derived, revision-bound supply for page presentation and Agent context.</summary>
public sealed record WorldPageView(
    string SourceRevisionId, string ContainerId,
    IReadOnlyList<WorldPageObservation> Observations,
    IReadOnlyList<WorldSemanticReference> SemanticReferences,
    IReadOnlyList<ViewportTransition> ViewportTransitions,
    IReadOnlyList<string> Unknown, string ContentDigest)
{
    public IReadOnlyList<PageCorrectionDecision> Corrections { get; init; } =
        System.Collections.Immutable.ImmutableArray<PageCorrectionDecision>.Empty;
    public IReadOnlyList<UniClaw.Kernel.World.UiRealization.ContainerRelation> ContextRelations { get; init; } =
        System.Collections.Immutable.ImmutableArray<UniClaw.Kernel.World.UiRealization.ContainerRelation>.Empty;
    public IReadOnlyList<WorldSemanticKnowledge> CurrentSemantics { get; init; } =
        System.Collections.Immutable.ImmutableArray<WorldSemanticKnowledge>.Empty;
    public WorldAccumulatedContent? AccumulatedContent { get; init; }
}
