using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>
/// TaskInstance 持久化记录（append-only JSON 行；RUNTIME-IDENTITY-001 D1）。
/// </summary>
public sealed record TaskInstanceRecord(
    string TaskInstanceId,
    string TaskRef,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    string? ProductSessionId = null);

/// <summary>
/// TaskInstanceStore — Runtime 铸造 taskInstanceId 的最小 append-only 文件存储
/// （ADR-0044/0045；RUNTIME-IDENTITY-001 D1-D4）。
/// 幂等：同 idempotencyKey 重入返回已有实例（ADR-0046）。
/// 1:N（taskInstance → productSession）预留但当前 1:1（ADR-0045 当前范围）。
/// </summary>
public sealed class TaskInstanceStore : IDisposable
{
    private readonly string _path;
    private readonly Dictionary<string, TaskInstanceRecord> _byId = new();
    private readonly Dictionary<string, TaskInstanceRecord> _byKey = new();
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private bool _faulted;

    public TaskInstanceStore(string filePath)
    {
        _path = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        Load();
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    /// <summary>同 idempotencyKey 幂等；新 key 铸造新 id。</summary>
    public TaskInstanceRecord Create(string taskRef, string idempotencyKey)
    {
        lock (_sync)
        {
            EnsureWritable();
            if (_byKey.TryGetValue(idempotencyKey, out var existing))
                return existing;
            var record = new TaskInstanceRecord(
                $"ti-{Guid.NewGuid():N}",
                taskRef, idempotencyKey, DateTimeOffset.UtcNow);
            Append(record);
            _byId[record.TaskInstanceId] = record;
            _byKey[idempotencyKey] = record;
            return record;
        }
    }

    public TaskInstanceRecord? Get(string taskInstanceId)
    {
        lock (_sync) return _byId.GetValueOrDefault(taskInstanceId);
    }

    /// <summary>绑定 session（幂等；同 instance 绑同 session 返回原记录）。</summary>
    public TaskInstanceRecord BindSession(string taskInstanceId, string productSessionId)
    {
        lock (_sync)
        {
            EnsureWritable();
            var record = _byId[taskInstanceId]
                ?? throw new KeyNotFoundException($"TaskInstance {taskInstanceId} not found");
            if (record.ProductSessionId == productSessionId) return record;
            if (record.ProductSessionId is not null)
                throw new InvalidOperationException(
                    $"TaskInstance {taskInstanceId} already bound to {record.ProductSessionId}");
            var updated = record with { ProductSessionId = productSessionId };
            Append(updated);
            _byId[taskInstanceId] = updated;
            _byKey[updated.IdempotencyKey] = updated;
            return updated;
        }
    }

    private void Append(TaskInstanceRecord record)
    {
        try { _writer!.WriteLine(JsonSerializer.Serialize(record)); }
        catch { _faulted = true; throw; }
    }

    private void EnsureWritable()
    {
        ObjectDisposedException.ThrowIf(_writer is null, this);
        if (_faulted) throw new IOException("task-instance-store-faulted-reopen-required");
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        foreach (var line in File.ReadAllLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var record = JsonSerializer.Deserialize<TaskInstanceRecord>(line);
                if (record is null) continue;
                _byId[record.TaskInstanceId] = record;
                _byKey[record.IdempotencyKey] = record;
            }
            catch (JsonException) { /* torn tail frame = never committed (journal 先例) */ }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            var writer = _writer;
            _writer = null;
            writer?.Dispose();
        }
    }
}
