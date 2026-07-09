using System.Collections.Immutable;
using UniClaw.Core.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace UniClaw.Core.Traversal;

/// <summary>
/// TraversalEngine — 统一遍历引擎入口，实现 IGraphTraversalEngine。
/// 对齐 Python GraphTraversalEngine: plan 驱动初始化 + RunAsync() 核心循环。
/// 构造器调用 Initialize() — fail-fast 模式。
/// sealed class (非 record): 4 个可变内部字段 (D-2, 与 TraversalRuntimeContext 例外一致)。
/// </summary>
public sealed class TraversalEngine : IGraphTraversalEngine
{
    private readonly TraversalPlan _plan;
    private readonly IVisionProvider _vision;
    private readonly IActionExecutor _action;
    private readonly TraversalEngineConfig _config;
    private readonly ITraceRecorder? _traceRecorder;

    // --- 内部组件 (构造器/Initialize 创建) ---
    private TraversalRuntimeContext _ctx = null!;
    private TraversalFSM _fsm = null!;
    private StepContext _stepCtx = null!;
    private StepOrchestrator _orchestrator = null!;
    private DictionaryNodeRegistry _registry = null!;

    // --- IGraphTraversalEngine 属性 ---
    /// <inheritdoc/>
    public TraversalPlan Plan => _plan;
    /// <inheritdoc/>
    public ITraversalContext Context => _ctx;   // 返回只读接口 (P-3)
    /// <inheritdoc/>
    public GlobalState CurrentState => _ctx.GlobalState;

    /// <summary>
    /// 构造 TraversalEngine — fail-fast 模式。构造器调用 Initialize()，
    /// 编译 Plan → 节点树 + 注册表 + FSM + Orchestrator。
    /// </summary>
    public TraversalEngine(
        TraversalPlan plan,
        IVisionProvider vision,
        IActionExecutor action,
        TraversalEngineConfig? config = null,
        ITraceRecorder? traceRecorder = null)
    {
        _plan = plan;
        _vision = vision;
        _action = action;
        _config = config ?? new TraversalEngineConfig();
        _traceRecorder = traceRecorder;

        Initialize();
    }

    // ── Initialize — 编译 Plan → 内部状态 ──────────────────

    /// <summary>
    /// Initialize() — 7 步设置: (1) 创建 Context + GlobalState=Initializing,
    /// (2) CompilePlan() → root + registry, (3) 推入根节点,
    /// (4) 创建 FSM, (5) 组装 StepContext, (6) 创建 Orchestrator,
    /// (7) GlobalState=Traversing。
    /// </summary>
    private void Initialize()
    {
        // 1. Create TraversalRuntimeContext
        _ctx = new TraversalRuntimeContext(
            traceId: $"engine-{Guid.NewGuid():N}"[..12],
            maxDepth: _config.MaxDepth);
        _ctx.GlobalState = GlobalState.Initializing;

        // 2. Compile Plan → root node + registry
        var (rootNode, registry) = CompilePlan();
        _registry = registry;

        // 3. Push root onto NodeStack + set CurrentFrame
        _ctx.NodeStack.Push(rootNode);
        _ctx.CurrentFrame = rootNode;
        if (_plan.CompletionPolicy != null)
            _ctx.SetCompletionPolicy(_plan.CompletionPolicy);

        // 4. Create TraversalFSM
        _fsm = new TraversalFSM(_ctx);

        // 5. Assemble StepContext (13 dependencies)
        _stepCtx = new StepContext(
            Context: _ctx,
            StateMachine: _fsm,
            Vision: _vision,
            Action: _action,
            ChildMgr: new DynamicChildManager(registry),
            NodeRegistry: registry,
            Trace: new TraceCoordinator(_traceRecorder, _ctx.TraceId),
            SnapshotMgr: new PageSnapshotManager(),
            Stack: new NodeStackAdapter(_ctx, registry));

        // 6. Create StepOrchestrator
        _orchestrator = new StepOrchestrator();

        // 7. GlobalState → Traversing (初始化完成)
        _ctx.GlobalState = GlobalState.Traversing;
    }

    // ── CompilePlan — TraversalPlan → 节点树 ──────────────────

