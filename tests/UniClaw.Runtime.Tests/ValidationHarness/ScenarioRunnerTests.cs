using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Scenarios;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-EVH-005 scenario capability tests (S1 + S3): each test follows
/// EvidenceFixture (deterministic world + recorded directive) → Runtime
/// Execution (exactly bounded <c>run.strategy.start</c> transport through the
/// real loopback wire) → Evidence Evaluation (admission legality, autonomy,
/// ledger accounting, evidence ordering, boundary cleanliness, G1–G4). The
/// assertions concern capabilities — never fixed click counts, coordinates,
/// page text, UI paths, or fixed action histories.
/// </summary>
public sealed class ScenarioRunnerTests
{
    // ── S1: Settings exploration depth 2 ─────────────────────────────────────

    /// <summary>
    /// S1 capability test: one depth-2 directive admitted into exactly one run;
    /// the run proceeds autonomously (zero driver calls after admission);
    /// record-only leaves are never dispatched; the Tier-A ledger is complete
    /// (five counts); GoalEvidenceProduced precedes RunCompleted; G1–G4 pass.
    /// </summary>
    [Fact]
    public async Task S1SettingsExplorationDepth2_AutonomousRun_CompleteLedger_BackedTerminal_GatesPass()
    {
        // ── EvidenceFixture + Runtime Execution ──────────────────────────────
        var outcome = await SettingsExplorationScenario.RunAsync();
        var evidence = outcome.Evidence;
        var result = outcome.Run.Result;

        // ── Evidence Evaluation ──────────────────────────────────────────────

        // Exactly one run.strategy.start; admission accepted; zero driver calls
        // after admission (the run's call-log slice holds only its start).
        Assert.Equal(1, evidence.DriverStartCount);
        var start = Assert.Single(outcome.Run.RunCallLog.Entries);
        Assert.Equal(EmulatorCallOutcome.Accepted, start.Outcome);
        Assert.Equal(evidence.RunId, start.Detail);
        Assert.True(outcome.Run.AdmittedRun);

        // Record-only leaves produced no dispatched action: the fixture world's
        // truthful dispatch record (delivered Taps on transitionless leaf
        // elements) is empty — derivation from the run's dispatch, not fixed counts.
        Assert.Equal(0, evidence.DispatchedTransitionlessTapCount);
        Assert.Equal("evh-s1-depth2", evidence.StrategyId);

        // Terminal Completed backed by GoalEvidenceProduced BEFORE RunCompleted
        // (projected event sequence ordering).
        Assert.Equal(RunState.Completed, result.Terminal.TerminalState.Value);
        Assert.True(evidence.GoalEvidencePrecedesRunCompleted, "GoalEvidenceProduced must precede RunCompleted in the event stream.");
        var events = result.Lifecycle.Events.Value;
        Assert.Contains(events, item => item.Kind == "GoalEvidenceProduced");
        Assert.Contains(events, item => item.Kind == "RunCompleted");
        Assert.DoesNotContain(events, item => item.Kind == "RunFailed");

        // Tier-A ledger complete per directive semantics: attested availability,
        // declared depth 2, five counts per scope with zero pending / unresolved /
        // unknown-frontier once the deterministic world satisfies the directive,
        // and the stable ledger digest present (read-only projection).
        Assert.Equal("tierA-attested", evidence.CoverageAvailability);
        Assert.Equal(2, evidence.DeclaredMaximumDepth);
        Assert.False(string.IsNullOrWhiteSpace(evidence.LedgerDigest), "The Tier-A ledger digest must be compiled.");
        Assert.NotEmpty(evidence.ScopeCounts);
        foreach (var scope in evidence.ScopeCounts)
        {
            Assert.Equal(0, scope.Pending);
            Assert.Equal(0, scope.Unresolved);
            Assert.Equal(0, scope.UnknownFrontier);
        }

        Assert.Contains(evidence.ScopeCounts, scope => scope.Discovered > 0);
        Assert.Contains(evidence.ScopeCounts, scope => scope.Visited > 0);

        // Runtime-attested strategy identity from the Agent read model.
        Assert.Equal("evh-s1-depth2", result.Admission.StrategyId.Value);

        AssertGatesAndReportSections("S1", outcome.Run);
    }

    // ── S3: cross-run adaptation simulation ──────────────────────────────────

