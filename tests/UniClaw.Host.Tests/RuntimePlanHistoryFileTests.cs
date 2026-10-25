using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RuntimePlanHistoryFileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("plan-history-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ProductionRunnerRecordsKnownEmptyPlanHistoryThroughReplaceableSources()
    {
        var result = RunResourceRecoveryTests.Run(_root); var archive = new FileRunRecoveryArchive(result.RunDir); var identity = archive.Read();
        Assert.Equal("uniclaw.runtime.original-identity.v5", identity.Schema);
        var expected = archive.ReadRuntimePlanHistory(identity.Run.RunId);
        Assert.Equal("initialize", Assert.Single(expected.Records).Operation); Assert.Null(expected.LatestCommittedAdoption);
        using var plans = new FileRuntimePlanJournal(Path.Combine(result.RunDir, "runtime-plan.journal.jsonl"), true);
        using var run = new FileRunStateJournal(Path.Combine(result.RunDir, "run-state.journal.jsonl"), true);
        var memory = new MemorySources(new(run.RecordingId, run.ReadRecords()), new(plans.RecordingId, plans.ReadRecords()));
        var memoryArchive = new MemoryArchive(identity);
        Directory.Delete(result.RunDir, true);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var actual = new RunRecoveryReader(memoryArchive, memory).ReadRuntimePlanHistory(identity.Run.RunId);
            Assert.Equal(expected.Position, actual.Position); Assert.Null(actual.LatestCommittedAdoption);
            Assert.False(actual.ContinuationSupported);
        }
        Assert.Equal(0, memory.Run.WriteAttempts + memory.Plan.WriteAttempts + memory.DisposeCalls + memoryArchive.WriteAttempts);
        Assert.False(Directory.Exists(result.RunDir));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("torn")]
    [InlineData("head-missing")]
    [InlineData("head-stale")]
    [InlineData("tail-loss")]
    [InlineData("hash")]
    [InlineData("wrong-plan-source")]
    [InlineData("wrong-run-source")]
    [InlineData("schema")]
    [InlineData("unknown-payload")]
    [InlineData("initial-version")]
    [InlineData("legacy-v1")]
    [InlineData("legacy-v2")]
    [InlineData("legacy-v3")]
    [InlineData("legacy-v4")]
    public void MissingLegacyOrInvalidPlanSourceIsUnavailableRatherThanKnownEmpty(string fault)
    {
        var result = RunResourceRecoveryTests.Run(_root); var archive = new FileRunRecoveryArchive(result.RunDir); var original = archive.Read();
        var path = Path.Combine(result.RunDir, "runtime-plan.journal.jsonl");
        var originalHistory = archive.ReadRuntimePlanHistory(original.Run.RunId);
        if (fault == "missing") File.Delete(path);
        if (fault == "torn") File.WriteAllBytes(path, File.ReadAllBytes(path)[..^2]);
        if (fault == "tail-loss") File.WriteAllText(path, "");
        if (fault == "head-missing") File.Delete(path + ".head");
        if (fault == "head-stale")
        {
            var head = JsonNode.Parse(File.ReadAllText(path + ".head"))!; head["sequence"] = 1000;
            File.WriteAllText(path + ".head", head.ToJsonString() + "\n");
        }
        if (fault == "hash") File.WriteAllText(path, File.ReadAllText(path).Replace("initialize", "invented", StringComparison.Ordinal));
        if (fault == "wrong-plan-source" || fault.StartsWith("legacy", StringComparison.Ordinal))
        {
            var identityPath = Path.Combine(result.RunDir, "run-identity.json");
            var frame = JsonNode.Parse(File.ReadAllText(identityPath))!; var payload = JsonNode.Parse(frame["payload"]!.GetValue<string>())!;
            if (fault == "wrong-plan-source") payload["runtimePlanRecordingId"] = Guid.NewGuid().ToString("N");
            else { payload["schema"] = "uniclaw.runtime.original-identity." + fault.Split('-')[1]; payload.AsObject().Remove("runtimePlanRecordingId"); }
            frame["payload"] = payload.ToJsonString(); frame["hash"] = Hash(frame["consultationFile"]!.GetValue<string>() + "\n" + payload.ToJsonString());
            File.WriteAllText(identityPath, frame.ToJsonString() + "\n");
            Assert.True(archive.Inspect(original.Run.RunId).AccountingComplete);
            if (fault == "legacy-v4") Assert.False(archive.ReadControlTraversalHistory(original.Run.RunId).ContinuationSupported);
        }
        if (fault is "wrong-run-source" or "schema" or "unknown-payload" or "initial-version")
        {
            CoreHistoryLegacyFixture.WriteRuntimePlan(path, originalHistory.Position.RecordingId, originalHistory.Records, rows =>
            {
                var row = rows[0]!;
                if (fault == "wrong-run-source") row["runRecordingId"] = Guid.NewGuid().ToString("N");
                if (fault == "schema") row["schema"] = "unknown-plan-schema";
                if (fault == "unknown-payload") row["invented"] = true;
                if (fault == "initial-version") row["versions"]!["invented"] = 1;
            });
        }
        var error = Record.Exception(() => archive.ReadRuntimePlanHistory(original.Run.RunId));
        Assert.True(error is IOException or InvalidDataException or JsonException, error?.ToString() ?? "invalid Plan source accepted");
        Assert.True(archive.RestoreRunState(original.Run.RunId).IsReadOnly);
    }

    [Fact]
    public async Task ActualKillPreservesCompleteOriginalPlanCommitBeforeRuntimePublication()
    {
        var directory = Path.Combine(_root, "kill"); Directory.CreateDirectory(directory);
        using var original = Start("write-plan", directory, "adopt");
        try
        {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(Path.Combine(directory, "ready.json")) && !original.HasExited && DateTime.UtcNow < until) await Task.Delay(25);
            Assert.True(File.Exists(Path.Combine(directory, "ready.json")), original.HasExited ? await original.StandardError.ReadToEndAsync() : "readiness timeout");
            var ready = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "ready.json")))!; var runId = ready["RunId"]!.GetValue<string>();
            Assert.False(ready["IsTerminal"]!.GetValue<bool>()); Assert.Null(ready["Plan"]);
            Assert.Empty(ready["RunOperations"]!.AsArray()); Assert.Equal(0, ready["Deliveries"]!.GetValue<int>());
            Assert.Equal(ready["PublishedSequence"]!.GetValue<int>() + 1, ready["SourceSequence"]!.GetValue<int>());
            original.Kill(true); await original.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, original.ExitCode);
            File.WriteAllText(Path.Combine(directory, "kill-observed.json"), JsonSerializer.Serialize(new { original.Id, original.ExitCode, original.HasExited }));
            var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "plan-expected.json")))!;
            var hashes = Directory.GetFiles(directory).Where(p => p.EndsWith(".jsonl", StringComparison.Ordinal) || p.EndsWith(".head", StringComparison.Ordinal)
                || Path.GetFileName(p) is "run-identity.json" or "exec.journal").ToDictionary(p => Path.GetFileName(p)!, p => HashFile(p));
            File.WriteAllText(Path.Combine(directory, "readonly-source-hashes.json"), JsonSerializer.Serialize(hashes));
            for (var index = 0; index < 2; index++)
            {
                using var reader = Start("read-plan", directory, runId);
                try
                {
                    await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                    var result = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "plan-reader.json")))!; var read = result["Read"]!;
                    Assert.NotEqual(original.Id, result["ProcessId"]!.GetValue<int>());
                    Assert.False(read["ContinuationSupported"]!.GetValue<bool>());
                    Assert.True(JsonNode.DeepEquals(expected["SourceRows"], read["Records"]));
                    var graph = read["LatestCommittedAdoption"]!["Graph"]!;
                    Assert.Equal(8, graph["nodes"]!.AsArray().Select(n => n!["kind"]!.GetValue<string>()).Distinct().Count());
                    Assert.Equal("original-plan", graph["planId"]!.GetValue<string>());
                    foreach (var (name, hash) in hashes) Assert.Equal(hash, HashFile(Path.Combine(directory, name)));
                    File.Copy(Path.Combine(directory, "plan-reader.json"), Path.Combine(directory, $"reader-{index}.json"));
                }
                finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
            }
            var output = Environment.GetEnvironmentVariable("RUNTIME_PLAN_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(output))
            {
                var target = Path.Combine(output, "adopt-kill"); Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            var archive = new FileRunRecoveryArchive(directory); var identity = archive.Read();
            MemorySources memory;
            using (var plans = new FileRuntimePlanJournal(Path.Combine(directory, "runtime-plan.journal.jsonl"), true))
            using (var run = new FileRunStateJournal(Path.Combine(directory, "run-state.journal.jsonl"), true))
                memory = new(new(run.RecordingId, run.ReadRecords()), new(plans.RecordingId, plans.ReadRecords()));
            Directory.Delete(directory, true);
            var memoryRead = new RunRecoveryReader(new MemoryArchive(identity), memory).ReadRuntimePlanHistory(runId);
            Assert.Equal(expected["SourceRows"]!.ToJsonString(), JsonSerializer.SerializeToNode(memoryRead.Records)!.ToJsonString());
            Assert.False(Directory.Exists(directory)); Assert.Equal(0, memory.Run.WriteAttempts + memory.Plan.WriteAttempts + memory.DisposeCalls);
        }
        finally { if (!original.HasExited) { original.Kill(true); await original.WaitForExitAsync(); } }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static Process Start(string mode, string directory, string argument)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("Probe missing");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", configuration, "net10.0/UniClaw.RunRecoveryProbe.dll"));
        start.ArgumentList.Add(mode); start.ArgumentList.Add(directory); start.ArgumentList.Add(argument); return Process.Start(start)!;
    }

    private sealed class MemoryArchive(RunRecoveryRecord row) : IRunRecoveryArchive
    {
        public int WriteAttempts { get; private set; }
        public RunRecoveryRecord Read() => row;
        public void Initialize(RunRecoveryRecord record) { WriteAttempts++; throw new InvalidOperationException("readonly"); }
    }
    private sealed class RunSource(string id, IReadOnlyList<RunHistoryRecord> rows) : IRunStateJournal
    {
        public bool CanInitialize => false; public string RecordingId => id; public int WriteAttempts { get; private set; }
        public IReadOnlyList<RunHistoryRecord> ReadRecords() => rows;
        public void Append(RunHistoryRecord row) { WriteAttempts++; throw new InvalidOperationException("readonly"); }
    }
    private sealed class PlanSource(string id, IReadOnlyList<RuntimePlanHistoryRecord> rows) : IRuntimePlanJournal
    {
        public bool CanInitialize => false; public string RecordingId => id; public int WriteAttempts { get; private set; }
        public IReadOnlyList<RuntimePlanHistoryRecord> ReadRecords() => rows;
        public void Append(RuntimePlanHistoryRecord row) { WriteAttempts++; throw new InvalidOperationException("readonly"); }
    }
    private sealed class MemorySources(RunSource run, PlanSource plan) : IRunRecoverySources, IDisposable
    {
        public RunSource Run => run; public PlanSource Plan => plan; public int DisposeCalls { get; private set; }
        public IRunStateJournal RunStateHistory => run;
        public IRuntimePlanJournal RuntimePlanHistory => plan;
        public IEvidenceJournal EvidenceHistory => throw new InvalidDataException("unavailable");
        public IWorldJournal WorldHistory => throw new InvalidDataException("unavailable");
        public IControlTraversalJournal ControlTraversalHistory => throw new InvalidDataException("unavailable");
        public RunResourceRead ReadResources(RunResourceBinding? binding, bool requireCommittedEndpoint) => throw new InvalidDataException("unavailable");
        public void Dispose() => DisposeCalls++;
    }
}
