using System.Text.Json;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// RUNTIME-TASK-001 切片 1 — TaskInstance 声明块持久化（T1）锚定测试。
/// A6-① 同 key 同声明复用 · A6-② 同 key 异声明 input-conflict ·
/// A6-③ 重启后声明钉扎不变 · canonical fingerprint 双端一致锁。
/// </summary>
public sealed class RuntimeTaskDeclarationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("runtime-declaration-").FullName;
    private string TempFile(string name) => Path.Combine(_root, name);
    public void Dispose() => Directory.Delete(_root, true);

    // 定形 T1 固定样例（无空白原样）：两端（C# / workbench JS）各自计算
    // canonical JSON（键递归排序、无缩进）的 SHA-256，必须得到同一 64 位 hex。
    public const string FixedDeclarationJson =
        """{"schemaVersion":"task-declaration.v1","taskRef":"task/android-settings/toggle-wifi-reuse","intentDigest":null,"testSet":{"id":"testset/android-settings","version":"default","sourceRevision":"repo-default"},"scenarios":[],"fixtures":[{"id":"fixture/android-settings/settings-root","digest":null}],"acceptanceRefs":[]}""";

    public const string FixedDeclarationFingerprint =
        "f011bc6fd68c1f152cc4cc4954ab11e3d2357563676bc8f5193e07e7089d3e8e";

    private static string DifferentDeclarationJson =>
        FixedDeclarationJson.Replace("toggle-wifi-reuse", "toggle-wifi-reuse-v2");

    [Fact]
    public void A6_1_SameKeySameDeclaration_ReusesRecordWithoutAppending()
    {
        var file = TempFile("ti.jsonl");
        using var store = new TaskInstanceStore(file);
        var first = store.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-1", FixedDeclarationJson);
        var second = store.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-1", FixedDeclarationJson);
        Assert.Equal(first.TaskInstanceId, second.TaskInstanceId);
        Assert.Equal(first.Declaration, second.Declaration);
        Assert.Equal(first.DeclarationFingerprint, second.DeclarationFingerprint);
        Assert.Single(RuntimeIdentityStoreTests.ReadAllLinesShared(file).Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    [Fact]
    public void A6_1_Declaration_IsPersistedCanonicalWithFingerprint()
    {
        using var store = new TaskInstanceStore(TempFile("ti.jsonl"));
        var record = store.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-2", FixedDeclarationJson);
        Assert.Equal(FixedDeclarationFingerprint, record.DeclarationFingerprint);
        // 持久化的 declaration 是 canonical 形式（键递归排序、无缩进），语义原样。
        using var doc = JsonDocument.Parse(record.Declaration!);
        Assert.Equal("task-declaration.v1", doc.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("task/android-settings/toggle-wifi-reuse", doc.RootElement.GetProperty("taskRef").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("scenarios").GetArrayLength());
        // canonical：fixtures 对象内键排序 digest < id。
        Assert.Contains("\"digest\":null,\"id\":\"fixture/android-settings/settings-root\"", record.Declaration);
    }

    [Fact]
    public void A6_2_SameKeyDifferentDeclaration_ThrowsInputConflictWithoutAppending()
    {
        var file = TempFile("ti.jsonl");
        using var store = new TaskInstanceStore(file);
        var original = store.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-3", FixedDeclarationJson);
        var linesBefore = RuntimeIdentityStoreTests.ReadAllLinesShared(file).Length;

        var conflict = Assert.Throws<TaskInstanceInputConflictException>(
            () => store.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-3", DifferentDeclarationJson));
        Assert.Equal(original.DeclarationFingerprint, conflict.ExistingFingerprint);
        Assert.NotEqual(original.DeclarationFingerprint, conflict.IncomingFingerprint);

        // 原记录不变、不覆盖、不追加。
        Assert.Equal(linesBefore, RuntimeIdentityStoreTests.ReadAllLinesShared(file).Length);
        var after = store.Get(original.TaskInstanceId);
        Assert.Equal(original.Declaration, after!.Declaration);
        Assert.Equal("task/android-settings/toggle-wifi-reuse", JsonDocument.Parse(after.Declaration!).RootElement.GetProperty("taskRef").GetString());
    }

    [Fact]
    public void A6_3_Restart_KeepsIdentityAndDeclarationPinned()
    {
        var file = TempFile("ti.jsonl");
        string id;
        using (var store = new TaskInstanceStore(file))
        {
            var record = store.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-4", FixedDeclarationJson);
            id = record.TaskInstanceId;
        }
        using (var restored = new TaskInstanceStore(file))
        {
            var record = restored.Get(id);
            Assert.NotNull(record);
            Assert.Equal("task/android-settings/toggle-wifi-reuse", record!.TaskRef);
            Assert.Equal("idem-decl-4", record.IdempotencyKey);
            Assert.Equal(FixedDeclarationFingerprint, record.DeclarationFingerprint);
            Assert.Equal("task-declaration.v1", JsonDocument.Parse(record.Declaration!).RootElement.GetProperty("schemaVersion").GetString());
            // 重启后同 key 同声明仍复用原实例。
            var again = restored.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-4", FixedDeclarationJson);
            Assert.Equal(id, again.TaskInstanceId);
            // 重启后同 key 异声明仍显式拒绝。
            Assert.Throws<TaskInstanceInputConflictException>(
                () => restored.Create("task/android-settings/toggle-wifi-reuse", "idem-decl-4", DifferentDeclarationJson));
        }
    }

    [Fact]
    public void NullDeclaration_RetainsLegacyIdempotentSemantics()
    {
        using var store = new TaskInstanceStore(TempFile("ti.jsonl"));
        var a = store.Create("settings-coverage", "idem-legacy-1");
        var b = store.Create("settings-coverage", "idem-legacy-1");
        Assert.Equal(a.TaskInstanceId, b.TaskInstanceId);
        Assert.Null(a.Declaration);
        Assert.Null(a.DeclarationFingerprint);
    }
}
