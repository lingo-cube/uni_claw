using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class ActivationRecordStoreTests
{
    [Fact]
    public void ReserveFinalizeAndReload_ReplaysOneRun()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uniclaw-activation-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "activation.jsonl");
        try
        {
            var key = new ActivationKey("view-1", 3);
            using (var store = new ActivationRecordStore(path))
            {
                var first = store.Reserve(key, "ti-1", "ps-1", "launch-1", "idem-1", "corr-1");
                Assert.True(first.Created);
                Assert.Equal("reserved", first.Record.Status);
                var duplicate = store.Reserve(key, "ti-1", "ps-1", "launch-1", "idem-1", "corr-1");
                Assert.False(duplicate.Created);
                var finalized = store.Finalize(key, "run-1");
                Assert.Equal("finalized", finalized.Status);
                Assert.Equal("run-1", finalized.RunId);
            }

            using var restored = new ActivationRecordStore(path);
            var replay = restored.Find(key);
            Assert.NotNull(replay);
            Assert.Equal("finalized", replay!.Status);
            Assert.Equal("run-1", replay.RunId);
            Assert.False(restored.Reserve(key, "ti-1", "ps-1", "launch-1", "idem-1", "corr-1").Created);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LaunchOrIdempotencyConflict_IsRejectedWithoutSecondRecord()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uniclaw-activation-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "activation.jsonl");
        try
        {
            using var store = new ActivationRecordStore(path);
            var first = store.Reserve(new ActivationKey("view-1", 1), "ti-1", "ps-1", "launch-1", "idem-1", "corr-1");
            Assert.False(first.Conflict);
            var conflict = store.Reserve(new ActivationKey("view-2", 1), "ti-1", "ps-1", "launch-1", "idem-2", "corr-2");
            Assert.True(conflict.Conflict);
            Assert.Equal("view-1", conflict.Record.AcceptedContractViewRef);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
