namespace UniClaw.Kernel.Evidence;

public sealed partial class EvidenceLedger
{
    private int _admissionBusy;

    private void EnterAdmission()
    {
        EnsureWritable();
        if (Interlocked.CompareExchange(ref _admissionBusy, 1, 0) != 0)
            throw new InvalidOperationException("evidence-admission-in-progress");
    }

    /// <summary>Prepare one observation's ordered admissions, save them, then publish. Earlier
    /// accepted rows in this candidate are available for later derived lineage, never to World
    /// before acknowledgement. Legacy journals retain explicit synchronous compatibility.</summary>
    public async ValueTask<IReadOnlyList<(AdmissionRecord Admission, EvidenceRecord? Record)>> AdmitBatchAsync(
        IReadOnlyList<ObservationProposal> observations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);
        cancellationToken.ThrowIfCancellationRequested();
        EnterAdmission();
        try
        {
            var input = observations.ToArray();
            // The serial gate keeps original records stable. Only this observation's
            // overlay is needed; copying all historical Evidence grows with every cycle.
            var candidate = new Dictionary<string, EvidenceRecord>(StringComparer.Ordinal);
            var results = new List<(AdmissionRecord Admission, EvidenceRecord? Record)>();
            var rows = new List<EvidenceHistoryRecord>();
            foreach (var proposal in input)
            {
                var result = PrepareAdmission(proposal, candidate);
                if (result.Record is { } record) candidate.TryAdd(record.EvidenceId, record);
                results.Add(result);
                rows.Add(new(_admissionLog.Count + rows.Count + 1, Freeze(result.Admission),
                    result.Record is null ? null : Capture(result.Record), AdmissionVersion: 2));
            }
            if (rows.Count > 0 && _journal is { } journal)
            {
                try
                {
                    if (journal is IAsyncEvidenceJournal asyncJournal)
                        await asyncJournal.AppendBatchAsync(rows.AsReadOnly(), cancellationToken).ConfigureAwait(false);
                    else foreach (var row in rows) journal.Append(row);
                }
                catch (Exception ex)
                {
                    _persistenceFailure = "evidence-persistence-failed:" + ex.GetType().Name;
                    throw new EvidencePersistenceException(_persistenceFailure, ex);
                }
            }
            foreach (var result in results)
            {
                if (result.Record is { } record) _canonicalRecords.TryAdd(record.EvidenceId, record);
                _admissionLog.Add(result.Admission);
            }
            return results.AsReadOnly();
        }
        finally { Volatile.Write(ref _admissionBusy, 0); }
    }
}
