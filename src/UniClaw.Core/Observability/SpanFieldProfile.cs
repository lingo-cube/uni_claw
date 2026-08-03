using System.Collections.Immutable;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Observability;

/// <summary>
/// SpanFieldProfile — 同 span 按 TraceLevel 分级记录字段的描述符
/// (trace-parent-linkage design D3, change M2)。
/// Basic = 核心结果/行为语义字段（Basic 级别保留）；Extended = 定位/计费/耗时细节字段
/// （Detailed+ 才记录）。分级不新增/改变 TraceFields 常量值（JSONL 契约冻结）。
/// 过滤由 <see cref="Filter"/> 在 helper 层执行（BeginSpanAsync 创建与 TraceSpanScope.End 合并时）。
/// </summary>
public sealed class SpanFieldProfile
{
    /// <summary>核心字段 — Basic 级别保留（TraceFields 常量）。</summary>
    public IReadOnlyList<string> Basic { get; }

    /// <summary>扩展字段 — 仅 Detailed+ 级别记录（TraceFields 常量）。</summary>
    public IReadOnlyList<string> Extended { get; }

    /// <summary>构造分级描述符。basic / extended 为 TraceFields 常量键名（目录成员由测试强制）。</summary>
    public SpanFieldProfile(IReadOnlyList<string> Basic, IReadOnlyList<string> Extended)
    {
        ArgumentNullException.ThrowIfNull(Basic);
        ArgumentNullException.ThrowIfNull(Extended);
        this.Basic = ImmutableArray.CreateRange(Basic);
        this.Extended = ImmutableArray.CreateRange(Extended);
    }

    /// <summary>
    /// 按 level 过滤属性字典（trace-parent-linkage M2 过滤规则，3.4）：
    /// <list type="bullet">
    ///   <item>profile == null → 不过滤（现状行为，span 全量记录）。</item>
    ///   <item>level >= Detailed → 不过滤（全量 —— 缺省 Detailed 是向后兼容的根基）。</item>
    ///   <item>level >= Basic → 仅保留 profile.Basic 中的键。</item>
    ///   <item>level &lt; Basic（None）→ 返回空字典（span 照常记录，status 保留）。</item>
    /// </list>
    /// </summary>
    internal static Dictionary<string, object>? Filter(
        Dictionary<string, object>? attributes,
        SpanFieldProfile? profile,
        TraceLevel level)
    {
        if (profile is null || level >= TraceLevel.Detailed)
            return attributes;

        if (level >= TraceLevel.Basic)
        {
            if (attributes is null)
                return null;
            var basic = profile.Basic;
            var filtered = new Dictionary<string, object>(attributes.Count);
            foreach (var (key, value) in attributes)
            {
                if (basic.Contains(key, StringComparer.Ordinal))
                    filtered[key] = value;
            }
            return filtered;
        }

        // level < Basic (None) — span 照常记录，属性为空字典。
        return new Dictionary<string, object>();
    }
}

/// <summary>
/// TraceSpanFields — 每 spanType 一个 <see cref="SpanFieldProfile"/> 实例
/// (trace-parent-linkage D3 / M2)。覆盖 TraceFields 目录全部 45 键
/// （Basic ∪ Extended = 全部；完整性由 SpanFieldLevelsTests 反射断言）。
/// 分级原则：结果/行为语义键（ai.success、action.type/result、entry.name、
/// analyze.observed/visited/.../rule、error.reason 等）→ Basic；
/// 定位/计费/耗时细节键（ai.provider_id/model/tokens/latency_ms/item_count/retry_count、
/// action.adb_ms/wait_ms、entry.node_id/step/depth/rule_id/...、analyze.p50/p95/...、
/// error.consecutive_steps/skipped/visited、poll.*）→ Extended。
/// </summary>
public static class TraceSpanFields
{
    /// <summary>
    /// ai.call — PageAnalyzer 视觉模型调用 span。Basic: 成功/模式/能力（结果语义）；
    /// Extended: provider/model/tokens/latency（计费与耗时细节）。ai.yolo/ocr/fusion/scroll
    /// （LocalVisionProvider）仅发 ai.latency_ms，无独立 profile，由本 profile 覆盖目录。
    /// </summary>
    public static readonly SpanFieldProfile AiCall = new(
        Basic: [TraceFields.AiSuccess, TraceFields.AiMode, TraceFields.AiCapability],
        Extended: [TraceFields.AiProviderId, TraceFields.AiModel, TraceFields.AiTokens, TraceFields.AiLatencyMs]);

