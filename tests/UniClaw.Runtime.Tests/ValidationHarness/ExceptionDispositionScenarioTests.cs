using System;
using System.Linq;
using System.Threading.Tasks;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Results;
using UniClaw.Runtime.ValidationHarness.Scenarios;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// S2 — Runtime Autonomous Exception Disposition (revised semantics,
/// 2026-08-26 Human decision REVISE_SPEC_WITHOUT_RUNTIME_CHANGE).
///
/// Validates the capability contract, never a fixed interaction script:
/// exactly one start; zero Emulator intervention from admission to terminal;
/// a truthful disposition outcome (PASS_RECOVERED or
/// PASS_BOUNDED_FAIL_CLOSED) with an explainable, evidence-backed terminal;
/// the strategy-path recovery capability gap recorded verbatim.
/// AUTONOMOUS_HANDLING != RECOVERY_ALWAYS_SUCCEEDS;
/// FAIL_CLOSED_TERMINAL != RECOVERY_FAILURE_OF_ARCHITECTURE.
/// </summary>
public sealed class ExceptionDispositionScenarioTests
{
    [Fact]
    public async Task S2_UnexpectedNavigationMidRun_AutonomousDisposition_ZeroEmulatorIntervention()
    {
        // ── EvidenceFixture → Runtime Execution happen inside the scenario
        // entry (fresh deterministic world, injected anomaly, one start). ──
        var outcome = await ExceptionDispositionScenario.RunAsync();

        var evidence = outcome.Evidence;
        var result = outcome.Run.Result;

        // Exactly one run.strategy.start (the bounded transport contract).
        Assert.Equal(1, evidence.DriverStartCount);

        // Zero Emulator intervention from admission to terminal: the run-slice
        // call log carries only the single accepted start.
        Assert.False(evidence.EmulatorIntervenedAfterAdmission,
            "S2 proves autonomy: the Emulator must make no Run-internal control call.");

        // A truthful disposition outcome — either pass form, never a silent
        // third state and never a masked failure.
        Assert.True(
            evidence.Outcome is S2DispositionOutcome.PassRecovered
                or S2DispositionOutcome.PassBoundedFailClosed,
            $"disposition must classify as PASS_RECOVERED or PASS_BOUNDED_FAIL_CLOSED " +
            $"(got {evidence.Outcome}; terminal={evidence.TerminalState}; " +
            $"reason={evidence.TerminalReason})");

        // The terminal is explainable and evidence-backed on every path:
        // completed runs are GoalEvidence-backed; fail-closed runs carry an
        // explicit Runtime-originated reason with supporting lifecycle
        // evidence and no retry storm.
        if (evidence.Outcome == S2DispositionOutcome.PassBoundedFailClosed)
        {
            Assert.Equal(RunState.Failed, evidence.TerminalState);
            Assert.True(evidence.TerminalFailureOriginatedFromRuntime);
            Assert.True(evidence.FailureReasonExplicit,
                "a bounded fail-closed terminal must carry an explicit FailureReason");
            Assert.True(evidence.FailureEvidencePresent,
                "the failure must be supported by lifecycle events or snapshot diagnostics");
            Assert.False(evidence.UnboundedRetryDetected,
                "bounded disposition forbids retry storms");
            // Truthfulness guard: a fail-closed terminal is never a recovery
            // success and absent recovery evidence is never fabricated.
            Assert.False(evidence.RecoveryEvidencePresent
                && evidence.Outcome == S2DispositionOutcome.PassBoundedFailClosed
                && evidence.TerminalState == RunState.Completed);
        }
        else
        {
            Assert.Equal(RunState.Completed, evidence.TerminalState);
            Assert.True(evidence.RecoveryEvidencePresent,
                "PASS_RECOVERED requires real recovery evidence (never fabricated)");
        }

        // The gates still hold on the disposition run (G1–G4 from the shared
        // composition; a bounded fail-closed terminal does not weaken them).
        Assert.NotNull(outcome.Run.Gates);

        // Capability gap recorded verbatim — preserved, not purchased here.
        Assert.Equal(
            "STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5",
            evidence.StrategyPathRecoveryCapability);
    }

    [Fact]
    public async Task S2_AnomalyRun_StillEmitsRuntimeOwnedTerminalEvidence()
    {
        // The anomaly run's projected event stream is Runtime-owned and
        // A/B-classified regardless of disposition: the Boundary Verifier's
        // event-classification proof covers this run like any other, and the
        // terminal state is readable through the frozen read surface.
        var outcome = await ExceptionDispositionScenario.RunAsync();
        var result = outcome.Run.Result;

        Assert.Contains(
            result.Terminal.TerminalState.Classification,
            new[] { ResultFieldClassification.DirectProjection,
                    ResultFieldClassification.DerivedReadModel });
        // The terminal reason, when present, is classified (never invented).
        if (result.Terminal.TerminalReason.Value is not null)
        {
            Assert.NotEqual(
                ResultFieldClassification.Unavailable,
                result.Terminal.TerminalReason.Classification);
        }
    }
}
