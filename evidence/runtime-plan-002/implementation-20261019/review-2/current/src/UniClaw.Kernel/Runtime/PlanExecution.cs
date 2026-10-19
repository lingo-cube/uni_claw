namespace UniClaw.Kernel.Runtime;

public enum PlanCandidateDisposition { Ready, Blocked, Unknown }

public sealed record PlanCandidate(
    string Key,
    string TargetRole,
    string TargetDescriptor,
    PlanCandidateDisposition Disposition,
    IReadOnlyList<string> EvidenceRefs,
    string? Reason = null);

public sealed record PlanCandidateSet(
    string ScopeKey,
    string Revision,
    IReadOnlyList<PlanCandidate> Candidates);

public enum PlanStepGuardVerdict { Allow, AlreadySatisfied, Reject, Unknown }

public sealed record PlanStepGuardResult(
    PlanStepGuardVerdict Verdict,
    string Reason,
    IReadOnlyList<string>? EvidenceRefs = null);

public sealed record PlanExecutionPosition(
    string PlanId,
    int Version,
    int ItemIndex,
    int Iteration,
    int BodyIndex,
    string? CandidateKey,
    int WorkUsed,
    string? ControlPath = null);

public enum PlanExecutionStatus { Active, Paused, Completed, Abandoned }

public sealed record PlanBlockedEvidence(
    string CandidateKey,
    string Source,
    string? Reason,
    IReadOnlyList<string> EvidenceRefs,
    PlanExecutionPosition Position);

public enum PlanAttemptStatus { Started, Unconfirmed, Verified, AlreadySatisfied, Rejected, UnknownOutcome }

public sealed record PlanAttempt(
    string AttemptId,
    PlanExecutionPosition Position,
    string DecisionId,
    AgentActionStep? Step,
    PlanAttemptStatus Status,
    string? ReceiptId,
    IReadOnlyList<string> EvidenceRefs,
    string? Reason);

public sealed record PlanExecutionView(
    PlanExecutionPosition Position,
    PlanExecutionStatus Status,
    string? StageExpectation,
    string? InterruptionReason,
    IReadOnlyList<PlanAttempt> Attempts,
    IReadOnlyList<PlanBlockedEvidence> BlockedEvidence,
    IReadOnlyList<PlanConditionEvaluation>? ConditionEvaluations = null,
    TraversalExecutionView? Traversal = null);

public sealed record PlanExecutionOverview(
    PlanExecutionPosition Position,
    PlanExecutionStatus Status,
    string? InterruptionReason,
    int Attempts,
    int BlockedCandidates,
    string? StageExpectation = null);

/// <summary>Read-only owner verdict; it can select control flow but grants no effect authority.</summary>
public sealed record PlanConditionResult(
    PolicyTruth Truth,
    string ScopeKey,
    string Revision,
    IReadOnlyList<string> EvidenceRefs,
    string? Reason = null);

public sealed record PlanConditionEvaluation(
    string EvaluationId,
    string Condition,
    PlanConditionResult Result,
    PlanExecutionPosition Position);
