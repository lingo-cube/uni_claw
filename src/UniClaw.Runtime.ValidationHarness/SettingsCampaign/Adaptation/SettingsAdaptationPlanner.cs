using System.Collections.Immutable;
using System.Globalization;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.ValidationHarness.Campaign;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using UniClaw.Runtime.ValidationHarness.PlanDelta;
using UniClaw.Runtime.ValidationHarness.SettingsBinding;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign.Adaptation;

/// <summary>
/// The Phase 2.6 validation-side evidence-informed adaptation planner for the
/// Settings campaign (spec "Phase 2.6A — iterative planning acceptance" +
/// "Frozen iterative loop with independent runs"; design D5 "PlanDelta
/// contract"; the Phase 2.5 "UniAgent emulator" mode, extended to Phase 2.6
/// cross-round plan adaptation). It composes the loop state — the scenario
/// fixture + the campaign <see cref="KnowledgeScope"/> — and, per round:
///  1. extracts knowledge candidates from the LAST round's own frozen evidence
///     (<see cref="RoundKnowledgeExtractor"/> — provenance-gated admission);
///  2. admits them into the <see cref="ScenarioKnowledgeFixture"/> (never
///     forced; the gate outcome is recorded);
///  3. applies the closed <see cref="AdaptationPlannerRules"/> table over the
///     ACTIVE knowledge + the previous directive → PlanDelta (or an honest
///     NO_OP_WITH_REASON) + the next directive;
///  4. records the <see cref="PlanningRound"/> (PreviousPlan, ObservedResult,
///     LoadedKnowledge, NewKnowledge, RemainingUnknowns, PlanDelta,
///     NextStrategy) and VALIDATES it with <see cref="PlanDeltaValidator"/> —
///     a Rejected validation is an internal error: the closed rules must only
///     emit contract-legal deltas (the tests prove it);
///  5. terminates the loop on the round budget or on a mature plan (last round
///     Completed at the bounded Settings depth cap) with a recorded
///     <see cref="CampaignTerminationKind.BoundedScopeExhaustion"/>.
///
/// THIS IS NOT A RUNTIME PLANNER (spec "Validation tooling, never runtime or
/// planning capability"): the planner is a pure reader of prior round outcomes
/// and only ever PRODUCES directives — it never touches Runtime state, never
/// injects knowledge into Runtime input, and never emits UI actions,
/// coordinates, selectors, or fixed navigation paths. Every next directive
/// starts from the CONSERVATIVE Settings shape (mirror of
/// <c>SettingsCampaignProgram.ConservativeDirective</c> — app identity
/// "com.android.settings", semantic root "Settings", navigate-only, both
/// prohibited effects) and carries a FRESH StrategyId per round ("p26-adapt-rN",
/// spec round independence; idempotency is UniAgent-owned).
/// </summary>
public sealed class SettingsAdaptationPlanner
{
    private const string StrategyPrefix = "p26-adapt";
    private const string DeviceSelector = "serial:emulator-5554";
    private const string GoalTemplate =
        "Adaptively explore the bounded Android Settings scope (round {0}); deepen only on fresh root-exhaustion "
        + "evidence; never mutate state.";

    private readonly ScenarioKnowledgeFixture _fixture;
    private readonly KnowledgeScope _scope;
    private readonly int _roundBudget;
    private readonly int _initialDepth;
    private readonly List<PlanningRound> _planningRounds = [];

    /// <summary>Create a planner bound to the campaign scope. The round budget
    /// bounds how many rounds the planner authorizes before terminating with
    /// bounded scope exhaustion; the initial depth is the conservative starting
    /// traversal depth (≥ 1, ≤ the Settings depth cap).</summary>
    public SettingsAdaptationPlanner(
        KnowledgeScope scope,
        int roundBudget = 5,
        int initialDepth = 1)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfLessThan(roundBudget, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDepth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(initialDepth, AdaptationPlannerRules.MaximumSettingsDepth);

        _scope = scope;
        _roundBudget = roundBudget;
        _initialDepth = initialDepth;
        _fixture = new ScenarioKnowledgeFixture(scope);
    }

    /// <summary>The scenario fixture this planner composes (advisory store;
    /// TEST_KNOWLEDGE != RUNTIME_TRUTH / ACTION_AUTHORITY).</summary>
    public ScenarioKnowledgeFixture Fixture => _fixture;

    /// <summary>The campaign knowledge scope (scenario/app/capability/version/
    /// locale/android context the fixture is bound to).</summary>
    public KnowledgeScope Scope => _scope;

