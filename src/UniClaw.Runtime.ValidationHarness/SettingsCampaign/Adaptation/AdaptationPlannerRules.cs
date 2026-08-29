using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using UniClaw.Runtime.ValidationHarness.PlanDelta;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign.Adaptation;

/// <summary>
/// The CLOSED rule set mapping ACTIVE scenario knowledge → contract-legal
/// PlanDeltas for the Phase 2.6 validation-side adaptation planner (spec
/// "PlanDelta contract" — deltas land strictly inside the eight directive
/// freedoms, citations resolve, and a round without a real delta is
/// <c>NO_OP_WITH_REASON</c>; design D5; Phase 2.5 "UniAgent emulator"
/// precedent). This is validation-side planning composition, NOT a Runtime
/// Planner: the rules only ever produce a directive — they never touch Runtime
/// state, never inject knowledge into Runtime input, and never emit UI
/// actions, coordinates, selectors, or fixed paths.
///
/// Conservative defaults (spec "Safety learning without dangerous
/// trial-and-error" + change principle "UNPROVEN_SAFE → 不放宽"): a dangerous
/// class can only ever be EXCLUDED (prohibited effects tighten to the maximal
/// set), never relaxed; an unresolved root never justifies a depth increase;
/// depth increases are bounded by <see cref="MaximumSettingsDepth"/>. Every
/// rule is a declarative entry of the data-driven <see cref="RuleTable"/>
/// (input = active knowledge + the previous directive + the observed round's
/// evidence; output = PlanDelta + next-directive factory). Rules consume ONLY
/// records the observed round itself produced (the observed SourceRunId) so
/// loaded/historical knowledge never substitutes for fresh runtime evidence
/// (spec "Four frozen invariants" — CURRENT FRESH EVIDENCE FIRST); the sole
/// exception is the safety rule for KnownPotentiallyStateMutating /
/// KnownExternalBoundary, which stays binding across rounds once identified
/// (spec: "Once identified ... subsequent plans SHALL exclude the class").
/// </summary>
public static class AdaptationPlannerRules
{
    /// <summary>The bounded Settings scope depth cap (a finite, deliberately
    /// small traversal bound; the settings campaign never authorizes deeper
    /// descent past it).</summary>
    public const int MaximumSettingsDepth = 3;

    /// <summary>The maximal prohibited-effects set the conservative posture
    /// always declares (both effect classes — "已全集", nothing more to
    /// tighten).</summary>
    public static readonly ImmutableHashSet<StrategyProhibitedEffect> MaximalProhibitedEffects =
        ImmutableHashSet.Create(
            StrategyProhibitedEffect.StateMutation,
            StrategyProhibitedEffect.ExternalBoundaryCrossing);

