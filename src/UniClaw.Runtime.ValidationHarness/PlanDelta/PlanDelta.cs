using System.Collections.Immutable;

namespace UniClaw.Runtime.ValidationHarness.PlanDelta;

/// <summary>
/// One round's plan revision (spec "PlanDelta contract"): either a non-empty
/// list of declared freedom changes — each evidencing and citing resolvable
/// knowledge/evidence — or an honest NO-OP (<see cref="IsNoOp"/> with the reason
/// in <see cref="NoOpReason"/>). Faking a delta to inflate adaptation counts is
/// forbidden: an empty delta MUST be a NO_OP_WITH_REASON, and a NO-OP round MUST
/// carry a NextStrategy equal to PreviousPlan on every compared lever (enforced
/// by <see cref="PlanDeltaValidator"/>). A PlanDelta is a validation artifact;
/// no field ever enters the wire.
/// </summary>
public sealed record PlanDelta
{
    /// <summary>Create a real delta carrying at least one freedom change.</summary>
    public PlanDelta(IReadOnlyList<PlanDeltaChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
            throw new ArgumentException(
                "An empty PlanDelta is a NO-OP: use PlanDelta.NoOp(reason) instead of an empty change list.",
                nameof(changes));

        Changes = changes.ToImmutableArray();
        NoOpReason = null;
    }

    private PlanDelta(string noOpReason)
    {
        // The reason is recorded as authored; PlanDeltaValidator enforces
        // NO_OP_WITH_REASON (non-empty reason) so the machine check is the
        // single, testable enforcement point for round honesty.
        NoOpReason = noOpReason;
        Changes = ImmutableArray<PlanDeltaChange>.Empty;
    }

    /// <summary>
    /// Create an honest NO_OP_WITH_REASON. The reason is recorded as authored;
    /// <see cref="PlanDeltaValidator"/> validates both that it is non-empty and
    /// that NextStrategy equals PreviousPlan on all compared levers.
    /// </summary>
    public static PlanDelta NoOp(string reason) => new(reason);

    /// <summary>The declared freedom changes (empty exactly when this is a NO-OP).</summary>
    public IReadOnlyList<PlanDeltaChange> Changes { get; }

    /// <summary>The NO-OP reason; null for a real delta. NO_OP_WITH_REASON is
    /// the only legal empty delta.</summary>
    public string? NoOpReason { get; }

    /// <summary>True when this round records no change (NO_OP_WITH_REASON).</summary>
    public bool IsNoOp => Changes.Count == 0;
}