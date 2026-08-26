using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Fixtures;
using UniClaw.Runtime.ValidationHarness.Hosting;
using UniClaw.Runtime.ValidationHarness.Reporting;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Scenarios;

/// <summary>
/// One bounded one-Directive-one-Run scenario execution: the complete
/// Driver → Collector → Verifier/Gates → Report composition (design D2/D3/D5/D7;
/// WI-EVH-005 5.1–5.3). The runner transports exactly ONE directive through the
/// EXISTING <c>run.strategy.start</c>, aggregates the ValidationResult from the
/// frozen read surfaces (Tier-A in-process), derives the boundary proof, and
/// emits the G1–G4 report — all through previously accepted components, zero
/// new Runtime surface, zero new wire method.
/// </summary>
/// <param name="Dispatch">Driver dispatch outcome (admission or deterministic refusal).</param>
/// <param name="Admission">Admission receipt view; a synthetic rejected view on
/// transport failure (accepted=false, no run — never a fabricated runtime fact).</param>
/// <param name="RunId">DriverHost-owned run identity, null when nothing was admitted.</param>
/// <param name="StrategyId">Strategy identity CARRIED BY THE TRANSPORTED DIRECTIVE
/// (the fixture-authored payload identity; the runtime-attested identity lives in
/// <see cref="Result"/>.Admission).</param>
/// <param name="RunCallLog">This run's bounded call-log slice (exactly one
/// <c>run.strategy.start</c> dispatch; zero driver activity after admission).</param>
/// <param name="DriverCallLog">The driver's full immutable call log at this point
/// (grows across chained runs — S3's cross-run boundary proof).</param>
/// <param name="TransportedPayloads">Canonical payloads that EXERCISED the transport
/// (the exact scan set the boundary no-injection proof re-validates).</param>
/// <param name="Boundary">Derived boundary proof (design D5).</param>
/// <param name="Gates">G1–G4 gate outcomes (design D7).</param>
/// <param name="Report">Composed report (result + gates + boundary).</param>
/// <param name="ReportJson">Rendered JSON report (deterministic).</param>
/// <param name="ReportMarkdown">Rendered Markdown report.</param>
public sealed record ScenarioRunOutcome(
    DriverDispatchResult Dispatch,
    StrategyRunAdmissionView Admission,
    string? RunId,
    string StrategyId,
    ValidationResult Result,
    EmulatorCallLog RunCallLog,
    EmulatorCallLog DriverCallLog,
    IReadOnlyList<JsonObject> TransportedPayloads,
    BoundaryVerification Boundary,
    ValidationGates Gates,
    ValidationReport Report,
    string ReportJson,
    string ReportMarkdown)
{
    /// <summary>This run accepted exactly one directive into exactly one run.</summary>
    public bool AdmittedRun => RunId is not null && Admission.Accepted;
}

