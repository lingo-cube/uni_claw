using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.ValidationHarness.Emulator;
using UniClaw.Runtime.ValidationHarness.Results;

namespace UniClaw.Runtime.ValidationHarness.Classification;

/// <summary>
/// One protocol failure classification (design D6; WI-EVH-006 7.1): the fixed
/// <see cref="FailureOwner"/>, the First Divergence Point derived from EXISTING
/// evidence (call-log entries, projected event kinds, snapshot diagnostics,
/// terminal reason), and the evidence references that support the label.
/// Construction enforces the anti-bare-failure guarantee: an instance without
/// an owner AND a non-blank First Divergence Point cannot exist, so a bare
/// "Runtime failed" conclusion is impossible by type design.
/// The classification is pure metadata: it never mutates gate outcomes.
/// </summary>
public sealed record ProtocolFailureClassification
{
    /// <summary>One of the eight fixed protocol owners.</summary>
    public FailureOwner Owner { get; }

    /// <summary>The earliest evidence-derived divergence point (never the
    /// final symptom alone).</summary>
    public string FirstDivergencePoint { get; }

    /// <summary>Evidence references supporting the label (call-log indices,
    /// event kinds, terminal reason, diagnostics).</summary>
    public ImmutableArray<string> EvidenceRefs { get; }

    /// <summary>True for a scenario failure; false ONLY for the
    /// BLOCKED_FOR_SPEC workflow-stop marker (S2), which is classification
    /// metadata and never a runtime failure.</summary>
    public bool IsFailure { get; }

    /// <summary>
    /// Create a classification. Throws when an owner is undefined or the First
    /// Divergence Point is blank — the type-level guarantee that no bare
    /// "Runtime failed" conclusion can be formed.
    /// </summary>
    public ProtocolFailureClassification(
        FailureOwner owner,
        string firstDivergencePoint,
        ImmutableArray<string> evidenceRefs = default,
        bool isFailure = true)
    {
        if (!Enum.IsDefined(owner))
        {
            throw new ArgumentOutOfRangeException(
                nameof(owner), owner,
                "a classification requires one of the eight fixed protocol owners (design D6).");
        }

        if (string.IsNullOrWhiteSpace(firstDivergencePoint))
        {
            throw new ArgumentException(
                "a classification requires a First Divergence Point; a bare 'Runtime failed' conclusion cannot be constructed.",
                nameof(firstDivergencePoint));
        }

        Owner = owner;
        FirstDivergencePoint = firstDivergencePoint;
        EvidenceRefs = evidenceRefs.IsDefault ? ImmutableArray<string>.Empty : evidenceRefs;
        IsFailure = isFailure;
    }

    /// <summary>BLOCKED_FOR_SPEC metadata (S2): Recovery owner, the stop reason
    /// as the divergence point, never a runtime failure (IsFailure = false).</summary>
    public static ProtocolFailureClassification BlockedForSpec(string stopReason, ImmutableArray<string> evidenceRefs = default)
        => new(FailureOwner.Recovery, stopReason, evidenceRefs, isFailure: false);
}

/// <summary>
/// Failure classifier (design D6; WI-EVH-006 7.1): labels a scenario's failure
/// with one protocol owner and its First Divergence Point, derived from EXISTING
/// evidence only — the immutable call log, projected event kinds, snapshot
/// diagnostics, coverage accounting, and the terminal reason. The classifier
/// never collects new evidence, never touches Runtime, and never alters gate
/// outcomes: it is an annotation layer over already-produced evidence. When no
/// failure evidence exists the scenario has no classification (null).
/// The protocol taxonomy is fixed: StrategyCompilation / Discovery / Grounding /
/// Authorization / Execution / Recovery / Environment / TestHarness.
/// </summary>
public sealed class ProtocolFailureClassifier
{
    /// <summary>The workflow-stop marker: a scenario stopped for human gate
    /// adjudication (S2) — classified as Recovery metadata, never a failure.</summary>
    public const string BlockedForSpecMarker = "BLOCKED_FOR_SPEC";

    /// <summary>Trap/recovery vocabulary in terminal text — structural recovery
    /// evidence (event kinds / snapshot fields) is checked before text.</summary>
    private static readonly string[] TrapRecoveryVocabulary =
        ["trap raised", "trapped", "recovery", "escalat"];

