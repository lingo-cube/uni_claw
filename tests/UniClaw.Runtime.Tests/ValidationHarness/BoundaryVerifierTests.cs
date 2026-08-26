using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Reporting;
using UniClaw.Runtime.ValidationHarness.Results;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-EVH-004 capability tests (tasks 6.1-6.4): the derived Boundary Verifier,
/// the G1-G4 gate report fields, and the tiered JSON/Markdown report rendering.
/// Structure is EvidenceFixture → Runtime Execution → Evidence Evaluation; the
/// adversarial test INPUTS (an injected-action payload string, a simulated
/// mutating call-log entry, a synthetic evidence ref) are deliberately
/// supplied to the verification layer — no assertion depends on fixed click
/// counts, coordinates, page text, UI paths, or action histories; assertions
/// check the violation records, the gate outcomes, and the rendered facts.
/// </summary>
public sealed class BoundaryVerifierTests
{
    // ── 6.4#1: injected-action directive payload → flagged violation with the
    //    offending record attached; G1/G4 fail (spec "Boundary violations are
    //    detectable") ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InjectedActionPayload_IsFlaggedWithOffendingRecord_GateFails()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var (driver, _, result, _, _) = await RunCleanFixtureAsync(host);

        // Adversarial input: the canonical strategy payload that CROSSED the
        // wire carries injected action-selection content in a string field.
        var injected = FreezeSettingsExplorePayload();
        injected["scope"]!["semanticRoot"] = "click the profile button";

