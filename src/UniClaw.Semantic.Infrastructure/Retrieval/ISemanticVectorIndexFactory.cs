using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Factory contract for creating a RETRIEVAL backend (vector index). Future
/// backends (FAISS / Qdrant / Milvus) implement this and register under
/// <see cref="SemanticVectorBackend"/>.*. Embedding providers are NOT created
/// here — the embedding layer (IEmbeddingProvider) is independent.
/// </summary>
public interface ISemanticVectorIndexFactory
{
    /// <summary>Retrieval backend identifier, e.g. SemanticVectorBackend.InMemory.</summary>
    string BackendName { get; }

    /// <summary>Creates a read-only IVectorSemanticIndex (no acceptance policy).</summary>
    IVectorSemanticIndex Create(InMemoryVectorIndexOptions? options = null);
}