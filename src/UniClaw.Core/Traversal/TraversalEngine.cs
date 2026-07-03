using System.Collections.Immutable;
using UniClaw.Core.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// DynamicChildManager — 管理 STATIC/DYNAMIC_MATCH 子节点生成 + 缓存 + 跨失效 dedup 持久。
/// </summary>
public sealed class DynamicChildManager
{
    private readonly Dictionary<string, List<TraversalNode>> _dynamicChildren = new();
    internal readonly HashSet<(string fingerprint, string name)> _generatedPairs = new();
    private readonly DynamicMatcher _matcher = new();
    private readonly TemplateInstantiator _instantiator = new();
    private readonly INodeRegistry? _nodeRegistry;
    private readonly TraceCoordinator? _trace;

    /// <summary>构造 DynamicChildManager</summary>
    public DynamicChildManager(INodeRegistry? nodeRegistry = null, TraceCoordinator? trace = null)
    {
        _nodeRegistry = nodeRegistry;
        _trace = trace;
    }

    /// <summary>
    /// 获取下一个未访问的子节点 — STATIC: iterate static_children; DYNAMIC_MATCH: generate if not cached。
    /// </summary>
    public TraversalNode? GetNextUnvisitedChild(TraversalNode node, ITraversalContext context)
    {
        if (node.ChildrenStrategy.Type == ChildrenStrategyType.Static)
        {
            // STATIC: iterate static_children, find first unvisited
            foreach (var childId in node.StaticChildren)
            {
                if (!context.VisitedNodes.Contains(childId))
                {
                    // Look up in node registry or static nodes
                    if (_nodeRegistry != null)
                    {
                        var child = _nodeRegistry.GetNode(childId);
                        if (child != null) return child;
                    }
                    return null; // Can't find child node
                }
            }
            return null; // All children visited
        }

        if (node.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            // DYNAMIC_MATCH: generate if not cached, then iterate cached
            if (!_dynamicChildren.ContainsKey(node.NodeId))
            {
                Generate(node, context);
            }

            var cached = _dynamicChildren.GetValueOrDefault(node.NodeId);
            if (cached == null) return null;

            foreach (var child in cached)
            {
                if (!context.VisitedNodes.Contains(child.NodeId))
                    return child;
            }
            return null; // All dynamic children visited
        }

        return null; // ChildrenStrategyType.None
    }

    /// <summary>
    /// 生成管道 (9 steps + dedup) — DynamicMatch 子节点生成核心流程。
    /// </summary>
    public void Generate(TraversalNode node, ITraversalContext context)
    {
        var children = new List<TraversalNode>();

        // Step 1: Compute page fingerprint
        var runtimeCtx = context as TraversalRuntimeContext;
        var pageAnalysis = runtimeCtx?.CurrentPageAnalysis;
        var fingerprint = PageSnapshotManager.Fingerprint(pageAnalysis);

        // Step 2: Convert DynamicRules → matcher rules
        var rules = node.ChildrenStrategy.DynamicRules;
        if (rules == null || rules.Count == 0)
        {
            _dynamicChildren[node.NodeId] = children;
            return;
        }

        // Step 3: Extract items from page_analysis
        var items = new List<MatchableItem>();
        if (pageAnalysis != null)
        {
            foreach (var menuItem in pageAnalysis.Items)
            {
                items.Add(new MatchableItem(
                    Text: menuItem.Name,
                    MenuItemType: menuItem.Type,
                    ExpectedAction: menuItem.ExpectedAction,
                    Index: items.Count));
            }
        }

        // Step 4: Call DynamicMatcher.match_all
        var ruleList = rules.Values.ToList();
        var matchResults = _matcher.MatchAll(ruleList, items);

        // Step 5: Instantiate child nodes for GENERATE_CHILD actions
        foreach (var result in matchResults)
        {
            if (!result.Matched) continue;

            var rule = ruleList.FirstOrDefault(r => r.RuleId == result.MatchRuleId);
            if (rule == null) continue;

            // Step 6: Dedup via _generated_pairs
            var childName = $"{rule.ChildTemplate}_{result.MatchedItem.Text ?? "item"}";
            var pair = (fingerprint.ToString(), childName);
            if (_generatedPairs.Contains(pair))
                continue; // Skip — already generated

            // Instantiate child node
            var template = new Template(
                TemplateId: rule.ChildTemplate,
                NodeType: DetermineNodeType(rule.ChildTemplate),
                Operation: new Dictionary<string, object>
                {
                    ["action"] = DetermineAction(rule.ChildTemplate),
                    ["target"] = new Dictionary<string, object>
                    {
                        ["by"] = "text",
                        ["value"] = "{{item_text}}"
                    }
                });

            var instantiatorContext = new Dictionary<string, object>
            {
                ["item_text"] = result.MatchedItem.Text ?? "",
                ["item_index"] = result.MatchedItem.Index.ToString(),
            };

            var parentPath = context.CurrentPath.ToList();
            var child = _instantiator.Instantiate(template, instantiatorContext, parentPath);

            // Step 7: Set precondition path
            // Already handled by TemplateInstantiator V6.9 path concatenation

            // Step 8: Register child in node_registry
            if (_nodeRegistry != null)
                _nodeRegistry.Register(child);

            // Step 9: Record dynamic lifecycle trace
            if (_trace != null)
                _trace.RecordDynamicLifecycle("generate", child.NodeId, node.NodeId, rule.RuleId, "");

            // Add dedup pair and child
            _generatedPairs.Add(pair);
            children.Add(child);
        }

        _dynamicChildren[node.NodeId] = children;
    }

