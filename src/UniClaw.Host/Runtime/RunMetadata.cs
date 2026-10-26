using System.Text.Json;

namespace UniClaw.Host.Runtime;

/// <summary>RUNTIME-TASK-001 切片 2（定形 S2-5，F4）：run 目录 metadata.json
/// 由 Host 侧单一所有、CLI 与 HTTP 双路径统一写入。声明层字段在无 Launch
/// （CLI）时为 null；runDir 为相对 runs 根的路径（S2-1 新布局下即
/// tasks/&lt;taskDir&gt;/&lt;instanceDir&gt; 或 _untasked/run-&lt;ts&gt;）。</summary>
public static class RunMetadata
{
    public const string SchemaVersion = "uniclaw.run-metadata.v2";

    /// <summary>changeId 读环境变量 UNICLAW_CHANGE_ID；无则 null。</summary>
    public static string? ChangeId() =>
        Environment.GetEnvironmentVariable("UNICLAW_CHANGE_ID") is { } value && value.Trim().Length > 0
            ? value.Trim() : null;

    public static async Task WriteAsync(string runRoot, string runDir, string runId,
        HostRunner.LaunchContext? launch, ImplementationFingerprint? fingerprint = null)
    {
        fingerprint ??= ImplementationFingerprint.Compute();
        var relative = Path.GetRelativePath(Path.GetFullPath(runRoot), Path.GetFullPath(runDir))
            .Replace('\\', '/');
        var metadata = new Metadata(
            SchemaVersion,
            runId,
            launch?.ProductSessionId,
            launch?.TaskInstanceId,
            launch?.TaskRef,
            launch?.LaunchId,
            launch?.CorrelationId,
            ChangeId(),
            launch?.DeclarationFingerprint,
            fingerprint,
            relative,
            DateTimeOffset.UtcNow);
        var path = Path.Combine(runDir, "metadata.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(metadata,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }))
            .ConfigureAwait(false);
    }

    private sealed record Metadata(
        string SchemaVersion,
        string RunId,
        string? ProductSessionId,
        string? TaskInstanceId,
        string? TaskRef,
        string? LaunchId,
        string? CorrelationId,
        string? ChangeId,
        string? DeclarationFingerprint,
        ImplementationFingerprint ImplementationFingerprint,
        string RunDir,
        DateTimeOffset CreatedAt);
}

/// <summary>RUNTIME-TASK-001 切片 2（定形 T2 / S2-7）：实现指纹。
/// commit = git rev-parse HEAD（失败 null）；sourceDigest = src/UniClaw.*/**/*.cs
/// （排除 bin/obj）按路径排序、CRLF→LF 归一拼接的 SHA-256（小写 hex）。
/// repo 探测不到（无 .git / *.sln(x) 标记）→ 双 null，不 fail。</summary>
public sealed record ImplementationFingerprint(string? Commit, string? SourceDigest)
{
    public static ImplementationFingerprint Compute(string? repoRoot = null)
    {
        var root = repoRoot is not null ? DetectMarker(repoRoot) : DetectFromBaseDirectory();
        if (root is null)
            return new ImplementationFingerprint(null, null);
        return new ImplementationFingerprint(TryGitCommit(root), ComputeSourceDigest(root));
    }

    /// <summary>注入根：根目录自身含 .git / *.sln / *.slnx 标记才算 repo。</summary>
    private static string? DetectMarker(string root)
    {
        if (!Directory.Exists(root)) return null;
        return HasRepoMarker(root) ? Path.GetFullPath(root) : null;
    }

    private static string? DetectFromBaseDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            if (HasRepoMarker(directory.FullName)) return directory.FullName;
        }
        return null;
    }

    private static bool HasRepoMarker(string directory) =>
        Directory.Exists(Path.Combine(directory, ".git"))
        || File.Exists(Path.Combine(directory, ".git"))
        || Directory.EnumerateFiles(directory, "*.sln").Any()
        || Directory.EnumerateFiles(directory, "*.slnx").Any();

    private static string? TryGitCommit(string repoRoot)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = repoRoot,
            };
            using var process = System.Diagnostics.Process.Start(info);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(10_000)) { try { process.Kill(); } catch { } return null; }
            var commit = output.Result.Trim();
            return process.ExitCode == 0 && commit.Length > 0 ? commit : null;
        }
        catch (Exception)
        {
            return null; // git 不可用 ≠ run 失败（T2 不 fail）
        }
    }

    private static string ComputeSourceDigest(string repoRoot)
    {
        var sourceDirectory = Path.Combine(repoRoot, "src");
        var files = Directory.Exists(sourceDirectory)
            ? Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsUnderIgnored(path))
                .OrderBy(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'),
                    StringComparer.Ordinal)
            : Enumerable.Empty<string>();
        using var incremental = System.Security.Cryptography.IncrementalHash.CreateHash
            (System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            var normalized = File.ReadAllText(file).Replace("\r\n", "\n").Replace('\r', '\n');
            incremental.AppendData(System.Text.Encoding.UTF8.GetBytes(normalized));
        }
        return Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool IsUnderIgnored(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }
}
