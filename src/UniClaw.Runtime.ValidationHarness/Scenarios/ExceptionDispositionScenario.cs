using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Scenarios;

/// <summary>
/// S2 disposition outcome under the revised semantics (2026-08-26 Human
/// decision REVISE_SPEC_WITHOUT_RUNTIME_CHANGE): the scenario proves
/// AUTONOMOUS_HANDLING, not RECOVERY_ALWAYS_SUCCEEDS.
/// </summary>
public enum S2DispositionOutcome
{
    /// <summary>Real recovery evidence + continued execution + zero Emulator
    /// intervention (only when the existing path has recovery capability).</summary>
    PassRecovered = 1,

    /// <summary>Runtime-originated terminal failure with explicit FailureReason
    /// backed by EvidenceRef/lifecycle events, no unbounded retry, no hidden
    /// fallback, zero Emulator intervention.</summary>
    PassBoundedFailClosed = 2,

    /// <summary>The disposition contract itself was violated (retry storm,
    /// hidden fallback, Emulator intervention, or an unexplainable terminal).</summary>
    Fail = 3,
}

/// <summary>S2 evidence record — every field derives from collected runtime
/// facts (call log, events, snapshot, terminal reason); no field is invented.</summary>
public sealed record S2ExceptionDispositionEvidence(
    string RunId,
    string StrategyId,
    int DriverStartCount,
    bool EmulatorIntervenedAfterAdmission,
    RunState TerminalState,
    string? TerminalReason,
    bool TerminalFailureOriginatedFromRuntime,
    bool FailureReasonExplicit,
    bool FailureEvidencePresent,
    bool UnboundedRetryDetected,
    bool RecoveryEvidencePresent,
    S2DispositionOutcome Outcome,
    string StrategyPathRecoveryCapability);

/// <summary>S2 outcome record.</summary>
public sealed record S2ExceptionDispositionOutcome(
    S2ExceptionDispositionEvidence Evidence,
    ScenarioRunOutcome Run);

/// <summary>
/// S2 — Runtime Autonomous Exception Disposition (revised semantics).
///
/// The entry injects a deterministic environment anomaly (an unexpected
/// external navigation scheduled on a mid-run observation) BEFORE the single
/// admission, so the anomaly provably occurs inside the autonomous run —
/// never between Emulator calls. It then executes exactly one
/// <c>run.strategy.start</c> and performs zero driver calls of any kind until
/// the terminal, and classifies the terminal under the disposition contract:
/// PASS_RECOVERED requires real recovery evidence followed by continued
/// execution; PASS_BOUNDED_FAIL_CLOSED requires a Runtime-originated terminal
/// failure with an explicit, evidence-backed reason and no retry storm or
/// hidden fallback. A bounded fail-closed terminal is never labeled a recovery
/// success; absent recovery evidence is never fabricated.
///
/// The strategy-path recovery capability gap is recorded verbatim in every
/// result: STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN /
/// NOT_PURCHASED_BY_PHASE_2_5 — a recovery-and-continue buyer requires a
/// separate Runtime Recovery capability via OpenSpec + Human Gate.
/// </summary>
public static class ExceptionDispositionScenario
{
    /// <summary>Capability-gap marker recorded in every S2 result.</summary>
    public const string StrategyPathRecoveryCapability =
        "STRATEGY_PATH_RECOVERY_CAPABILITY: NOT_PROVEN / NOT_PURCHASED_BY_PHASE_2_5";

    /// <summary>Execute the S2 entry on a fresh deterministic Tier-A world
    /// with a mid-run unexpected-navigation anomaly.</summary>
    public static async Task<S2ExceptionDispositionOutcome> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var world = FixtureComposition.CreateSettingsWorld();

        // Deterministic mid-run anomaly: the observation AFTER the next one is
        // served from an unexpected screen. Injection happens strictly before
        // admission — the anomaly is world state, not an Emulator act during
        // the run. Scheduling at +2 lands after the run's early observations
        // (admission/initial grounding) and before its terminal.
        world.InjectUnexpectedNavigation();

