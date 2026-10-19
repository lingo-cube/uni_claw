using UniClaw.Kernel.Capability;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Runtime;

/// <summary>Closed read-only typed binding to the existing Language Inspection protocol.
/// Finding semantics remain the capability's responsibility; Required is availability,
/// RequirePass is an explicit continuation condition, independent of invocation completion.</summary>
public sealed record PlanLanguageHandler(
    string HandlerId,
    string CapabilityId,
    string ProtocolVersion,
    string ExpectedLanguage,
    bool Required = true,
    bool RequirePass = false);

public enum TraversalOperationKind { ReadOnly, Navigate }
public sealed record TraversalOperation(
    string OperationId,
    TraversalOperationKind Kind,
    AgentActionStep? Step,
    IReadOnlyList<PlanLanguageHandler> Before,
    IReadOnlyList<PlanLanguageHandler> AfterVerified);
public sealed record TraversalRule(string RuleId, string Condition, TraversalOperation Operation);

/// <summary>Owner-projected candidate references. Never a locator or authorization.</summary>
public sealed record TraversalCandidate(
    PlanCandidate Target,
    IReadOnlyDictionary<string, PolicyTruth> Conditions);

/// <summary>One current World reference view, supplied through an explicit projection seam.
/// Discovery evidence is separate from an empty candidate set. Historical graph nodes do
/// not become executable targets. Kernel validates revision/evidence/current-page linkage.</summary>
public sealed record TraversalObservation(
    WorldGraphProjection Graph,
    IReadOnlyList<TraversalCandidate> Candidates,
    bool DiscoveryComplete,
    IReadOnlyList<string> DiscoveryEvidenceRefs);

public sealed record PlanHandlerInput(
    string RunId,
    string OperationId,
    string Phase,
    string SourceRevisionId,
    string ContainerId,
    PlanCandidate Target);
public enum PlanHandlerCallStatus { Completed, Unavailable, Failed, Skipped, TimedOut }
public sealed record PlanHandlerRecord(
    string OperationId,
    string HandlerId,
    string Phase,
    string SourceRevisionId,
    PlanHandlerCallStatus Status,
    Finding? Result,
    string? Reason);
/// <summary>Run-owned input/result association for a concrete zero-effect or
/// navigation operation. ObservedState is a snapshot reference to World evidence,
/// never a new World fact or independent completion proof.</summary>
public sealed record TraversalOperationRecord(
    string OperationId,
    string TemplateId,
    string ContainerId,
    string? TargetKey,
    string? ObservedState,
    string SourceRevisionId,
    IReadOnlyList<string> EvidenceRefs,
    bool Completed);

public sealed record TraversalGap(string ContainerId, string? CandidateKey, string Reason, IReadOnlyList<string> EvidenceRefs);
public sealed record TraversalExecutionView(
    string PlanId,
    int Version,
    PlanExecutionStatus Status,
    string? CurrentContainerId,
    int Depth,
    int OperationsCompleted,
    int WorkUsed,
    string? PauseReason,
    IReadOnlyList<PlanHandlerRecord> Handlers,
    IReadOnlyList<TraversalGap> Gaps,
    IReadOnlyList<string> CompletedWork,
    bool DiscoveryComplete,
    string? EndReason,
    IReadOnlyList<TraversalOperationRecord>? Operations = null);

/// <summary>Facts supplied to Control Loop Recovery; never an effect replay request.</summary>
public sealed record TraversalRecoveryFacts(
    PlanExecutionPosition Position,
    TraversalExecutionView Traversal,
    IReadOnlyList<PlanAttempt> Attempts,
    int StepsConsumed,
    int ConsultationsConsumed);
