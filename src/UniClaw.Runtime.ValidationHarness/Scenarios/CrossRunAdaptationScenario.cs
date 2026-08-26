using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Scenarios;

/// <summary>
/// S3 scenario-specific evidence (spec "S3 — cross-run adaptation simulation";
/// WI-EVH-005 5.3). Run 1 completes under directive A; harness-LOCAL pure
/// analysis reads Result 1's ledger coverage facts and derives a coverage
/// digest; Run 2 is authorized under a NEW StrategyId whose content references
/// that digest. The future Memory insertion point is exactly
/// <c>Historical Result → Strategy</c> — OUTSIDE the Runtime boundary: nothing
/// in the Runtime (state, evidence, snapshots, events) is touched between runs.
/// </summary>
/// <param name="RunOne">Bounded one-Directive-one-Run execution of directive A.</param>
/// <param name="RunTwo">Bounded one-Directive-one-Run execution of directive B
/// (same one-Directive-one-Run shape, new StrategyId).</param>
/// <param name="CoverageDigest">Digest derived by the harness-local pure analysis
/// from Result 1's coverage facts (the facts that influenced Run 2).</param>
/// <param name="AdaptedStrategyId">The NEW StrategyId whose content references the
/// Result 1 digest (closed directive vocabulary — strategy identity string).</param>
/// <param name="MemoryInsertionPoint">The future insertion point, verbatim:
/// <c>Historical Result → Strategy</c>, harness-local and outside the Runtime.</param>
/// <param name="OnlyStrategyIdChanged">Payload diff: the two directives differ ONLY
/// in strategyId — proof that Result 1 facts influenced only the Run 2 directive.</param>
/// <param name="RunOneFactsUnchangedAfterRunTwo">Re-read of Run 1's snapshot /
/// event stream / trap after Run 2 equals the pre-Run-2 read (no Runtime state or
/// evidence of Run 1 was mutated).</param>
public sealed record CrossRunAdaptationOutcome(
    ScenarioRunOutcome RunOne,
    ScenarioRunOutcome RunTwo,
    string CoverageDigest,
    string AdaptedStrategyId,
    string MemoryInsertionPoint,
    bool OnlyStrategyIdChanged,
    bool RunOneFactsUnchangedAfterRunTwo);

/// <summary>
/// Harness-local pure analysis for S3 (the ONLY place Result 1 facts are
/// interpreted): reads the aggregated coverage counts, derives a deterministic
/// digest, and renders the insertion-point synthesis. Pure functions over
/// already-collected facts — no Runtime state, no Memory capability, no Planner.
/// </summary>
public static class CrossRunCoverageAnalysis
{
    /// <summary>The future Memory insertion point — exactly Historical Result →
    /// Strategy, outside the Runtime boundary (spec S3; never a Runtime feature).</summary>
    public const string MemoryInsertionPoint = "Historical Result → Strategy";

    /// <summary>Render one scope's five counts into a stable fact line (the
    /// atomic fact record the analysis consumes).</summary>
    public static string Render(CoverageScopeCounts scope)
        => $"{scope.ScopeIdentity}[d={scope.Discovered} v={scope.Visited} p={scope.Pending} u={scope.Unresolved} f={scope.UnknownFrontier}]";

