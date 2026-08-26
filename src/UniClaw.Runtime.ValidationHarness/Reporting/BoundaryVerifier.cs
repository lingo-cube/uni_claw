using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Reporting;

/// <summary>
/// The four boundary prohibitions the verifier proves (design D5; spec
/// "Evidence boundary verification"): the Emulator performed no Runtime state
/// mutation, no FSM control, no action injection, and no evidence fabrication.
/// </summary>
public enum BoundaryProhibitionKind
{
    /// <summary>(a) zero mutating wire calls — call-log proof: the only mutating
    /// op is <c>run.strategy.start</c>, invoked exactly the scenario's start
    /// count.</summary>
    NoRuntimeStateMutation = 0,

    /// <summary>(b) no injected actions — directive payload scans reusing the
    /// <see cref="StrategyDirectiveValidator"/> closed-vocabulary + forbidden
    /// content result.</summary>
    NoActionInjection = 1,

    /// <summary>(c) no FSM control — every received event belongs to the
    /// audited A/B source-classified vocabulary; C-class kinds are absent.</summary>
    NoFsmControl = 2,

    /// <summary>(d) no evidence fabrication — every <c>EvidenceRef</c> resolves
    /// through <c>evidence.get</c> (the collector's evidence section).</summary>
    NoEvidenceFabrication = 3,
}

/// <summary>
/// One detected boundary violation, carrying the OFFENDING RECORD (the exact
/// call-log entry / payload field path / event / evidence reference) plus the
/// reason. Never a bare "Runtime failed" — the record that failed is attached
/// (change principle: 违规输出携带肇事记录).
/// </summary>
public sealed record BoundaryViolation(
    BoundaryProhibitionKind Prohibition,
    string OffendingRecord,
    string Reason);

/// <summary>
/// Outcome of one prohibition: <see cref="Positive"/> means positive bound
/// evidence for the prohibition was established (a clean bound, not merely
/// absence of a failure), with the <see cref="EvidenceRefs"/> that support it;
/// otherwise every <see cref="Violations"/> carries the offending record.
/// </summary>
public sealed record BoundaryProhibitionResult(
    BoundaryProhibitionKind Prohibition,
    bool Positive,
    ImmutableArray<string> EvidenceRefs,
    ImmutableArray<BoundaryViolation> Violations);

/// <summary>
/// The complete boundary proof (design D5, derived — never instrumented): the
/// four prohibition results in the fixed order
/// mutation / injection / FSM / fabrication. <see cref="Passed"/> is true only
/// when ALL four prohibitions carry positive bound evidence.
/// </summary>
public sealed record BoundaryVerification(
    ImmutableArray<BoundaryProhibitionResult> Prohibitions)
{
    /// <summary>Boundary clean: every prohibition has positive bound evidence.</summary>
    public bool Passed => Prohibitions.All(p => p.Positive);

    /// <summary>All detected violations, across prohibitions, in fixed order.</summary>
    public IEnumerable<BoundaryViolation> Violations
        => Prohibitions.SelectMany(p => p.Violations);

    /// <summary>Outcome of one prohibition.</summary>
    public BoundaryProhibitionResult this[BoundaryProhibitionKind kind]
        => Prohibitions.First(p => p.Prohibition == kind);
}

