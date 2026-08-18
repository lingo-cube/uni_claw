namespace UniClaw.Runtime.Model;

/// <summary>
/// CALLER_SOURCE_PROVENANCE_CONTRACT — immutable caller grounding claim binding a
/// branch identity to one navigation source occurrence reference.
///
/// The caller EXPLAINS where its branch points; it does not assert equivalence
/// and does not declare a logical source. The Agent
/// (<see cref="UniClaw.Runtime.Agent.SourceGroundingValidator"/>) is the sole
/// verifier that the referenced occurrence:
///   1. belongs to the current run,
///   2. belongs to the current Container's accepted viewport observations,
///   3. is an accepted viewport observation,
///   4. actually exists,
///   5. is a NAVIGATION_CANDIDATE occurrence,
///   6. resolves to a logical source via the current normalization result.
/// </summary>
public sealed record BranchSourceGroundingEvidence
{
    /// <summary>Caller-declared branch identity (an explanation label, never
    /// source truth).</summary>
    public string BranchIdentity { get; }

    /// <summary>The occurrence the branch points at.</summary>
    public NavigationSourceOccurrenceReference SourceOccurrenceReference { get; }

    public BranchSourceGroundingEvidence(
        string branchIdentity,
        NavigationSourceOccurrenceReference sourceOccurrenceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchIdentity);
        ArgumentNullException.ThrowIfNull(sourceOccurrenceReference);
        BranchIdentity = branchIdentity;
        SourceOccurrenceReference = sourceOccurrenceReference;
    }
}
