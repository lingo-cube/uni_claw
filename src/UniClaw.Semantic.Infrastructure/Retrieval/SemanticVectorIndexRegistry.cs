using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Registry for RETRIEVAL backends. Currently only the exact in-memory vector
/// index is registered. Future retrieval backends implement
/// <see cref="ISemanticVectorIndexFactory"/>; embedding models are not part of
/// this registry.
/// </summary>
public static class SemanticVectorIndexRegistry
{
    private static readonly IReadOnlyDictionary<string, ISemanticVectorIndexFactory> Factories =
        new Dictionary<string, ISemanticVectorIndexFactory>(StringComparer.OrdinalIgnoreCase)
        {
            [SemanticVectorBackend.InMemory] = new ExactInMemoryVectorIndexFactory(),
        };

    /// <summary>Creates a vector index for the given retrieval backend name.</summary>
    public static IVectorSemanticIndex Create(
        string backend,
        InMemoryVectorIndexOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(backend))
        {
            throw new ArgumentException("Backend must not be empty.", nameof(backend));
        }

        if (Factories.TryGetValue(backend, out var factory))
        {
            return factory.Create(options);
        }

        throw new NotSupportedException(
            $"Unsupported retrieval backend '{backend}'. Supported: {string.Join(", ", Factories.Keys)}");
    }

    /// <summary>Returns true when the retrieval backend is registered.</summary>
    public static bool IsSupported(string backend) =>
        !string.IsNullOrWhiteSpace(backend) && Factories.ContainsKey(backend);

    /// <summary>
    /// Creates an exact in-memory vector index over the given prototype store
    /// using the deterministic embedding provider (V1 representation).
    /// </summary>
    public static IVectorSemanticIndex CreateInMemory(
        IContainerIdentityPrototypeStore prototypes,
        int maxResults = 0)
        => new ExactInMemoryVectorIndex(
            prototypes,
            new DeterministicSemanticEmbeddingProvider(),
            maxResults);

    private sealed class ExactInMemoryVectorIndexFactory : ISemanticVectorIndexFactory
    {
        public string BackendName => SemanticVectorBackend.InMemory;

        public IVectorSemanticIndex Create(InMemoryVectorIndexOptions? options = null)
            => CreateInMemory(
                new ContainerIdentityPrototypeStore(),
                options?.MaxResults ?? 0);
    }
}