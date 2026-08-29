using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Campaign;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Reporting;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Scenarios;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-P26-B (Phase 2.6) campaign capability tests: the iterative campaign
/// runner drives N independent Runtime Runs through the graduated Phase 2.5
/// chain and re-asserts, PER ROUND, exactly one accepted <c>run.strategy.start</c>,
/// zero mid-run intervention, and the four frozen invariants with evidence refs
/// drawn from the round's own outputs. Real Tier-A host runs cover loop
/// independence / autonomy / genuine chain reuse; a fake executor (the
/// ScenarioRunnerTests composition pattern, scripting round slices) engineers
/// the violation cases the real host cannot produce. Assertions check
/// capabilities (independence, autonomy, termination closure, per-round
/// re-assertion) — never fixed click counts, coordinates, page text, UI paths,
/// or action histories.
/// </summary>
public sealed class CampaignRunnerTests
{
    // ── Loop independence (real Tier-A chain; spec "Frozen iterative loop with
    //    independent runs" + "Every run is autonomous and independent") ────────

    [Fact]
    public async Task ThreeRounds_DistinctStrategyAndRunIds_MonotonicCampaignLog_OneAcceptedStartPerRound_AllInvariantsPass()
    {
        // ── EvidenceFixture + Runtime Execution ──────────────────────────────
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(),
            FixtureComposition.CreateCompiler());
        var outcome = await IterativeCampaignRunner.RunAsync(
            PlanThreeRoundsThenBoundedExhaustion,
            CampaignRunExecutors.TierA(host),
            maxRounds: 5);

        // ── Evidence Evaluation ──────────────────────────────────────────────

