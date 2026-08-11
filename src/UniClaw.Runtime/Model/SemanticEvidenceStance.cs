namespace UniClaw.Runtime.Model;

/// <summary>
/// A single evidence source's stance on a semantic claim.
/// This is EVIDENCE, not adjudicated belief (SemanticBeliefState).
/// Evidence answers: "What does Source S say about Claim C?"
/// Belief answers: "What does the semantic owner believe after fusion?"
/// </summary>
public enum SemanticEvidenceStance
{
    /// <summary>This source's evidence supports the claim.</summary>
    Supports = 1,

    /// <summary>This source's evidence contradicts the claim.</summary>
    Contradicts = 2,

    /// <summary>This source has insufficient evidence to judge the claim.</summary>
    Insufficient = 3,
}