    /// <summary>
    /// CompilePlan() — 创建 DictionaryNodeRegistry, 注册 StaticNodes,
    /// 确定 root (优先 plan.RootNode, fallback BuildDefaultRoot),
    /// 确保 root 自身注册。
    /// </summary>
    private (TraversalNode root, DictionaryNodeRegistry registry) CompilePlan()
    {
        var registry = new DictionaryNodeRegistry();

        // Register all StaticNodes
        if (_plan.StaticNodes != null)
        {
            foreach (var (id, node) in _plan.StaticNodes)
                registry.Register(node);
        }

        // Root node: prefer plan.RootNode, fallback to BuildDefaultRoot
        var root = _plan.RootNode ?? BuildDefaultRoot(_plan.EntryApp);

        // Ensure root itself is registered (if StaticNodes doesn't contain root ID)
        if (registry.GetNode(root.NodeId) == null)
            registry.Register(root);

        return (root, registry);
    }

    /// <summary>
    /// BuildDefaultRoot — Plan 无 RootNode 时，构建 minimal Container root。
    /// StaticChildren = StaticNodes.Keys (flat plan 的直接子节点 ID)。
    /// </summary>
    private TraversalNode BuildDefaultRoot(string entryApp)
    {
        var childIds = _plan.StaticNodes?.Keys.ToList() ?? new List<string>();

        return new TraversalNode(
            NodeId: $"{entryApp}_root",
            Name: $"Root of {entryApp}",
            NodeType: NodeType.Container,
            Operation: new Operation(OperationType.NoAction),
            ChildrenStrategy: new ChildrenStrategy(
                ChildrenStrategyType.Static,
                StaticChildren: childIds),
            Precondition: null,
            ErrorPolicy: null,
            ExitCondition: null,
            Meta: null);
    }

    // ── RunAsync — 核心循环 ────────────────────────────────

    /// <summary>
    /// RunAsync() — 核心遍历循环。实现 IGraphTraversalEngine.RunAsync()。
    /// Log-and-Continue: 永不向调用方抛出异常。
    /// 异常捕获 → Done(Reasons.Error)。
    /// </summary>
    public async Task<TraversalResult> RunAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var traceRecords = _config.TraceEnabled ? new List<TraceRecord>() : null;
        var visitedPages = new List<string>();
        var fromState = _fsm.CurrentState;