    /// <summary>Every validated <see cref="PlanningRound"/> this planner
    /// produced, in campaign order (the evidence artifact of this planner
    /// instance).</summary>
    public IReadOnlyList<PlanningRound> PlanningRoundHistory => _planningRounds;

    /// <summary>
    /// The <see cref="CampaignRoundPlanner"/> adapter: prior round outcomes
    /// (immutable, per-round-asserted evidence) → the next round directive or
    /// an explicit termination. This is the delegate the leader wires into the
    /// <see cref="IterativeCampaignRunner"/> after both work items land.
    /// </summary>
    public CampaignPlannerDecision Plan(
        IReadOnlyList<CampaignRoundOutcome> priorRounds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = PlanRound(priorRounds);
        if (result.Next is { } next)
        {
            return new CampaignPlannerDecision.Continue(next);
        }

        return new CampaignPlannerDecision.Stop(
            result.Stop ?? throw new InvalidOperationException(
                "planner internal error: a stop result without a termination is impossible by construction."));
    }

    /// <summary>
    /// Plan one round from the immutable prior outcomes. Round 0 authors the
    /// conservative initial directive (no previous plan to revise — no
    /// PlanningRound is produced for it); later rounds run
    /// extract → admit → apply rules → record+validate PlanningRound. Returns
    /// the next directive (Continue) or the recorded termination (Stop); a
    /// Stop after the mature-plan planning round still carries the final
    /// PlanningRound as evidence.
    /// </summary>
    public PlanRoundResult PlanRound(IReadOnlyList<CampaignRoundOutcome> priorRounds)
    {
        ArgumentNullException.ThrowIfNull(priorRounds);
        var rounds = priorRounds;

        if (rounds.Count == 0)
        {
            var initial = BuildConservativeDirective(StrategyIdForRoundIndex(0), _initialDepth);
            return PlanRoundResult.ContinueWith(ToRoundDirective(0, initial));
        }

        if (rounds.Count >= _roundBudget)
        {
            return PlanRoundResult.StopWith(CampaignTermination.BoundedScopeExhaustion(
                $"planned adaptation round budget reached ({_roundBudget}); the bounded settings campaign is fully traversed."));
        }

        var last = rounds[^1];
        if (last.RunId is null)
        {
            throw new InvalidOperationException(
                $"planning round {last.RoundIndex}: the planner is a pure reader of observed results; a round without an "
                + "admitted run carries no evidence to extract (RunId is null).");
        }

        // 1. extract knowledge candidates from the LAST round's own evidence
        //    (the round's citation universe + provenance-gated candidates).
        var extraction = RoundKnowledgeExtractor.Extract(last, _scope);

        // 2. admit into the fixture — never forced: the stateless gate outcome
        //    is returned by the extraction, and a store-level rejection (e.g.
        //    duplicate canonical content) simply withholds the NewKnowledge
        //    entry (old knowledge is never re-applied as new).
        var activeBefore = _fixture.ActiveKnowledge(_scope);
        var loadedKnowledge = activeBefore.Select(record => record.RecordId).ToArray();
        var newKnowledge = new List<string>();
        foreach (var candidate in extraction.Candidates)
        {
            if (candidate.Admission is KnowledgeAdmission.Rejected)
            {
                continue;
            }

            if (_fixture.Admit(candidate.Record) is KnowledgeAdmission.Admitted admitted)
            {
                newKnowledge.Add(admitted.Record.RecordId);
            }
        }

        // 3. closed rule set: ACTIVE knowledge + previous directive → delta.
        var previousDirective = last.Directive.Directive;
        var application = AdaptationPlannerRules.Apply(
            _fixture.ActiveKnowledge(_scope),
            previousDirective,
            StrategyIdForRoundIndex(rounds.Count),
            observedSourceRunId: last.RunId,
            observedTerminalEvidenceRef: RoundKnowledgeExtractor.TerminalRef(last.RunId));

        // Honest remaining unknowns: every ACTIVE KnownUnresolved anchor
        // (unresolved items are accounted, never guessed away).
        var remainingUnknowns = _fixture.ActiveKnowledge(_scope)
            .Where(record => record.KnowledgeType == KnowledgeType.KnownUnresolved)
            .Select(record => record.SemanticAnchor)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var planningRound = new PlanningRound(
            roundIndex: last.RoundIndex,
            previousPlan: previousDirective,
            observedResult: extraction.EvidenceSummary,
            loadedKnowledge: loadedKnowledge,
            newKnowledge: newKnowledge,
            remainingUnknowns: remainingUnknowns,
            planDelta: application.Delta,
            nextStrategy: application.NextDirective);

        // 4. the PlanDelta contract is a HARD gate: a Rejected validation is
        //    an internal error — the closed rules must only emit legal deltas.
        if (PlanDeltaValidator.Validate(planningRound) is PlanDeltaValidation.Rejected rejected)
        {
            throw new InvalidOperationException(
                $"planner internal error: the closed rule set emitted a contract-illegal PlanDelta "
                + $"('{application.RuleName}', observed round {planningRound.RoundIndex}): {rejected.Reason} — "
                + "the rules must only emit legal deltas; this is a harness bug, never a silent record.");
        }

        _planningRounds.Add(planningRound);

        // 5. termination: the mature plan reached the bounded Settings depth
        //    cap with a Completed last round → bounded scope exhaustion (the
        //    final NO-OP planning round is the evidence of WHY the loop stops).
        if (last.Result.Terminal.TerminalState.Value == RunState.Completed
            && previousDirective.Scope.MaximumDepth >= AdaptationPlannerRules.MaximumSettingsDepth)
        {
            return PlanRoundResult.StopWith(
                CampaignTermination.BoundedScopeExhaustion(
                    $"mature plan reached bounded exhaustion: the root container was exhausted at the bounded Settings "
                    + $"depth cap ({AdaptationPlannerRules.MaximumSettingsDepth}); no further depth is authorized."),
                planningRound);
        }

        return PlanRoundResult.ContinueWith(
            ToRoundDirective(rounds.Count, application.NextDirective),
            planningRound);
    }