    /// <summary>
    /// ai.analyze — PageAnalysis 完成标记（parent = ai.call）。仅 detail 键
    /// （item_count/retry_count），Basic 级别下该 span 属性为空。
    /// </summary>
    public static readonly SpanFieldProfile AiAnalyze = new(
        Basic: [],
        Extended: [TraceFields.AiItemCount, TraceFields.AiRetryCount]);

    /// <summary>
    /// action.wait — SafetyGate 等待 span。Basic: type/result；Extended: wait_ms。
    /// </summary>
    public static readonly SpanFieldProfile ActionWait = new(
        Basic: [TraceFields.ActionType, TraceFields.ActionResult],
        Extended: [TraceFields.ActionWaitMs]);

    /// <summary>
    /// action.click / action.scroll / action.back / action.launch — SafetyGate ADB 执行 span
    /// （四者键集相同，共用一份 profile）。Basic: type/result；Extended: adb_ms。
    /// </summary>
    public static readonly SpanFieldProfile Action = new(
        Basic: [TraceFields.ActionType, TraceFields.ActionResult],
        Extended: [TraceFields.ActionAdbMs]);

    /// <summary>
    /// entry.visited — InterceptionHandler 子节点入栈事件。Basic: name；Extended: node_id/step/depth。
    /// </summary>
    public static readonly SpanFieldProfile EntryVisited = new(
        Basic: [TraceFields.EntryName],
        Extended: [TraceFields.EntryNodeId, TraceFields.EntryStep, TraceFields.EntryDepth]);

    /// <summary>
    /// entry.skipped — SafetyGate deny 分支事件。Basic: name/reason（行为语义）；
    /// Extended: rule_id（定位细节）。
    /// </summary>
    public static readonly SpanFieldProfile EntrySkipped = new(
        Basic: [TraceFields.EntryName, TraceFields.EntryReason],
        Extended: [TraceFields.EntryRuleId]);

    /// <summary>
    /// entry.generate — 动态子节点生成管道 span。
    /// 注意：entry.generate/observed/ignored 当前经 ITraceCoordinator.StartSpan 同步 passthrough
    /// （TraceCoordinator seam，AC3 白名单冻结 —— 不做分级过滤）。本 profile 用于目录覆盖完整性
    /// （parent_node/fingerprint/parent/match_rule/index/match_count/ignored_count 仅此处可达）
    /// 及未来 helper 路由；passthrough 调用点不传 profile。
    /// </summary>
    public static readonly SpanFieldProfile EntryGenerate = new(
        Basic: [],
        Extended:
        [
            TraceFields.EntryParentNode, TraceFields.EntryFingerprint,
            TraceFields.EntryParent, TraceFields.EntryMatchRule, TraceFields.EntryIndex,
            TraceFields.EntryMatchCount, TraceFields.EntryIgnoredCount,
        ]);

    /// <summary>
    /// analyze.completion — EnumerateCompletionAnalyzer 每次评估 span。
    /// Basic: observed/visited/skipped/pending/end_reached/rule（结果语义）；
    /// Extended: p50/p95/cold_start/abnormal_spike（耗时细节）。
    /// </summary>
    public static readonly SpanFieldProfile AnalyzeCompletion = new(
        Basic:
        [
            TraceFields.AnalyzeObserved, TraceFields.AnalyzeVisited, TraceFields.AnalyzeSkipped,
            TraceFields.AnalyzePending, TraceFields.AnalyzeEndReached, TraceFields.AnalyzeRule,
        ],
        Extended:
        [
            TraceFields.AnalyzeP50, TraceFields.AnalyzeP95, TraceFields.AnalyzeColdStart,
            TraceFields.AnalyzeAbnormalSpike,
        ]);

    /// <summary>
    /// analyze.error_loop — ErrorLoopAnalyzer 终止 verdict span。
    /// Basic: reason（结果语义）；Extended: consecutive_steps/skipped/visited（定位细节）。
    /// </summary>
    public static readonly SpanFieldProfile ErrorLoop = new(
        Basic: [TraceFields.ErrorReason],
        Extended: [TraceFields.ErrorConsecutiveSteps, TraceFields.ErrorSkipped, TraceFields.ErrorVisited]);

    /// <summary>
    /// CompletionMonitor 轮询 span（spanType 复用 analyze.completion / analyze.error_loop，
    /// 但属性为 poll.* 键集，故独立 profile）。poll.* 全为 Extended
    /// （verdict/confidence/action/escalated/callback_outcome 均为细节/决策路径信息）。
    /// </summary>
    public static readonly SpanFieldProfile Poll = new(
        Basic: [],
        Extended:
        [
            TraceFields.PollVerdict, TraceFields.PollConfidence, TraceFields.PollAction,
            TraceFields.PollEscalated, TraceFields.PollCallbackOutcome,
        ]);
}
