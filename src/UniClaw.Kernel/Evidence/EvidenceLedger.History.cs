using System.Collections.Frozen;
using System.Text.Json;

namespace UniClaw.Kernel.Evidence;

/// <summary>Trusted original Evidence Owner storage. Append success commits one original admission attempt.</summary>
public interface IEvidenceJournal
{
    bool CanInitialize { get; }
    string RecordingId { get; }
    IReadOnlyList<EvidenceHistoryRecord> ReadRecords();
    void Append(EvidenceHistoryRecord record);
}

/// <summary>Ordered original admissions. Completion acknowledges all rows; cancellation or failure
/// after acceptance may leave original commits and must never be interpreted as a safe retry.</summary>
public interface IAsyncEvidenceJournal : IEvidenceJournal
{
    ValueTask AppendBatchAsync(IReadOnlyList<EvidenceHistoryRecord> records, CancellationToken cancellationToken = default);
}

public sealed record EvidenceHistoryPosition(string RecordingId, int Sequence);

/// <summary>Storage projection of a canonical record; collections are frozen by the Owner on restore.</summary>
public sealed record EvidenceRecordData(string EvidenceId, ObservationClaim Claim, IngressKind Kind,
    ObservationContext Context, Provenance Provenance, ObservationOrigin Origin,
    string[] DerivedFromEvidenceIds, string[] TransitiveEvidenceBasis, string? FusionRule, string? FusionVersion,
    int IdentityVersion = 1, AttemptReference? AttemptCorrelation = null);

public sealed record EvidenceHistoryRecord(int Sequence, AdmissionRecord Admission, EvidenceRecordData? Record, int AdmissionVersion = 1);

internal sealed class EvidencePersistenceException(string message, Exception? inner = null) : IOException(message, inner);

public sealed partial class EvidenceLedger
{
    private readonly IEvidenceJournal? _journal;
    private string? _restoredRecordingId;
    private string? _persistenceFailure;
    public bool IsReadOnly { get; private set; }
    internal string? ExecutionBlockReason => IsReadOnly ? "restored-evidence-is-read-only" : _persistenceFailure
        ?? (Volatile.Read(ref _admissionBusy) != 0 ? "evidence-admission-in-progress" : null);
    internal string? RecordingId => _journal?.RecordingId ?? _restoredRecordingId;
    public EvidenceHistoryPosition? HistoryPosition => RecordingId is { } id ? new(id, _admissionLog.Count) : null;

    internal void EnsureWritable()
    {
        if (IsReadOnly) throw new InvalidOperationException("restored-evidence-is-read-only");
        if (_persistenceFailure is not null) throw new EvidencePersistenceException(_persistenceFailure);
        if (Volatile.Read(ref _admissionBusy) != 0) throw new InvalidOperationException("evidence-admission-in-progress");
    }

    private static EvidenceRecordData Capture(EvidenceRecord r) => new(r.EvidenceId, r.Claim, r.Kind, r.Context,
        r.Provenance with { TransformationLineage = Array.AsReadOnly(r.Provenance.TransformationLineage.ToArray()) }, r.Origin,
        r.DerivedFromEvidenceIds.ToArray(), r.TransitiveEvidenceBasis.Order(StringComparer.Ordinal).ToArray(), r.FusionRule, r.FusionVersion, r.IdentityVersion, r.AttemptCorrelation);

    private static AdmissionRecord Freeze(AdmissionRecord a) => a with { Checks = Array.AsReadOnly(a.Checks.ToArray()) };

    private void CommitAdmission(AdmissionRecord admission, EvidenceRecord? record)
    {
        if (_journal is { } journal)
        {
            try { journal.Append(new(_admissionLog.Count + 1, Freeze(admission), record is null ? null : Capture(record), AdmissionVersion: 2)); }
            catch (Exception ex)
            {
                _persistenceFailure = "evidence-persistence-failed:" + ex.GetType().Name;
                throw new EvidencePersistenceException(_persistenceFailure, ex);
            }
        }
        if (record is not null) _canonicalRecords.TryAdd(record.EvidenceId, record);
        _admissionLog.Add(admission);
    }

