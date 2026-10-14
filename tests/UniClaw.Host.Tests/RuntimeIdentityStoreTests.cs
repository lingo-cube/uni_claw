using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// RUNTIME-IDENTITY-001 — TaskInstanceStore / ProductSessionStore 锚定测试。
/// A1 Create 幂等 · A2 重启恢复 · A3 1:1 强制 · A4 BindRun 幂等。
/// </summary>
public sealed class RuntimeIdentityStoreTests
{
    private static string TempFile(string name) =>
        Path.Combine(Path.GetTempPath(), $"rid-{Guid.NewGuid():N}", name);

    [Fact]
    public void A1_TaskInstance_CreateIdempotent()
    {
        var file = TempFile("ti.jsonl");
        using (var store = new TaskInstanceStore(file))
        {
            var a = store.Create("settings-coverage", "launch-key-001");
            var b = store.Create("settings-coverage", "launch-key-001");
            Assert.Equal(a.TaskInstanceId, b.TaskInstanceId);
            Assert.Single(System.IO.File.ReadAllLines(file).Where(l => !string.IsNullOrWhiteSpace(l)));
        }
    }

    [Fact]
    public void A1_ProductSession_CreateIdempotent_1To1()
    {
        var file = TempFile("ps.jsonl");
        using (var store = new ProductSessionStore(file))
        {
            var a = store.Create("ti-001");
            var b = store.Create("ti-001");
            Assert.Equal(a.ProductSessionId, b.ProductSessionId);
        }
    }

    [Fact]
    public void A2_TaskInstance_RestartRecovers()
    {
        var file = TempFile("ti.jsonl");
        string id;
        using (var store = new TaskInstanceStore(file))
        {
            id = store.Create("settings-coverage", "launch-key-002").TaskInstanceId;
        }
        using (var restored = new TaskInstanceStore(file))
        {
            var record = restored.Get(id);
            Assert.NotNull(record);
            Assert.Equal("settings-coverage", record!.TaskRef);
            // 幂等键也恢复了
            var again = restored.Create("settings-coverage", "launch-key-002");
            Assert.Equal(id, again.TaskInstanceId);
        }
    }

    [Fact]
    public void A2_ProductSession_RestartRecovers()
    {
        var file = TempFile("ps.jsonl");
        string sessionId;
        using (var store = new ProductSessionStore(file))
        {
            sessionId = store.Create("ti-003").ProductSessionId;
        }
        using (var restored = new ProductSessionStore(file))
        {
            var record = restored.Get(sessionId);
            Assert.NotNull(record);
            Assert.Equal("ti-003", record!.TaskInstanceId);
        }
    }

    [Fact]
    public void A3_TaskInstance_BindSession_Idempotent()
    {
        var file = TempFile("ti.jsonl");
        using var store = new TaskInstanceStore(file);
        var record = store.Create("settings-coverage", "launch-key-003");
        store.BindSession(record.TaskInstanceId, "ps-abc");
        // 同 session 重入幂等
        var again = store.BindSession(record.TaskInstanceId, "ps-abc");
        Assert.Equal("ps-abc", again.ProductSessionId);
    }

    [Fact]
    public void A3_TaskInstance_BindSecondSession_Throws()
    {
        var file = TempFile("ti.jsonl");
        using var store = new TaskInstanceStore(file);
        var record = store.Create("settings-coverage", "launch-key-004");
        store.BindSession(record.TaskInstanceId, "ps-first");
        Assert.Throws<InvalidOperationException>(
            () => store.BindSession(record.TaskInstanceId, "ps-second"));
    }

    [Fact]
    public void A4_ProductSession_BindRun_Idempotent()
    {
        var file = TempFile("ps.jsonl");
        using var store = new ProductSessionStore(file);
        var session = store.Create("ti-005");
        store.BindRun(session.ProductSessionId, "run-001");
        var again = store.BindRun(session.ProductSessionId, "run-001");
        Assert.Equal("run-001", again.RunId);
    }

    [Fact]
    public void A4_ProductSession_BindSecondRun_Throws()
    {
        var file = TempFile("ps.jsonl");
        using var store = new ProductSessionStore(file);
        var session = store.Create("ti-006");
        store.BindRun(session.ProductSessionId, "run-001");
        Assert.Throws<InvalidOperationException>(
            () => store.BindRun(session.ProductSessionId, "run-002"));
    }
}
