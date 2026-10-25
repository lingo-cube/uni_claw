using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Evidence Ledger — sole Evidence Admission Authority（Target §11，不变量 9）。
/// 只判断 record integrity / provenance completeness / source identity /
/// capture time / declared scope / transformation lineage / canonicalization。
/// 不判断 truth、belief relevance、reconciliation weight、proof sufficiency（不变量 10）。
/// </summary>
public sealed partial class EvidenceLedger
{
    private object? _attemptIssuer;
    internal void AttachAttemptIssuer(object issuer)
    {
        if (_attemptIssuer is not null && !ReferenceEquals(_attemptIssuer, issuer))
            throw new InvalidOperationException("evidence-attempt-issuer-already-bound");
        _attemptIssuer = issuer;
    }
    private bool ValidLink(ObservationProposal p) => p.AttemptCorrelation is not { } link
        || ReferenceEquals(link.Issuer, _attemptIssuer) && link.IsOpen && link.Kind == p.Kind && link.Attempt.IsValid;

    private readonly List<AdmissionRecord> _admissionLog = new();
    private readonly Dictionary<string, EvidenceRecord> _canonicalRecords = new();
    private readonly ReadOnlyCollection<AdmissionRecord> _admissionLogView;
    private readonly ReadOnlyDictionary<string, EvidenceRecord> _canonicalRecordsView;

    /// <summary>构造 ledger；对外视图只读封装。</summary>
    public EvidenceLedger() : this(null) { }

    public EvidenceLedger(IEvidenceJournal? journal)
    {
        _admissionLogView = _admissionLog.AsReadOnly();
        _canonicalRecordsView = new ReadOnlyDictionary<string, EvidenceRecord>(_canonicalRecords);
        if (journal is not null && (!journal.CanInitialize || journal.ReadRecords().Count != 0))
            throw new InvalidOperationException("evidence-history-recovery-read-only");
        _journal = journal;
    }

    /// <summary>每次 admission 尝试的留痕（append-oriented，不删改）。</summary>
    public IReadOnlyList<AdmissionRecord> AdmissionLog => _admissionLogView;

    /// <summary>canonical record 存储（按 EvidenceId 索引；内容去重）。</summary>
    public IReadOnlyDictionary<string, EvidenceRecord> CanonicalRecords => _canonicalRecordsView;

    /// <summary>
    /// Admission path：输入 → 检查 → (accepted: canonical record) | (rejected: 零副作用)。
    /// rejected 时不创建 canonical record、不影响既有内容。
    /// </summary>
    public (AdmissionRecord Admission, EvidenceRecord? Record) Admit(ObservationProposal observation)
    {
        EnterAdmission();
        try
        {
            var result = PrepareAdmission(observation, _canonicalRecords);
            CommitAdmission(result.Admission, result.Record);
            return result;
        }
        finally { Volatile.Write(ref _admissionBusy, 0); }
    }

    private (AdmissionRecord Admission, EvidenceRecord? Record) PrepareAdmission(ObservationProposal observation,
        IReadOnlyDictionary<string, EvidenceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var provenance = observation.Provenance;
        var integrityPassed = observation.Claim is not null
            && !string.IsNullOrWhiteSpace(observation.Claim.Subject)
            && !string.IsNullOrWhiteSpace(observation.Claim.Value);

        var checks = new List<AdmissionCheck>
        {
            new("record-integrity", integrityPassed),
            new("provenance-present", provenance is not null),
            new("source-identity", provenance is not null && !string.IsNullOrWhiteSpace(provenance.Producer)),
            new("capture-time", provenance is not null && provenance.CaptureTime != default),
            new("declared-scope", provenance is not null && !string.IsNullOrWhiteSpace(provenance.Scope)),
            new("transformation-lineage", provenance is not null
                && provenance.TransformationLineage is { Count: > 0 }),
            // ING-006 D2：未识别的 kind 值 fail-closed（enum 可被 cast 出
            // 非法值；实现层只认已锁成员，新 kind 需协议 + 代码双改）。
            // context 同一 cast 攻击面，对称校验（Review F2）
            new("kind-recognized", Enum.IsDefined(typeof(IngressKind), observation.Kind)),
            new("context-recognized", Enum.IsDefined(typeof(ObservationContext), observation.Context)),
            // canonicalization：完整性成立时 EvidenceId 可确定性导出
            new("canonicalization", integrityPassed && provenance is not null),
        };

        var origin = observation.AttemptCorrelation is null ? ObservationOrigin.External : ObservationOrigin.PostActionSelf;
        var id = checks.All(c => c.Passed)
            ? ComputeEvidenceIdV2(observation, origin, observation.AttemptCorrelation?.Attempt) : null;
        var lineage = ValidateLineage(observation, id, records);
        checks.Add(new("lineage-valid", lineage.Valid));
        checks.Add(new("attempt-correlation-valid", ValidLink(observation)));

        AdmissionRecord admission;
        if (checks.All(c => c.Passed))
        {
            if (!TryFindRecord(records, id!, out var record))
            {
                // ADR-0048: a flow label and producer self-report do not establish an actual attempt.
                var derivedOrigin = origin;
                record = new EvidenceRecord(
                    id!, observation.Claim!, observation.Kind, observation.Context,
                    observation.Provenance! with
                    {
                        TransformationLineage = Array.AsReadOnly(provenance!.TransformationLineage.ToArray()),
                    }, derivedOrigin)
                {
                    IdentityVersion = 2, AttemptCorrelation = observation.AttemptCorrelation?.Attempt,
                    DerivedFromEvidenceIds = Array.AsReadOnly(observation.DerivedFromEvidenceIds.ToArray()),
                    TransitiveEvidenceBasis = observation.TransitiveEvidenceBasis.ToFrozenSet(StringComparer.Ordinal),
                    FusionRule = observation.FusionRule,
                    FusionVersion = observation.FusionVersion,
                };
            }
            admission = new AdmissionRecord(AdmissionDecision.Accepted, checks.AsReadOnly(), id, RejectionReason: null);
            return (admission, record);
        }

        admission = new AdmissionRecord(
            AdmissionDecision.Rejected, checks.AsReadOnly(), EvidenceId: null,
            RejectionReason: !lineage.Valid
                ? "MalformedLineage"
                : checks.First(c => !c.Passed).Name);
        return (admission, Record: null);
    }