    /// <summary>
    /// The ordered rule table (data-driven, first-match wins). Safety rules
    /// outrank depth rules: an unresolved root or an identified
    /// state-mutating/external-boundary class never coexists with a
    /// relaxation in the same round.
    /// </summary>
    public static readonly IReadOnlyList<AdaptationRule> RuleTable =
    [
        new(
            Name: "unresolved-root-normalization",
            Select: (active, previous, observedRunId) => active.FirstOrDefault(record =>
                record.KnowledgeType == KnowledgeType.KnownUnresolved
                && string.Equals(record.SemanticAnchor, RoundKnowledgeExtractor.RootInventoryAnchor, StringComparison.Ordinal)
                && string.Equals(record.SourceRunId, observedRunId, StringComparison.Ordinal)
                && previous.Scope.MaximumDepth >= 1),
            Decide: (record, previous, nextStrategyId, observedTerminalEvidenceRef) =>
                NoOpDecision(
                    "unresolved-root-normalization",
                    record,
                    "unresolved normalization at root; no freedom change justified (safety: no depth increase while "
                    + "the root viewport normalization is unresolved)",
                    previous,
                    nextStrategyId)),
        new(
            Name: "potentially-mutating-or-external-boundary-tighten",
            Select: (active, previous, observedRunId) => active.FirstOrDefault(record =>
                record.KnowledgeType is KnowledgeType.KnownPotentiallyStateMutating or KnowledgeType.KnownExternalBoundary),
            Decide: (record, previous, nextStrategyId, observedTerminalEvidenceRef) =>
            {
                if (previous.Constraints.ProhibitedEffects.SetEquals(MaximalProhibitedEffects))
                {
                    return NoOpDecision(
                        "potentially-mutating-or-external-boundary-tighten",
                        record,
                        "prohibitions already maximal; no scope expansion this round",
                        previous,
                        nextStrategyId);
                }

                // Dangers are only ever TIGHTENED: add whatever prohibited
                // effect class the previous directive is missing (never remove).
                var merged = previous.Constraints.ProhibitedEffects.Union(MaximalProhibitedEffects);
                var next = new StrategyDirective(
                    nextStrategyId,
                    previous.ContractVersion,
                    previous.Objective,
                    previous.Scope,
                    previous.Exploration,
                    new StrategyConstraintSet(previous.Constraints.AllowedInteractionCategories, merged),
                    previous.Completion,
                    previous.Adaptation);
                return new RuleApplication(
                    "potentially-mutating-or-external-boundary-tighten",
                    record.RecordId,
                    new PlanDelta.PlanDelta(
                    [
                        new PlanDeltaChange(
                            PlanDeltaFreedom.ProhibitedEffects,
                            "dangerous/external-boundary classes identified; prohibited effects tightened to the maximal set "
                            + "(safety: dangers are excluded, never learned by execution)",
                            [record.RecordId],
                            [observedTerminalEvidenceRef]),
                    ]),
                    next);
            }),
        new(
            Name: "root-container-exhausted-deepen",
            Select: (active, previous, observedRunId) => active.FirstOrDefault(record =>
                record.KnowledgeType == KnowledgeType.KnownContainer
                && string.Equals(
                    record.SemanticAnchor,
                    string.Concat(RoundKnowledgeExtractor.ContainerAnchorPrefix, previous.Scope.SemanticRoot),
                    StringComparison.Ordinal)
                && string.Equals(record.SourceRunId, observedRunId, StringComparison.Ordinal)),
            Decide: (record, previous, nextStrategyId, observedTerminalEvidenceRef) =>
            {
                if (previous.Scope.MaximumDepth >= MaximumSettingsDepth)
                {
                    return NoOpDecision(
                        "root-container-exhausted-deepen",
                        record,
                        $"bounded Settings depth cap ({MaximumSettingsDepth}) reached; the root container is exhausted "
                        + "at the cap — no depth increase is authorized",
                        previous,
                        nextStrategyId);
                }

                var deeper = previous.Scope.MaximumDepth + 1;
                var next = new StrategyDirective(
                    nextStrategyId,
                    previous.ContractVersion,
                    previous.Objective,
                    new StrategyScope(previous.Scope.ApplicationIdentity, previous.Scope.SemanticRoot, deeper),
                    previous.Exploration,
                    previous.Constraints,
                    previous.Completion,
                    previous.Adaptation);
                return new RuleApplication(
                    "root-container-exhausted-deepen",
                    record.RecordId,
                    new PlanDelta.PlanDelta(
                    [
                        new PlanDeltaChange(
                            PlanDeltaFreedom.Depth,
                            "root container exhausted within the declared depth (fresh observed result); deepen traversal "
                            + "inside the bounded Settings scope",
                            [record.RecordId],
                            [observedTerminalEvidenceRef]),
                    ]),
                    next);
            }),
        new(
            Name: "depth-boundary-children-record-only",
            Select: (active, previous, observedRunId) => active.FirstOrDefault(record =>
                record.KnowledgeType == KnowledgeType.KnownRecordOnly
                && record.SemanticAnchor.StartsWith(RoundKnowledgeExtractor.DepthBoundaryAnchorPrefix, StringComparison.Ordinal)
                && string.Equals(record.SourceRunId, observedRunId, StringComparison.Ordinal)),
            Decide: (record, previous, nextStrategyId, observedTerminalEvidenceRef) =>
                NoOpDecision(
                    "depth-boundary-children-record-only",
                    record,
                    "children recorded honestly at the depth boundary; objective stays ExploreScope with no scope/objective change",
                    previous,
                    nextStrategyId)),
    ];

