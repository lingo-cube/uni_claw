namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Audited 18-family semantic runtime event vocabulary (v0.5 audit —
/// openspec/changes/dsh-kernel-read-only-observability/design.md §2).
/// Every kind carries a source classification; C-class kinds are OUT OF SCOPE
/// for this slice and MUST NEVER be emitted.
/// </summary>
public enum RuntimeEventKind
{
    /// <summary>A fresh Observation became available through the public read model.</summary>
    ObservationProduced,

    /// <summary>Container refresh span evidence (span: container.refresh).</summary>
    ContainerReconciled,

    /// <summary>ObjectBindings delta — source not on Agent public surface; not emitted in this slice.</summary>
    BindingUpdated,

    /// <summary>ObjectStateBeliefs delta — source not on Agent public surface; not emitted in this slice.</summary>
    StateBeliefUpdated,

    /// <summary>C-class — no pre-dispatch decision record exists. NEVER emitted.</summary>
    DecisionProposed,

    /// <summary>C-class — no decision stream exists. NEVER emitted.</summary>
    DecisionAccepted,

    /// <summary>C-class — no authorization event is recorded. NEVER emitted.</summary>
    ActionAuthorized,

    /// <summary>Dispatch record evidence (span: traversal.execution + TraceEvent ActionId/Action).</summary>
    ActionDispatched,

    /// <summary>Post-action observation — journal not on Agent public surface; not emitted in this slice.</summary>
    PostActionObserved,

    /// <summary>Step-level journal result — journal not on Agent public surface; not emitted in this slice.</summary>
    VerificationCompleted,

    /// <summary>Accepted cross-container transition evidence (Agent.NavigationEvidence).</summary>
    NavigationDecision,

    /// <summary>Bounded viewport exploration decision (TraceEvent.Reason classified prefix).</summary>
    ViewportExplorationDecision,

    /// <summary>Trap emission (TraceEvent TrapKind/TrapScope + Agent.LastTrap).</summary>
    TrapRaised,

    /// <summary>Recovery start (TraceEvent.RecoveryId).</summary>
    RecoveryStarted,

    /// <summary>C-class — no recovery-verification result is recorded. NEVER emitted.</summary>
    RecoveryVerified,

    /// <summary>Partial goal evidence (State=Completed + Reason only; full record not on public surface).</summary>
    GoalEvidenceProduced,

    /// <summary>Run completed (Agent.State + Reason).</summary>
    RunCompleted,

    /// <summary>Run failed (Agent.State + Reason).</summary>
    RunFailed,
}

/// <summary>
/// Repository-audited event source classification (design.md §2):
/// A = derivable from an existing span; B = derivable from the existing public
/// read model; C = requires new runtime semantic emission (out of scope).
/// </summary>
public enum RuntimeEventSourceClassification
{
    /// <summary>A — DERIVABLE_FROM_EXISTING_SPAN.</summary>
    DerivableFromExistingSpan,

    /// <summary>B — DERIVABLE_FROM_EXISTING_PUBLIC_READ_MODEL.</summary>
    DerivableFromExistingPublicReadModel,

    /// <summary>A+B — span skeleton plus public read model content.</summary>
    DerivableFromExistingSpanAndPublicReadModel,

    /// <summary>C — REQUIRES_NEW_RUNTIME_SEMANTIC_EMISSION.</summary>
    RequiresNewRuntimeSemanticEmission,
}

/// <summary>One row of the audited classification table.</summary>
public sealed record RuntimeEventKindMetadata(
    RuntimeEventKind Kind,
    RuntimeEventSourceClassification Classification,
    bool EmittableInSlice,
    string? NotEmittedReason);
