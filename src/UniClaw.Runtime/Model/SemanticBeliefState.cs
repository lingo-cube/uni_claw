namespace UniClaw.Runtime.Model;

/// <summary>
/// The belief state produced by fusing multiple <see cref="SemanticEvidence"/> stances
/// about the same semantic claim. This is BELIEF, not truth — the external world
/// remains authoritative (I-4).
/// </summary>
public enum SemanticBeliefState
{
    /// <summary>≥1 source Supports, 0 Contradicts — the claim is supported by evidence.</summary>
    Supported = 1,

    /// <summary>All sources Insufficient, or no sources — cannot determine the claim.</summary>
    Unresolved = 2,

    /// <summary>≥1 source Supports AND ≥1 Contradicts — sources disagree; refuse to collapse.</summary>
    Contradicted = 3,
}
