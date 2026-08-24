using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Benchmark;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Evaluation;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Semantic.Tests.BenchmarkTests;

public sealed class DeveloperOptionsBenchmarkTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public DeveloperOptionsBenchmarkTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:developer-options-benchmark",
        ImmutableArray.Create("Developer options", "Enable demo mode", "Show demo mode", "Automatic system updates"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch", "switch:True"));

    private static FastSemanticContainerIdentityProvider Provider() =>
        new(new InMemoryVectorSemanticIndex(ImmutableArray.Create(DeveloperOptionsPattern)));

    private static SemanticOptions Options() =>
        new()
        {
            Evaluation = new SemanticEvaluationOptions { RecoveryConfidenceThreshold = 0.6, TopK = 3 },
            Benchmark = new SemanticBenchmarkOptions { MeasurementRuns = 1 },
        };

    private static SemanticCorpus Corpus() => DeveloperOptionsBenchmarkCorpus.Create();

    [Fact]
    public async Task T1_CorrectRetrieval()
    {
        var provider = Provider();
        var obs = Corpus().Cases.Single(c => c.CaseId == "dev-B-title-offscreen").InputObservation;
        var evidence = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));
        var single = Assert.Single(evidence);
        Assert.Equal("DeveloperOptions", single.Candidate);
    }

    [Fact]
    public async Task T2_TopKCalculation()
    {
        var evaluator = new SemanticEvaluator();
        var metrics = await evaluator.EvaluateAsync(new SemanticEvaluationContext(Provider(), Corpus(), Options()));

        Assert.True(metrics.Retrieval.Top1Accuracy >= 0.8);
        Assert.True(metrics.Retrieval.Top3Recall >= 0.8);
        Assert.True(metrics.Retrieval.Top5Recall >= 0.8);

        _output.WriteLine(
            $"Top1={metrics.Retrieval.Top1Accuracy:F4} Top3={metrics.Retrieval.Top3Recall:F4} Top5={metrics.Retrieval.Top5Recall:F4} TopK={metrics.Retrieval.TopKRecall:F4} FalseRecovery={metrics.Safety.FalseRecoveryRate:F4} FalsePositive={metrics.Safety.FalsePositiveRate:F4} MeanConf={metrics.Confidence.MeanConfidence:F4} CalErr={metrics.Confidence.CalibrationError:F4} Acc={metrics.Confidence.Accuracy:F4}");
    }

    [Fact]
    public async Task T3_FalseRecoveryDetection()
    {
        var evaluator = new SemanticEvaluator();
        var metrics = await evaluator.EvaluateAsync(new SemanticEvaluationContext(Provider(), Corpus(), Options()));

        Assert.Equal(0.0, metrics.Safety.FalseRecoveryRate);
    }

    [Fact]
    public async Task T4_ConfidenceEvaluation()
    {
        var evaluator = new SemanticEvaluator();
        var metrics = await evaluator.EvaluateAsync(new SemanticEvaluationContext(Provider(), Corpus(), Options()));

        Assert.True(metrics.Confidence.MeanConfidence >= 0.0);
        Assert.True(metrics.Confidence.CalibrationError >= 0.0);
        Assert.True(metrics.Confidence.Accuracy >= 0.0);
    }

    [Fact]
    public async Task T5_LatencyMeasurement()
    {
        var evaluator = new SemanticEvaluator();
        var metrics = await evaluator.EvaluateAsync(new SemanticEvaluationContext(Provider(), Corpus(), Options()));

        Assert.True(metrics.Performance.SampleCount > 0);
        Assert.True(metrics.Performance.P50Ms >= 0.0);
        Assert.True(metrics.Performance.P95Ms >= 0.0);
        Assert.True(metrics.Performance.P99Ms >= 0.0);

        _output.WriteLine(
            $"P50={metrics.Performance.P50Ms:F4}ms P95={metrics.Performance.P95Ms:F4}ms P99={metrics.Performance.P99Ms:F4}ms samples={metrics.Performance.SampleCount}");
    }

    [Fact]
    public async Task T6_EmptyVectorResult()
    {
        var provider = Provider();
        var obs = Corpus().Cases.Single(c => c.CaseId == "dev-D-wrong-page").InputObservation;
        var evidence = await provider.ResolveAsync(new ObservationContext(obs, "DeveloperOptions"));
        Assert.Empty(evidence);
    }

    [Fact]
    public async Task T7_RegressionCaseLoading()
    {
        var corpus = Corpus();
        var report = await new SemanticBenchmarkRunner().RunAsync(Provider(), corpus, Options());

        Assert.Equal("DeveloperOptions-v1", report.CorpusId);
        Assert.Equal(5, report.CaseResults.Length);
        Assert.Contains(report.CaseResults, r => r.CaseId == "dev-A-title-visible");
        Assert.Contains(report.CaseResults, r => r.CaseId == "dev-E-similar-page");
    }
}