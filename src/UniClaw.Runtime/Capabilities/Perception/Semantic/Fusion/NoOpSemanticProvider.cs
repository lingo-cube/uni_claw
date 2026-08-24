using System.Collections.Immutable;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>
/// Default Semantic provider when no real provider is wired. Returns empty
/// evidence so Runtime continues normally (no Semantic provider → empty evidence).
/// It never executes Action / Goal / Plan / World mutation.
/// </summary>
public sealed class NoOpSemanticProvider : ISemanticProvider
{
    /// <inheritdoc />
    public Task<ImmutableArray<SemanticEvidence>> ResolveAsync(
        ObservationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ImmutableArray<SemanticEvidence>.Empty);
    }
}
