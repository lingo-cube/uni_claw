using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Reporting;

/// <summary>
/// One checkable gate outcome (design D7, task 6.3): pass/fail plus the
/// evidence references that support it. When the gate fails,
/// <see cref="OffendingEvidence"/> names the exact offending evidence — never a
/// masked or weakened failure (spec "A gate failure is reported, not masked").
/// </summary>
public sealed record GateOutcome(
    bool Passed,
    ImmutableArray<string> EvidenceRefs,
    string? OffendingEvidence = null);

/// <summary>
/// The four approved gates as explicit report fields (design D7): G1
/// directive-legal (deterministic validation result), G2 end-to-end autonomy
/// (zero driver calls after admission + terminal through the existing path),
/// G3 result evidence-backed (field-walk classification invariant), G4
/// boundary clean (verifier pass).
/// </summary>
public sealed record ValidationGates(
    GateOutcome G1,
    GateOutcome G2,
    GateOutcome G3,
    GateOutcome G4)
{
    /// <summary>All four gates pass (the deterministic tier condition).</summary>
    public bool AllPass => G1.Passed && G2.Passed && G3.Passed && G4.Passed;
}

/// <summary>
/// Gate evaluator (task 6.3): evaluates G1–G4 as pure functions over the
/// aggregated result, the boundary verification, and the call log. A scenario
/// fails its gate when any outcome is not satisfied; the evaluator never
/// weakens a check, modifies Runtime, or invents a surface to make a gate pass.
/// </summary>
public static class ValidationGateEvaluator
{
    /// <summary>
    /// Evaluate all four gates. <paramref name="transportedDirectives"/> and
    /// <paramref name="validator"/> feed G1's deterministic re-validation
    /// (exactly the same payloads the <see cref="BoundaryVerifier"/> scans).
    /// </summary>
    public static ValidationGates Evaluate(
        ValidationResult result,
        BoundaryVerification boundary,
        EmulatorCallLog callLog,
        int expectedStartCount,
        IReadOnlyList<JsonObject>? transportedDirectives = null,
        StrategyDirectiveValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(callLog);
        var payloadScanner = validator ?? new StrategyDirectiveValidator();
        var relayed = transportedDirectives ?? [];

        return new ValidationGates(
            G1: EvaluateG1DirectiveLegal(callLog, expectedStartCount, relayed, payloadScanner),
            G2: EvaluateG2EndToEndAutonomy(result, callLog, expectedStartCount),
            G3: EvaluateG3ResultEvidenceBacked(result),
            G4: EvaluateG4BoundaryClean(boundary));
    }

    // ---- G1: the driver can produce a legal Directive (deterministic
    //    validation result) ───────────────────────────────────────────────────

    private static GateOutcome EvaluateG1DirectiveLegal(
        EmulatorCallLog callLog,
        int expectedStartCount,
        IReadOnlyList<JsonObject> transportedDirectives,
        StrategyDirectiveValidator validator)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        var accepts = 0;

        foreach (var entry in callLog.Entries)
        {
            switch (entry.Outcome)
            {
                case EmulatorCallOutcome.RejectedBeforeTransport:
                    return Fail(evidence, entry.Detail,
                        "the directive was refused before transport — no legal directive existed for the scenario.");
                case EmulatorCallOutcome.TransportFailed:
                    return Fail(evidence, entry.Detail,
                        "the directive transport failed; end-to-end delivery of a legal directive was not achieved.");
                case EmulatorCallOutcome.DirectiveRequired:
                    return Fail(evidence, entry.Detail,
                        "only goal prose was supplied; no directive was produced (no strategy inference).");
                case EmulatorCallOutcome.Accepted:
                    accepts++;
                    evidence.Add($"admission accept {accepts}: run {entry.Detail}");
                    break;
                case EmulatorCallOutcome.RejectedByAdmission:
                    return Fail(evidence, entry.Detail,
                        "the transported directive was rejected by admission; the directive was not legal for the runtime.");
            }
        }

        if (accepts != expectedStartCount)
        {
            return Fail(evidence,
                $"expected {expectedStartCount} run.strategy.start accept(s), call log records {accepts}",
                "the scenario's directive transport count does not match its bound.");
        }

        if (transportedDirectives.Count != accepts)
        {
            return Fail(evidence,
                $"payload scan coverage {transportedDirectives.Count}/{accepts} accepted transports",
                "directive legality cannot be proven without the exact transported payloads.");
        }

        for (var index = 0; index < transportedDirectives.Count; index++)
        {
            if (validator.Validate(transportedDirectives[index]) is DirectiveValidationResult.Rejected rejected)
            {
                return Fail(evidence,
                    $"transported directive payload {index}: {rejected.Reason}",
                    rejected.Category is not null
                        ? "a transported payload carried forbidden (injected) action/coordinate/path/locator/callback/prose content; only legal directive payloads may cross the wire."
                        : "a transported payload failed the closed Strategy vocabulary; the directive was not legal.");
            }

            evidence.Add($"payload {index} validated legal (closed vocabulary, zero forbidden content)");
        }

