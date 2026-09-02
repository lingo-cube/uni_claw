using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Benchmark;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Evaluation;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Semantic.Infrastructure.Retrieval;
using Xunit;

namespace UniClaw.Semantic.Tests.BenchmarkTests.BackendEvaluationTests;

/// <summary>
/// Backend Evaluation Tests for the separated IVectorSemanticIndex abstraction.
/// Verifies transparent RETRIEVAL backend replacement, accuracy comparison,
/// latency, empty result behavior, failure isolation, and Runtime boundary
/// unchanged. Retrieval backends here never accept candidates — acceptance is
/// the Candidate Policy's job.
/// </summary>
public sealed class BackendEvaluationTests
{
    private sealed class DelegatingVectorSemanticIndex : IVectorSemanticIndex
    {
        private readonly IVectorSemanticIndex _inner;
        public string Name { get; }

        public DelegatingVectorSemanticIndex(IVectorSemanticIndex inner, string name)
        {
            _inner = inner;
            Name = name;
        }

        public IReadOnlyList<SemanticCandidate> Retrieve(EmbeddingVector queryVector) => _inner.Retrieve(queryVector);
    }

    private sealed class ThrowingVectorSemanticIndex : IVectorSemanticIndex
    {
        public IReadOnlyList<SemanticCandidate> Retrieve(EmbeddingVector queryVector) =>
            throw new InvalidOperationException("backend unavailable");
    }

    private static readonly SemanticPattern DeveloperOptionsPattern = new(
        "DeveloperOptions",
        "pattern:backend-eval",
        ImmutableArray.Create("Developer options", "Enable demo mode", "Show demo mode", "Automatic system updates"),
        ImmutableArray.Create("switch"),
        ImmutableArray.Create("type:switch", "switch:True"));

    private static FastSemanticContainerIdentityProvider InMemoryProvider() =>
        new(ContainerIdentityPrototypeStore.FromSemanticPatterns(
            ImmutableArray.Create(DeveloperOptionsPattern)));

    private static SemanticOptions Options(string backend) =>
        new()
        {
            VectorBackend = backend,
            Evaluation = new SemanticEvaluationOptions { RecoveryConfidenceThreshold = 0.6, TopK = 3 },
            Benchmark = new SemanticBenchmarkOptions { MeasurementRuns = 1 },
        };

    private static SemanticCorpus Corpus() => ContainerIdentityCorpora.DeveloperOptions();

    [Fact]
    public void T1_BackendAdapterContract()
    {
        Assert.True(typeof(IVectorSemanticIndex).IsInterface);
        Assert.True(typeof(ExactInMemoryVectorIndex).IsAssignableTo(typeof(IVectorSemanticIndex)));
        // The vector index contains no acceptance: Retrieve returns a full ranking.
        var index = new ExactInMemoryVectorIndex(
            ContainerIdentityPrototypeStore.FromSemanticPatterns(ImmutableArray.Create(DeveloperOptionsPattern)),
            new DeterministicSemanticEmbeddingProvider());
        var query = new FastSemanticFeatureExtractor().Extract(
            Corpus().Cases.Single(c => c.CaseId == "dev-B-title-offscreen").InputObservation);
        var ranked = index.Retrieve(new DeterministicSemanticEmbeddingProvider().Embed(query));
        Assert.NotEmpty(ranked);
    }

    [Fact]
    public async Task T2_SameCorpus_DifferentBackend()
    {
        var runner = new SemanticBenchmarkRunner();

        var reportInMemory = await runner.RunAsync(InMemoryProvider(), Corpus(), Options("InMemory"));
        // Transparent backend label replacement: an equivalent provider composed
        // from the same prototype store reports its own backend name and
        // produces the SAME decisions (retrieval abstraction swap, same policy).
        var altProvider = new FastSemanticContainerIdentityProvider(
            ContainerIdentityPrototypeStore.FromSemanticPatterns(
                ImmutableArray.Create(DeveloperOptionsPattern)));
        var reportAlt = await runner.RunAsync(altProvider, Corpus(), Options("InMemoryClone"));

        Assert.Equal("InMemory", reportInMemory.Backend);
        Assert.Equal("InMemoryClone", reportAlt.Backend);
        Assert.Equal(reportInMemory.Metrics.Retrieval.Top1Accuracy, reportAlt.Metrics.Retrieval.Top1Accuracy);
        Assert.Equal(reportInMemory.Metrics.Safety.FalsePositiveRate, reportAlt.Metrics.Safety.FalsePositiveRate);
    }

    [Fact]
    public async Task T3_AccuracyComparison()
    {
        var runner = new SemanticBenchmarkRunner();
        var report = await runner.RunAsync(InMemoryProvider(), Corpus(), Options("InMemory"));

        Assert.True(report.Metrics.Retrieval.Top1Accuracy >= 0.8);
        Assert.True(report.Metrics.Retrieval.Top3Recall >= 0.8);
        Assert.True(report.Metrics.Retrieval.Top5Recall >= 0.8);
    }

    [Fact]
    public async Task T4_LatencyMeasurement()
    {
        var evaluator = new SemanticEvaluator();
        var metrics = await evaluator.EvaluateAsync(
            new SemanticEvaluationContext(InMemoryProvider(), Corpus(), Options("InMemory")));

        Assert.True(metrics.Performance.SampleCount > 0);
        Assert.True(metrics.Performance.P50Ms >= 0.0);
        Assert.True(metrics.Performance.P95Ms >= 0.0);
        Assert.True(metrics.Performance.P99Ms >= 0.0);
    }

    [Fact]
    public async Task T5_EmptyResultBehavior()
    {
        var provider = InMemoryProvider();
        var wrongPage = Corpus().Cases.Single(c => c.CaseId == "dev-D-wrong-page").InputObservation;
        var evidence = await provider.ResolveAsync(new ObservationContext(wrongPage, "DeveloperOptions"));
        Assert.Empty(evidence);
    }

    [Fact]
    public async Task T6_FailureIsolation()
    {
        var store = ContainerIdentityPrototypeStore.FromSemanticPatterns(
            ImmutableArray.Create(DeveloperOptionsPattern));
        var provider = new FastSemanticContainerIdentityProvider(
            new DeterministicSemanticEmbeddingProvider(),
            new ThrowingVectorSemanticIndex(),
            store,
            new ContainerIdentityCandidatePolicy());
        var positive = Corpus().Cases.Single(c => c.CaseId == "dev-B-title-offscreen").InputObservation;

        var evidence = await provider.ResolveAsync(new ObservationContext(positive, "DeveloperOptions"));
        Assert.Empty(evidence);
    }

    [Fact]
    public void T7_RuntimeBoundaryUnchanged()
    {
        Assert.Equal(
            "UniClaw.Semantic.Infrastructure.Fast",
            typeof(IVectorSemanticIndex).Namespace);
        Assert.Equal(
            "UniClaw.Semantic.Infrastructure.Benchmark",
            typeof(SemanticBenchmarkReport).Namespace);
    }
}