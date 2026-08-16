using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Wire DTOs for the DriverHost transport (protocol baseline §9 — TRANSPORT_DEFERRED
/// now resolved to ONE concrete local transport: loopback TCP, newline-delimited
/// JSON-RPC). These are KERNEL-FACT COPIES: plain immutable data built from the
/// DriverHost read models, never live objects, never mutable references. The DSH
/// side consumes them as read-only structured data.
///
/// Field classification is preserved end-to-end: every snapshot field carries
/// { value, classification, truthSource, isPartial } so a NotCurrentlyAvailable
/// field stays VISIBLY unavailable — it is never collapsed into null-as-truth.
/// </summary>
public static class UniClawWireContract
{
    /// <summary>Protocol version of this wire contract.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Identity value returned by ping.</summary>
    public const string ServiceName = "dsh-uniclaw-driverhost";

    /// <summary>Archived baseline change this contract is built against.</summary>
    public const string BaselineChange = "dsh-uniclaw-control-plane-protocol-baseline";

    /// <summary>Typed protocol error: malformed request or missing parameter.</summary>
    public const string ErrorBadRequest = "bad_request";

    /// <summary>Typed protocol error: method not in the read-only method table.</summary>
    public const string ErrorUnknownMethod = "unknown_method";

    /// <summary>Typed protocol error: dispatch failure (fail-open, connection stays usable).</summary>
    public const string ErrorInternalError = "internal_error";
}

/// <summary>One classified snapshot field on the wire (classification + truth source preserved).</summary>
/// <param name="Value">Field value; null when not currently available (never invented).</param>
/// <param name="Classification">Audited classification: directPublicProjection, derivedReadModel, notCurrentlyAvailable.</param>
/// <param name="TruthSource">Human-auditable source statement for the value.</param>
/// <param name="IsPartial">True for partial evidence (e.g. goal evidence without observation anchor).</param>
public sealed record UniClawFieldDto(
    JsonNode? Value,
    string Classification,
    string TruthSource,
    bool IsPartial);

/// <summary>Goal summary derived from the RunSemanticGoal span tag (DERIVED_READ_MODEL).</summary>
/// <param name="Goal">Goal text.</param>
public sealed record UniClawGoalDto(string Goal);

/// <summary>Latest decision-shaped trace event (DERIVED_READ_MODEL).</summary>
/// <param name="Reason">Decision reason when recorded.</param>
/// <param name="ActionId">Dispatched action id when recorded.</param>
/// <param name="StepId">Step id when attributable.</param>
/// <param name="ContainerId">Container id when attributable.</param>
public sealed record UniClawDecisionDto(
    string? Reason, string? ActionId, string? StepId, string? ContainerId);

/// <summary>Latest dispatched action trace event (DERIVED_READ_MODEL).</summary>
/// <param name="ActionId">Dispatched action id.</param>
/// <param name="StepId">Step id when attributable.</param>
/// <param name="ContainerId">Container id when attributable.</param>
/// <param name="ActionDescription">Deterministic human-readable action description.</param>
public sealed record UniClawActionDto(
    string ActionId, string? StepId, string? ContainerId, string ActionDescription);

/// <summary>Latest recovery trace event (DERIVED_READ_MODEL).</summary>
/// <param name="RecoveryId">Recovery id.</param>
/// <param name="Reason">Recovery reason when recorded.</param>
/// <param name="ContainerId">Container id when attributable.</param>
/// <param name="StepId">Step id when attributable.</param>
public sealed record UniClawRecoveryDto(
    string RecoveryId, string? Reason, string? ContainerId, string? StepId);

/// <summary>Partial goal evidence summary (full GoalEvidence not on the Agent public surface).</summary>
/// <param name="Satisfied">Whether completion evidence was satisfied.</param>
/// <param name="Reason">Completion reason when recorded.</param>
/// <param name="SourceObservationSequence">Kernel observation anchor; null because not on the public surface.</param>
/// <param name="IsPartial">Always true in this slice (partial evidence only).</param>
public sealed record UniClawGoalEvidenceDto(
    bool Satisfied, string? Reason, long? SourceObservationSequence, bool IsPartial);

/// <summary>Trap as an immutable wire copy (no live references).</summary>
/// <param name="Kind">TrapKind name.</param>
/// <param name="Scope">TrapScope name.</param>
/// <param name="Expected">Expected observation sequence when recorded.</param>
/// <param name="Observed">Observed observation sequence when recorded.</param>
/// <param name="Source">Trap source.</param>
/// <param name="Evidence">Trap evidence statement.</param>
/// <param name="LastActionDescription">Deterministic description of the last action, when recorded.</param>
public sealed record UniClawTrapDto(
    string Kind,
    string Scope,
    long? Expected,
    long? Observed,
    string Source,
    string Evidence,
    string? LastActionDescription);

/// <summary>RunSnapshot projection as an immutable wire copy.</summary>
/// <param name="RunId">Run identity.</param>
/// <param name="RunState">Classified Agent.State field.</param>
/// <param name="CurrentSemanticPage">Classified current semantic page field.</param>
/// <param name="ActiveTrap">Classified active trap field.</param>
/// <param name="CurrentGoal">Classified derived goal field.</param>
/// <param name="LastDecision">Classified derived last decision field.</param>
/// <param name="LastAction">Classified derived last action field.</param>
/// <param name="RecoveryState">Classified derived recovery field.</param>
/// <param name="LatestGoalEvidence">Classified partial goal evidence field.</param>
/// <param name="CurrentObservationSequence">Classified unavailable observation sequence field.</param>
/// <param name="CurrentContainerSummary">Classified unavailable container summary field.</param>
/// <param name="BindingsSummary">Classified unavailable bindings field.</param>
/// <param name="StateBeliefsSummary">Classified unavailable state beliefs field.</param>
/// <param name="Diagnostics">Projection diagnostics (never runtime authority).</param>
public sealed record UniClawRunSnapshotDto(
    string RunId,
    UniClawFieldDto RunState,
    UniClawFieldDto CurrentSemanticPage,
    UniClawFieldDto ActiveTrap,
    UniClawFieldDto CurrentGoal,
    UniClawFieldDto LastDecision,
    UniClawFieldDto LastAction,
    UniClawFieldDto RecoveryState,
    UniClawFieldDto LatestGoalEvidence,
    UniClawFieldDto CurrentObservationSequence,
    UniClawFieldDto CurrentContainerSummary,
    UniClawFieldDto BindingsSummary,
    UniClawFieldDto StateBeliefsSummary,
    ImmutableArray<string> Diagnostics);

/// <summary>Logical evidence ref as an immutable wire copy (never a filesystem path).</summary>
/// <param name="EvidenceId">Canonical evidence identity.</param>
/// <param name="Kind">EvidenceKind name.</param>
/// <param name="RunId">Run identity the evidence belongs to.</param>
/// <param name="ObservationSequence">Kernel observation anchor when attributable.</param>
/// <param name="ContentIdentity">Content-based identity when available.</param>
/// <param name="Maturity">Audited AssetMaturity name.</param>
/// <param name="SizeBytes">Byte count when known.</param>
/// <param name="Locator">LOGICAL locator key — never a path.</param>
public sealed record UniClawEvidenceRefDto(
    string EvidenceId,
    string Kind,
    string RunId,
    long? ObservationSequence,
    string? ContentIdentity,
    string Maturity,
    int? SizeBytes,
    string Locator);

/// <summary>Capture record metadata (never embedded content).</summary>
/// <param name="Order">Record order within the capture session.</param>
/// <param name="Kind">CaptureRecordKind name.</param>
/// <param name="SequenceNumber">Kernel observation sequence anchor when recorded.</param>
/// <param name="FrameId">Frame id when recorded.</param>
/// <param name="ActionId">Action id when recorded.</param>
/// <param name="ResultOutcome">Result outcome when recorded.</param>
/// <param name="Info">Free-form metadata when recorded.</param>
public sealed record UniClawCaptureRecordDto(
    int Order,
    string Kind,
    long SequenceNumber,
    string? FrameId,
    string? ActionId,
    string? ResultOutcome,
    string? Info);

/// <summary>Capture artifact metadata (never embedded content).</summary>
/// <param name="ArtifactId">Artifact identity.</param>
/// <param name="FrameId">Frame id when recorded.</param>
/// <param name="FileName">File name when recorded (metadata only, never resolved).</param>
/// <param name="ContentHash">Content-based identity when available.</param>
/// <param name="ByteCount">Byte count.</param>
public sealed record UniClawCaptureArtifactDto(
    string ArtifactId,
    string? FrameId,
    string? FileName,
    string? ContentHash,
    int ByteCount);

/// <summary>Evidence resolution result (metadata only, logical locator only).</summary>
/// <param name="Found">Whether the logical evidence resolved.</param>
/// <param name="Ref">Canonical ref when found.</param>
/// <param name="CaptureSessionId">Capture session the evidence resolves to.</param>
/// <param name="Record">Resolved capture record metadata when applicable.</param>
/// <param name="Artifact">Resolved capture artifact metadata when applicable.</param>
/// <param name="Diagnostic">Truthful diagnostic when not found.</param>
public sealed record UniClawEvidenceResolutionDto(
    bool Found,
    UniClawEvidenceRefDto? Ref,
    string? CaptureSessionId,
    UniClawCaptureRecordDto? Record,
    UniClawCaptureArtifactDto? Artifact,
    string? Diagnostic);

/// <summary>Run-scoped event cursor (projection order only — never observation progress).</summary>
/// <param name="RunId">Run identity the cursor belongs to.</param>
/// <param name="LastSequence">Last consumed projected sequence.</param>
public sealed record UniClawEventCursorDto(string RunId, long LastSequence);

/// <summary>One projected runtime event as an immutable wire copy.</summary>
/// <param name="EventId">Stable store-assigned event identity.</param>
/// <param name="RunId">Run identity.</param>
/// <param name="Sequence">Monotonic projected ordering metadata — NOT world truth.</param>
/// <param name="Kind">RuntimeEventKind name.</param>
/// <param name="CorrelationId">Protocol/run correlation when recorded.</param>
/// <param name="CausationId">Semantic causation ONLY where truthfully known.</param>
/// <param name="ObservationSequence">Kernel observation anchor when attributable.</param>
/// <param name="EvidenceRefs">Logical evidence refs attached to the event.</param>
/// <param name="Payload">Kind-specific payload as structured data (never interpreted by DSH).</param>
public sealed record UniClawRuntimeEventDto(
    string EventId,
    string RunId,
    long Sequence,
    string Kind,
    string? CorrelationId,
    string? CausationId,
    long? ObservationSequence,
    ImmutableArray<UniClawEvidenceRefDto> EvidenceRefs,
    JsonNode? Payload);

/// <summary>One runtime event page (GetAfter semantics preserved).</summary>
/// <param name="RunId">Run identity.</param>
/// <param name="Events">Events in projected order.</param>
/// <param name="NextCursor">Cursor to continue from; null when no more events.</param>
/// <param name="HasMore">Whether more events exist beyond this page.</param>
/// <param name="Diagnostics">Page diagnostics.</param>
public sealed record UniClawRuntimeEventPageDto(
    string RunId,
    ImmutableArray<UniClawRuntimeEventDto> Events,
    UniClawEventCursorDto? NextCursor,
    bool HasMore,
    ImmutableArray<string> Diagnostics);

/// <summary>Frozen control-audit result for one operation.</summary>
/// <param name="Operation">Audited operation name.</param>
/// <param name="Supported">Whether the DriverHost surface supports the operation read-only.</param>
/// <param name="Reason">Reason constant: READ_ONLY_INSPECT, DEFERRED_NO_KERNEL_CONTROL_BUYER, or UNKNOWN_OPERATION.</param>
/// <param name="Evidence">Source evidence strings.</param>
/// <param name="ReadOnly">Whether the operation is read-only (always true in this slice).</param>
public sealed record UniClawControlSupportDto(
    string Operation,
    bool Supported,
    string Reason,
    ImmutableArray<string> Evidence,
    bool ReadOnly);

/// <summary>Identity handshake result.</summary>
/// <param name="Service">DriverHost service name.</param>
/// <param name="ProtocolVersion">Wire contract version.</param>
/// <param name="BaselineChange">Archived baseline change name.</param>
public sealed record UniClawPingDto(
    string Service,
    int ProtocolVersion,
    string BaselineChange);

/// <summary>Registered run ids (read-only diagnostic view).</summary>
/// <param name="RunIds">Sorted registered run ids.</param>
public sealed record UniClawRunListDto(ImmutableArray<string> RunIds);

/// <summary>Classified active-trap read result.</summary>
/// <param name="RunId">Run identity.</param>
/// <param name="Found">Whether the run currently has an active trap.</param>
/// <param name="Trap">Classified trap field (null only when the result itself is absent).</param>
/// <param name="Diagnostic">Truthful diagnostic when the trap cannot be read.</param>
public sealed record UniClawTrapResultDto(
    string RunId,
    bool Found,
    UniClawFieldDto? Trap,
    string? Diagnostic);
