using System.Collections.Immutable;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Caller-injected strategy-rule set that the decomposer projects 1:1 onto a
/// type-directed <see cref="Goal"/>. It carries only rule delegates — no plan,
/// no coordinates, no <see cref="DeviceAction"/>, no element index. The caller
/// supplies strategy <em>knowledge</em>; the RuntimeAgent owns execution
/// <em>authority</em>. The decomposer never invents a rule.
/// </summary>
/// <param name="EvidenceEvaluator">Caller completion-criterion rule.</param>
/// <param name="CandidateAuthorizationEvaluator">Caller candidate-authorization rule.</param>
/// <param name="BranchInventoryEvaluator">Caller required-branch inventory rule.</param>
/// <param name="ViewportExplorationEvaluator">Optional caller same-Container viewport exploration criterion.</param>
/// <param name="CategoryClassifier">Optional caller element-category classifier.</param>
public sealed record DirectiveStrategyRules(
    Func<Observation, GoalEvidence> EvidenceEvaluator,
    Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? CandidateAuthorizationEvaluator = null,
    Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>? BranchInventoryEvaluator = null,
    Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? ViewportExplorationEvaluator = null,
    Func<ObservedElement, TypeLevelElementCategory?>? CategoryClassifier = null)
{
    /// <summary>
    /// Convenience constructor for the mandatory completion, authorization, and
    /// inventory rules (viewport exploration and category classification absent →
    /// navigation-only behavior).
    /// </summary>
    public DirectiveStrategyRules(
        Func<Observation, GoalEvidence> evidenceEvaluator,
        Func<Observation, ObservedElement, CandidateAuthorizationEvidence> candidateAuthorizationEvaluator,
        Func<ImmutableArray<Observation>, int, BranchInventoryEvidence> branchInventoryEvaluator)
        : this(
            evidenceEvaluator,
            candidateAuthorizationEvaluator,
            branchInventoryEvaluator,
            ViewportExplorationEvaluator: null,
            CategoryClassifier: null)
    {
    }
}

/// <summary>
/// Immutable caller-side expression of a bounded exploration intent. It carries
/// only task-level declarations: a declared task scope, an entry boundary, a
/// maximum semantic depth, a safety boundary, a completion requirement, and a
/// caller-injected strategy-rule set (plus an optional dispatch policy). It
/// carries no <see cref="Plan"/>, no element coordinates, no
/// <see cref="DeviceAction"/>, no <see cref="TraversalStepResult"/>, and no
/// element index — it holds no precompiled physical step.
/// </summary>
public sealed record Directive
{
    /// <summary>Creates a validated bounded-exploration directive.</summary>
    public Directive(
        TypeLevelTaskScope scope,
        TypeLevelEntryBoundary entry,
        int maximumDepth,
        TypeLevelSafetyBoundary safety,
        TypeLevelCompletionRequirement completion,
        DirectiveStrategyRules strategyRules,
        TypeLevelDispatchPolicy? dispatchPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(safety);
        ArgumentNullException.ThrowIfNull(strategyRules);

        if (maximumDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        if (completion != TypeLevelCompletionRequirement.ExhaustiveWithinScope)
            throw new ArgumentOutOfRangeException(nameof(completion));

        Scope = scope;
        Entry = entry;
        MaximumDepth = maximumDepth;
        Safety = safety;
        Completion = completion;
        StrategyRules = strategyRules;
        DispatchPolicy = dispatchPolicy;
    }

    /// <summary>Declared application + semantic task scope boundary.</summary>
    public TypeLevelTaskScope Scope { get; }
    /// <summary>Declared application + semantic entry boundary.</summary>
    public TypeLevelEntryBoundary Entry { get; }
    /// <summary>Declared upper bound on semantic traversal depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Declared allowed-interaction boundary.</summary>
    public TypeLevelSafetyBoundary Safety { get; }
    /// <summary>Declared caller completion requirement.</summary>
    public TypeLevelCompletionRequirement Completion { get; }
    /// <summary>Caller-injected strategy-rule set; the decomposer projects these 1:1.</summary>
    public DirectiveStrategyRules StrategyRules { get; }
    /// <summary>Optional caller-authorized category → handling dispatch policy; absent = navigation-only (Tap).</summary>
    public TypeLevelDispatchPolicy? DispatchPolicy { get; }
}
