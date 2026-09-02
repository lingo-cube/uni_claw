namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Named candidate policy profiles (versioned, configurable, rollbackable).
/// The pipeline profile binds one of these by version id:
///   - LegacyReference : exact legacy C# profile semantics (threshold only)
///   - V1              : V1 rules — threshold + structural + conflict + min-evidence
///   - V2              : V1 + margin-based abstention + evidence sufficiency
///                       (SEMANTIC_SAFETY_HARDENING_APPLY mechanisms)
/// </summary>
public static class CandidatePolicies
{
    /// <summary>Legacy reference policy: threshold only.</summary>
    public static IContainerIdentityCandidatePolicy LegacyReference(double acceptanceThreshold = 0.3)
        => new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
        {
            AcceptanceThreshold = acceptanceThreshold,
            StructuralCompatibility = false,
            PreviousIdentityConflictRejection = false,
            MinimumEvidenceAbstention = false,
        });

    /// <summary>Separated V1 policy: threshold + structural + conflict + minimum evidence.</summary>
    public static IContainerIdentityCandidatePolicy V1(double acceptanceThreshold = 0.3)
        => new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
        {
            AcceptanceThreshold = acceptanceThreshold,
        });

    /// <summary>
    /// CONTAINER_IDENTITY_POLICY_V2: V1 plus hardening A (margin) and
    /// hardening B (evidence sufficiency). Parameters are profile-bound; the
    /// margin constant mirrors the committed V2 profile JSON selected by the
    /// safety-first margin scan (pinned by tests).
    /// </summary>
    public static IContainerIdentityCandidatePolicy V2(double minimumTop1Top2Margin = 0.05)
        => new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
        {
            AcceptanceThreshold = 0.3,
            StructuralCompatibility = true,
            PreviousIdentityConflictRejection = true,
            MinimumEvidenceAbstention = true,
            MinimumTop1Top2Margin = minimumTop1Top2Margin,
            EvidenceSufficiency = EvidenceSufficiencyProfiles.V1,
        });

    /// <summary>
    /// Builds a policy from a policy profile version id (profile-bound binding;
    /// "v1" → V1, "v2" → V2, anything else → V1). Used by the pipeline factory.
    /// </summary>
    public static IContainerIdentityCandidatePolicy FromVersion(string profileVersion)
        => string.Equals(profileVersion, "v2", StringComparison.OrdinalIgnoreCase) ? V2() : V1();
}