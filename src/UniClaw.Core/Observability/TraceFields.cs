namespace UniClaw.Core.Observability;

/// <summary>
/// TraceFields — static catalog of every emitted dotted span attribute key
/// (design D2, change trace-parent-linkage M0). Business code references these
/// constants instead of hand-written key strings; the values are frozen JSONL
/// persistence / downstream-consumption contracts and must never change.
/// Every key emitted in production or test must be a member of this catalog
/// (enforced by TraceFieldsTests).
/// </summary>
public static class TraceFields
{
    // ── ai.call / ai.analyze layer ──────────────────────────
    public const string AiCapability = "ai.capability";
    public const string AiMode = "ai.mode";
    public const string AiSuccess = "ai.success";
    public const string AiProviderId = "ai.provider_id";
    public const string AiModel = "ai.model";
    public const string AiTokens = "ai.tokens";
    public const string AiLatencyMs = "ai.latency_ms";
    public const string AiItemCount = "ai.item_count";
    public const string AiRetryCount = "ai.retry_count";

    // ── action layer ────────────────────────────────────────
    public const string ActionType = "action.type";
    public const string ActionResult = "action.result";
    public const string ActionWaitMs = "action.wait_ms";
    public const string ActionAdbMs = "action.adb_ms";

    // ── entry layer ─────────────────────────────────────────
    public const string EntryName = "entry.name";
    public const string EntryNodeId = "entry.node_id";
    public const string EntryStep = "entry.step";
    public const string EntryDepth = "entry.depth";
    public const string EntryRuleId = "entry.rule_id";
    public const string EntryReason = "entry.reason";
    public const string EntryParentNode = "entry.parent_node";
    public const string EntryFingerprint = "entry.fingerprint";
    public const string EntryParent = "entry.parent";
    public const string EntryMatchRule = "entry.match_rule";
    public const string EntryIndex = "entry.index";
    public const string EntryMatchCount = "entry.match_count";
    public const string EntryIgnoredCount = "entry.ignored_count";

    // ── analyze.completion layer ────────────────────────────
    public const string AnalyzeObserved = "analyze.observed";
    public const string AnalyzeVisited = "analyze.visited";
    public const string AnalyzeSkipped = "analyze.skipped";
    public const string AnalyzePending = "analyze.pending";
    public const string AnalyzeEndReached = "analyze.end_reached";
    public const string AnalyzeP50 = "analyze.p50";
    public const string AnalyzeP95 = "analyze.p95";
    public const string AnalyzeColdStart = "analyze.cold_start";
    public const string AnalyzeRule = "analyze.rule";
    public const string AnalyzeAbnormalSpike = "analyze.abnormal_spike";

    // ── completion poll layer ───────────────────────────────
    public const string PollVerdict = "poll.verdict";
    public const string PollConfidence = "poll.confidence";
    public const string PollAction = "poll.action";
    public const string PollEscalated = "poll.escalated";
    public const string PollCallbackOutcome = "poll.callback_outcome";

    // ── analyze.error_loop layer ────────────────────────────
    public const string ErrorReason = "error.reason";
    public const string ErrorConsecutiveSteps = "error.consecutive_steps";
    public const string ErrorSkipped = "error.skipped";
    public const string ErrorVisited = "error.visited";

    /// <summary>All known attribute key strings (for catalog-membership tests).</summary>
    public static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        AiCapability, AiMode, AiSuccess, AiProviderId, AiModel, AiTokens, AiLatencyMs,
        AiItemCount, AiRetryCount,
        ActionType, ActionResult, ActionWaitMs, ActionAdbMs,
        EntryName, EntryNodeId, EntryStep, EntryDepth, EntryRuleId, EntryReason,
        EntryParentNode, EntryFingerprint, EntryParent, EntryMatchRule, EntryIndex,
        EntryMatchCount, EntryIgnoredCount,
        AnalyzeObserved, AnalyzeVisited, AnalyzeSkipped, AnalyzePending, AnalyzeEndReached,
        AnalyzeP50, AnalyzeP95, AnalyzeColdStart, AnalyzeRule, AnalyzeAbnormalSpike,
        PollVerdict, PollConfidence, PollAction, PollEscalated, PollCallbackOutcome,
        ErrorReason, ErrorConsecutiveSteps, ErrorSkipped, ErrorVisited,
    };

    /// <summary>Check whether an attribute key is a known catalog member.</summary>
    public static bool IsKnown(string key) => All.Contains(key);
}
