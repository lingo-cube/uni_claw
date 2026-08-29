namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// Admission outcome of the provenance gate (spec requirements
/// "Provenance-gated admission" and "Forbidden knowledge sources are
/// rejected"; design D3 — admission requires provenance record → SourceRunId
/// → EvidenceRefs → observed result).
/// </summary>
public abstract record KnowledgeAdmission
{
    /// <summary>The candidate passed the gate; the admitted record is
    /// returned verbatim with its deterministic <see cref="ScenarioKnowledgeRecord.RecordId"/>.</summary>
    public sealed record Admitted(ScenarioKnowledgeRecord Record) : KnowledgeAdmission;

    /// <summary>
    /// The candidate was rejected. <see cref="ForbiddenSource"/> is the
    /// explicit forbidden-source marker when the rejection IS one of the
    /// forbidden source classes (spec "Forbidden knowledge sources are
    /// rejected"); otherwise null (e.g. missing provenance, incomplete scope,
    /// out-of-range confidence, undefined type/status).
    /// </summary>
    public sealed record Rejected(string Reason, KnowledgeAdmissionSource? ForbiddenSource) : KnowledgeAdmission;

    /// <summary>
    /// Stateless admission gate (closed contract): accepts ONLY candidates
    /// that trace to an observed result and satisfy every provenance,
    /// vocabulary, and scope rule:
    /// <list type="bullet">
    /// <item>provenance: non-empty SourceRunId AND ≥1 non-empty EvidenceRef;</item>
    /// <item>source: exactly <see cref="KnowledgeAdmissionSource.ObservedResult"/> (each
    /// forbidden source class is rejected with its explicit marker);</item>
    /// <item>anchor: non-empty typed semantic anchor (not coordinates/paths/selectors);</item>
    /// <item>confidence: within [0.0, 1.0] (NaN rejected);</item>
    /// <item>scope: complete — scenario/app/capability-id/capability-version/
    /// android-assumptions/locale/created-from-run-set all present;</item>
    /// <item>type/status: within the closed graduated vocabularies; version ≥ 1.</item>
    /// </list>
    /// Store-level freshness rules (duplicate canonical content, scope binding)
    /// are enforced by <see cref="ScenarioKnowledgeFixture.Admit"/>, which
    /// re-runs this gate.
    /// </summary>
    public static KnowledgeAdmission TryAdmit(ScenarioKnowledgeRecord candidate, KnowledgeAdmissionSource source)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // Closed vocabularies (spec: "no new runtime semantics"; forbidden:
        // no eighth KnowledgeType / new semantic word).
        if (!Enum.IsDefined(candidate.KnowledgeType))
        {
            return new Rejected(
                $"KnowledgeType '{candidate.KnowledgeType}' is not a graduated-vocabulary type.",
                null);
        }

        if (!Enum.IsDefined(candidate.Status))
        {
            return new Rejected(
                $"KnowledgeStatus '{candidate.Status}' is not a defined lifecycle status.",
                null);
        }

        // Provenance chain: record → SourceRunId → ≥1 EvidenceRef (spec:
        // records lacking provenance are rejected).
        if (string.IsNullOrWhiteSpace(candidate.SourceRunId))
        {
            return new Rejected(
                "SourceRunId is required — knowledge must trace to an observed run.",
                null);
        }

        if (candidate.EvidenceRefs is null || candidate.EvidenceRefs.Count == 0)
        {
            return new Rejected(
                "At least one EvidenceRef is required — knowledge must trace to observed evidence.",
                null);
        }

        if (candidate.EvidenceRefs.Any(string.IsNullOrWhiteSpace))
        {
            return new Rejected(
                "Every EvidenceRef must be non-empty.",
                null);
        }

        // Source gate: only ObservedResult; forbidden classes carry their
        // explicit marker into the rejection (explicit and testable).
        if (source != KnowledgeAdmissionSource.ObservedResult)
        {
            return new Rejected(
                $"Source '{source}' is forbidden — only ObservedResult is admissible "
                + "(spec: forbidden knowledge sources are rejected).",
                source);
        }

        // Anchor: a typed semantic anchor id, NOT coordinates/fixed paths/
        // selector scripts (spec + design D3).
        if (string.IsNullOrWhiteSpace(candidate.SemanticAnchor))
        {
            return new Rejected(
                "SemanticAnchor is required — a typed semantic anchor id, not coordinates, selectors, or fixed paths.",
                null);
        }

        // Confidence within [0.0, 1.0].
        if (double.IsNaN(candidate.Confidence) || candidate.Confidence < 0.0 || candidate.Confidence > 1.0)
        {
            return new Rejected(
                "Confidence must be within [0.0, 1.0].",
                null);
        }

        // Scope complete: scenario/app/capability-id/capability-version/
        // android-assumptions/locale/created-from-run-set (spec: implicit
        // global knowledge and automatic cross-context reuse are forbidden).
        if (!IsScopeComplete(candidate.Scope))
        {
            return new Rejected(
                "Scope is incomplete — scenario/app/capability-id/capability-version/android-assumptions/locale/created-from-run-set are all required.",
                null);
        }

        // Version ≥ 1 (spec record field contract).
        if (candidate.Version < 1)
        {
            return new Rejected(
                "Version must be >= 1.",
                null);
        }

        return new Admitted(candidate);
    }

    private static bool IsScopeComplete(KnowledgeScope? scope)
        => scope is not null
           && !string.IsNullOrWhiteSpace(scope.ScenarioId)
           && !string.IsNullOrWhiteSpace(scope.ApplicationPackage)
           && !string.IsNullOrWhiteSpace(scope.SemanticCapabilityId)
           && !string.IsNullOrWhiteSpace(scope.SemanticCapabilityVersion)
           && !string.IsNullOrWhiteSpace(scope.AndroidAssumptions)
           && !string.IsNullOrWhiteSpace(scope.Locale)
           && scope.CreatedFromRunIds is { Count: > 0 };
}