    /// <summary>
    /// Apply the closed rule set: input = the fixture's ACTIVE knowledge (post
    /// this round's admission), the previous directive, the id of the round
    /// whose evidence was just observed, and the observed round's terminal
    /// evidence ref (the current citation universe); output = the first
    /// applicable rule's (PlanDelta or NO-OP, next-directive). When no rule
    /// consumes knowledge the honest fallback
    /// <see cref="NoConsumedKnowledgeNoOp"/> applies — a real delta is never
    /// invented to inflate adaptation counts (spec "PlanDelta contract").
    /// </summary>
    public static RuleApplication Apply(
        IReadOnlyList<ScenarioKnowledgeRecord> activeKnowledge,
        StrategyDirective previousDirective,
        string nextStrategyId,
        string observedSourceRunId,
        string observedTerminalEvidenceRef)
    {
        ArgumentNullException.ThrowIfNull(activeKnowledge);
        ArgumentNullException.ThrowIfNull(previousDirective);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextStrategyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedSourceRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedTerminalEvidenceRef);

        foreach (var rule in RuleTable)
        {
            if (rule.TryApply(activeKnowledge, previousDirective, nextStrategyId, observedSourceRunId, observedTerminalEvidenceRef) is { } application)
            {
                return application;
            }
        }

        return NoConsumedKnowledgeNoOp(previousDirective, nextStrategyId);
    }

    /// <summary>
    /// The honest fallback when no rule consumed knowledge this round: a
    /// NO_OP_WITH_REASON — the round carries the previous directive's levers
    /// unchanged and a fresh StrategyId (idempotency identity), never an
    /// invented delta.
    /// </summary>
    public static RuleApplication NoConsumedKnowledgeNoOp(StrategyDirective previousDirective, string nextStrategyId)
        => NoOpDecision("no-consumed-knowledge", null, "no consumed active knowledge; no freedom change justified", previousDirective, nextStrategyId);

    private static RuleApplication NoOpDecision(
        string ruleName,
        ScenarioKnowledgeRecord? consumed,
        string reason,
        StrategyDirective previousDirective,
        string nextStrategyId)
        => new(ruleName, consumed?.RecordId, PlanDelta.PlanDelta.NoOp(reason), WithStrategyId(previousDirective, nextStrategyId));

    /// <summary>Rebuild a directive field-by-field (StrategyDirective has a
    /// custom constructor — no <c>with</c> expressions) with a fresh strategy
    /// identity; all levers stay identical (the NO-OP invariant).</summary>
    private static StrategyDirective WithStrategyId(StrategyDirective directive, string strategyId)
        => new(
            strategyId,
            directive.ContractVersion,
            directive.Objective,
            directive.Scope,
            directive.Exploration,
            directive.Constraints,
            directive.Completion,
            directive.Adaptation);
}

/// <summary>One declarative rule entry of the closed rule table: a selector
/// over (active knowledge + previous directive + observed run id) and a
/// decision factory over (the consumed record + previous directive + next
/// strategy id + observed terminal evidence ref).</summary>
public sealed record AdaptationRule(
    string Name,
    Func<IReadOnlyList<ScenarioKnowledgeRecord>, StrategyDirective, string, ScenarioKnowledgeRecord?> Select,
    Func<ScenarioKnowledgeRecord, StrategyDirective, string, string, RuleApplication> Decide)
{
    /// <summary>Run the rule once: the decision when the selector consumed a
    /// record, else null (not applicable).</summary>
    public RuleApplication? TryApply(
        IReadOnlyList<ScenarioKnowledgeRecord> activeKnowledge,
        StrategyDirective previousDirective,
        string nextStrategyId,
        string observedSourceRunId,
        string observedTerminalEvidenceRef)
        => Select(activeKnowledge, previousDirective, observedSourceRunId) is { } record
            ? Decide(record, previousDirective, nextStrategyId, observedTerminalEvidenceRef)
            : null;
}

/// <summary>
/// One rule application: the rule name, the consumed knowledge record id (null
/// for the no-knowledge fallback), the PlanDelta (possibly a
/// <c>NO_OP_WITH_REASON</c> <see cref="PlanDelta.NoOp"/>), and the fully built
/// next directive (fresh StrategyId; levers equal to the previous directive
/// for NO-OP rounds). Validation artifact; no field ever enters the wire.
/// </summary>
public sealed record RuleApplication(
    string RuleName,
    string? ConsumedKnowledgeId,
    PlanDelta.PlanDelta Delta,
    StrategyDirective NextDirective)
{
    /// <summary>True when this application recorded an honest NO-OP.</summary>
    public bool IsNoOp => Delta.IsNoOp;
}