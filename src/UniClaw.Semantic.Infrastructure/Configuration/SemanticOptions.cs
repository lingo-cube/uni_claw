using UniClaw.Semantic.Infrastructure.Retrieval;

namespace UniClaw.Semantic.Infrastructure.Configuration;

/// <summary>Benchmark configuration for Semantic infrastructure.</summary>
public sealed record SemanticBenchmarkOptions
{
    /// <summary>TopK used for recall metrics. Default 1.</summary>
    public int TopK { get; init; } = 1;

    /// <summary>Number of warmup runs before measurement. Default 0.</summary>
    public int WarmupRuns { get; init; } = 0;

    /// <summary>Number of measurement runs per case. Default 1.</summary>
    public int MeasurementRuns { get; init; } = 1;
}

/// <summary>Evaluation parameters for Semantic infrastructure.</summary>
public sealed record SemanticEvaluationOptions
{
    /// <summary>Minimum confidence threshold for a candidate to be considered a recovery.</summary>
    public double RecoveryConfidenceThreshold { get; init; } = 0.6;

    /// <summary>TopK used for recall metrics. Default 1.</summary>
    public int TopK { get; init; } = 1;
}

/// <summary>
/// Unified Semantic configuration entry. Semantic-related configuration should
/// live here instead of being scattered through Runtime code. Runtime continues
/// to depend only on <c>ISemanticProvider</c>; it does not read this directly.
/// </summary>
public sealed record SemanticOptions
{
    /// <summary>Whether the Fast Semantic provider is enabled.</summary>
    public bool FastSemanticProviderEnabled { get; init; } = true;

    /// <summary>Vector backend selection key. Defaults to SemanticVectorBackend.InMemory.</summary>
    public string VectorBackend { get; init; } = SemanticVectorBackend.InMemory;

    /// <summary>InMemory backend options (used when VectorBackend == InMemory).</summary>
    public InMemoryVectorIndexOptions InMemoryIndex { get; init; } = new();

    /// <summary>Benchmark configuration.</summary>
    public SemanticBenchmarkOptions Benchmark { get; init; } = new();

    /// <summary>Evaluation configuration.</summary>
    public SemanticEvaluationOptions Evaluation { get; init; } = new();
}