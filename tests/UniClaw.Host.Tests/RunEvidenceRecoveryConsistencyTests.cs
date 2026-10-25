using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Effects.ExecutionSource;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Evidence;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunEvidenceRecoveryConsistencyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("run-evidence-joint-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void ProductionResourceCommitReferencesTheOriginalEvidenceEndpoint()
    {
        var result = RunResourceRecoveryTests.Run(_root);
        using var resources = new FileRunResourceJournal(Path.Combine(result.RunDir, "resource.journal.jsonl"), readOnly: true);
        using var evidence = new FileEvidenceJournal(Path.Combine(result.RunDir, "evidence.journal.jsonl"), readOnly: true);
        var original = EvidenceLedger.RestoreReadOnly(evidence, evidence.RecordingId);
        Assert.Equal(original.HistoryPosition, resources.ReadRecords()[^1].EvidenceHistoryPosition);
        var archive = new FileRunRecoveryArchive(result.RunDir); var id = archive.Read().Run.RunId;
        var hashes = Hashes(result.RunDir);
        for (var i = 0; i < 2; i++) AssertConsistent(archive.InspectEvidenceRecovery(id));
        Unchanged(hashes); Export("production-read", result.RunDir);
    }

    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
    private static readonly string[] Sources = ["run-identity.json", "run-state.journal.jsonl", "run-state.journal.jsonl.head",
        "resource.journal.jsonl", "resource.journal.jsonl.head", "consultations.journal.jsonl", "exec.journal",
        "evidence.journal.jsonl", "evidence.journal.jsonl.head"];
    private static ObservationProposal Seed(string value = "seed") => new(new("live.frame", value), IngressKind.Observation,
        ObservationContext.External, new("original-seven", Time, "capture:original", ["synthetic", "original"]));
    private static void AssertConsistent(RunEvidenceRecoveryRead read)
    {
        Assert.True(read.HistoriesConsistent, read.Reason); Assert.Null(read.Reason); Assert.NotNull(read.Point);
        Assert.True(read.RunAndResources.StateAndResourcesConsistent); Assert.True(read.RunAndResources.Run!.IsReadOnly);
        Assert.True(read.Evidence!.IsReadOnly); Assert.False(read.ContinuationSupported);
        Assert.Equal(read.RunAndResources.Point, read.Point.RunAndResources);
        Assert.Equal(read.Evidence.HistoryPosition, read.Point.EvidenceHistory);
        Assert.Equal(read.RunAndResources.Resources.EvidenceHistoryPosition, read.Point.EvidenceHistory);
        Assert.Throws<InvalidOperationException>(() => read.Evidence.Admit(Seed("new")));
        Assert.Throws<InvalidOperationException>(() => read.RunAndResources.Run.RecordCycle());
    }
    private sealed class Fresh : IFreshnessEvaluator
    { public FreshnessJudgment Evaluate(FreshnessEvaluationInput _) => new(FreshnessSufficiency.Sufficient, "controlled"); }
    private sealed class NoDelivery : IEffectDriver
    { public DispatchResult Deliver(DispatchRequest _) => throw new InvalidOperationException("No delivery in metadata-only fixture"); }
    private sealed class CheckpointFault(IRunResourceJournal inner, bool afterCommit) : IRunResourceJournal
    {
        public bool CanInitialize => inner.CanInitialize;
        public string? RecordingId => inner.RecordingId;
        public IReadOnlyList<RunResourceRecord> ReadRecords() => inner.ReadRecords();
        public void Append(RunResourceRecord row)
        {
            if (row.Operation != "checkpoint" || row.Phase != RunResourcePhase.Confirm) { inner.Append(row); return; }
            if (afterCommit) inner.Append(row);
            throw new IOException("controlled original checkpoint interruption");
        }
    }
    private sealed class OriginalRun : IDisposable
    {
        public FileRunStateJournal State { get; }
        public FileEvidenceJournal Evidence { get; }
        public FileConsultationJournal P25 { get; }
        public FileExecutionJournal Execution { get; }
        public FileRunResourceJournal Resources { get; }
        public EvidenceLedger Ledger { get; }
        public UniKernel Kernel { get; }
        public KernelRunDriver Driver { get; }
        public int Consultations { get; private set; }
        public OriginalRun(string directory, bool terminal = false, bool empty = false, bool? checkpointFailure = null)
        {
            Directory.CreateDirectory(directory);
            State = new(Path.Combine(directory, "run-state.journal.jsonl"));
            Evidence = new(Path.Combine(directory, "evidence.journal.jsonl"));
            P25 = new(Path.Combine(directory, "consultations.journal.jsonl")); Execution = new(Path.Combine(directory, "exec.journal"));
            Resources = new(Path.Combine(directory, "resource.journal.jsonl"), consultationPath: Path.Combine(directory, "consultations.journal.jsonl"),
                executionPath: Path.Combine(directory, "exec.journal"));
            Ledger = new(Evidence); var plan = new AgentPlanPolicy();
            Kernel = new(Ledger, new WorldModel(new HashSet<string> { "live.frame" }, new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()),
                DisabledRunTrace.Instance, new RunModel(State), new ControlLoop(plan), new RuntimeAssurance(new Fresh()), new EffectBoundary(new NoDelivery(), Execution));
            var seen = 0;
            Driver = new(Kernel, plan, new RunDriverInputs
            {
                ResourceJournal = checkpointFailure is { } after ? new CheckpointFault(Resources, after) : Resources,
                ConsultationJournal = P25, RecoveryArchive = new FileRunRecoveryArchive(directory),
                PollCancellation = terminal ? () => new("original safe stop", Time) : null,
                NextInput = _ => !empty && seen++ == 0 ? new RunDriverInput.Observation([Seed()]) : null,
                ConsultAgent = _ => { Consultations++; return null; },
            });
            Assert.True(Kernel.AdmitContract(new("triple-v1", "original three history read", new HashSet<string> { "live.frame" },
                new HashSet<string> { "tap" }, new HashSet<string>(), ["original"],
                Obligations: terminal ? [new("stop", RunObligationKind.SafeStop, "live.frame", "stop", true)] : null,
                MaxConsultations: 4, MaxTotalSteps: 10, TaskInstanceId: "triple-task", ProductSessionId: "triple-session", ContractRef: "triple-contract")).Accepted);
            Assert.True(Driver.Activate().Accepted); Driver.Drive();
        }
        public void Dispose() { Resources.Dispose(); Execution.Dispose(); P25.Dispose(); Evidence.Dispose(); State.Dispose(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EmptyAndTerminalOriginalHistoriesHavePreciseReadOnlyPoints(bool terminal)
    {
        using var original = new OriginalRun(_root, terminal, empty: true);
        var archive = new FileRunRecoveryArchive(_root); var hashes = Hashes(_root);
        var read = archive.InspectEvidenceRecovery(original.Kernel.RunId); AssertConsistent(read);
        Assert.Equal(terminal, read.RunAndResources.Run!.IsTerminal);
        Assert.Equal(terminal ? 1 : 0, read.Evidence!.HistoryPosition!.Sequence);
        Assert.Equal(0, read.RunAndResources.Resources.Confirmed!.StepsConsumed);
        Assert.Equal(0, read.RunAndResources.Resources.Confirmed.RoundsConsumed);
        Unchanged(hashes); Export(terminal ? "terminal-linked" : "empty", _root);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("rejected")] [InlineData("accepted")]
    public void AdmissionGrowthWithoutRunProgressRequiresItsOwnResourceCheckpoint(string kind)
    {
        using var original = new OriginalRun(_root); var archive = new FileRunRecoveryArchive(_root);
        AssertConsistent(archive.InspectEvidenceRecovery(original.Kernel.RunId));
        var runPosition = original.State.ReadRecords().Count; var canonical = original.Ledger.CanonicalRecords.Count;
        original.Ledger.Admit(kind == "duplicate" ? Seed() : kind == "rejected" ? Seed() with { Claim = new("", "bad") } : Seed("new fact"));
        Assert.Equal(runPosition, original.State.ReadRecords().Count);
        Assert.Equal(canonical + (kind == "accepted" ? 1 : 0), original.Ledger.CanonicalRecords.Count);
        var gap = archive.InspectEvidenceRecovery(original.Kernel.RunId);
        Assert.True(gap.RunAndResources.StateAndResourcesConsistent);
        Assert.False(gap.HistoriesConsistent); Assert.Null(gap.Point); Assert.NotNull(gap.Evidence);
        Assert.Equal("resource-evidence-position-mismatch", gap.Reason);
        Export("gap-" + kind, _root);
        var before = original.Resources.ReadRecords(); var facts = JsonSerializer.Serialize(before[^1].Snapshot);
        var consultations = original.Consultations;
        original.Driver.Drive();
        Assert.Equal(consultations, original.Consultations); Assert.Equal(runPosition, original.State.ReadRecords().Count);
        var after = original.Resources.ReadRecords(); Assert.Equal(before.Count + 2, after.Count);
        Assert.Equal("checkpoint", after[^1].Operation); Assert.Equal(facts, JsonSerializer.Serialize(after[^1].Snapshot));
        var hashes = Hashes(_root); AssertConsistent(archive.InspectEvidenceRecovery(original.Kernel.RunId)); Unchanged(hashes);
        Export("checkpoint-" + kind, _root);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CheckpointBeforeOrAfterCommitFailureUsesOriginalFactsAndNeverRetries(bool afterCommit)
    {
        using var original = new OriginalRun(_root, terminal: true, checkpointFailure: afterCommit);
        var archive = new FileRunRecoveryArchive(_root); var read = archive.InspectEvidenceRecovery(original.Kernel.RunId);
        Assert.True(read.RunAndResources.Run!.IsTerminal); Assert.NotNull(read.Evidence);
        Assert.Equal(afterCommit, read.HistoriesConsistent); Assert.False(read.ContinuationSupported);
        if (afterCommit) AssertConsistent(read);
        else { Assert.Null(read.Point); Assert.Equal("checkpoint", read.RunAndResources.Resources.PendingOperation); }
        var count = original.Resources.ReadRecords().Count;
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, original.Driver.Drive().Status);
        Assert.Equal(count, original.Resources.ReadRecords().Count);
        Export("checkpoint-failed-" + afterCommit, _root);
    }

    [Fact]
    public void ActualVerifiedDeliveryRemainsLinkedToTheSameEvidenceEndpoint()
    {
        var feed = new RunResourceAccountingTests.Source(navigation: true); string? id = null;
        var result = SettingsCoverageRunner.Run(_root, new("synthetic-device",
            new("synthetic-device", "unused", "/unused", "/unused/python"),
            new("1", new("triple receipt read", "offline", false, false), new(4, 6, 0, 3, 1),
                new(true, "configured", 0, 0, false, 0), ["Child"], new(true, true, true, true), "synthetic:resources", "unused", null),
            c => { id ??= c.RunId; return new AgentDecision.Act(new(c.DecisionId, [new("ui.element", "Open child", "tap", null)], "original navigation")); },
            ViewportWidth: 100, ViewportHeight: 100, FeedNext: feed.Next, CurrentCaptureId: () => feed.CaptureId,
            EffectDriver: feed, UseCoverageDirector: false, PollCancellation: () => feed.PostIdentitySupplied ? new("stop after verified receipt", Time.AddMinutes(1)) : null));
        Assert.Equal(1, feed.Deliveries); Assert.True(Assert.Single(result.Steps).Verified);
        var read = new FileRunRecoveryArchive(result.RunDir).InspectEvidenceRecovery(id!); var hashes = Hashes(result.RunDir);
        AssertConsistent(read); Assert.Equal(1, read.RunAndResources.Resources.Confirmed!.StepsConsumed);
        var receipt = Assert.Single(read.RunAndResources.Resources.Confirmed.ReceiptIds);
        var report = Assert.Single(read.Evidence!.CanonicalRecords.Values.Where(r => r.Kind == IngressKind.AttemptReport));
        Assert.Equal(receipt, report.AttemptCorrelation!.ReceiptId);
        Assert.Contains(read.Evidence.CanonicalRecords.Values, r => r.Kind == IngressKind.Observation && r.AttemptCorrelation == report.AttemptCorrelation);
        Unchanged(hashes); Export("verified-receipt", result.RunDir);
    }

    [Theory]
    [InlineData("missing")] [InlineData("missing-head")] [InlineData("stale-head")] [InlineData("torn")]
    [InlineData("whole-tail-loss")] [InlineData("uncommitted-tail")] [InlineData("wrong-source")]
    [InlineData("legacy-no-link")] [InlineData("initial-link-missing")] [InlineData("late-link-missing")]
    [InlineData("wrong-position")] [InlineData("wrong-recording")] [InlineData("backwards")]
    [InlineData("wrong-run")] [InlineData("state-missing")] [InlineData("p25-suffix")]
    public void MissingConflictingOrUnsupportedOriginalSourcesCannotPublishAThreeHistoryPoint(string fault)
    {
        string id;
        using (var original = new OriginalRun(_root, terminal: true)) id = original.Kernel.RunId;
        LegacyResourceFixture.ConvertToV3(Path.Combine(_root, "resource.journal.jsonl"));
        var evidencePath = Path.Combine(_root, "evidence.journal.jsonl");
        if (fault == "missing") File.Delete(evidencePath);
        if (fault == "missing-head") File.Delete(evidencePath + ".head");
        if (fault == "stale-head") { var head = JsonNode.Parse(File.ReadAllText(evidencePath + ".head"))!; head["sequence"] = 999; File.WriteAllText(evidencePath + ".head", head.ToJsonString() + "\n"); }
        if (fault == "torn") File.WriteAllBytes(evidencePath, File.ReadAllBytes(evidencePath)[..^5]);
        if (fault == "whole-tail-loss") { var lines = File.ReadAllLines(evidencePath); File.WriteAllText(evidencePath, string.Join("\n", lines[..^1]) + "\n"); }
        if (fault == "uncommitted-tail")
        {
            var frame = JsonNode.Parse(File.ReadLines(evidencePath).Last())!; var row = JsonNode.Parse(frame["payload"]!.GetValue<string>())!;
            var sequence = frame["sequence"]!.GetValue<int>() + 1; row["sequence"] = sequence;
            frame["previous"] = frame["hash"]!.GetValue<string>(); frame["sequence"] = sequence; frame["payload"] = row.ToJsonString();
            frame["hash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(frame["previous"]!.GetValue<string>() + "\n"
                + frame["recordingId"]!.GetValue<string>() + "\n" + sequence + "\n" + row.ToJsonString())));
            File.AppendAllText(evidencePath, frame.ToJsonString() + "\n");
        }
        if (fault == "wrong-source")
        {
            var other = Path.Combine(_root, "other"); using (var original = new OriginalRun(other, terminal: true)) { }
            foreach (var suffix in new[] { "", ".head" }) File.Copy(Path.Combine(other, "evidence.journal.jsonl" + suffix), evidencePath + suffix, true);
        }
        if (fault is "legacy-no-link" or "initial-link-missing" or "late-link-missing" or "wrong-position" or "wrong-recording" or "backwards")
            RewriteResources(_root, row =>
            {
                if (fault == "legacy-no-link" || fault == "initial-link-missing" && row["sequence"]!.GetValue<int>() == 1
                    || fault == "late-link-missing" && row["phase"]!.GetValue<int>() == 2) row["evidenceHistoryPosition"] = null;
                if (fault == "wrong-position") row["evidenceHistoryPosition"]!["sequence"] = 999;
                if (fault == "wrong-recording") row["evidenceHistoryPosition"]!["recordingId"] = "11111111111111111111111111111111";
                if (fault == "backwards" && row["phase"]!.GetValue<int>() == 2) row["evidenceHistoryPosition"]!["sequence"] = 0;
            });
        if (fault == "wrong-run") id = "wrong-run";
        if (fault == "state-missing") File.Delete(Path.Combine(_root, "run-state.journal.jsonl"));
        if (fault == "p25-suffix") File.AppendAllText(Path.Combine(_root, "consultations.journal.jsonl"), "unaccounted\n");
        var read = new FileRunRecoveryArchive(_root).InspectEvidenceRecovery(id);
        Assert.False(read.HistoriesConsistent); Assert.Null(read.Point); Assert.False(read.ContinuationSupported);
        if (fault is "legacy-no-link" or "wrong-position" or "wrong-recording")
        { Assert.True(read.RunAndResources.StateAndResourcesConsistent); Assert.NotNull(read.Evidence); }
        if (fault == "legacy-no-link") Assert.Equal("resource-evidence-link-missing", read.Reason);
        if (fault == "state-missing") Assert.NotNull(read.Evidence);
        Export("bad-" + fault, _root);
    }

    [Fact]
    public void AnActiveLedgerCannotCertifyAHistoricalPoint()
    {
        using var original = new OriginalRun(_root, empty: true);
        var read = new FileRunRecoveryArchive(_root).InspectRecovery(original.Kernel.RunId);
        var result = RunResourceAccounting.InspectEvidenceRecovery(read.Run!, read.Resources, original.Ledger);
        Assert.False(result.HistoriesConsistent); Assert.Null(result.Point);
        Assert.Equal("evidence-history-not-read-only", result.Reason);
    }


    [Fact]
    public void OriginalLegacyNativeArchiveRetainsItsTwoHistoryReadWithoutClaimingAThreeHistoryPoint()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyEvidenceV1");
        foreach (var file in Directory.GetFiles(fixture)) File.Copy(file, Path.Combine(_root, Path.GetFileName(file)));
        var archive = new FileRunRecoveryArchive(_root); var id = archive.Read().Run.RunId; var hashes = Hashes(_root);
        Assert.True(archive.InspectRecovery(id).StateAndResourcesConsistent);
        var read = archive.InspectEvidenceRecovery(id);
        Assert.False(read.HistoriesConsistent); Assert.Null(read.Point); Assert.NotNull(read.Evidence);
        Assert.Equal("resource-evidence-link-missing", read.Reason); Unchanged(hashes); Export("native-legacy", _root);
    }

    [Theory]
    [InlineData("evidence-linked", true, false)] [InlineData("evidence-derived", false, false)]
    [InlineData("evidence-empty", true, false)] [InlineData("pending", false, false)]
    [InlineData("terminal-linked", true, true)]
    public async Task ActualKillAndDifferentReaderProcessCompareTheThreeOriginalEndpoints(string mode, bool consistent, bool terminal)
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
            File.WriteAllText(Path.Combine(_root, "three-kill-observed.json"), JsonSerializer.Serialize(new { ProcessId = writer.Id, writer.ExitCode, writer.HasExited }));
            var hashes = Hashes(_root);
            using var reader = Start("read-three", _root, ready.GetProperty("RunId").GetString()!);
            try
            {
                await reader.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(0, reader.ExitCode);
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "three-reader-result.json"))); var read = doc.RootElement;
                Assert.NotEqual(writer.Id, read.GetProperty("ProcessId").GetInt32()); Assert.Equal(consistent, read.GetProperty("HistoriesConsistent").GetBoolean());
                Assert.Equal(terminal, read.GetProperty("IsTerminal").GetBoolean()); Assert.True(read.GetProperty("RunIsReadOnly").GetBoolean());
                Assert.True(read.GetProperty("EvidenceIsReadOnly").GetBoolean()); Assert.False(read.GetProperty("ContinuationSupported").GetBoolean());
                Assert.Equal(ready.GetProperty("EvidenceAdmissionCount").GetInt32(), read.GetProperty("AdmissionCount").GetInt32());
                Assert.Equal(ready.GetProperty("EvidenceCanonicalCount").GetInt32(), read.GetProperty("CanonicalCount").GetInt32());
                if (mode == "evidence-derived")
                { Assert.True(read.GetProperty("StateAndResourcesConsistent").GetBoolean()); Assert.Equal("resource-evidence-position-mismatch", read.GetProperty("Reason").GetString()); }
                if (consistent)
                {
                    Assert.Equal(read.GetProperty("EvidencePosition").GetRawText(), read.GetProperty("Point").GetProperty("EvidenceHistory").GetRawText());
                    Assert.Equal(read.GetProperty("RunPosition").GetRawText(), read.GetProperty("Point").GetProperty("RunAndResources").GetProperty("RunHistory").GetRawText());
                }
                Unchanged(hashes); Export("kill-" + mode, _root);
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

    private static void RewriteResources(string directory, Action<JsonNode> transform)
    {
        var path = Path.Combine(directory, "resource.journal.jsonl"); LegacyResourceFixture.ConvertToV3(path);
        var frames = File.ReadAllLines(path).Select(s => JsonNode.Parse(s)!).ToArray(); var previous = "";
        foreach (var frame in frames)
        {
            var row = JsonNode.Parse(frame["payload"]!.GetValue<string>())!; transform(row);
            frame["payload"] = row.ToJsonString(); frame["previous"] = previous;
            previous = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previous + "\n" + row.ToJsonString()
                + "\n" + frame["sources"]!.ToJsonString() + "\n" + frame["recordingId"]!.GetValue<string>())));
            frame["hash"] = previous;
        }
        File.WriteAllText(path, string.Join("\n", frames.Select(f => f.ToJsonString())) + "\n");
        var headPath = path + ".head"; var head = JsonNode.Parse(File.ReadAllText(headPath))!; head["hash"] = previous;
        File.WriteAllText(headPath, head.ToJsonString() + "\n");
    }
    private static Dictionary<string, string> Hashes(string directory) => Sources.ToDictionary(n => Path.Combine(directory, n),
        n => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, n)))));
    private static void Unchanged(Dictionary<string, string> hashes)
    { foreach (var (path, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))); }
    private static void Export(string label, string directory)
    {
        if (Environment.GetEnvironmentVariable("TRIPLE_RECOVERY_EVIDENCE_DIR") is not { Length: > 0 } output) return;
        var target = Path.Combine(output, label); Directory.CreateDirectory(target);
        foreach (var path in Directory.GetFiles(directory)) File.Copy(path, Path.Combine(target, Path.GetFileName(path)), true);
    }
}
