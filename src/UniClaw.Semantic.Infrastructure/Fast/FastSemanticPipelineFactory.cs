using UniClaw.Semantic.Infrastructure.Configuration;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Composes a <see cref="FastSemanticContainerIdentityProvider"/> from
/// configuration identities (retrieval backend, embedding, prototype profile,
/// candidate policy profile). This is where SemanticOptions → components
/// binding happens. This gate wires the V1 deterministic path only — real
/// embedding models are NOT connected here.
/// </summary>
public static class FastSemanticPipelineFactory
{
    /// <summary>
    /// Creates the provider from options + prototype store, binding the
    /// candidate policy by <c>options.Policy.ProfileVersion</c> ("v2" → the
    /// hardened CONTAINER_IDENTITY_POLICY_V2; anything else → V1). Rollback to
    /// V1 = configuration change only, no runtime change.
    /// </summary>
    public static FastSemanticContainerIdentityProvider CreateFromOptions(
        SemanticOptions options,
        IContainerIdentityPrototypeStore prototypes)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(prototypes);

        var policy = CandidatePolicies.FromVersion(options.Policy.ProfileVersion);
        return new FastSemanticContainerIdentityProvider(prototypes, policy, source: "FAST");
    }

    /// <summary>Creates the V1 separated provider from options + prototype store.</summary>
    public static FastSemanticContainerIdentityProvider CreateV1(
        SemanticOptions options,
        IContainerIdentityPrototypeStore prototypes)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(prototypes);

        var policy = new ContainerIdentityCandidatePolicy(new CandidatePolicyOptions
        {
            AcceptanceThreshold = options.Policy.AcceptanceThreshold,
            StructuralCompatibility = options.Policy.StructuralCompatibility,
            PreviousIdentityConflictRejection = options.Policy.PreviousIdentityConflictRejection,
            MinimumEvidenceAbstention = options.Policy.MinimumEvidenceAbstention,
        });

        return new FastSemanticContainerIdentityProvider(prototypes, policy, source: "FAST");
    }
}