using System.Text.Json;
using UniClaw.Core.Observability;
using UniClaw.Host.Artifacts;
using Xunit;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Task 7.1 — TraceRunLoader / TraceRun aggregate / RunDiffer unit tests, driven by the
/// snapshot fixtures (success/ + failure/). The success snapshot predates span recording
/// (35 execution + 11 state_transition + 1 page_transition, 0 spans); the failure snapshot
/// carries 81 span records. Assertions follow what the fixtures actually contain.
/// </summary>
public sealed class TraceRunTests : IClassFixture<TraceRunFixture>
{
    private readonly TraceRunFixture _fixture;

    public TraceRunTests(TraceRunFixture fixture) => _fixture = fixture;

    [Fact]
    public void LoadAsync_SuccessRun_LoadsAllArtifacts()
    {
        var run = _fixture.SuccessRun;

        Assert.NotNull(run.Manifest);
        Assert.NotNull(run.Result);
        // The success snapshot predates span recording — the replayed service is
        // non-empty via execution records, spans are zero.
        Assert.Empty(run.Trace.GetAllSpans());
        Assert.NotEmpty(run.Trace.GetExecutions());
        Assert.NotNull(run.StepAssets);
        Assert.Equal("success", run.Status);
    }

    [Fact]
    public void LoadAsync_FailureRun_LoadsAllArtifacts()
    {
        var run = _fixture.FailureRun;

        Assert.NotNull(run.Manifest);
        Assert.NotNull(run.Result);
        Assert.NotEmpty(run.Trace.GetAllSpans());
        Assert.NotEmpty(run.Trace.GetExecutions());
        Assert.NotNull(run.StepAssets);
        Assert.Equal("failure", run.Status);
    }

    [Fact]
    public void LoadAsync_SuccessRun_HasCorrectMetadata()
    {
        var run = _fixture.SuccessRun;

        Assert.Equal("20260801T124355012Z-efb7da591f864be", run.RunId);
        Assert.Equal("success", run.Status);
        Assert.Equal("locate-one-item", run.ScenarioId);
        Assert.Equal("emulator-5554", run.DeviceSerial);
        Assert.Equal("com.android.settings", run.AppPackage);
        Assert.Equal("sensenova", run.ProviderId);
    }

    [Fact]
    public void LoadAsync_FailureRun_HasFailureStatus()
    {
        var run = _fixture.FailureRun;

        Assert.Equal("failure", run.Status);
        Assert.Equal("target_page_identity_not_verified", run.Result?.CompletionReason);
    }

    [Fact]
    public async Task LoadAsync_MissingDirectory_ReturnsEmptyTraceRun()
    {
        var run = await TraceRunLoader.LoadAsync(
            Path.Combine(Path.GetTempPath(), $"missing-run-{Guid.NewGuid():N}"));

        Assert.Null(run.Manifest);
        Assert.Null(run.Result);
        Assert.Empty(run.Trace.GetAllSpans());
        Assert.Empty(run.StepAssets);
        Assert.Equal("unknown", run.Status);
    }