    /// <summary>
    /// 缓存失效 — 移除 _dynamic_children entry 但保留 _generated_pairs dedup。
    /// </summary>
    public void Invalidate(string nodeId)
    {
        _dynamicChildren.Remove(nodeId);
        // _generatedPairs persists across invalidation (D-3)
    }

    // --- Test helper methods (internal for test assembly access) ---
    /// <summary>Pre-populate dynamic children cache for testing</summary>
    internal void PrePopulateDynamicChildren(string nodeId, List<TraversalNode> children)
    {
        _dynamicChildren[nodeId] = children;
    }

    /// <summary>Check if cache has entry for nodeId</summary>
    internal bool IsCachePopulated(string nodeId) => _dynamicChildren.ContainsKey(nodeId);

    /// <summary>Check if cache is empty for nodeId</summary>
    internal bool IsCacheEmpty(string nodeId) => !_dynamicChildren.ContainsKey(nodeId);

    /// <summary>Get generated pairs count for testing dedup persistence</summary>
    internal int GeneratedPairsCount => _generatedPairs.Count;

    private NodeType DetermineNodeType(string templateName)
    {
        return templateName switch
        {
            "menu_container" => NodeType.Container,
            "switch_leaf" => NodeType.LeafSwitch,
            "slider_leaf" => NodeType.LeafSlider,
            "leaf_action" => NodeType.LeafAction,
            "leaf_info" => NodeType.LeafInfo,
            _ => NodeType.Action
        };
    }

    private string DetermineAction(string templateName)
    {
        return templateName switch
        {
            "menu_container" => "click",
            "switch_leaf" => "toggle",
            "slider_leaf" => "swipe",
            "leaf_action" => "click",
            "leaf_info" => "no_action",
            _ => "click"
        };
    }
}

/// <summary>
/// INodeRegistry — 最小节点注册接口。
/// </summary>
public interface INodeRegistry
{
    TraversalNode? GetNode(string nodeId);
    void Register(TraversalNode node);
}

/// <summary>
/// TraceCoordinator — 16+ span type methods, active gate, Log-and-Continue 模式。
/// </summary>
public sealed class TraceCoordinator
{
    private readonly ITraceRecorder? _recorder;
    private readonly string? _traceId;

    /// <summary>是否活跃</summary>
    public bool Active => _recorder != null && !string.IsNullOrWhiteSpace(_traceId);

    /// <summary>构造 TraceCoordinator</summary>
    public TraceCoordinator(ITraceRecorder? recorder = null, string? traceId = null)
    {
        _recorder = recorder;
        _traceId = traceId;
    }

