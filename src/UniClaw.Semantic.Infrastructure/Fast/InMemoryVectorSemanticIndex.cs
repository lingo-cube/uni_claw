using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// A read-only, validated semantic pattern used by the in-memory index.
/// This is NOT a production Memory/Vector store; it is a minimal fixture-like
/// index to prove the retrieval pipeline. No Runtime write path is exposed.
/// </summary>
public sealed record SemanticPattern(
    string IdentityCandidate,
    string PatternReference,
    ImmutableArray<string> TextFragments,
    ImmutableArray<string> ElementTypes,
    ImmutableArray<string> StructuralFeatures);

/// <summary>
/// Minimal in-memory, read-only <see cref="IVectorSemanticIndex"/> implementation.
/// It computes a simple text/structure overlap similarity and returns the best
/// candidate when the score meets the threshold. It is used to validate the Fast
/// Semantic retrieval pipeline, not as production Vector Memory.
/// </summary>
public sealed class InMemoryVectorSemanticIndex : IVectorSemanticIndex
{
    private readonly ImmutableArray<SemanticPattern> _patterns;
    private readonly double _matchThreshold;

    /// <summary>Creates an in-memory vector semantic index.</summary>
    public InMemoryVectorSemanticIndex(
        ImmutableArray<SemanticPattern> patterns,
        double matchThreshold = 0.3)
    {
        if (matchThreshold is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchThreshold), matchThreshold, "Threshold must be within [0, 1].");
        }
        _patterns = patterns.IsDefault ? ImmutableArray<SemanticPattern>.Empty : patterns;
        _matchThreshold = matchThreshold;
    }

    /// <inheritdoc />
    public SemanticCandidate? Retrieve(ContainerSemanticQuery query)
    {
        SemanticCandidate? best = null;

        foreach (var pattern in _patterns)
        {
            var score = Score(query, pattern);
            if (score <= 0d)
            {
                continue;
            }

            if (best is null || score > best.SimilarityScore)
            {
                best = new SemanticCandidate(
                    pattern.IdentityCandidate,
                    score,
                    pattern.PatternReference);
            }
        }

        return best is not null && best.SimilarityScore >= _matchThreshold ? best : null;
    }

    private static double Score(ContainerSemanticQuery query, SemanticPattern pattern)
    {
        var total = query.TextFragments.Length
                    + query.ElementTypes.Length
                    + query.StructuralFeatures.Length;
        if (total == 0)
        {
            return 0d;
        }

        var matches = 0;
        foreach (var text in query.TextFragments)
        {
            if (pattern.TextFragments.Contains(text, StringComparer.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        foreach (var type in query.ElementTypes)
        {
            if (pattern.ElementTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        foreach (var feature in query.StructuralFeatures)
        {
            if (pattern.StructuralFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        return (double)matches / total;
    }
}
