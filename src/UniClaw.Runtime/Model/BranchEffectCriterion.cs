namespace UniClaw.Runtime.Model;

/// <summary>
/// SC-P3-CAND-009 bounded immutable discovered-branch effect criterion: one durable external-effect
/// hypothesis associating one already-defined bounded semantic branch identity with one deterministic
/// Observation-only three-way evaluator. The carrier is a hypothesis, not proof — it does not establish
/// inventory membership, authorization, historical completion, current validity, lifecycle, Recovery,
/// completion, GoalEvidence, or Run outcome by itself.
/// The evaluator is deterministic, side-effect-free, and reads only the supplied Observation plus
/// immutable values captured by the caller. true/false/null mean positively revalidated / positively
/// contradicted / unobservable-or-unresolved. `BranchIdentity` only names an existing semantic identity
/// inside a bounded active parent scope; it is not an identity authority.
/// </summary>
public sealed record BranchEffectCriterion
{
    /// <summary>Non-empty semantic branch identity inside a bounded active parent scope.</summary>
    public string BranchIdentity { get; }

    /// <summary>Deterministic Observation-only three-way external-effect evaluator (non-null).</summary>
    public Func<Observation, bool?> Evaluator { get; }

    /// <summary>Create one validated immutable branch-effect criterion.</summary>
    /// <exception cref="ArgumentException">branchIdentity 为空或空白。</exception>
    /// <exception cref="ArgumentNullException">evaluator 为 null。</exception>
    public BranchEffectCriterion(string branchIdentity, Func<Observation, bool?> evaluator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchIdentity);
        ArgumentNullException.ThrowIfNull(evaluator);

        BranchIdentity = branchIdentity;
        Evaluator = evaluator;
    }
}