    /// <summary>Authorization vocabulary in terminal text / diagnostics.</summary>
    private static readonly string[] AuthorizationVocabulary =
        ["forbidden", "authoriz", "denied", "prohibited", "unauthorized", "permission"];

    /// <summary>Device-environment vocabulary in terminal text / diagnostics.</summary>
    private static readonly string[] EnvironmentVocabulary =
        ["unexpected", "popup", "external boundary", "unclassifiable", "unexpected navigation"];

    /// <summary>Grounding vocabulary in terminal text / diagnostics.</summary>
    private static readonly string[] GroundingVocabulary =
        ["belief", "stale", "grounding", "mismatch", "reconcile", "drift", "binding"];

    /// <summary>
    /// Classify a scenario from its aggregated result and immutable call log.
    /// Returns null when no failure evidence exists (a clean scenario has no
    /// failure classification). The BLOCKED_FOR_SPEC marker (S2) classifies as
    /// Recovery with the stop reason as the divergence point and IsFailure =
    /// false — metadata only, never a runtime failure, never a gate change.
    /// </summary>
    public ProtocolFailureClassification? Classify(ValidationResult result, EmulatorCallLog callLog)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(callLog);

        // A. BLOCKED_FOR_SPEC workflow stop (S2) — checked first: the scenario
        //    never executed, so no runtime failure exists.
        if (TryClassifyBlockedForSpec(result, callLog, out var blocked))
        {
            return blocked;
        }

        // B. Earliest divergence in the call log: a failed dispatch precedes
        //    any terminal evidence (transport happens before execution).
        for (var index = 0; index < callLog.Entries.Length; index++)
        {
            var entry = callLog.Entries[index];
            var methodEvidence = $"call-log[{index}] {entry.Method} — {entry.Outcome}";
            if (!string.Equals(entry.Method, EmulatorDriver.StartStrategyMethod, StringComparison.Ordinal))
            {
                return Classification(
                    FailureOwner.TestHarness,
                    $"a driver call outside {EmulatorDriver.StartStrategyMethod} was recorded: {entry.Method}",
                    [methodEvidence]);
            }

            switch (entry.Outcome)
            {
                case EmulatorCallOutcome.DirectiveRequired:
                    return Classification(
                        FailureOwner.TestHarness,
                        entry.Detail ?? "DIRECTIVE_REQUIRED",
                        [methodEvidence]);
                case EmulatorCallOutcome.RejectedBeforeTransport:
                    return Classification(
                        FailureOwner.StrategyCompilation,
                        entry.Detail ?? "directive rejected before transport",
                        [methodEvidence]);
                case EmulatorCallOutcome.RejectedByAdmission:
                    var code = ExtractAdmissionRejectionCode(result, entry.Detail);
                    return Classification(
                        FailureOwner.StrategyCompilation,
                        $"admission rejection code: {code}",
                        [methodEvidence, entry.Detail ?? "no detail"]);
                case EmulatorCallOutcome.TransportFailed:
                    return Classification(
                        FailureOwner.TestHarness,
                        entry.Detail ?? "transport failed",
                        [methodEvidence]);
            }
        }

        // C. Terminal divergence: the run was admitted and ran to RunFailed.
        var terminalAvailable = result.Terminal.TerminalState.Classification
            != ResultFieldClassification.Unavailable;
        if (!terminalAvailable || result.Terminal.TerminalState.Value != RunState.Failed)
        {
            // No failed dispatch and no failed terminal: the only remaining
            // failure-shaped evidence is an unresolvable evidence reference
            // (a runtime evidence-provenance gap, recorded never fabricated).
            var unresolvable = FirstUnresolvableEvidenceRef(result);
            if (unresolvable is ValidationEvidenceEntry entry)
            {
                return Classification(
                    FailureOwner.Execution,
                    $"unresolvable evidence reference '{entry.RequestedRef.Locator}': {entry.Diagnostic.Value ?? "no diagnostic"}",
                    ["evidence.get resolution failed"]);
            }

            return null;
        }

