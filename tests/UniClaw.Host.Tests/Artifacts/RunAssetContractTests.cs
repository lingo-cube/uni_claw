using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Artifacts;

public sealed class RunAssetContractTests : IDisposable
{
    private const string Secret = "top-secret-provider-token";
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-assets-{Guid.NewGuid():N}");

    private static readonly ScenarioSnapshot Snapshot =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"));

    [Fact]
    public async Task CreateAsync_IsAtomicIsolatedAndSelfDescribing()
    {
        var store = new RunAssetStore();

        var first = await store.CreateAsync(
            _root,
            Snapshot,
            new { planId = "plan-1" },
            Input("run-one"));
        var second = await store.CreateAsync(
            _root,
            Snapshot,
            new { planId = "plan-2" },
            Input("run-two"));

        Assert.NotEqual(first.RunDirectory, second.RunDirectory);
        Assert.True(File.Exists(Path.Combine(first.RunDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(first.RunDirectory, "scenario.snapshot.json")));
        Assert.True(File.Exists(Path.Combine(first.RunDirectory, "plan.json")));
        Assert.True(Directory.Exists(Path.Combine(first.RunDirectory, "assets", "run-one", "steps")));
        Assert.True(Directory.Exists(Path.Combine(first.RunDirectory, "trace", "run-one")));
        Assert.True(File.Exists(Path.Combine(first.RunDirectory, "issues.jsonl")));
        Assert.True(File.Exists(Path.Combine(first.RunDirectory, "result.json")));
        Assert.Empty(Directory.EnumerateDirectories(
            Path.GetDirectoryName(first.RunDirectory)!,
            "*.creating-*"));
    }

    [Fact]
    public async Task CreateAsync_RefusesToOverwriteAnExistingRun()
    {
        var store = new RunAssetStore();
        await store.CreateAsync(
            _root,
            Snapshot,
            new { },
            Input("same-run"));

        await Assert.ThrowsAsync<IOException>(
            () => store.CreateAsync(
                _root,
                Snapshot,
                new { },
                Input("same-run")));
    }

    [Fact]
    public async Task StepEvidence_PreservesCausalOrderAndExplicitPartialFailure()
    {
        var session = await CreateSessionAsync("causal-run");
        var step = await session.BeginStepAsync(1, "before-fingerprint");
        await step.WriteBeforeAsync(
            new byte[] { 1, 2, 3 },
            "<hierarchy />");
        await step.WriteAnalysisAsync(new { page = "Settings" }, "success");
        await step.WriteStepPlanAsync<object>(
            null,
            "missing",
            "analysis did not produce a trusted target");
        await step.WriteVerificationAsync<object>(
            null,
            "not_attempted",
            "no action was attempted");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.BeginStepAsync(3, "skipped-step"));

        var stepDirectory = Path.Combine(
            session.RunDirectory,
            "assets",
            "causal-run",
            "steps",
            "0001");
        Assert.True(File.Exists(Path.Combine(stepDirectory, "before.png")));
        using var plan = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(stepDirectory, "step-plan.json")));
        Assert.Equal("causal-run", plan.RootElement.GetProperty("runId").GetString());
        Assert.Equal(1, plan.RootElement.GetProperty("stepNumber").GetInt32());
        Assert.Equal(
            "analysis did not produce a trusted target",
            plan.RootElement.GetProperty("missingReason").GetString());
    }

    [Fact]
    public async Task IssueLog_IsAppendOnlyAndAggregateGroupsFingerprints()
    {
        var session = await CreateSessionAsync("issue-run");
        var first = session.CreateIssue(
            "verification",
            "verify",
            "error",
            "Target page mismatch",
            1,
            ["assets/issue-run/steps/0001/verification.json"]);
        var repeated = first with
        {
            IssueId = "issue-repeat",
            OccurrenceCount = 2,
            RepeatsIssueId = first.IssueId,
        };
        await session.AppendIssueAsync(first);
        await session.AppendIssueAsync(repeated);

        var lines = await File.ReadAllLinesAsync(
            Path.Combine(session.RunDirectory, "issues.jsonl"));
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Contains(first.Fingerprint, line));

        var aggregate = IterationAggregator.Create(
            "aggregate-1",
            [
                Child("run-1", "failure", [first.Fingerprint]),
                Child("run-2", "success", [first.Fingerprint]),
                Child("run-3", "success", []),
            ],
            ["disappeared-fingerprint"]);
        Assert.Equal(2d / 3d, aggregate.SuccessRate, 6);
        Assert.Equal(2, aggregate.LongestConsecutiveSuccesses);
        Assert.Contains(first.Fingerprint, aggregate.NewIssueFingerprints);
        Assert.Contains(first.Fingerprint, aggregate.RepeatedIssueFingerprints);
        Assert.Contains("disappeared-fingerprint", aggregate.DisappearedIssueFingerprints);
    }

    [Fact]
    public async Task FinalResult_RejectsDishonestSuccessAndPersistsCancellation()
    {
        var dishonest = await CreateSessionAsync("dishonest-run");
        await Assert.ThrowsAsync<ArgumentException>(
            () => dishonest.FinalizeAsync(Result("dishonest-run", "success", false)));

        var cancelled = await CreateSessionAsync("cancelled-run");
        await cancelled.FinalizeAsync(
            Result("cancelled-run", "cancelled", false) with
            {
                CompletionReason = "ctrl_c",
            });

        using var json = JsonDocument.Parse(
            await File.ReadAllTextAsync(
                Path.Combine(cancelled.RunDirectory, "result.json")));
        Assert.Equal("cancelled", json.RootElement.GetProperty("status").GetString());
        Assert.False(
            json.RootElement.GetProperty("successCriteriaSatisfied").GetBoolean());
    }

    [Fact]
    public async Task Result_RunLogPath_WrittenToResultJson()
    {
        // 5.1: runLogPath 相对路径写入 result.json — 初始 (CreateAsync) 与
        // finalize 均携带, 对称 TracePath 先例 (schemaVersion 不 bump)。
        var session = await CreateSessionAsync("log-path-run");
        using var initial = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(session.RunDirectory, "result.json")));
        Assert.Equal(
            "trace/log-path-run/run.log",
            initial.RootElement.GetProperty("runLogPath").GetString());

        await session.FinalizeAsync(Result("log-path-run", "cancelled", false));
        using var finalized = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(session.RunDirectory, "result.json")));
        Assert.Equal(
            "trace/log-path-run/run.log",
            finalized.RootElement.GetProperty("runLogPath").GetString());
    }

    [Fact]
    public async Task Redaction_CoversManifestIssueAndStepAssets()
    {
        var store = new RunAssetStore(new AssetRedactor([Secret]));
        var session = await store.CreateAsync(
            _root,
            Snapshot,
            new { modelMetadata = $"Authorization: Bearer {Secret}" },
            Input("redacted-run") with
            {
                Model = $"api_key={Secret}",
            });
        var step = await session.BeginStepAsync(1, "fingerprint");
        await step.WriteBeforeAsync(
            new byte[] { 1 },
            $"<node text=\"{Secret}\" />");
        var issue = session.CreateIssue(
            "provider",
            "analysis",
            "error",
            $"Authorization: Bearer {Secret}",
            1);
        await session.AppendIssueAsync(issue);

        var persisted = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    session.RunDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".json" or ".jsonl" or ".xml")
                .Select(File.ReadAllText));
        Assert.DoesNotContain(Secret, persisted);
        Assert.Contains("[REDACTED]", persisted);
    }

    private async Task<RunAssetSession> CreateSessionAsync(string runId) =>
        await new RunAssetStore().CreateAsync(
            _root,
            Snapshot,
            new { plan = "compiled" },
            Input(runId));

    private static RunManifestInput Input(string runId) =>
        new(
            runId,
            null,
            null,
            "revision",
            "emulator-5554",
            "AOSP API 35",
            "mock",
            "deterministic",
            "mode-a");

    private static RunResult Result(
        string runId,
        string status,
        bool criteriaSatisfied) =>
        new(
            "1",
            runId,
            status,
            "test",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            $"trace/{runId}/trace.jsonl",
            $"trace/{runId}/run.log",
            [],
            criteriaSatisfied,
            [],
            DateTimeOffset.UtcNow);

    private static AggregateChildRun Child(
        string runId,
        string status,
        ImmutableArray<string> issues) =>
        new(
            runId,
            status,
            100,
            1,
            0,
            Snapshot.ScenarioHash,
            Snapshot.PolicyHash,
            issues,
            ImmutableDictionary<string, long>.Empty.Add("analysis", 10));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
