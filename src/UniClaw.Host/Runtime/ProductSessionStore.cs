using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>
/// ProductSession 持久化记录（append-only JSON 行；RUNTIME-IDENTITY-001 D1）。
/// </summary>
public sealed record ProductSessionRecord(
    string ProductSessionId,
    string TaskInstanceId,
    DateTimeOffset CreatedAt,
    string? RunId = null);

/// <summary>
/// ProductSessionStore — Runtime 铸造 productSessionId 的最小 append-only 文件存储
/// （ADR-0044/0045；RUNTIME-IDENTITY-001 D1-D4）。
/// 幂等：同 taskInstanceId 重入返回已有 session（1:1 当前基数，ADR-0045）。
/// </summary>
public sealed class ProductSessionStore : IDisposable
{
    private readonly string _path;
    private readonly Dictionary<string, ProductSessionRecord> _byId = new();
    private readonly Dictionary<string, ProductSessionRecord> _byTask = new();
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private bool _faulted;

    public ProductSessionStore(string filePath)
    {
        _path = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        Load();
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    /// <summary>同 taskInstanceId 幂等（1:1）；新 taskInstance 铸造新 session。</summary>
    public ProductSessionRecord Create(string taskInstanceId)
    {
        lock (_sync)
        {
            EnsureWritable();
            if (_byTask.TryGetValue(taskInstanceId, out var existing))
                return existing;
            var record = new ProductSessionRecord(
                $"ps-{Guid.NewGuid():N}",
                taskInstanceId, DateTimeOffset.UtcNow);
            Append(record);
            _byId[record.ProductSessionId] = record;
            _byTask[taskInstanceId] = record;
            return record;
        }
    }

    public ProductSessionRecord? Get(string productSessionId)
    {
        lock (_sync) return _byId.GetValueOrDefault(productSessionId);
    }

    /// <summary>绑定 run（可选且幂等；同 session 绑同 run 返回原记录）。</summary>
    public ProductSessionRecord BindRun(string productSessionId, string runId)
    {
        lock (_sync)
        {
            EnsureWritable();
            var record = _byId[productSessionId]
                ?? throw new KeyNotFoundException($"ProductSession {productSessionId} not found");
            if (record.RunId == runId) return record;
            if (record.RunId is not null)
                throw new InvalidOperationException(
                    $"ProductSession {productSessionId} already bound to run {record.RunId}");
            var updated = record with { RunId = runId };
            Append(updated);
            _byId[productSessionId] = updated;
            _byTask[updated.TaskInstanceId] = updated;
            return updated;
        }
    }

    private void Append(ProductSessionRecord record)
    {
        try { _writer!.WriteLine(JsonSerializer.Serialize(record)); }
        catch { _faulted = true; throw; }
    }

    private void EnsureWritable()
    {
        ObjectDisposedException.ThrowIf(_writer is null, this);
        if (_faulted) throw new IOException("product-session-store-faulted-reopen-required");
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        foreach (var line in File.ReadAllLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var record = JsonSerializer.Deserialize<ProductSessionRecord>(line);
                if (record is null) continue;
                _byId[record.ProductSessionId] = record;
                _byTask[record.TaskInstanceId] = record;
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
