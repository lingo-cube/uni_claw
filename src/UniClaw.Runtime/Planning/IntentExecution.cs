using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Bounded execution entries for already-resolved intent projections. This seam neither parses
/// input nor plans, observes, selects targets, constructs routes, or decides Goal completion.
/// </summary>
public static class IntentExecution
{
    /// <summary>
    /// Forwards an already compiled semantic goal to the existing Agent-owned semantic loop.
    /// </summary>
    public static Task<SemanticRunResult> RunResolvedAsync(
        RuntimeAgent agent,
        IntentCompilationResult.Resolved resolved,
        ImmutableArray<SemanticObject> objects,
        ImmutableArray<Capability> capabilities,
        string runId,
        CancellationToken cancellationToken = default,
        int maxIterations = 5)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(resolved);
        return agent.RunSemanticGoalAsync(
            resolved.Goal,
            objects,
            capabilities,
            runId,
            cancellationToken,
            maxIterations);
    }

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

        return RunOpenWorldCoreAsync(agent, envelope, runId, cancellationToken, null);
    }

    internal static Task<RunState> RunStrategyOpenWorldAsync(
        RuntimeAgent agent,
        IntentSemanticEnvelope.Resolved envelope,
        string runId,
        ExplorationExecutionSemantics semantics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(semantics);
        return RunOpenWorldCoreAsync(agent, envelope, runId, cancellationToken, semantics);
    }

    private static async Task<RunState> RunOpenWorldCoreAsync(
        RuntimeAgent agent,
        IntentSemanticEnvelope.Resolved envelope,
        string runId,
        CancellationToken cancellationToken,
        ExplorationExecutionSemantics? semantics)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (envelope.Representation is not IntentExecutionRepresentation.OpenWorldTypeLevel openWorld)
            throw new ArgumentException(
                "Open-world execution requires IntentExecutionRepresentation.OpenWorldTypeLevel.",
                nameof(envelope));

        var specification = openWorld.Specification;
        if (specification.Completion != TypeLevelCompletionRequirement.ExhaustiveWithinScope
            || !string.Equals(specification.Scope.ApplicationIdentity, specification.Entry.ApplicationIdentity, StringComparison.Ordinal)
            || !string.Equals(specification.Scope.SemanticRoot, specification.Entry.ExpectedSemanticEntry, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The bounded open-world execution entry requires ExhaustiveWithinScope completion and matching scope and entry boundaries.",
                nameof(envelope));
        }

        // Multi-stage intent seam: one structural span per open-world intent
        // execution (outcome = execution closure only; never Goal completion).
        using var span = RuntimeObservability.StartSpan(
            "RunIntentOpenWorld", ObservabilityLayer.Agent, ObservabilityComponent.IntentExecution);
        try
        {
            var state = await agent.RunOpenWorldAsync(
                envelope.Goal,
                specification.Scope.ApplicationIdentity,
                specification.Entry.ExpectedSemanticEntry,
                specification.MaximumDepth,
                runId,
                specification.DispatchPolicy,
                cancellationToken,
                semantics);
            RuntimeObservability.Complete(span, ObservabilityOutcome.Succeeded);
            return state;
        }
        catch (OperationCanceledException)
        {
            RuntimeObservability.Complete(span, ObservabilityOutcome.Cancelled);
            throw;
        }
        catch (Exception)
        {
            RuntimeObservability.Complete(span, ObservabilityOutcome.Failed);
            throw;
        }
    }
}