    /// <summary>The fresh strategy identity for the round being authored
    /// (zero-based round index → "p26-adapt-rN"): round 0 = "p26-adapt-r1",
    /// round n = "p26-adapt-r{n+1}".</summary>
    public static string StrategyIdForRoundIndex(int roundIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(roundIndex);
        return $"{StrategyPrefix}-r{roundIndex + 1}";
    }

    /// <summary>
    /// The conservative initial/next directive — mirrors
    /// <c>SettingsCampaignProgram.ConservativeDirective</c> exactly (app
    /// identity + semantic root from <see cref="SettingsStrategyBinding"/>,
    /// ExploreScope objective, exhaustive-within-scope completion, navigate-only
    /// interaction, BOTH prohibited effects, the two runtime-local adaptation
    /// permissions).
    /// </summary>
    private static StrategyDirective BuildConservativeDirective(string strategyId, int depth) => new(
        strategyId,
        contractVersion: 1,
        new StrategyObjective(StrategyObjectiveKind.ExploreScope),
        new StrategyScope(
            SettingsStrategyBinding.ApplicationIdentity,
            SettingsStrategyBinding.RootIdentity,
            depth),
        ExplorationIntent.ExhaustiveWithinScope,
        new StrategyConstraintSet(
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            AdaptationPlannerRules.MaximalProhibitedEffects),
        new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
        new StrategyAdaptationBoundary(ImmutableHashSet.Create(
            StrategyAdaptationKind.ReconcileBelief,
            StrategyAdaptationKind.ReviseExecutionHypothesis)));

    private CampaignRoundDirective ToRoundDirective(int roundIndex, StrategyDirective directive)
        => new(
            string.Format(CultureInfo.InvariantCulture, GoalTemplate, roundIndex + 1),
            directive,
            DeviceSelector);
}

/// <summary>One planner decision's full shape: either author the next round
/// directive (Continue), or terminate with the recorded termination (Stop).
/// <see cref="PlanningRound"/> is non-null exactly for decisions produced from
/// evidence (round 0's initial directive has no previous plan to revise). A
/// mature-plan Stop still carries its final planning round as evidence.</summary>
public sealed record PlanRoundResult(
    CampaignRoundDirective? Next,
    CampaignTermination? Stop,
    PlanningRound? PlanningRound)
{
    /// <summary>Continue: author the next round directive.</summary>
    public static PlanRoundResult ContinueWith(CampaignRoundDirective next, PlanningRound? planningRound = null)
        => new(next, null, planningRound);

    /// <summary>Stop: terminate the loop with the recorded termination.</summary>
    public static PlanRoundResult StopWith(CampaignTermination stop, PlanningRound? planningRound = null)
        => new(null, stop, planningRound);
}