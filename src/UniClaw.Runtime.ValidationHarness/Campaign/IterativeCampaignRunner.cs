using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.Emulator;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// Whole-campaign result (Phase 2.6, spec "Frozen iterative loop with
/// independent runs"): the recorded termination, the per-round outcomes, and the
/// ONE immutable whole-campaign driver call log — grown across rounds through
/// ScenarioRunner's priorCallLog chaining (the cross-run boundary proof surface,
/// design D5).
/// </summary>
public sealed record CampaignRunOutcome(
    CampaignTermination Termination,
    ImmutableArray<CampaignRoundOutcome> Rounds,
    EmulatorCallLog CampaignCallLog);

/// <summary>
/// Phase 2.6 iterative campaign runner (spec "Frozen iterative loop with
/// independent runs"): drives N independent Runtime Runs through the graduated
/// Phase 2.5 single-run chain (<see cref="UniClaw.Runtime.ValidationHarness.Scenarios.ScenarioRunner.RunTierAAsync"/>
/// or any composition behind the thin executor seam). Per round it:
/// (a) verifies the directive's StrategyId differs from every prior round
/// (rejects duplicates — idempotency is UniAgent-owned);
/// (b) after the run, re-asserts — from THIS round's OWN call-log slice —
/// exactly one accepted <c>run.strategy.start</c> and zero driver/wire control
/// calls after admission (spec "Every run is autonomous and independent");
/// (c) re-asserts the four frozen invariants as explicit boolean outcomes with
/// evidence refs drawn from this round's own result and call log (spec "Four
/// frozen invariants"), never a prior round's conclusion;
/// (d) accumulates ONE immutable whole-campaign call log across rounds.
/// Termination is closed: the planner supplies the next directive or an explicit
/// <see cref="CampaignTermination"/>, and the hard MaxRounds bound guarantees the
/// loop always stops (recorded as a bounded stop) — there is no implicit
/// fall-through.
/// </summary>
public static class IterativeCampaignRunner
{
    /// <summary>
    /// Run one bounded campaign. The planner is consulted every round with the
    /// immutable prior outcomes; the executor runs exactly one round's
    /// composition. The loop stops on the planner's explicit termination, on a
    /// rejected duplicate StrategyId (surfaced as an evidenced contract gap), or
    /// on the hard <paramref name="maxRounds"/> bound (recorded as a bounded
    /// stop) — whichever comes first.
    /// </summary>
    public static async Task<CampaignRunOutcome> RunAsync(
        CampaignRoundPlanner planner,
        CampaignRunExecutor runExecutor,
        int maxRounds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(runExecutor);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRounds, 1);

        var rounds = ImmutableArray.CreateBuilder<CampaignRoundOutcome>();
        var campaignLog = EmulatorCallLog.Empty;
        CampaignTermination? termination = null;

        while (termination is null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Immutable snapshot for the planner: it reviews prior evidence and
            // can never mutate the runner's round list.
            var decision = planner(rounds.ToImmutable(), cancellationToken);
            switch (decision)
            {
                case CampaignPlannerDecision.Stop stop:
                    ArgumentNullException.ThrowIfNull(stop.Termination);
                    termination = stop.Termination;
                    break;

                case CampaignPlannerDecision.Continue next:
                    ArgumentNullException.ThrowIfNull(next.Directive);
                    // Hard bound FIRST: never run a round beyond the bound — the
                    // closure guarantee when the planner never terminates.
                    if (rounds.Count >= maxRounds)
                    {
                        termination = CampaignTermination.MaxRoundsExceeded(maxRounds);
                        break;
                    }

                    if (FindDuplicate(next.Directive, rounds) is { } duplicate)
                    {
                        termination = CampaignTermination.EvidencedRuntimeContractGap(
                            $"round independence contract (spec 'Frozen iterative loop with independent runs') violated: "
                            + $"StrategyId '{next.Directive.StrategyId}' was already used by round {duplicate.RoundIndex}; "
                            + "idempotency is UniAgent-owned, the planner must author a fresh identity per round.",
                            [
                                $"duplicate round directive: StrategyId '{next.Directive.StrategyId}'",
                                $"prior round {duplicate.RoundIndex} used the same StrategyId '{duplicate.StrategyId}'",
                            ]);
                        break;
                    }

                    var outcome = await ExecuteRoundAsync(
                        runExecutor,
                        next.Directive,
                        roundIndex: rounds.Count,
                        priorCallLog: campaignLog,
                        cancellationToken).ConfigureAwait(false);
                    rounds.Add(outcome);
                    campaignLog = outcome.Run.DriverCallLog;
                    break;

                default:
                    // Closed union: this branch is unreachable; an implicit
                    // fall-through would violate the boundary contract.
                    throw new InvalidOperationException(
                        "Unreachable: CampaignPlannerDecision is a closed union; an implicit fall-through is forbidden.");
            }
        }

        // The loop exits only with a termination (planner Stop / duplicate
        // rejection / hard bound).
        return new CampaignRunOutcome(termination!, rounds.ToImmutable(), campaignLog);
    }

    private static async Task<CampaignRoundOutcome> ExecuteRoundAsync(
        CampaignRunExecutor runExecutor,
        CampaignRoundDirective directive,
        int roundIndex,
        EmulatorCallLog priorCallLog,
        CancellationToken cancellationToken)
    {
        var run = await runExecutor(directive, priorCallLog, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"campaign round {roundIndex}: the run executor returned no outcome (a round must always produce a result).");

        // Per-round re-assertions — derived fresh from THIS round's slice and
        // outputs; a previous round's assertion result is never reused.
        var autonomy = CampaignAutonomyAssertor.Assert(run.RunCallLog, run.RunId, roundIndex);
        var invariants = CampaignInvariantEvaluator.AssertAll(
            directive,
            run,
            roundIndex,
            precedingRunCount: roundIndex);

        return new CampaignRoundOutcome(
            RoundIndex: roundIndex,
            Directive: directive,
            StrategyId: run.StrategyId,
            RunId: run.RunId,
            DispatchResult: run.Dispatch,
            Run: run,
            RoundCallLog: run.RunCallLog,
            Autonomy: autonomy,
            InvariantAssertions: invariants,
            AllInvariantsPass: invariants.All(assertion => assertion.Passed));
    }

    /// <summary>The first prior round that already used the directive's
    /// StrategyId, or null when the identity is fresh (round independence).</summary>
    private static CampaignRoundOutcome? FindDuplicate(
        CampaignRoundDirective directive,
        ImmutableArray<CampaignRoundOutcome>.Builder priorRounds)
    {
        foreach (var prior in priorRounds)
        {
            if (string.Equals(prior.StrategyId, directive.StrategyId, StringComparison.Ordinal))
            {
                return prior;
            }
        }

        return null;
    }
}