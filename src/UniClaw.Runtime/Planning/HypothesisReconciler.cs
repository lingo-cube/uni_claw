using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Stateless, pure reconciliation of an <see cref="ExecutionHypothesis"/> against the
/// observed world + trace evidence into a bounded <see cref="RuntimeDecision"/>. It is
/// structurally identical to <c>Reconcile.FromObservation</c> (World/): 无状态、无决策
/// authority — it classifies evidence into a decision state; it does not perform the
/// decision, does not observe the world, and does not call any Agent method.
/// <para>
/// All decision reasons are derived from generic trace event reasons + belief state
/// (never scenario strings). Escalate is a RECORD of the authority boundary being
/// exceeded — the RuntimeAgent does not perform an escalation action.
/// </para>
/// </summary>
public static class HypothesisReconciler
{
    /// <summary>
    /// Classifies <paramref name="hypothesis"/> against <paramref name="belief"/> and
    /// <paramref name="trace"/> into exactly one <see cref="RuntimeDecision"/>. The run
    /// outcome is derived from the trace's terminal <see cref="RunState"/> events. Pure
    /// and deterministic: identical inputs always produce structurally identical decisions.
    /// </summary>
    /// <exception cref="ArgumentNullException">hypothesis 或 trace 为 null。</exception>
    public static RuntimeDecision Reconcile(
        ExecutionHypothesis hypothesis,
        WorldBelief? belief,
        IReadOnlyList<DecisionRecord> trace)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        ArgumentNullException.ThrowIfNull(trace);

        var runFailed = DecisionRecord.IndicatesFailedRun(trace);
        var boundaryObserved = AnyReason(trace, "EXTERNAL_BOUNDARY_OBSERVED");
        var inScopeProgress = AnyReason(trace, "open-world container inventory complete")
            || AnyReason(trace, "open-world branch inventory complete")
            || AnyReason(trace, "open-world branch inventory bounded-leaf")
            || AnyReason(trace, "verified parent return");

        // Escalate — the problem exceeds the current RuntimeAgent authority. Must be
        // classified first so a terminal authority-boundary failure is never masked as
        // a mere Revise. Escalate is a passive record, not an escalation action.
        var authorityFailureReason = AuthorityBoundaryFailureReason(trace);
        if (authorityFailureReason is not null && runFailed)
            return Escalate(
                hypothesis,
                "Authority boundary exceeded: the run failed at an authority-boundary indicator (identity safety / depth cutoff / boundary not handled).",
                runFailed: true);

        if (hypothesis.Status == ExecutionHypothesisStatus.Revised && runFailed)
            return Escalate(
                hypothesis,
                "Hypothesis was revised and the run failed; the RuntimeAgent could not reconcile and continue within its bounded authority.",
                runFailed: true);

        // Revise — the hypothesis no longer matches world evidence.
        if (boundaryObserved)
            return Revise(hypothesis, "External boundary observation contradicts the in-scope hypothesis expectation.");

        if (hypothesis.Status == ExecutionHypothesisStatus.Revised)
            return Revise(hypothesis, "Hypothesis was revised against world evidence.");

        if (belief?.SemanticPage is null)
            return Revise(hypothesis, "World belief is unknown (semantic page unresolved); hypothesis cannot be confirmed.");

        // Continue — hypothesis remains consistent with the observed world.
        if (hypothesis.Status is ExecutionHypothesisStatus.Confirmed
                or ExecutionHypothesisStatus.Active
            && inScopeProgress
            && !boundaryObserved)
        {
            return Continue(hypothesis, belief.SemanticPage, "In-scope progress confirms the hypothesis against the observed world.");
        }

        // Conservative fallback: a hypothesis neither contradicted nor confirmed by
        // in-scope progress is Revised — Continue is never fabricated without evidence.
        return Revise(hypothesis, "Hypothesis is not proven consistent with the observed world.");
    }

    private static RuntimeDecision Continue(ExecutionHypothesis hypothesis, string semanticPage, string reason)
        => New(hypothesis, RuntimeDecisionState.Continue, reason, evidence: $"semantic page '{semanticPage}' understood; in-scope progress");

    private static RuntimeDecision Revise(ExecutionHypothesis hypothesis, string reason)
        => New(hypothesis, RuntimeDecisionState.Revise, reason, evidence: "contradicting or unknown world evidence");

    private static RuntimeDecision Escalate(ExecutionHypothesis hypothesis, string reason, bool runFailed)
        => New(hypothesis, RuntimeDecisionState.Escalate, reason, evidence: "terminal authority-boundary failure");

    private static RuntimeDecision New(ExecutionHypothesis hypothesis, RuntimeDecisionState state, string reason, string evidence)
        => new(
            runId: hypothesis.RunId,
            state: state,
            hypothesisReference: hypothesis.RunId,
            evidenceReference: evidence,
            decisionReason: reason);

    private static bool AnyReason(IReadOnlyList<DecisionRecord> trace, string marker)
        => trace.Any(entry => entry.Reason is not null
            && entry.Reason.Contains(marker, StringComparison.Ordinal));

    /// <summary>
    /// Returns the first generic authority-boundary indicator found in the trace reasons
    /// (identity safety, bounded depth cutoff, or boundary not handled), or null when none
    /// is present. Derived from generic trace reasons — no scenario strings.
    /// </summary>
    private static string? AuthorityBoundaryFailureReason(IReadOnlyList<DecisionRecord> trace)
        => FirstReason(trace, "Open-world identity safety")
            ?? FirstReason(trace, "bounded cutoff")
            ?? FirstReason(trace, "was not handled; fail closed");

    private static string? FirstReason(IReadOnlyList<DecisionRecord> trace, string marker)
    {
        foreach (var entry in trace)
        {
            if (entry.Reason is not null
                && entry.Reason.Contains(marker, StringComparison.Ordinal))
            {
                return entry.Reason;
            }
        }
        return null;
    }
}
