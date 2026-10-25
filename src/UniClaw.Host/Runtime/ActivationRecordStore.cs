using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>当前 Run 激活的幂等身份。</summary>
public sealed record ActivationKey(string AcceptedContractViewRef, int ContractGeneration);

/// <summary>
/// P24 activation record。它先记录 reserved，再在真实 RunId 创建后 finalize；
/// launch/idempotency 只作为查询索引，不替代 activation key。
/// </summary>
public sealed record ActivationRecord(
    string ActivationKey,
    string AcceptedContractViewRef,
    int ContractGeneration,
    string TaskInstanceId,
    string ProductSessionId,
    string LaunchId,
    string IdempotencyKey,
    string CorrelationId,
    string Status,
    string? RunId,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

public sealed class ActivationRecordStore : IDisposable
{
    private readonly string _path;
    private readonly Dictionary<string, ActivationRecord> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActivationRecord> _byLaunch = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActivationRecord> _byIdempotency = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private bool _faulted;

    public ActivationRecordStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Activation record path is required", nameof(filePath));
        _path = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        Load();
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    public ActivationRecord? Find(ActivationKey key)
    {
        lock (_sync) return _byKey.GetValueOrDefault(CanonicalKey(key));
    }

    public ActivationRecord? FindByLaunchId(string launchId)
    {
        lock (_sync) return _byLaunch.GetValueOrDefault(launchId);
    }

    public ActivationRecord? FindByIdempotencyKey(string idempotencyKey)
    {
        lock (_sync) return _byIdempotency.GetValueOrDefault(idempotencyKey);
    }

    public Reservation Reserve(
        ActivationKey key,
        string taskInstanceId,
        string productSessionId,
        string launchId,
        string idempotencyKey,
        string correlationId,
        DateTimeOffset? now = null)
    {
        lock (_sync)
        {
            EnsureWritable();
            ArgumentNullException.ThrowIfNull(key);
            var activationKey = CanonicalKey(key);
            if (string.IsNullOrWhiteSpace(taskInstanceId)
                || string.IsNullOrWhiteSpace(productSessionId)
                || string.IsNullOrWhiteSpace(launchId)
                || string.IsNullOrWhiteSpace(idempotencyKey)
                || string.IsNullOrWhiteSpace(correlationId))
                throw new ArgumentException("Activation binding fields are required");

            if (_byKey.TryGetValue(activationKey, out var existing))
                return new Reservation(existing, Conflict: false, Created: false);

            if (_byLaunch.TryGetValue(launchId, out var launchExisting)
                && !string.Equals(launchExisting.ActivationKey, activationKey, StringComparison.Ordinal))
                return new Reservation(launchExisting, Conflict: true, Created: false);
            if (_byIdempotency.TryGetValue(idempotencyKey, out var idemExisting)
                && !string.Equals(idemExisting.ActivationKey, activationKey, StringComparison.Ordinal))
                return new Reservation(idemExisting, Conflict: true, Created: false);

            var timestamp = now ?? DateTimeOffset.UtcNow;
            var record = new ActivationRecord(
                activationKey,
                key.AcceptedContractViewRef,
                key.ContractGeneration,
                taskInstanceId,
                productSessionId,
                launchId,
                idempotencyKey,
                correlationId,
                Status: "reserved",
                RunId: null,
                Reason: null,
                CreatedAt: timestamp,
                UpdatedAt: timestamp,
                Revision: 1);
            Store(record);
            return new Reservation(record, Conflict: false, Created: true);
        }
    }

    public ActivationRecord Finalize(ActivationKey key, string runId, DateTimeOffset? now = null)
    {
        lock (_sync)
        {
            EnsureWritable();
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("RunId is required", nameof(runId));
            var current = Require(key);
            if (current.Status == "finalized" && string.Equals(current.RunId, runId, StringComparison.Ordinal))
                return current;
            if (current.Status != "reserved")
                throw new InvalidOperationException($"Activation record is {current.Status}, not reserved");
            return Update(current with
            {
                Status = "finalized",
                RunId = runId,
                UpdatedAt = now ?? DateTimeOffset.UtcNow,
                Revision = current.Revision + 1,
            });
        }
    }

    public ActivationRecord Fail(ActivationKey key, string status, string reason, DateTimeOffset? now = null)
    {
        lock (_sync)
        {
            EnsureWritable();
            if (status is not ("failed" or "blocked"))
                throw new ArgumentException("Activation failure status must be failed or blocked", nameof(status));
            var current = Require(key);
            if (current.Status == status && string.Equals(current.Reason, reason, StringComparison.Ordinal))
                return current;
            if (current.Status is "finalized" or "failed" or "blocked")
                return current;
            return Update(current with
            {
                Status = status,
                Reason = reason,
                UpdatedAt = now ?? DateTimeOffset.UtcNow,
                Revision = current.Revision + 1,
            });
        }
    }

    private ActivationRecord Require(ActivationKey key) =>
        _byKey.GetValueOrDefault(CanonicalKey(key))
        ?? throw new KeyNotFoundException($"Activation key '{CanonicalKey(key)}' was not found");

    private ActivationRecord Update(ActivationRecord record)
    {
        Store(record);
        return record;
    }

    private void Store(ActivationRecord record)
    {
        Append(record);
        _byKey[record.ActivationKey] = record;
        _byLaunch[record.LaunchId] = record;
        _byIdempotency[record.IdempotencyKey] = record;
    }

    private void Append(ActivationRecord record)
    {
        try { _writer!.WriteLine(JsonSerializer.Serialize(record)); }
        catch { _faulted = true; throw; }
    }

    private void EnsureWritable()
    {
        ObjectDisposedException.ThrowIf(_writer is null, this);
        if (_faulted) throw new IOException("activation-record-store-faulted-reopen-required");
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var record = JsonSerializer.Deserialize<ActivationRecord>(line);
                if (record is null) continue;
                _byKey[record.ActivationKey] = record;
                _byLaunch[record.LaunchId] = record;
                _byIdempotency[record.IdempotencyKey] = record;
            }
            catch (JsonException) { /* torn tail frame is not committed */ }
        }
    }

    private static string CanonicalKey(ActivationKey key) =>
        key.AcceptedContractViewRef + "\u001f" + key.ContractGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void Dispose()
    {
        lock (_sync)
        {
            var writer = _writer;
            _writer = null;
            writer?.Dispose();
        }
    }

    public sealed record Reservation(ActivationRecord Record, bool Conflict, bool Created);
}
