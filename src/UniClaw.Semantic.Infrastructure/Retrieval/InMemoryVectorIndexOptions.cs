using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>Options for creating an InMemory vector index.</summary>
public sealed record InMemoryVectorIndexOptions
{
    /// <summary>Seed patterns loaded into the read-only InMemory index.</summary>
    public ImmutableArray<SemanticPattern> Patterns { get; init; }
        = ImmutableArray<SemanticPattern>.Empty;

    /// <summary>Similarity threshold for returning a candidate. Default 0.3.</summary>
    public double MatchThreshold { get; init; } = 0.3;
}