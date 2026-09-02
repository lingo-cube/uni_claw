using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Inputs available to the Candidate Policy. The policy may use any pipeline
/// internal information: ranked candidates, scores, margin, element types
/// (structural compatibility), text fragments, structural signals, previous
/// verified identity, and evidence sufficiency. It never sees Runtime belief
/// and never mutates world state.
///
/// The evidence fields (text fragments / structural signals / element count)
/// were added by SEMANTIC_SAFETY_HARDENING_APPLY to support
/// evidence-sufficiency evaluation (generic vs identity-discriminative
/// evidence). They are defaulted so existing constructions remain valid.
/// </summary>
public sealed record CandidateEvaluationContext(
    IReadOnlyList<SemanticCandidate> RankedCandidates,
    IReadOnlyDictionary<string, ContainerIdentityPrototype> PrototypesById,
    string? PreviousVerifiedIdentity,
    ImmutableArray<string> ObservedElementTypes,
    int ObservedTextTokenCount,
    bool HasAnyEvidence,
    ImmutableArray<string> ObservedTextFragments = default,
    ImmutableArray<string> ObservedStructuralFeatures = default,
    int ObservedElementCount = 0)
{
    /// <summary>Non-empty observed text fragments, in element order.</summary>
    public ImmutableArray<string> TextFragments =>
        ObservedTextFragments.IsDefault ? ImmutableArray<string>.Empty : ObservedTextFragments;

    /// <summary>Observed structural signals (type:/switch: markers), in element order.</summary>
    public ImmutableArray<string> StructuralSignals =>
        ObservedStructuralFeatures.IsDefault ? ImmutableArray<string>.Empty : ObservedStructuralFeatures;

    /// <summary>Number of observed elements.</summary>
    public int ElementCount => ObservedElementCount;
}