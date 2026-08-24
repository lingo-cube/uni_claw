using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Registry for vector backends. Currently only InMemory is registered.
/// Future backends can be added by implementing ISemanticVectorIndexFactory
/// and registering under SemanticVectorBackend.*.
/// </summary>
public static class SemanticVectorIndexRegistry
{
    private static readonly IReadOnlyDictionary<string, ISemanticVectorIndexFactory> Factories =
        new Dictionary<string, ISemanticVectorIndexFactory>(StringComparer.OrdinalIgnoreCase)
        {
            [SemanticVectorBackend.InMemory] = new InMemoryVectorIndexFactory(),
        };

    /// <summary>Creates a vector index for the given backend name.</summary>
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
            $"Unsupported vector backend '{backend}'. Supported: {string.Join(", ", Factories.Keys)}");
    }

    /// <summary>Returns true when the backend is registered.</summary>
    public static bool IsSupported(string backend) =>
        !string.IsNullOrWhiteSpace(backend) && Factories.ContainsKey(backend);

    private sealed class InMemoryVectorIndexFactory : ISemanticVectorIndexFactory
    {
        public string BackendName => SemanticVectorBackend.InMemory;

        public IVectorSemanticIndex Create(InMemoryVectorIndexOptions? options = null)
        {
            var opts = options ?? new InMemoryVectorIndexOptions();
            return new InMemoryVectorSemanticIndex(
                opts.Patterns.IsDefault ? ImmutableArray<SemanticPattern>.Empty : opts.Patterns,
                opts.MatchThreshold);
        }
    }
}