using UniClaw.Host.Runtime;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunAsyncStorageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("run-native-async-").FullName;
    public void Dispose() => Directory.Delete(_root, true);
    private static ExecutionContract Contract() => new("native-run-v1", "original progress", new HashSet<string> { "switch" },
        new HashSet<string> { "tap" }, new HashSet<string>(), ["goal"],
        TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract");

    [Fact]
    public async Task NativeAsyncRunCommitsReadBackThroughTheOriginalReadOnlyOwner()
    {
        var path = Path.Combine(_root, "run"); string id; RunHistoryRecord[] rows;
        await using (var writer = new FileRunStateJournal(path))
        {
            Assert.IsAssignableFrom<IAsyncRunStateJournal>(writer); id = writer.RecordingId;
            var run = new RunModel(writer);
            Assert.True((await run.AdmitContractAsync(Contract())).Accepted);
            await run.RecordCycleAsync(); await run.RecordActionAsync();
            Assert.Equal(3, run.HistoryPosition!.Sequence); rows = writer.ReadRecords().ToArray();
        }
        using var reader = new FileRunStateJournal(path, true);
        var restored = RunModel.RestoreReadOnly(reader, rows[0].Identity!, id);
        Assert.True(restored.IsReadOnly); Assert.False(restored.IsTerminal);
        Assert.Equal(1, restored.State!.Progress.Cycles); Assert.Equal(1, restored.State.Progress.Acts);
        Assert.Equal(new[] { RunHistoryTransition.Admitted, RunHistoryTransition.Cycle, RunHistoryTransition.Action }, reader.ReadRecords().Select(r => r.Transition));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restored.RecordCycleAsync().AsTask());
    }

    [Fact]
    public async Task NativePreAcceptanceCancellationWritesNothingAndLeavesTheWriterUsable()
    {
        var path = Path.Combine(_root, "cancel"); await using var writer = new FileRunStateJournal(path);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.AppendAsync(new(1, RunHistoryTransition.Admitted, 0, 0), cancelled.Token).AsTask());
        Assert.Empty(File.ReadAllBytes(path)); Assert.Empty(writer.ReadRecords());
        Assert.True((await new RunModel(writer).AdmitContractAsync(Contract())).Accepted);
        Assert.Single(writer.ReadRecords());
    }

    [Fact]
    public async Task OversizedNativeRecordFailsBeforeWritingAndLatches()
    {
        var path = Path.Combine(_root, "limit"); await using var writer = new FileRunStateJournal(path, maxRecordBytes: 128);
        var run = new RunModel(writer);
        await Assert.ThrowsAnyAsync<IOException>(() => run.AdmitContractAsync(Contract()).AsTask());
        Assert.Empty(File.ReadAllBytes(path)); Assert.False(File.Exists(path + ".head")); Assert.Null(run.View);
        await Assert.ThrowsAsync<IOException>(() => writer.AppendAsync(new(1, RunHistoryTransition.Admitted, 0, 0)).AsTask());
    }

    [Fact]
    public async Task NativeCloseSettlesAnAlreadyAcceptedRecordBeforeReleasingTheFile()
    {
        var path = Path.Combine(_root, "close"); var writer = new FileRunStateJournal(path); var run = new RunModel(writer);
        var pending = run.AdmitContractAsync(Contract() with { ProofCriteria = [new string('x', 256 * 1024)] }).AsTask();
        var closing = writer.DisposeAsync().AsTask();
        Assert.True((await pending).Accepted); await closing;
        using var reader = new FileRunStateJournal(path, true);
        Assert.Equal(run.HistoryPosition!.RecordingId, reader.RecordingId); Assert.Single(reader.ReadRecords());
    }

    [Fact]
    public async Task ActualDriverKillAfterRunCommitBeforeOwnerAcknowledgementPreservesOriginalRecords()
    {
        using var writer = StartProbe("write-run-async", _root, "original");
        try
        {
            var marker = Path.Combine(_root, "ready.json"); var deadline = DateTime.UtcNow.AddSeconds(40);
            while (!File.Exists(marker) && !writer.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.True(File.Exists(marker), writer.HasExited ? await writer.StandardError.ReadToEndAsync() : "Run commit marker timeout");
            using var readyDocument = JsonDocument.Parse(File.ReadAllText(marker)); var ready = readyDocument.RootElement;
            Assert.True(ready.GetProperty("DriverPending").GetBoolean()); Assert.False(ready.GetProperty("IsTerminal").GetBoolean());
            Assert.Equal(0, ready.GetProperty("Handlers").GetInt32()); Assert.Equal(0, ready.GetProperty("PublishedHandlers").GetInt32());
            Assert.Equal(1, ready.GetProperty("Consultations").GetInt32()); Assert.Equal(1, ready.GetProperty("PublishedOperations").GetInt32());
            Assert.Equal(ready.GetProperty("SourceRows").GetInt32() - 1, ready.GetProperty("OwnerPosition").GetProperty("Sequence").GetInt32());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(_root, "kill-observed.json"), JsonSerializer.Serialize(new { writer.Id, writer.HasExited, writer.ExitCode }));
            var hashes = Directory.GetFiles(_root).ToDictionary(file => Path.GetFileName(file)!, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            File.WriteAllText(Path.Combine(_root, "source-hashes.json"), JsonSerializer.Serialize(hashes));
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "expected.json"))); var readerIds = new HashSet<int>();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                using var reader = StartProbe("read-run-async", _root, ready.GetProperty("RunId").GetString()!);
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                    Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                    using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "run-reader.json"))); var read = result.RootElement;
                    var readerId = read.GetProperty("ProcessId").GetInt32(); Assert.NotEqual(writer.Id, readerId); Assert.True(readerIds.Add(readerId));
                    Assert.True(read.GetProperty("IsReadOnly").GetBoolean()); Assert.True(read.GetProperty("TraversalHistoryAvailable").GetBoolean());
                    Assert.False(read.GetProperty("ContinuationSupported").GetBoolean()); Assert.False(read.GetProperty("IsTerminal").GetBoolean());
                    Assert.Equal((int)PlanHandlerCallStatus.Running, read.GetProperty("TraversalHandlers")[0].GetProperty("Status").GetInt32());
                    Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("Rows"), read.GetProperty("Records")));
                    foreach (var (file, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_root, file)))));
                    File.Copy(Path.Combine(_root, "run-reader.json"), Path.Combine(_root, "reader-" + repeat + ".json"));
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            if (Environment.GetEnvironmentVariable("UNICLAW_RUN_ASYNC_EXPORT") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                foreach (var file in Directory.GetFiles(_root)) File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
            }
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
    }

    private static Process StartProbe(params string[] arguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("original probe missing");
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", config, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }
}
