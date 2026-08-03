using System.Reflection;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// TraceFields catalog-integrity tests (change trace-parent-linkage M0, design D2).
/// The constant VALUES are frozen JSONL persistence / downstream-consumption
/// contracts: a key-value freeze assertion locks each one, and the full-key
/// list asserts every attribute key used in production is a catalog member.
/// </summary>
public class TraceFieldsTests
{
    /// <summary>
    /// Every trace attribute key emitted in production (collected from the M0
    /// code sweep: PageAnalyzer, TraversalEngine, InterceptionHandler,
    /// TraversalFSM, SafetyGate, CompletionMonitor, ErrorLoopAnalyzer,
    /// EnumerateCompletionAnalyzer). Must be a TraceFields constant value.
    /// </summary>
    private static readonly string[] ProductionKeys =
    {
        // ai.call / ai.analyze
        "ai.capability", "ai.mode", "ai.success", "ai.provider_id", "ai.model",
        "ai.tokens", "ai.latency_ms", "ai.item_count", "ai.retry_count",
        // action.*
        "action.type", "action.result", "action.wait_ms", "action.adb_ms",
        // entry.visited / skipped / generate / observed / ignored
        "entry.name", "entry.node_id", "entry.step", "entry.depth",
        "entry.rule_id", "entry.reason",
        "entry.parent_node", "entry.fingerprint",
        "entry.parent", "entry.match_rule", "entry.index",
        "entry.match_count", "entry.ignored_count",
        // analyze.completion
        "analyze.observed", "analyze.visited", "analyze.skipped", "analyze.pending",
        "analyze.end_reached", "analyze.p50", "analyze.p95", "analyze.cold_start",
        "analyze.rule", "analyze.abnormal_spike",
        // completion poll
        "poll.verdict", "poll.confidence", "poll.action", "poll.escalated",
        "poll.callback_outcome",
        // analyze.error_loop
        "error.reason", "error.consecutive_steps", "error.skipped", "error.visited",
    };

    [Fact(DisplayName = "TraceFields: every production attribute key is a catalog member")]
    public void AllProductionKeys_AreCatalogMembers()
    {
        foreach (var key in ProductionKeys)
            Assert.True(
                TraceFields.IsKnown(key),
                $"Production attribute key '{key}' is missing from the TraceFields catalog.");
    }

    [Fact(DisplayName = "TraceFields: every constant value is non-empty and layer.namespaced")]
    public void EveryConstant_IsNonEmpty_AndLayerNamespaced()
    {
        foreach (var (name, value) in EnumerateCatalogConstants())
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"TraceFields.{name} must not be empty.");
            Assert.Contains('.', value);
            Assert.True(value.Split('.')[0].Length > 0, $"TraceFields.{name} must have a layer prefix.");
        }
    }

    [Fact(DisplayName = "TraceFields: key values are frozen (JSONL contract locks)")]
    public void KeyValues_AreFrozen()
    {
        // ai.call / ai.analyze
        Assert.Equal("ai.capability", TraceFields.AiCapability);
        Assert.Equal("ai.mode", TraceFields.AiMode);
        Assert.Equal("ai.success", TraceFields.AiSuccess);
        Assert.Equal("ai.provider_id", TraceFields.AiProviderId);
        Assert.Equal("ai.model", TraceFields.AiModel);
        Assert.Equal("ai.tokens", TraceFields.AiTokens);
        Assert.Equal("ai.latency_ms", TraceFields.AiLatencyMs);
        Assert.Equal("ai.item_count", TraceFields.AiItemCount);
        Assert.Equal("ai.retry_count", TraceFields.AiRetryCount);

        // action.*
        Assert.Equal("action.type", TraceFields.ActionType);
        Assert.Equal("action.result", TraceFields.ActionResult);
        Assert.Equal("action.wait_ms", TraceFields.ActionWaitMs);
        Assert.Equal("action.adb_ms", TraceFields.ActionAdbMs);

        // entry.*
        Assert.Equal("entry.name", TraceFields.EntryName);
        Assert.Equal("entry.node_id", TraceFields.EntryNodeId);
        Assert.Equal("entry.step", TraceFields.EntryStep);
        Assert.Equal("entry.depth", TraceFields.EntryDepth);
        Assert.Equal("entry.rule_id", TraceFields.EntryRuleId);
        Assert.Equal("entry.reason", TraceFields.EntryReason);
        Assert.Equal("entry.parent_node", TraceFields.EntryParentNode);
        Assert.Equal("entry.fingerprint", TraceFields.EntryFingerprint);
        Assert.Equal("entry.parent", TraceFields.EntryParent);
        Assert.Equal("entry.match_rule", TraceFields.EntryMatchRule);
        Assert.Equal("entry.index", TraceFields.EntryIndex);
        Assert.Equal("entry.match_count", TraceFields.EntryMatchCount);
        Assert.Equal("entry.ignored_count", TraceFields.EntryIgnoredCount);

        // analyze.completion
        Assert.Equal("analyze.observed", TraceFields.AnalyzeObserved);
        Assert.Equal("analyze.visited", TraceFields.AnalyzeVisited);
        Assert.Equal("analyze.skipped", TraceFields.AnalyzeSkipped);
        Assert.Equal("analyze.pending", TraceFields.AnalyzePending);
        Assert.Equal("analyze.end_reached", TraceFields.AnalyzeEndReached);
        Assert.Equal("analyze.p50", TraceFields.AnalyzeP50);
        Assert.Equal("analyze.p95", TraceFields.AnalyzeP95);
        Assert.Equal("analyze.cold_start", TraceFields.AnalyzeColdStart);
        Assert.Equal("analyze.rule", TraceFields.AnalyzeRule);
        Assert.Equal("analyze.abnormal_spike", TraceFields.AnalyzeAbnormalSpike);

        // completion poll
        Assert.Equal("poll.verdict", TraceFields.PollVerdict);
        Assert.Equal("poll.confidence", TraceFields.PollConfidence);
        Assert.Equal("poll.action", TraceFields.PollAction);
        Assert.Equal("poll.escalated", TraceFields.PollEscalated);
        Assert.Equal("poll.callback_outcome", TraceFields.PollCallbackOutcome);

        // analyze.error_loop
        Assert.Equal("error.reason", TraceFields.ErrorReason);
        Assert.Equal("error.consecutive_steps", TraceFields.ErrorConsecutiveSteps);
        Assert.Equal("error.skipped", TraceFields.ErrorSkipped);
        Assert.Equal("error.visited", TraceFields.ErrorVisited);
    }

    [Fact(DisplayName = "TraceFields: catalog All set matches declared constants exactly")]
    public void AllSet_MatchesDeclaredConstants()
    {
        var declared = EnumerateCatalogConstants().Select(kv => kv.Value).ToHashSet();
        Assert.Equal(declared, TraceFields.All);
    }

    /// <summary>Reflect all public const string fields of TraceFields.</summary>
    private static IEnumerable<(string Name, string Value)> EnumerateCatalogConstants()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return typeof(TraceFields)
            .GetFields(flags)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetValue(null)!));
    }
}