        // Bounded scope exhaustion recorded with a reason (spec termination #1).
        Assert.Equal(CampaignTerminationKind.BoundedScopeExhaustion, outcome.Termination.Kind);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Termination.Reason));

        // N rounds → N distinct StrategyIds, N distinct RunIds.
        Assert.Equal(3, outcome.Rounds.Length);
        Assert.Equal(3, outcome.Rounds.Select(r => r.StrategyId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(outcome.Rounds, r => Assert.True(r.AdmittedRun));
        Assert.Equal(3, outcome.Rounds.Select(r => r.RunId).Where(id => id is not null).Distinct(StringComparer.Ordinal).Count());

        // Every round re-asserts the four frozen invariants (each id asserted,
        // distinct, passing) — per-round, never carried over.
        var expectedIds = new[]
        {
            CampaignInvariantIds.HistoricalKnowledgeNotCurrentWorldTruth,
            CampaignInvariantIds.HistoricalResultNotRuntimeActionAuthority,
            CampaignInvariantIds.RuntimeCompletedNotValidationScenarioPass,
            CampaignInvariantIds.AutonomousExceptionDispositionNotUniversalRecovery,
        };
        foreach (var round in outcome.Rounds)
        {
            Assert.Equal(4, round.InvariantAssertions.Length);
            Assert.Equal(expectedIds.OrderBy(id => id), round.InvariantAssertions.Select(a => a.InvariantId).OrderBy(id => id));
            Assert.All(round.InvariantAssertions, a => Assert.True(a.Passed, $"{a.InvariantId}: {a.Reason}"));
            Assert.True(round.AllInvariantsPass);
        }

        // Each round's OWN slice: exactly one accepted start, zero post-admission
        // driver/wire calls; the campaign call log grows monotonically.
        for (var index = 0; index < outcome.Rounds.Length; index++)
        {
            var round = outcome.Rounds[index];
            Assert.Equal(index, round.RoundIndex);
            Assert.Equal(1, round.RoundCallLog.Count);
            var entry = Assert.Single(round.RoundCallLog.Entries);
            Assert.Equal(EmulatorDriver.StartStrategyMethod, entry.Method);
            Assert.Equal(EmulatorCallOutcome.Accepted, entry.Outcome);
            Assert.Equal(round.RunId, entry.Detail);

            Assert.True(round.Autonomy.Passed, round.Autonomy.OffendingEvidence);
            Assert.Equal(1, round.Autonomy.AcceptedStartCount);
            Assert.Equal(0, round.Autonomy.EntriesAfterAdmission);

            // Monotonic whole-campaign log across rounds (S3 cross-run surface).
            Assert.Equal(index + 1, round.Run.DriverCallLog.Count);

            // The graduated chain gates still hold on the real chain.
            Assert.True(round.Gates.AllPass, $"round {index}: {round.Gates.G1.OffendingEvidence ?? round.Gates.G2.OffendingEvidence ?? round.Gates.G3.OffendingEvidence ?? round.Gates.G4.OffendingEvidence}");
        }

        // One immutable whole-campaign call log: exactly the three accepted starts.
        Assert.Equal(3, outcome.CampaignCallLog.Count);
        Assert.All(outcome.CampaignCallLog.Entries, e => Assert.Equal(EmulatorCallOutcome.Accepted, e.Outcome));
    }

    // ── Zero mid-run intervention per round (engineered slice; assertor works
    //    over the round's OWN slice, so a poisoned round fails alone) ─────────

    [Fact]
    public async Task EngineeredPostAdmissionDriverCall_FailsThatRoundsAutonomy_CleanRoundsStillPass()
    {
        var outcome = await IterativeCampaignRunner.RunAsync(
            PlanThreeRounds(
                "evh-intervene-1",
                "evh-intervene-2",
                "evh-intervene-3"),
            FakeExecutor((directive, roundIndex) => directive.StrategyId switch
            {
                "evh-intervene-2" => PostAdmissionInterventionSpec(),
                _ => CleanSpec(),
            }),
            maxRounds: 5);

        Assert.Equal(3, outcome.Rounds.Length);

        // Round 0 and 2: autonomy passes (zero post-admission entries).
        Assert.True(outcome.Rounds[0].Autonomy.Passed);
        Assert.Equal(0, outcome.Rounds[0].Autonomy.EntriesAfterAdmission);
        Assert.True(outcome.Rounds[2].Autonomy.Passed);
        Assert.Equal(0, outcome.Rounds[2].Autonomy.EntriesAfterAdmission);

        // Round 1: the engineered post-admission driver call is caught — from
        // THAT round's slice, not a prior round's conclusion.
        var intervened = outcome.Rounds[1];
        Assert.False(intervened.Autonomy.Passed);
        Assert.Equal(1, intervened.Autonomy.AcceptedStartCount);
        Assert.Equal(1, intervened.Autonomy.EntriesAfterAdmission);
        Assert.NotNull(intervened.Autonomy.OffendingEvidence);
        Assert.Contains(intervened.Autonomy.EvidenceRefs, r => r.Contains(ForeignControlMethod, StringComparison.Ordinal));
    }

    // ── Per-run re-assertion: a round whose composed result violates an
    //    invariant fails THAT round's assertion while other rounds still pass ──

    [Fact]
    public async Task PoisonedKnowledgeTruthSource_FailsThatRoundsHistoricalKnowledgeInvariant_OtherRoundsPass()
    {
        var outcome = await IterativeCampaignRunner.RunAsync(
            PlanThreeRounds("evh-knowledge-1", "evh-knowledge-2", "evh-knowledge-3"),
            FakeExecutor((directive, roundIndex) => directive.StrategyId switch
            {
                "evh-knowledge-2" => PoisonedKnowledgeSpec(),
                _ => CleanSpec(),
            }),
            maxRounds: 5);

        Assert.Equal(3, outcome.Rounds.Length);

        // Round 1: the goal prose leaked into a result field's truth source →
        // I1 fails for that round with evidence refs + reason; autonomy is
        // untouched (the violation is isolated, not blanket).
        var poisoned = outcome.Rounds[1];
        var i1 = Assert.Single(poisoned.InvariantAssertions, a => a.InvariantId == CampaignInvariantIds.HistoricalKnowledgeNotCurrentWorldTruth);
        Assert.False(i1.Passed);
        Assert.NotEmpty(i1.EvidenceRefs);
        Assert.False(string.IsNullOrWhiteSpace(i1.Reason));
        Assert.Contains("goal prose", i1.Reason);
        Assert.False(poisoned.AllInvariantsPass);
        Assert.True(poisoned.Autonomy.Passed, poisoned.Autonomy.OffendingEvidence);

        // Rounds 0 and 2 re-assert cleanly — a failure is never campaign-cached.
        foreach (var clean in new[] { outcome.Rounds[0], outcome.Rounds[2] })
        {
            Assert.All(clean.InvariantAssertions, a => Assert.True(a.Passed, $"{a.InvariantId}: {a.Reason}"));
            Assert.True(clean.AllInvariantsPass);
        }
    }

    [Fact]
    public async Task ForgedMergedScenarioVerdict_FailsThatRoundsRuntimeVsScenarioInvariant_NoScenarioVerdictInvented()
    {
        var outcome = await IterativeCampaignRunner.RunAsync(
            PlanThreeRounds("evh-verdict-1", "evh-verdict-2", "evh-verdict-3"),
            FakeExecutor((directive, roundIndex) => directive.StrategyId switch
            {
                "evh-verdict-2" => MergedScenarioVerdictSpec(),
                _ => CleanSpec(),
            }),
            maxRounds: 5);

        Assert.Equal(3, outcome.Rounds.Length);

        var merged = outcome.Rounds[1];
        var i3 = Assert.Single(merged.InvariantAssertions, a => a.InvariantId == CampaignInvariantIds.RuntimeCompletedNotValidationScenarioPass);
        Assert.False(i3.Passed);
        Assert.NotEmpty(i3.EvidenceRefs);
        Assert.Contains("scenarioVerdict", i3.Reason);
        Assert.False(merged.AllInvariantsPass);

        // The honest report path (no merged verdict) passes on clean rounds.
        foreach (var clean in new[] { outcome.Rounds[0], outcome.Rounds[2] })
        {
            Assert.All(clean.InvariantAssertions, a => Assert.True(a.Passed, $"{a.InvariantId}: {a.Reason}"));
            Assert.True(clean.AllInvariantsPass);
        }
    }

    // ── Termination: planner stop, three kinds recorded with reason+evidence ──

    [Fact]
    public async Task PlannerStop_ImmediateUnsafeRemainingFrontier_ZeroRounds_RecordedWithReasonAndEvidence()
    {
        var outcome = await IterativeCampaignRunner.RunAsync(
            (_, _) => new CampaignPlannerDecision.Stop(CampaignTermination.UnsafeRemainingFrontier(
                "the remaining frontier holds only known state-mutating classes; continuing would cross prohibited effects.",
                ["remaining scope node 'factory-reset' disposition: KnownPotentiallyStateMutating",
                 "plan delta history: zero safe traversal options remain"])),
            ThrowingExecutor,
            maxRounds: 3);

        Assert.Equal(CampaignTerminationKind.UnsafeRemainingFrontier, outcome.Termination.Kind);
        Assert.Contains("state-mutating", outcome.Termination.Reason);
        Assert.Equal(2, outcome.Termination.EvidenceRefs.Length);
        Assert.Empty(outcome.Rounds);
        Assert.Equal(0, outcome.CampaignCallLog.Count);
    }

    [Fact]
    public async Task PlannerStop_BoundedScopeExhaustion_AfterOneRound_StopsLoop()
    {
        var executed = 0;
        CampaignRoundPlanner planner = (priorRounds, _) =>
        {
            if (priorRounds.Count == 0)
            {
                executed++;
                return new CampaignPlannerDecision.Continue(AuthorRound("evh-stop-1"));
            }

            return new CampaignPlannerDecision.Stop(CampaignTermination.BoundedScopeExhaustion(
                "the single planned round is fully traversed."));
        };

        var outcome = await IterativeCampaignRunner.RunAsync(
            planner,
            FakeExecutor((_, _) => CleanSpec()),
            maxRounds: 5);

        Assert.Equal(CampaignTerminationKind.BoundedScopeExhaustion, outcome.Termination.Kind);
        Assert.Equal(1, outcome.Rounds.Length);
        Assert.Equal(1, executed);
        Assert.Equal(1, outcome.CampaignCallLog.Count);
    }

    // ── Duplicate StrategyId is rejected (round independence; idempotency is
    //    UniAgent-owned) ──────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateStrategyId_IsRejected_RecordedAsEvidencedContractGap_WithReasonAndEvidence()
    {
        CampaignRoundPlanner planner = (priorRounds, _) =>
            new CampaignPlannerDecision.Continue(AuthorRound("evh-dup-1"));

        var outcome = await IterativeCampaignRunner.RunAsync(
            planner,
            FakeExecutor((_, _) => CleanSpec()),
            maxRounds: 5);

        Assert.Equal(CampaignTerminationKind.EvidencedRuntimeContractGap, outcome.Termination.Kind);
        Assert.Contains("round independence", outcome.Termination.Reason);
        Assert.Contains("evh-dup-1", outcome.Termination.Reason);
        Assert.Equal(2, outcome.Termination.EvidenceRefs.Length);
        Assert.Contains(outcome.Termination.EvidenceRefs, r => r.Contains("evh-dup-1", StringComparison.Ordinal));

        // Round 0 ran under the identity; round 1 (the duplicate) was REJECTED —
        // never executed, never recorded as a round.
        Assert.Equal(1, outcome.Rounds.Length);
        Assert.Equal("evh-dup-1", outcome.Rounds[0].StrategyId);
        Assert.Equal(1, outcome.CampaignCallLog.Count);
    }

    // ── Hard bound: a planner that never terminates cannot loop forever ───────

    [Fact]
    public async Task PlannerNeverStops_HardMaxRoundsBound_StopsWithBoundedStopMaxRoundsExceeded()
    {
        var outcome = await IterativeCampaignRunner.RunAsync(
            PlanForeverNewStrategy,
            FakeExecutor((_, _) => CleanSpec()),
            maxRounds: 2);

        Assert.Equal(CampaignTerminationKind.BoundedStop, outcome.Termination.Kind);
        Assert.Contains("max rounds exceeded", outcome.Termination.Reason);
        Assert.Equal(2, outcome.Rounds.Length);
        Assert.Equal(2, outcome.CampaignCallLog.Count);
    }

    // ── planners ──────────────────────────────────────────────────────────────

    /// <summary>Planner: author three rounds (new StrategyId each) then stop
    /// with bounded scope exhaustion.</summary>
    private static CampaignPlannerDecision PlanThreeRoundsThenBoundedExhaustion(
        IReadOnlyList<CampaignRoundOutcome> priorRounds, CancellationToken _)
    {
        if (priorRounds.Count < 3)
        {
            return new CampaignPlannerDecision.Continue(AuthorRound($"evh-campaign-{priorRounds.Count + 1}"));
        }

        return new CampaignPlannerDecision.Stop(CampaignTermination.BoundedScopeExhaustion(
            "the planned three-round scope is fully traversed."));
    }

    /// <summary>Planner: author exactly the given strategy ids in order, then
    /// stop with bounded scope exhaustion.</summary>
    private static CampaignRoundPlanner PlanThreeRounds(string first, string second, string third)
    {
        var ids = new[] { first, second, third };
        return (priorRounds, _) => priorRounds.Count < ids.Length
            ? new CampaignPlannerDecision.Continue(AuthorRound(ids[priorRounds.Count]))
            : new CampaignPlannerDecision.Stop(CampaignTermination.BoundedScopeExhaustion(
                "the planned three-round scope is fully traversed."));
    }

    /// <summary>Planner: ALWAYS author a new strategy (never terminates) — tests
    /// the runner-owned hard bound.</summary>
    private static CampaignPlannerDecision PlanForeverNewStrategy(
        IReadOnlyList<CampaignRoundOutcome> priorRounds, CancellationToken _)
        => new CampaignPlannerDecision.Continue(AuthorRound($"evh-bound-{priorRounds.Count + 1}"));

    /// <summary>Author one round directive (deterministic fixture vocabulary).</summary>
    private static CampaignRoundDirective AuthorRound(string strategyId)
        => new(
            "Campaign round traversal objective (deterministic fixture).",
            DirectiveFixtureCatalog.BuildLegalDirective(strategyId),
            FixtureComposition.FixtureDeviceText);

    // ── fake executor (ScenarioRunnerTests composition pattern; scripts each
    //    round's OWN slice — the campaign assertion layer reads it alone) ─────

    /// <summary>A foreign wire method the engineered mid-run intervention uses
    /// (string input only — the harness never declares such a method).</summary>
    private const string ForeignControlMethod = "run.strategy.advance";

    /// <summary>Scripted round: runId + the round's OWN call-log slice.</summary>
    private sealed record FakeRoundSpec(
        Func<CampaignRoundDirective, int, (string RunId, ImmutableArray<EmulatorCallLogEntry> Entries)> Producer,
        bool MergeScenarioVerdict = false,
        bool PoisonWithGoalProse = false);

    /// <summary>Executor that never runs a round (planner terminates first).</summary>
    private static Task<ScenarioRunOutcome> ThrowingExecutor(
        CampaignRoundDirective directive, EmulatorCallLog priorCallLog, CancellationToken cancellationToken)
        => throw new InvalidOperationException("no round was expected (the planner stopped before authoring).");

    /// <summary>Fake executor: per-directive spec (producer) scripts the round's
    /// OWN slice; the whole-campaign log grows by appending each slice.</summary>
    private static CampaignRunExecutor FakeExecutor(Func<CampaignRoundDirective, int, FakeRoundSpec> specFactory)
    {
        return async (directive, priorCallLog, _) =>
        {
            var roundIndex = priorCallLog.Count;
            var spec = specFactory(directive, roundIndex);
            var (runId, entries) = spec.Producer(directive, roundIndex);
            var slice = EmulatorCallLog.FromEntries(entries);
            var driverLog = priorCallLog;
            foreach (var entry in entries)
            {
                driverLog = driverLog.Append(entry);
            }

            return BuildFakeOutcome(
                directive,
                runId,
                slice,
                driverLog,
                mergeScenarioVerdict: spec.MergeScenarioVerdict,
                poisonedGoalTruthSource: spec.PoisonWithGoalProse ? directive.Goal : null);
        };
    }

    private static FakeRoundSpec CleanSpec() => new(CleanRoundProducer);

    private static FakeRoundSpec PostAdmissionInterventionSpec() => new(PostAdmissionRoundProducer);

    private static FakeRoundSpec MergedScenarioVerdictSpec() => new(CleanRoundProducer, MergeScenarioVerdict: true);

    private static FakeRoundSpec PoisonedKnowledgeSpec() => new(CleanRoundProducer, PoisonWithGoalProse: true);

    private static (string RunId, ImmutableArray<EmulatorCallLogEntry> Entries) CleanRoundProducer(
        CampaignRoundDirective directive, int roundIndex)
    {
        var runId = $"campaign-fake-run-{roundIndex}";
        return (runId, [AcceptedEntry(directive.StrategyId, runId)]);
    }

    /// <summary>Engineered violation: an accepted start followed by a FOREIGN
    /// wire call — post-admission driver activity in the round's OWN slice.</summary>
    private static (string RunId, ImmutableArray<EmulatorCallLogEntry> Entries) PostAdmissionRoundProducer(
        CampaignRoundDirective directive, int roundIndex)
    {
        var runId = $"campaign-fake-run-{roundIndex}";
        var accepted = AcceptedEntry(directive.StrategyId, runId);
        var intervention = new EmulatorCallLogEntry(
            ForeignControlMethod,
            $"fake-digest-{directive.StrategyId}",
            EmulatorCallOutcome.TransportFailed,
            "engineered post-admission driver call (campaign test fixture)",
            accepted.TimestampUtc.AddSeconds(1));
        return (runId, [accepted, intervention]);
    }

    private static EmulatorCallLogEntry AcceptedEntry(string strategyId, string runId)
        => new(
            EmulatorDriver.StartStrategyMethod,
            $"fake-digest-{strategyId}",
            EmulatorCallOutcome.Accepted,
            runId,
            DateTimeOffset.UtcNow);

    // ── fake single-run outcome composition (reuses the REAL graduated chain
    //    components over scripted inputs — never a fabricated verdict) ────────

    /// <summary>
    /// Compose a fabricated round outcome through the real boundary verifier /
    /// gate evaluator / report renderer so the campaign assertion layer sees
    /// genuine-shaped inputs. <paramref name="poisonedGoalTruthSource"/> lets a
    /// test leak the authored goal into a result field's truth source (I1
    /// violation); <paramref name="mergeScenarioVerdict"/> forges a rendered
    /// report that merges a scenario verdict into the terminal section (I3
    /// violation). The fake never invents a scenario verdict on its own.
    /// </summary>
    private static ScenarioRunOutcome BuildFakeOutcome(
        CampaignRoundDirective directive,
        string runId,
        EmulatorCallLog roundCallLog,
        EmulatorCallLog driverCallLog,
        bool mergeScenarioVerdict = false,
        string? poisonedGoalTruthSource = null)
    {
        var result = BuildFakeResult(directive.Goal, directive.StrategyId, runId, poisonedGoalTruthSource);
        var payloads = ImmutableArray<JsonObject>.Empty;
        var transports = roundCallLog.Entries.Count(e => e.Outcome
            is EmulatorCallOutcome.Accepted or EmulatorCallOutcome.RejectedByAdmission or EmulatorCallOutcome.TransportFailed);
        if (transports > 0)
        {
            payloads = [StrategyPayloadJson.Freeze(directive.Directive)];
        }

        var acceptedStarts = roundCallLog.Entries.Count(e => e.Outcome == EmulatorCallOutcome.Accepted);
        var boundary = BoundaryVerifier.Verify(roundCallLog, result, expectedStartCount: acceptedStarts, transportedDirectives: payloads);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, roundCallLog, expectedStartCount: acceptedStarts, transportedDirectives: payloads);
        var admission = new StrategyRunAdmissionView(true, runId, "Idle", null, null);
        var dispatch = acceptedStarts > 0
            ? (DriverDispatchResult)new DriverDispatchResult.Transported(admission)
            : new DriverDispatchResult.TransportFailed("fake: transport not executed by the campaign test fixture");
        var report = new ValidationReport(result, gates, boundary);
        var reportJson = mergeScenarioVerdict
            ? ForgeMergedScenarioVerdictJson(runId)
            : ValidationReportRenderer.ToJson(report).ToJsonString();

        return new ScenarioRunOutcome(
            Dispatch: dispatch,
            Admission: admission,
            RunId: runId,
            StrategyId: directive.StrategyId,
            Result: result,
            RunCallLog: roundCallLog,
            DriverCallLog: driverCallLog,
            TransportedPayloads: payloads,
            Boundary: boundary,
            Gates: gates,
            Report: report,
            ReportJson: reportJson,
            ReportMarkdown: ValidationReportRenderer.ToMarkdown(report));
    }

    /// <summary>A rendered report that MERGES a scenario verdict into the
    /// terminal section — exactly what invariant I3 forbids (test-only forge).</summary>
    private static string ForgeMergedScenarioVerdictJson(string runId)
        => new JsonObject
        {
            ["validationReport"] = new JsonObject
            {
                ["sections"] = new JsonObject
                {
                    ["terminal"] = new JsonObject
                    {
                        ["terminalState"] = "Completed",
                        ["scenarioVerdict"] = "PASS",
                    },
                },
                ["gates"] = new JsonObject
                {
                    ["g1"] = true,
                    ["g2"] = true,
                    ["g3"] = true,
                    ["g4"] = true,
                },
            },
        }.ToJsonString();

    /// <summary>
    /// A complete ValidationResult with honest structure: runtime-read fields
    /// classified DirectProjection with surface truth sources, everything the
    /// synthetic surface cannot honestly answer explicitly Unavailable (never
    /// invented). <paramref name="poisonedTruthSource"/> (when non-null) makes
    /// snapshot.runState cite the authored goal prose — the I1 violation the
    /// runner must catch from THIS round's own result.
    /// </summary>
    private static ValidationResult BuildFakeResult(
        string goal,
        string strategyId,
        string runId,
        string? poisonedTruthSource)
    {
        var runStateSource = poisonedTruthSource ?? "run.snapshot.get (runState)";
        var unavailable = "fake campaign surface: section intentionally not composed (campaign assertion-layer test fixture)";
        return new ValidationResult(
            Admission: new AdmissionSection(
                ResultField<string>.Direct(runId, "run.strategy.start admission receipt (result.runId)"),
                ResultField<string>.Direct(strategyId, "run.strategy.start admission receipt (result.strategyId)"),
                ResultField<bool>.Direct(true, "run.strategy.start admission receipt (result.accepted)"),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<int>.Direct(1, "run.strategy.start admission receipt (result.declaredMaximumDepth)")),
            Lifecycle: new LifecycleSection(ResultField<ImmutableArray<SurfaceRuntimeEvent>>.Unavailable(unavailable)),
            Snapshot: new SnapshotSection(
                ResultField<string>.Direct(runId, "run.snapshot.get (runId)"),
                ResultField<RunState>.Direct(RunState.Completed, runStateSource),
                ResultField<string?>.Direct("campaign-fake-page", "run.snapshot.get (currentSemanticPage)"),
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
                ResultField<RunState>.Direct(RunState.Completed, "run.snapshot.get (runState)"),
                ResultField<string?>.Unavailable(unavailable),
                ResultField<bool?>.Unavailable(unavailable)),
            Boundary: BoundarySection.Placeholder);
    }
}