    [Fact]
    public async Task LoadAsync_MissingResultJson_DegradesGracefully()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(), $"manifest-only-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.Copy(
                Path.Combine(TraceRunFixture.FixturePath("success"), "manifest.json"),
                Path.Combine(tempDir, "manifest.json"));
            TestRunFactory.CopyDirectory(
                Path.Combine(TraceRunFixture.FixturePath("success"), "trace"),
                Path.Combine(tempDir, "trace"));

            var run = await TraceRunLoader.LoadAsync(tempDir);

            // Manifest survives; result degrades to null without crashing.
            Assert.Null(run.Result);
            Assert.NotNull(run.Manifest);
            Assert.NotNull(run.Trace);
            // Without result.json the loader cannot resolve the runId-keyed trace path
            // (tracePath comes from result.TracePath) — the service is empty, not broken.
            Assert.Empty(run.Trace.GetAllSpans());
            Assert.Empty(run.Trace.GetExecutions());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_IssuesJsonl_Present_LoadsIssueRecords()
    {
        // trace-issue-evidence (D-1/D-2): issues.jsonl lines are serialized
        // RunIssue records (Host's camelCase writer) — the loader must expose
        // them with all fields intact, step number when recorded.
        var tempDir = Path.Combine(Path.GetTempPath(), $"issues-run-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var issues = new[]
            {
                new RunIssue(
                    "1", "issue-1", "fp-001",
                    "verification", "completion", "error",
                    "target_page_identity_not_verified: Post-action page identity '<empty>' did not match the scenario success identities.",
                    "run-1", 3, ["steps/0003/after.png"],
                    DateTimeOffset.UtcNow, 1, null, "open"),
                new RunIssue(
                    "1", "issue-2", "fp-002",
                    "verification", "completion", "error",
                    "enumeration_evidence_unavailable: trace/safety journals missing.",
                    "run-1", null, [],
                    DateTimeOffset.UtcNow, 1, null, "open"),
            };
            var json = string.Join(
                '\n', issues.Select(i => JsonSerializer.Serialize(i, TestJsonOptions)));
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "issues.jsonl"), json + "\n");

            var run = await TraceRunLoader.LoadAsync(tempDir);

            Assert.Equal(2, run.Issues.Count);
            var first = run.Issues[0];
            Assert.Equal("fp-001", first.Fingerprint);
            Assert.Equal("verification", first.Category);
            Assert.Equal("completion", first.Phase);
            Assert.Equal("error", first.Severity);
            Assert.Equal(3, first.StepNumber);
            Assert.Contains("did not match", first.Summary, StringComparison.Ordinal);
            Assert.Equal("run-1", first.RunId);
            var second = run.Issues[1];
            Assert.Equal("fp-002", second.Fingerprint);
            Assert.Null(second.StepNumber);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_IssuesJsonl_Missing_YieldsEmptyCollection()
    {
        // trace-issue-evidence spec: absence of issues.jsonl must not fail the
        // load — the issues collection is empty.
        var tempDir = Path.Combine(Path.GetTempPath(), $"no-issues-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);

            var run = await TraceRunLoader.LoadAsync(tempDir);

            Assert.NotNull(run.Issues);
            Assert.Empty(run.Issues);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_IssuesJsonl_MalformedLine_Skipped()
    {
        // trace-issue-evidence spec: a malformed line is excluded from the
        // issues collection while the load still succeeds.
        var tempDir = Path.Combine(Path.GetTempPath(), $"bad-issues-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var valid = JsonSerializer.Serialize(
                new RunIssue(
                    "1", "issue-1", "fp-001",
                    "verification", "completion", "error",
                    "target_page_identity_not_verified: page identity mismatch.",
                    "run-1", 3, [], DateTimeOffset.UtcNow, 1, null, "open"),
                TestJsonOptions);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "issues.jsonl"),
                valid + "\n{this is not valid json\n");

            var run = await TraceRunLoader.LoadAsync(tempDir);

            var issue = Assert.Single(run.Issues);
            Assert.Equal("fp-001", issue.Fingerprint);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TraceRun_MetadataHelpers_FallbackToUnknown()
    {
        var emptyTrace = new InMemoryTraceService(new InMemoryTraceStorage());
        var run = new TraceRun(
            "/runs/run-1", null, null, emptyTrace, Array.Empty<StepAsset>(),
            Array.Empty<RunIssue>());

        Assert.Equal("run-1", run.RunId);
        Assert.Equal("unknown", run.Status);
        Assert.Equal("unknown", run.TaskId);
        Assert.Equal("unknown", run.Purpose);
        Assert.Equal("unknown", run.ScenarioId);
        Assert.Equal("unknown", run.DeviceSerial);
        Assert.Equal("unknown", run.ProviderId);
        Assert.Equal("unknown", run.AppPackage);
        Assert.Equal(0, run.DurationMs);
        Assert.Null(run.Manifest);
        Assert.Null(run.Result);
    }

    [Fact]
    public void RunDiffer_Diff_DetectsStatusChange()
    {
        var diff = RunDiffer.Diff(_fixture.SuccessRun, _fixture.FailureRun);

        Assert.True(diff.HasDifferences);
        Assert.Equal("Regression: run A was success, run B is failure.", diff.Conclusion);
    }

    [Fact]
    public void RunDiffer_Diff_SameRun_NoDifferences()
    {
        var diff = RunDiffer.Diff(_fixture.SuccessRun, _fixture.SuccessRun);

        Assert.False(diff.HasDifferences);
        Assert.Empty(diff.StepDiffs);
        Assert.Empty(diff.MetricDiffs);
        Assert.Empty(diff.AiComparisons);
        Assert.Equal("No behavioral differences detected between runs.", diff.Conclusion);
    }

    [Fact]
    public void RunDiffer_Diff_DetectsMetricDiffs()
    {
        var diff = RunDiffer.Diff(_fixture.SuccessRun, _fixture.FailureRun);

        Assert.NotEmpty(diff.MetricDiffs);
        Assert.Contains(diff.MetricDiffs, m => m.Metric == "Steps Consumed");
        Assert.Contains(diff.MetricDiffs, m => m.Metric == "Duration (ms)");
        // success fixture: 8 steps / 79117 ms vs failure fixture: 4 steps / 5389 ms
        var steps = diff.MetricDiffs.First(m => m.Metric == "Steps Consumed");
        Assert.Equal(8, steps.ValueA);
        Assert.Equal(4, steps.ValueB);
        Assert.Equal(-4, steps.Delta);
    }

    /// <summary>
    /// Mirrors Host RunAssetStore serialization (camelCase) so test issues.jsonl
    /// lines match the real D-192 writer output.
    /// </summary>
    private static readonly JsonSerializerOptions TestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
