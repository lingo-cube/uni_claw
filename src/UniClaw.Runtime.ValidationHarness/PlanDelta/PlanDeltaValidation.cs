namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// Deterministic outcome of contract-legal plan-delta validation (spec "PlanDelta
/// contract" scenario "Deltas are evidenced and contract-legal"). <see cref="Accepted"/>
/// means every declared change resolves its citations, maps to a real directive
/// freedom difference, and the round carries no undeclared directive drift;
/// <see cref="Rejected"/> names the first contract violation. Rejection is a
/// typed result, never an exception-as-control-flow.
/// </summary>
public abstract record PlanDeltaValidation
{
    private PlanDeltaValidation()
    {
    }

    /// <summary>The round's PlanDelta is contract-legal.</summary>
    public sealed record Accepted : PlanDeltaValidation;

    /// <summary>The round's PlanDelta violates the PlanDelta contract (first violation).</summary>
    public sealed record Rejected(string Reason) : PlanDeltaValidation;

    /// <summary>Whether the round's delta is contract-legal.</summary>
    public bool IsAccepted => this is Accepted;
}