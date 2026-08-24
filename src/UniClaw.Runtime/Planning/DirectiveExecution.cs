using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Additive bounded execution entry for an already-decomposed bounded
/// exploration directive. It wraps the decomposed spec in an open-world
/// <see cref="IntentSemanticEnvelope.Resolved"/> and forwards through the
/// existing <see cref="IntentExecution.RunOpenWorldAsync"/> seam — it never
/// adds a public <see cref="RuntimeAgent"/> method, never observes the world,
/// and holds no authority.
/// </summary>
public static class DirectiveExecution
{
    /// <summary>
    /// Runs a resolved bounded-exploration decomposition through the existing
    /// open-world DFS seam.
    /// <para>
    /// Additive optional <paramref name="hypothesisLedger"/>: when null, the
    /// Phase 1 behavior is preserved exactly (zero regression). When provided,
    /// the ledger is Activate()d before the run and ReviseFromEvidence()d from
    /// the Agent's trace + returned RunState after — the DFS engine call is
    /// unchanged, and the ledger only records; it never decides.
    /// </para>
    /// </summary>
    public static Task<RunState> RunDirectiveAsync(
        RuntimeAgent agent,
        DirectiveDecompositionResult.Resolved resolved,
        string runId,
        CancellationToken cancellationToken = default,
        ExecutionHypothesisLedger? hypothesisLedger = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(resolved);

        var envelope = IntentSemanticEnvelope.Project(
            "Open-world bounded exploration directive",
            resolved.Goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(resolved.Specification));

        hypothesisLedger?.Activate();

        var runTask = IntentExecution.RunOpenWorldAsync(agent, envelope, runId, cancellationToken);

        if (hypothesisLedger is null)
            return runTask;

        // Post-run, trace-derived hypothesis revision. The DFS engine and the seam call
        // are unchanged; the ledger only records, it never decides the run outcome.
        return runTask.ContinueWith(
            completed =>
            {
                var result = completed.GetAwaiter().GetResult();
                hypothesisLedger.ReviseFromEvidence(agent.Trace, result);
                // Reconcile the hypothesis against the observed world, producing a
                // bounded RuntimeDecision (Continue/Revise/Escalate). Additive and
                // non-authoritative: the caller reads hypothesisLedger.LatestDecision
                // after awaiting. The Agent stays the sole run authority.
                hypothesisLedger.Reconcile(agent.Belief);
                // Apply the bounded RuntimeDecision to the run-local execution
                // hypothesis (Keep/Replace/Escalate), closing the
                // decision-to-hypothesis loop. Additive and non-authoritative: the
                // caller reads hypothesisLedger.LatestAdaptation after awaiting. The
                // Agent stays the sole run authority; the adaptation only records.
                hypothesisLedger.Adapt();
                return result;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