    /// <summary>Deterministic SHA-256 digest over the ordered per-scope fact
    /// lines of Result 1's coverage section.</summary>
    public static string DeriveCoverageDigest(ImmutableArray<CoverageScopeCounts> scopes)
    {
        var canonical = string.Join(";", scopes.OrderBy(scope => scope.ScopeIdentity, StringComparer.Ordinal).Select(Render));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>
/// S3 scenario entry (spec "S3 — cross-run adaptation simulation"): completes
/// Run 1 (directive A), interprets Result 1 inside harness-local analysis,
/// authorizes Run 2 under a NEW StrategyId whose content references Result 1
/// facts, and proves the adaptation touched ONLY the Run 2 directive — the
/// insertion point stays outside the Runtime. The scenario composes and records;
/// assertions live in the capability test over this evidence.
/// </summary>
public static class CrossRunAdaptationScenario
{
    private const string RunOneStrategyId = "evh-s3-run1";

    /// <summary>Execute the S3 entry on the Tier-A deterministic host (fresh
    /// deterministic worlds per admission; the two runs share one driver call
    /// log so the cross-run boundary proof observes every dispatch).</summary>
    public static async Task<CrossRunAdaptationOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(),
            FixtureComposition.CreateCompiler());

        // ── Run 1 (directive A) ──────────────────────────────────────────────
        var runOneDirective = DirectiveFixtureCatalog.SettingsExploreDepth2(RunOneStrategyId);
        var runOne = await ScenarioRunner.RunTierAAsync(
            host,
            runOneDirective,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var runOneRunId = runOne.RunId ?? throw new InvalidOperationException("S3 Run 1 must be admitted.");

        // ── Harness-local interpretation of Result 1 (pure analysis) ────────
        var runOneScopes = runOne.Result.Coverage.Scopes.Classification == ResultFieldClassification.Unavailable
            ? ImmutableArray<CoverageScopeCounts>.Empty
            : runOne.Result.Coverage.Scopes.Value;
        var coverageDigest = CrossRunCoverageAnalysis.DeriveCoverageDigest(runOneScopes);
        var adaptedStrategyId = $"evh-s3-run2-adapt-{coverageDigest[..12]}";

        // Preserve Run 1 evidence BEFORE Run 2 (the immutability witness).
        var runOneFactsBefore = await ReadSurfaceFactsAsync(host, runOneRunId, cancellationToken).ConfigureAwait(false);

        // Run 2 must wait for Run 1's device reservation release (ONE_ACTIVE_RUN
        // per device; a bounded harness-local wait — zero wire calls, zero
        // runtime mutation — so the second admission is deterministic).
        await WaitForRunReleaseAsync(host, runOneRunId, cancellationToken).ConfigureAwait(false);

        // ── Run 2 (directive B: NEW StrategyId referencing Result 1 facts) ───
        var runTwoDirective = DirectiveFixtureCatalog.SettingsExploreDepth2(adaptedStrategyId);
        var runTwo = await ScenarioRunner.RunTierAAsync(
            host,
            runTwoDirective,
            priorCallLog: runOne.DriverCallLog,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // ── Boundary-insertion-point proofs ──────────────────────────────────
        var onlyStrategyIdChanged = OnlyStrategyIdChanged(
            StrategyPayloadJson.Freeze(runOneDirective.Directive!),
            StrategyPayloadJson.Freeze(runTwoDirective.Directive!));
        var runOneFactsAfter = await ReadSurfaceFactsAsync(host, runOneRunId, cancellationToken).ConfigureAwait(false);

        return new CrossRunAdaptationOutcome(
            RunOne: runOne,
            RunTwo: runTwo,
            CoverageDigest: coverageDigest,
            AdaptedStrategyId: adaptedStrategyId,
            MemoryInsertionPoint: CrossRunCoverageAnalysis.MemoryInsertionPoint,
            OnlyStrategyIdChanged: onlyStrategyIdChanged,
            RunOneFactsUnchangedAfterRunTwo: string.Equals(runOneFactsBefore, runOneFactsAfter, StringComparison.Ordinal));
    }

    /// <summary>Canonical payload diff: the two directives are byte-identical
    /// after removing <c>strategyId</c> — the ONLY payload difference the
    /// adaptation introduced.</summary>
    private static bool OnlyStrategyIdChanged(JsonObject first, JsonObject second)
    {
        var a = (JsonObject)first.DeepClone();
        var b = (JsonObject)second.DeepClone();
        a.Remove("strategyId");
        b.Remove("strategyId");
        return string.Equals(a.ToJsonString(), b.ToJsonString(), StringComparison.Ordinal);
    }

    /// <summary>Deterministic digest of Run 1's surface facts (snapshot state +
    /// projected event kinds/sequences + trap read) — the immutability witness
    /// compared across Run 2.</summary>
    private static async Task<string> ReadSurfaceFactsAsync(
        TierAHost host,
        string runId,
        CancellationToken cancellationToken)
    {
        var surface = new TierAReadSurface(host, runId);
        var snapshot = await surface.GetRunSnapshotAsync(runId, cancellationToken).ConfigureAwait(false);
        var events = await surface.GetRuntimeEventsAfterAsync(runId, cursor: null, cancellationToken).ConfigureAwait(false);
        var trap = await surface.GetRunTrapAsync(runId, cancellationToken).ConfigureAwait(false);
        var eventFacts = string.Join(";", events.Events.Select(item => $"{item.Kind}@{item.Sequence}"));
        return $"state={snapshot.RunState.Value}|events=[{eventFacts}]|trapFound={trap.Found}";
    }

    /// <summary>Bounded wait for the coordinator to release the run record and
    /// its device reservation (read of the coordinator diagnostic view only).</summary>
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