using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Bounded execution entry for an already-resolved open-world semantic envelope.
/// This validates and destructures caller authority only; it does not parse, plan,
/// observe, select a target, construct a route, or decide Goal completion.
/// </summary>
public static class IntentSemanticEnvelopeExecution
{
    /// <summary>
    /// Runs the navigation-only, exhaustive type-level representation through the
    /// existing Agent-owned bounded traversal protocol.
    /// </summary>
    public static Task<RunState> RunOpenWorldAsync(
        RuntimeAgent agent,
        IntentSemanticEnvelope.Resolved envelope,
        string runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (envelope.Representation is not IntentExecutionRepresentation.OpenWorldTypeLevel openWorld)
        {
            throw new ArgumentException(
                "Open-world execution requires IntentExecutionRepresentation.OpenWorldTypeLevel.",
                nameof(envelope));
        }

        var specification = openWorld.Specification;
        if (specification.Completion != TypeLevelCompletionRequirement.ExhaustiveWithinScope
            || !string.Equals(specification.Scope.ApplicationIdentity, specification.Entry.ApplicationIdentity, StringComparison.Ordinal)
            || !string.Equals(specification.Scope.SemanticRoot, specification.Entry.ExpectedSemanticEntry, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The bounded open-world execution entry requires ExhaustiveWithinScope completion and matching scope and entry boundaries.",
                nameof(envelope));
        }

        return agent.RunOpenWorldAsync(
            envelope.Goal,
            specification.Scope.ApplicationIdentity,
            specification.Entry.ExpectedSemanticEntry,
            specification.MaximumDepth,
            runId,
            specification.DispatchPolicy,
            cancellationToken);
    }
}
