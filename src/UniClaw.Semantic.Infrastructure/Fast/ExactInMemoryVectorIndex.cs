using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Exact in-memory vector retrieval backend (truly vector → nearest vectors).
///
/// Indexes prototype vectors (derived from the <see cref="IContainerIdentityPrototypeStore"/>
/// via the embedding provider — the store remains the owner of prototypes) and
/// answers cosine nearest-candidate ranking for a query vector. Contains NO
/// acceptance threshold and no policy. A zero/empty result set or a low score is
/// the Candidate Policy's problem.
/// </summary>
public sealed class ExactInMemoryVectorIndex : IVectorSemanticIndex
{
    private readonly IReadOnlyList<(ContainerIdentityPrototype Prototype, ImmutableArray<float> Vector)> _indexed;
    private readonly int _maxResults;

    /// <summary>Creates an exact vector index from prototype vectors.</summary>
    /// <param name="prototypes">Prototype store (owner of identity representation).</param>
    /// <param name="embeddingProvider">Embedding provider used to derive prototype vectors.</param>
    /// <param name="maxResults">Cap on returned candidates (0 = all).</param>
    public ExactInMemoryVectorIndex(
        IContainerIdentityPrototypeStore prototypes,
        IEmbeddingProvider embeddingProvider,
        int maxResults = 0)
    {
        ArgumentNullException.ThrowIfNull(prototypes);
        ArgumentNullException.ThrowIfNull(embeddingProvider);
        _maxResults = Math.Max(0, maxResults);

        var builder = ImmutableArray.CreateBuilder<(ContainerIdentityPrototype, ImmutableArray<float>)>();
        foreach (var prototype in prototypes.All())
        {
            var query = new ContainerSemanticQuery(
                ImmutableArray<UniClaw.Runtime.Model.ObservedElement>.Empty,
                prototype.ElementTypes,
                prototype.TextFragments,
                prototype.StructuralFeatures);
            var vector = embeddingProvider.Embed(query);
            builder.Add((prototype, vector.Values));
        }

        _indexed = builder.ToImmutable();
    }

    /// <inheritdoc />
    public IReadOnlyList<SemanticCandidate> Retrieve(EmbeddingVector queryVector)
    {
        ArgumentNullException.ThrowIfNull(queryVector);
        if (_indexed.Count == 0)
        {
            return Array.Empty<SemanticCandidate>();
        }

        var queryNorm = Norm(queryVector.Values);
        var scores = new List<SemanticCandidate>(_indexed.Count);
        foreach (var (prototype, vector) in _indexed)
        {
            var cosine = Cosine(queryVector.Values, queryNorm, vector);
            if (cosine <= 0d)
            {
                continue;
            }

            scores.Add(new SemanticCandidate(
                prototype.IdentityCandidate,
                cosine,
                prototype.PrototypeId));
        }

        scores.Sort((a, b) => b.SimilarityScore.CompareTo(a.SimilarityScore));
        return _maxResults > 0 ? scores.Take(_maxResults).ToArray() : scores;
    }

    private static double Norm(ImmutableArray<float> values)
    {
        var sum = 0d;
        foreach (var value in values)
        {
            sum += value * value;
        }

        return Math.Sqrt(sum);
    }

    private static double Cosine(ImmutableArray<float> a, double aNorm, ImmutableArray<float> b)
    {
        if (a.Length != b.Length || aNorm <= 0d)
        {
            return 0d;
        }

        var dot = 0d;
        var bNorm = 0d;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            bNorm += b[i] * b[i];
        }

        bNorm = Math.Sqrt(bNorm);
        if (bNorm <= 0d)
        {
            return 0d;
        }

        var cosine = dot / (aNorm * bNorm);
        return Math.Clamp(cosine, 0d, 1d);
    }
}