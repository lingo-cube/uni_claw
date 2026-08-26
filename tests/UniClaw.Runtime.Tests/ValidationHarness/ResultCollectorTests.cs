using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Results;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-EVH-003 capability tests (task 4.4): the Result Collector's truthfulness
/// contract —
///  1. a completed fixture run aggregates a ValidationResult whose every field
///     carries one of the three truth-source classifications tracing to the
///     admission receipt / read surface / Tier-A read model, with the Boundary
///     placeholder section present;
///  2. aggregating through the wire surface only leaves ledger coverage
///     explicitly Unavailable with classification and zero fabricated values;
///  3. Tier-A coverage attests the full ledger (five per-scope counts + stable
///     digest); aggregating twice from the same evidence yields an identical
///     digest;
///  4. EvidenceRefs are resolved through evidence.get; an unresolvable
///     synthetic ref through the harness adapter is recorded unresolvable —
///     never dropped, never fabricated.
/// Structure follows EvidenceFixture → Runtime Execution → Evidence
/// Evaluation; assertions check capabilities (admission legality, autonomy via
/// the read surface, classified fields, ledger accounting) — never fixed click
/// counts, coordinates, page text, UI paths, or action histories.
/// </summary>
public sealed class ResultCollectorTests
{
    // ── 4.4#1: completed run → every populated field carries one of the three
    //    classifications tracing to surfaces; Boundary placeholder present ───

