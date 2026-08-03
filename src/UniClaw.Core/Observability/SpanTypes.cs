namespace UniClaw.Core.Observability;

/// <summary>
/// SpanTypes — static catalog of every emitted dotted spanType string.
/// Used by both instrumentation (StartSpan) and queries (GetSpansByType).
/// The string namespace is intentionally open-ended; the SpanType enum (constitution-locked C-11)
/// is NOT extended. Every spanType emitted in production or test must be a member of this catalog.
/// </summary>
public static class SpanTypes
{
    // ── Engine layer ────────────────────────────────────────
    public const string EngineRun = "engine.run";
    public const string EngineStep = "engine.step";

    // ── Entry layer ─────────────────────────────────────────
    public const string EntryGenerate = "entry.generate";
    public const string EntryObserved = "entry.observed";
    public const string EntryIgnored = "entry.ignored";
    public const string EntryVisited = "entry.visited";
    public const string EntrySkipped = "entry.skipped";
    public const string EntryAction = "entry.action";

    // ── Action layer ────────────────────────────────────────
    public const string ActionClick = "action.click";
    public const string ActionScroll = "action.scroll";
    public const string ActionBack = "action.back";
    public const string ActionLaunch = "action.launch";
    public const string ActionWait = "action.wait";

    // ── AI layer ────────────────────────────────────────────
    public const string AiCall = "ai.call";
    public const string AiAnalyze = "ai.analyze";

    // ── AI sub-spans (local vision timing) ──────────────────
    public const string AiYolo = "ai.yolo";
    public const string AiOcr = "ai.ocr";
    public const string AiFusion = "ai.fusion";
    public const string AiScroll = "ai.scroll";

    // ── Analysis layer ──────────────────────────────────────
    public const string AnalyzeCompletion = "analyze.completion";
    public const string AnalyzeErrorLoop = "analyze.error_loop";
    public const string AnalyzeTree = "analyze.tree";

    /// <summary>All known spanType strings (for catalog-membership tests).</summary>
    public static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        EngineRun, EngineStep,
        EntryGenerate, EntryObserved, EntryIgnored, EntryVisited, EntrySkipped, EntryAction,
        ActionClick, ActionScroll, ActionBack, ActionLaunch, ActionWait,
        AiCall, AiAnalyze, AiYolo, AiOcr, AiFusion, AiScroll,
        AnalyzeCompletion, AnalyzeErrorLoop, AnalyzeTree,
    };

    /// <summary>Check whether a spanType is a known catalog member.</summary>
    public static bool IsKnown(string spanType) => All.Contains(spanType);
}
