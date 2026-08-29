using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.ValidationHarness.Campaign;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Knowledge;
using UniClaw.Runtime.ValidationHarness.PlanDelta;
using UniClaw.Runtime.ValidationHarness.Reporting;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Scenarios;
using UniClaw.Runtime.ValidationHarness.SettingsBinding;
using UniClaw.Runtime.ValidationHarness.SettingsCampaign.Adaptation;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-P26-G2 (Phase 2.6A) capability tests: the validation-side evidence-informed
/// adaptation planner (spec "PlanDelta contract" + "Phase 2.6A — iterative
/// planning acceptance"; design D5; the Phase 2.5 "UniAgent emulator" mode
/// extended to cross-round adaptation) —
///  1. ≥3 legal adaptation rounds over a deterministic pseudo-history: every
///     PlanningRound validates ACCEPTED, the delta shape is
///     NO-OP (record-only) → depth 1→2 → depth 2→3 → NO-OP (bounded depth
///     cap), every round's directive carries a FRESH StrategyId, and the
///     campaign terminates with recorded bounded scope exhaustion;
///  2. citation resolution: a delta citing an unknown knowledge/evidence ref
///     is rejected by the PlanDeltaValidator (negative control);
///  3. provenance: extractor candidates lacking SourceRunId/EvidenceRefs, or
///     carrying a forbidden admission source, are REJECTED and never forced
///     into the fixture;
///  4. extraction rules: Completed → KnownContainer (root), Failed with
///     normalization/unresolved → KnownUnresolved (settings-root-inventory),
///     Failed with depth-boundary reason → KnownRecordOnly (children at the
///     depth boundary), Failed with launch/foreground → KnownUnresolved
///     (settings-entry) — each with truthful SourceRunId + EvidenceRefs that
///     resolve inside the round's evidence universe;
///  5. rule table: the closed mapping ACTIVE knowledge + previous directive →
///     (PlanDelta or NO-OP, next directive) — unresolved root → NO depth
///     increase, fresh root container → depth +1 (capped), record-only
///     children → honest NO-OP, state-mutating/external-boundary → prohibited
///     effects maximal (or tightened — never relaxed), no consumed knowledge →
///     NO-OP, and stale/historical records never drive a delta.
/// Round outcomes are deterministic fakes (CampaignRunnerTests' composition
/// pattern): scripted terminal facts through the REAL boundary verifier / gate
/// evaluator / report renderer, so the assertion layer always sees
/// genuine-shaped evidence. Assertions check capabilities (contract legality,
/// citation resolution, provenance, honesty, round independence) — never fixed
/// click counts, coordinates, page paths, selectors, or action histories.
/// </summary>
public sealed class SettingsAdaptationPlannerTests
{
    private const string Device = "serial:emulator-5554";

    // ── campaign scope (design D3: scenario/app/capability/version/locale/
    //    android context; the created-from run set is provenance, not reuse
    //    context — Matches excludes it) ────────────────────────────────────────

    private static KnowledgeScope CampaignScope(params string[] runs)
        => new(
            ScenarioId: "settings-real-emulator",
            ApplicationPackage: "com.android.settings",
            SemanticCapabilityId: "uni-claw.settings.semantic",
            SemanticCapabilityVersion: "1",
            AndroidAssumptions: "emulator google_apis;API 35",
            Locale: "en-US",
            CreatedFromRunIds: runs.Length > 0 ? runs : ["campaign"]);

    private static KnowledgeScope RoundScope(string runId)
        => CampaignScope(runId);

    // ── directive builders (StrategyDirective / StrategyScope have custom
    //    constructors — rebuilt field-by-field, no `with` expressions) ─────────

    private static StrategyDirective Directive(string strategyId, int depth) => new(
        strategyId,
        contractVersion: 1,
        new StrategyObjective(StrategyObjectiveKind.ExploreScope),
        new StrategyScope(SettingsStrategyBinding.ApplicationIdentity, SettingsStrategyBinding.RootIdentity, depth),
        ExplorationIntent.ExhaustiveWithinScope,
        new StrategyConstraintSet(
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            AdaptationPlannerRules.MaximalProhibitedEffects),
        new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
        new StrategyAdaptationBoundary(ImmutableHashSet.Create(
            StrategyAdaptationKind.ReconcileBelief,
            StrategyAdaptationKind.ReviseExecutionHypothesis)));

    /// <summary>A conservative directive MISSING the external-boundary
    /// prohibition (the tightening test's previous plan).</summary>
    private static StrategyDirective DirectiveWithoutExternalCrossing(string strategyId, int depth = 1) => new(
        strategyId,
        contractVersion: 1,
        new StrategyObjective(StrategyObjectiveKind.ExploreScope),
        new StrategyScope(SettingsStrategyBinding.ApplicationIdentity, SettingsStrategyBinding.RootIdentity, depth),
        ExplorationIntent.ExhaustiveWithinScope,
        new StrategyConstraintSet(
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            ImmutableHashSet.Create(StrategyProhibitedEffect.StateMutation)),
        new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
        new StrategyAdaptationBoundary(ImmutableHashSet.Create(
            StrategyAdaptationKind.ReconcileBelief,
            StrategyAdaptationKind.ReviseExecutionHypothesis)));

