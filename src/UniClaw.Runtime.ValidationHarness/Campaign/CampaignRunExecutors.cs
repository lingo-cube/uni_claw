using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Scenarios;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// Ready-made executor adapters (Phase 2.6): the Tier-A adapter reuses the EXACT
/// graduated Phase 2.5 chain (<see cref="ScenarioRunner.RunTierAAsync"/>) over
/// the real in-process host with the real loopback wire (design D3); Tier-B
/// real-emulator compositions can plug in through the same thin seam later.
/// These adapters own composition pacing only — they never add a wire call or
/// touch Runtime state.
/// </summary>
public static class CampaignRunExecutors
{
    /// <summary>
    /// Tier-A executor over one shared in-process host: transports each round's
    /// directive through the real loopback JSON-RPC wire and runs the graduated
    /// single-run composition, chaining the campaign's immutable driver call log
    /// across rounds. A bounded read-only wait between rounds lets the
    /// coordinator release the previous run's device reservation so the next
    /// admission is deterministic (ONE_ACTIVE_RUN per device) — the same zero
    /// wire-call wait the S3 cross-run scenario uses.
    /// </summary>
    public static CampaignRunExecutor TierA(TierAHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        string? lastRunId = null;
        return async (directive, priorCallLog, cancellationToken) =>
        {
            if (lastRunId is not null)
            {
                await WaitForRunReleaseAsync(host, lastRunId, cancellationToken).ConfigureAwait(false);
            }

            var outcome = await ScenarioRunner.RunTierAAsync(
                host,
                directive.ToFixtureRecord(),
                priorCallLog,
                cancellationToken).ConfigureAwait(false);
            lastRunId = outcome.RunId;
            return outcome;
        };
    }

    /// <summary>Bounded wait for the coordinator to release the run record and
    /// its device reservation (read of the coordinator diagnostic view only —
    /// zero wire calls, zero runtime mutation).</summary>
    private static async Task WaitForRunReleaseAsync(
        TierAHost host,
        string runId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!host.Runs.ContainsKey(runId))
            {
                return;
            }

            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Run '{runId}' was not released by the coordinator within the bounded wait.");
    }
}