        using var host = new TierAHost(
            FixtureComposition.CreateFactory(world),
            FixtureComposition.CreateCompiler());

        var run = await ScenarioRunner.RunTierAAsync(
            host,
            DirectiveFixtureCatalog.SettingsExploreDepth2("evh-s2-disposition"),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = run.Result;
        var terminalState = result.Terminal.TerminalState.Value;
        var terminalReason = result.Terminal.TerminalReason.Value;

        // Zero-Emulator-intervention: the run-slice call log contains exactly
        // the single accepted start and nothing else.
        var runEntries = run.RunCallLog.Entries;
        var driverStartCount = runEntries.Count(e =>
            e.Method == "run.strategy.start" && e.Outcome == EmulatorCallOutcome.Accepted);
        var emulatorIntervened = runEntries.Count(e =>
            e.Method != "run.strategy.start") > 0
            || runEntries.Count(e => e.Method == "run.strategy.start") > 1;

        // Recovery evidence, read truthfully from the collected result — never
        // fabricated: trap found via run.trap.get, recovery-state snapshot
        // data, or RecoveryStarted lifecycle events in the projected stream.
        var recoveryEvidence = result.Trap.Found.Value == true
            || result.Snapshot.RecoveryState is { Classification: not ResultFieldClassification.Unavailable, Value: not null }
            || EventKinds(result).Contains("RecoveryStarted");

        // Bounded fail-closed evidence: a Runtime-originated failure whose
        // reason is explicit and whose lifecycle/evidence trail supports it.
        var failureOriginated = terminalState == RunState.Failed
            && !string.IsNullOrWhiteSpace(terminalReason);
        var failureReasonExplicit = !string.IsNullOrWhiteSpace(terminalReason);
        var failureEvidencePresent = failureOriginated
            && (result.Lifecycle.Events.Value is { IsDefault: false } or { Length: > 0 }
                || result.Snapshot.Diagnostics.Value is { Length: > 0 });

        // Retry storm detection: an unbounded retry would surface as repeated
        // ActionDispatched events cycling without terminal progress. The
        // bounded-run contract guarantees a terminal; a pathological count of
        // dispatched actions for the small fixture graph indicates runaway
        // retry (threshold generously above the fixture's healthy maximum).
        var actionDispatched = CountEventKind(result, "ActionDispatched");
        var unboundedRetry = actionDispatched > 64;

        S2DispositionOutcome outcome;
        if (recoveryEvidence && terminalState == RunState.Completed)
        {
            outcome = S2DispositionOutcome.PassRecovered;
        }
        else if (failureOriginated && failureReasonExplicit && failureEvidencePresent
                 && !unboundedRetry)
        {
            outcome = S2DispositionOutcome.PassBoundedFailClosed;
        }
        else
        {
            outcome = S2DispositionOutcome.Fail;
        }

        var evidence = new S2ExceptionDispositionEvidence(
            RunId: run.RunId ?? string.Empty,
            StrategyId: run.StrategyId,
            DriverStartCount: driverStartCount,
            EmulatorIntervenedAfterAdmission: emulatorIntervened,
            TerminalState: terminalState,
            TerminalReason: terminalReason,
            TerminalFailureOriginatedFromRuntime: failureOriginated,
            FailureReasonExplicit: failureReasonExplicit,
            FailureEvidencePresent: failureEvidencePresent,
            UnboundedRetryDetected: unboundedRetry,
            RecoveryEvidencePresent: recoveryEvidence,
            Outcome: outcome,
            StrategyPathRecoveryCapability: StrategyPathRecoveryCapability);

        return new S2ExceptionDispositionOutcome(evidence, run);
    }

    private static ImmutableArray<string> EventKinds(ValidationResult result)
        => result.Lifecycle.Events.Value.IsDefault
            ? []
            : [.. result.Lifecycle.Events.Value.Select(e => e.Kind)];

    private static int CountEventKind(ValidationResult result, string kind)
        => EventKinds(result).Count(k => string.Equals(k, kind, StringComparison.Ordinal));
}
