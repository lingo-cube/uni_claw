using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// RUNTIME-TASK-001 切片 2（定形 S2-1..S2-7）：task 组织的 run 存储布局、
/// task.json write-if-absent、metadata.json（uniclaw.run-metadata.v2）双路径
/// 统一投影、T2 实现指纹。HostRunner 与 RuntimeHttpServer 共用这些单元；
/// 完整 RunOnceAsync 闭环需真机（ENVIRONMENT 门控 live 测试），此处按单元
/// 缝验证形状。
/// </summary>
public sealed class RunLayoutTests
{
    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), "uniclaw-s2-" + Guid.NewGuid().ToString("N"));

    private static string Hash8(string taskRef) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(taskRef))).ToLowerInvariant()[..8];

    // ---- S2-2 TaskDirName ------------------------------------------------

    [Fact]
    public void TaskDirName_UsesCleanedTailPlusHashSuffix()
    {
        var taskRef = "task/android-settings/toggle-wifi-reuse";
        Assert.Equal("toggle-wifi-reuse." + Hash8(taskRef), RunLayout.TaskDirName(taskRef));
    }

    [Fact]
    public void TaskDirName_CleansIllegalCharsCollapsesHyphensAndTruncates()
    {
        var taskRef = "task/Android Settings!!/My  Feature";
        var name = RunLayout.TaskDirName(taskRef);
        Assert.Equal("my-feature." + Hash8(taskRef), name);

        var longTail = new string('x', 100);
        var truncated = RunLayout.TaskDirName("task/" + longTail);
        Assert.Equal(new string('x', 40) + "." + Hash8("task/" + longTail), truncated);
    }

    [Fact]
    public void TaskDirName_IsDeterministicPerFullRef()
    {
        // 尾段相同、全 ref 不同 → 同 slug 不同 hash（不同 task 不共享目录）
        var a = RunLayout.TaskDirName("task/set-a/flip");
        var b = RunLayout.TaskDirName("task/set-b/flip");
        Assert.Equal(a[..a.IndexOf('.')], b[..b.IndexOf('.')]);
        Assert.NotEqual(a, b);
    }

    // ---- S2-1/S2-3 目录布局 ---------------------------------------------

    [Fact]
    public void ComputeRunDir_TaskLaunch_LandsUnderTasksTwoLevelLayout()
    {
        var root = TempRoot();
        try
        {
            var taskRef = "task/android-settings/toggle-wifi-reuse";
            var runDir = RunLayout.CreateRunDir(root, taskRef, "ti-abc", "run-1");
            var expected = Path.Combine(Path.GetFullPath(root), "tasks",
                "toggle-wifi-reuse." + Hash8(taskRef), "ti-abc");
            Assert.Equal(expected, runDir);
            Assert.True(Directory.Exists(runDir));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ComputeRunDir_InstanceDirFallsBackToRunId()
    {
        var root = TempRoot();
        try
        {
            var runDir = RunLayout.CreateRunDir(root, "task/t/flip", null, "run-xyz");
            Assert.EndsWith(Path.Combine("tasks", RunLayout.TaskDirName("task/t/flip"), "run-xyz"), runDir);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ComputeRunDir_NoLaunch_FallsToUntaskedTimestamp()
    {
        var root = TempRoot();
        try
        {
            var runDir = RunLayout.CreateRunDir(root, null, null, null);
            Assert.StartsWith(Path.Combine(Path.GetFullPath(root), "_untasked", "run-"), runDir);
            Assert.True(Directory.Exists(runDir));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- A2 _untasked 数量可统计 -----------------------------------------

    [Fact]
    public void CountUntaskedRuns_EnumeratesRunDirectories()
    {
        var root = TempRoot();
        try
        {
            Assert.Equal(0, RunLayout.CountUntaskedRuns(root));
            RunLayout.CreateRunDir(root, null, null, null);
            RunLayout.CreateRunDir(root, null, null, null);
            RunLayout.CreateRunDir(root, "task/t/flip", "ti-1", "run-1");
            Assert.Equal(2, RunLayout.CountUntaskedRuns(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- S2-6 task.json write-if-absent（D7 引用非副本） ------------------

    [Fact]
    public void TaskReferenceJson_IsWriteIfAbsent()
    {
        var root = TempRoot();
        try
        {
            RunLayout.WriteTaskReferenceIfAbsent(root, "task/t/flip", "fp-first", "run-1");
            var path = Path.Combine(Path.GetFullPath(root), "tasks",
                RunLayout.TaskDirName("task/t/flip"), "task.json");
            var first = File.ReadAllText(path);
            Assert.Contains("\"task-reference.v1\"", first);
            Assert.Contains("fp-first", first);
            Assert.Contains("run-1", first);

            // 同 task 第二次 run：不改写首见内容
            RunLayout.WriteTaskReferenceIfAbsent(root, "task/t/flip", "fp-second", "run-2");
            Assert.Equal(first, File.ReadAllText(path));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- S2-5 metadata.json（uniclaw.run-metadata.v2） --------------------

    [Fact]
    public async Task RunMetadata_V2ContainsIdentityAndFingerprints()
    {
        var root = TempRoot();
        try
        {
            var taskRef = "task/t/flip";
            var launch = new HostRunner.LaunchContext(
                "run-1", "ps-1", "launch-1", "idem-1", "corr-1",
                TaskInstanceId: "ti-1",
                TaskRef: taskRef,
                DeclarationFingerprint: "decl-fp");
            var runDir = RunLayout.CreateRunDir(root, taskRef, "ti-1", "run-1");
            var fingerprint = new ImplementationFingerprint("commit-abc", "digest-123");
            await RunMetadata.WriteAsync(root, runDir, "run-1", launch, fingerprint);

            using var meta = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(runDir, "metadata.json")));
            var m = meta.RootElement;
            Assert.Equal("uniclaw.run-metadata.v2", m.GetProperty("schemaVersion").GetString());
            Assert.Equal("run-1", m.GetProperty("runId").GetString());
            Assert.Equal("ps-1", m.GetProperty("productSessionId").GetString());
            Assert.Equal("ti-1", m.GetProperty("taskInstanceId").GetString());
            Assert.Equal(taskRef, m.GetProperty("taskRef").GetString());
            Assert.Equal("launch-1", m.GetProperty("launchId").GetString());
            Assert.Equal("corr-1", m.GetProperty("correlationId").GetString());
            Assert.Equal("decl-fp", m.GetProperty("declarationFingerprint").GetString());
            Assert.Equal("commit-abc", m.GetProperty("implementationFingerprint").GetProperty("commit").GetString());
            Assert.Equal("digest-123", m.GetProperty("implementationFingerprint").GetProperty("sourceDigest").GetString());
            // runDir 相对根（不再绝对）
            var relative = m.GetProperty("runDir").GetString()!;
            Assert.False(Path.IsPathRooted(relative));
            Assert.Equal(Path.Combine("tasks", RunLayout.TaskDirName(taskRef), "ti-1"),
                Path.Combine(relative.Split('/', '\\')));
            Assert.True(m.TryGetProperty("createdAt", out _));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task RunMetadata_DeclarationLayerIsNullWithoutLaunch()
    {
        var root = TempRoot();
        try
        {
            var runDir = RunLayout.CreateRunDir(root, null, null, null);
            await RunMetadata.WriteAsync(root, runDir, "run-9", null, new ImplementationFingerprint(null, null));
            using var meta = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(runDir, "metadata.json")));
            var m = meta.RootElement;
            Assert.Equal(JsonValueKind.Null, m.GetProperty("taskInstanceId").ValueKind);
            Assert.Equal(JsonValueKind.Null, m.GetProperty("taskRef").ValueKind);
            Assert.Equal(JsonValueKind.Null, m.GetProperty("productSessionId").ValueKind);
            Assert.Equal(JsonValueKind.Null, m.GetProperty("declarationFingerprint").ValueKind);
            Assert.Equal(JsonValueKind.Null, m.GetProperty("implementationFingerprint").GetProperty("commit").ValueKind);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task RunMetadata_ChangeIdReadsEnvironmentVariable()
    {
        var root = TempRoot();
        var previous = Environment.GetEnvironmentVariable("UNICLAW_CHANGE_ID");
        try
        {
            Environment.SetEnvironmentVariable("UNICLAW_CHANGE_ID", "change-42");
            var runDir = RunLayout.CreateRunDir(root, null, null, null);
            await RunMetadata.WriteAsync(root, runDir, "run-1", null, new ImplementationFingerprint(null, null));
            using var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir, "metadata.json")));
            Assert.Equal("change-42", meta.RootElement.GetProperty("changeId").GetString());

            Environment.SetEnvironmentVariable("UNICLAW_CHANGE_ID", null);
            var runDir2 = RunLayout.CreateRunDir(root, null, null, null);
            await RunMetadata.WriteAsync(root, runDir2, "run-2", null, new ImplementationFingerprint(null, null));
            using var meta2 = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDir2, "metadata.json")));
            Assert.Equal(JsonValueKind.Null, meta2.RootElement.GetProperty("changeId").ValueKind);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNICLAW_CHANGE_ID", previous);
            Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>S2-7 T2 实现指纹：commit + src 归一哈希；repo 探测不到 → 双 null 不抛。</summary>
public sealed class ImplementationFingerprintTests
{
    private static string TempRepo()
    {
        var root = Directory.CreateTempSubdirectory("uniclaw-fp-").FullName;
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        return root;
    }

    private static void WriteSource(string repoRoot, string relative, string content)
    {
        var path = Path.Combine(repoRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void SameCommitDifferentSourceContent_YieldsDifferentDigest()
    {
        var repoA = TempRepo();
        var repoB = TempRepo();
        try
        {
            WriteSource(repoA, "src/UniClaw.Sample/Sample.cs", "class A { }\r\n");
            WriteSource(repoB, "src/UniClaw.Sample/Sample.cs", "class A { changed }\r\n");
            var a = ImplementationFingerprint.Compute(repoA);
            var b = ImplementationFingerprint.Compute(repoB);
            Assert.NotEqual(a.SourceDigest, b.SourceDigest);
        }
        finally
        {
            Directory.Delete(repoA, recursive: true);
            Directory.Delete(repoB, recursive: true);
        }
    }

    [Fact]
    public void BinAndObjAreExcludedFromDigest()
    {
        var repoA = TempRepo();
        var repoB = TempRepo();
        try
        {
            WriteSource(repoA, "src/UniClaw.Sample/Sample.cs", "same");
            WriteSource(repoB, "src/UniClaw.Sample/Sample.cs", "same");
            WriteSource(repoB, "src/UniClaw.Sample/bin/Debug/Generated.cs", "different");
            WriteSource(repoB, "src/UniClaw.Sample/obj/Release/Generated.cs", "different");
            Assert.Equal(
                ImplementationFingerprint.Compute(repoA).SourceDigest,
                ImplementationFingerprint.Compute(repoB).SourceDigest);
        }
        finally
        {
            Directory.Delete(repoA, recursive: true);
            Directory.Delete(repoB, recursive: true);
        }
    }

    [Fact]
    public void CrlfAndLfNormalizeToSameDigest()
    {
        var repoA = TempRepo();
        var repoB = TempRepo();
        try
        {
            WriteSource(repoA, "src/UniClaw.Sample/Sample.cs", "a\r\nb\r\n");
            WriteSource(repoB, "src/UniClaw.Sample/Sample.cs", "a\nb\n");
            Assert.Equal(
                ImplementationFingerprint.Compute(repoA).SourceDigest,
                ImplementationFingerprint.Compute(repoB).SourceDigest);
        }
        finally
        {
            Directory.Delete(repoA, recursive: true);
            Directory.Delete(repoB, recursive: true);
        }
    }

    [Fact]
    public void RepoNotDetected_YieldsNulls_WithoutThrowing()
    {
        var bare = Directory.CreateTempSubdirectory("uniclaw-fp-bare-").FullName;
        try
        {
            var fingerprint = ImplementationFingerprint.Compute(bare);
            Assert.Null(fingerprint.Commit);
            Assert.Null(fingerprint.SourceDigest);
        }
        finally { Directory.Delete(bare, recursive: true); }
    }

    [Fact]
    public void DetectedRepo_CommitIsNullWhenGitUnavailable_ButDigestIsStable()
    {
        var repo = TempRepo();
        try
        {
            WriteSource(repo, "src/UniClaw.Sample/Sample.cs", "stable");
            var first = ImplementationFingerprint.Compute(repo);
            var second = ImplementationFingerprint.Compute(repo);
            Assert.NotNull(first.SourceDigest);
            Assert.Equal(first.SourceDigest, second.SourceDigest);
        }
        finally { Directory.Delete(repo, recursive: true); }
    }
}
