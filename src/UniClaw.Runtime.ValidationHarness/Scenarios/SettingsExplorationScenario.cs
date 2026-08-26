using System.Collections.Immutable;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Scenarios;

/// <summary>
/// S1 scenario-specific evidence (spec "S1 — Settings exploration depth 2";
/// WI-EVH-005 5.1). Every field derives from the run's truthful surfaces: the
/// call log (exactly one start, zero driver activity after admission), the
/// fixture world's dispatch record (record-only leaves never dispatched), the
/// projected event stream (GoalEvidenceProduced before RunCompleted), and the
/// Tier-A ledger attestation (five counts per scope — pending/unresolved/frontier
/// zero when the deterministic world satisfies the directive).
/// </summary>
/// <param name="RunId">DriverHost-owned run identity.</param>
/// <param name="StrategyId">Strategy identity carried by the transported directive.</param>
/// <param name="DriverStartCount">Call-log slice length — bounded to exactly ONE
/// <c>run.strategy.start</c>; entries after admission would count here and fail.</param>
/// <param name="DispatchedTransitionlessTapCount">Fixture-world dispatch record of
/// delivered Taps on transitionless (record-only leaf) elements — must be zero.</param>
/// <param name="GoalEvidencePrecedesRunCompleted">Projected sequence ordering fact.</param>
/// <param name="CoverageAvailability">Tier-A attestation availability ("tierA-attested").</param>
/// <param name="ScopeCounts">Per-scope five counts (discovered/visited/pending/unresolved/unknown-frontier).</param>
/// <param name="LedgerDigest">Stable ledger digest (read-only projection).</param>
/// <param name="DeclaredMaximumDepth">Declared depth attested from the Agent read model.</param>
public sealed record SettingsExplorationEvidence(
    string RunId,
    string StrategyId,
    int DriverStartCount,
    int DispatchedTransitionlessTapCount,
    bool GoalEvidencePrecedesRunCompleted,
    string CoverageAvailability,
    ImmutableArray<CoverageScopeCounts> ScopeCounts,
    string? LedgerDigest,
    int DeclaredMaximumDepth);

/// <summary>S1 entry outcome: the scenario evidence plus the composed run
/// (result / boundary / gates / report).</summary>
public sealed record SettingsExplorationOutcome(
    SettingsExplorationEvidence Evidence,
    ScenarioRunOutcome Run);

/// <summary>
/// S1 scenario entry (spec "S1 — Settings exploration depth 2"): one directive
/// (declared depth 2, container-expand + record-only leaves from the fixture
/// catalog; zero state mutation, zero boundary crossing) is transported exactly
/// once through <c>run.strategy.start</c>; the run proceeds autonomously and the
/// harness aggregates the Tier-A evidence through the frozen surfaces — the
/// scenario itself only composes and records, never asserts (assertions live in
/// the capability tests over this evidence).
/// </summary>
public static class SettingsExplorationScenario
{
    /// <summary>Execute the S1 entry on a fresh deterministic Tier-A world.</summary>
    public static async Task<SettingsExplorationOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var world = FixtureComposition.CreateSettingsWorld();
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(world),
            FixtureComposition.CreateCompiler());

        var run = await ScenarioRunner.RunTierAAsync(
            host,
            DirectiveFixtureCatalog.SettingsExploreDepth2("evh-s1-depth2"),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = run.Result;
        var scopes = result.Coverage.Scopes.Classification == ResultFieldClassification.Unavailable
            ? ImmutableArray<CoverageScopeCounts>.Empty
            : result.Coverage.Scopes.Value;

        var evidence = new SettingsExplorationEvidence(
            RunId: run.RunId ?? string.Empty,
            StrategyId: run.StrategyId,
            DriverStartCount: run.RunCallLog.Count,
            DispatchedTransitionlessTapCount: world.DispatchedTransitionlessTapCount,
            GoalEvidencePrecedesRunCompleted: result.Terminal.GoalEvidenceBacksCompletion.Value ?? false,
            CoverageAvailability: result.Coverage.Availability.Value ?? string.Empty,
            ScopeCounts: scopes,
            LedgerDigest: result.Coverage.LedgerDigest.Value,
            DeclaredMaximumDepth: result.Admission.DeclaredMaximumDepth.Value);

        return new SettingsExplorationOutcome(evidence, run);
    }
}