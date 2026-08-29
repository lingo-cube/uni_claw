using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Runtime.ValidationHarness.Knowledge;

/// <summary>
/// One knowledge record (spec requirement "ScenarioKnowledgeFixture as a
/// validation test asset" — full record field set; design D3). Immutable value
/// with a deterministic identity: <see cref="RecordId"/> is a SHA-256 over the
/// canonical CONTENT — knowledge type, semantic anchor, provenance
/// (SourceRunId + EvidenceRefs), observed role, full scope, disposition,
/// confidence, validity assumption, version. The lifecycle-only fields
/// (Status / Supersedes / SupersededBy) and the deterministic admission
/// ordinal are deliberately EXCLUDED from identity: a downgraded record keeps
/// its identity, so frozen fixtures stay diffable (spec "Human-readable
/// persisted asset"). There is NO DateTime anywhere — fixtures must be
/// deterministic; <see cref="AdmissionOrdinal"/> (campaign round / admission
/// sequence) replaces any wall-clock timestamp.
///
/// TEST_KNOWLEDGE != RUNTIME_TRUTH; TEST_KNOWLEDGE != ACTION_AUTHORITY;
/// TEST_KNOWLEDGE != FORMAL_MEMORY — this record is a validation asset only.
/// </summary>
/// <param name="KnowledgeType">Graduated observation class (one of the seven).</param>
/// <param name="SemanticAnchor">Typed semantic anchor id (capability kind + stable anchor text OBSERVED, e.g. "settings.container:Network & internet"); never coordinates/paths/selectors.</param>
/// <param name="SourceRunId">Run id whose observed result produced this knowledge (provenance head).</param>
/// <param name="EvidenceRefs">Evidence refs of that run backing this knowledge (≥1 required by the admission gate).</param>
/// <param name="ObservedRole">Role the anchor was observed in (e.g. "container observed", "boundary observed").</param>
/// <param name="Scope">Explicit reuse scope (scenario/app/capability/version/android/locale/run-set).</param>
/// <param name="Disposition">Free-text rationale (e.g. "record-only observed", "boundary observed").</param>
/// <param name="Confidence">Observed confidence in [0.0, 1.0].</param>
/// <param name="ValidityAssumption">Assumption under which this knowledge is believed valid (e.g. "stable across frames").</param>
/// <param name="Version">Knowledge content version (≥1; a refreshed observation of the same anchor is a higher version → new identity).</param>
/// <param name="Status">Validation-asset lifecycle status (Active until fresh evidence downgrades it).</param>
/// <param name="AdmissionOrdinal">Deterministic creation ordinal (campaign round / admission sequence) replacing a wall-clock timestamp — excluded from RecordId.</param>
/// <param name="Supersedes">RecordId of the record THIS record replaced (traceable pair with <see cref="SupersededBy"/>).</param>
/// <param name="SupersededBy">RecordId of the record that replaced THIS one (set only by fresh-evidence transitions).</param>
public sealed record ScenarioKnowledgeRecord(
    KnowledgeType KnowledgeType,
    string SemanticAnchor,
    string SourceRunId,
    IReadOnlyList<string> EvidenceRefs,
    string ObservedRole,
    KnowledgeScope Scope,
    string Disposition,
    double Confidence,
    string ValidityAssumption,
    int Version,
    KnowledgeStatus Status,
    int AdmissionOrdinal,
    string? Supersedes = null,
    string? SupersededBy = null)
{
    /// <summary>
    /// Deterministic identity: SHA-256 (lowercase hex) over the length-prefixed
    /// canonical content fields (lifecycle-only Status / Supersedes /
    /// SupersededBy and the admission ordinal excluded). Same content ⇒ same
    /// RecordId — freezing is diffable and re-admission of identical old
    /// knowledge is detectable as a duplicate.
    /// </summary>
    public string RecordId { get; } = ComputeRecordId(
        KnowledgeType,
        SemanticAnchor,
        SourceRunId,
        EvidenceRefs,
        ObservedRole,
        Scope,
        Disposition,
        Confidence,
        ValidityAssumption,
        Version);

    /// <summary>
    /// Immutable lifecycle transition: returns a NEW record with the same
    /// identity (<see cref="RecordId"/> unchanged) carrying the given status
    /// and — when given — the <see cref="SupersededBy"/> link. The original
    /// record is never mutated. There is NO inverse transition: no helper
    /// re-activates a downgraded record.
    /// </summary>
    public ScenarioKnowledgeRecord WithStatus(KnowledgeStatus status, string? supersededBy = null)
        => this with { Status = status, SupersededBy = supersededBy };

    private static string ComputeRecordId(
        KnowledgeType type,
        string? semanticAnchor,
        string? sourceRunId,
        IReadOnlyList<string>? evidenceRefs,
        string? observedRole,
        KnowledgeScope? scope,
        string? disposition,
        double confidence,
        string? validityAssumption,
        int version)
    {
        var canonical = string.Concat(
            Part(type.ToString()),
            Part(semanticAnchor ?? string.Empty),
            Part(sourceRunId ?? string.Empty),
            Part(JoinSorted(evidenceRefs)),
            Part(observedRole ?? string.Empty),
            Part(scope?.ScenarioId ?? string.Empty),
            Part(scope?.ApplicationPackage ?? string.Empty),
            Part(scope?.SemanticCapabilityId ?? string.Empty),
            Part(scope?.SemanticCapabilityVersion ?? string.Empty),
            Part(scope?.AndroidAssumptions ?? string.Empty),
            Part(scope?.Locale ?? string.Empty),
            Part(JoinSorted(scope?.CreatedFromRunIds)),
            Part(disposition ?? string.Empty),
            Part(confidence.ToString("R", CultureInfo.InvariantCulture)),
            Part(validityAssumption ?? string.Empty),
            Part(version.ToString(CultureInfo.InvariantCulture)));

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(digest);
    }

    private static string JoinSorted(IReadOnlyList<string>? values)
        => values is null
            ? string.Empty
            : string.Join(",", values.Order(StringComparer.Ordinal));

    private static string Part(string value)
        => value is null ? "0:" : value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
}