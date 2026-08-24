using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Stateless, pure adaptation of a <see cref="RuntimeDecision"/> against the current
/// run-local <see cref="ExecutionHypothesis"/> into exactly one bounded
/// <see cref="HypothesisAdaptation"/>. It is structurally identical to
/// <c>HypothesisReconciler.Reconcile</c> (Planning/) and <c>Reconcile.FromObservation</c>
/// (World/): 无状态、无决策 authority — it maps a decision to a bounded hypothesis
/// update; it does not perform the update's execution consequences.
/// <para>
/// The adapter does NOT observe the world, does NOT authorize an action, does NOT
/// execute anything, does NOT recover, does NOT retry, does NOT modify the Goal or
/// completion, and does NOT call any Agent method. It consumes only Model/ types
/// (<see cref="RuntimeDecision"/> + <see cref="ExecutionHypothesis"/>).
/// </para>
/// <para>
/// Replace does NOT execute SystemBack: the adaptation only records a boundary-aware
/// objective; the existing ExternalBoundary capability inside the DFS loop remains
/// solely responsible for boundary handling. Escalate does NOT recover: the adaptation
/// records the authority boundary being exceeded; no recovery, retry, or action
/// dispatch is performed. All adaptation reasons are derived from the decision reason +
/// generic boundary/authority language (never scenario strings).
/// </para>
/// </summary>
public static class HypothesisAdapter
{
    /// <summary>
    /// Maps <paramref name="decision"/> and <paramref name="currentHypothesis"/> into
    /// exactly one <see cref="HypothesisAdaptation"/>. Pure and deterministic: identical
    /// inputs always produce structurally identical adaptations with no side effects.
    /// </summary>
    /// <exception cref="ArgumentNullException">decision 或 currentHypothesis 为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException">decision.State 未定义。</exception>
    public static HypothesisAdaptation Adapt(
        RuntimeDecision decision,
        ExecutionHypothesis currentHypothesis)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(currentHypothesis);

        return decision.State switch
        {
            // Keep — the current hypothesis remains consistent with the observed world;
            // confirm it. No new assumption, no action, no Goal modification.
            RuntimeDecisionState.Continue => Keep(decision, currentHypothesis),

            // Replace — the current hypothesis no longer explains reality; mark it
            // superseded and create a boundary-aware replacement. Records a hypothesis
            // update only — NO SystemBack / DeviceAction / Tap, no traversal action.
            RuntimeDecisionState.Revise => Replace(decision, currentHypothesis),

            // Escalate — the RuntimeAgent cannot adapt inside its current authority;
            // record the inability (Revised + escalation reason). No recovery / retry /
            // action dispatch.
            RuntimeDecisionState.Escalate => Escalate(decision, currentHypothesis),

            _ => throw new ArgumentOutOfRangeException(nameof(decision), "Undefined decision state."),
        };
    }

    /// <summary>
    /// Keep adaptation: the adapted hypothesis is the current hypothesis with Status
    /// Confirmed (unchanged when it is already Confirmed). The reason is the decision
    /// reason — generic, never a scenario string.
    /// </summary>
    private static HypothesisAdaptation Keep(RuntimeDecision decision, ExecutionHypothesis current)
    {
        var adapted = current.Status == ExecutionHypothesisStatus.Confirmed
            ? current
            : current with { Status = ExecutionHypothesisStatus.Confirmed };

        return New(
            decision,
            current,
            HypothesisAdaptationType.Keep,
            adapted,
            decision.DecisionReason);
    }

    /// <summary>
    /// Replace adaptation: the adapted hypothesis is a NEW boundary-aware hypothesis
    /// (Status Created, generic objective derived from the decision's evidence
    /// reference — NOT a scenario string, NOT a SystemBack instruction). The superseded
    /// current hypothesis is referenced as the previous hypothesis; the ledger records
    /// it as Replaced when the adaptation is applied. No action is executed or referenced.
    /// </summary>
    private static HypothesisAdaptation Replace(RuntimeDecision decision, ExecutionHypothesis current)
    {
        var boundaryAware = new ExecutionHypothesis(
            runId: current.RunId,
            directiveReference: current.DirectiveReference,
            objective: "External boundary relation requires bounded return handling",
            expectedTransition: current.ExpectedTransition,
            expectedOutcome: current.ExpectedOutcome,
            confidence: current.Confidence,
            revisionReason: null,
            createdAtObservation: null,
            status: ExecutionHypothesisStatus.Created);

        return New(
            decision,
            current,
            HypothesisAdaptationType.Replace,
            boundaryAware,
            decision.DecisionReason);
    }

    /// <summary>
    /// Escalate adaptation: the adapted hypothesis is the current hypothesis with Status
    /// Revised and an escalation-marked revision reason recording the inability (derived
    /// from the decision reason — generic authority language, never a scenario string).
    /// The adaptation records only; no recovery, retry, or action dispatch.
    /// </summary>
    private static HypothesisAdaptation Escalate(RuntimeDecision decision, ExecutionHypothesis current)
    {
        var adapted = current with
        {
            Status = ExecutionHypothesisStatus.Revised,
            RevisionReason = $"Escalation: {decision.DecisionReason}",
        };

        return New(
            decision,
            current,
            HypothesisAdaptationType.Escalate,
            adapted,
            $"Escalation: {decision.DecisionReason}");
    }

    /// <summary>Builds the validated adaptation record referencing the decision + previous hypothesis.</summary>
    private static HypothesisAdaptation New(
        RuntimeDecision decision,
        ExecutionHypothesis current,
        HypothesisAdaptationType type,
        ExecutionHypothesis adapted,
        string reason)
        => new(
            runId: current.RunId,
            adaptationType: type,
            decisionReference: decision.HypothesisReference,
            previousHypothesisReference: current.RunId,
            adaptedHypothesis: adapted,
            adaptationReason: reason);
}