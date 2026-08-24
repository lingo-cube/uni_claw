using UniClaw.Semantic.Infrastructure.Fast;

namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Factory contract for creating a vector index backend. Future backends
/// (BGE, FAISS, Qdrant, Milvus) implement this and register a backend name.
/// </summary>
public interface ISemanticVectorIndexFactory
{
    /// <summary>Backend identifier, e.g. SemanticVectorBackend.InMemory.</summary>
    string BackendName { get; }

    /// <summary>Creates a read-only IVectorSemanticIndex.</summary>
    IVectorSemanticIndex Create(InMemoryVectorIndexOptions? options = null);
}