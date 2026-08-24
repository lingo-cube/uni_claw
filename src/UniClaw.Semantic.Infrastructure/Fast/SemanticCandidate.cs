namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// A vector retrieval candidate. It is a semantic pattern match, NOT a Fact and
/// NOT a decision. The Runtime decides whether to use the candidate.
/// </summary>
public sealed record SemanticCandidate
{
    /// <summary>The identity candidate, e.g. "DeveloperOptions".</summary>
    public string IdentityCandidate { get; }

    /// <summary>Similarity score in [0,1].</summary>
    public double SimilarityScore { get; }

    /// <summary>A stable pattern reference for the matched vector pattern.</summary>
    public string PatternReference { get; }

    /// <summary>Creates a semantic candidate.</summary>
    public SemanticCandidate(string identityCandidate, double similarityScore, string patternReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityCandidate);
        ArgumentException.ThrowIfNullOrWhiteSpace(patternReference);
        if (similarityScore is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(similarityScore), similarityScore, "Similarity must be within [0, 1].");
        }
        IdentityCandidate = identityCandidate;
        SimilarityScore = similarityScore;
        PatternReference = patternReference;
    }
}