        evidence.Add("G1: every dispatched directive is deterministic-validated Legal and transported exactly the scenario bound");
        return new GateOutcome(Passed: true, evidence.ToImmutable(), OffendingEvidence: null);
    }

    // ---- G2: end-to-end autonomy (zero driver calls after admission +
    //    terminal through the existing path) ──────────────────────────────────

    private static GateOutcome EvaluateG2EndToEndAutonomy(
        ValidationResult result,
        EmulatorCallLog callLog,
        int expectedStartCount)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        var transportCount = 0;
        var lastAcceptedIndex = -1;

        for (var index = 0; index < callLog.Entries.Length; index++)
        {
            var entry = callLog.Entries[index];
            if (!string.Equals(entry.Method, EmulatorDriver.StartStrategyMethod, StringComparison.Ordinal))
            {
                return Fail(evidence,
                    $"call-log entry {index} (method '{entry.Method}')",
                    "a driver call outside run.strategy.start was recorded.");
            }

            if (entry.Outcome == EmulatorCallOutcome.Accepted)
            {
                transportCount++;
                lastAcceptedIndex = index;
            }
            else if (entry.Outcome is EmulatorCallOutcome.RejectedByAdmission or EmulatorCallOutcome.TransportFailed)
            {
                transportCount++;
            }
        }

        if (transportCount != expectedStartCount)
        {
            return Fail(evidence,
                $"start count {transportCount} ≠ scenario bound {expectedStartCount}",
                "end-to-end autonomy requires exactly the scenario's bounded starts.");
        }

        if (lastAcceptedIndex >= 0 && lastAcceptedIndex != callLog.Entries.Length - 1)
        {
            var stray = callLog.Entries[lastAcceptedIndex + 1];
            return Fail(evidence,
                $"driver activity after admission: call-log entry {lastAcceptedIndex + 1} ({stray.Outcome})",
                "zero driver calls after admission is required; the run must proceed without emulator intervention.");
        }

        evidence.Add(lastAcceptedIndex >= 0
            ? $"driver calls after admission: {callLog.Entries.Length - 1 - lastAcceptedIndex} (zero required)"
            : "no accepted admission recorded");

        // Terminal through the existing path: the run reached a terminal state
        // via the runtime's own lifecycle end (B-class terminal event) — never
        // via any harness control.
        var terminalState = result.Terminal.TerminalState.Value;
        var terminalStateText = result.Terminal.TerminalState.Classification == ResultFieldClassification.Unavailable
            ? "unavailable"
            : terminalState.ToString();
        if (terminalState is not (RunState.Completed or RunState.Failed))
        {
            return Fail(evidence,
                $"terminal runState: {terminalStateText}",
                "the run did not reach a terminal state through the existing runtime path.");
        }

        var events = result.Lifecycle.Events.Classification == ResultFieldClassification.Unavailable
            ? Array.Empty<string>()
            : result.Lifecycle.Events.Value
                .Where(e => e.Kind is "RunCompleted" or "RunFailed")
                .Select(e => e.Kind)
                .ToArray();
        if (events.Length == 0)
        {
            return Fail(evidence,
                "no RunCompleted/RunFailed event on the projected stream",
                "terminal must be evidenced by the runtime's B-class terminal event, not by the harness.");
        }

        evidence.Add("terminal event on the projected stream: " + string.Join(", ", events));
        evidence.Add($"terminal state {terminalState} reached through the existing runtime path");
        return new GateOutcome(Passed: true, evidence.ToImmutable(), OffendingEvidence: null);
    }

    // ---- G3: every Result field is Runtime-Evidence-backed (field-walk
    //    classification invariant) ────────────────────────────────────────────

    private static GateOutcome EvaluateG3ResultEvidenceBacked(ValidationResult result)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        foreach (var (name, field) in EnumerateNamedFields(result))
        {
            if (string.IsNullOrWhiteSpace(field.TruthSource))
            {
                return Fail(evidence,
                    $"{name} (no truth-source statement)",
                    "every Result field must carry a truth-source statement.");
            }

            if (field.Classification == ResultFieldClassification.Unavailable)
            {
                // A value-type Unavailable field holds default(T) — the explicit
                // "absent" encoding (a value type cannot be null), never a real
                // fact; only a non-default reference value would violate the
                // populated ⇒ classified invariant.
                var raw = field.RawValue;
                var appearsPopulated = raw is not null && !raw.GetType().IsValueType;
                if (appearsPopulated)
                {
                    return Fail(evidence,
                        $"{name} (classified Unavailable with a populated value)",
                        "a populated Result field must be classified DirectProjection or DerivedReadModel — unavailable must be value-less, never guessed.");
                }
            }
            else
            {
                evidence.Add($"{name}: {field.Classification}, source: {field.TruthSource}");
            }
        }

        evidence.Add("G3: every populated Result field carries a classified, stated truth source");
        return new GateOutcome(Passed: true, evidence.ToImmutable(), OffendingEvidence: null);
    }

    // ---- G4: boundary clean (verifier pass) ──────────────────────────────────

    private static GateOutcome EvaluateG4BoundaryClean(BoundaryVerification boundary)
    {
        var evidence = boundary.Prohibitions.Select(p =>
                $"{p.Prohibition}: {(p.Positive ? "positive bound evidence" : "VIOLATION")}")
            .ToImmutableArray();
        if (boundary.Passed)
        {
            return new GateOutcome(Passed: true, evidence, OffendingEvidence: null);
        }

        var first = boundary.Violations.FirstOrDefault();
        return new GateOutcome(
            Passed: false,
            evidence,
            first is null
                ? "boundary verification failed without a recorded violation"
                : $"[{first.Prohibition}] {first.OffendingRecord} — {first.Reason}");
    }

    // ---- helpers ─────────────────────────────────────────────────────────────

    private static GateOutcome Fail(
        ImmutableArray<string>.Builder evidence,
        string? offendingEvidence,
        string reason)
        => new(
            Passed: false,
            evidence.ToImmutable(),
            OffendingEvidence: $"{offendingEvidence ?? "no offending evidence recorded"} — {reason}");

    /// <summary>All classified fields with fixed names (mirrors
    /// <see cref="ValidationResult.EnumerateClassifiedFields"/> ordering so the
    /// walk and the names agree).</summary>
    private static IEnumerable<(string Name, IClassifiedField Field)> EnumerateNamedFields(ValidationResult result)
    {
        yield return ("admission.runId", result.Admission.RunId);
        yield return ("admission.strategyId", result.Admission.StrategyId);
        yield return ("admission.accepted", result.Admission.Accepted);
        yield return ("admission.rejectionCode", result.Admission.RejectionCode);
        yield return ("admission.rejectionReason", result.Admission.RejectionReason);
        yield return ("admission.declaredMaximumDepth", result.Admission.DeclaredMaximumDepth);
        yield return ("lifecycle.events", result.Lifecycle.Events);
        yield return ("snapshot.runId", result.Snapshot.RunId);
        yield return ("snapshot.runState", result.Snapshot.RunState);
        yield return ("snapshot.currentSemanticPage", result.Snapshot.CurrentSemanticPage);
        yield return ("snapshot.activeTrap", result.Snapshot.ActiveTrap);
        yield return ("snapshot.currentGoal", result.Snapshot.CurrentGoal);
        yield return ("snapshot.lastDecision", result.Snapshot.LastDecision);
        yield return ("snapshot.lastAction", result.Snapshot.LastAction);
        yield return ("snapshot.recoveryState", result.Snapshot.RecoveryState);
        yield return ("snapshot.latestGoalEvidence", result.Snapshot.LatestGoalEvidence);
        yield return ("snapshot.currentObservationSequence", result.Snapshot.CurrentObservationSequence);
        yield return ("snapshot.currentContainerSummary", result.Snapshot.CurrentContainerSummary);
        yield return ("snapshot.bindingsSummary", result.Snapshot.BindingsSummary);
        yield return ("snapshot.stateBeliefsSummary", result.Snapshot.StateBeliefsSummary);
        yield return ("snapshot.diagnostics", result.Snapshot.Diagnostics);
        yield return ("trap.found", result.Trap.Found);
        yield return ("trap.trap", result.Trap.Trap);
        yield return ("trap.diagnostic", result.Trap.Diagnostic);
        yield return ("evidence.entries", result.Evidence.Entries);
        if (result.Evidence.Entries.Value is { } evidenceEntries)
        {
            var index = 0;
            foreach (var entry in evidenceEntries)
            {
                yield return ($"evidence.entries[{index}].resolved", entry.Resolved);
                yield return ($"evidence.entries[{index}].canonicalRef", entry.CanonicalRef);
                yield return ($"evidence.entries[{index}].diagnostic", entry.Diagnostic);
                index++;
            }
        }

        yield return ("coverage.availability", result.Coverage.Availability);
        yield return ("coverage.ledger", result.Coverage.Ledger);
        yield return ("coverage.scopes", result.Coverage.Scopes);
        yield return ("coverage.ledgerDigest", result.Coverage.LedgerDigest);
        yield return ("terminal.terminalState", result.Terminal.TerminalState);
        yield return ("terminal.terminalReason", result.Terminal.TerminalReason);
        yield return ("terminal.goalEvidenceBacksCompletion", result.Terminal.GoalEvidenceBacksCompletion);
    }
}