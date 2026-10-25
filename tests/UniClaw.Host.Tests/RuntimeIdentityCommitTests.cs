using UniClaw.Host.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

public sealed class RuntimeIdentityCommitTests(ITestOutputHelper output)
{
    [Fact]
    public void TaskInstance_CreateAfterDispose_DoesNotPublishIdentity()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "task-instances.jsonl");
        var store = new TaskInstanceStore(file);
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);

        TaskInstanceRecord? returned = null;
        var exception = Record.Exception(() =>
            returned = store.Create("settings-coverage", "closed-create"));
        using var reopened = new TaskInstanceStore(file);
        var visible = returned is null ? null : store.Get(returned.TaskInstanceId);
        var recovered = returned is null ? null : reopened.Get(returned.TaskInstanceId);
        output.WriteLine($"Create returned ID={returned?.TaskInstanceId ?? "<none>"}; in-memory={visible}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(visible);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
        Assert.Null(recovered);
    }

    [Fact]
    public void TaskInstance_BindAfterDispose_DoesNotPublishBinding()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "task-instances.jsonl");
        var store = new TaskInstanceStore(file);
        var created = store.Create("settings-coverage", "closed-bind");
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);

        TaskInstanceRecord? returned = null;
        var exception = Record.Exception(() =>
            returned = store.BindSession(created.TaskInstanceId, "ps-after-close"));
        var visible = store.Get(created.TaskInstanceId);
        using var reopened = new TaskInstanceStore(file);
        var recovered = reopened.Get(created.TaskInstanceId);
        output.WriteLine($"Bind returned session={returned?.ProductSessionId ?? "<none>"}; in-memory={visible}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(visible!.ProductSessionId);
        Assert.Null(recovered!.ProductSessionId);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
    }

    [Fact]
    public void ProductSession_CreateAfterDispose_DoesNotPublishIdentity()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "product-sessions.jsonl");
        var store = new ProductSessionStore(file);
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);

        ProductSessionRecord? returned = null;
        var exception = Record.Exception(() =>
            returned = store.Create("ti-after-close"));
        using var reopened = new ProductSessionStore(file);
        var visible = returned is null ? null : store.Get(returned.ProductSessionId);
        var recovered = returned is null ? null : reopened.Get(returned.ProductSessionId);
        output.WriteLine($"Create returned ID={returned?.ProductSessionId ?? "<none>"}; in-memory={visible}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(visible);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
        Assert.Null(recovered);
    }

    [Fact]
    public void ProductSession_BindAfterDispose_DoesNotPublishBinding()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "product-sessions.jsonl");
        var store = new ProductSessionStore(file);
        var created = store.Create("ti-before-close");
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);

        ProductSessionRecord? returned = null;
        var exception = Record.Exception(() =>
            returned = store.BindRun(created.ProductSessionId, "run-after-close"));
        var visible = store.Get(created.ProductSessionId);
        using var reopened = new ProductSessionStore(file);
        var recovered = reopened.Get(created.ProductSessionId);
        output.WriteLine($"Bind returned run={returned?.RunId ?? "<none>"}; in-memory={visible}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(visible!.RunId);
        Assert.Null(recovered!.RunId);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() => Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"runtime-identity-commit-{Guid.NewGuid():N}");

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
