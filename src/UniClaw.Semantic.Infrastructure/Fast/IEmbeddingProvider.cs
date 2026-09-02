namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Embedding boundary: converts the semantic representation
/// (<see cref="ContainerSemanticQuery"/>) into an <see cref="EmbeddingVector"/>.
///
/// An embedding provider ONLY represents. It never thresholds, never looks up
/// prototypes, never accepts candidates, and never touches a vector database.
/// (BGE-small is an embedding model; a future BgeSmallEmbeddingProvider will
/// implement this interface. This gate establishes the abstraction plus a
/// deterministic implementation — no production BGE wiring.)
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Embeds a semantic query representation into a vector.</summary>
    EmbeddingVector Embed(ContainerSemanticQuery query);
}