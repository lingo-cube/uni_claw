using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// A semantic embedding vector plus the minimum metadata needed by retrieval and
/// by profile qualification: the values, the dimension, and the
/// <see cref="EmbeddingModelIdentity"/> that produced it.
/// </summary>
public sealed record EmbeddingVector
{
    /// <summary>Embedding coordinates.</summary>
    public ImmutableArray<float> Values { get; }

    /// <summary>Producing model identity.</summary>
    public EmbeddingModelIdentity Model { get; }

    /// <summary>Dimension of this vector.</summary>
    public int Dimension => Values.Length;

    /// <summary>Creates an embedding vector.</summary>
    public EmbeddingVector(ImmutableArray<float> values, EmbeddingModelIdentity model)
    {
        Values = values.IsDefault ? ImmutableArray<float>.Empty : values;
        Model = model;
        if (model.Dimension > 0 && Values.Length != model.Dimension)
        {
            throw new ArgumentException(
                $"Embedding dimension {Values.Length} does not match model dimension {model.Dimension}.");
        }
    }
}