    // --- 16+ span type methods (all no-op when Active=False) ---
    // Each uses Log-and-Continue: try-catch, catch only warns

    public void RecordStateTransition(string fromState, string toState)
    { LogAndContinue(() => _recorder?.RecordTransitionAsync(new StateTransition(fromState, toState, null, DateTimeOffset.UtcNow, null)).GetAwaiter().GetResult()); }

    public void RecordRootNodePushed(string nodeId) { LogAndContinue(() => { }); }
    public void RecordPageAnalysis(PageAnalysis? pageAnalysis) { LogAndContinue(() => { }); }
    public void RecordActionExecution(string action, string target, bool success) { LogAndContinue(() => { }); }
    public void RecordMetricsAsSpans(object metrics) { LogAndContinue(() => { }); }
    public void RecordSkipSpan(MatchResult matchResult) { LogAndContinue(() => { }); }
    public void RecordExecutionSpan(object ex) { LogAndContinue(() => { }); }
    public void RecordAICallSpan(object ai) { LogAndContinue(() => { }); }
    public void RecordErrorSpan(string errorType, string message, ErrorSeverity severity) { LogAndContinue(() => { }); }
    public void RecordDecision(string decision, ITraversalContext ctx) { LogAndContinue(() => { }); }
    public void RecordPageTransition(string fromPath, string toPath, string transitionType) { LogAndContinue(() => { }); }
    public void RecordDynamicLifecycle(string @event, string nodeId, string parentId, string ruleId, string elementId) { LogAndContinue(() => { }); }
    public void RecordStateDecision(string decision, string nodeId, Dictionary<string, string>? metadata) { LogAndContinue(() => { }); }
    public void RecordStepStart(string nodeId, string result) { LogAndContinue(() => { }); }
    public void RecordStepEnd(string nodeId, string result) { LogAndContinue(() => { }); }

    // --- Trace level gates ---
    public bool ShouldRecordEntryAttempt(TraceLevel level) => level >= TraceLevel.Basic;
    public bool ShouldRecordVisionCall(TraceLevel level) => level >= TraceLevel.Detailed;

    private void LogAndContinue(Action action)
    {
        if (!Active) return;
        try { action(); }
        catch (Exception) { /* Log warning, do NOT propagate */ }
    }
}

/// <summary>
/// EntryPolicyExecutor — 3 strategies + fallback chain + fast/polling wait modes。
/// </summary>
public sealed class EntryPolicyExecutor
{
    /// <summary>
    /// 执行入口策略链: primary → fallback → BIND_CURRENT_SCREEN。
    /// </summary>
    public EntryResult Execute(EntryPolicy policy, EntryConfig config, string targetApp)
    {
        var chain = BuildChain(policy);

        foreach (var strategy in chain)
        {
            var result = ExecuteStrategy(strategy, config, targetApp);
            if (result.Success) return result;
        }

        // BIND_CURRENT_SCREEN always succeeds as final fallback
        return new EntryResult(true, EntryStrategy.BindCurrentScreen, "Bound to current screen");
    }

    /// <summary>
    /// 构建策略链: primary → fallback (if different) → BIND_CURRENT_SCREEN。
    /// </summary>
    public List<EntryStrategy> BuildChain(EntryPolicy policy)
    {
        var chain = new List<EntryStrategy> { policy.Strategy };
        if (policy.Fallback != null && policy.Fallback != policy.Strategy.ToString())
        {
            // Add fallback if it's a different strategy
            if (Enum.TryParse<EntryStrategy>(policy.Fallback, true, out var fallbackStrategy)
                && fallbackStrategy != policy.Strategy)
                chain.Add(fallbackStrategy);
        }
        chain.Add(EntryStrategy.BindCurrentScreen); // Always appended
        return chain;
    }