    /// <summary>确定性内容哈希：相同 canonical semantic content（claim +
    /// kind + context + provenance）→ 相同 EvidenceId（ING-006 D5；拼法属
    /// realization）。PER-013 Slice C：provenance 携带 hierarchy descriptor 时
    /// 附加其 canonical render（metadata 参与 id；legacy path 恒 null、
    /// 不追加任何内容 → 既有 EvidenceId 字节不变）。
    /// </summary>
    internal static string ComputeLegacyEvidenceId(ObservationProposal observation)
    {
        var claim = observation.Claim!;
        var provenance = observation.Provenance!;
        var canonical = string.Join('\x1F',
            claim.Subject,
            claim.Value,
            observation.Kind.ToString(),
            observation.Context.ToString(),
            provenance.Producer,
            provenance.CaptureTime.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            provenance.Scope,
            string.Join('\x1E', provenance.TransformationLineage));
        if (provenance.Hierarchy is { } hierarchy)
        {
            canonical += '\x1F' + hierarchy.RenderCanonical();
        }

        if (observation.DerivedFromEvidenceIds is { Count: > 0 })
        {
            canonical += '\x1F' + string.Join('\x1E', observation.DerivedFromEvidenceIds
                .OrderBy(id => id, StringComparer.Ordinal));
            canonical += '\x1F' + string.Join('\x1E', observation.TransitiveEvidenceBasis
                .OrderBy(id => id, StringComparer.Ordinal));
            canonical += '\x1F' + (observation.FusionRule ?? "-");
            canonical += '\x1F' + (observation.FusionVersion ?? "-");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "ev-" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Current identity prediction for internal producers/tests; historical replay explicitly selects v1.
    internal static string ComputeEvidenceId(ObservationProposal proposal) => ComputeEvidenceIdV2(proposal,
        proposal.AttemptCorrelation is null ? ObservationOrigin.External : ObservationOrigin.PostActionSelf,
        proposal.AttemptCorrelation?.Attempt);

    internal static string ComputeEvidenceIdV2(ObservationProposal proposal, ObservationOrigin origin, AttemptReference? attempt)
    {
        // Structured tuple avoids delimiter collisions in untrusted reference strings.
        var canonical = System.Text.Json.JsonSerializer.Serialize(new object?[]
            { "uniclaw.evidence.identity.v2", ComputeLegacyEvidenceId(proposal), origin, attempt });
        return "ev-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record LineageValidation(bool Valid);

    private bool TryFindRecord(IReadOnlyDictionary<string, EvidenceRecord> overlay, string id, out EvidenceRecord? record) =>
        overlay.TryGetValue(id, out record) || !ReferenceEquals(overlay, _canonicalRecords) && _canonicalRecords.TryGetValue(id, out record);

    /// <summary>
    /// Validates typed derived lineage before a proposal can become canonical.
    /// Parent ids are resolved only against already-admitted records; lineage
    /// strings are deliberately not interpreted as graph edges.
    /// </summary>
    private LineageValidation ValidateLineage(ObservationProposal observation, string? originalId = null,
        IReadOnlyDictionary<string, EvidenceRecord>? records = null)
    {
        records ??= _canonicalRecords;
        var parents = observation.DerivedFromEvidenceIds ?? Array.Empty<string>();
        var declaredBasis = observation.TransitiveEvidenceBasis
            ?? new HashSet<string>(StringComparer.Ordinal);
        var hasDerivedMetadata = parents.Count > 0
            || declaredBasis.Count > 0
            || observation.FusionRule is not null
            || observation.FusionVersion is not null;

        if (!hasDerivedMetadata)
            return new(true);

        if (observation.Claim is null || observation.Provenance is null)
            return new(false);

        if (parents.Count == 0
            || parents.Any(string.IsNullOrWhiteSpace)
            || parents.Distinct(StringComparer.Ordinal).Count() != parents.Count
            || declaredBasis.Count == 0
            || string.IsNullOrWhiteSpace(observation.FusionRule)
            || string.IsNullOrWhiteSpace(observation.FusionVersion))
            return new(false);

        var candidateId = originalId ?? ComputeEvidenceId(observation);
        if (parents.Contains(candidateId, StringComparer.Ordinal)
            || declaredBasis.Contains(candidateId, StringComparer.Ordinal))
            return new(false);

        var leaves = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            if (!TryFindRecord(records, id, out var parent) || parent is null)
                return false; // missing parent
            if (!visiting.Add(id))
                return false; // ancestry cycle

            var parentIds = parent.DerivedFromEvidenceIds ?? Array.Empty<string>();
            if (parentIds.Count == 0)
            {
                leaves.Add(id);
                visiting.Remove(id);
                return true;
            }

            foreach (var parentId in parentIds)
            {
                if (parentId == candidateId || !Visit(parentId))
                    return false;
            }

            visiting.Remove(id);
            return true;
        }

        foreach (var parentId in parents)
        {
            if (!Visit(parentId))
                return new(false);
        }

        return new(leaves.SetEquals(declaredBasis));
    }
}
