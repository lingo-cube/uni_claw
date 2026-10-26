using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>
/// RUNTIME-TASK-001 切片 2（S2-8 / A2 / A3 / A4 / A5）：ResolveRunDir 契约升级——
/// 相对多段路径（新布局 tasks/&lt;taskDir&gt;/&lt;instanceDir&gt;/）与单段旧名共存；
/// 拒绝 .. / rooted / 盘符 / 空；GetFullPath 后包含性校验。以及身份反向测试：
/// 篡改/重命名 run 目录不改变 TaskInstanceStore 与 RuntimeRunStore 的查询结果
/// （D8：身份权威在 store，不在目录名）。
/// </summary>
public sealed class RuntimeRunDirContractTests
{
    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "uniclaw-s2dsh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    // ---- S2-8 接受分支 ----------------------------------------------------

    [Fact]
    public void RelativeMultiSegmentPath_IsAccepted()
    {
        var root = TempRoot();
        try
        {
            var runDir = Path.Combine(root, "tasks", "flip.a1b2c3d4", "ti-1");
            Directory.CreateDirectory(runDir);
            Assert.Equal(Path.GetFullPath(runDir),
                RuntimeToolHost.ResolveRunDir(root, Path.Combine("tasks", "flip.a1b2c3d4", "ti-1")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void LegacySingleSegmentName_IsStillAccepted()
    {
        var root = TempRoot();
        try
        {
            var runDir = Path.Combine(root, "run-20261026-000000-000");
            Directory.CreateDirectory(runDir);
            Assert.Equal(Path.GetFullPath(runDir),
                RuntimeToolHost.ResolveRunDir(root, "run-20261026-000000-000"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- S2-8 拒绝分支（四种） ---------------------------------------------

    [Theory]
    [InlineData("tasks/../escape")]            // .. 段
    [InlineData("..")]
    [InlineData("run..x")]                     // 旧契约：任何 .. 子串拒绝
    [InlineData("C:\\absolute\\path")]         // rooted/盘符
    [InlineData("/absolute/path")]             // rooted
    [InlineData("")]
    [InlineData("   ")]
    public void UnsafeRunDirs_AreRejected(string runDirName)
    {
        var root = TempRoot();
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => RuntimeToolHost.ResolveRunDir(root, runDirName));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void MissingDirectory_IsRejected()
    {
        var root = TempRoot();
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => RuntimeToolHost.ResolveRunDir(root, "run-not-there"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- A3 TaskInstanceStore 身份查询不受 run 目录改名影响 ------------------

    [Fact]
    public void TaskInstanceLookup_UnchangedAfterRunDirectoryRename()
    {
        var root = TempRoot();
        var storePath = Path.Combine(root, ".identity", "task-instances.jsonl");
        try
        {
            TaskInstanceRecord record;
            using (var store = new TaskInstanceStore(storePath))
            {
                record = store.Create("task/t/flip", "idem-a3");
                Assert.Equal("task/t/flip", store.Get(record.TaskInstanceId)!.TaskRef);
            }

            var runDir = Path.Combine(root, "tasks", "flip.deadbeef", record.TaskInstanceId);
            Directory.CreateDirectory(runDir);
            var renamed = Path.Combine(root, "tasks", "flip.deadbeef", "ti-renamed");
            Directory.Move(runDir, renamed);

            using (var reloaded = new TaskInstanceStore(storePath))
            {
                var found = reloaded.Get(record.TaskInstanceId);
                Assert.NotNull(found);
                Assert.Equal(record.TaskRef, found.TaskRef);
                Assert.Equal(record.IdempotencyKey, found.IdempotencyKey);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- A5 RuntimeRunStore 身份查询不受 run 目录改名影响 --------------------

    [Fact]
    public void RuntimeRunLookup_UnchangedAfterRunDirectoryRename()
    {
        var root = TempRoot();
        try
        {
            var store = new RuntimeRunStore(Path.Combine(root, ".runtime-runs"));
            var run = store.Create(new RuntimeRunStore.CreateRequest(
                "session-a5", null, "launch-a5", "idem-a5", "corr-a5", null, null, null, null));

            var runDir = Path.Combine(root, "tasks", "flip.deadbeef", run.RunId);
            Directory.CreateDirectory(runDir);
            Directory.Move(runDir, Path.Combine(root, "tasks", "flip.deadbeef", "tampered-name"));

            Assert.NotNull(store.Get(run.RunId));
            Assert.Equal(run.RunId, store.FindByIdempotencyKey("idem-a5")!.RunId);
            Assert.Equal(run.RunId, store.FindByLaunchId("launch-a5")!.RunId);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // ---- A2 _untasked 数量可统计（工具函数视角） -----------------------------

    [Fact]
    public void UntaskedRuns_AreCountable()
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
}
