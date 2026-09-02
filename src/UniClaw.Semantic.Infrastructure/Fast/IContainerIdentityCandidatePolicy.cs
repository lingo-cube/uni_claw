namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Candidate Policy boundary: Candidate Evaluation Context → Accept / Abstain.
///
/// The policy is the ONLY layer that decides whether ranked candidates are
/// reliable enough to become evidence. It must be independently testable and
/// agnostic to the retrieval backend that produced the candidates (matcher or
/// vector index). It never forms Runtime belief and never modifies world state.
/// </summary>
public interface IContainerIdentityCandidatePolicy
{
    /// <summary>Evaluates ranked candidates and returns Accept or Abstain.</summary>
    CandidatePolicyResult Decide(CandidateEvaluationContext context);
}