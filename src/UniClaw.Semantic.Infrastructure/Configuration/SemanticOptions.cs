using UniClaw.Semantic.Infrastructure.Fast;
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

/// <summary>Retrieval (vector index) configuration identity.</summary>
public sealed record SemanticRetrievalOptions
{
    /// <summary>Similarity metric used by the retrieval backend, e.g. "overlap" (V1 matcher) / "cosine" (exact vector index).</summary>
    public string Metric { get; init; } = "overlap";

    /// <summary>TopK candidates the retrieval should return to the policy (0 = all).</summary>
    public int TopK { get; init; } = 0;
}

/// <summary>Embedding configuration identity (embedding provider + model identity).</summary>
public sealed record SemanticEmbeddingOptions
{
    /// <summary>Embedding provider key (V1: "Deterministic"; future: "BgeSmall", "BgeBase", ...).</summary>
    public string Provider { get; init; } = "Deterministic";

    /// <summary>Binding model identity of the provider.</summary>
    public EmbeddingModelIdentity Model { get; init; } =
        new EmbeddingModelIdentity("deterministic-v1", "v1", 64, "in-process", "none");
}

/// <summary>Prototype profile configuration identity.</summary>
public sealed record SemanticPrototypeOptions
{
    /// <summary>Prototype profile/version, e.g. "v1-canonical-signatures".</summary>
    public string ProfileVersion { get; init; } = "v1-canonical-signatures";
}

/// <summary>Candidate policy configuration identity (expresses existing V1 semantics only).</summary>
public sealed record SemanticPolicyOptions
{
    /// <summary>Policy profile/version, e.g. "v1".</summary>
    public string ProfileVersion { get; init; } = "v1";

    /// <summary>Acceptance threshold (single; per-identity maps come from the policy profile). Default 0.3 (legacy).</summary>
    public double AcceptanceThreshold { get; init; } = 0.3;

    /// <summary>Structural type compatibility rule enabled.</summary>
    public bool StructuralCompatibility { get; init; } = true;

    /// <summary>Previous verified identity conflict rejection enabled (fail-closed).</summary>
    public bool PreviousIdentityConflictRejection { get; init; } = true;

    /// <summary>Minimum evidence abstention enabled.</summary>
    public bool MinimumEvidenceAbstention { get; init; } = true;
}

/// <summary>
/// Unified Semantic configuration entry. Semantic-related configuration should
/// live here instead of being scattered through Runtime code. Runtime continues
/// to depend only on <c>ISemanticProvider</c>; it does not read this directly.
///
/// Responsibility identities (each independently expressible and pinnable):
/// retrieval backend (VectorBackend + Retrieval), embedding (Embedding),
/// prototype profile (Prototype), candidate policy (Policy), and the complete
/// pipeline profile id (PipelineProfileId). Embedding models are not retrieval
/// backends — the historical "BGE backend" concept is removed.
/// </summary>
public sealed record SemanticOptions
{
    /// <summary>Whether the Fast Semantic provider is enabled.</summary>
    public bool FastSemanticProviderEnabled { get; init; } = true;

    /// <summary>Retrieval backend selection key (vector index backends only). Defaults to SemanticVectorBackend.InMemory.</summary>
    public string VectorBackend { get; init; } = SemanticVectorBackend.InMemory;

    /// <summary>Retrieval options (metric / topK) for the selected backend.</summary>
    public SemanticRetrievalOptions Retrieval { get; init; } = new();

    /// <summary>Embedding provider + model identity.</summary>
    public SemanticEmbeddingOptions Embedding { get; init; } = new();

    /// <summary>Prototype profile identity.</summary>
    public SemanticPrototypeOptions Prototype { get; init; } = new();

    /// <summary>Candidate policy profile identity.</summary>
    public SemanticPolicyOptions Policy { get; init; } = new();

    /// <summary>Complete pipeline profile identity (object of qualification).</summary>
    public string PipelineProfileId { get; init; } = SemanticPerceptionProfiles.SeparatedV1.ProfileId;

    /// <summary>Retrieval index options for the InMemory retrieval backend (retrieval-only knobs).</summary>
    public InMemoryVectorIndexOptions InMemoryIndex { get; init; } = new();

    /// <summary>Benchmark configuration.</summary>
    public SemanticBenchmarkOptions Benchmark { get; init; } = new();

    /// <summary>Evaluation configuration.</summary>
    public SemanticEvaluationOptions Evaluation { get; init; } = new();
}