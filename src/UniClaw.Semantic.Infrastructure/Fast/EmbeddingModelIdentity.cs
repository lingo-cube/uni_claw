namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Minimal identity of a semantic embedding model. Purpose: qualification and
/// profile binding (a SemanticPerceptionProfile pins an
/// <see cref="EmbeddingModelIdentity"/>). This is NOT a model management
/// framework — it is the minimum bookkeeping a profile needs.
/// </summary>
public sealed record EmbeddingModelIdentity
{
    /// <summary>Model id, e.g. "BAAI/bge-small-en-v1.5" or "deterministic-v1".</summary>
    public string ModelId { get; }

    /// <summary>Model revision pin, e.g. "v1.5" / commit / fastembed pin.</summary>
    public string Revision { get; }

    /// <summary>Embedding dimension (0 = unbound/dynamic).</summary>
    public int Dimension { get; }

    /// <summary>Model runtime, e.g. "fastembed+onnxruntime" / "in-process" / "torch".</summary>
    public string Runtime { get; }

    /// <summary>Numerical precision, e.g. "fp32" / "int8" / "n/a".</summary>
    public string Precision { get; }

    /// <summary>Creates an embedding model identity.</summary>
    public EmbeddingModelIdentity(
        string modelId,
        string revision,
        int dimension,
        string runtime,
        string precision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ModelId = modelId;
        Revision = revision;
        Dimension = dimension;
        Runtime = runtime;
        Precision = precision;
    }
}