using System.Collections.Immutable;
using System.Text;
using UniClaw.Runtime.ValidationHarness.Emulator;

namespace UniClaw.Runtime.ValidationHarness.Campaign;

/// <summary>
/// Per-round autonomy assertor (Phase 2.6, spec "Every run is autonomous and
/// independent"): derives the autonomy assertion from THIS round's OWN call-log
/// slice only — exactly one accepted <c>run.strategy.start</c>, zero
/// driver/wire control calls after admission, zero calls outside the frozen
/// start method. A pure function over the slice; nothing is asserted from a
/// prior round's conclusion.
/// </summary>
public static class CampaignAutonomyAssertor
{
    /// <summary>Assert autonomy over one round's own call-log slice.</summary>
    /// <param name="roundCallLog">The round's own slice (the executor chain
    /// guarantees this is the round's dispatches only).</param>
    /// <param name="runId">The admitted run identity (evidence text only).</param>
    /// <param name="roundIndex">Zero-based round index (evidence text only).</param>
    public static CampaignAutonomyAssertion Assert(
        EmulatorCallLog roundCallLog,
        string? runId,
        int roundIndex)
    {
        ArgumentNullException.ThrowIfNull(roundCallLog);
        ArgumentOutOfRangeException.ThrowIfNegative(roundIndex);

        var entries = roundCallLog.Entries;
        var evidence = ImmutableArray.CreateBuilder<string>();
        var acceptedStartCount = 0;
        var lastAccepted = -1;
        var foreignCalls = 0;

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (!string.Equals(entry.Method, EmulatorDriver.StartStrategyMethod, StringComparison.Ordinal))
            {
                foreignCalls++;
                evidence.Add($"round {roundIndex}: foreign driver call at slice entry {index} (method '{entry.Method}', outcome {entry.Outcome})");
                continue;
            }

            if (entry.Outcome == EmulatorCallOutcome.Accepted)
            {
                acceptedStartCount++;
                lastAccepted = index;
            }
        }

        // Entries AFTER the last accepted admission — the run's own progress
        // must be driver-free. A slice with no acceptance counts everything.
        var entriesAfterAdmission = lastAccepted >= 0
            ? entries.Length - 1 - lastAccepted
            : entries.Length;

        evidence.Add(
            $"round {roundIndex}: accepted run.strategy.start count {acceptedStartCount} (exactly one required)"
            + (acceptedStartCount == 1 ? $" — DriverHost run {runId ?? "<null>"}" : string.Empty));
        evidence.Add($"round {roundIndex}: driver/wire control calls after admission: {entriesAfterAdmission} (zero required)");
        if (foreignCalls == 0)
        {
            evidence.Add($"round {roundIndex}: zero calls outside run.strategy.start (frozen wire surface only)");
        }

        var passed = acceptedStartCount == 1 && entriesAfterAdmission == 0 && foreignCalls == 0;
        string? offending = null;
        if (!passed)
        {
            var problems = new StringBuilder();
            if (acceptedStartCount != 1)
            {
                problems.Append($"accepted starts {acceptedStartCount} ≠ 1; ");
            }

            if (entriesAfterAdmission != 0)
            {
                problems.Append($"{entriesAfterAdmission} driver/wire call(s) after admission (zero required); ");
            }

            if (foreignCalls > 0)
            {
                problems.Append($"{foreignCalls} call(s) outside run.strategy.start; ");
            }

            offending = $"round {roundIndex} autonomy violation: {problems.ToString().TrimEnd(' ', ';')}";
        }

        return new CampaignAutonomyAssertion(
            Passed: passed,
            AcceptedStartCount: acceptedStartCount,
            EntriesAfterAdmission: entriesAfterAdmission,
            EvidenceRefs: evidence.ToImmutable(),
            OffendingEvidence: offending);
    }
}