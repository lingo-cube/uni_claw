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
        ImmutableDictionary<string, long> completedSiblingEvidence,
        ImmutableDictionary<string, long>? authorizedSiblingEvidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentSemanticPage);
        ArgumentNullException.ThrowIfNull(approvedSiblingEvidence);
        ArgumentNullException.ThrowIfNull(completedSiblingEvidence);

        ParentSemanticPage = parentSemanticPage;
        ApprovedSiblingEvidence = approvedSiblingEvidence.WithComparers(StringComparer.Ordinal);
        CompletedSiblingEvidence = completedSiblingEvidence.WithComparers(StringComparer.Ordinal);
        AuthorizedSiblingEvidence = (authorizedSiblingEvidence ?? ImmutableDictionary<string, long>.Empty)
            .WithComparers(StringComparer.Ordinal);
        ValidateEvidence(ApprovedSiblingEvidence, nameof(approvedSiblingEvidence));
        ValidateEvidence(CompletedSiblingEvidence, nameof(completedSiblingEvidence));
        ValidateEvidence(AuthorizedSiblingEvidence, nameof(authorizedSiblingEvidence));
        if (CompletedSiblingEvidence.Keys.Any(branch => !ApprovedSiblingEvidence.ContainsKey(branch)))
        {
            throw new ArgumentException(
                "Completed sibling evidence must be a subset of the approved sibling inventory.",
                nameof(completedSiblingEvidence));
        }
        if (AuthorizedSiblingEvidence.Keys.Any(branch => !ApprovedSiblingEvidence.ContainsKey(branch)))
        {
            throw new ArgumentException(
                "Authorized sibling evidence must be a subset of the approved sibling inventory.",
                nameof(authorizedSiblingEvidence));
        }
    }

    /// <summary>Semantic identity of the bounded parent scope.</summary>
    public string ParentSemanticPage { get; }

    /// <summary>Approved sibling identity → fresh parent-inventory Observation sequence.</summary>
    public ImmutableDictionary<string, long> ApprovedSiblingEvidence { get; }

    /// <summary>Approved sibling identity → child-local completion Observation sequence.</summary>
    public ImmutableDictionary<string, long> CompletedSiblingEvidence { get; }

    /// <summary>
    /// AUTHORIZED recursive obligation identity → source Observation sequence.
    /// A discovered candidate becomes an AUTHORIZED CHILD OBLIGATION only when
    /// the Agent explicitly authorized and dispatched it. Discovered-but-
    /// audited (denied) candidates are NOT obligations: they never enter this
    /// set and never block the verified parent return.
    /// </summary>
    public ImmutableDictionary<string, long> AuthorizedSiblingEvidence { get; }

    /// <summary>Derived evidence coverage; not stored as another production field.</summary>
    public bool IsSubtreeComplete
        => ApprovedSiblingEvidence.Count > 0
           && ApprovedSiblingEvidence.Keys.All(CompletedSiblingEvidence.ContainsKey);

    /// <summary>
    /// REQUIRED CHILDREN — ONLY the explicitly AUTHORIZED_CHILD recursive
    /// obligations (execution evidence: <see cref="AuthorizedSiblingEvidence"/>).
    /// DISCOVERED != AUTHORIZED; GROUNDED != AUTHORIZED; AUDITED/DENIED !=
    /// REQUIRED_CHILD. The discovered inventory is never automatically converted
    /// into obligations.
    /// </summary>
    public ImmutableDictionary<string, long> RequiredChildren => AuthorizedSiblingEvidence;

    /// <summary>
    /// COMPLETED CHILDREN — the required children with verified completion:
    /// the completed obligations that correspond to an AUTHORIZED obligation
    /// (a CompletedChild must map to the exact authorized obligation — never
    /// matched by BranchIdentity / source title / ordinal alone).
    /// </summary>
    public ImmutableDictionary<string, long> CompletedChildren
        => CompletedSiblingEvidence
            .Where(kv => AuthorizedSiblingEvidence.ContainsKey(kv.Key))
            .ToImmutableDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    /// <summary>
    /// SUBTREE COMPLETE (the sibling/subtree-ledger rule): every REQUIRED child
    /// (authorized obligation) has completed with a verified return. The
    /// ContainerComplete component of the rule is evaluated by the Agent /
    /// ledger evaluation (the frozen discovery epoch must exist). GoalEvidence
    /// == TRUE, ContainerComplete, or return-eligibility alone NEVER imply
    /// SubtreeComplete.
    /// </summary>
    public bool IsSubtreeCompleteByRequiredChildren
        => RequiredChildren.Count > 0
           && RequiredChildren.Keys.All(CompletedSiblingEvidence.ContainsKey);

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
            CompletedSiblingEvidence.SetItem(siblingIdentity, sourceObservationSequence),
            AuthorizedSiblingEvidence);
    }

    /// <summary>Return a new snapshot with one sibling recorded as an AUTHORIZED
    /// recursive obligation (the Agent authorized and dispatched it).</summary>
    public BranchProgressEvidence WithAuthorizedSibling(string siblingIdentity, long sourceObservationSequence)
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
            CompletedSiblingEvidence,
            AuthorizedSiblingEvidence.SetItem(siblingIdentity, sourceObservationSequence));
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
