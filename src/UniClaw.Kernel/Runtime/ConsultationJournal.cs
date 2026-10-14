namespace UniClaw.Kernel.Runtime;

/// <summary>P25 consultation 的四元组身份。DecisionId 是 correlation。</summary>
public sealed record ConsultationKey(
    string RunId,
    string ContractViewRef,
    string DecisionBoundaryId,
    string ConsultationCorrelationId);

public enum ConsultationJournalStatus
{
    Pending,
    Completed,
    Rejected,
}

/// <summary>持久化的 P25 结果；Rejected 也占用一次 semantic consultation。</summary>
public sealed record ConsultationJournalEntry(
    ConsultationKey Key,
    string RequestFingerprint,
    ConsultationJournalStatus Status,
    AgentDecision? Decision,
    string? RejectionReason,
    int ConsumedOrdinal);

public sealed class ConsultationJournalConflictException : InvalidOperationException
{
    public ConsultationJournalConflictException(string message) : base(message) { }
}

/// <summary>
/// P25 journal seam。Kernel owns key/decision/count semantics; Host supplies
/// durable storage. Implementations must make Reserve idempotent by key+fingerprint.
/// </summary>
public interface IConsultationJournal
{
    ConsultationJournalEntry? Find(ConsultationKey key);
    ConsultationJournalEntry Reserve(ConsultationKey key, string requestFingerprint);
    ConsultationJournalEntry Complete(ConsultationKey key, string requestFingerprint, AgentDecision decision);
    ConsultationJournalEntry Reject(ConsultationKey key, string requestFingerprint, string reason);
    int ConsumedCount(string runId, string contractViewRef);
    bool HasPending(string runId, string contractViewRef);
}

/// <summary>Kernel/test implementation; Host production path injects file storage.</summary>
public sealed class InMemoryConsultationJournal : IConsultationJournal
{
    private readonly Dictionary<ConsultationKey, ConsultationJournalEntry> _entries = new();
    private readonly object _sync = new();

    public ConsultationJournalEntry? Find(ConsultationKey key)
    {
        lock (_sync) return _entries.GetValueOrDefault(key);
    }

    public ConsultationJournalEntry Reserve(ConsultationKey key, string requestFingerprint)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                EnsureFingerprint(existing, requestFingerprint);
                return existing;
            }
            var entry = new ConsultationJournalEntry(
                key, requestFingerprint, ConsultationJournalStatus.Pending, null, null,
                ConsumedCountCore(key.RunId, key.ContractViewRef) + 1);
            _entries.Add(key, entry);
            return entry;
        }
    }

    public ConsultationJournalEntry Complete(ConsultationKey key, string requestFingerprint, AgentDecision decision) =>
        Finish(key, requestFingerprint, ConsultationJournalStatus.Completed, decision, null);

    public ConsultationJournalEntry Reject(ConsultationKey key, string requestFingerprint, string reason) =>
        Finish(key, requestFingerprint, ConsultationJournalStatus.Rejected, null, reason);

    public int ConsumedCount(string runId, string contractViewRef)
    {
        lock (_sync) return ConsumedCountCore(runId, contractViewRef);
    }

    public bool HasPending(string runId, string contractViewRef)
    {
        lock (_sync)
            return _entries.Values.Any(e => e.Key.RunId == runId
                && e.Key.ContractViewRef == contractViewRef
                && e.Status == ConsultationJournalStatus.Pending);
    }

    private ConsultationJournalEntry Finish(
        ConsultationKey key, string requestFingerprint, ConsultationJournalStatus status,
        AgentDecision? decision, string? reason)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out var existing))
                throw new KeyNotFoundException($"Consultation '{key.ConsultationCorrelationId}' is not reserved");
            EnsureFingerprint(existing, requestFingerprint);
            if (existing.Status != ConsultationJournalStatus.Pending)
                return existing;
            var updated = existing with { Status = status, Decision = decision, RejectionReason = reason };
            _entries[key] = updated;
            return updated;
        }
    }

    private int ConsumedCountCore(string runId, string contractViewRef) =>
        _entries.Values.Count(e => e.Key.RunId == runId
            && e.Key.ContractViewRef == contractViewRef
            && e.Status != ConsultationJournalStatus.Pending);

    private static void EnsureFingerprint(ConsultationJournalEntry existing, string fingerprint)
    {
        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new ConsultationJournalConflictException(
                $"consultation-payload-conflict:{existing.Key.ConsultationCorrelationId}");
    }
}
