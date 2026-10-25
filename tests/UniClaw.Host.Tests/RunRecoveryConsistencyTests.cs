using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Host.Runtime;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunRecoveryConsistencyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("run-joint-read-").FullName;
    private static readonly string[] Sources = ["run-identity.json", "run-state.journal.jsonl", "run-state.journal.jsonl.head",
        "resource.journal.jsonl", "resource.journal.jsonl.head", "consultations.journal.jsonl", "exec.journal"];
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ProductionArchiveJointReadPreservesOriginalVersionsBalancesAndReadOnlyState()
    {
        string? id = null; var result = RunResourceRecoveryTests.Run(_root, c => id = c.RunId);
        var hashes = Hashes(result.RunDir); var archive = new FileRunRecoveryArchive(result.RunDir);
        for (var i = 0; i < 2; i++)
        {
            var read = archive.InspectRecovery(id!);
            Assert.True(read.StateAndResourcesConsistent, read.Reason);
            Assert.True(read.Run!.IsReadOnly); Assert.False(read.ContinuationSupported);
            Assert.Equal(read.Run.HistoryPosition, read.Point!.RunHistory);
            Assert.Equal(read.Resources.RunHistoryPosition, read.Point.RunHistory);
            Assert.Equal(1, read.Point.RunHistory.Sequence);
            Assert.Equal(0, read.Run.State!.Progress.Acts); Assert.Equal(1, read.Resources.Confirmed!.RoundsConsumed);
            Assert.Equal(4, read.Resources.StepsRemaining); Assert.Equal(5, read.Resources.RoundsRemaining);
            Assert.Throws<InvalidOperationException>(() => read.Run.RecordCycle());
        }
        Unchanged(result.RunDir, hashes); Export("production-read", result.RunDir);
    }

    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "synthetic"); }
    private sealed class NoDelivery : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("no effect in joint read"); }
    private sealed class Fault(IRunResourceJournal inner, bool afterCommit) : IRunResourceJournal
    {
        public bool CanInitialize => inner.CanInitialize;
        public string? RecordingId => inner.RecordingId;
        public IReadOnlyList<RunResourceRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RunResourceRecord row)
        {
            if (row.Operation != "checkpoint" || row.Phase != RunResourcePhase.Confirm) { inner.Append(row); return; }
            if (afterCommit) inner.Append(row);
            throw new IOException("controlled checkpoint confirmation failure");
        }
    }
    private string TerminalRun(bool? confirmationFailure = null)
    {
        using var state = new FileRunStateJournal(Path.Combine(_root, "run-state.journal.jsonl"));
        using var p25 = new FileConsultationJournal(Path.Combine(_root, "consultations.journal.jsonl"));
        using var exec = new FileExecutionJournal(Path.Combine(_root, "exec.journal"));
        using var resources = new FileRunResourceJournal(Path.Combine(_root, "resource.journal.jsonl"),
            consultationPath: Path.Combine(_root, "consultations.journal.jsonl"), executionPath: Path.Combine(_root, "exec.journal"));
        var model = new RunModel(state); var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(new HashSet<string> { "live.frame" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()), DisabledRunTrace.Instance,
            model, new ControlLoop(policy), new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery(), exec));
        var driver = new KernelRunDriver(kernel, policy, new RunDriverInputs
        {
            ResourceJournal = confirmationFailure is { } after ? new Fault(resources, after) : resources,
            ConsultationJournal = p25, RecoveryArchive = new FileRunRecoveryArchive(_root),
            PollCancellation = () => new("synthetic bounded stop", DateTimeOffset.Parse("2026-10-24T00:00:00Z")),
            NextInput = _ => throw new InvalidOperationException("must not observe"),
            ConsultAgent = _ => throw new InvalidOperationException("must not consult"),
        });
        Assert.True(kernel.AdmitContract(new("joint-v1", "original joint read", new HashSet<string> { "live.frame" },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["stop"],
            [new("stop", RunObligationKind.SafeStop, "live.frame", "stop", true)], MaxConsultations: 4, MaxTotalSteps: 10,
            TaskInstanceId: "joint-task", ProductSessionId: "joint-session", ContractRef: "joint-contract")).Accepted);
        Assert.True(driver.Activate().Accepted);
        var result = driver.Drive(); Assert.True(model.IsTerminal);
        Assert.Equal(confirmationFailure is null ? RunDriveStatus.Completed : RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        if (confirmationFailure is not null)
        {
            var count = resources.ReadRecords().Count; driver.Drive(); Assert.Equal(count, resources.ReadRecords().Count);
        }
        return kernel.RunId;
    }

    [Fact]
    public void DriverLinksActualTerminalWithoutChangingResourceFacts()
    {
        var id = TerminalRun(); var archive = new FileRunRecoveryArchive(_root); var read = archive.InspectRecovery(id);
        Assert.True(read.StateAndResourcesConsistent, read.Reason); Assert.True(read.Run!.IsTerminal);
        Assert.Equal(TerminalClassification.SafeStop, read.Run.State!.Outcome!.Classification);
        Assert.Equal(2, read.Point!.RunHistory.Sequence); Assert.Equal(3, read.Point.ResourceSequence);
        Assert.Equal(0, read.Resources.Confirmed!.StepsConsumed); Assert.Equal(0, read.Resources.Confirmed.RoundsConsumed);
        Assert.Equal(10, read.Resources.StepsRemaining); Assert.Equal(4, read.Resources.RoundsRemaining);
        Assert.False(read.ContinuationSupported); Export("terminal-linked", _root);
    }

    [Fact]
    public void CommonPointAlsoIncludesARealDispatchedAndVerifiedReceipt()
    {
        var feed = new RunResourceAccountingTests.Source(navigation: true); string? id = null;
        var result = SettingsCoverageRunner.Run(_root, new("synthetic-device",
            new("synthetic-device", "unused", "/unused", "/unused/python"),
            new("1", new("joint receipt read", "offline", false, false), new(4, 6, 0, 3, 1),
                new(true, "configured", 0, 0, false, 0), ["Child"], new(true, true, true, true), "synthetic:resources", "unused", null),
            context => { id ??= context.RunId; return new AgentDecision.Act(new(context.DecisionId,
                [new("ui.element", "Open child", "tap", null)], "original navigation")); },
            ViewportWidth: 100, ViewportHeight: 100, FeedNext: feed.Next, CurrentCaptureId: () => feed.CaptureId,
            EffectDriver: feed, UseCoverageDirector: false, PollCancellation: () => feed.PostIdentitySupplied
                ? new("stop after verified receipt", DateTimeOffset.Parse("2026-10-24T00:01:00Z")) : null));
        Assert.Equal(1, feed.Deliveries); Assert.True(Assert.Single(result.Steps).Verified);
        var read = new FileRunRecoveryArchive(result.RunDir).InspectRecovery(id!);
        Assert.True(read.StateAndResourcesConsistent, read.Reason); Assert.True(read.Run!.IsReadOnly);
        Assert.Equal(1, read.Resources.Confirmed!.StepsConsumed); Assert.Single(read.Resources.Confirmed.ReceiptIds);
        Assert.Equal(3, read.Resources.StepsRemaining); Assert.Equal(read.Run.HistoryPosition, read.Point!.RunHistory);
        Assert.False(read.ContinuationSupported); Export("verified-receipt", result.RunDir);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckpointFailureUsesActualCommittedFactsAndNeverRetries(bool afterCommit)
    {
        var id = TerminalRun(afterCommit); var read = new FileRunRecoveryArchive(_root).InspectRecovery(id);
        Assert.True(read.Run!.IsTerminal); Assert.True(read.Run.IsReadOnly);
        Assert.Equal(afterCommit, read.StateAndResourcesConsistent); Assert.False(read.ContinuationSupported);
        if (!afterCommit) { Assert.Null(read.Point); Assert.Equal("checkpoint", read.Resources.PendingOperation); Assert.Null(read.Resources.StepsRemaining); }
        Export("checkpoint-failed-" + afterCommit, _root);
    }

    [Theory]
    [InlineData("missing-head")]
    [InlineData("stale-head")]
    [InlineData("whole-checkpoint-loss")]
    [InlineData("uncommitted-tail")]
    [InlineData("missing-link")]
    [InlineData("wrong-position")]
    [InlineData("wrong-recording")]
    [InlineData("checkpoint-grant")]
    [InlineData("checkpoint-progress")]
    [InlineData("wrong-run")]
    [InlineData("p25-suffix")]
    [InlineData("exec-suffix")]
    [InlineData("state-missing")]
    public void UnavailableOrConflictingSourcesCannotCertifyACommonPoint(string fault)
    {
        var id = TerminalRun(); var path = Path.Combine(_root, "resource.journal.jsonl");
        LegacyResourceFixture.ConvertToV3(path);
        var lines = File.ReadAllLines(path);
        if (fault == "missing-head") File.Delete(path + ".head");
        if (fault == "stale-head")
        { var head = JsonNode.Parse(File.ReadAllText(path + ".head"))!; head["sequence"] = 999; File.WriteAllText(path + ".head", head.ToJsonString() + "\n"); }
        if (fault == "whole-checkpoint-loss") File.WriteAllText(path, lines[0] + "\n");
        if (fault == "uncommitted-tail")
        {
            var frame = JsonNode.Parse(lines[^1])!; var row = JsonNode.Parse(frame["payload"]!.GetValue<string>())!;
            row["sequence"] = lines.Length + 1; row["phase"] = 1; row["snapshot"] = null;
            frame["previous"] = frame["hash"]!.GetValue<string>(); frame["payload"] = row.ToJsonString();
            frame["hash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(frame["previous"]!.GetValue<string>()
                + "\n" + row.ToJsonString() + "\n" + frame["sources"]!.ToJsonString() + "\n" + frame["recordingId"]!.GetValue<string>())));
            File.AppendAllText(path, frame.ToJsonString() + "\n");
        }
        if (fault == "missing-link") File.WriteAllText(path, lines[0] + "\n");
        if (fault is "missing-link" or "wrong-position" or "wrong-recording" or "checkpoint-grant" or "checkpoint-progress")
        {
            RewriteResources(path, row =>
            {
                if (fault == "missing-link") row["runHistoryPosition"] = null;
                if (fault == "wrong-position") row["runHistoryPosition"]!["sequence"] = 999;
                if (fault == "wrong-recording") row["runHistoryPosition"]!["recordingId"] = "11111111111111111111111111111111";
                if (fault == "checkpoint-grant" && row["phase"]!.GetValue<int>() == 2)
                {
                    // No proposal or accepted renewal: a checkpoint cannot mint credit.
                    row["snapshot"]!["grantedSteps"] = 1;
                }
                if (fault == "checkpoint-progress" && row["phase"]!.GetValue<int>() == 2)
                    row["snapshot"]!["usedProgressFacts"] = new JsonArray("old-progress:1");
            });
        }
        if (fault == "wrong-run") id = "wrong-run";
        if (fault == "p25-suffix") File.AppendAllText(Path.Combine(_root, "consultations.journal.jsonl"), "unconfirmed\n");
        if (fault == "exec-suffix") File.AppendAllText(Path.Combine(_root, "exec.journal"), "unconfirmed\n");
        if (fault == "state-missing") File.Delete(Path.Combine(_root, "run-state.journal.jsonl"));
        var archive = new FileRunRecoveryArchive(_root); var read = archive.InspectRecovery(id);
        Assert.False(read.StateAndResourcesConsistent); Assert.Null(read.Point); Assert.False(read.ContinuationSupported);
        if (fault == "whole-checkpoint-loss")
        { Assert.True(archive.Inspect(id).AccountingComplete); Assert.True(read.Run!.IsTerminal); Assert.False(read.Resources.CommittedEndpointVerified); }
        if (fault is "wrong-position" or "wrong-recording") Assert.Equal("run-resource-position-mismatch", read.Reason);
        if (fault == "missing-link") Assert.Equal("run-resource-link-missing", read.Reason);
        Export("bad-" + fault, _root);
    }

    private static void RewriteResources(string path, Action<JsonNode> change)
    {
        LegacyResourceFixture.ConvertToV3(path);
        var frames = File.ReadAllLines(path).Select(l => JsonNode.Parse(l)!).ToArray(); var previous = "";
        foreach (var frame in frames)
        {
            var row = JsonNode.Parse(frame["payload"]!.GetValue<string>())!; change(row);
            frame["payload"] = row.ToJsonString(); frame["previous"] = previous;
            previous = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "\n" + frame["payload"]!.GetValue<string>()
                + "\n" + frame["sources"]!.ToJsonString() + "\n" + frame["recordingId"]!.GetValue<string>())));
            frame["hash"] = previous;
        }
        File.WriteAllText(path, string.Join("\n", frames.Select(f => f.ToJsonString())) + "\n");
        var head = JsonNode.Parse(File.ReadAllText(path + ".head"))!; head["hash"] = previous; head["sequence"] = frames.Length;
        File.WriteAllText(path + ".head", head.ToJsonString() + "\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACommonPointRequiresUnbrokenLinksAndAgreementWithOriginalTerminalState(bool missingInitialLink)
    {
        string id; string directory;
        if (missingInitialLink) { id = TerminalRun(); directory = _root; }
        else
        {
            string? original = null;
            var result = RunResourceRecoveryTests.Run(_root, c => original = c.RunId);
            id = original!; directory = result.RunDir;
        }
        RewriteResources(Path.Combine(directory, "resource.journal.jsonl"), row =>
        {
            if (missingInitialLink && row["sequence"]!.GetValue<int>() == 1) row["runHistoryPosition"] = null;
            if (!missingInitialLink && row["snapshot"] is { } snapshot) snapshot["terminal"] = true;
        });
        var read = new FileRunRecoveryArchive(directory).InspectRecovery(id);
        Assert.False(read.StateAndResourcesConsistent); Assert.Null(read.Point);
        Assert.False(read.ContinuationSupported);
        Export(missingInitialLink ? "partial-link" : "terminal-conflict", directory);
    }

    [Theory]
    [InlineData("settled", true, false)]
    [InlineData("terminal-linked", true, true)]
    [InlineData("terminal", false, true)]
    [InlineData("pending", false, false)]
    public async Task IndependentProcessComparesOriginalHistoriesAfterActualKill(string mode, bool consistent, bool terminal)
    {
        using var writer = Start("write", _root, mode);
        try
        {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(Path.Combine(_root, "ready.json")) && !writer.HasExited && DateTime.UtcNow < until) await Task.Delay(25);
            Assert.True(File.Exists(Path.Combine(_root, "ready.json")), writer.HasExited ? await writer.StandardError.ReadToEndAsync() : "readiness timeout");
            using var readyDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "ready.json"))); var ready = readyDoc.RootElement;
            Assert.Equal(terminal, ready.GetProperty("Terminal").GetBoolean());
            writer.Kill(true); await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.NotEqual(0, writer.ExitCode);
            File.WriteAllText(Path.Combine(_root, "joint-kill-observed.json"), JsonSerializer.Serialize(new { ProcessId = writer.Id, writer.ExitCode, writer.HasExited }));
            var hashes = Hashes(_root);
            using var reader = Start("read-joint", _root, ready.GetProperty("RunId").GetString()!);
            try
            {
                await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(reader.ExitCode == 0, await reader.StandardError.ReadToEndAsync());
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "joint-reader-result.json"))); var read = doc.RootElement;
                Assert.NotEqual(writer.Id, read.GetProperty("ProcessId").GetInt32());
                Assert.Equal(consistent, read.GetProperty("StateAndResourcesConsistent").GetBoolean());
                Assert.Equal(terminal, read.GetProperty("IsTerminal").GetBoolean()); Assert.True(read.GetProperty("IsReadOnly").GetBoolean());
                Assert.False(read.GetProperty("ContinuationSupported").GetBoolean());
                if (mode == "settled")
                { Assert.Equal(12, read.GetProperty("Resources").GetProperty("StepsRemaining").GetInt32()); Assert.Equal(5, read.GetProperty("Resources").GetProperty("RoundsRemaining").GetInt32()); }
                if (mode == "terminal") Assert.Equal("run-resource-position-mismatch", read.GetProperty("Reason").GetString());
                Unchanged(_root, hashes); Export("kill-" + mode, _root);
            }
            finally { if (!reader.HasExited) { reader.Kill(true); await reader.WaitForExitAsync(); } }
        }
        finally { if (!writer.HasExited) { writer.Kill(true); await writer.WaitForExitAsync(); } }
    }
    private static Process Start(string mode, string directory, string argument)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/RunRecoveryProbe/RunRecoveryProbe.csproj"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("probe missing");
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { Path.Combine(root.FullName, "tools/RunRecoveryProbe/bin", config, "net10.0/UniClaw.RunRecoveryProbe.dll"), mode, directory, argument }) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }
    private static Dictionary<string, string> Hashes(string directory) => Sources.ToDictionary(n => n,
        n => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, n)))));
    private static void Unchanged(string directory, Dictionary<string, string> hashes)
    { foreach (var (name, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name))))); }
    private static void Export(string label, string directory)
    {
        var targetRoot = Environment.GetEnvironmentVariable("JOINT_RECOVERY_EVIDENCE_DIR"); if (string.IsNullOrEmpty(targetRoot)) return;
        var target = Path.Combine(targetRoot, label); Directory.CreateDirectory(target);
        foreach (var path in Directory.GetFiles(directory)) File.Copy(path, Path.Combine(target, Path.GetFileName(path)), true);
    }
}
