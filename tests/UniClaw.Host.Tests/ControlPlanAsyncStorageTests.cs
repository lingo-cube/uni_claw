using UniClaw.Host.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Trace;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class ControlPlanAsyncStorageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("control-plan-native-async-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    private static ExecutionContract Contract(string criterion = "goal") => new("native-group-v1", "original histories", new HashSet<string> { "live.frame" },
        new HashSet<string> { "tap" }, new HashSet<string>(), [criterion], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract");

    private static (UniKernel Kernel, KernelRunDriver Driver) Compose(FileRunStateJournal runSource,
        FileControlTraversalJournal controlSource, FileRuntimePlanJournal planSource, RunModel? run = null)
    {
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "live.frame" }), DisabledRunTrace.Instance,
            run ?? new RunModel(runSource), new ControlLoop(policy, controlSource));
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            RuntimePlanJournal = planSource,
            NextInput = _ => new RunDriverInput.Observation([new(new("live.frame", "original"), IngressKind.Observation,
                ObservationContext.External, new("offline", DateTimeOffset.Parse("2026-10-25T00:00:00Z"), "original", ["offline"]))]),
            ConsultAgent = context => new AgentDecision.Blocked(context.DecisionId, "done"),
        });
        return (kernel, driver);
    }

    [Fact]
    public async Task BothNativeFilesPreserveOriginalDomainHistoriesAndSourceIdentities()
    {
        var runPath = Path.Combine(_root, "run"); var controlPath = Path.Combine(_root, "control"); var planPath = Path.Combine(_root, "plan");
        string runId, controlId, planId;
        await using (var runSource = new FileRunStateJournal(runPath))
        await using (var controlSource = new FileControlTraversalJournal(controlPath))
        await using (var planSource = new FileRuntimePlanJournal(planPath))
        {
            Assert.IsAssignableFrom<IAsyncControlTraversalJournal>(controlSource); Assert.IsAssignableFrom<IAsyncRuntimePlanJournal>(planSource);
            runId = runSource.RecordingId; controlId = controlSource.RecordingId; planId = planSource.RecordingId;
            var (kernel, driver) = Compose(runSource, controlSource, planSource);
            Assert.True((await kernel.AdmitContractAsync(Contract())).Accepted);
            Assert.True(driver.Activate().Accepted); await driver.DriveAsync();
        }
        using var runReader = new FileRunStateJournal(runPath, true); var rows = runReader.ReadRecords();
        var original = RunModel.RestoreReadOnly(runReader, rows[0].Identity!, runId);
        using var controlReader = new FileControlTraversalJournal(controlPath, true);
        using var planReader = new FileRuntimePlanJournal(planPath, true);
        var control = ControlTraversalHistoryRead.ReadOriginal(controlReader, controlId, original);
        var plan = RuntimePlanHistoryRead.ReadOriginal(planReader, planId, original);
        Assert.Equal(runId, control.RunRecordingId); Assert.Equal(runId, plan.RunRecordingId);
        Assert.Equal("initialize", Assert.Single(control.Records).Operation); Assert.Equal("initialize", Assert.Single(plan.Records).Operation);
        Assert.False(control.ContinuationSupported); Assert.False(plan.ContinuationSupported);
    }

    [Fact]
    public async Task RealConfigureAndStartBatchPreservesFramesAndReducesManagedWrites()
    {
        await using (var source = new FileControlTraversalJournal(Path.Combine(_root, "capability")))
            Assert.True(source.CanAppendBatch);
        var measurements = new Dictionary<string, JsonElement>();
        foreach (var mode in new[] { "single", "batch" })
        {
            var directory = Path.Combine(_root, mode);
            using var process = StartProbe("write-control-plan-async", directory, mode);
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync());
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "measurement.json")));
                measurements.Add(mode, document.RootElement.Clone());
            }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
        }
        var baseline = measurements["single"]; var batch = measurements["batch"];
        Assert.True(JsonElement.DeepEquals(baseline.GetProperty("ControlOperations"), batch.GetProperty("ControlOperations")));
        Assert.Equal(baseline.GetProperty("ControlRows").GetInt32(), batch.GetProperty("ControlRows").GetInt32());
        var before = baseline.GetProperty("ControlStatistics"); var after = batch.GetProperty("ControlStatistics");
        foreach (var count in new[] { "FrameWrites", "FrameFlushes", "HeadWrites", "HeadFlushes" })
            Assert.Equal(before.GetProperty(count).GetInt64() - 1, after.GetProperty(count).GetInt64());
        Assert.Equal(1, before.GetProperty("PeakBatchRecords").GetInt32()); Assert.Equal(2, after.GetProperty("PeakBatchRecords").GetInt32());
        Assert.Equal(1, baseline.GetProperty("CompletedOperations").GetInt32()); Assert.Equal(1, batch.GetProperty("CompletedOperations").GetInt32());
        // Alternate files below measure physical bounds only, never original recovery evidence.
        var originalPath = Path.Combine(_root, "single", "control-traversal.journal.jsonl");
        using var original = new FileControlTraversalJournal(originalPath, true); var rows = original.ReadRecords().Take(3).ToArray();
        var lengths = File.ReadAllLines(originalPath).Take(3).Select(line => Encoding.UTF8.GetByteCount(line + "\n")).ToArray();
        var limit = lengths.Max(); Assert.True(limit < lengths[1] + lengths[2]);
        var limitedPath = Path.Combine(_root, "batch-limit");
        await using var limited = new FileControlTraversalJournal(limitedPath, maxRecordBytes: limit);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => limited.AppendBatchAsync(rows).AsTask());
        Assert.Equal(0, limited.WriteStatistics.FrameWrites);
        await limited.AppendAsync(rows[0]); var bytes = File.ReadAllBytes(limitedPath);
        await Assert.ThrowsAsync<InvalidDataException>(() => limited.AppendBatchAsync(rows[1..]).AsTask());
        Assert.Equal(bytes, File.ReadAllBytes(limitedPath)); Assert.Equal(1, limited.WriteStatistics.FrameWrites);
        await Assert.ThrowsAsync<IOException>(() => limited.AppendAsync(rows[1]).AsTask());
        if (Environment.GetEnvironmentVariable("UNICLAW_CONTROL_PLAN_ASYNC_EXPORT") is { Length: > 0 } output)
        {
            output = Path.Combine(output, "measurement"); Directory.CreateDirectory(output);
            foreach (var (mode, result) in measurements) File.WriteAllText(Path.Combine(output, mode + ".json"), result.GetRawText());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativePreAcceptanceCancellationDoesNotWriteOrPoisonEitherFile(bool plan)
    {
        await using var runSource = new FileRunStateJournal(Path.Combine(_root, "run"));
        await using var control = new FileControlTraversalJournal(Path.Combine(_root, "control"));
        await using var plans = new FileRuntimePlanJournal(Path.Combine(_root, "plan"));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var empty = JsonSerializer.SerializeToElement<object?>(null);
        if (plan) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => plans.AppendAsync(new("uniclaw.runtime.plan.history.v1", 1, "initialize", new Dictionary<string, int>()), cancelled.Token).AsTask());
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => control.AppendAsync(new("uniclaw.control.traversal.history.v1", 1, "initialize", empty, empty, empty), cancelled.Token).AsTask());
        Assert.Empty(File.ReadAllBytes(Path.Combine(_root, plan ? "plan" : "control")));
        var (kernel, driver) = Compose(runSource, control, plans);
        Assert.True((await kernel.AdmitContractAsync(Contract())).Accepted); Assert.True(driver.Activate().Accepted); await driver.DriveAsync();
        Assert.Single(control.ReadRecords()); Assert.Single(plans.ReadRecords());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeByteLimitFailsBeforeWritingAndLatchesEachSource(bool plan)
    {
        await using var runSource = new FileRunStateJournal(Path.Combine(_root, "run"));
        await using var control = new FileControlTraversalJournal(Path.Combine(_root, "control"), maxRecordBytes: plan ? 16 * 1024 * 1024 : 128);
        await using var plans = new FileRuntimePlanJournal(Path.Combine(_root, "plan"), maxRecordBytes: plan ? 128 : 16 * 1024 * 1024);
        var (kernel, driver) = Compose(runSource, control, plans);
        if (plan)
        {
            Assert.True((await kernel.AdmitContractAsync(Contract())).Accepted); Assert.True(driver.Activate().Accepted);
            Assert.Contains("runtime-plan-persistence-failed", (await driver.DriveAsync()).Reason); Assert.Null(driver.RuntimePlanHistory);
            await Assert.ThrowsAsync<IOException>(() => plans.AppendAsync(new("uniclaw.runtime.plan.history.v1", 1, "initialize", new Dictionary<string, int>())).AsTask());
        }
        else
        {
            await Assert.ThrowsAnyAsync<IOException>(() => kernel.AdmitContractAsync(Contract()).AsTask()); Assert.False(driver.Activate().Accepted);
            var empty = JsonSerializer.SerializeToElement<object?>(null);
            await Assert.ThrowsAsync<IOException>(() => control.AppendAsync(new("uniclaw.control.traversal.history.v1", 1, "initialize", empty, empty, empty)).AsTask());
        }
        var path = Path.Combine(_root, plan ? "plan" : "control");
        Assert.Empty(File.ReadAllBytes(path)); Assert.False(File.Exists(path + ".head"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCloseSettlesAnAcceptedDomainInitialization(bool plan)
    {
        await using var runSource = new FileRunStateJournal(Path.Combine(_root, "run"));
        await using var control = new FileControlTraversalJournal(Path.Combine(_root, "control"));
        await using var plans = new FileRuntimePlanJournal(Path.Combine(_root, "plan"));
        var run = new RunModel(runSource); var contract = Contract(new string('x', 256 * 1024));
        Assert.True((await run.AdmitContractAsync(contract)).Accepted);
        var (kernel, driver) = Compose(runSource, control, plans, run);
        Task pending;
        if (plan)
        {
            Assert.True((await kernel.AdmitContractAsync(contract)).Accepted); Assert.True(driver.Activate().Accepted);
            pending = driver.DriveAsync();
        }
        else pending = kernel.AdmitContractAsync(contract).AsTask();
        var closing = plan ? plans.DisposeAsync().AsTask() : control.DisposeAsync().AsTask();
        await pending; await closing;
        if (plan) { using var read = new FileRuntimePlanJournal(Path.Combine(_root, "plan"), true); Assert.Single(read.ReadRecords()); }
        else { using var read = new FileControlTraversalJournal(Path.Combine(_root, "control"), true); Assert.Single(read.ReadRecords()); }
    }

    [Theory]
    [InlineData("control")]
    [InlineData("plan")]
    public async Task RealDriverKillBetweenOriginalCommitAndOwnerAcknowledgementPreservesBothSources(string kind)
    {
        using var writer = StartProbe("write-control-plan-async", _root, kind);
        try
        {
            var marker = Path.Combine(_root, "ready.json"); var deadline = DateTime.UtcNow.AddSeconds(40);
            while (!File.Exists(marker) && !writer.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.True(File.Exists(marker), writer.HasExited ? await writer.StandardError.ReadToEndAsync() : "owner commit marker timeout");
            using var readyDocument = JsonDocument.Parse(File.ReadAllText(marker)); var ready = readyDocument.RootElement;
            Assert.True(ready.GetProperty("DriverPending").GetBoolean()); Assert.False(ready.GetProperty("IsTerminal").GetBoolean());
            Assert.Equal(0, ready.GetProperty("Handlers").GetInt32()); Assert.Equal(0, ready.GetProperty("PublishedOperations").GetInt32());
            Assert.Equal(1, ready.GetProperty("Consultations").GetInt32());
            Assert.Equal(kind == "control", ready.GetProperty("PublishedPlan").GetBoolean());
            Assert.Equal(ready.GetProperty("OwnerControlPosition").GetProperty("Sequence").GetInt32() + (kind == "control" ? 1 : 0), ready.GetProperty("SourceControlRows").GetInt32());
            Assert.Equal(ready.GetProperty("OwnerPlanPosition").GetProperty("Sequence").GetInt32() + (kind == "plan" ? 1 : 0), ready.GetProperty("SourcePlanRows").GetInt32());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(_root, "kill-observed.json"), JsonSerializer.Serialize(new { writer.Id, writer.HasExited, writer.ExitCode }));
            var hashes = Directory.GetFiles(_root).ToDictionary(file => Path.GetFileName(file)!, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            File.WriteAllText(Path.Combine(_root, "source-hashes.json"), JsonSerializer.Serialize(hashes));
            using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "expected.json"))); var readerIds = new HashSet<int>();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                using var reader = StartProbe("read-control-plan-async", _root, ready.GetProperty("RunId").GetString()!);
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                    using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "control-plan-reader.json"))); var read = result.RootElement;
                    var id = read.GetProperty("ProcessId").GetInt32(); Assert.NotEqual(writer.Id, id); Assert.True(readerIds.Add(id));
                    Assert.True(read.GetProperty("IsReadOnly").GetBoolean()); Assert.False(read.GetProperty("IsTerminal").GetBoolean()); Assert.False(read.GetProperty("ContinuationSupported").GetBoolean());
                    Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("ControlRows"), read.GetProperty("ControlRecords")));
                    Assert.True(JsonElement.DeepEquals(expected.RootElement.GetProperty("PlanRows"), read.GetProperty("PlanRecords")));
                    foreach (var (file, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_root, file)))));
                    File.Copy(Path.Combine(_root, "control-plan-reader.json"), Path.Combine(_root, "reader-" + repeat + ".json"));
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            if (Environment.GetEnvironmentVariable("UNICLAW_CONTROL_PLAN_ASYNC_EXPORT") is { Length: > 0 } output)
            {
                output = Path.Combine(output, kind); Directory.CreateDirectory(output);
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
