using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Host.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class StorageClosureFileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("storage-closure-").FullName;
    public void Dispose() => Directory.Delete(_root, true);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeFactoriesReturnOnlyAfterOriginalEmptyMarkerAndHeadAreReadable(bool world)
    {
        var path = Path.Combine(_root, "source"); string id;
        if (world)
        {
            await using var source = await FileWorldJournal.CreateAsync(path); id = source.RecordingId; Assert.Empty(source.ReadRecords());
            using var reader = new FileWorldJournal(path, true); Assert.Equal(id, reader.RecordingId); Assert.Empty(reader.ReadRecords());
        }
        else
        {
            await using var source = await FileEvidenceJournal.CreateAsync(path); id = source.RecordingId; Assert.Empty(source.ReadRecords());
            using var reader = new FileEvidenceJournal(path, true); Assert.Equal(id, reader.RecordingId); Assert.Empty(reader.ReadRecords());
        }
        Assert.Single(File.ReadAllLines(path)); Assert.Single(File.ReadAllLines(path + ".head"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PreAcceptanceFactoryCancellationCreatesNoSource(bool world)
    {
        var path = Path.Combine(_root, "cancelled"); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        if (world) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileWorldJournal.CreateAsync(path, cancellationToken: cancel.Token).AsTask());
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FileEvidenceJournal.CreateAsync(path, cancellationToken: cancel.Token).AsTask());
        Assert.False(File.Exists(path)); Assert.False(File.Exists(path + ".head"));
    }
    [Fact]
    public async Task OneActualDriverMeasuresNineNativeSourcesAndOriginalFrames()
    {
        var directory = Path.Combine(_root, "performance"); using var probe = Start("write-storage-closure", directory, "performance");
        await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25)); Assert.True(probe.ExitCode == 0, await probe.StandardError.ReadToEndAsync());
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "performance.json"))); var read = document.RootElement;
        Assert.Equal(9, read.GetProperty("Statistics").EnumerateObject().Count()); Assert.Equal(1, read.GetProperty("DeliveryCalls").GetInt32());
        Assert.Equal(1, read.GetProperty("Receipts").GetInt32());
        foreach (var domain in read.GetProperty("Statistics").EnumerateObject())
        {
            var records = read.GetProperty("SourceRows").GetProperty(domain.Name).GetInt32(); var counts = domain.Value;
            var marker = domain.Name is "world" or "evidence" ? 1 : 0;
            Assert.Equal(domain.Name == "execution" ? records - 1 : records + marker, counts.GetProperty("FrameWrites").GetInt64());
            Assert.Equal(counts.GetProperty("FrameWrites").GetInt64(), counts.GetProperty("FrameFlushes").GetInt64());
            Assert.Equal(domain.Name is "execution" or "consultation" or "archive" ? 0 : counts.GetProperty("FrameWrites").GetInt64(), counts.GetProperty("HeadWrites").GetInt64());
            Assert.Equal(counts.GetProperty("HeadWrites").GetInt64(), counts.GetProperty("HeadFlushes").GetInt64());
            Assert.True(counts.GetProperty("PeakBatchBytes").GetInt32() <= 16 * 1024 * 1024);
        }
        Assert.Equal(0, read.GetProperty("AdmissionQueueRecords").GetInt32());
        Assert.True(read.GetProperty("DriverElapsedMilliseconds").GetDouble() >= 0);
        // Quota on a separate fixture file; never substitute it for original recovery evidence.
        var record = new FileRunRecoveryArchive(directory).Read(); var fixture = Path.Combine(_root, "quota"); Directory.CreateDirectory(fixture);
        var limited = new FileRunRecoveryArchive(fixture, maxArchiveBytes: 32);
        await Assert.ThrowsAsync<InvalidDataException>(() => limited.InitializeAsync(record).AsTask());
        await Assert.ThrowsAsync<IOException>(() => limited.InitializeAsync(record).AsTask()); Assert.False(File.Exists(Path.Combine(fixture, "run-identity.json")));
        Export("performance", directory);
    }
    [Fact]
    public async Task ActualArchiveCommitBeforeOwnerAcknowledgementKillHasTwoOriginalReadOnlyReaders()
    {
        var directory = Path.Combine(_root, "archive"); using var writer = Start("write-storage-closure", directory, "archive"); var error = writer.StandardError.ReadToEndAsync();
        try
        {
            var until = DateTime.UtcNow.AddSeconds(25);
            while (!File.Exists(Path.Combine(directory, "ready.json")) && !writer.HasExited && DateTime.UtcNow < until) await Task.Delay(20);
            Assert.True(File.Exists(Path.Combine(directory, "ready.json")), writer.HasExited ? await error : "readiness timeout");
            using var ready = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "ready.json"))); var status = ready.RootElement;
            Assert.True(status.GetProperty("DriverPending").GetBoolean()); Assert.False(status.GetProperty("IsTerminal").GetBoolean());
            Assert.False(status.GetProperty("ResourceBound").GetBoolean()); Assert.Equal(0, status.GetProperty("Consultations").GetInt32());
            Assert.Equal(0, status.GetProperty("Deliveries").GetInt32()); Assert.Equal(0, status.GetProperty("Observations").GetInt32());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(directory, "kill-observed.json"), JsonSerializer.Serialize(new { writer.Id, writer.ExitCode, UsedKill = true }));
            var hashes = Directory.GetFiles(directory).Where(f => Path.GetFileName(f) is not ("ready.json" or "expected.json" or "kill-observed.json")).ToDictionary(Path.GetFileName, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            File.WriteAllText(Path.Combine(directory, "source-hashes.json"), JsonSerializer.Serialize(hashes));
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "expected.json")));
            for (var index = 0; index < 2; index++)
            {
                using var reader = Start("read-storage-closure", directory, status.GetProperty("RunId").GetString()!);
                await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "reader.json"))); var read = result.RootElement;
                Assert.True(read.GetProperty("IsReadOnly").GetBoolean()); Assert.False(read.GetProperty("ContinuationSupported").GetBoolean());
                Assert.True(read.GetProperty("HistoriesConsistent").GetBoolean()); Assert.NotEqual(writer.Id, read.GetProperty("ProcessId").GetInt32());
                Assert.True(JsonElement.DeepEquals(expected.RootElement, read.GetProperty("Original")));
                File.Copy(Path.Combine(directory, "reader.json"), Path.Combine(directory, $"reader-{index}.json"));
                foreach (var (file, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, file!)))));
            }
            Export("archive", directory);
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
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
        if (Environment.GetEnvironmentVariable("UNICLAW_STORAGE_CLOSURE_EXPORT") is not { Length: > 0 } output) return;
        var target = Path.Combine(output, label); Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
    }

    private sealed class PausedContext : SynchronizationContext
    {
        private readonly object _sync = new();
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
        private bool _released;
        public override SynchronizationContext CreateCopy() => this;
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_sync)
            {
                if (!_released) { _queue.Enqueue((callback, state)); return; }
            }
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
        public void Release()
        {
            lock (_sync)
            {
                _released = true;
                while (_queue.TryDequeue(out var item)) ThreadPool.QueueUserWorkItem(_ => item.Callback(item.State));
            }
        }
    }
    [Fact]
    public async Task SynchronousHostBridgeDoesNotRequireTheCallersMessageLoopToRun()
    {
        var context = new PausedContext(); var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = Path.Combine(_root, "context");
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                var source = new RunResourceAccountingTests.Source();
                UniClaw.Host.SettingsCoverage.SettingsCoverageRunner.Run(directory, new("synthetic-device",
                    new("synthetic-device", "unused", "/unused", "/unused/python"),
                    new("1", new("closure", "offline", false, false), new(4, 6, 0, 3, 1), new(true, "configured", 0, 0, false, 0), ["Unreached row"],
                        new(true, true, true, true), "synthetic:resources", "unused", null),
                    c => new UniClaw.Kernel.Runtime.AgentDecision.Blocked(c.DecisionId, "done"),
                    ViewportWidth: 100, ViewportHeight: 100, FeedNext: source.Next, CurrentCaptureId: () => source.CaptureId,
                    EffectDriver: source, UseCoverageDirector: false));
                done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
        }) { IsBackground = true };
        thread.Start();
        try
        {
            var first = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(done.Task, first); await done.Task;
        }
        finally { context.Release(); await done.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
    }

    [Fact]
    public async Task HostRunOnceBridgeAwaitsInitializationFailureArtifactAndCloseWithoutMessageLoop()
    {
        var context = new PausedContext(); var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = Path.Combine(_root, "host-context"); var consultations = 0;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                // Missing DeviceId stops before creating delivery or pulling perception;
                // the existing public Host still initializes real histories and writes failure.json.
                var error = Assert.Throws<InvalidOperationException>(() => UniClaw.Host.HostRunner.RunOnce(directory,
                    new(Live: new("no-device", "unused", "/unused", "/unused/python"), ConsultAgent: _ => { consultations++; return null; })));
                Assert.StartsWith("ENVIRONMENT_UNAVAILABLE:", error.Message); done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
        }) { IsBackground = true };
        thread.Start();
        try
        {
            var first = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(3))); Assert.Same(done.Task, first); await done.Task;
            var run = Assert.Single(Directory.GetDirectories(directory));
            using var artifact = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "failure.json")));
            Assert.StartsWith("ENVIRONMENT_UNAVAILABLE:", artifact.RootElement.GetProperty("message").GetString());
            using var evidence = new FileEvidenceJournal(Path.Combine(run, "evidence.journal.jsonl"), true);
            using var world = new FileWorldJournal(Path.Combine(run, "world.journal.jsonl"), true);
            Assert.Empty(evidence.ReadRecords()); Assert.Equal("initialize", Assert.Single(world.ReadRecords()).Operation);
            Assert.Equal(0, consultations);
        }
        finally { context.Release(); await done.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
    }

}
