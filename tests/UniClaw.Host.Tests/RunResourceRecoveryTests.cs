using UniClaw.Host.SettingsCoverage;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class RunResourceRecoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("resource-recovery-").FullName;
    public void Dispose() => Directory.Delete(_root, true);

    internal static SettingsCoverageRunner.RunResult Run(string root, Action<AgentDecisionContext>? observe = null)
    {
        var feed = new RunResourceAccountingTests.Source();
        return SettingsCoverageRunner.Run(root, new("synthetic-device",
            new("synthetic-device", "unused", "/unused", "/unused/python"),
            new("1", new("original identity archive", "offline", false, false),
                new(4, 6, 0, 3, 1), new(true, "configured", 0, 0, false, 0), ["Unreached row"],
                new(true, true, true, true), "synthetic:resources", "unused", null),
            context => { observe?.Invoke(context); return new AgentDecision.Blocked(context.DecisionId, "synthetic stop"); },
            ViewportWidth: 100, ViewportHeight: 100, FeedNext: feed.Next,
            CurrentCaptureId: () => feed.CaptureId, EffectDriver: feed, UseCoverageDirector: false));
    }

    [Fact]
    public void OriginalProductionRunnerWritesIndependentIdentityArchive()
    {
        var result = Run(_root, _ => Assert.Single(Directory.GetFiles(_root, "run-identity.json", SearchOption.AllDirectories)));
        Assert.True(File.Exists(Path.Combine(result.RunDir, "run-identity.json")),
            "The original accepted Owner identity must survive separately from the resource journal.");
    }
    [Fact]
    public void FreshReaderRecoversOriginalIdentityWithoutOriginalDriverObject()
    {
        string? originalRunId = null;
        var result = Run(_root, context => originalRunId = context.RunId);
        var files = Directory.GetFiles(result.RunDir).ToDictionary(path => Path.GetFileName(path)!,
            path => System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        var archive = new FileRunRecoveryArchive(result.RunDir);
        for (var i = 0; i < 2; i++)
        {
            var read = archive.Inspect(originalRunId!);
            Assert.True(read.AccountingComplete, read.Reason);
            Assert.Equal(originalRunId, read.Identity!.RunId);
            Assert.Equal(0, read.Confirmed!.StepsConsumed); Assert.Equal(1, read.Confirmed.RoundsConsumed);
            Assert.Equal(4, read.StepsRemaining); Assert.Equal(5, read.RoundsRemaining);
            Assert.False(read.ContinuationSupported);
        }
        foreach (var (file, hash) in files)
            Assert.Equal(hash, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(result.RunDir, file!))));
        Export("original-read", result.RunDir);
    }

    [Fact]
    public void IdenticalContractRunsCannotSwapTheirOriginalArchive()
    {
        string? runId = null;
        var first = Run(Path.Combine(_root, "one"), context => runId = context.RunId);
        var second = Run(Path.Combine(_root, "two"));
        File.Copy(Path.Combine(second.RunDir, "run-identity.json"), Path.Combine(first.RunDir, "run-identity.json"), true);
        var read = new FileRunRecoveryArchive(first.RunDir).Inspect(runId!);
        Assert.False(read.AccountingComplete); Assert.Equal("resource-recording-mismatch", read.Reason);
        Assert.Equal(1, read.Confirmed!.RoundsConsumed); Assert.Null(read.RoundsRemaining);
        Export("swapped-archive", first.RunDir);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("torn")]
    [InlineData("corrupt")]
    [InlineData("wrong-run")]
    [InlineData("source-suffix")]
    public void UnavailableOriginalIdentityOrSourceNeverCertifiesBalance(string fault)
    {
        string? runId = null;
        var result = Run(_root, context => runId = context.RunId);
        var path = Path.Combine(result.RunDir, "run-identity.json");
        if (fault == "missing") File.Delete(path);
        if (fault == "torn") File.WriteAllBytes(path, File.ReadAllBytes(path)[..^5]);
        if (fault == "corrupt") File.AppendAllText(path, "invalid\n");
        if (fault == "wrong-run") runId = "another-run";
        if (fault == "source-suffix") File.AppendAllText(Path.Combine(result.RunDir, "consultations.journal.jsonl"), "invalid\n");
        var read = new FileRunRecoveryArchive(result.RunDir).Inspect(runId!);
        Assert.False(read.AccountingComplete); Assert.Null(read.RoundsRemaining);
        Assert.Equal(1, read.Confirmed!.RoundsConsumed);
        Export(fault, result.RunDir);
    }

    [Theory]
    [InlineData("view")]
    [InlineData("task")]
    [InlineData("policy")]
    [InlineData("origin")]
    [InlineData("format")]
    [InlineData("recording")]
    public void InternallyConflictingArchiveDoesNotMintAQualifiedBinding(string fault)
    {
        string? runId = null;
        var result = Run(_root, context => runId = context.RunId);
        var path = Path.Combine(result.RunDir, "run-identity.json");
        var frame = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var payload = System.Text.Json.Nodes.JsonNode.Parse(frame["payload"]!.GetValue<string>())!;
        if (fault == "view") payload["run"]!["version"] = "different-view-version";
        if (fault == "task") payload["run"]!["taskInstanceId"] = "different-task";
        if (fault == "policy") payload["policy"]!["renewalThresholdRounds"] = 999;
        if (fault == "origin") payload["run"]!["consultationsOrigin"] = 1;
        if (fault == "format") payload["schema"] = "unsupported-archive-version";
        if (fault == "recording") payload["recordingId"] = Guid.NewGuid().ToString("N");
        var payloadText = payload.ToJsonString();
        frame["payload"] = payloadText;
        frame["hash"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            frame["consultationFile"]!.GetValue<string>() + "\n" + payloadText)));
        File.WriteAllText(path, frame.ToJsonString() + "\n");
        var read = new FileRunRecoveryArchive(result.RunDir).Inspect(runId!);
        Assert.False(read.AccountingComplete); Assert.Null(read.RoundsRemaining);
        Assert.Equal(1, read.Confirmed!.RoundsConsumed);
        Export("conflicting-" + fault, result.RunDir);
    }

    private static void Export(string label, string directory)
    {
        var output = Environment.GetEnvironmentVariable("RECOVERY_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(output)) return;
        var target = Path.Combine(output, label); Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(directory)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
    }

}
