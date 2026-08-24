using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Corpus;

/// <summary>
/// Corpus catalog helper for category-based benchmark integration. This is a
/// management facility only; it never affects Runtime decisions.
/// </summary>
public static class SemanticCorpusCatalog
{
    /// <summary>Filters corpora by category.</summary>
    public static ImmutableArray<SemanticCorpus> FilterByCategory(
        IEnumerable<SemanticCorpus> corpora,
        SemanticCorpusCategory category)
    {
        ArgumentNullException.ThrowIfNull(corpora);
        return corpora.Where(c => c.Category == category).ToImmutableArray();
    }
}