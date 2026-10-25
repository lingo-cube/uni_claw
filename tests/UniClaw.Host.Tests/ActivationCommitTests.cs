using UniClaw.Host.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

public sealed class ActivationCommitTests(ITestOutputHelper output)
{
    [Fact]
    public void ReserveAfterDispose_DoesNotPublishIdentity()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "activation.jsonl");
        var store = new ActivationRecordStore(file);
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);
        var key = new ActivationKey("view-reserve", 1);

        ActivationRecordStore.Reservation? returned = null;
        var exception = Record.Exception(() =>
            returned = store.Reserve(key, "ti-reserve", "ps-reserve", "launch-reserve", "idem-reserve", "corr-reserve"));
        var visible = store.Find(key);
        using var reopened = new ActivationRecordStore(file);
        var recovered = reopened.Find(key);
        output.WriteLine($"Reserve returned={returned?.Record.ActivationKey ?? "<none>"}; in-memory={visible}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(returned);
        Assert.Null(visible);
        Assert.Null(recovered);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
    }

    [Fact]
    public void FinalizeAfterDispose_DoesNotPublishUpdate()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "activation.jsonl");
        var store = new ActivationRecordStore(file);
        var key = new ActivationKey("view-finalize", 1);
        var original = store.Reserve(key, "ti-finalize", "ps-finalize", "launch-finalize", "idem-finalize", "corr-finalize").Record;
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);

        ActivationRecord? returned = null;
        var exception = Record.Exception(() => returned = store.Finalize(key, "run-finalize"));
        var byKey = store.Find(key);
        var byLaunch = store.FindByLaunchId(original.LaunchId);
        var byIdempotency = store.FindByIdempotencyKey(original.IdempotencyKey);
        using var reopened = new ActivationRecordStore(file);
        var recovered = reopened.Find(key);
        output.WriteLine($"Finalize returned={returned}; in-memory={byKey}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(returned);
        Assert.Equal(original, byKey);
        Assert.Equal(original, byLaunch);
        Assert.Equal(original, byIdempotency);
        Assert.Equal(original, recovered);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
    }

    [Fact]
    public void FailAfterDispose_DoesNotPublishUpdate()
    {
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "activation.jsonl");
        var store = new ActivationRecordStore(file);
        var key = new ActivationKey("view-fail", 1);
        var original = store.Reserve(key, "ti-fail", "ps-fail", "launch-fail", "idem-fail", "corr-fail").Record;
        store.Dispose();
        var bytesBefore = File.ReadAllBytes(file);

        ActivationRecord? returned = null;
        var exception = Record.Exception(() => returned = store.Fail(key, "failed", "closed-store"));
        var byKey = store.Find(key);
        var byLaunch = store.FindByLaunchId(original.LaunchId);
        var byIdempotency = store.FindByIdempotencyKey(original.IdempotencyKey);
        using var reopened = new ActivationRecordStore(file);
        var recovered = reopened.Find(key);
        output.WriteLine($"Fail returned={returned}; in-memory={byKey}; reopened={recovered}");

        Assert.IsType<ObjectDisposedException>(exception);
        Assert.Null(returned);
        Assert.Equal(original, byKey);
        Assert.Equal(original, byLaunch);
        Assert.Equal(original, byIdempotency);
        Assert.Equal(original, recovered);
        Assert.Equal(bytesBefore, File.ReadAllBytes(file));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() => Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"activation-commit-{Guid.NewGuid():N}");

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
