using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Evidence-backed cross-Container progress for one bounded semantic parent scope (SC-P3-CAND-004).
/// The approved inventory and completed siblings are immutable evidence maps whose values reference
/// source Observation sequence numbers. A visit, action dispatch, or local completion flag alone is
/// not represented as branch completion.
/// </summary>
public sealed record BranchProgressEvidence
{
    /// <summary>Create one validated immutable progress snapshot.</summary>
    public BranchProgressEvidence(
        string parentSemanticPage,
        ImmutableDictionary<string, long> approvedSiblingEvidence,
        ImmutableDictionary<string, long> completedSiblingEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentSemanticPage);
        ArgumentNullException.ThrowIfNull(approvedSiblingEvidence);
        ArgumentNullException.ThrowIfNull(completedSiblingEvidence);

        ParentSemanticPage = parentSemanticPage;
        ApprovedSiblingEvidence = approvedSiblingEvidence.WithComparers(StringComparer.Ordinal);
        CompletedSiblingEvidence = completedSiblingEvidence.WithComparers(StringComparer.Ordinal);
        ValidateEvidence(ApprovedSiblingEvidence, nameof(approvedSiblingEvidence));
        ValidateEvidence(CompletedSiblingEvidence, nameof(completedSiblingEvidence));
        if (CompletedSiblingEvidence.Keys.Any(branch => !ApprovedSiblingEvidence.ContainsKey(branch)))
        {
            throw new ArgumentException(
                "Completed sibling evidence must be a subset of the approved sibling inventory.",
                nameof(completedSiblingEvidence));
        }
    }

    /// <summary>Semantic identity of the bounded parent scope.</summary>
    public string ParentSemanticPage { get; }

    /// <summary>Approved sibling identity → fresh parent-inventory Observation sequence.</summary>
    public ImmutableDictionary<string, long> ApprovedSiblingEvidence { get; }

    /// <summary>Approved sibling identity → child-local completion Observation sequence.</summary>
    public ImmutableDictionary<string, long> CompletedSiblingEvidence { get; }

    /// <summary>Derived evidence coverage; not stored as another production field.</summary>
    public bool IsSubtreeComplete
        => ApprovedSiblingEvidence.Count > 0
           && ApprovedSiblingEvidence.Keys.All(CompletedSiblingEvidence.ContainsKey);

    /// <summary>Return a new snapshot with one approved sibling's completion evidence.</summary>
    public BranchProgressEvidence WithCompletedSibling(string siblingIdentity, long sourceObservationSequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siblingIdentity);
        if (sourceObservationSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceObservationSequence));
        if (!ApprovedSiblingEvidence.ContainsKey(siblingIdentity))
        {
            throw new ArgumentException(
                $"Sibling '{siblingIdentity}' is not in the approved inventory for '{ParentSemanticPage}'.",
                nameof(siblingIdentity));
        }
        return new BranchProgressEvidence(
            ParentSemanticPage,
            ApprovedSiblingEvidence,
            CompletedSiblingEvidence.SetItem(siblingIdentity, sourceObservationSequence));
    }

    private static void ValidateEvidence(ImmutableDictionary<string, long> evidence, string parameterName)
    {
        foreach (var (identity, sequence) in evidence)
        {
            if (string.IsNullOrWhiteSpace(identity))
                throw new ArgumentException("Sibling identity cannot be empty.", parameterName);
            if (sequence < 0)
                throw new ArgumentOutOfRangeException(parameterName, "Observation sequence cannot be negative.");
        }
    }
}
