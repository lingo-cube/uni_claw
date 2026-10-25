using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class StorageBarrierFileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("native-storage-barriers-").FullName;
    public void Dispose() => Directory.Delete(_root, true);
    private static ExecutionRegistration Registration => new(null, "effect", "intent", "binding", "tap", "switch", null, "revision", 1, "offline", null);
    private static EffectReceipt Receipt => new("receipt", "intent", "binding", "switch", "revision", 1, DispatchOutcome.DeliveryCompleted, "original", DateTimeOffset.Parse("2026-10-25T00:00:00Z"));
    private static ConsultationKey Key => new("run", "view", "boundary", "correlation");
    private static RunResourceRecord InitialResource => new(1, RunResourcePhase.Initialize, "initialize",
        new("run", "task", "session", "contract", "v1", 1, "policy", 4, 3),
        new(0, 0, 0, 0, [], [], [], [], new Dictionary<string, IReadOnlyList<string>>(), false, false, false));

    [Fact]
    public async Task ActualDriverResultBatchPreservesThreeFramesWithFewerWritesAndFlushes()
    {
        var result = new Dictionary<string, JsonElement>();
        foreach (var mode in new[] { "single", "batch" })
        {
            var directory = Path.Combine(_root, mode); using var probe = Start("write-storage-barrier", directory, mode);
            await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(probe.ExitCode == 0, await probe.StandardError.ReadToEndAsync());
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "measurement.json")));
            result[mode] = document.RootElement.Clone(); Export(mode, directory);
        }
        var single = result["single"]; var batch = result["batch"];
        Assert.Equal(1, single.GetProperty("Calls").GetInt32()); Assert.Equal(1, batch.GetProperty("Calls").GetInt32());
        Assert.Equal(1, batch.GetProperty("Receipts").GetInt32());
        Assert.Equal(3, single.GetProperty("ManagedWriteCalls").GetInt64()); Assert.Equal(2, batch.GetProperty("ManagedWriteCalls").GetInt64());
        Assert.Equal(3, single.GetProperty("ManagedFlushCalls").GetInt64()); Assert.Equal(2, batch.GetProperty("ManagedFlushCalls").GetInt64());
        Assert.Equal(2, batch.GetProperty("PeakBatchRecords").GetInt32());
        var rows = batch.GetProperty("Attempt").GetProperty("Entries"); Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal((int)ExecutionEntryKind.Submission, rows[0].GetProperty("Kind").GetInt32());
        Assert.Equal((int)ExecutionEntryKind.Receipt, rows[1].GetProperty("Kind").GetInt32());
        Assert.Equal((int)ExecutionAttemptStatus.Completed, batch.GetProperty("Attempt").GetProperty("Status").GetInt32());
    }

    [Theory]
    [InlineData("resource", 0)] [InlineData("consultation", 0)] [InlineData("prepare", 0)] [InlineData("result", 1)]
    public async Task ActualDriverCommitBeforeAcknowledgementKillPreservesOriginalsInTwoNewReaders(string kind, int deliveries)
    {
        var directory = Path.Combine(_root, kind); using var writer = Start("write-storage-barrier", directory, kind);
        var error = writer.StandardError.ReadToEndAsync();
        try
        {
            var until = DateTime.UtcNow.AddSeconds(25);
            while (!File.Exists(Path.Combine(directory, "ready.json")) && !writer.HasExited && DateTime.UtcNow < until) await Task.Delay(20);
            Assert.True(File.Exists(Path.Combine(directory, "ready.json")), writer.HasExited ? await error : "readiness timeout");
            using var ready = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "ready.json"))); var status = ready.RootElement;
            Assert.False(status.GetProperty("IsTerminal").GetBoolean()); Assert.True(status.GetProperty("DriverPending").GetBoolean());
            Assert.Equal(deliveries, status.GetProperty("Deliveries").GetInt32());
            Assert.Equal(kind is "resource" or "consultation" ? 0 : 1, status.GetProperty("Consultations").GetInt32());
            Assert.Equal(deliveries, status.GetProperty("PublishedReceipts").GetInt32());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(directory, "kill-observed.json"), JsonSerializer.Serialize(new { writer.Id, writer.ExitCode, UsedKill = true }));
            var files = Directory.GetFiles(directory).Where(f => Path.GetFileName(f) is not ("ready.json" or "expected.json" or "kill-observed.json")).ToDictionary(Path.GetFileName, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            File.WriteAllText(Path.Combine(directory, "source-hashes.json"), JsonSerializer.Serialize(files));
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "expected.json")));
            for (var index = 0; index < 2; index++)
            {
                using var reader = Start("read-storage-barrier", directory, status.GetProperty("RunId").GetString()!);
                await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "reader.json"))); var read = document.RootElement;
                Assert.NotEqual(writer.Id, read.GetProperty("ProcessId").GetInt32()); Assert.True(read.GetProperty("IsReadOnly").GetBoolean()); Assert.False(read.GetProperty("ContinuationSupported").GetBoolean());
                foreach (var property in new[] { "ResourceRows", "Consultation", "Execution" }) Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty(property), read.GetProperty(property)), property);
                File.Copy(Path.Combine(directory, "reader.json"), Path.Combine(directory, $"reader-{index}.json"));
                foreach (var (file, hash) in files) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, file!)))));
            }
            Export(kind, directory);
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
    }

    [Theory]
    [InlineData("resource")] [InlineData("consultation")] [InlineData("execution")]
    public async Task NativePreAcceptanceCancellationDoesNotPoisonAndNormalCloseSettlesWrite(string kind)
    {
        var path = Path.Combine(_root, kind); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        if (kind == "resource")
        {
            var source = new FileRunResourceJournal(path); var row = InitialResource;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.AppendAsync(row, cancelled.Token).AsTask());
            Assert.Empty(source.ReadRecords()); var accepted = source.AppendAsync(row); var closing = source.DisposeAsync(); await accepted; await closing;
            using var reader = new FileRunResourceJournal(path, true); Assert.Single(reader.ReadRecords());
        }
        else if (kind == "consultation")
        {
            var source = new FileConsultationJournal(path);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ReserveAsync(Key, "fp", cancelled.Token).AsTask()); Assert.Null(source.Find(Key));
            var accepted = source.ReserveAsync(Key, "fp"); var closing = source.DisposeAsync(); await accepted; await closing;
            using var reader = new FileConsultationJournal(path); Assert.Equal(ConsultationJournalStatus.Pending, reader.Find(Key)!.Status);
        }
        else
        {
            var source = new FileExecutionJournal(path);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.CommitPrepareAsync(Registration, cancelled.Token).AsTask()); Assert.Equal(0, source.AttemptCount);
            var accepted = source.CommitPrepareAsync(Registration); var closing = source.DisposeAsync(); var commit = await accepted; await closing;
            using var reader = new FileExecutionJournal(path); Assert.Equal(ExecutionCommitOutcome.Success, commit.Outcome); Assert.Single(reader.DiscoverPending());
        }
    }

    [Theory]
    [InlineData("resource")] [InlineData("consultation")] [InlineData("execution")]
    public async Task NativeByteLimitWritesNothingAndLatchesFailure(string kind)
    {
        var path = Path.Combine(_root, kind);
        if (kind == "resource")
        {
            await using var source = new FileRunResourceJournal(path, maxRecordBytes: 32);
            await Assert.ThrowsAsync<InvalidDataException>(() => source.AppendAsync(InitialResource).AsTask());
            await Assert.ThrowsAsync<IOException>(() => source.AppendAsync(InitialResource).AsTask());
            Assert.Empty(source.ReadRecords());
        }
        else if (kind == "consultation")
        {
            await using var source = new FileConsultationJournal(path, maxRecordBytes: 32);
            await Assert.ThrowsAsync<InvalidDataException>(() => source.ReserveAsync(Key, "fp").AsTask()); Assert.Null(source.Find(Key));
            await Assert.ThrowsAsync<IOException>(() => source.ReserveAsync(Key, "fp").AsTask());
        }
        else
        {
            await using var source = new FileExecutionJournal(path, maxBatchBytes: 32);
            Assert.Equal(ExecutionCommitOutcome.Failure, (await source.CommitPrepareAsync(Registration)).Outcome);
            Assert.Equal(ExecutionCommitOutcome.Failure, (await source.CommitPrepareAsync(Registration)).Outcome); Assert.Equal(0, source.AttemptCount);
        }
        Assert.Empty(File.ReadAllBytes(path));
    }

    [Fact]
    public async Task NativeExecutionAsyncFeedbackLinksAndOriginalLengthFrameReplayStayCompatible()
    {
        var path = Path.Combine(_root, "linked"); string id, retry; ExecutionLink compensation;
        await using (var source = new FileExecutionJournal(path))
        {
            id = (await source.CommitPrepareAsync(Registration)).AttemptId!;
            await source.AppendResultAsync(id, "actual submission", Receipt); await source.AppendLateFeedbackAsync(id, new("feedback", "late"));
            await source.AppendLateFeedbackAsync(null, new("orphan", "unassociated"));
            retry = await source.LinkRetryAsync(id); compensation = await source.LinkCompensationAsync(id);
        }
        using var reader = new FileExecutionJournal(path);
        Assert.Equal(ExecutionAttemptStatus.Completed, reader.GetAttempt(id)!.Status); Assert.Equal(3, reader.GetAttempt(id)!.Entries.Count);
        Assert.Equal(id, reader.GetAttempt(retry)!.Registration.RetryOf); Assert.Equal("effect", reader.GetAttempt(compensation.NewAttemptId)!.Registration.Compensates);
        Assert.Equal("orphan", Assert.Single(reader.UnassociatedFeedback).FeedbackId); Assert.Equal(2, reader.DiscoverPending().Count);
    }

    [Fact]
    public async Task CombinedResultByteQuotaIsCheckedBeforeEitherOriginalFrameIsWritten()
    {
        var note = new string('s', 2000); var receipt = Receipt with { Report = new string('r', 2000) };
        var fixture = Path.Combine(_root, "size-fixture"); long before, submission, receiptSize;
        await using (var source = new FileExecutionJournal(fixture))
        {
            var id = (await source.CommitPrepareAsync(Registration)).AttemptId!; before = new FileInfo(fixture).Length;
            await source.AppendSubmissionAsync(id, note); submission = new FileInfo(fixture).Length - before;
            await source.AppendReceiptAsync(id, receipt); receiptSize = new FileInfo(fixture).Length - before - submission;
        }
        var limit = (int)Math.Max(before, Math.Max(submission, receiptSize)); Assert.True(limit < submission + receiptSize);
        var path = Path.Combine(_root, "limited-pair"); await using var limited = new FileExecutionJournal(path, limit);
        var original = (await limited.CommitPrepareAsync(Registration)).AttemptId!; var bytes = File.ReadAllBytes(path);
        await Assert.ThrowsAsync<IOException>(() => limited.AppendResultAsync(original, note, receipt).AsTask());
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(1, limited.ManagedWriteCalls);
        Assert.Equal(ExecutionAttemptStatus.CommittedPending, limited.GetAttempt(original)!.Status);
        Assert.Equal(ExecutionCommitOutcome.Failure, (await limited.CommitPrepareAsync(Registration)).Outcome);
    }
    private static Process Start(string mode, string directory, string argument)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "tools/RunRecoveryProbe/bin", configuration, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        start.ArgumentList.Add(mode); start.ArgumentList.Add(directory); start.ArgumentList.Add(argument); return Process.Start(start)!;
    }
    private static void Export(string label, string directory)
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_STORAGE_BARRIER_EXPORT") is not { Length: > 0 } output) return;
        var target = Path.Combine(output, label); Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
    }
}