/// <summary>
/// Boundary verifier (task 6.2): derives the boundary proof from FOUR existing
/// surfaces only — the immutable call log, the directive payload scans
/// (reusing <see cref="StrategyDirectiveValidator"/> results), the audited
/// A/B event-source classification table the DriverHost exposes, and the
/// collector's <c>evidence.get</c> resolution outcomes. There is ZERO Runtime
/// instrumentation: no probe, no extra surface, no new collection — a pure
/// function over facts the harness already holds.
/// </summary>
public static class BoundaryVerifier
{
    /// <summary>
    /// Verify the boundary over one call log + one aggregated result
    /// (+ the canonical strategy payloads that were actually transported, so
    /// the no-injection scan covers exactly what crossed the wire).
    /// </summary>
    /// <param name="callLog">Immutable call log (design D5.1) — proof (a).</param>
    /// <param name="result">Aggregated ValidationResult — proofs (c) and (d)
    /// read its lifecycle events and evidence section (collector outcomes).</param>
    /// <param name="expectedStartCount">Exact <c>run.strategy.start</c> transport
    /// count the scenario bounds (one-Directive-one-Run).</param>
    /// <param name="transportedDirectives">The canonical strategy payloads
    /// (<c>StrategyPayloadJson.Freeze</c> output) of every entry that exercised
    /// the transport. Scan coverage is exact: if this set does not match the
    /// transport count, the no-injection bound cannot be attested
    /// (fail-closed — absence of proof is never a pass).</param>
    /// <param name="validator">Optional injected validator (testability);
    /// defaults to the deterministic <see cref="StrategyDirectiveValidator"/>.</param>
    public static BoundaryVerification Verify(
        EmulatorCallLog callLog,
        ValidationResult result,
        int expectedStartCount,
        IReadOnlyList<JsonObject>? transportedDirectives = null,
        StrategyDirectiveValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(callLog);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedStartCount);
        var payloadScanner = validator ?? new StrategyDirectiveValidator();
        var relayed = transportedDirectives ?? [];

