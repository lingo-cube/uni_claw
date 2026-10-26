using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>
/// TaskInstance 持久化记录（append-only JSON 行；RUNTIME-IDENTITY-001 D1）。
/// Declaration/DeclarationFingerprint：RUNTIME-TASK-001 切片 1（定形 T1）——
/// 声明块由 workbench 计算、Runtime 原样持久化（canonical JSON 字符串 +
/// 其键递归排序无缩进形式的 SHA-256 64 位 hex），不引入 Runtime 侧 resolver。
/// </summary>
public sealed record TaskInstanceRecord(
    string TaskInstanceId,
    string TaskRef,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    string? ProductSessionId = null,
    string? Declaration = null,
    string? DeclarationFingerprint = null);

/// <summary>
/// RUNTIME-TASK-001 切片 1（A6-②）：同 idempotencyKey 但声明指纹不同——
/// 显式拒绝；原记录不变、不覆盖、不追加（供端点转 4xx input-conflict）。
/// </summary>
public sealed class TaskInstanceInputConflictException : Exception
{
    public string IdempotencyKey { get; }
    public string? ExistingFingerprint { get; }
    public string? IncomingFingerprint { get; }

    public TaskInstanceInputConflictException(string idempotencyKey,
        string? existingFingerprint, string? incomingFingerprint)
        : base($"task-instance-input-conflict: idempotencyKey '{idempotencyKey}' is pinned to declaration fingerprint '{existingFingerprint ?? "null"}'; incoming fingerprint '{incomingFingerprint ?? "null"}' differs")
    {
        IdempotencyKey = idempotencyKey;
        ExistingFingerprint = existingFingerprint;
        IncomingFingerprint = incomingFingerprint;
    }
}

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

    /// <summary>同 idempotencyKey 幂等；新 key 铸造新 id（无声明历史路径）。</summary>
    public TaskInstanceRecord Create(string taskRef, string idempotencyKey) =>
        Create(taskRef, idempotencyKey, declaration: null);

    /// <summary>
    /// RUNTIME-TASK-001 切片 1：admission 幂等以声明指纹为比较基准——
    /// 同 idempotencyKey 且 fingerprint 相同 → 复用已有记录（A6-①）；
    /// 同 key 但 fingerprint 不同 → 抛 TaskInstanceInputConflictException（A6-②）。
    /// 先持久化（append）后返回成功；append-only 与 Load 重放语义不变。
    /// </summary>
    public TaskInstanceRecord Create(string taskRef, string idempotencyKey, string? declaration)
    {
        string? declarationCanonical = null;
        string? fingerprint = null;
        if (declaration is not null)
        {
            // 声明块原样持久化仅指语义；物理形态取 canonical JSON（键递归排序、
            // 无缩进），fingerprint 即其 SHA-256——input-conflict 的比较基准。
            using var document = JsonDocument.Parse(declaration);
            declarationCanonical = CanonicalJson(document.RootElement);
            fingerprint = DeclarationFingerprint(declarationCanonical);
        }
        lock (_sync)
        {
            EnsureWritable();
            if (_byKey.TryGetValue(idempotencyKey, out var existing))
            {
                if (!string.Equals(existing.DeclarationFingerprint, fingerprint, StringComparison.Ordinal))
                    throw new TaskInstanceInputConflictException(
                        idempotencyKey, existing.DeclarationFingerprint, fingerprint);
                return existing;
            }
            var record = new TaskInstanceRecord(
                $"ti-{Guid.NewGuid():N}",
                taskRef, idempotencyKey, DateTimeOffset.UtcNow,
                Declaration: declarationCanonical,
                DeclarationFingerprint: fingerprint);
            Append(record);
            _byId[record.TaskInstanceId] = record;
            _byKey[idempotencyKey] = record;
            return record;
        }
    }

    /// <summary>canonical JSON：对象键递归按 Ordinal 排序、无空白；标量取原始文本。</summary>
    internal static string CanonicalJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => $"{JsonSerializer.Serialize(property.Name)}:{CanonicalJson(property.Value)}"))
            + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(CanonicalJson)) + "]",
        _ => element.GetRawText(),
    };

    /// <summary>declarationFingerprint ＝ canonical JSON（UTF-8）SHA-256 的小写 64 位 hex。</summary>
    internal static string DeclarationFingerprint(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

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
