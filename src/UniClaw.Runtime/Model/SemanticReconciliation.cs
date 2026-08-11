namespace UniClaw.Runtime.Model;

/// <summary>
/// Pure (stateless) reconciliation of multiple <see cref="SemanticEvidence"/> stances
/// into a single <see cref="SemanticBeliefState"/>. This is a pure operation — it owns
/// no mutable state. The caller (Container for local belief) stores the result and
/// remains the sole state owner (I-2).
/// </summary>
public static class SemanticReconciliation
{
    /// <summary>
    /// Fuse multiple evidence stances about the same claim into a belief state.
    /// SUPPORTED    = ≥1 Supports, 0 Contradicts
    /// UNRESOLVED   = all Insufficient, or no sources
    /// CONTRADICTED = ≥1 Supports AND ≥1 Contradicts
    /// </summary>
    public static SemanticBeliefState FuseBelief(params SemanticEvidence[] evidence)
    {
        if (evidence is null || evidence.Length == 0)
            return SemanticBeliefState.Unresolved;

        var hasSupports = false;
        var hasContradicts = false;

        foreach (var e in evidence)
        {
            if (e.Stance == SemanticEvidenceStance.Supports)
                hasSupports = true;
            else if (e.Stance == SemanticEvidenceStance.Contradicts)
                hasContradicts = true;
        }

        if (hasSupports && hasContradicts)
            return SemanticBeliefState.Contradicted;
        if (hasSupports)
            return SemanticBeliefState.Supported;
        return SemanticBeliefState.Unresolved;
    }
}