/// <summary>
/// Scenario composition root (WI-EVH-005 5.1–5.3): runs one bounded
/// one-Directive-one-Run execution on the Tier-A in-process host, reusing the
/// accepted EmulatorDriver → ResultCollector → BoundaryVerifier →
/// ValidationGateEvaluator → ValidationReportRenderer chain. The directive
/// comes from the recorded fixture catalog (deterministic mode, design D2); the
/// harness never authors a strategy. A <paramref name="priorCallLog"/> lets
/// multi-Run scenarios (S3) grow ONE immutable driver call log across runs so
/// the cross-run boundary proof sees every dispatch.
/// </summary>
public static class ScenarioRunner
{
    /// <summary>
    /// Execute one scenario run. The directive transport count is bounded to
    /// exactly one <c>run.strategy.start</c>; everything after admission is the
    /// run's own autonomous progress — ZERO driver calls (the collector's reads
    /// go through the frozen read surface, never the driver).
    /// </summary>
    public static async Task<ScenarioRunOutcome> RunTierAAsync(
        TierAHost host,
        DirectiveFixtureRecord fixture,
        EmulatorCallLog? priorCallLog = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(fixture);

        var transport = new LoopbackEmulatorTransport(host.BoundPort);
        var driver = new EmulatorDriver(transport, initialLog: priorCallLog);
        var sliceStart = driver.CallLog.Count;

        var dispatch = await driver.StartAsync(fixture, cancellationToken).ConfigureAwait(false);
        var fullLog = driver.CallLog;
        var runLog = EmulatorCallLog.FromEntries(fullLog.Entries.Skip(sliceStart));

        // Admission receipt: the real receipt on Transported; a harness-local
        // rejected view on transport failure — accepted=false, no run, no
        // invented runtime fact (the call-log entry carries the truthful reason).
        StrategyRunAdmissionView admission;
        switch (dispatch)
        {
            case DriverDispatchResult.Transported transported:
                admission = transported.Admission;
                break;
            case DriverDispatchResult.TransportFailed failed:
                admission = new StrategyRunAdmissionView(
                    Accepted: false,
                    RunId: null,
                    RunState: null,
                    RejectionCode: "TRANSPORT_FAILED",
                    RejectionReason: failed.Reason);
                break;
            default:
                // DirectiveRequired / RejectedBeforeTransport — zero wire calls.
                admission = new StrategyRunAdmissionView(
                    Accepted: false, RunId: null, RunState: null, RejectionCode: null, RejectionReason: null);
                break;
        }

        var result = await CollectAsync(host, admission, cancellationToken).ConfigureAwait(false);

        // The payload scan set: every canonical payload that EXERCISED the
        // transport (accepted / rejected-by-admission / failed transport carry a
        // payload that crossed the wire; refusals before transport never do).
        var transportedPayloads = ImmutableArray<JsonObject>.Empty;
        if (fixture.Directive is not null
            && dispatch is DriverDispatchResult.Transported or DriverDispatchResult.TransportFailed)
        {
            transportedPayloads = [StrategyPayloadJson.Freeze(fixture.Directive)];
        }

        var boundary = BoundaryVerifier.Verify(
            callLog: runLog,
            result: result,
            expectedStartCount: 1,
            transportedDirectives: transportedPayloads);
        var gates = ValidationGateEvaluator.Evaluate(
            result: result,
            boundary: boundary,
            callLog: runLog,
            expectedStartCount: 1,
            transportedDirectives: transportedPayloads);
        var report = new ValidationReport(result, gates, boundary);

        return new ScenarioRunOutcome(
            Dispatch: dispatch,
            Admission: admission,
            RunId: admission.RunId,
            StrategyId: fixture.Directive?.StrategyId ?? string.Empty,
            Result: result,
            RunCallLog: runLog,
            DriverCallLog: fullLog,
            TransportedPayloads: transportedPayloads,
            Boundary: boundary,
            Gates: gates,
            Report: report,
            ReportJson: ValidationReportRenderer.ToJson(report).ToJsonString(),
            ReportMarkdown: ValidationReportRenderer.ToMarkdown(report));
    }

    /// <summary>Aggregate the run through the collector over the Tier-A surface
    /// (surface typed to the frozen read operations; ledger attested in-process).</summary>
    private static async Task<ValidationResult> CollectAsync(
        TierAHost host,
        StrategyRunAdmissionView admission,
        CancellationToken cancellationToken)
    {
        // No run was admitted — the collector records every section as
        // Unavailable with classification (truthful; never fabricated).
        IRuntimeReadSurface surface = admission.Accepted && !string.IsNullOrWhiteSpace(admission.RunId)
            ? new TierAReadSurface(host, admission.RunId!)
            : new NullReadSurface();
        return await new ResultCollector(surface, admission).CollectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Admission-rejection surface: every read answers empty/unavailable
    /// (the collector's rejected-admission path never issues reads).</summary>
    private sealed class NullReadSurface : IRuntimeReadSurface
    {
        public Task<RunSnapshot> GetRunSnapshotAsync(string runId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("rejected admission: no run exists to snapshot");

        public Task<SurfaceEventPage> GetRuntimeEventsAfterAsync(string runId, EventCursor? cursor = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("rejected admission: no run event stream exists");

        public Task<SurfaceEventPage> DrainRuntimeEventsAsync(string runId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("rejected admission: no run event stream exists");

        public Task<InspectTrapResult> GetRunTrapAsync(string runId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("rejected admission: no run trap exists");

        public Task<EvidenceResolution> GetEvidenceAsync(EvidenceRef evidenceRef, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("rejected admission: no evidence exists");
    }
}