    private static CampaignRoundDirective RoundDirective(StrategyDirective directive)
        => new("Adaptive settings exploration (deterministic test fixture).", directive, Device);

    // ── knowledge record builder (rules-table tests) ───────────────────────────

    private static ScenarioKnowledgeRecord ObservedRecord(
        KnowledgeType type,
        string anchor,
        string runId,
        IReadOnlyList<string>? evidenceRefs = null)
        => new(
            KnowledgeType: type,
            SemanticAnchor: anchor,
            SourceRunId: runId,
            EvidenceRefs: evidenceRefs ?? [$"run:{runId}:terminal"],
            ObservedRole: "test-observed-role",
            Scope: RoundScope(runId),
            Disposition: "test disposition (deterministic fixture)",
            Confidence: 0.8,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: 1);

    // ── deterministic round fakes (CampaignRunnerTests composition pattern) ───

    /// <summary>
    /// Scripted round outcome: the terminal facts are authored by the test;
    /// the boundary proof / G1–G4 gates / rendered report are derived through
    /// the REAL graduated components over those scripted inputs (never a
    /// fabricated verdict). <paramref name="runId"/> may be null to shape a
    /// run-less round (no admission — the planner must refuse to plan).
    /// </summary>
    private static CampaignRoundOutcome FakeRound(
        int roundIndex,
        CampaignRoundDirective directive,
        string? runId,
        RunState terminalState,
        string? terminalReason)
    {
        var run = BuildFakeRun(directive, runId, terminalState, terminalReason);
        var runLog = run.RunCallLog;
        return new CampaignRoundOutcome(
            RoundIndex: roundIndex,
            Directive: directive,
            StrategyId: directive.StrategyId,
            RunId: run.RunId,
            DispatchResult: run.Dispatch,
            Run: run,
            RoundCallLog: runLog,
            Autonomy: new CampaignAutonomyAssertion(
                Passed: run.RunId is not null,
                AcceptedStartCount: run.RunId is not null ? 1 : 0,
                EntriesAfterAdmission: 0,
                EvidenceRefs: [],
                OffendingEvidence: null),
            InvariantAssertions: ImmutableArray.Create(
                new InvariantAssertion(true, CampaignInvariantIds.HistoricalKnowledgeNotCurrentWorldTruth, [], null),
                new InvariantAssertion(true, CampaignInvariantIds.HistoricalResultNotRuntimeActionAuthority, [], null),
                new InvariantAssertion(true, CampaignInvariantIds.RuntimeCompletedNotValidationScenarioPass, [], null),
                new InvariantAssertion(true, CampaignInvariantIds.AutonomousExceptionDispositionNotUniversalRecovery, [], null)),
            AllInvariantsPass: true);
    }

    /// <summary>Compose the fake single-run outcome through the real
    /// verifier / evaluator / renderer (see <see cref="FakeRound"/>).</summary>
    private static ScenarioRunOutcome BuildFakeRun(
        CampaignRoundDirective directive,
        string? runId,
        RunState terminalState,
        string? terminalReason)
    {
        var result = BuildFakeResult(directive, runId, terminalState, terminalReason);
        var payloads = runId is null
            ? ImmutableArray<JsonObject>.Empty
            : [StrategyPayloadJson.Freeze(directive.Directive)];
        var entries = runId is null
            ? ImmutableArray<EmulatorCallLogEntry>.Empty
            : [EmulatorCallLogEntry.Accepted(EmulatorDriver.StartStrategyMethod, $"fake-digest-{directive.StrategyId}", runId, DateTimeOffset.UtcNow)];
        var runLog = EmulatorCallLog.FromEntries(entries);

        var boundary = BoundaryVerifier.Verify(runLog, result, expectedStartCount: runId is null ? 0 : 1, transportedDirectives: payloads);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, runLog, expectedStartCount: runId is null ? 0 : 1, transportedDirectives: payloads);
        var report = new ValidationReport(result, gates, boundary);
        var admission = new StrategyRunAdmissionView(
            Accepted: runId is not null,
            RunId: runId,
            RunState: runId is null ? null : terminalState.ToString(),
            RejectionCode: runId is null ? "NO_RUN" : null,
            RejectionReason: runId is null ? "fake: nothing was admitted (run-less round fixture)" : null);
        var dispatch = runId is null
            ? (DriverDispatchResult)new DriverDispatchResult.TransportFailed("fake: no transport for the run-less round fixture")
            : new DriverDispatchResult.Transported(admission);

