using System.Collections.Immutable;
using System.Text;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Deterministic embedding provider (V1 / test implementation).
///
/// Maps the semantic query representation into a fixed-dimension bag-of-tokens
/// vector using a stable token hash (FNV-1a over UTF-8 bytes — NOT
/// string.GetHashCode, which is randomized per process). It exists so the
/// Embedding boundary is independently composable and testable before any real
/// model (BGE-small etc.) is wired. Pure representation: no threshold, no
/// prototype lookup, no acceptance.
/// </summary>
public sealed class DeterministicSemanticEmbeddingProvider : IEmbeddingProvider
{
    /// <summary>Identity of this deterministic embedding (profile-bookkeeping).</summary>
    public static EmbeddingModelIdentity ModelIdentity { get; } = new(
        "deterministic-v1", "v1", 64, "in-process", "none");

    private readonly int _dimension;

    /// <summary>Creates the provider (dimension default 64).</summary>
    public DeterministicSemanticEmbeddingProvider(int dimension = 64)
    {
        _dimension = dimension >= 8 ? dimension : 8;
    }

    /// <inheritdoc />
    public EmbeddingVector Embed(ContainerSemanticQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var counts = new float[_dimension];
        var tokenCount = 0;
        foreach (var token in Tokens(query))
        {
            counts[StableHash(token) % _dimension] += 1f;
            tokenCount++;
        }

        if (tokenCount == 0)
        {
            return new EmbeddingVector(ImmutableArray.Create(counts), ModelIdentity);
        }

        // L2-normalize so cosine retrieval is comparable across inputs.
        var norm = 0d;
        foreach (var value in counts)
        {
            norm += value * value;
        }

        norm = Math.Sqrt(norm);
        if (norm > 0d)
        {
            for (var i = 0; i < counts.Length; i++)
            {
                counts[i] = (float)(counts[i] / norm);
            }
        }

        return new EmbeddingVector(ImmutableArray.Create(counts), ModelIdentity);
    }

    private static IEnumerable<string> Tokens(ContainerSemanticQuery query)
    {
        foreach (var text in query.TextFragments)
        {
            yield return text.ToLowerInvariant();
        }

        foreach (var type in query.ElementTypes)
        {
            yield return $"type:{type.ToLowerInvariant()}";
        }

        foreach (var feature in query.StructuralFeatures)
        {
            yield return feature.ToLowerInvariant();
        }
    }

    /// <summary>FNV-1a 32-bit stable hash (deterministic across processes).</summary>
    private static uint StableHash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        uint hash = 2166136261;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 16777619;
        }

        return hash;
    }
}