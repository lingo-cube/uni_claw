using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic;

/// <summary>
/// Input context for a Semantic provider, limited to the allowed Semantic inputs:
/// Current Observation, Visible Elements (via <see cref="CurrentObservation"/>),
/// and Previous Verified Identity / Container History. Goal, Action command,
/// Expected state, and Planning context MUST NOT be passed here.
/// </summary>
public sealed record ObservationContext
{
    /// <summary>The current observation (evidence snapshot, not semantic truth).</summary>
    public Observation CurrentObservation { get; }

    /// <summary>Previous verified container identity candidate, if any.</summary>
    public string? PreviousVerifiedIdentity { get; }

    /// <summary>Creates a Semantic observation context.</summary>
    public ObservationContext(Observation currentObservation, string? previousVerifiedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(currentObservation);
        CurrentObservation = currentObservation;
        PreviousVerifiedIdentity = previousVerifiedIdentity;
    }
}

/// <summary>
/// Semantic Perception provider port. A provider may only query, reason, and
/// return evidence. It MUST NOT execute Action, complete Goal, Plan, or mutate
/// World. It does NOT bypass Runtime and does NOT produce Fact.
/// </summary>
public interface ISemanticProvider
{
    /// <summary>
    /// Resolves SemanticEvidence for the given observation context.
    /// </summary>
    Task<ImmutableArray<SemanticEvidence>> ResolveAsync(
        ObservationContext context,
        CancellationToken cancellationToken = default);
}