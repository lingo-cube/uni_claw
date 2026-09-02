using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Fast;
using UniClaw.Semantic.Infrastructure.Retrieval;
using Xunit;
using SemanticEvidence = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;

namespace UniClaw.Semantic.Tests.ContractTests;

public sealed class SemanticContractTests
{
    [Fact]
    public void SemanticEvidence_CarriesContractFields()
    {
        var evidence = new SemanticEvidence(
            evidenceId: "evt-1",
            version: "1",
            source: "FAST",
            kind: SemanticEvidenceKind.ContainerIdentity,
            candidate: "DeveloperOptions",
            confidence: 0.87,
            scope: SemanticEvidenceScope.CurrentContainer,
            observationSequence: 42,
            createdAt: DateTimeOffset.UtcNow)
        {
            References = ImmutableArray.Create(
                new SemanticEvidenceReference("Observation", "42")),
        };

        Assert.Equal("evt-1", evidence.EvidenceId);
        Assert.Equal(SemanticEvidenceKind.ContainerIdentity, evidence.Kind);
        Assert.Equal("DeveloperOptions", evidence.Candidate);
        Assert.Equal(0.87, evidence.Confidence);
        Assert.Equal(42, evidence.ObservationSequence);
        Assert.Single(evidence.References);
    }

    [Fact]
    public void SemanticOptions_IsUnifiedConfigurationEntry()
    {
        var options = new SemanticOptions
        {
            FastSemanticProviderEnabled = true,
            VectorBackend = SemanticVectorBackend.InMemory,
            Benchmark = new SemanticBenchmarkOptions { TopK = 3, MeasurementRuns = 5 },
            Evaluation = new SemanticEvaluationOptions { RecoveryConfidenceThreshold = 0.6, TopK = 1 },
        };

        Assert.True(options.FastSemanticProviderEnabled);
        Assert.Equal(SemanticVectorBackend.InMemory, options.VectorBackend);
        Assert.Equal(3, options.Benchmark.TopK);
        Assert.Equal(5, options.Benchmark.MeasurementRuns);
        Assert.Equal(0.6, options.Evaluation.RecoveryConfidenceThreshold);
    }

    [Fact]
    public void SemanticOptions_DefaultBackendIsInMemory()
    {
        var options = new SemanticOptions();
        Assert.Equal(SemanticVectorBackend.InMemory, options.VectorBackend);
        Assert.NotNull(options.InMemoryIndex);
    }

    [Fact]
    public void VectorIndexRegistry_CreatesInMemory()
    {
        // Registry is RETRIEVAL-only: InMemory returns the exact vector index
        // (no acceptance inside); embedding models are not registry backends.
        var index = SemanticVectorIndexRegistry.Create(SemanticVectorBackend.InMemory);
        Assert.IsType<ExactInMemoryVectorIndex>(index);
        Assert.True(SemanticVectorIndexRegistry.IsSupported(SemanticVectorBackend.InMemory));
        Assert.False(SemanticVectorIndexRegistry.IsSupported("BGE"));
        Assert.False(SemanticVectorIndexRegistry.IsSupported("FAISS"));
    }

    [Fact]
    public void VectorIndexRegistry_RejectsUnknown()
    {
        // FAISS / Qdrant / Milvus are candidate retrieval backends, not wired.
        Assert.Throws<NotSupportedException>(
            () => SemanticVectorIndexRegistry.Create(SemanticVectorBackend.Faiss));
    }

    [Fact]
    public void EmbeddingModelsAreNotRetrievalBackends()
    {
        // Concept correction: BGE is an embedding MODEL, never a vector backend.
        Assert.Null(typeof(SemanticVectorBackend).GetField("Bge"));
        Assert.Contains("InMemory", SemanticVectorBackend.InMemory);
        Assert.Equal("InMemory", SemanticVectorBackend.InMemory);
    }
}