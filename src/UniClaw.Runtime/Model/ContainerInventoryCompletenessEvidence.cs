using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Agent-owned immutable evidence that the current Container's discoverable
/// Settings navigation-source inventory has been proven complete within the
/// approved deterministic exploration boundary.
///
/// DISCOVERY EPOCH lifecycle (non-monotonic evidence extension):
/// <see cref="SourceObservationSequences"/> / <see cref="FrozenDiscoveryObservationSequences"/>
/// are the FROZEN first-forward-exploration observation set that owns the
/// inventory-generation proof. Once <see cref="IsComplete"/> holds, the epoch is
/// frozen: later ScrollBackward / parent-return / same-Container fresh
/// observations are NEVER appended to this normalization input and NEVER change
/// <see cref="ProvenLogicalSources"/> — they can only be CONSISTENT with the
/// proven inventory or INVALIDATE completeness (validated by the Agent's
/// post-completeness consistency validator).
///
/// This is NOT GoalEvidence, leaf proof, subtree completion, or full-tree
/// enumeration.
/// </summary>
/// <param name="ContainerSemanticPage">Current Container semantic page name.</param>
/// <param name="SourceObservationSequences">FROZEN discovery-epoch Observation sequences (forward exploration only).</param>
/// <param name="UniqueNavigationSourceIdentities">Normalized unique discovered navigation-source identities (signature keys).</param>
/// <param name="ExplorationExhausted">Positive-exhaustion evidence: true only when deterministic forward exploration exhaustion was proven.</param>
/// <param name="UnresolvedCandidateCount">Number of relevant unresolved candidates that block completeness.</param>
/// <param name="Reason">Deterministic reason.</param>
/// <param name="ProvenLogicalSources">Evidence-built frozen logical source classes (signature key + discovery occurrence provenance).</param>
public sealed record ContainerInventoryCompletenessEvidence(
    string ContainerSemanticPage,
    ImmutableArray<long> SourceObservationSequences,
    ImmutableArray<string> UniqueNavigationSourceIdentities,
    bool ExplorationExhausted,
    int UnresolvedCandidateCount,
    string Reason,
    ImmutableArray<ProvenLogicalSource> ProvenLogicalSources = default)
{
    /// <summary>True only when all completeness conditions hold.</summary>
    public bool IsComplete =>
        ExplorationExhausted
        && UnresolvedCandidateCount == 0
        && !SourceObservationSequences.IsDefaultOrEmpty;

    /// <summary>The frozen discovery epoch observation sequences (alias of <see cref="SourceObservationSequences"/>).</summary>
    public ImmutableArray<long> FrozenDiscoveryObservationSequences => SourceObservationSequences;

    /// <summary>Positive-exhaustion evidence (alias of <see cref="ExplorationExhausted"/>).</summary>
    public bool PositiveExhaustionEvidence => ExplorationExhausted;
}
