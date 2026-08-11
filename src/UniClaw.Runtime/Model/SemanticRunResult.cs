namespace UniClaw.Runtime.Model;

/// <summary>
/// Outcome of a semantic closed-loop run.
/// Phase 5 terminal states — truthful, no fabricated completion.
/// </summary>
public abstract record SemanticRunResult
{
    /// <summary>Desired state confirmed by fresh observation evidence.</summary>
    public sealed record Satisfied(GoalEvidence Evidence) : SemanticRunResult;

    /// <summary>Current state unknown — cannot safely dispatch. State evidence required.</summary>
    public sealed record StateEvidenceRequired(string Reason) : SemanticRunResult;

    /// <summary>Object binding unresolved — cannot identify interaction surface.</summary>
    public sealed record BindingUnresolved(string Reason) : SemanticRunResult;

    /// <summary>Semantic contradiction — evidence conflicts, Agent cannot adjudicate automatically.</summary>
    public sealed record SemanticContradiction(string Reason) : SemanticRunResult;

    /// <summary>Loop budget exhausted without achieving desired state.</summary>
    public sealed record BudgetExhausted(string Reason) : SemanticRunResult;

    /// <summary>Execution failed — action dispatch or post-action observation failed.</summary>
    public sealed record ExecutionFailed(string Reason) : SemanticRunResult;

    private SemanticRunResult() { }
}
