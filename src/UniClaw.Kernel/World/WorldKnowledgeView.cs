namespace UniClaw.Kernel.World;

public enum WorldKnowledgeGapKind { Identity, Attribution, RelationBasis, ObservationCoverage }

/// <summary>Missing belief support, independent of conflict and without execution authority.</summary>
public sealed record WorldKnowledgeGap(string GapId, WorldKnowledgeGapKind Kind, string ObjectRef,
    string Scope, string Reason, string SourceRevisionId, IReadOnlyList<string> EvidenceIds,
    bool Conflicted, string CauseRef);

public sealed record WorldKnowledgeView(string? SourceRevisionId, string CollectionStatus,
    IReadOnlyList<WorldKnowledgeGap> Gaps)
{
    public IReadOnlyList<WorldSemanticReference> SemanticReferences { get; init; } =
        System.Collections.Immutable.ImmutableArray<WorldSemanticReference>.Empty;
    public IReadOnlyList<WorldSemanticKnowledge> CurrentSemantics { get; init; } =
        System.Collections.Immutable.ImmutableArray<WorldSemanticKnowledge>.Empty;
    public IReadOnlyList<PageCorrectionDecision> Corrections { get; init; } =
        System.Collections.Immutable.ImmutableArray<PageCorrectionDecision>.Empty;
}
