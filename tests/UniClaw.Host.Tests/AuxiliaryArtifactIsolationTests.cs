using System.Text.Json;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class AuxiliaryArtifactIsolationTests
{
    [Fact]
    public async Task TraceArtifactFailureDoesNotChangeCoverageRunResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "aux-artifact-isolation-" + Guid.NewGuid().ToString("N"));
        var baselineRoot = Path.Combine(root, "baseline");
        var failureRoot = Path.Combine(root, "trace-directory");
        var baselineSource = new RunResourceAccountingTests.Source();
        var failureSource = new RunResourceAccountingTests.Source();
        try
        {
            var baseline = await SettingsCoverageRunner.RunAsync(baselineRoot,
                CreateOptions(baselineSource, context => new AgentDecision.Blocked(context.DecisionId, "synthetic stop")));
            var withArtifactFailure = await SettingsCoverageRunner.RunAsync(failureRoot,
                CreateOptions(failureSource, context =>
                {
                    var runDir = Directory.GetDirectories(failureRoot, "run-*").Single();
                    Directory.CreateDirectory(Path.Combine(runDir, "trace.json"));
                    return new AgentDecision.Blocked(context.DecisionId, "synthetic stop");
                }));

            Assert.Equal(baseline.Status, withArtifactFailure.Status);
            Assert.Equal(baseline.Reason, withArtifactFailure.Reason);
            Assert.Equal(baseline.Outcome, withArtifactFailure.Outcome);
            Assert.Equal(baseline.Report.Status, withArtifactFailure.Report.Status);
            Assert.Equal(baseline.Report.CoverageRate, withArtifactFailure.Report.CoverageRate);
            Assert.Equal(baseline.Report.StepSuccessRate, withArtifactFailure.Report.StepSuccessRate);
            Assert.Equal(baseline.Report.UncoveredItems, withArtifactFailure.Report.UncoveredItems);
            Assert.Equal(baseline.Report.ConsultRounds, withArtifactFailure.Report.ConsultRounds);
            Assert.Equal(0, baselineSource.Deliveries);
            Assert.Equal(0, failureSource.Deliveries);
            Assert.True(File.Exists(Path.Combine(withArtifactFailure.RunDir, "resource.journal.jsonl")));
            using var facts = JsonDocument.Parse(File.ReadAllText(Path.Combine(withArtifactFailure.RunDir, "facts.json")));
            var exports = facts.RootElement.GetProperty("diagnostics").GetProperty("exports");
            Assert.Equal(1, exports.GetProperty("failed").GetInt32());
            Assert.Contains(exports.GetProperty("errors").EnumerateArray(), error =>
                error.GetString()!.StartsWith("trace.json:", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(withArtifactFailure.RunDir, "coverage-report.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AuxiliaryArtifactByteLimitDropsTraceAndPreservesCoreResults()
    {
        var root = Path.Combine(Path.GetTempPath(), "aux-artifact-limit-" + Guid.NewGuid().ToString("N"));
        var source = new RunResourceAccountingTests.Source();
        try
        {
            var result = await SettingsCoverageRunner.RunAsync(root,
                CreateOptions(source, context => new AgentDecision.Blocked(context.DecisionId, "synthetic stop"),
                    new AuxiliaryArtifactLimits { MaxArtifactBytes = 1 }));

            Assert.Equal(0, source.Deliveries);
            Assert.True(File.Exists(Path.Combine(result.RunDir, "facts.json")));
            Assert.True(File.Exists(Path.Combine(result.RunDir, "coverage-report.json")));
            Assert.False(File.Exists(Path.Combine(result.RunDir, "trace.json")));
            using var facts = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "facts.json")));
            var exports = facts.RootElement.GetProperty("diagnostics").GetProperty("exports");
            Assert.True(exports.GetProperty("dropped").GetInt32() > 0);
            Assert.Contains(exports.GetProperty("errors").EnumerateArray(), error =>
                error.GetString()!.Contains("trace.json:byte-limit", StringComparison.Ordinal));
            Assert.Equal(result.Report.Status, facts.RootElement.GetProperty("coverageStatus").GetString());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static SettingsCoverageRunner.Options CreateOptions(
        RunResourceAccountingTests.Source source,
        Func<AgentDecisionContext, AgentDecision?> consult,
        AuxiliaryArtifactLimits? limits = null) => new(
        "synthetic-device",
        new("synthetic-device", "unused", "/unused", "/unused/python"),
        new("1", new("auxiliary artifact isolation", "offline", false, false),
            new(4, 6, 0, 3, 1), new(true, "configured", 0, 0, false, 0), ["Unreached row"],
            new(true, true, true, true), "synthetic:resources", "unused", null),
        consult,
        ViewportWidth: 100,
        ViewportHeight: 100,
        FeedNext: source.Next,
        CurrentCaptureId: () => source.CaptureId,
        EffectDriver: source,
        UseCoverageDirector: false,
        AuxiliaryArtifactLimits: limits);
}
