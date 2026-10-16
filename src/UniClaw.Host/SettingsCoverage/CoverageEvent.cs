namespace UniClaw.Host.SettingsCoverage;

/// <summary>
/// Owner-owned facts emitted by the settings coverage ledger. These events are
/// deliberately separate from RuntimeRunEvent: the runtime store indexes a run,
/// while this stream describes coverage-owned facts inside that run.
/// </summary>
public enum CoverageEventType
{
    ObservationAccepted,
    PopupStateObserved,
    EffectReceiptProduced,
    PostActionVerified,
    TargetCoverageMarked,
}

/// <summary>Coverage mark state. Pending is represented by absence of a mark.</summary>
public enum TargetCoverageMarkKind
{
    ReachedOverlay,
    RouteEntered,
    Skipped,
}

/// <summary>
/// Append-only, typed coverage fact. Nullable fields are intentionally scoped
/// to the event kind; no free-form action or decision is encoded here.
/// </summary>
public sealed record CoverageEvent(
    long Sequence,
    CoverageEventType Type,
    string Owner,
    string? Target = null,
    string? Route = null,
    string? DecisionId = null,
    string? Reason = null,
    IReadOnlyList<string>? EvidenceRefs = null,
    TargetCoverageMarkKind? MarkKind = null,
    bool? Verified = null,
    string? ReceiptId = null,
    string? CaptureId = null);

/// <summary>Current target mark projection, separate from structural exclusions.</summary>
public sealed record TargetCoverageMark(
    string Target,
    TargetCoverageMarkKind Kind,
    string? DecisionId,
    string? Reason,
    IReadOnlyList<string> EvidenceRefs,
    long EventSequence);