    /// <summary>
    /// S3 capability test: two distinct one-Directive-one-Run executions under
    /// two strategy identities; Result 1's ledger coverage facts (read by a
    /// harness-local pure analysis) influenced ONLY Run 2's directive payload
    /// (diff = strategyId only); Run 1's Runtime evidence is immutable between
    /// runs; the future Memory insertion point stays outside the Runtime; G1–G4
    /// pass on both runs.
    /// </summary>
    [Fact]
    public async Task S3CrossRunAdaptation_TwoBoundedRuns_OnlyStrategyChanged_Run1EvidenceImmutable_GatesPass()
    {
        // ── EvidenceFixture + Runtime Execution + Evidence Evaluation ────────
        var outcome = await CrossRunAdaptationScenario.RunAsync();
        var runOne = outcome.RunOne;
        var runTwo = outcome.RunTwo;

        // Two distinct one-Directive-one-Run executions: two admissions, two
        // DriverHost-owned runIds, two distinct strategy identities.
        Assert.True(runOne.AdmittedRun, "S3 Run 1 must be admitted.");
        Assert.True(runTwo.AdmittedRun, "S3 Run 2 must be admitted.");
        Assert.NotEqual(runOne.RunId, runTwo.RunId);
        Assert.NotEqual(runOne.StrategyId, runTwo.StrategyId);
        Assert.Equal(1, runOne.RunCallLog.Count);
        Assert.Equal(1, runTwo.RunCallLog.Count);

        // The shared immutable call log: exactly two starts, both accepted, and
        // no driver activity after either admission.
        Assert.Equal(2, runTwo.DriverCallLog.Count);
        Assert.All(runTwo.DriverCallLog.Entries, entry => Assert.Equal(EmulatorCallOutcome.Accepted, entry.Outcome));

        // Distinct strategy payload digests prove the second transport differed
        // from the first at the wire level.
        Assert.NotEqual(runOne.RunCallLog.Entries[0].PayloadDigest, runTwo.RunCallLog.Entries[0].PayloadDigest);

        // Harness-local pure analysis read Result 1's coverage facts and derived
        // the coverage digest that shaped Run 2.
        Assert.False(string.IsNullOrWhiteSpace(outcome.CoverageDigest), "S3 analysis must derive a coverage digest from Run 1 facts.");
        Assert.Equal("evh-s3-run2-adapt-" + outcome.CoverageDigest[..12], outcome.AdaptedStrategyId);
        Assert.Equal(outcome.AdaptedStrategyId, runTwo.StrategyId);

        // Result 1 facts influenced ONLY the Run 2 directive: the canonical
        // payloads differ in strategyId and nothing else.
        Assert.True(outcome.OnlyStrategyIdChanged, "The two directives must differ only in strategyId (the adaptation surface).");

        // Future Memory insertion point = Historical Result → Strategy, OUTSIDE
        // the Runtime boundary, operationally proven: Run 1's snapshot / event
        // stream / trap are unchanged when re-read after Run 2.
        Assert.Equal("Historical Result → Strategy", outcome.MemoryInsertionPoint);
        Assert.True(outcome.RunOneFactsUnchangedAfterRunTwo, "No Runtime state or evidence of Run 1 may be mutated between runs.");

        // Both runs complete terminal through the existing path with G1–G4.
        Assert.Equal(RunState.Completed, runOne.Result.Terminal.TerminalState.Value);
        Assert.Equal(RunState.Completed, runTwo.Result.Terminal.TerminalState.Value);
        AssertGatesAndReportSections("S3.run1", runOne);
        AssertGatesAndReportSections("S3.run2", runTwo);
    }

    // ── shared helper: G1–G4 + report sections ───────────────────────────────

    /// <summary>
    /// Asserts all four gates pass (with the boundary proof positive) and the
    /// report renders both JSON and Markdown with every section — the report is
    /// RETURNED by the scenario entry, not written to disk.
    /// </summary>
    private static void AssertGatesAndReportSections(string scenario, ScenarioRunOutcome run)
    {
        Assert.True(run.Gates.G1.Passed, $"{scenario}: G1 directive-legal must pass — {run.Gates.G1.OffendingEvidence}");
        Assert.True(run.Gates.G2.Passed, $"{scenario}: G2 end-to-end autonomy must pass — {run.Gates.G2.OffendingEvidence}");
        Assert.True(run.Gates.G3.Passed, $"{scenario}: G3 result evidence-backed must pass — {run.Gates.G3.OffendingEvidence}");
        Assert.True(run.Gates.G4.Passed, $"{scenario}: G4 boundary clean must pass — {run.Gates.G4.OffendingEvidence}");
        Assert.True(run.Gates.AllPass, $"{scenario}: all four gates must pass.");
        Assert.True(run.Boundary.Passed, $"{scenario}: the boundary proof must be positive for all four prohibitions.");

        Assert.Contains("\"validationReport\"", run.ReportJson);
        Assert.Contains("\"gates\"", run.ReportJson);
        Assert.Contains("\"g1\"", run.ReportJson);
        Assert.Contains("\"boundary\"", run.ReportJson);
        Assert.Contains("# Validation Report", run.ReportMarkdown);
        Assert.Contains("## Gates", run.ReportMarkdown);
        Assert.Contains("## Boundary", run.ReportMarkdown);
        Assert.Contains("## Coverage", run.ReportMarkdown);
    }
}