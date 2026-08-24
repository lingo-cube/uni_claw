namespace UniClaw.Semantic.Infrastructure.Retrieval;

/// <summary>
/// Stable vector backend identifiers used by <c>SemanticOptions.VectorBackend</c>.
/// These constants prevent magic strings and make future backends manageable.
/// </summary>
public static class SemanticVectorBackend
{
    /// <summary>In-memory read-only vector/pattern index (current default).</summary>
    public const string InMemory = "InMemory";

    /// <summary>BGE embedding backend (candidate).</summary>
    public const string Bge = "BGE";

    /// <summary>FAISS backend (candidate).</summary>
    public const string Faiss = "FAISS";

    /// <summary>Qdrant backend (candidate).</summary>
    public const string Qdrant = "Qdrant";

    /// <summary>Milvus backend (candidate).</summary>
    public const string Milvus = "Milvus";
}