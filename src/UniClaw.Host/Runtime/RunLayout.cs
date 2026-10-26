using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>
/// RUNTIME-TASK-001 切片 2（定形 S2-1/2/3/6，D7/D8/D9）：task 组织的 run
/// 存储布局。新 run 落 <c>tasks/&lt;TaskDirName(taskRef)&gt;/&lt;instanceDir&gt;/</c>；
/// 无 task（CLI / 无 Launch）落 <c>_untasked/run-&lt;时间戳&gt;/</c>；旧平铺
/// <c>run-&lt;ts&gt;</c> 目录原地共存、不迁移（S2-1）。
/// TaskDirName 非身份（D8：身份权威在 TaskInstanceStore），仅介质组织；
/// task.json 是引用+指纹（D7），write-if-absent、永不改写。
/// </summary>
public static class RunLayout
{
    /// <summary>S2-2：taskRef 尾段清洗为 [a-z0-9-]（压缩连字符、截断 40），
    /// 后缀 "." + sha256(taskRef) 前 8 hex（小写）。全 ref 决定 hash，防尾段碰撞。</summary>
    public static string TaskDirName(string taskRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskRef);
        var tail = taskRef.Split('/', '\\').Last(segment => segment.Length > 0);
        var slug = new StringBuilder();
        foreach (var character in tail.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '-')
                slug.Append(character);
            else if (character == ' ' || character == '_' || character == '.')
                slug.Append('-');
            // 其余字符丢弃（清洗为 [a-z0-9-]）
        }
        var cleaned = new StringBuilder();
        foreach (var character in slug.ToString())
        {
            if (character == '-' && cleaned.Length > 0 && cleaned[^1] == '-') continue;
            cleaned.Append(character);
        }
        var name = cleaned.ToString().Trim('-');
        if (name.Length > 40) name = name[..40].Trim('-');
        if (name.Length == 0) name = "task";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(taskRef)))
            .ToLowerInvariant()[..8];
        return $"{name}.{hash}";
    }

    /// <summary>S2-3：instanceDir = TaskInstanceId（admission 铸的 ti-），
    /// 缺省回退 runId；无 taskRef → _untasked/run-&lt;ts&gt;（沿用旧命名）。
    /// 返回已创建的绝对 run 目录。</summary>
    public static string CreateRunDir(string runRoot, string? taskRef, string? taskInstanceId, string? runId)
    {
        var rootFull = Path.GetFullPath(runRoot);
        Directory.CreateDirectory(rootFull);
        string runDir;
        if (!string.IsNullOrWhiteSpace(taskRef))
        {
            var instanceDir = Coalesce(taskInstanceId) ?? Coalesce(runId) ?? $"run-{Timestamp()}";
            runDir = Path.Combine(rootFull, "tasks", TaskDirName(taskRef), instanceDir);
        }
        else
        {
            runDir = Path.Combine(rootFull, "_untasked", $"run-{Timestamp()}");
            for (var suffix = 1; Directory.Exists(runDir); suffix++)
                runDir = Path.Combine(rootFull, "_untasked", $"run-{Timestamp()}-{suffix}");
        }
        Directory.CreateDirectory(runDir);
        return runDir;
    }

    /// <summary>task 目录：tasks/&lt;TaskDirName(taskRef)&gt;/（介质组织，非身份）。</summary>
    public static string TaskDirectory(string runRoot, string taskRef) =>
        Path.Combine(Path.GetFullPath(runRoot), "tasks", TaskDirName(taskRef));

    /// <summary>S2-6（D7）：task.json 引用+指纹非副本；write-if-absent、永不改写。</summary>
    public static void WriteTaskReferenceIfAbsent(string runRoot, string taskRef,
        string? declarationFingerprint, string firstSeenRunId)
    {
        var directory = TaskDirectory(runRoot, taskRef);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "task.json");
        if (File.Exists(path)) return; // write-if-absent：首见内容即权威
        File.WriteAllText(path, JsonSerializer.Serialize(
            new TaskReference("task-reference.v1", taskRef, declarationFingerprint,
                firstSeenRunId, DateTimeOffset.UtcNow),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    /// <summary>A2（D9）：_untasked 下 run 目录数量可统计（无段 → 0）。</summary>
    public static int CountUntaskedRuns(string runsRoot)
    {
        var directory = Path.Combine(Path.GetFullPath(runsRoot), "_untasked");
        return Directory.Exists(directory) ? Directory.EnumerateDirectories(directory).Count() : 0;
    }

    private static string? Coalesce(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Timestamp() => DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

    private sealed record TaskReference(
        string SchemaVersion, string TaskRef, string? DeclarationFingerprint,
        string FirstSeenRunId, DateTimeOffset CreatedAt);
}
