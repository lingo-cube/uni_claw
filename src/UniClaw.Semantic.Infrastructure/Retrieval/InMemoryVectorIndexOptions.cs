using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Options for creating an exact in-memory vector index (retrieval only).
/// Contains NO prototype data (prototypes live in
/// <see cref="IContainerIdentityPrototypeStore"/>) and NO acceptance threshold
/// (acceptance lives in <see cref="IContainerIdentityCandidatePolicy"/>).
/// </summary>
public sealed record InMemoryVectorIndexOptions
{
    /// <summary>Cap on returned candidates (0 = return all ranked).</summary>
    public int MaxResults { get; init; } = 0;
}