    [Fact]
    public async Task CompleteFixtureRun_Aggregate_EveryFieldClassified_BoundarySectionExists()
    {
        // ── EvidenceFixture ─────────────────────────────────────────────────
        var world = FixtureComposition.CreateSettingsWorld();
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(world),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));

        // ── Runtime Execution: exactly one run.strategy.start via the wire ──
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        Assert.True(transported.Admission.Accepted);
        var runId = transported.Admission.RunId!;

        // Collector + Tier-A read surface (agent captured at admission).
        var collector = new ResultCollector(
            new TierAReadSurface(host, runId),
            transported.Admission);

        // ── Evidence Evaluation ─────────────────────────────────────────────
        var result = await collector.CollectAsync();
        Assert.Equal(runId, result.Admission.RunId.Value);

        // Every field carries one of the three classifications + a truth source.
        var fields = result.EnumerateClassifiedFields().ToArray();
        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            Assert.True(
                field.Classification is ResultFieldClassification.DirectProjection
                    or ResultFieldClassification.DerivedReadModel
                    or ResultFieldClassification.Unavailable,
                $"field must carry one of the three classifications (got {field.Classification}).");
            Assert.False(string.IsNullOrWhiteSpace(field.TruthSource), "every field records a truth-source statement.");
            // Truthfulness: a populated value is never classified Unavailable.
            if (field.RawValue is not null)
            {
                Assert.NotEqual(ResultFieldClassification.Unavailable, field.Classification);
            }
        }

        // Traceability spot checks: snapshot state is a direct public
        // projection; Tier-A coverage is a derived read model.
        Assert.Equal(
            ResultFieldClassification.DirectProjection,
            result.Snapshot.RunState.Classification);
        Assert.Equal(
            ResultFieldClassification.DerivedReadModel,
            result.Coverage.Ledger.Classification);
        Assert.Equal(RunState.Completed, result.Terminal.TerminalState.Value);

        // Boundary placeholder section exists (typed empty in this increment).
        Assert.NotNull(result.Boundary);
        Assert.Equal(BoundarySection.Placeholder, result.Boundary);
    }

    // ── 4.4#2: wire surface only → ledger coverage Unavailable + classified,
    //    no fabricated values ────────────────────────────────────────────────

    [Fact]
    public async Task AggregateViaWireSurfaceOnly_LedgerCoverageUnavailable_NoFabricatedValues()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        Assert.True(transported.Admission.Accepted);

        // The collector reads ONLY the loopback wire surface (no in-process
        // Agent seam) — exactly the Tier-B/C read path.
        var collector = new ResultCollector(new WireReadSurface(host.BoundPort), transported.Admission);
        var result = await collector.CollectAsync();

        // Ledger-level coverage is explicitly unavailable WITH classification.
        Assert.Equal(ResultFieldClassification.Unavailable, result.Coverage.Ledger.Classification);
        Assert.Null(result.Coverage.Ledger.Value);
        Assert.Equal(ResultFieldClassification.Unavailable, result.Coverage.Scopes.Classification);
        // ImmutableArray<T> is a value type: an Unavailable field holds the
        // default (empty) array, not a null reference. The truthful assertion
        // is default-or-empty (no fabricated scope rows), via RawValue null.
        var scopesField = (UniClaw.Runtime.ValidationHarness.Results.IClassifiedField)result.Coverage.Scopes;
        Assert.Null(scopesField.RawValue);
        var scopesValue = result.Coverage.Scopes.Value;
        Assert.True(scopesValue.IsDefault
            || (scopesValue.IsDefaultOrEmpty && scopesValue.Length == 0),
            "an Unavailable struct field must hold no fabricated scope rows");
        Assert.Equal(ResultFieldClassification.Unavailable, result.Coverage.LedgerDigest.Classification);
        Assert.Null(result.Coverage.LedgerDigest.Value);
        Assert.Equal("wireTier-unavailable", result.Coverage.Availability.Value);

        // Nothing else is fabricated: the strategy identity of the wire tier
        // has no frozen surface source → unavailable, not invented.
        Assert.Equal(ResultFieldClassification.Unavailable, result.Admission.StrategyId.Classification);
        Assert.Equal(ResultFieldClassification.Unavailable, result.Admission.DeclaredMaximumDepth.Classification);

        // The wire tier still truthfully reads the frozen surface facts.
        Assert.Equal(ResultFieldClassification.DirectProjection, result.Snapshot.RunState.Classification);
        Assert.Equal(RunState.Completed, result.Terminal.TerminalState.Value);
    }

    // ── 4.4#3: Tier-A coverage has five counts + digest; identical digest
    //    across aggregation of the same evidence ─────────────────────────────

    [Fact]
    public async Task TierAAttestation_FivePerScopeCountsAndDigest_IdenticalAcrossReaggregation()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        Assert.True(transported.Admission.Accepted);

        var collector = new ResultCollector(
            new TierAReadSurface(host, transported.Admission.RunId!),
            transported.Admission);

        var first = await collector.CollectAsync();
        var second = await collector.CollectAsync();

        Assert.Equal(ResultFieldClassification.DerivedReadModel, first.Coverage.Ledger.Classification);
        var ledger = first.Coverage.Ledger.Value;
        Assert.NotNull(ledger);

        // Five per-scope counts, copied verbatim from the read model.
        Assert.NotNull(first.Coverage.Scopes.Value);
        Assert.NotEmpty(first.Coverage.Scopes.Value!);
        foreach (var scope in first.Coverage.Scopes.Value!)
        {
            Assert.False(string.IsNullOrWhiteSpace(scope.ScopeIdentity));
            Assert.True(scope.Discovered >= 0);
            Assert.True(scope.Visited >= 0);
            Assert.True(scope.Pending >= 0);
            Assert.True(scope.Unresolved >= 0);
            Assert.True(scope.UnknownFrontier >= 0);
        }

        // Digest present and stable across aggregation of the same evidence.
        Assert.False(string.IsNullOrWhiteSpace(first.Coverage.LedgerDigest.Value));
        Assert.Equal(ledger!.LedgerDigest, first.Coverage.LedgerDigest.Value);
        Assert.Equal(first.Coverage.LedgerDigest.Value, second.Coverage.LedgerDigest.Value);
    }

    // ── 4.4#4: EvidenceRefs resolve through evidence.get; an unresolvable
    //    synthetic ref through the harness adapter is recorded unresolvable ──

    [Fact]
    public async Task EvidenceRef_UnresolvableSynthetic_RecordedUnresolvable_NotDroppedNotFabricated()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        var runId = transported.Admission.RunId!;

        // A logical, synthetic evidence reference (never a path/URL) that no
        // captured catalog can resolve — resolution MUST go through evidence.get.
        var synthetic = new EvidenceRef
        {
            EvidenceId = "capture:missing-session:record:1",
            Kind = EvidenceKind.TraceFragment,
            RunId = runId,
            Locator = "capture:missing-session:record:1",
        };

        var collector = new ResultCollector(
            new WireReadSurface(host.BoundPort),
            transported.Admission,
            evidenceRefsToResolve: [synthetic]);

        var result = await collector.CollectAsync();

        var entries = result.Evidence.Entries.Value;
        Assert.NotNull(entries);

        var entry = Assert.Single(entries!, e => e.RequestedRef.Locator == synthetic.Locator);
        // Recorded as unresolvable — the answer comes from the runtime surface
        // (no fabricated record), and the entry is never dropped.
        Assert.False(entry.Resolved.Value);
        Assert.False(string.IsNullOrWhiteSpace(entry.Diagnostic.Value));
        Assert.Contains("catalog", entry.Diagnostic.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ResultFieldClassification.DirectProjection, entry.Resolved.Classification);
        Assert.Equal(ResultFieldClassification.DirectProjection, entry.Diagnostic.Classification);
    }
}