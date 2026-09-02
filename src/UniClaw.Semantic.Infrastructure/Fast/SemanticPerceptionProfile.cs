namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// A complete Semantic Perception pipeline profile: the object of qualification.
///
/// Qualification is never about a single model — it is about the binding of
/// Feature Extraction + Embedding + Prototype + Retrieval + Similarity +
/// Candidate Policy. Each component identity is bound independently so a
/// profile can be pinned, compared, and requalified without touching the others.
/// </summary>
public sealed record SemanticPerceptionProfile(
    string ProfileId,
    string FeatureExtractionVersion,
    string EmbeddingProvider,
    EmbeddingModelIdentity EmbeddingModel,
    string PrototypeProfileVersion,
    string RetrievalBackend,
    string SimilarityMetric,
    string CandidatePolicyProfileVersion);

/// <summary>Known pipeline profiles.</summary>
public static class SemanticPerceptionProfiles
{
    /// <summary>
    /// Separated C# Profile V1: deterministic representation + deterministic
    /// embedding + canonical prototypes v1 + reference matcher retrieval +
    /// V1 candidate policy. NOTE: qualification status of the pipeline profile
    /// is UNCHANGED by this gate — Profile V1 remains SAFETY_NOT_QUALIFIED
    /// (held-out RED evidence stays; see the held-out validation result).
    /// </summary>
    public static SemanticPerceptionProfile SeparatedV1 { get; } = new(
        "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V1",
        "v1-text-plus-type",
        "DeterministicSemantic",
        new EmbeddingModelIdentity("deterministic-v1", "v1", 64, "in-process", "none"),
        "v1-canonical-signatures",
        "DeterministicMatcher",
        "overlap",
        "v1");

    /// <summary>
    /// SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2 (SEMANTIC_SAFETY_HARDENING_APPLY):
    /// same representation / embedding / prototypes / retrieval, but the
    /// candidate policy is CONTAINER_IDENTITY_POLICY_V2 (margin + evidence
    /// sufficiency) and the evidence-sufficiency profile is bound. Qualification
    /// state: NOT_QUALIFIED until a fresh held-out-v2 qualification gate.
    /// </summary>
    public static SemanticPerceptionProfile V2 { get; } = new(
        "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V2",
        "v1-text-plus-type",
        "BgeSmall|Deterministic", // binding: BGE-small for the embedding evidence path; deterministic in-process for the C# pipeline
        new EmbeddingModelIdentity("BAAI/bge-small-en-v1.5", "v1.5", 384, "fastembed+onnxruntime", "fp32"),
        "v1-canonical-signatures",
        "exact-in-memory-cosine", // BGE path: cosine over prototype vectors
        "cosine",
        "CONTAINER_IDENTITY_POLICY_V2");

    /// <summary>
    /// SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3 (SEMANTIC_PROFILE_V3_DEVELOPMENT):
    /// prototype representation hardened to multi-state per identity
    /// (identity-max aggregation), anchors extended to the state vocabulary;
    /// candidate policy safety semantics (margin 0.05, conflict, structural,
    /// min-evidence principles) UNCHANGED. Development-exit targets met on
    /// regression evidence; qualification requires ContainerIdentity-heldout-v3.
    /// </summary>
    public static SemanticPerceptionProfile V3 { get; } = new(
        "SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3",
        "v1-text-plus-type",
        "BgeSmall",
        new EmbeddingModelIdentity("BAAI/bge-small-en-v1.5", "v1.5", 384, "fastembed+onnxruntime", "fp32"),
        "v3-multi-state", // profiles/SEMANTIC_CONTAINER_IDENTITY_PROFILE_V3.json identity_prototypes
        "exact-in-memory-cosine-identity-max",
        "cosine",
        "CONTAINER_IDENTITY_POLICY_V2");
}