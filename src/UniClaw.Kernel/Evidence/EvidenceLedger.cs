using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Kernel.Evidence;

/// <summary>
/// Evidence Ledger — sole Evidence Admission Authority（Target §11，不变量 9）。
/// 只判断 record integrity / provenance completeness / source identity /
/// capture time / declared scope / transformation lineage / canonicalization。
/// 不判断 truth、belief relevance、reconciliation weight、proof sufficiency（不变量 10）。
/// </summary>
public sealed class EvidenceLedger
{
    private readonly List<AdmissionRecord> _admissionLog = new();
    private readonly Dictionary<string, EvidenceRecord> _canonicalRecords = new();
    private readonly ReadOnlyCollection<AdmissionRecord> _admissionLogView;
    private readonly ReadOnlyDictionary<string, EvidenceRecord> _canonicalRecordsView;

    /// <summary>构造 ledger；对外视图只读封装。</summary>
    public EvidenceLedger()
    {
        _admissionLogView = _admissionLog.AsReadOnly();
        _canonicalRecordsView = new ReadOnlyDictionary<string, EvidenceRecord>(_canonicalRecords);
    }

    /// <summary>每次 admission 尝试的留痕（append-oriented，不删改）。</summary>
    public IReadOnlyList<AdmissionRecord> AdmissionLog => _admissionLogView;

    /// <summary>canonical record 存储（按 EvidenceId 索引；内容去重）。</summary>
    public IReadOnlyDictionary<string, EvidenceRecord> CanonicalRecords => _canonicalRecordsView;

    /// <summary>
    /// Admission path：输入 → 检查 → (accepted: canonical record) | (rejected: 零副作用)。
    /// rejected 时不创建 canonical record、不影响既有内容。
    /// </summary>
    public (AdmissionRecord Admission, EvidenceRecord? Record) Admit(ObservationRecord observation)
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
            // canonicalization：完整性成立时 EvidenceId 可确定性导出
            new("canonicalization", integrityPassed && provenance is not null),
        };

        AdmissionRecord admission;
        if (checks.All(c => c.Passed))
        {
            var id = ComputeEvidenceId(observation);
            if (!_canonicalRecords.TryGetValue(id, out var record))
            {
                record = new EvidenceRecord(id, observation.Claim!, observation.Provenance!);
                _canonicalRecords[id] = record;
            }
            admission = new AdmissionRecord(AdmissionDecision.Accepted, checks, id, RejectionReason: null);
            _admissionLog.Add(admission);
            return (admission, record);
        }

        admission = new AdmissionRecord(
            AdmissionDecision.Rejected, checks, EvidenceId: null,
            RejectionReason: checks.First(c => !c.Passed).Name);
        _admissionLog.Add(admission);
        return (admission, Record: null);
    }

    /// <summary>确定性内容哈希：相同 claim + provenance → 相同 EvidenceId。</summary>
    internal static string ComputeEvidenceId(ObservationRecord observation)
    {
        var claim = observation.Claim!;
        var provenance = observation.Provenance!;
        var canonical = string.Join('\x1F',
            claim.Subject,
            claim.Value,
            provenance.Producer,
            provenance.CaptureTime.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            provenance.Scope,
            string.Join('\x1E', provenance.TransformationLineage));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return "ev-" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
