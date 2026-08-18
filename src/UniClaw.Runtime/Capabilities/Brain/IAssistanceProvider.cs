using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Brain;

/// <summary>
/// L1 CONSULT seam (External Contract Plane 3 — Assistance): the Runtime requests
/// external INFORMATION when semantic adjudication cannot decide
/// (belief <see cref="SemanticBeliefState.Unresolved"/> /
/// <see cref="SemanticBeliefState.Contradicted"/>).
///
/// Semantics (frozen):
///  - This is a capability-gap expression, NOT an LLM/VLM/model invocation.
///  - The Agent keeps final decision authority (I-3): the advice is candidate
///    information only; it NEVER writes belief/binding/Container/state.
///  - Advice is not truth, not authorization, not goal completion.
///  - Advice must echo the context correlation and the context world version;
///    stale/uncorrelated advice is discarded by the consumer.
///  - Null-safe optional injection: an absent provider preserves today's
///    fail-closed behavior (zero regression).
///
/// The implementation side (an external intelligence host adapter) is provided at
/// the composition root; this interface depends only on BCL + Model (Guard 2).
/// </summary>
public interface IAssistanceProvider
{
    /// <summary>Request external information for one adjudication point.
    /// Returns null or a stale/uncorrelated advice when no usable information.</summary>
    Task<AssistanceAdvice?> ConsultAsync(AssistanceContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What the Runtime truthfully knows at the adjudication point (immutable
/// snapshot of the public surface — no internal state, no live references).
/// </summary>
/// <param name="RequestId">Correlation identity (per consult; echoed by advice).</param>
/// <param name="RunId">Run identity.</param>
/// <param name="SemanticPage">Current container semantic page.</param>
/// <param name="BeliefState">The adjudication trigger: Unresolved or Contradicted.</param>
/// <param name="WorldVersion">Observation.SequenceNumber the adjudication is based on.</param>
/// <param name="Observation">The fresh observation evidence (immutable copy).</param>
public sealed record AssistanceContext(
    string RequestId,
    string RunId,
    string SemanticPage,
    SemanticBeliefState BeliefState,
    long WorldVersion,
    Observation Observation);

/// <summary>
/// Candidate information returned by a provider — never authority.
/// </summary>
/// <param name="RequestId">MUST echo the context correlation; mismatches are discarded.</param>
/// <param name="WorldVersion">MUST equal the context world version; older advice is stale and discarded.</param>
/// <param name="Recommendation">Optional bounded deterministic action the Agent may take:
/// <c>re-observe</c> / <c>rebind</c> / <c>dismiss-obstruction</c>; null or unknown =
/// not actionable (consumer falls back to existing fail-closed semantics).</param>
/// <param name="AdditionalEvidence">Optional supplementary recognition knowledge
/// (informational only in this slice).</param>
/// <param name="Reason">Human-auditable rationale.</param>
public sealed record AssistanceAdvice(
    string RequestId,
    long WorldVersion,
    string? Recommendation,
    string? AdditionalEvidence,
    string Reason);
