using System.Collections.Immutable;

namespace UniClaw.Runtime.Model;

/// <summary>
/// SC-P3-CAND-008 bounded required-branch inventory evidence consumed by Agent authority.
/// A non-null map proves the complete bounded inventory, an empty map proves a bounded leaf,
/// and null means inventory completeness remains unresolved. It is not authorization, route state,
/// branch progress, GoalEvidence, or Run completion.
/// </summary>
public sealed record BranchInventoryEvidence
{
    /// <summary>Required semantic branch identity → accepted source Observation sequence; null = unresolved.</summary>
    public ImmutableDictionary<string, long>? RequiredBranchEvidence { get; }

    /// <summary>
    /// CALLER_SOURCE_PROVENANCE_CONTRACT — optional explicit occurrence grounding
    /// per branch identity (branch identity → occurrence reference). When
    /// present, the Agent MUST validate each grounding via
    /// SourceGroundingValidator before dispatch (fail closed). Absent keeps the
    /// legacy identity→sequence contract; the parent change's caller-inventory
    /// slice makes explicit grounding mandatory.
    /// </summary>
    public ImmutableDictionary<string, NavigationSourceOccurrenceReference>? RequiredBranchGrounding { get; }

    /// <summary>Deterministic non-empty explanation of the bounded inventory result.</summary>
    public string Reason { get; }

    /// <summary>Create one validated immutable inventory-evidence result.</summary>
    public BranchInventoryEvidence(
        ImmutableDictionary<string, long>? requiredBranchEvidence,
        string reason,
        ImmutableDictionary<string, NavigationSourceOccurrenceReference>? requiredBranchGrounding = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RequiredBranchGrounding = requiredBranchGrounding?.WithComparers(StringComparer.Ordinal);

        RequiredBranchEvidence = requiredBranchEvidence?.WithComparers(StringComparer.Ordinal);
        Reason = reason;

        if (RequiredBranchEvidence is null)
            return;

        foreach (var (identity, sequence) in RequiredBranchEvidence)
        {
            if (string.IsNullOrWhiteSpace(identity))
                throw new ArgumentException("Branch identity cannot be empty.", nameof(requiredBranchEvidence));
            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredBranchEvidence),
                    "Source Observation sequence cannot be negative.");
            }
        }
    }
}