        return new ScenarioRunOutcome(
            Dispatch: dispatch,
            Admission: admission,
            RunId: runId,
            StrategyId: directive.StrategyId,
            Result: result,
            RunCallLog: runLog,
            DriverCallLog: runLog,
            TransportedPayloads: payloads,
            Boundary: boundary,
            Gates: gates,
            Report: report,
            ReportJson: ValidationReportRenderer.ToJson(report).ToJsonString(),
            ReportMarkdown: ValidationReportRenderer.ToMarkdown(report));
    }

    /// <summary>
    /// A complete ValidationResult with honest structure: runtime-read fields
    /// classified DirectProjection / DerivedReadModel with surface truth
    /// sources; everything the synthetic surface cannot honestly answer
    /// explicitly Unavailable (never invented). Event kinds are real audited
    /// vocabulary entries with the audited source classification so the
    /// derived boundary/gate proofs stay genuine.
    /// </summary>
    private static ValidationResult BuildFakeResult(
        CampaignRoundDirective directive,
        string? runId,
        RunState terminalState,
        string? terminalReason)
    {
        var unavailable = "fake adaptation-planner surface: section intentionally not composed (deterministic round fixture)";
        var terminalKinds = terminalState == RunState.Completed
            ? new[] { "GoalEvidenceProduced", "RunCompleted" }
            : terminalState == RunState.Failed
                ? new[] { "RunFailed" }
                : Array.Empty<string>();
        var events = terminalKinds
            .Select((kind, index) => new SurfaceRuntimeEvent(
                EventId: $"evt-{index}",
                Kind: kind,
                Sequence: index,
                SourceClassification: RuntimeEventSourceClassification.DerivableFromExistingPublicReadModel.ToString(),
                ObservationSequence: index,
                Reason: terminalState == RunState.Completed && kind == "RunCompleted" ? terminalReason : null,
                EvidenceRefs: ImmutableArray<EvidenceRef>.Empty))
            .ToImmutableArray();
        var depth = directive.Directive.Scope.MaximumDepth;

        return new ValidationResult(
            Admission: new AdmissionSection(
                ResultField<string>.Direct(runId ?? string.Empty, "run.strategy.start admission receipt (result.runId)"),
                ResultField<string>.Direct(directive.StrategyId, "run.strategy.start admission receipt (result.strategyId)"),
                ResultField<bool>.Direct(runId is not null, "run.strategy.start admission receipt (result.accepted)"),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<int>.Direct(depth, "run.strategy.start admission receipt (result.declaredMaximumDepth)")),
            Lifecycle: new LifecycleSection(ResultField<ImmutableArray<SurfaceRuntimeEvent>>.Direct(events, "run.events.drain (projected stream)")),
            Snapshot: new SnapshotSection(
                ResultField<string>.Direct(runId ?? string.Empty, "run.snapshot.get (runId)"),
                ResultField<RunState>.Direct(terminalState, "run.snapshot.get (runState)"),
                ResultField<string?>.Direct(directive.Directive.Scope.SemanticRoot, "run.snapshot.get (currentSemanticPage)"),
                ResultField<Trap?>.Unavailable(unavailable),
                ResultField<GoalSummary?>.Unavailable(unavailable),
                ResultField<DecisionSummary?>.Unavailable(unavailable),
                ResultField<ActionSummary?>.Unavailable(unavailable),
                ResultField<RecoverySummary?>.Unavailable(unavailable),
                ResultField<GoalEvidenceSummary?>.Unavailable(unavailable),
                ResultField<long?>.Direct(1, "run.snapshot.get (currentObservationSequence)"),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<ImmutableArray<string>>.Direct([], "run.snapshot.get (diagnostics)")),
            Trap: new TrapSection(
                ResultField<bool>.Direct(false, "run.trap.get (found)"),
                ResultField<Trap?>.Unavailable(unavailable),
                ResultField<string?>.Direct("none", "run.trap.get (diagnostic)")),
            Evidence: new EvidenceSection(ResultField<ImmutableArray<ValidationEvidenceEntry>>.Direct(
                [],
                "evidence.get (zero refs requested — nothing to fabricate)")),
            Coverage: new CoverageSection(
                ResultField<string>.Direct("tierA-attested", "harness tier composition (surface type)"),
                ResultField<ExplorationLedgerView?>.Unavailable(unavailable),
                ResultField<ImmutableArray<CoverageScopeCounts>>.Unavailable(unavailable),
                ResultField<string?>.Unavailable(unavailable)),
            Terminal: new TerminalSection(
                ResultField<RunState>.Direct(terminalState, "run.snapshot.get (runState)"),
                ResultField<string?>.Direct(terminalReason, "derived: terminal reason from the run's terminal event (derived read model)"),
                ResultField<bool?>.Direct(terminalState == RunState.Completed, "derived: GoalEvidenceProduced preceded RunCompleted (S1)")),
            Boundary: BoundarySection.Placeholder);
    }

    /// <summary>Scripted executor: one authored round outcome per round index
    /// (CampaignRunnerTests pattern) — the campaign assertion layer reads the
    /// round's OWN slice alone.</summary>
    private static CampaignRunExecutor ScriptedExecutor(params (RunState State, string? Reason)[] script)
    {
        return (directive, priorCallLog, _) =>
        {
            var roundIndex = priorCallLog.Count;
            var (state, reason) = script[roundIndex];
            return Task.FromResult(BuildFakeRun(
                directive,
                runId: $"p26-adapt-fake-run-{roundIndex + 1}",
                state,
                reason));
        };
    }

    // ── 1. ≥3 legal adaptation rounds over a deterministic pseudo-history ─────

    [Fact]
    public async Task RunnerLoop_AtLeastThreeLegalAdaptationRounds_EveryPlanningRoundAccepted_TerminatesBounded()
    {
        // Deterministic pseudo-history: r1 fails at the depth boundary
        // (record-only children), r2..r4 complete at depths 1, 2, 3 (root
        // container exhausted each time). Expected adaptation path (all
        // contract-legal):
        //   PR0: NO-OP_WITH_REASON (record-only children at depth boundary)
        //   PR1: Depth 1 → 2 (root container exhausted, fresh evidence)
        //   PR2: Depth 2 → 3 (root container exhausted, fresh evidence)
        //   PR3: NO_OP_WITH_REASON ("bounded Settings depth cap reached") +
        //        mature-plan termination (BoundedScopeExhaustion).
        var planner = new SettingsAdaptationPlanner(CampaignScope(), roundBudget: 5, initialDepth: 1);
        CampaignRoundPlanner adapter = planner.Plan;

        var outcome = await IterativeCampaignRunner.RunAsync(
            adapter,
            ScriptedExecutor(
                (RunState.Failed, "depth boundary reached: no deeper pages remain within the declared depth"),
                (RunState.Completed, null),
                (RunState.Completed, null),
                (RunState.Completed, null),
                (RunState.Completed, null)),
            maxRounds: 7);

        // The campaign ran ≥3 rounds and terminated with a recorded bounded
        // scope exhaustion (planner decision, not a hard bound).
        Assert.True(outcome.Rounds.Length >= 3, $"expected >= 3 rounds, got {outcome.Rounds.Length}");
        Assert.Equal(CampaignTerminationKind.BoundedScopeExhaustion, outcome.Termination.Kind);
        Assert.All(outcome.Rounds, r => Assert.True(r.Autonomy.Passed, r.Autonomy.OffendingEvidence));
        Assert.All(outcome.Rounds, r => Assert.True(r.AllInvariantsPass));

        // ≥3 genuine adaptation rounds, each contract-validated Accepted.
        var planningRounds = planner.PlanningRoundHistory;
        Assert.True(planningRounds.Count >= 3, $"expected >= 3 planning rounds, got {planningRounds.Count}");
        foreach (var planningRound in planningRounds)
        {
            Assert.IsType<PlanDeltaValidation.Accepted>(PlanDeltaValidator.Validate(planningRound));
        }

        // The observed adaptation shape (production-shaped honest path).
        Assert.True(planningRounds[0].PlanDelta.IsNoOp);
        Assert.Contains("depth boundary", planningRounds[0].PlanDelta.NoOpReason, StringComparison.Ordinal);
        Assert.Empty(planningRounds[0].PlanDelta.Changes); // NO-OP carries zero declared changes

        var deepen1 = Assert.Single(planningRounds[1].PlanDelta.Changes);
        Assert.Equal(PlanDeltaFreedom.Depth, deepen1.Freedom);
        Assert.Equal(2, planningRounds[1].NextStrategy.Scope.MaximumDepth);
        Assert.Equal(1, planningRounds[1].PreviousPlan.Scope.MaximumDepth);

        var deepen2 = Assert.Single(planningRounds[2].PlanDelta.Changes);
        Assert.Equal(PlanDeltaFreedom.Depth, deepen2.Freedom);
        Assert.Equal(3, planningRounds[2].NextStrategy.Scope.MaximumDepth);
        Assert.Equal(2, planningRounds[2].PreviousPlan.Scope.MaximumDepth);

        Assert.True(planningRounds[^1].PlanDelta.IsNoOp);
        Assert.Contains("cap", planningRounds[^1].PlanDelta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal(3, planningRounds[^1].NextStrategy.Scope.MaximumDepth);
        Assert.Equal(3, planningRounds[^1].PreviousPlan.Scope.MaximumDepth);

        // Round independence: every directive carries a fresh StrategyId.
        var plannedIds = planningRounds.Select(p => p.NextStrategy.StrategyId).Append("p26-adapt-r1").ToArray();
        Assert.Equal(plannedIds.Length, plannedIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(planningRounds, p => Assert.Equal($"p26-adapt-r{p.RoundIndex + 2}", p.NextStrategy.StrategyId));

        // The planner never emits action/coordinate/selector/path content: all
        // differences across rounds are the validated 8-freedom surface (the
        // Accepted verdict already rejects any non-freedom drift), and the
        // delta descriptions carry no UI-scripting vocabulary.
        foreach (var planningRound in planningRounds)
        {
            foreach (var change in planningRound.PlanDelta.Changes)
            {
                Assert.DoesNotContain("click", change.Description, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("coordinate", change.Description, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("selector", change.Description, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(" path ", change.Description, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void RoundZero_ConservativeInitialDirective_NoPreviousPlan()
    {
        var planner = new SettingsAdaptationPlanner(CampaignScope());
        var result = planner.PlanRound([]);

        Assert.Null(result.Stop);
        Assert.Null(result.PlanningRound);
        var directive = Assert.IsType<CampaignRoundDirective>(result.Next);
        Assert.Equal("p26-adapt-r1", directive.StrategyId);
        Assert.Equal(SettingsStrategyBinding.ApplicationIdentity, directive.Directive.Scope.ApplicationIdentity);
        Assert.Equal(SettingsStrategyBinding.RootIdentity, directive.Directive.Scope.SemanticRoot);
        Assert.Equal(1, directive.Directive.Scope.MaximumDepth);
        Assert.Equal(StrategyObjectiveKind.ExploreScope, directive.Directive.Objective.Kind);
        Assert.Equal(StrategyCompletionKind.ExhaustiveCoverageWithinScope, directive.Directive.Completion.Kind);
        Assert.True(directive.Directive.Constraints.ProhibitedEffects.SetEquals(AdaptationPlannerRules.MaximalProhibitedEffects));
        Assert.Empty(planner.PlanningRoundHistory);
    }

    [Fact]
    public void FreshRootContainer_DepthIncreaseDelta_AcceptedAndCitedToResolvableEvidence()
    {
        var planner = new SettingsAdaptationPlanner(CampaignScope());
        var initial = planner.PlanRound([]).Next!;
        var round = FakeRound(0, initial, "run-1", RunState.Completed, terminalReason: null);

        var result = planner.PlanRound([round]);
        Assert.Null(result.Stop);
        var planningRound = Assert.IsType<PlanningRound>(result.PlanningRound);

        Assert.IsType<PlanDeltaValidation.Accepted>(PlanDeltaValidator.Validate(planningRound));
        var change = Assert.Single(planningRound.PlanDelta.Changes);
        Assert.Equal(PlanDeltaFreedom.Depth, change.Freedom);

        // The depth delta cites the fresh container record (this round's
        // NewKnowledge) and an evidence ref inside the observed universe.
        var recordId = Assert.Single(change.KnowledgeRefs);
        Assert.Contains(recordId, planningRound.NewKnowledge);
        Assert.All(change.EvidenceRefs, evidenceRef => Assert.Contains(evidenceRef, planningRound.ObservedResult.EvidenceRefs));

        Assert.Equal(2, planningRound.NextStrategy.Scope.MaximumDepth);
        Assert.Equal("p26-adapt-r2", planningRound.NextStrategy.StrategyId);
        Assert.Equal(RunState.Completed.ToString(), planningRound.ObservedResult.TerminalState);
        Assert.Contains(planningRound.ObservedResult.EvidenceRefs, r => r == "run:run-1:terminal");
    }

    [Fact]
    public void SameKnowledgeAtDepthCap_MaturePlanNoOp_TerminatesWithBoundedScopeExhaustion()
    {
        var planner = new SettingsAdaptationPlanner(CampaignScope(), roundBudget: 6);
        var directive = RoundDirective(Directive("p26-adapt-r9", depth: 3));
        var round = FakeRound(0, directive, "run-9", RunState.Completed, terminalReason: null);

        var result = planner.PlanRound([round]);
        Assert.Null(result.Next);
        var termination = Assert.IsType<CampaignTermination>(result.Stop);
        Assert.Equal(CampaignTerminationKind.BoundedScopeExhaustion, termination.Kind);
        Assert.Contains("mature plan", termination.Reason, StringComparison.Ordinal);

        var planningRound = Assert.IsType<PlanningRound>(result.PlanningRound);
        Assert.True(planningRound.PlanDelta.IsNoOp);
        Assert.Contains("cap", planningRound.PlanDelta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal(3, planningRound.NextStrategy.Scope.MaximumDepth);
        Assert.IsType<PlanDeltaValidation.Accepted>(PlanDeltaValidator.Validate(planningRound));
    }

    [Fact]
    public void RoundBudgetReached_TerminatesWithBoundedScopeExhaustion_BeforePlanningAgain()
    {
        var planner = new SettingsAdaptationPlanner(CampaignScope(), roundBudget: 2, initialDepth: 1);
        var r1 = planner.PlanRound([]).Next!;
        var round0 = FakeRound(0, r1, "run-a", RunState.Failed, "depth boundary reached");
        var afterFirst = planner.PlanRound([round0]);
        Assert.NotNull(afterFirst.Next);

        var round1 = FakeRound(1, afterFirst.Next!, "run-b", RunState.Completed, terminalReason: null);
        var afterBudget = planner.PlanRound([round0, round1]);
        Assert.Null(afterBudget.Next);
        var termination = Assert.IsType<CampaignTermination>(afterBudget.Stop);
        Assert.Equal(CampaignTerminationKind.BoundedScopeExhaustion, termination.Kind);
        Assert.Contains("budget reached", termination.Reason, StringComparison.Ordinal);
        Assert.Null(afterBudget.PlanningRound);
    }

    [Fact]
    public void RunLessRound_PlannerThrowsInternalError_NoEvidenceToExtract()
    {
        var planner = new SettingsAdaptationPlanner(CampaignScope());
        var initial = planner.PlanRound([]).Next!;
        var runless = FakeRound(0, initial, runId: null, RunState.Idle, terminalReason: null);

        var exception = Assert.Throws<InvalidOperationException>(() => planner.PlanRound([runless]));
        Assert.Contains("no evidence to extract", exception.Message, StringComparison.Ordinal);
    }

    // ── 2. citation resolution (negative control) ─────────────────────────────

    [Fact]
    public void CitationResolution_NegativeControl_UnknownKnowledgeRef_ValidatorRejects()
    {
        var previous = Directive("p26-adapt-prev", 1);
        var next = Directive("p26-adapt-next", 2);
        var observed = new RoundEvidenceSummary(
            runId: "run-x",
            strategyId: previous.StrategyId,
            terminalState: "Completed",
            eventKinds: [],
            evidenceRefs: ["run:run-x:terminal"]);

        var unknownKnowledge = new PlanningRound(
            0, previous, observed, loadedKnowledge: [], newKnowledge: [], remainingUnknowns: [],
            new PlanDelta(
            [
                new PlanDeltaChange(PlanDeltaFreedom.Depth, "negative control: unresolvable knowledge citation", ["k-no-such"], ["run:run-x:terminal"]),
            ]),
            next);
        var rejectedKnowledge = Assert.IsType<PlanDeltaValidation.Rejected>(PlanDeltaValidator.Validate(unknownKnowledge));
        Assert.Contains("k-no-such", rejectedKnowledge.Reason, StringComparison.Ordinal);
        Assert.Contains("unresolvable knowledge ref", rejectedKnowledge.Reason, StringComparison.Ordinal);

        var unknownEvidence = new PlanningRound(
            0, previous, observed, loadedKnowledge: ["k-1"], newKnowledge: [], remainingUnknowns: [],
            new PlanDelta(
            [
                new PlanDeltaChange(PlanDeltaFreedom.Depth, "negative control: unresolvable evidence citation", ["k-1"], ["run:run-x:no-such"]),
            ]),
            next);
        var rejectedEvidence = Assert.IsType<PlanDeltaValidation.Rejected>(PlanDeltaValidator.Validate(unknownEvidence));
        Assert.Contains("run:run-x:no-such", rejectedEvidence.Reason, StringComparison.Ordinal);
        Assert.Contains("unresolvable evidence ref", rejectedEvidence.Reason, StringComparison.Ordinal);
    }

    // ── 3. provenance (knowledge only from ObservedResult evidence) ────────────

    [Fact]
    public void Extractor_ForbiddenSource_EveryCandidateRejected_NotForcedIntoFixture()
    {
        var directive = RoundDirective(Directive("p26-adapt-r1", 1));
        var round = FakeRound(0, directive, "run-1", RunState.Completed, terminalReason: null);

        // Force every candidate through a forbidden source class: the gate
        // rejects each with the explicit marker — extraction never forces.
        var extraction = RoundKnowledgeExtractor.Extract(round, CampaignScope(), KnowledgeAdmissionSource.Guesswork);
        var candidate = Assert.Single(extraction.Candidates);
        var rejected = Assert.IsType<KnowledgeAdmission.Rejected>(candidate.Admission);
        Assert.Equal(KnowledgeAdmissionSource.Guesswork, rejected.ForbiddenSource);

        // The fixture side re-runs the gate: a forbidden-source candidate is
        // never admitted, and the fixture stays empty.
        var fixture = new ScenarioKnowledgeFixture(CampaignScope());
        Assert.IsType<KnowledgeAdmission.Rejected>(fixture.Admit(candidate.Record, KnowledgeAdmissionSource.Guesswork));
        Assert.Empty(fixture.Records);
    }

    [Fact]
    public void AdmissionGate_MissingProvenance_Rejected_NotAdmitted()
    {
        var noSourceRunId = new ScenarioKnowledgeRecord(
            KnowledgeType: KnowledgeType.KnownContainer,
            SemanticAnchor: "settings.container:Settings",
            SourceRunId: "",
            EvidenceRefs: ["run:run-x:terminal"],
            ObservedRole: "test",
            Scope: RoundScope("run-x"),
            Disposition: "test",
            Confidence: 0.9,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: 1);
        var rejectedSource = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(noSourceRunId, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("SourceRunId", rejectedSource.Reason, StringComparison.Ordinal);

        var noEvidenceRefs = new ScenarioKnowledgeRecord(
            KnowledgeType: KnowledgeType.KnownContainer,
            SemanticAnchor: "settings.container:Settings",
            SourceRunId: "run-x",
            EvidenceRefs: [],
            ObservedRole: "test",
            Scope: RoundScope("run-x"),
            Disposition: "test",
            Confidence: 0.9,
            ValidityAssumption: "stable across frames",
            Version: 1,
            Status: KnowledgeStatus.Active,
            AdmissionOrdinal: 1);
        var rejectedRefs = Assert.IsType<KnowledgeAdmission.Rejected>(
            KnowledgeAdmission.TryAdmit(noEvidenceRefs, KnowledgeAdmissionSource.ObservedResult));
        Assert.Contains("EvidenceRef", rejectedRefs.Reason, StringComparison.Ordinal);

        // A provenance-less record never enters the fixture (store re-runs the gate).
        var fixture = new ScenarioKnowledgeFixture(CampaignScope());
        Assert.IsType<KnowledgeAdmission.Rejected>(fixture.Admit(noEvidenceRefs, KnowledgeAdmissionSource.ObservedResult));
        Assert.Empty(fixture.Records);
    }

    // ── 4. extraction rules (typed, conservative, refs resolve in-universe) ───

    [Fact]
    public void Extractor_CompletedRound_ProposesContainerCandidate_WithTruthfulProvenance()
    {
        var directive = RoundDirective(Directive("p26-adapt-r1", 1));
        var round = FakeRound(0, directive, "run-1", RunState.Completed, terminalReason: null);

        var extraction = RoundKnowledgeExtractor.Extract(round, CampaignScope());
        var candidate = Assert.Single(extraction.Candidates);
        var admitted = Assert.IsType<KnowledgeAdmission.Admitted>(candidate.Admission);
        Assert.Equal(KnowledgeType.KnownContainer, admitted.Record.KnowledgeType);
        Assert.Equal("settings.container:Settings", admitted.Record.SemanticAnchor);
        Assert.Equal("run-1", admitted.Record.SourceRunId);
        Assert.Equal(0.9, admitted.Record.Confidence);

        // Every cited evidence ref resolves inside the round's own universe.
        Assert.All(admitted.Record.EvidenceRefs, evidenceRef =>
            Assert.Contains(evidenceRef, extraction.EvidenceSummary.EvidenceRefs));
        Assert.Equal(RunState.Completed.ToString(), extraction.EvidenceSummary.TerminalState);
        Assert.Contains("RunCompleted", extraction.EvidenceSummary.EventKinds);
    }

    [Theory]
    [InlineData(
        "normalization unresolved at the settings root viewport",
        KnowledgeType.KnownUnresolved,
        RoundKnowledgeExtractor.RootInventoryAnchor,
        0.7)]
    [InlineData(
        "depth boundary reached: no deeper pages remain within the declared depth",
        KnowledgeType.KnownRecordOnly,
        "settings.depth-boundary:Settings:depth:1",
        0.8)]
    [InlineData(
        "launch failed: the settings entry activity never reached the foreground",
        KnowledgeType.KnownUnresolved,
        RoundKnowledgeExtractor.SettingsEntryAnchor,
        0.7)]
    public void Extractor_FailedTerminalReasons_ProposeExpectedKnowledgeCandidates(
        string terminalReason,
        KnowledgeType expectedType,
        string expectedAnchor,
        double expectedConfidence)
    {
        var directive = RoundDirective(Directive("p26-adapt-r1", 1));
        var round = FakeRound(0, directive, "run-1", RunState.Failed, terminalReason);

        var extraction = RoundKnowledgeExtractor.Extract(round, CampaignScope());
        var candidate = Assert.Single(extraction.Candidates);
        var admitted = Assert.IsType<KnowledgeAdmission.Admitted>(candidate.Admission);
        Assert.Equal(expectedType, admitted.Record.KnowledgeType);
        Assert.Equal(expectedAnchor, admitted.Record.SemanticAnchor);
        Assert.Equal("run-1", admitted.Record.SourceRunId);
        Assert.Equal(expectedConfidence, admitted.Record.Confidence, precision: 10);
        Assert.All(admitted.Record.EvidenceRefs, evidenceRef =>
            Assert.Contains(evidenceRef, extraction.EvidenceSummary.EvidenceRefs));
    }

    // ── 5. rule table: ACTIVE knowledge + previous directive → delta ──────────

    [Fact]
    public void Rules_UnresolvedRootNormalization_NoOpWithReason_DepthNeverIncreased()
    {
        var unresolved = ObservedRecord(KnowledgeType.KnownUnresolved, RoundKnowledgeExtractor.RootInventoryAnchor, "run-7");
        var previous = Directive("p26-adapt-prev", 1);

        var application = AdaptationPlannerRules.Apply(
            [unresolved], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        Assert.Equal("unresolved-root-normalization", application.RuleName);
        Assert.True(application.IsNoOp);
        Assert.Contains("unresolved normalization at root", application.Delta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal("p26-adapt-next", application.NextDirective.StrategyId);
        Assert.Equal(1, application.NextDirective.Scope.MaximumDepth);
    }

    [Fact]
    public void Rules_FreshRootContainer_DepthIncrease_CappedAtSettingsDepthCap()
    {
        var fresh = ObservedRecord(KnowledgeType.KnownContainer, "settings.container:Settings", "run-7");
        var previous = Directive("p26-adapt-prev", 2);

        var application = AdaptationPlannerRules.Apply(
            [fresh], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        Assert.Equal("root-container-exhausted-deepen", application.RuleName);
        Assert.False(application.IsNoOp);
        var change = Assert.Single(application.Delta.Changes);
        Assert.Equal(PlanDeltaFreedom.Depth, change.Freedom);
        Assert.Equal(3, application.NextDirective.Scope.MaximumDepth);
        Assert.Contains("run:run-7:terminal", change.EvidenceRefs);

        // At the cap the same fresh knowledge yields an honest NO-OP — the
        // bounded Settings scope never authorizes a deeper descent.
        var atCap = Directive("p26-adapt-prev", AdaptationPlannerRules.MaximumSettingsDepth);
        var capped = AdaptationPlannerRules.Apply(
            [fresh], atCap, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");
        Assert.True(capped.IsNoOp);
        Assert.Contains("cap", capped.Delta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal(AdaptationPlannerRules.MaximumSettingsDepth, capped.NextDirective.Scope.MaximumDepth);
    }

    [Fact]
    public void Rules_StaleKnowledge_NeverDrivesADelta_NoConsumedKnowledgeNoOp()
    {
        // A root-container record from an EARLIER run stays ACTIVE in the
        // fixture but is not fresh for this round: history never substitutes
        // for fresh runtime evidence, so it is not consumed for a depth delta.
        var stale = ObservedRecord(KnowledgeType.KnownContainer, "settings.container:Settings", "run-1");
        var previous = Directive("p26-adapt-prev", 1);

        var application = AdaptationPlannerRules.Apply(
            [stale], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        Assert.Equal("no-consumed-knowledge", application.RuleName);
        Assert.True(application.IsNoOp);
        Assert.Contains("no consumed active knowledge", application.Delta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal(1, application.NextDirective.Scope.MaximumDepth);
        Assert.Null(application.ConsumedKnowledgeId);
    }

    [Fact]
    public void Rules_DepthBoundaryChildrenRecordOnly_NoOpWithReason()
    {
        var recordOnly = ObservedRecord(KnowledgeType.KnownRecordOnly, "settings.depth-boundary:Settings:depth:1", "run-7");
        var previous = Directive("p26-adapt-prev", 1);

        var application = AdaptationPlannerRules.Apply(
            [recordOnly], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        Assert.Equal("depth-boundary-children-record-only", application.RuleName);
        Assert.True(application.IsNoOp);
        Assert.Contains("children recorded honestly at the depth boundary", application.Delta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal(StrategyObjectiveKind.ExploreScope, application.NextDirective.Objective.Kind);
        Assert.Equal(previous.Scope.SemanticRoot, application.NextDirective.Scope.SemanticRoot);
    }

    [Fact]
    public void Rules_StateMutatingKnowledge_ProhibitionsAlreadyMaximal_NoOpWithReason()
    {
        var mutating = ObservedRecord(
            KnowledgeType.KnownPotentiallyStateMutating,
            "settings.danger:owner-reset",
            "run-7");
        var previous = Directive("p26-adapt-prev", 1);

        var application = AdaptationPlannerRules.Apply(
            [mutating], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        Assert.Equal("potentially-mutating-or-external-boundary-tighten", application.RuleName);
        Assert.True(application.IsNoOp);
        Assert.Contains("prohibitions already maximal", application.Delta.NoOpReason, StringComparison.Ordinal);
        Assert.True(application.NextDirective.Constraints.ProhibitedEffects.SetEquals(previous.Constraints.ProhibitedEffects));
    }

    [Fact]
    public void Rules_StateMutatingKnowledge_MissingProhibition_TighteningDelta_ValidatedAccepted()
    {
        var mutating = ObservedRecord(
            KnowledgeType.KnownPotentiallyStateMutating,
            "settings.danger:owner-reset",
            "run-7");
        var previous = DirectiveWithoutExternalCrossing("p26-adapt-prev");

        var application = AdaptationPlannerRules.Apply(
            [mutating], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        // Dangers are only ever TIGHTENED: the missing external-boundary
        // prohibition is added — never removed, never relaxed.
        Assert.Equal("potentially-mutating-or-external-boundary-tighten", application.RuleName);
        Assert.False(application.IsNoOp);
        var change = Assert.Single(application.Delta.Changes);
        Assert.Equal(PlanDeltaFreedom.ProhibitedEffects, change.Freedom);
        Assert.Contains(StrategyProhibitedEffect.ExternalBoundaryCrossing, application.NextDirective.Constraints.ProhibitedEffects);
        Assert.Contains(StrategyProhibitedEffect.StateMutation, application.NextDirective.Constraints.ProhibitedEffects);

        // The tightened round is contract-legal when recorded.
        var round = new PlanningRound(
            0,
            previous,
            new RoundEvidenceSummary("run-7", previous.StrategyId, "Failed", ["RunFailed"], ["run:run-7:terminal"]),
            loadedKnowledge: [mutating.RecordId],
            newKnowledge: [],
            remainingUnknowns: [],
            application.Delta,
            application.NextDirective);
        Assert.IsType<PlanDeltaValidation.Accepted>(PlanDeltaValidator.Validate(round));
    }

    [Fact]
    public void Rules_NoActiveKnowledge_NoOpWithReason()
    {
        var previous = Directive("p26-adapt-prev", 1);
        var application = AdaptationPlannerRules.Apply(
            [], previous, "p26-adapt-next", observedSourceRunId: "run-7", observedTerminalEvidenceRef: "run:run-7:terminal");

        Assert.Equal("no-consumed-knowledge", application.RuleName);
        Assert.True(application.IsNoOp);
        Assert.Contains("no consumed active knowledge", application.Delta.NoOpReason, StringComparison.Ordinal);
        Assert.Equal(1, application.NextDirective.Scope.MaximumDepth);
    }
}