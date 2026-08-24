namespace UniClaw.Runtime.Model;

/// <summary>
/// The bounded run-level decision state a reconciliation can classify.
/// Continue means the current execution hypothesis remains consistent with the
/// observed world. Revise means the hypothesis no longer matches world evidence.
/// Escalate means the problem exceeds the current RuntimeAgent authority.
/// <remarks>The decision <b>records</b> a classification; it never performs it. Escalate is a
/// record of the authority boundary being exceeded — it is not an escalation action.</remarks>
/// </summary>
public enum RuntimeDecisionState
{
    /// <summary>The current hypothesis remains consistent with the observed world; execution may continue.</summary>
    Continue = 1,

    /// <summary>The current hypothesis no longer matches world evidence; it should be revised.</summary>
    Revise = 2,

    /// <summary>The problem exceeds the current RuntimeAgent authority. A passive record — not an action.</summary>
    Escalate = 3,
}

/// <summary>
/// One immutable, passive run-level decision recorded after reconciling an
/// <see cref="ExecutionHypothesis"/> against the observed world. It carries a run
/// identity, a decision state, a reference to the reconciled hypothesis, a reference
/// to the supporting evidence, and a decision reason derived from generic trace event
/// reasons + belief state (never scenario strings).
/// <para>
/// It is analogous to <see cref="ExecutionHypothesis"/> and <see cref="TraceEvent"/> —
/// an observable record that drives no decision. It carries NO Action, NO authorization,
/// NO UI element selection, NO Goal modification, NO Traversal control, and NO execution
/// authority. It must not be consulted by the Agent for decisions, authorization,
/// completion, or execution.
/// </para>
/// </summary>
public sealed record RuntimeDecision
{
    /// <summary>Creates a validated runtime decision record.</summary>
    /// <exception cref="ArgumentException">A required field is blank or the state is undefined.</exception>
    public RuntimeDecision(
        string runId,
        RuntimeDecisionState state,
        string hypothesisReference,
        string evidenceReference,
        string decisionReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hypothesisReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionReason);
        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(nameof(state));

        RunId = runId;
        State = state;
        HypothesisReference = hypothesisReference;
        EvidenceReference = evidenceReference;
        DecisionReason = decisionReason;
    }

    /// <summary>Run identity the decision belongs to.</summary>
    public string RunId { get; init; }

    /// <summary>The decision state (Continue / Revise / Escalate).</summary>
    public RuntimeDecisionState State { get; init; }

    /// <summary>Reference to the reconciled <see cref="ExecutionHypothesis"/> (its run identity).</summary>
    public string HypothesisReference { get; init; }

    /// <summary>Reference to the evidence the decision was derived from (a trace reason or belief state).</summary>
    public string EvidenceReference { get; init; }

    /// <summary>Generic decision reason derived from trace event reasons + belief state; never a scenario string.</summary>
    public string DecisionReason { get; init; }
}