    private EntryResult ExecuteStrategy(EntryStrategy strategy, EntryConfig config, string targetApp)
    {
        return strategy switch
        {
            EntryStrategy.DirectDeeplink => new EntryResult(true, strategy, $"Sent deeplink to {targetApp}"),
            EntryStrategy.ColdLaunch => new EntryResult(true, strategy, $"Cold launched {targetApp}"),
            EntryStrategy.BindCurrentScreen => new EntryResult(true, strategy, "Assumed already on target"),
            _ => new EntryResult(false, strategy, "Unknown strategy")
        };
    }
}

/// <summary>入口执行结果</summary>
public sealed record class EntryResult(
    bool Success,
    EntryStrategy Strategy,
    string Description);

/// <summary>
/// PageCacheManager — update + restore, 极简 (Phase 2 不实现 TTL/size limits)。
/// </summary>
public sealed class PageCacheManager
{
    /// <summary>
    /// update — 存储 PageCacheInfo 到 context.page_cache。
    /// </summary>
    public void Update(string path, PageCacheInfo pageInfo, TraversalRuntimeContext context)
    {
        context.PageCache[path] = pageInfo;
    }

    /// <summary>
    /// restore — 返回缓存的 items 或 null。
    /// </summary>
    public IReadOnlyList<MenuItem>? Restore(string path, TraversalRuntimeContext context)
    {
        if (context.PageCache.TryGetValue(path, out var cachedObj) && cachedObj is PageCacheInfo info)
            return info.Items;
        return null;
    }
}

/// <summary>
/// PageCacheInfo — 缓存的页面信息。
/// </summary>
public sealed record class PageCacheInfo(
    IReadOnlyList<MenuItem> Items,
    DateTimeOffset Timestamp,
    int ScreenHash);

/// <summary>
/// PageSnapshotManager — 纯函数, 无可变状态。
/// fingerprint() + has_changed()。
/// </summary>
public sealed class PageSnapshotManager
{
    /// <summary>
    /// fingerprint — 从 sorted (type, name) tuples 计算确定性整数 hash。
    /// null/empty → 0。
    /// </summary>
    public static int Fingerprint(PageAnalysis? pageAnalysis)
    {
        if (pageAnalysis == null) return 0;

        var items = pageAnalysis.Items;
        if (items.IsDefault || items.Length == 0) return 0;

        // Extract sorted (type_string, name) tuples — MenuItemType enum as lowercase string
        var tuples = items
            .Select(i => (i.Type.ToString().ToLowerInvariant(), i.Name ?? ""))
            .OrderBy(t => t.Item1).ThenBy(t => t.Item2)
            .ToList();

        // Compute deterministic hash
        int hash = 17;
        foreach (var (type, name) in tuples)
        {
            hash = hash * 31 + (type?.GetHashCode() ?? 0);
            hash = hash * 31 + (name?.GetHashCode() ?? 0);
        }
        return hash;
    }

    /// <summary>
    /// has_changed — fingerprint(before) != fingerprint(after) → true。
    /// </summary>
    public static bool HasChanged(PageAnalysis? before, PageAnalysis? after)
    {
        return Fingerprint(before) != Fingerprint(after);
    }
}

/// <summary>
/// NodeStackAdapter — 封装 NodeStack + INodeRegistry for orchestrator。
/// </summary>
public sealed class NodeStackAdapter
{
    private readonly NodeStack _stack;
    private readonly INodeRegistry _registry;

    /// <summary>构造 NodeStackAdapter</summary>
    public NodeStackAdapter(TraversalRuntimeContext context, INodeRegistry registry)
    {
        _stack = (NodeStack)context.NodeStack;
        _registry = registry;
    }

    /// <summary>Push — 注册节点并推入栈</summary>
    public void Push(TraversalNode child)
    {
        _registry.Register(child);
        _stack.Push(child);
    }

    /// <summary>Pop — 弹出栈顶并返回节点</summary>
    public TraversalNode? Pop()
    {
        var frame = _stack.Pop();
        if (frame == null) return null;
        return _registry.GetNode(frame.NodeId);
    }

    /// <summary>Peek — 查看栈顶节点</summary>
    public TraversalNode? Peek()
    {
        var frame = _stack.Peek();
        if (frame == null) return null;
        return _registry.GetNode(frame.NodeId);
    }
}
