using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Benchmark;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

public sealed class SemanticBenchmarkTests
{
    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:dev",
        ImmutableArray.Create("Enable demo mode", "Show demo mode"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch", "switch:True"));

    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    [Fact]
    public async Task BenchmarkRunner_ProducesStandardReport()
    {
        var provider = new FastSemanticContainerIdentityProvider(
            new InMemoryVectorSemanticIndex(ImmutableArray.Create(DeveloperOptionsPattern)));

        var corpus = new SemanticCorpus(
            "DeveloperOptions-v1",
            ImmutableArray.Create(
                new SemanticCase(
                    "dev-001",
                    Obs(1,
                        new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                        new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                        new ObservedElement("Automatic system updates", true, 2, null, "switch")),
                    "DeveloperOptions",
                    "DeveloperOptions",
                    SemanticCaseSource.Regression,
                    SemanticCaseDifficulty.Easy)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                }));

        var options = new SemanticOptions
        {
            Evaluation = new SemanticEvaluationOptions { RecoveryConfidenceThreshold = 0.6 },
            Benchmark = new SemanticBenchmarkOptions { MeasurementRuns = 1 },
        };

        var runner = new SemanticBenchmarkRunner();
        var report = await runner.RunAsync(provider, corpus, options);

        Assert.Equal("FastSemanticContainerIdentityProvider", report.Provider);
        Assert.Equal("DeveloperOptions-v1", report.CorpusId);
        Assert.Single(report.CaseResults);
        Assert.True(report.Metrics.Performance.SampleCount > 0);
    }
}