        return ClassifyTerminalFailure(result);
    }

    /// <summary>Recovery metadata for a BLOCKED_FOR_SPEC stop (S2): the stop
    /// reason is the First Divergence Point; never a runtime failure.</summary>
    private static bool TryClassifyBlockedForSpec(
        ValidationResult result,
        EmulatorCallLog callLog,
        out ProtocolFailureClassification? classification)
    {
        var evidence = ImmutableArray.CreateBuilder<string>();
        var texts = new List<string>();

        AddIfPresent(texts, result.Terminal.TerminalReason.Value, evidence, "terminal.reason");
        AddDiagnostics(texts, result.Snapshot.Diagnostics.Value, evidence, "snapshot.diagnostics");
        AddIfPresent(texts, result.Admission.RejectionReason.Value, evidence, "admission.rejectionReason");
        foreach (var entry in callLog.Entries)
        {
            AddIfPresent(texts, entry.Detail, evidence, "call-log detail");
        }

        foreach (var text in texts)
        {
            var markerIndex = text.IndexOf(BlockedForSpecMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                continue;
            }

            var stopReason = text[(markerIndex + BlockedForSpecMarker.Length)..].Trim();
            stopReason = stopReason.TrimStart(':', ' ', '\t');
            classification = ProtocolFailureClassification.BlockedForSpec(
                string.IsNullOrWhiteSpace(stopReason) ? BlockedForSpecMarker : stopReason,
                evidence.ToImmutable());
            return true;
        }

        classification = null;
        return false;
    }

    /// <summary>Owner cascade for a RunFailed terminal — structural recovery
    /// evidence first, then text vocabulary, with Execution as the default
    /// (RunFailed with its failure reason in the divergence point).</summary>
    private static ProtocolFailureClassification ClassifyTerminalFailure(ValidationResult result)
    {
        var reason = FirstTerminalFailureReason(result);
        var events = result.Lifecycle.Events.Value;
        var hasTerminalEvents = !events.IsDefault;

        // 1. Structural trap/recovery evidence (projected event kinds, snapshot
        //    trap/recovery fields) — the strongest terminal evidence.
        var hasTrapEvent = hasTerminalEvents && events.Any(e => e.Kind is "TrapRaised" or "RecoveryStarted");
        var hasTrapState = result.Snapshot.ActiveTrap.Value is not null
            || !string.IsNullOrWhiteSpace(result.Snapshot.RecoveryState.Value?.RecoveryId)
            || result.Trap.Found.Value == true;
        if (hasTrapEvent || hasTrapState || ContainsAny(reason, TrapRecoveryVocabulary)
            || DiagnosticsContainAny(result, TrapRecoveryVocabulary))
        {
            var evidence = ImmutableArray.CreateBuilder<string>();
            if (hasTrapEvent)
            {
                evidence.Add($"lifecycle: {string.Join(",", events.Where(e => e.Kind is "TrapRaised" or "RecoveryStarted").Select(e => e.Kind))}");
            }
            if (result.Snapshot.ActiveTrap.Value is { } trap)
            {
                evidence.Add($"snapshot.activeTrap: {trap.Kind}/{trap.Scope} {trap.Source}");
            }
            if (reason is not null)
            {
                evidence.Add($"terminal.reason: {reason}");
            }
            return Classification(
                FailureOwner.Recovery,
                reason ?? "trap/recovery path diverged before terminal",
                evidence.ToImmutable());
        }

        // 2. Authorization vocabulary.
        if (ContainsAny(reason, AuthorizationVocabulary) || DiagnosticsContainAny(result, AuthorizationVocabulary))
        {
            return Classification(
                FailureOwner.Authorization,
                reason ?? "authorization divergence in terminal text",
                TerminalEvidence(result, reason));
        }

        // 3. Device-environment vocabulary.
        if (ContainsAny(reason, EnvironmentVocabulary) || DiagnosticsContainAny(result, EnvironmentVocabulary))
        {
            return Classification(
                FailureOwner.Environment,
                reason ?? "environment divergence in terminal text",
                TerminalEvidence(result, reason));
        }

        // 4. Incomplete scope discovery at terminal (coverage accounting) —
        //    unresolved / unknown-frontier scope rows evidence a Discovery gap.
        if (reason is not null && reason.Contains("unresolved", StringComparison.OrdinalIgnoreCase)
            || HasUnresolvedCoverage(result))
        {
            return Classification(
                FailureOwner.Discovery,
                reason ?? "scope discovery incomplete at terminal (unresolved coverage)",
                TerminalEvidence(result, reason));
        }

        // 5. Grounding vocabulary.
        if (ContainsAny(reason, GroundingVocabulary) || DiagnosticsContainAny(result, GroundingVocabulary))
        {
            return Classification(
                FailureOwner.Grounding,
                reason ?? "grounding divergence in terminal text",
                TerminalEvidence(result, reason));
        }

        // 6. Default: the run failed through the existing path with its reason
        //    as the divergence point.
        return Classification(
            FailureOwner.Execution,
            reason ?? "terminal RunFailed without a recorded reason",
            TerminalEvidence(result, reason));
    }

    /// <summary>The first truthful failure reason: the terminal field, falling
    /// back to the projected RunFailed event reason.</summary>
    private static string? FirstTerminalFailureReason(ValidationResult result)
    {
        if (result.Terminal.TerminalReason is { Classification: not ResultFieldClassification.Unavailable, Value: { } reason }
            && !string.IsNullOrWhiteSpace(reason))
        {
            return reason;
        }

        var events = result.Lifecycle.Events.Value;
        if (!events.IsDefault)
        {
            foreach (var failureEvent in events.Where(e => e.Kind == "RunFailed"))
            {
                if (!string.IsNullOrWhiteSpace(failureEvent.Reason))
                {
                    return failureEvent.Reason;
                }
            }
        }

        return null;
    }

    /// <summary>Admission rejection code: the classified Admission field when
    /// truthful, otherwise parsed from the call-log detail (ADMISSION_REJECT(code)).</summary>
    private static string ExtractAdmissionRejectionCode(ValidationResult result, string? detail)
    {
        if (result.Admission.RejectionCode is { Classification: not ResultFieldClassification.Unavailable, Value: { } code }
            && !string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            const string prefix = "ADMISSION_REJECT(";
            var start = detail.IndexOf(prefix, StringComparison.Ordinal);
            var end = detail.IndexOf(')', start + prefix.Length);
            if (start >= 0 && end > start)
            {
                return detail[(start + prefix.Length)..end];
            }
        }

        return detail ?? "unknown";
    }

    private static ValidationEvidenceEntry? FirstUnresolvableEvidenceRef(ValidationResult result)
    {
        var entries = result.Evidence.Entries.Value;
        if (entries.IsDefault)
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (entry.Resolved.Value == false)
            {
                return entry;
            }
        }

        return null;
    }

    private static bool HasUnresolvedCoverage(ValidationResult result)
    {
        var scopes = result.Coverage.Scopes.Value;
        if (scopes.IsDefault)
        {
            return false;
        }

        foreach (var scope in scopes)
        {
            if (scope.Unresolved > 0 || scope.UnknownFrontier > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string? text, string[] vocabulary)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var token in vocabulary)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DiagnosticsContainAny(ValidationResult result, string[] vocabulary)
    {
        var diagnostics = result.Snapshot.Diagnostics.Value;
        if (diagnostics.IsDefault)
        {
            return false;
        }

        foreach (var diagnostic in diagnostics)
        {
            if (ContainsAny(diagnostic, vocabulary))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<string> TerminalEvidence(ValidationResult result, string? reason)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        if (reason is not null)
        {
            builder.Add($"terminal.reason: {reason}");
        }
        var diagnostics = result.Snapshot.Diagnostics.Value;
        if (!diagnostics.IsDefault)
        {
            foreach (var diagnostic in diagnostics.Take(1))
            {
                builder.Add($"snapshot.diagnostics: {diagnostic}");
            }
        }
        return builder.ToImmutable();
    }

    private static ProtocolFailureClassification Classification(
        FailureOwner owner,
        string firstDivergencePoint,
        ImmutableArray<string> evidenceRefs = default)
        => new(owner, firstDivergencePoint, evidenceRefs, isFailure: true);

    private static void AddIfPresent(
        List<string> texts,
        string? value,
        ImmutableArray<string>.Builder evidence,
        string evidenceLabel)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            texts.Add(value);
            evidence.Add($"{evidenceLabel}: {value}");
        }
    }

    private static void AddDiagnostics(
        List<string> texts,
        ImmutableArray<string> diagnostics,
        ImmutableArray<string>.Builder evidence,
        string evidenceLabel)
    {
        if (diagnostics.IsDefault)
        {
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            AddIfPresent(texts, diagnostic, evidence, evidenceLabel);
        }
    }
}