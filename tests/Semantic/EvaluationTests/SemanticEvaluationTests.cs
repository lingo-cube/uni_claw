using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Evaluation;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Semantic.Tests.EvaluationTests;

public sealed class SemanticEvaluationTests
{
    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:dev",
        ImmutableArray.Create("Enable demo mode", "Show demo mode"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch", "switch:True"));

    private static Observation Obs(long seq, params ObservedElement[] elements) =>
        new(elements.ToImmutableArray(), "com.android.settings", seq);

    private static FastSemanticContainerIdentityProvider Provider() =>
        new(new InMemoryVectorSemanticIndex(ImmutableArray.Create(DeveloperOptionsPattern)));

    private static SemanticCorpus Corpus() =>
        new(
            "DeveloperOptions-v1",
            ImmutableArray.Create(
                new SemanticCase(
                    "dev-positive-001",
                    Obs(1,
                        new ObservedElement("Enable demo mode", null, 0, null, "menu_item"),
                        new ObservedElement("Show demo mode", null, 1, null, "menu_item"),
                        new ObservedElement("Automatic system updates", true, 2, null, "switch")),
                    "DeveloperOptions",
                    "DeveloperOptions",
                    SemanticCaseSource.RealWorld,
                    SemanticCaseDifficulty.Medium)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                },
                new SemanticCase(
                    "dev-negative-001",
                    Obs(2, new ObservedElement("Unknown row", null, 0, null, "menu_item")),
                    "None",
                    null,
                    SemanticCaseSource.Synthetic,
                    SemanticCaseDifficulty.Hard)
                {
                    PreviousVerifiedIdentity = "DeveloperOptions",
                }));

    [Fact]
    public async Task Evaluator_ComputesAccuracyAndSafety()
    {
        var evaluator = new SemanticEvaluator();
        var options = new SemanticOptions
        {
            Evaluation = new SemanticEvaluationOptions { RecoveryConfidenceThreshold = 0.6, TopK = 1 },
            Benchmark = new SemanticBenchmarkOptions { MeasurementRuns = 1 },
        };

        var metrics = await evaluator.EvaluateAsync(
            new SemanticEvaluationContext(Provider(), Corpus(), options));

        Assert.Equal(1.0, metrics.Retrieval.Top1Accuracy);
        Assert.Equal(1.0, metrics.Retrieval.TopKRecall);
        Assert.Equal(0.0, metrics.Safety.FalseRecoveryRate);
        Assert.Equal(0.0, metrics.Safety.FalsePositiveRate);
        Assert.True(metrics.Performance.SampleCount > 0);
    }
}