        try
        {
            for (int i = 0; i < _config.MaxSteps; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Delay per step (simulation delay / production UI stabilization)
                if (_config.DelayPerStepMs > 0)
                    await Task.Delay(_config.DelayPerStepMs, ct);

                // Pre-step: analyze current page via vision provider
                // Required for DynamicChildManager.Generate() to extract items from page
                var pageAnalysis = await _vision.AnalyzeCurrentPageAsync(ct);
                _ctx.SetCurrentPageAnalysis(pageAnalysis);

                var stepResult = _orchestrator.ExecuteStep(_stepCtx);

                // Leaf execution → pop stack (same as SimulationRunner fix)
                if (stepResult.NextState == TraversalState.ResultVerify
                    && _ctx.NodeStack.Depth > 1
                    && _ctx.CurrentFrame?.ChildrenStrategy.Type == ChildrenStrategyType.None)
                    _ctx.NodeStack.Pop();

                // Sync CurrentFrame from stack top
                _ctx.CurrentFrame = _ctx.NodeStack.Peek()?.Node;

                // BRANCH interception pushed child → force NodeSelect
                if (stepResult.ChildPushed
                    && _fsm.CanTransitionTo(TraversalState.NodeSelect))
                    _fsm.TransitionTo(TraversalState.NodeSelect);

                // Record trace (TraceRecord per step)
                if (_config.TraceEnabled && traceRecords != null)
                {
                    traceRecords.Add(new TraceRecord(
                        StepNumber: i + 1,
                        FromState: fromState,
                        ToState: stepResult.NextState,
                        CurrentNodeId: _ctx.CurrentFrame?.NodeId,
                        CurrentPageId: GetCurrentPageId(),
                        ActionExecuted: GetLastAction(),
                        ActionSuccess: GetLastActionSuccess(),
                        ChildPushed: stepResult.ChildPushed,
                        FrameCompleted: stepResult.FrameCompleted));
                }

                // Record page visit
                RecordPageVisit(visitedPages);

                // Termination: frame completed at root level
                if (stepResult.FrameCompleted && _ctx.NodeStack.Depth <= 1)
                    return Done(TraversalResult.Reasons.AllVisited, i + 1,
                        stopwatch, traceRecords, visitedPages);

                // Termination: anti-loop triggered
                if (stepResult.AntiLoopTriggered)
                    return Done(TraversalResult.Reasons.AntiLoop, i + 1,
                        stopwatch, traceRecords, visitedPages);

                // ── CompletionPolicy checks (user intent termination) ──
                var policy = _ctx.CompletionPolicy;
                if (policy != null && policy.Type != CompletionPolicyType.None)
                {
                    // TARGET_FOUND: current node's Operation.Target.Value matches policy target
                    if (policy.Type == CompletionPolicyType.TargetFound)
                    {
                        var currentNode = _ctx.CurrentFrame;
                        if (currentNode != null)
                        {
                            // Match field: Operation.Target.Value (element text, e.g. "Dark mode")
                            // Only TraversalNode has Operation; other ITraversalNode impls use Name fallback
                            // Fallback: Name (for static/root nodes with NoAction where Target.Value is null)
                            var targetValue = (currentNode is TraversalNode tNode)
                                ? tNode.Operation?.Target?.Value?.ToString()
                                : null;
                            var matchValue = !string.IsNullOrEmpty(targetValue)
                                ? targetValue
                                : currentNode.Name;

                            bool matched = policy.MatchMode == MatchMode.Exact
                                ? string.Equals(matchValue, policy.TargetName, StringComparison.OrdinalIgnoreCase)
                                : matchValue.Contains(policy.TargetName!, StringComparison.OrdinalIgnoreCase);

                            if (matched)
                                return Done(TraversalResult.Reasons.TargetFound, i + 1,
                                    stopwatch, traceRecords, visitedPages);
                        }
                    }

                    // TIMEOUT: elapsed > policy.TimeoutSeconds
                    if (policy.Type == CompletionPolicyType.Timeout
                        && stopwatch.Elapsed.TotalSeconds > policy.TimeoutSeconds!)
                    {
                        return Done(TraversalResult.Reasons.Timeout, i + 1,
                            stopwatch, traceRecords, visitedPages);
                    }

                    // MAX_STEPS (policy soft limit): user-specified step limit
                    if (policy.Type == CompletionPolicyType.MaxSteps
                        && i + 1 >= policy.MaxSteps!)
                    {
                        return Done(TraversalResult.Reasons.MaxSteps, i + 1,
                            stopwatch, traceRecords, visitedPages);
                    }
                }

                fromState = _fsm.CurrentState;
            }

            // MaxSteps exhausted
            return Done(TraversalResult.Reasons.MaxSteps, _config.MaxSteps,
                stopwatch, traceRecords, visitedPages);
        }
        catch (OperationCanceledException)
        {
            // CancellationToken — user-initiated stop
            return Done(TraversalResult.Reasons.Cancelled, _ctx.StepCount,
                stopwatch, traceRecords, visitedPages);
        }
        catch (Exception ex) when (!_config.ThrowOnError)
        {
            // Log-and-Continue: catch all, return Error result
            _ctx.GlobalState = GlobalState.Error;
            return Done(TraversalResult.Reasons.Error, _ctx.StepCount,
                stopwatch, traceRecords, visitedPages, ex);
        }
        finally
        {
            // Trace session end (Log-and-Continue: swallow EndSessionAsync exceptions)
            try
            {
                if (_traceRecorder != null)
                    await _traceRecorder.EndSessionAsync();
            }
            catch { /* swallow — 不影响结果返回 */ }
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Run() — 同步便利包装，仿真测试用。
    /// ⚠️ GetAwaiter().GetResult() 在 ASP.NET / WinForms / WPF SynchronizationContext 线程可能死锁。
    /// 仅用于 CLI / 测试环境 (无 SynchronizationContext 的线程)。
    /// </summary>
    public TraversalResult Run()
        => RunAsync().GetAwaiter().GetResult();

    // ── IGraphTraversalEngine lifecycle stubs (Phase 3 完整实现) ──

    /// <inheritdoc/>
    /// <remarks>构造器已初始化 — 此方法为 contract validation no-op</remarks>
    public Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>Phase 3 stub — 应检查 GlobalState==Traversing 才允许 pause</remarks>
    public Task PauseAsync(CancellationToken ct = default)
    {
        _ctx.GlobalState = GlobalState.Paused;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>Phase 3 stub — 应检查 GlobalState==Paused 才允许 resume</remarks>
    public Task ResumeAsync(CancellationToken ct = default)
    {
        _ctx.GlobalState = GlobalState.Traversing;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct = default)
    {
        _ctx.GlobalState = GlobalState.Terminated;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<GlobalState> GetStateAsync(CancellationToken ct = default)
        => Task.FromResult(_ctx.GlobalState);

    // ── Done helper ────────────────────────────────────────

    /// <summary>
    /// Done() — 映射 CompletionReason→GlobalState, 构建完整 TraversalResult。
    /// </summary>
    private TraversalResult Done(string reason, int steps, Stopwatch sw,
        List<TraceRecord>? trace, List<string> pages, Exception? error = null)
    {
        // GlobalState mapping
        _ctx.GlobalState = reason is TraversalResult.Reasons.AllVisited
                             or TraversalResult.Reasons.AntiLoop
                             or TraversalResult.Reasons.TargetFound
            ? GlobalState.Completed
            : reason is TraversalResult.Reasons.Cancelled
                or TraversalResult.Reasons.Timeout
                ? GlobalState.Terminated
                : GlobalState.Error;

        return new TraversalResult(
            Success: reason is TraversalResult.Reasons.AllVisited
                         or TraversalResult.Reasons.AntiLoop
                         or TraversalResult.Reasons.TargetFound,
            CompletionReason: reason,
            TotalSteps: steps,
            ElapsedSeconds: sw.Elapsed.TotalSeconds,
            ActionHistory: _action.GetHistory().ToImmutableArray(),
            VisitedPages: pages.ToImmutableArray(),
            Trace: trace?.ToImmutableArray() ?? ImmutableArray<TraceRecord>.Empty,
            TraceId: _ctx.TraceId,
            FinalState: _fsm.CurrentState,
            Error: error);
    }

    // ── Helper methods ──────────────────────────────────────

    private string? GetCurrentPageId()
    {
        // PageAnalysis doesn't have a PageId field.
        // Track visited pages by node IDs — what the engine actually visits.
        // In simulation mode, mock-specific page IDs are not accessible through IVisionProvider.
        return _ctx.CurrentFrame?.NodeId;
    }

    private string? GetLastAction()
        => _ctx.ActionHistoryInternal.LastOrDefault()?.Action;

    private bool GetLastActionSuccess()
        => _ctx.ActionHistoryInternal.LastOrDefault()?.Success ?? false;

    private void RecordPageVisit(List<string> pages)
    {
        var currentPage = GetCurrentPageId();
        if (currentPage != null && !pages.Contains(currentPage))
            pages.Add(currentPage);
    }
}

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

            // Step 6: Dedup via _generated_pairs (childName without parent — preserves same-page dedup)
            var childName = $"{rule.ChildTemplate}_{result.MatchedItem.Text ?? "item"}";
            var pair = (fingerprint.ToString(), childName);
            if (_generatedPairs.Contains(pair))
                continue; // Skip — already generated

            var itemText = result.MatchedItem.Text ?? "";
            var parentPath = context.CurrentPath.ToList();

            // Step 5b: Container templates inherit parent's DynamicMatch for nested traversal
            // menu_container nodes need DynamicMatch ChildrenStrategy to explore sub-pages.
            // Leaf templates (switch_leaf, slider_leaf, leaf_action, leaf_info) remain None.
            TraversalNode child;
            if (rule.ChildTemplate == "menu_container")
            {
                var nodeId = $"dyn_{rule.ChildTemplate}_{itemText}_{node.NodeId}";
                child = new TraversalNode(
                    NodeId: nodeId,
                    Name: rule.ChildTemplate,
                    NodeType: NodeType.Container,
                    Operation: new Operation(OperationType.Click,
                        new Target(TargetType.Text, itemText)),
                    ChildrenStrategy: new ChildrenStrategy(
                        ChildrenStrategyType.DynamicMatch,
                        DynamicRules: node.ChildrenStrategy.DynamicRules),
                    Precondition: new Precondition(
                        Path: parentPath.Concat(new[] { rule.ChildTemplate }).ToList()),
                    ErrorPolicy: new ErrorPolicy(ErrorPolicyType.Retry, MaxRetries: 1),
                    ExitCondition: node.ExitCondition);
            }
            else
            {
                // Leaf nodes: use TemplateInstantiator as before
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
                    ["item_text"] = itemText,
                    ["item_index"] = result.MatchedItem.Index.ToString(),
                    ["parent_node_id"] = node.NodeId,
                };

                child = _instantiator.Instantiate(template, instantiatorContext, parentPath);
            }

            // Step 7: Set precondition path
            // TemplateInstantiator V6.9 path concatenation (handled in direct creation above)

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
        catch (Exception ex) { Console.WriteLine($"[TraceCoordinator Warning] {ex.GetType().Name}: {ex.Message}"); }
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

        // Compute deterministic hash (H-10: character-based, no string.GetHashCode)
        int hash = 17;
        foreach (var (type, name) in tuples)
        {
            foreach (var ch in type ?? "")
                hash = hash * 31 + (int)ch;
            foreach (var ch in name ?? "")
                hash = hash * 31 + (int)ch;
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
