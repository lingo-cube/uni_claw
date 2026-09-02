namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Retrieval backend identifiers used by <c>SemanticOptions.VectorBackend</c>.
///
/// These are VECTOR INDEX / RETRIEVAL BACKENDS ONLY. Embedding models are NOT
/// backends — they live in the Embedding layer (IEmbeddingProvider +
/// EmbeddingModelIdentity). The historical "BGE backend" concept is retired.
/// </summary>
public static class SemanticVectorBackend
{
    /// <summary>Exact in-memory vector index (retrieval backend).</summary>
    public const string InMemory = "InMemory";

    /// <summary>FAISS backend (candidate, not wired).</summary>
    public const string Faiss = "FAISS";

    /// <summary>Qdrant backend (candidate, not wired).</summary>
    public const string Qdrant = "Qdrant";

    /// <summary>Milvus backend (candidate, not wired).</summary>
    public const string Milvus = "Milvus";
}