        var boundary = BoundaryVerifier.Verify(driver.CallLog, result, expectedStartCount: 1, [injected]);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, driver.CallLog, 1, [injected]);

        Assert.False(boundary.Passed);
        var injection = boundary[BoundaryProhibitionKind.NoActionInjection];
        Assert.False(injection.Positive);
        var violation = Assert.Single(injection.Violations);
        Assert.Equal(BoundaryProhibitionKind.NoActionInjection, violation.Prohibition);
        Assert.Contains("strategy.scope.semanticRoot", violation.OffendingRecord, StringComparison.Ordinal);
        Assert.Contains("ActionSelection", violation.Reason, StringComparison.Ordinal);

        // G1 (directive-legal) and G4 (boundary clean) fail with the offending
        // payload attached; G2/G3 stay pass — nothing is weakened.
        Assert.False(gates.G1.Passed);
        Assert.Contains("semanticRoot", gates.G1.OffendingEvidence, StringComparison.Ordinal);
        Assert.False(gates.G4.Passed);
        Assert.Contains("semanticRoot", gates.G4.OffendingEvidence, StringComparison.Ordinal);
        Assert.True(gates.G2.Passed);
        Assert.True(gates.G3.Passed);
    }

    // ── 6.4#2: simulated mutating call in the call log → flagged with the
    //    offending entry; gates fail (spec "Boundary violations are detectable") ──

    [Fact]
    public async Task SimulatedMutatingCall_IsFlaggedWithOffendingEntry_GateFails()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var (_, _, result, runId, payload) = await RunCleanFixtureAsync(host);

        // Simulated log: the real accepted start plus a MUTATING foreign wire
        // call recorded after admission (a harness that steers the runtime).
        var timestamp = DateTimeOffset.UtcNow;
        var taintedLog = EmulatorCallLog.Empty
            .Append(EmulatorCallLogEntry.Accepted(EmulatorDriver.StartStrategyMethod, "digest-start", runId, timestamp))
            .Append(new EmulatorCallLogEntry("run.strategy.override", "digest-override", EmulatorCallOutcome.Accepted, "override-1", timestamp.AddSeconds(1)));

        var boundary = BoundaryVerifier.Verify(taintedLog, result, expectedStartCount: 1, [payload]);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, taintedLog, 1, [payload]);

        Assert.False(boundary.Passed);
        var mutation = boundary[BoundaryProhibitionKind.NoRuntimeStateMutation];
        Assert.False(mutation.Positive);
        Assert.Contains(
            mutation.Violations,
            violation => violation.OffendingRecord.Contains("run.strategy.override", StringComparison.Ordinal));
        Assert.Contains(
            mutation.Violations,
            violation => violation.OffendingRecord.Contains("start count 2", StringComparison.Ordinal));

        Assert.False(gates.G4.Passed);
        Assert.Contains("run.strategy.override", gates.G4.OffendingEvidence, StringComparison.Ordinal);
        // End-to-end autonomy is also broken: a driver call outside
        // run.strategy.start was recorded (autonomy forbids ANY harness call).
        Assert.False(gates.G2.Passed);
        Assert.Contains("run.strategy.override", gates.G2.OffendingEvidence, StringComparison.Ordinal);
    }

    // ── 6.4#3: unresolvable evidence ref → flagged with the offending ref;
    //    G4 fails (spec "Boundary violations are detectable") ──────────────────

    [Fact]
    public async Task UnresolvableEvidenceRef_IsFlaggedWithOffendingRef_GateFails()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        var runId = transported.Admission.RunId!;
        var payload = FreezeSettingsExplorePayload();

        // A logical synthetic ref no capture catalog can resolve — resolution
        // MUST go through evidence.get (collector records it, never fabricates).
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

        var boundary = BoundaryVerifier.Verify(driver.CallLog, result, expectedStartCount: 1, [payload]);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, driver.CallLog, 1, [payload]);

        Assert.False(boundary.Passed);
        var fabrication = boundary[BoundaryProhibitionKind.NoEvidenceFabrication];
        Assert.False(fabrication.Positive);
        var violation = Assert.Single(fabrication.Violations);
        Assert.Contains("capture:missing-session:record:1", violation.OffendingRecord, StringComparison.Ordinal);

        Assert.False(gates.G4.Passed);
        Assert.Contains("capture:missing-session:record:1", gates.G4.OffendingEvidence, StringComparison.Ordinal);
        Assert.True(gates.G1.Passed, "the directive itself is legal; only the fabricated ref fails the boundary");
        Assert.True(gates.G2.Passed);
        Assert.True(gates.G3.Passed);
    }

    // ── 6.4#4: clean fixture run → positive bound evidence for all four
    //    prohibitions, gates pass, no failure (spec "Clean run proves the
    //    boundary") ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CleanFixtureRun_AllFourProhibitionsPositive_GatesPass_NoFailure()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var (driver, _, result, _, payload) = await RunCleanFixtureAsync(host);

        var boundary = BoundaryVerifier.Verify(driver.CallLog, result, expectedStartCount: 1, [payload]);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, driver.CallLog, 1, [payload]);

        Assert.True(boundary.Passed, "the clean fixture run must prove every prohibition");
        Assert.Empty(boundary.Violations);
        foreach (var kind in new[]
                 {
                     BoundaryProhibitionKind.NoRuntimeStateMutation,
                     BoundaryProhibitionKind.NoActionInjection,
                     BoundaryProhibitionKind.NoFsmControl,
                     BoundaryProhibitionKind.NoEvidenceFabrication,
                 })
        {
            var outcome = boundary[kind];
            Assert.True(outcome.Positive, $"{kind} must carry positive bound evidence");
            Assert.Empty(outcome.Violations);
            Assert.NotEmpty(outcome.EvidenceRefs);
        }

        Assert.True(gates.AllPass, "all four gates pass on the deterministic tier");
        Assert.True(gates.G1.Passed);
        Assert.True(gates.G2.Passed);
        Assert.True(gates.G3.Passed);
        Assert.True(gates.G4.Passed);
        Assert.Null(gates.G1.OffendingEvidence);
        Assert.Null(gates.G4.OffendingEvidence);
    }

    // ── 6.4#5a: report rendering — JSON + Markdown contain the eight sections
    //    and the G1-G4 fields (spec "All gates pass on the deterministic tier"
    //    report eligibility) ──────────────────────────────────────────────────

    [Fact]
    public async Task Report_JsonAndMarkdown_ContainEightSectionsAndG1G4()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var (driver, _, result, _, payload) = await RunCleanFixtureAsync(host);
        var boundary = BoundaryVerifier.Verify(driver.CallLog, result, 1, [payload]);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, driver.CallLog, 1, [payload]);
        var report = new ValidationReport(result, gates, boundary);

        var json = ValidationReportRenderer.ToJson(report);
        var sections = json["validationReport"]!["sections"]!.AsObject();
        foreach (var section in new[] { "admission", "lifecycle", "snapshot", "trap", "evidence", "coverage", "terminal", "boundary" })
        {
            Assert.NotNull(sections[section]);
        }

        var gatesJson = json["validationReport"]!["gates"]!.AsObject();
        foreach (var gate in new[] { "g1", "g2", "g3", "g4" })
        {
            var gateNode = gatesJson[gate];
            Assert.NotNull(gateNode);
            Assert.True(gateNode!["passed"]!.GetValue<bool>(), $"clean report: gate {gate} must render pass");
        }

        var markdown = ValidationReportRenderer.ToMarkdown(report);
        foreach (var header in new[]
                 {
                     "## Admission", "## Lifecycle", "## Snapshot", "## Trap",
                     "## Evidence", "## Coverage", "## Terminal", "## Boundary", "## Gates",
                 })
        {
            Assert.Contains(header, markdown, StringComparison.Ordinal);
        }

        Assert.Contains("- G1 directive-legal: PASS", markdown, StringComparison.Ordinal);
        Assert.Contains("- G2 end-to-end autonomy: PASS", markdown, StringComparison.Ordinal);
        Assert.Contains("- G3 result evidence-backed: PASS", markdown, StringComparison.Ordinal);
        Assert.Contains("- G4 boundary clean: PASS", markdown, StringComparison.Ordinal);
    }

    // ── 6.4#5b: wire-tier ledger coverage renders unavailable with its reason
    //    (spec "Wire tiers record coverage availability truthfully") ───────────

    [Fact]
    public async Task Report_WireTierCoverage_RendersUnavailableWithReason()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        var payload = FreezeSettingsExplorePayload();

        // The collector reads ONLY the loopback wire surface — exactly the
        // Tier-B/C read path; the ledger is not on the frozen wire surface.
        var collector = new ResultCollector(new WireReadSurface(host.BoundPort), transported.Admission);
        var result = await collector.CollectAsync();
        var boundary = BoundaryVerifier.Verify(driver.CallLog, result, 1, [payload]);
        var gates = ValidationGateEvaluator.Evaluate(result, boundary, driver.CallLog, 1, [payload]);
        var report = new ValidationReport(result, gates, boundary);

        var json = ValidationReportRenderer.ToJson(report);
        var coverage = json["validationReport"]!["sections"]!["coverage"]!.AsObject();
        Assert.Equal("unavailable", coverage["ledger"]!["value"]!.GetValue<string>());
        var reason = coverage["ledger"]!["reason"]!.GetValue<string>();
        Assert.Contains("wire", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("wireTier-unavailable", coverage["availability"]!["value"]!.GetValue<string>());

        var markdown = ValidationReportRenderer.ToMarkdown(report);
        Assert.Contains("- ledger: unavailable", markdown, StringComparison.Ordinal);
        // Truthful tiering: G3 still passes because unavailable is recorded,
        // not fabricated — and no gate is weakened.
        Assert.True(gates.G3.Passed);
    }

    // ── 6.4#5c: a forced gate failure renders failed with the offending
    //    evidence; passing gates are NOT weakened (spec "A gate failure is
    //    reported, not masked") ───────────────────────────────────────────────

    [Fact]
    public async Task Report_ForcedGateFailure_RendersFailed_NothingWeakened()
    {
        using var host = new TierAHost(
            FixtureComposition.CreateFactory(FixtureComposition.CreateSettingsWorld()),
            FixtureComposition.CreateCompiler());
        var (driver, _, result, _, _) = await RunCleanFixtureAsync(host);

        // Forced failure at the verification layer: the transported payload
        // carries injected action content (G1) and the boundary flags it (G4).
        var injected = FreezeSettingsExplorePayload();
        injected["scope"]!["semanticRoot"] = "click the profile button";
        var cleanPayload = FreezeSettingsExplorePayload();

        var failingBoundary = BoundaryVerifier.Verify(driver.CallLog, result, 1, [injected]);
        var report = new ValidationReport(
            result,
            ValidationGateEvaluator.Evaluate(result, failingBoundary, driver.CallLog, 1, [injected]),
            failingBoundary);

        var json = ValidationReportRenderer.ToJson(report);
        var gatesJson = json["validationReport"]!["gates"]!.AsObject();
        Assert.False(gatesJson["g1"]!["passed"]!.GetValue<bool>());
        Assert.Contains("semanticRoot", gatesJson["g1"]!["offendingEvidence"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(gatesJson["g4"]!["passed"]!.GetValue<bool>());
        // G2/G3 were pass on the clean run and remain pass — nothing weakened.
        Assert.True(gatesJson["g2"]!["passed"]!.GetValue<bool>());
        Assert.True(gatesJson["g3"]!["passed"]!.GetValue<bool>());

        var boundaryJson = json["validationReport"]!["sections"]!["boundary"]!.AsObject();
        Assert.False(boundaryJson["passed"]!.GetValue<bool>());
        Assert.Contains(
            boundaryJson["prohibitions"]!.AsArray(),
            node => node!["violations"]!.AsArray().Count > 0);

        var markdown = ValidationReportRenderer.ToMarkdown(report);
        Assert.Contains("- G1 directive-legal: FAIL", markdown, StringComparison.Ordinal);
        Assert.Contains("- G4 boundary clean: FAIL", markdown, StringComparison.Ordinal);
        Assert.Contains("- G3 result evidence-backed: PASS", markdown, StringComparison.Ordinal);

        // Sanity: the same runner with the CLEAN payload passes every gate.
        var cleanBoundary = BoundaryVerifier.Verify(driver.CallLog, result, 1, [cleanPayload]);
        var cleanGates = ValidationGateEvaluator.Evaluate(result, cleanBoundary, driver.CallLog, 1, [cleanPayload]);
        Assert.True(cleanGates.AllPass);
    }

    // ── 6.1 rendering contract, pure over the renderer: value +
    //    classification + truth-source; Unavailable renders as "unavailable"
    //    with its reason; IsPartial renders as "partial" ──────────────────────

    [Fact]
    public void Report_FieldRendering_PartialAndUnavailable_RenderAsDeclared()
    {
        // Deterministic minimal result: one direct field, one PARTIAL derived
        // field, one UNAVAILABLE field — the rendering contract is asserted on
        // the pure renderer, independent of any fixture's field semantics.
        var partialPage = new ResultField<string?>
        {
            Value = "settings",
            Classification = ResultFieldClassification.DerivedReadModel,
            TruthSource = "probe: derived read model",
            IsPartial = true,
        };
        var result = BuildMinimalResult(partialPage);
        var boundary = BoundaryVerifier.Verify(
            EmulatorCallLog.Empty,
            result,
            expectedStartCount: 0);
        var report = new ValidationReport(
            result,
            new ValidationGates(
                new GateOutcome(true, ["probe"]),
                new GateOutcome(true, ["probe"]),
                new GateOutcome(true, ["probe"]),
                new GateOutcome(true, ["probe"])),
            boundary);
        Assert.True(boundary.Passed);

        var json = ValidationReportRenderer.ToJson(report);
        var snapshot = json["validationReport"]!["sections"]!["snapshot"]!.AsObject();
        Assert.Equal("settings", snapshot["currentSemanticPage"]!["value"]!.GetValue<string>());
        Assert.Equal("DerivedReadModel", snapshot["currentSemanticPage"]!["classification"]!.GetValue<string>());
        Assert.True(snapshot["currentSemanticPage"]!["partial"]!.GetValue<bool>(), "IsPartial must render as partial");

        Assert.Equal("unavailable", snapshot["recoveryState"]!["value"]!.GetValue<string>());
        Assert.Equal("probe: no truthful surface source", snapshot["recoveryState"]!["reason"]!.GetValue<string>());
        Assert.Equal("Unavailable", snapshot["recoveryState"]!["classification"]!.GetValue<string>());

        Assert.Equal("run-x", json["validationReport"]!["sections"]!["admission"]!["runId"]!["value"]!.GetValue<string>());
        Assert.Equal("probe: admission receipt", json["validationReport"]!["sections"]!["admission"]!["runId"]!["truthSource"]!.GetValue<string>());

        var markdown = ValidationReportRenderer.ToMarkdown(report);
        Assert.Contains("- currentSemanticPage: settings [DerivedReadModel] (partial)", markdown, StringComparison.Ordinal);
        Assert.Contains("- recoveryState: unavailable [Unavailable]", markdown, StringComparison.Ordinal);
    }

    /// <summary>Minimal fully-classified result over a partial snapshot page
    /// field; coverage mirrors the wire tier (ledger unavailable).</summary>
    private static ValidationResult BuildMinimalResult(ResultField<string?> partialPage)
        => new(
            Admission: new AdmissionSection(
                ResultField<string>.Direct("run-x", "probe: admission receipt"),
                ResultField<string>.Unavailable("probe: not on wire surface"),
                ResultField<bool>.Direct(true, "probe: admission receipt"),
                ResultField<string?>.Direct(null, "probe: admission receipt"),
                ResultField<string?>.Direct(null, "probe: admission receipt"),
                ResultField<int>.Unavailable("probe: not on wire surface")),
            Lifecycle: new LifecycleSection(ResultField<ImmutableArray<SurfaceRuntimeEvent>>.Derived([], "probe: projected stream")),
            Snapshot: new SnapshotSection(
                ResultField<string>.Direct("run-x", "probe: snapshot"),
                ResultField<RunState>.Direct(RunState.Completed, "probe: snapshot"),
                partialPage,
                ResultField<Trap?>.Direct(null, "probe: snapshot"),
                ResultField<GoalSummary?>.Direct(null, "probe: snapshot"),
                ResultField<DecisionSummary?>.Direct(null, "probe: snapshot"),
                ResultField<ActionSummary?>.Direct(null, "probe: snapshot"),
                ResultField<RecoverySummary?>.Unavailable("probe: no truthful surface source"),
                ResultField<GoalEvidenceSummary?>.Direct(null, "probe: snapshot"),
                ResultField<long?>.Direct(null, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<string?>.Direct(null, "probe: snapshot"),
                ResultField<ImmutableArray<string>>.Direct([], "probe: snapshot")),
            Trap: new TrapSection(
                ResultField<bool>.Direct(false, "probe: trap"),
                ResultField<Trap?>.Direct(null, "probe: trap"),
                ResultField<string?>.Direct(null, "probe: trap")),
            Evidence: new EvidenceSection(ResultField<ImmutableArray<ValidationEvidenceEntry>>.Derived([], "probe: evidence resolutions")),
            Coverage: new CoverageSection(
                ResultField<string>.Direct("wireTier-unavailable", "probe: tier composition"),
                ResultField<ExplorationLedgerView?>.Unavailable("probe: ledger not on the wire surface"),
                ResultField<ImmutableArray<CoverageScopeCounts>>.Unavailable("probe: ledger not on the wire surface"),
                ResultField<string?>.Unavailable("probe: ledger not on the wire surface")),
            Terminal: new TerminalSection(
                ResultField<RunState>.Direct(RunState.Completed, "probe: terminal"),
                ResultField<string?>.Direct("completed", "probe: terminal"),
                ResultField<bool?>.Direct(true, "probe: terminal")),
            Boundary: BoundarySection.Placeholder);

    // ── shared fixture helper ─────────────────────────────────────────────────

    /// <summary>Canonical strategy payload of the recorded SettingsExplore
    /// fixture (exactly what the driver froze before transport).</summary>
    private static JsonObject FreezeSettingsExplorePayload()
        => StrategyPayloadJson.Freeze(DirectiveFixtureCatalog.SettingsExplore().Directive!);

    /// <summary>EvidenceFixture → Runtime Execution (exactly one
    /// run.strategy.start via the wire) → Tier-A aggregation. Admission must
    /// be accepted; the result is aggregated through the Tier-A read surface.</summary>
    private static async Task<(EmulatorDriver Driver, StrategyRunAdmissionView Admission, ValidationResult Result, string RunId, JsonObject Payload)> RunCleanFixtureAsync(TierAHost host)
    {
        var driver = new EmulatorDriver(new LoopbackEmulatorTransport(host.BoundPort));
        var dispatch = await driver.StartAsync(DirectiveFixtureCatalog.SettingsExplore());
        var transported = Assert.IsType<DriverDispatchResult.Transported>(dispatch);
        Assert.True(transported.Admission.Accepted);
        var runId = transported.Admission.RunId!;
        var payload = FreezeSettingsExplorePayload();

        var collector = new ResultCollector(new TierAReadSurface(host, runId), transported.Admission);
        var result = await collector.CollectAsync();
        return (driver, transported.Admission, result, runId, payload);
    }
}