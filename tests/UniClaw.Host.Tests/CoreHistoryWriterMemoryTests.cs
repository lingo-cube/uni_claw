using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

[Collection("WorldJournalMemory")]
public sealed class CoreHistoryWriterMemoryTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("core-writer-memory-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ControlWriterDoesNotKeepASecondFullHistoryInManagedMemory()
    {
        var path = Path.Combine(_root, "control-writer-memory-" + Guid.NewGuid().ToString("N"));
        var payload = JsonSerializer.SerializeToElement(new { retainedPayload = new string('x', 256 * 1024) });
        var empty = JsonSerializer.SerializeToElement<object?>(null);
        try
        {
            using var writer = new FileControlTraversalJournal(path);
            // Warm serializer metadata before measuring retained managed memory.
            writer.Append(new("uniclaw.control.traversal.history.v1", 1, "fixture", empty, empty, empty));
            var before = GC.GetTotalMemory(true);
            for (var sequence = 2; sequence <= 97; sequence++)
                writer.Append(new("uniclaw.control.traversal.history.v1", sequence, "fixture", payload, empty, empty));

            var retained = GC.GetTotalMemory(true) - before;
            output.WriteLine($"Control writer retained {retained} bytes for 96 already-persisted 256 KiB payloads.");
            GC.KeepAlive(writer);
            Assert.True(retained < 24L * 1024 * 1024,
                $"Control writer retained {retained} bytes for 96 already-persisted payloads");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".head");
        }
    }

    [Fact]
    public void RuntimePlanWriterDoesNotKeepASecondFullHistoryInManagedMemory()
    {
        var path = Path.Combine(_root, "plan-writer-memory-" + Guid.NewGuid().ToString("N"));
        var payload = JsonSerializer.SerializeToElement(new { retainedPayload = new string('x', 256 * 1024) });
        try
        {
            using var writer = new FileRuntimePlanJournal(path);
            // Warm serializer metadata before measuring retained managed memory.
            writer.Append(new("uniclaw.runtime.plan.history.v1", 1, "fixture", new Dictionary<string, int>()));
            var before = GC.GetTotalMemory(true);
            for (var sequence = 2; sequence <= 97; sequence++)
                writer.Append(new("uniclaw.runtime.plan.history.v1", sequence, "fixture",
                    new Dictionary<string, int>(), Graph: payload));

            var retained = GC.GetTotalMemory(true) - before;
            output.WriteLine($"RuntimePlan writer retained {retained} bytes for 96 already-persisted 256 KiB payloads.");
            GC.KeepAlive(writer);
            Assert.True(retained < 24L * 1024 * 1024,
                $"RuntimePlan writer retained {retained} bytes for 96 already-persisted payloads");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".head");
        }
    }

    [Fact]
    public void ControlWriterReadReturnsIndependentSnapshotAndContinuesAtNextSequence()
    {
        var path = Path.Combine(_root, "control-writer-read-" + Guid.NewGuid().ToString("N"));
        var firstPayload = JsonSerializer.SerializeToElement(new { value = "first-control-row" });
        var secondPayload = JsonSerializer.SerializeToElement(new { value = "second-control-row" });
        var empty = JsonSerializer.SerializeToElement<object?>(null);
        try
        {
            using var writer = new FileControlTraversalJournal(path);
            writer.Append(new("uniclaw.control.traversal.history.v1", 1, "fixture", firstPayload, empty, empty));
            var firstRead = writer.ReadRecords();

            writer.Append(new("uniclaw.control.traversal.history.v1", 2, "fixture", secondPayload, empty, empty));

            Assert.Single(firstRead);
            Assert.Equal(1, firstRead[0].Sequence);
            Assert.True(JsonElement.DeepEquals(firstPayload, firstRead[0].Input));
            var completeRead = writer.ReadRecords();
            Assert.Equal(new[] { 1, 2 }, completeRead.Select(record => record.Sequence));
            Assert.True(JsonElement.DeepEquals(secondPayload, completeRead[1].Input));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".head");
        }
    }

    [Fact]
    public void RuntimePlanWriterReadReturnsIndependentSnapshotAndContinuesAtNextSequence()
    {
        var path = Path.Combine(_root, "plan-writer-read-" + Guid.NewGuid().ToString("N"));
        var secondPayload = JsonSerializer.SerializeToElement(new { value = "second-plan-row" });
        try
        {
            using var writer = new FileRuntimePlanJournal(path);
            writer.Append(new("uniclaw.runtime.plan.history.v1", 1, "fixture", new Dictionary<string, int>()));
            var firstRead = writer.ReadRecords();

            writer.Append(new("uniclaw.runtime.plan.history.v1", 2, "fixture", new Dictionary<string, int>(), Graph: secondPayload));

            Assert.Single(firstRead);
            Assert.Equal(1, firstRead[0].Sequence);
            Assert.Null(firstRead[0].Graph);
            var completeRead = writer.ReadRecords();
            Assert.Equal(new[] { 1, 2 }, completeRead.Select(record => record.Sequence));
            Assert.True(JsonElement.DeepEquals(secondPayload, completeRead[1].Graph!.Value));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".head");
        }
    }

    [Fact]
    public void ControlWriterReadRejectsAValidReplacementWithDifferentRecordingId()
    {
        var path = Path.Combine(_root, "control-writer-endpoint-" + Guid.NewGuid().ToString("N"));
        var replacementPath = path + ".replacement";
        var empty = JsonSerializer.SerializeToElement<object?>(null);
        try
        {
            using var writer = new FileControlTraversalJournal(path);
            writer.Append(new("uniclaw.control.traversal.history.v1", 1, "fixture", empty, empty, empty));
            using (var replacement = new FileControlTraversalJournal(replacementPath))
                replacement.Append(new("uniclaw.control.traversal.history.v1", 1, "fixture", empty, empty, empty));

            writer.Dispose();
            File.Delete(path);
            File.Move(replacementPath, path);
            File.Delete(path + ".head");
            File.Move(replacementPath + ".head", path + ".head");

            Assert.Throws<InvalidDataException>(() => writer.ReadRecords());
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".head");
            File.Delete(replacementPath);
            File.Delete(replacementPath + ".head");
        }
    }

    [Fact]
    public void RuntimePlanWriterReadRejectsAValidReplacementWithDifferentRecordingId()
    {
        var path = Path.Combine(_root, "plan-writer-endpoint-" + Guid.NewGuid().ToString("N"));
        var replacementPath = path + ".replacement";
        try
        {
            using var writer = new FileRuntimePlanJournal(path);
            writer.Append(new("uniclaw.runtime.plan.history.v1", 1, "fixture", new Dictionary<string, int>()));
            using (var replacement = new FileRuntimePlanJournal(replacementPath))
                replacement.Append(new("uniclaw.runtime.plan.history.v1", 1, "fixture", new Dictionary<string, int>()));

            writer.Dispose();
            File.Delete(path);
            File.Move(replacementPath, path);
            File.Delete(path + ".head");
            File.Move(replacementPath + ".head", path + ".head");

            Assert.Throws<InvalidDataException>(() => writer.ReadRecords());
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".head");
            File.Delete(replacementPath);
            File.Delete(replacementPath + ".head");
        }
    }
}
