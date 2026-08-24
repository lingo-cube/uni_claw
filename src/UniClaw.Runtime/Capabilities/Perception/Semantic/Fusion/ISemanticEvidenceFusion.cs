namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>
/// Runtime Evidence Fusion port. It is the SOLE consumer of SemanticEvidence
/// (falsifier F1 / consumption boundary). It validates evidence and produces a
/// <see cref="ValidatedSemanticEvidenceResult"/> containing only evidence and
/// weights — never an Action, Goal decision, Plan, or World mutation. It does
/// NOT create Fact; the Runtime belief system owns Fact / Belief Update.
/// </summary>
public interface ISemanticEvidenceFusion
{
    /// <summary>
    /// Validates and fuses SemanticEvidence against the current observation,
    /// vision evidence, container history, and existing belief context.
    /// </summary>
    /// <param name="input">The evidence/context input.</param>
    /// <returns>A validated result with accepted/rejected evidence and weights
    /// (no Action / Goal / Plan / World mutation).</returns>
    ValidatedSemanticEvidenceResult Fuse(SemanticEvidenceFusionInput input);
}