        return new BoundaryVerification(
            [
                VerifyNoRuntimeStateMutation(callLog, expectedStartCount),
                VerifyNoActionInjection(callLog, relayed, payloadScanner),
                VerifyNoFsmControl(result),
                VerifyNoEvidenceFabrication(result),
            ]);
    }

    // ---- (a) zero mutating wire calls, exact start counts ────────────────────

    private static BoundaryProhibitionResult VerifyNoRuntimeStateMutation(
        EmulatorCallLog callLog,
        int expectedStartCount)
    {
        var violations = ImmutableArray.CreateBuilder<BoundaryViolation>();
        var evidence = ImmutableArray.CreateBuilder<string>();
        var transportCount = 0;

        for (var index = 0; index < callLog.Entries.Length; index++)
        {
            var entry = callLog.Entries[index];
            var isStart = string.Equals(entry.Method, EmulatorDriver.StartStrategyMethod, StringComparison.Ordinal);
            if (!isStart)
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoRuntimeStateMutation,
                    $"call-log entry {index} (method '{entry.Method}', outcome {entry.Outcome})",
                    "a wire method outside run.strategy.start was invoked; run.strategy.start is the only mutating op the emulator may transport, and no foreign method may be called at all."));
            }

            var exercisedTransport = entry.Outcome is EmulatorCallOutcome.Accepted
                or EmulatorCallOutcome.RejectedByAdmission
                or EmulatorCallOutcome.TransportFailed;
            if (exercisedTransport)
            {
                transportCount++;
                evidence.Add($"call-log entry {index}: {entry.Method} → {entry.Outcome}" +
                             (entry.Outcome == EmulatorCallOutcome.Accepted ? $" (run {entry.Detail})" : string.Empty));
            }
        }

        if (transportCount != expectedStartCount)
        {
            violations.Add(new BoundaryViolation(
                BoundaryProhibitionKind.NoRuntimeStateMutation,
                $"call log start count {transportCount} ≠ scenario bound {expectedStartCount}",
                "exact run.strategy.start counts per scenario (one Directive, one Run) were violated."));
        }

        evidence.Add($"run.strategy.start transport count {transportCount} == expected {expectedStartCount}: {transportCount == expectedStartCount}");
        return new BoundaryProhibitionResult(
            BoundaryProhibitionKind.NoRuntimeStateMutation,
            Positive: violations.Count == 0 && transportCount == expectedStartCount,
            evidence.ToImmutable(),
            violations.ToImmutable());
    }

    // ---- (b) no injected actions: payload scans with the validator ───────────

    private static BoundaryProhibitionResult VerifyNoActionInjection(
        EmulatorCallLog callLog,
        IReadOnlyList<JsonObject> transportedDirectives,
        StrategyDirectiveValidator validator)
    {
        var violations = ImmutableArray.CreateBuilder<BoundaryViolation>();
        var evidence = ImmutableArray.CreateBuilder<string>();

        // The transport-exercising entries are the ones whose payloads crossed
        // the wire and therefore MUST carry an exact scan record.
        var transports = callLog.Entries
            .Where(e => e.Outcome is EmulatorCallOutcome.Accepted
                or EmulatorCallOutcome.RejectedByAdmission
                or EmulatorCallOutcome.TransportFailed)
            .ToArray();

        if (transportedDirectives.Count != transports.Length)
        {
            violations.Add(new BoundaryViolation(
                BoundaryProhibitionKind.NoActionInjection,
                $"payload scan coverage {transportedDirectives.Count}/{transports.Length} transport entries",
                "the no-injection bound could not be attested: every transported payload must be re-scanned; absence of proof is never a pass."));
            return new BoundaryProhibitionResult(
                BoundaryProhibitionKind.NoActionInjection,
                Positive: false,
                evidence.ToImmutable(),
                violations.ToImmutable());
        }

        for (var index = 0; index < transportedDirectives.Count; index++)
        {
            var payload = transportedDirectives[index];
            if (validator.Validate(payload) is DirectiveValidationResult.Rejected rejected)
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoActionInjection,
                    $"transported directive payload {index}: {rejected.Reason}",
                    (rejected.Category is not null
                        ? $"forbidden {rejected.Category} content was carried by a transported payload (category '{rejected.Category}')."
                        : "a transported payload failed the closed-vocabulary validation; injected content must never cross the wire.")));
            }
            else
            {
                evidence.Add($"payload scan {index}: legal (closed vocabulary, zero forbidden content)");
            }
        }

        // Driver-side scan refusals corroborate the defense (refused BEFORE
        // transport — the refusal itself is scan evidence, never a violation).
        foreach (var entry in callLog.Entries)
        {
            if (entry.Outcome == EmulatorCallOutcome.RejectedBeforeTransport)
            {
                evidence.Add($"driver-side scan refused an entry before transport: {entry.Detail}");
            }
        }

        return new BoundaryProhibitionResult(
            BoundaryProhibitionKind.NoActionInjection,
            Positive: violations.Count == 0,
            evidence.ToImmutable(),
            violations.ToImmutable());
    }

    // ---- (c) all received events in the audited A/B vocabulary ───────────────

    private static BoundaryProhibitionResult VerifyNoFsmControl(ValidationResult result)
    {
        var violations = ImmutableArray.CreateBuilder<BoundaryViolation>();
        var evidence = ImmutableArray.CreateBuilder<string>();

        var eventsField = result.Lifecycle.Events;
        if (eventsField.Classification == ResultFieldClassification.Unavailable)
        {
            violations.Add(new BoundaryViolation(
                BoundaryProhibitionKind.NoFsmControl,
                "lifecycle.events",
                "the projected event stream is unavailable; FSM-control freedom cannot be attested (fail-closed)."));
            return new BoundaryProhibitionResult(
                BoundaryProhibitionKind.NoFsmControl,
                Positive: false,
                evidence.ToImmutable(),
                violations.ToImmutable());
        }

        var events = eventsField.Value;
        if (events.IsDefaultOrEmpty)
        {
            // Zero received events — nothing to classify (vacuous, truthful).
            evidence.Add("zero events received; no event required classification");
            return new BoundaryProhibitionResult(
                BoundaryProhibitionKind.NoFsmControl,
                Positive: true,
                evidence.ToImmutable(),
                ImmutableArray<BoundaryViolation>.Empty);
        }

        foreach (var item in events)
        {
            object? kind = null;
            if (!Enum.TryParse<RuntimeEventKind>(item.Kind, ignoreCase: true, out var parsedKind))
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoFsmControl,
                    $"event '{item.EventId}' kind '{item.Kind}' (seq {item.Sequence})",
                    "received an event kind outside the audited 18-family vocabulary; unknown kinds have no runtime provenance."));
                continue;
            }

            kind = parsedKind;
            var table = RuntimeEventKindTable.For(parsedKind);
            if (table.Classification == RuntimeEventSourceClassification.RequiresNewRuntimeSemanticEmission)
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoFsmControl,
                    $"event '{item.EventId}' kind '{item.Kind}' (seq {item.Sequence})",
                    "C-class event kind was received; C-class kinds must NEVER be emitted on the read surface."));
            }
            else if (!table.EmittableInSlice)
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoFsmControl,
                    $"event '{item.EventId}' kind '{item.Kind}' (seq {item.Sequence})",
                    $"received a kind the audited table marks not emittable in this slice ({(string.IsNullOrWhiteSpace(table.NotEmittedReason) ? "no source on the public surface" : table.NotEmittedReason)})."));
            }

            if (kind is not null
                && !string.Equals(item.SourceClassification, table.Classification.ToString(), StringComparison.Ordinal))
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoFsmControl,
                    $"event '{item.EventId}' kind '{item.Kind}' (seq {item.Sequence})",
                    $"event source classification '{item.SourceClassification}' does not match the audited table classification '{table.Classification}' — provenance cannot be attributed."));
            }
            else if (kind is not null)
            {
                evidence.Add($"event '{item.Kind}' seq {item.Sequence}: A/B source classification '{item.SourceClassification}' matches the audited table");
            }
        }

        var positive = violations.Count == 0;
        if (positive)
        {
            evidence.Add($"all {events.Length} received events belong to the audited A/B vocabulary; C-class absent");
        }

        return new BoundaryProhibitionResult(
            BoundaryProhibitionKind.NoFsmControl,
            Positive: positive,
            evidence.ToImmutable(),
            violations.ToImmutable());
    }

    // ---- (d) every EvidenceRef resolves through evidence.get ─────────────────

    private static BoundaryProhibitionResult VerifyNoEvidenceFabrication(ValidationResult result)
    {
        var violations = ImmutableArray.CreateBuilder<BoundaryViolation>();
        var evidence = ImmutableArray.CreateBuilder<string>();

        var entriesField = result.Evidence.Entries;
        if (entriesField.Classification == ResultFieldClassification.Unavailable)
        {
            violations.Add(new BoundaryViolation(
                BoundaryProhibitionKind.NoEvidenceFabrication,
                "evidence.entries",
                "the evidence-resolution section is unavailable; provenance cannot be attested (fail-closed)."));
            return new BoundaryProhibitionResult(
                BoundaryProhibitionKind.NoEvidenceFabrication,
                Positive: false,
                evidence.ToImmutable(),
                violations.ToImmutable());
        }

        var entries = entriesField.Value;
        foreach (var entry in entries)
        {
            var locator = entry.RequestedRef.Locator;
            if (!entry.Resolved.Value)
            {
                violations.Add(new BoundaryViolation(
                    BoundaryProhibitionKind.NoEvidenceFabrication,
                    $"evidence ref '{locator}'",
                    entry.Diagnostic.Value ?? "evidence.get did not resolve the ref; the harness never invents a capture record."));
            }
            else
            {
                evidence.Add($"evidence.get('{locator}') resolved (found={entry.Resolved.Value})");
            }
        }

        if (entries.IsDefaultOrEmpty)
        {
            evidence.Add("zero evidence refs requested; nothing to fabricate (vacuous)");
        }

        var positive = violations.Count == 0;
        if (positive && !entries.IsDefaultOrEmpty)
        {
            evidence.Add($"all {entries.Length} evidence refs resolved through evidence.get; no fabricated capture records");
        }

        return new BoundaryProhibitionResult(
            BoundaryProhibitionKind.NoEvidenceFabrication,
            Positive: positive,
            evidence.ToImmutable(),
            violations.ToImmutable());
    }
}