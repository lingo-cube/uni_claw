namespace UniClaw.Runtime.Model;

/// <summary>
/// CP12 two-phase, deterministic target hypothesis: current candidate support and the explicit
/// expected-effect falsifier for the first fresh post-action Observation.
/// </summary>
public sealed record TargetGroundingCriterion
{
    /// <summary>Deterministic candidate-support evaluator over one supplied current Observation.</summary>
    public Func<Observation, ObservedElement, TargetGroundingEvidence> CandidateEvaluator { get; }

    /// <summary>Deterministic expected-effect evaluator over the first fresh post-action Observation.</summary>
    public Func<Observation, TargetGroundingEvidence> PostActionEvaluator { get; }

    /// <summary>Creates a complete two-phase criterion; neither evaluator may be absent.</summary>
    public TargetGroundingCriterion(
        Func<Observation, ObservedElement, TargetGroundingEvidence> candidateEvaluator,
        Func<Observation, TargetGroundingEvidence> postActionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(candidateEvaluator);
        ArgumentNullException.ThrowIfNull(postActionEvaluator);
        CandidateEvaluator = candidateEvaluator;
        PostActionEvaluator = postActionEvaluator;
    }
}
