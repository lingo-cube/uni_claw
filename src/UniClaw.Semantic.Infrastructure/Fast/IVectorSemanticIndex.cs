namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Read-only vector semantic index for semantic pattern retrieval. It returns
/// <see cref="SemanticCandidate"/> or null. It never returns Fact and never
/// decides identity — Runtime decides.
/// </summary>
public interface IVectorSemanticIndex
{
    /// <summary>Retrieves the best matching semantic candidate for a query.</summary>
    /// <returns>A candidate when a pattern matches; null on miss.</returns>
    SemanticCandidate? Retrieve(ContainerSemanticQuery query);
}
