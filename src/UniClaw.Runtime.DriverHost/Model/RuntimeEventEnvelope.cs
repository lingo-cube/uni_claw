using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Logical envelope for one projected runtime event (design.md §3).
/// Transport-neutral: serialization and wire format are explicitly deferred.
///
/// <list type="bullet">
/// <item><see cref="EventId"/> — stable, unique within the projected run; assigned by the append-only store.</item>
/// <item><see cref="Sequence"/> — monotonic within the projected run; ordering metadata ONLY.
/// NOT world truth, NOT semantic identity.</item>
/// <item><see cref="ObservationSequence"/> — Kernel-assigned <c>Observation.SequenceNumber</c>
/// anchor when truthfully attributable. Never equal to <see cref="Sequence"/>.</item>
/// <item><see cref="CorrelationId"/> — protocol/run operation correlation (TraceId reuse).</item>
/// <item><see cref="CausationId"/> — semantic causal relation ONLY where truthfully known;
/// never populated merely because two events occurred nearby.</item>
/// </list>
/// </summary>
public sealed record RuntimeEventEnvelope
{
    /// <summary>Stable unique event identity within the projected run (store-assigned).</summary>
    public string EventId { get; init; } = "";

    /// <summary>Run identity.</summary>
    public string RunId { get; init; } = "";

    /// <summary>Monotonic projected ordering metadata — NOT world truth, NOT identity.</summary>
    public long Sequence { get; init; }

    /// <summary>Audited event family.</summary>
    public RuntimeEventKind Kind { get; init; }

    /// <summary>Protocol/run operation correlation (TraceId reuse); null = none recorded.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Semantic causal relation only where truthfully known; null in this slice.</summary>
    public string? CausationId { get; init; }

    /// <summary>Kernel-assigned Observation.SequenceNumber anchor when attributable; null = none.</summary>
    public long? ObservationSequence { get; init; }

    /// <summary>Logical evidence references (never embedded content).</summary>
    public ImmutableArray<EvidenceRef> EvidenceRefs { get; init; } = [];

    /// <summary>Kind-specific minimal facts from the classified source.</summary>
    public RuntimeEventPayload? Payload { get; init; }
}

/// <summary>
/// Base of kind-specific typed payloads. Emitted kinds only — kinds whose
/// source is not truthfully reachable (or C-class) never construct a payload.
/// </summary>
public abstract record RuntimeEventPayload
{
    protected RuntimeEventPayload() { }
}

/// <summary>B — a fresh Observation became available (Agent.NavigationEvidence).</summary>
/// <param name="SequenceNumber">Kernel-assigned observation sequence (world evidence anchor).</param>
/// <param name="ForegroundApplication">Foreground app evidence; null = unavailable.</param>
/// <param name="ElementCount">Number of observed elements.</param>
public sealed record ObservationProducedPayload(
    long SequenceNumber,
    string? ForegroundApplication,
    int ElementCount) : RuntimeEventPayload;

/// <summary>A — container refresh span evidence (span: container.refresh).</summary>
/// <param name="SpanId">Corresponding observability span id.</param>
/// <param name="Outcome">Observability outcome (SUCCEEDED/FAILED/CANCELLED/UNKNOWN) — structural, not semantic.</param>
/// <param name="StartOffsetNs">Span start offset (monotonic clock).</param>
/// <param name="DurationNs">Span duration (monotonic clock).</param>
public sealed record ContainerReconciledPayload(
    string SpanId,
    string Outcome,
    long StartOffsetNs,
    long DurationNs) : RuntimeEventPayload;

/// <summary>A+B — one dispatched semantic action (TraceEvent ActionId/Action + traversal.execution span skeleton).</summary>
/// <param name="ActionId">Dispatch record action id (e.g. "Action-1").</param>
/// <param name="StepId">Traversal step id; null = not recorded on this trace event.</param>
/// <param name="ContainerId">Semantic container id; null = not recorded.</param>
/// <param name="ActionDescription">Deterministic action summary (no coordinates, no bounds).</param>
public sealed record ActionDispatchedPayload(
    string ActionId,
    string? StepId,
    string? ContainerId,
    string ActionDescription) : RuntimeEventPayload;

/// <summary>B — accepted cross-container transition evidence (Agent.NavigationEvidence).</summary>
public sealed record NavigationDecisionPayload(
    long SequenceNumber,
    string? ForegroundApplication,
    int ElementCount) : RuntimeEventPayload;

/// <summary>B — bounded viewport exploration decision (TraceEvent.Reason classified prefix).</summary>
/// <param name="Outcome">Parsed outcome token (continue/exhausted/unresolved).</param>
/// <param name="SourceObservationSequence">source-seq reference parsed from the classified Reason prefix.</param>
/// <param name="ContainerId">Container id from the trace event; null = none.</param>
/// <param name="StepId">Step id from the trace event; null = none.</param>
public sealed record ViewportExplorationDecisionPayload(
    string Outcome,
    long SourceObservationSequence,
    string? ContainerId,
    string? StepId) : RuntimeEventPayload;

/// <summary>B — trap emission (TraceEvent TrapKind/TrapScope + matching Agent.LastTrap refs).</summary>
public sealed record TrapRaisedPayload(
    TrapKind TrapKind,
    TrapScope TrapScope,
    long? ExpectedSequence,
    long? ObservedSequence,
    string? ContainerId,
    string? StepId) : RuntimeEventPayload;

/// <summary>B — recovery started (TraceEvent.RecoveryId).</summary>
public sealed record RecoveryStartedPayload(
    string RecoveryId,
    string? Reason,
    string? ContainerId,
    string? StepId) : RuntimeEventPayload;

/// <summary>B (partial) — goal evidence produced; full record not on Agent public surface.</summary>
/// <param name="Reason">Completion reason recorded on the trace.</param>
/// <param name="IsPartial">Always true in this slice: SourceObservationSequence is not publicly available.</param>
public sealed record GoalEvidenceProducedPayload(
    string Reason,
    bool IsPartial) : RuntimeEventPayload;

/// <summary>B — run completed (Agent.State=Completed + Reason).</summary>
public sealed record RunCompletedPayload(string Reason) : RuntimeEventPayload;

/// <summary>B — run failed (Agent.State=Failed + Reason).</summary>
public sealed record RunFailedPayload(string Reason) : RuntimeEventPayload;