    /// <summary>Restore original historical eligibility records only. No new Admit, current World or execution authority.</summary>
    public static EvidenceLedger RestoreReadOnly(IEvidenceJournal journal, string expectedRecordingId)
    {
        if (!Guid.TryParseExact(expectedRecordingId, "N", out _) || journal.RecordingId != expectedRecordingId)
            throw new InvalidDataException("evidence-recording-mismatch");
        var restored = new EvidenceLedger { IsReadOnly = true, _restoredRecordingId = expectedRecordingId };
        string[] names = ["record-integrity", "provenance-present", "source-identity", "capture-time", "declared-scope",
            "transformation-lineage", "kind-recognized", "context-recognized", "canonicalization", "lineage-valid"];
        foreach (var row in journal.ReadRecords())
        {
            var a = row.Admission;
            if (row.Sequence != restored._admissionLog.Count + 1 || a is null
                || row.AdmissionVersion is not (1 or 2)
                || !a.Checks.Select(c => c.Name).SequenceEqual(row.AdmissionVersion == 1 ? names : [.. names, "attempt-correlation-valid"])
                || !Enum.IsDefined(a.Decision))
                throw new InvalidDataException("evidence-admission-sequence-or-shape");
            if (a.Decision == AdmissionDecision.Rejected)
            {
                if (row.Record is not null || a.EvidenceId is not null || a.Checks.All(c => c.Passed)
                    || a.RejectionReason != (a.Checks.Single(c => c.Name == "lineage-valid").Passed ? a.Checks.First(c => !c.Passed).Name : "MalformedLineage"))
                    throw new InvalidDataException("evidence-rejected-shape");
            }
            else
            {
                var r = row.Record ?? throw new InvalidDataException("evidence-accepted-record-missing");
                if (!a.Checks.All(c => c.Passed) || a.RejectionReason is not null || a.EvidenceId != r.EvidenceId
                    || !Enum.IsDefined(r.Kind) || !Enum.IsDefined(r.Context) || !Enum.IsDefined(r.Origin)
                    || r.Claim is null || string.IsNullOrWhiteSpace(r.Claim.Subject) || string.IsNullOrWhiteSpace(r.Claim.Value)
                    || r.Provenance is null || string.IsNullOrWhiteSpace(r.Provenance.Producer) || r.Provenance.CaptureTime == default
                    || string.IsNullOrWhiteSpace(r.Provenance.Scope) || r.Provenance.TransformationLineage.Count == 0
                    || r.TransitiveEvidenceBasis.Distinct(StringComparer.Ordinal).Count() != r.TransitiveEvidenceBasis.Length)
                    throw new InvalidDataException("evidence-canonical-shape");
                var proposal = new ObservationProposal(r.Claim, r.Kind, r.Context, r.Provenance)
                {
                    DerivedFromEvidenceIds = r.DerivedFromEvidenceIds,
                    TransitiveEvidenceBasis = r.TransitiveEvidenceBasis.ToFrozenSet(StringComparer.Ordinal),
                    FusionRule = r.FusionRule, FusionVersion = r.FusionVersion,
                };
                if (r.IdentityVersion != row.AdmissionVersion
                    || r.IdentityVersion == 1 && r.AttemptCorrelation is not null
                    || r.IdentityVersion == 2 && ((r.Origin == ObservationOrigin.PostActionSelf) != (r.AttemptCorrelation is not null)
                        || r.AttemptCorrelation is { IsValid: false }))
                    throw new InvalidDataException("evidence-original-correlation-shape");
                var expectedId = r.IdentityVersion == 1 ? ComputeLegacyEvidenceId(proposal)
                    : ComputeEvidenceIdV2(proposal, r.Origin, r.AttemptCorrelation);
                if (expectedId != r.EvidenceId || !restored.ValidateLineage(proposal, expectedId).Valid)
                    throw new InvalidDataException("evidence-original-id-or-lineage");
                var record = new EvidenceRecord(r.EvidenceId, r.Claim, r.Kind, r.Context,
                    r.Provenance with { TransformationLineage = Array.AsReadOnly(r.Provenance.TransformationLineage.ToArray()) }, r.Origin)
                {
                    IdentityVersion = r.IdentityVersion, AttemptCorrelation = r.AttemptCorrelation,
                    DerivedFromEvidenceIds = Array.AsReadOnly(r.DerivedFromEvidenceIds.ToArray()),
                    TransitiveEvidenceBasis = r.TransitiveEvidenceBasis.ToFrozenSet(StringComparer.Ordinal),
                    FusionRule = r.FusionRule, FusionVersion = r.FusionVersion,
                };
                if (restored._canonicalRecords.TryGetValue(r.EvidenceId, out var prior)
                    && JsonSerializer.Serialize(Capture(prior)) != JsonSerializer.Serialize(Capture(record)))
                    throw new InvalidDataException("evidence-id-content-conflict");
                restored._canonicalRecords.TryAdd(r.EvidenceId, record);
            }
            restored._admissionLog.Add(Freeze(a));
        }
        return restored;
    }
}
