using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TraceLevel = UniClaw.Core.Graph.Models.TraceLevel;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Abstractions;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.UniBrain;
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
    private readonly IUniBrain _brain;
    private readonly IScreenStateProvider _screenState;
    private readonly IActionExecutor _action;
    private readonly TraversalEngineConfig _config;
    private readonly ITraceRecorder? _traceRecorder;
    private readonly ILogger<TraversalEngine> _logger;
    // trace-correlated logging (D-4): composition-root loggers threaded through —
    // the FSM is constructed in Initialize(), so the logger must arrive via the
    // engine constructor; ErrorHandler is wired into StepContext for the FSM's
    // ErrorHandling state. Both optional — null → NullLogger/default fallback.
    private readonly ILogger<TraversalFSM>? _fsmLogger;
    private readonly ErrorHandler? _errorHandler;

    /// <summary>
    /// trace-parent-linkage M2 (3.3): 引擎侧 span（engine.run/engine.step）的 TraceLevel 来源 —
    /// TraversalPlan.EntryConfig.TraceLevel。未配置（EntryConfig 为 null，现状全部路径与快照 fixture）
    /// → 缺省 Detailed = 全量记录（向后兼容，S1–S6 逐字节不变）。span 目前无 profile，
    /// level 仅作为通道建立；挂上 SpanFieldProfile 后即按级过滤。
    /// </summary>
    private TraceLevel SpanTraceLevel =>
        _plan.EntryConfig?.TraceLevel ?? TraceLevel.Detailed;

    // --- 内部组件 (构造器/Initialize 创建) ---
    private TraversalRuntimeContext _ctx = null!;
    private TraversalFSM _fsm = null!;
    private StepContext _stepCtx = null!;
    private StepOrchestrator _orchestrator = null!;
    private DictionaryNodeRegistry _registry = null!;

    // --- Pause/resume gate (P4-B2) ---
    private volatile TaskCompletionSource _resumeSignal = CreateCompletedTCS();
    private CancellationTokenRegistration _pauseCtRegistration;
    // --- Hook dispatch (D-A: ImmutableArray from config, not mutable List) ---
    private readonly ImmutableArray<ITraversalHook> _hooks;

    // --- IGraphTraversalEngine 属性 ---
    /// <inheritdoc/>
    public TraversalPlan Plan => _plan;
    /// <inheritdoc/>
    public ITraversalContext Context => _ctx;   // 返回只读接口 (P-3)
    /// <inheritdoc/>
    public GlobalState CurrentState => _ctx.GlobalState;
    /// <inheritdoc/>
    public IActionExecutor ActionExecutor => _action;
    /// <inheritdoc/>
    public IUniBrain Brain => _brain;

    /// <summary>
    /// 构造 TraversalEngine — fail-fast 模式。构造器调用 Initialize()，
    /// 编译 Plan → 节点树 + 注册表 + FSM + Orchestrator。
    /// </summary>
    public TraversalEngine(
        TraversalPlan plan,
        IUniBrain brain,
        IScreenStateProvider screenState,
        IActionExecutor action,
        TraversalEngineConfig? config = null,
        ITraceRecorder? traceRecorder = null,
        ILogger<TraversalEngine>? logger = null,
        ILogger<TraversalFSM>? fsmLogger = null,
        ErrorHandler? errorHandler = null)
    {
        _plan = plan;
        _brain = brain;
        _screenState = screenState;
        _action = action;
        _config = config ?? new TraversalEngineConfig();
        _hooks = _config.Hooks;  // D-A: immutable, assigned once at construction
        _traceRecorder = traceRecorder;
        _logger = logger ?? NullLogger<TraversalEngine>.Instance;
        _fsmLogger = fsmLogger;
        _errorHandler = errorHandler;

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
        // effective_depth = min(config.MaxDepth, plan.IntentSlots.Depth ?? int.MaxValue) (緊者勝)
        // Computed BEFORE TraversalRuntimeContext so NodeStack respects the plan's depth constraint.
        var effectiveMaxDepth = _plan.IntentSlots?.Depth.HasValue == true
            ? Math.Min(_config.MaxDepth, _plan.IntentSlots.Depth.Value)
            : _config.MaxDepth;

        // 1. Create TraversalRuntimeContext
        _ctx = new TraversalRuntimeContext(
            traceId: $"engine-{Guid.NewGuid():N}"[..12],
            maxDepth: effectiveMaxDepth);

        // 1b. Register GlobalFSM trace callbacks BEFORE first transition —
        //     所有 GlobalFSM 转换 (含 Initializing/Traversing) 写入 ITraceRecorder
        RegisterGlobalFsmTraceCallbacks();

        _ctx.SetGlobalState(GlobalState.Initializing, "engine_init");

        // 2. Compile Plan → root node + registry
        var (rootNode, registry) = CompilePlan();
        _registry = registry;

        // 3. Push root onto NodeStack + set CurrentFrame
        _ctx.NodeStack.Push(rootNode);
        _ctx.SetCurrentFrame(rootNode);
        if (_plan.CompletionPolicy != null)
            _ctx.SetCompletionPolicy(_plan.CompletionPolicy);

        // 4. Create TraversalFSM (logger threaded from the composition root)
        _fsm = new TraversalFSM(_ctx, _fsmLogger);

        // 5. Assemble StepContext (13 dependencies — interface-typed locals for D-V)
        // D-134 P2 wiring fix: trace is constructed BEFORE childMgr so DynamicChildManager receives
        // the live ITraceCoordinator — the `:859` RecordDynamicLifecycleAsync call now fires (was dead).
        ITraceCoordinator trace = new TraceCoordinator(_traceRecorder, _ctx.TraceId, ctx: _ctx);
        IDynamicChildManager childMgr = new DynamicChildManager(registry, trace);
        IPageSnapshotManager snapshotMgr = new PageSnapshotManager();
        INodeStackAdapter stack = new NodeStackAdapter(_ctx, registry);
        var containerHandler = new ContainerHandler();
        _stepCtx = new StepContext(
            Context: _ctx,
            StateMachine: _fsm,
            Brain: _brain,
            ScreenState: _screenState,
            Action: _action,
            ChildMgr: childMgr,
            NodeRegistry: registry,
            Trace: trace,
            SnapshotMgr: snapshotMgr,
            Stack: stack,
            ErrorHandler: _errorHandler,
            ContainerHandler: containerHandler,
            EffectiveMaxDepth: effectiveMaxDepth,
            ScrollSwipe: _config.ScrollSwipe);

        // 6. Create StepOrchestrator
        _orchestrator = new StepOrchestrator();

        // 7. GlobalState → Traversing (初始化完成)
        _ctx.SetGlobalState(GlobalState.Traversing, "init_complete");
    }

    /// <summary>
    /// RegisterGlobalFsmTraceCallbacks — 在关键状态 (Completed, Error, Traversing, Idle)
    /// 上注册 GlobalFSM 回调，转换经 ITraceRecorder 写入 StateTransition (FsmType="GlobalFSM")。
    /// ForceState 恢复路径不触发回调，故不产生 trace 记录 (spec: ForceState does not produce trace records)。
    /// </summary>
    private void RegisterGlobalFsmTraceCallbacks()
    {
        if (_traceRecorder == null)
            return;

        var fsm = _ctx.Session.InternalGlobalFSM;
        // Register all 8 states. Completed/Terminated have no outgoing transitions but
        // need registration for incoming transition tracing (e.g., Traversing→Completed).
        foreach (var state in Enum.GetValues<GlobalState>())
        {

            fsm.RegisterStateCallback(state, args =>
            {
                // Fire-and-forget: callback 是同步 Action, 记录失败由 GlobalFSM catch 吞掉
                _ = _traceRecorder.RecordTransitionAsync(new StateTransition(
                    FromState: args.FromState.ToString(),
                    ToState: args.ToState.ToString(),
                    Context: new TraceContext(
                        NodeId: _ctx.CurrentFrame?.NodeId,
                        StepSpanId: null,               // 事件在步骤循环间发生
                        StepNumber: _ctx.StepCount,
                        TraceId: _ctx.TraceId),
                    FsmType: "GlobalFSM",
                    Timestamp: args.Timestamp,
                    Reason: args.Reason));
            });
        }
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
        string? lastPageId = null;

        // D-134 P2: engine.run root span — one per run, parent null (M4 5.1).
        // Scope-based: each terminal branch closes it via runScope.End at the original
        // EndSpan position (same statuses); dispose auto-end "ok" never fires because
        // every return path ends explicitly.
        await using var runScope = await _traceRecorder.BeginSpanAsync(
            SpanTypes.EngineRun, level: SpanTraceLevel);

        // OnBeforeRun — fires before step loop, outside try block (D-D)
        await FireAsync(h => h.OnBeforeRunAsync(_plan, _ctx));

        try
        {
            for (int i = 0; i < _config.MaxSteps; i++)
            {
                ct.ThrowIfCancellationRequested();

                // D-134 P2: engine.step span per iteration — parent = engine.run (M4 5.2).
                // Scope replaces the manual StartSpan/EndSpan pair. Deliberately NOT await-using:
                // the scope ends explicitly at the original close sites (7 × "ok"), and an exception
                // mid-iteration leaves the span open exactly as before (no dispose auto-end).
                var stepScope = await _traceRecorder.BeginSpanAsync(
                    SpanTypes.EngineStep, parentSpanId: runScope.SpanId, level: SpanTraceLevel);
                if (_stepCtx.Trace is TraceCoordinator tc)
                    tc.TrackEngineStepSpan(stepScope.SpanId);
                // trace-correlated-logging D-2: Push 必须在 BeginSpanAsync 返回后由调用方执行
                // （async 方法内的 AsyncLocal 写入对调用方 ExecutionContext 不可见——.NET async
                // boundary copy-on-write 语义）。引擎显式 Push 是 span 上下文通道的唯一入口点；
                // DisposeAsync Pop（EndEngineStepSpan 内）在引擎流内执行故可见。
                // 非引擎 span（SourceGen 生成代码）其 spanId 不可见——已知限制（D-222/D-223）。
                EngineStepSpanContext.Instance.Push(stepScope.SpanId);

                // Delay per step (simulation delay / production UI stabilization)
                if (_config.DelayPerStepMs > 0)
                    await Task.Delay(_config.DelayPerStepMs, ct);

                // Pause check (P4-B2): block when Paused (TCS uncompleted),
                // return immediately when Traversing (TCS completed).
                // Placed before expensive vision analysis to avoid wasted work while suspended.
                await _resumeSignal.Task;
                ct.ThrowIfCancellationRequested();

                // Step numbering: increment BEFORE OnBeforeStep so hooks see 1,2,3…
                // (previously never called → StepCount stayed 0 → trace StepNumber 0 and
                // RunAssetHook's sequential-step assertion would throw)
                _ctx.IncrementStepCount();
                _logger.LogDebug("Step {StepNumber} start span={SpanId}", _ctx.StepCount, stepScope.SpanId);

                // OnBeforeStep — fires after pause-gate, before vision analysis
                await FireAsync(h => h.OnBeforeStepAsync(_ctx));

                // Pre-step: analyze current page via UniBrain
                // Required for DynamicChildManager.Generate() to extract items from page
                var pageAnalysis = await _brain.PageAnalyzer.AnalyzeCurrentPageAsync(ct);
                _ctx.SetCurrentPageAnalysis(pageAnalysis);

                // Capture the frame and action-history boundary for completion
                // policies. Execute steps may pop a leaf and resync CurrentFrame
                // to its parent before the policy check runs.
                var stepFrame = _ctx.CurrentFrame;
                var actionsBeforeStep = _action.GetHistory().Count;
                var stepResult = await _orchestrator.ExecuteStepAsync(_stepCtx);

                // OnError (recoverable) — engine-level intercept of FSM ErrorHandling state (D-B)
                // FSM does not access hooks; engine observes state transition externally.
                if (stepResult.NextState == TraversalState.ErrorHandling && _ctx.LastError != null)
                    await FireAsync(h => h.OnErrorAsync(
                        new TraversalErrorContext(_ctx.LastError.GetType().Name, _ctx.LastError.Message,
                            _ctx.CurrentFrame?.NodeId, IsRecoverable: true), _ctx));

                // Leaf execution → pop stack (same as SimulationRunner fix)
                if (stepResult.NextState == TraversalState.ResultVerify
                    && _ctx.NodeStack.Depth > 1
                    && _ctx.CurrentFrame?.ChildrenStrategy.Type == ChildrenStrategyType.None)
                {
                    _ctx.NodeStack.Pop();

                    // DfsBacktrack trace — leaf_execution_complete
                    if (_stepCtx.HandlerTrace != null)
                    {
                        var traceCtx = _stepCtx.Trace.BuildCorrelation();
                        var meta = new Dictionary<string, object> { ["backtrack_reason"] = "leaf_execution_complete" };
                        await _stepCtx.HandlerTrace.RecordHandlerLifecycleAsync(
                            "dfs_backtrack", SpanType.DfsBacktrack, "ok", meta, traceCtx);
                    }
                }

                // Sync CurrentFrame from stack top
                _ctx.SetCurrentFrame(_ctx.NodeStack.Peek()?.Node);

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
                        FrameCompleted: stepResult.FrameCompleted,
                        SpanTypes: _stepCtx.Trace.GetStepSnapshot(),
                        PageFrom: lastPageId,
                        PageTo: lastPageId != GetCurrentPageId() ? GetCurrentPageId() : null,
                        PageTransitionType: lastPageId != null && lastPageId != GetCurrentPageId() ? "navigation" : null));
                }

                // Record page transition when fingerprint changes
                var currentPageId = GetCurrentPageId();
                if (lastPageId != null && lastPageId != currentPageId)
                {
                    await _stepCtx.Trace.RecordPageTransitionAsync(lastPageId, currentPageId, "navigation");
                }

                // Record page visit
                RecordPageVisit(visitedPages);
                lastPageId = currentPageId;

                // OnAfterStep — fires before termination checks (including terminating step)
                await FireAsync(h => h.OnAfterStepAsync(_ctx));

                // Termination: frame completed at root level
                if (stepResult.FrameCompleted && _ctx.NodeStack.Depth <= 1)
                {
                    var result = Done(TraversalResult.Reasons.AllVisited, i + 1,
                        stopwatch, traceRecords, visitedPages);
                    await FireAsync(h => h.OnAfterRunAsync(result));
                    await EndEngineStepSpan(_stepCtx.Trace, stepScope, _logger, _ctx.StepCount);
                    await runScope.End(result.CompletionReason);
                    return result;
                }

                // Termination: anti-loop triggered
                if (stepResult.AntiLoopTriggered)
                {
                    var result = Done(TraversalResult.Reasons.AntiLoop, i + 1,
                        stopwatch, traceRecords, visitedPages);
                    await FireAsync(h => h.OnAfterRunAsync(result));
                    await EndEngineStepSpan(_stepCtx.Trace, stepScope, _logger, _ctx.StepCount);
                    await runScope.End(result.CompletionReason);
                    return result;
                }

                // ── CompletionPolicy checks (user intent termination) ──
                var policy = _ctx.CompletionPolicy;
                if (policy != null && policy.Type != CompletionPolicyType.Exhaustive)
                {
                    // TARGET_FOUND supports both existing semantics:
                    // MarkAndStop completes as soon as the matching node is selected;
                    // ExecuteThenStop requires that matching node's Execute step to
                    // append a successful real action before completion.
                    if (policy.Type == CompletionPolicyType.TargetFound)
                    {
                        var targetValue = (stepFrame as TraversalNode)
                            ?.Operation?.Target?.Value?.ToString();
                        var matchValue = !string.IsNullOrEmpty(targetValue)
                            ? targetValue
                            : stepFrame?.Name;
                        var targetNames = policy.TargetAliases
                            .Insert(0, policy.TargetName!);
                        var matched = matchValue is not null
                            && targetNames.Any(targetName =>
                                policy.MatchMode == MatchMode.Exact
                                    ? string.Equals(
                                        matchValue,
                                        targetName,
                                        StringComparison.OrdinalIgnoreCase)
                                    : matchValue.Contains(
                                        targetName,
                                        StringComparison.OrdinalIgnoreCase));

                        if (matched)
                        {
                            var mayComplete = policy.ActionOnFound
                                == TargetFoundAction.MarkAndStop;
                            if (!mayComplete
                                && fromState == TraversalState.Execute
                                && stepResult.NextState == TraversalState.ResultVerify)
                            {
                                var actions = _action.GetHistory();
                                mayComplete = actions.Count > actionsBeforeStep
                                    && actions[^1].Success;
                            }

                            if (mayComplete)
                            {
                                var result = Done(TraversalResult.Reasons.TargetFound, i + 1,
                                    stopwatch, traceRecords, visitedPages);
                                await FireAsync(h => h.OnAfterRunAsync(result));
                                await EndEngineStepSpan(_stepCtx.Trace, stepScope, _logger, _ctx.StepCount);
                                await runScope.End(result.CompletionReason);
                                return result;
                            }
                        }
                    }

                    // TIMEOUT: elapsed > policy.TimeoutSeconds
                    if (policy.Type == CompletionPolicyType.Timeout
                        && stopwatch.Elapsed.TotalSeconds > policy.TimeoutSeconds!)
                    {
                        var result = Done(TraversalResult.Reasons.Timeout, i + 1,
                            stopwatch, traceRecords, visitedPages);
                        await FireAsync(h => h.OnAfterRunAsync(result));
                        await EndEngineStepSpan(_stepCtx.Trace, stepScope, _logger, _ctx.StepCount);
                        await runScope.End(result.CompletionReason);
                        return result;
                    }

                    // MAX_STEPS (policy soft limit): user-specified step limit
                    if (policy.Type == CompletionPolicyType.MaxSteps
                        && i + 1 >= policy.MaxSteps!)
                    {
                        var result = Done(TraversalResult.Reasons.MaxSteps, i + 1,
                            stopwatch, traceRecords, visitedPages);
                        await FireAsync(h => h.OnAfterRunAsync(result));
                        await EndEngineStepSpan(_stepCtx.Trace, stepScope, _logger, _ctx.StepCount);
                        await runScope.End(result.CompletionReason);
                        return result;
                    }
                }

                // D-134 P2: close the engine.step span for a normally-completed iteration
                await EndEngineStepSpan(_stepCtx.Trace, stepScope, _logger, _ctx.StepCount);

                fromState = _fsm.CurrentState;
            }

            // MaxSteps exhausted
            var exhaustedResult = Done(TraversalResult.Reasons.MaxSteps, _config.MaxSteps,
                stopwatch, traceRecords, visitedPages);
            await FireAsync(h => h.OnAfterRunAsync(exhaustedResult));
            await runScope.End(exhaustedResult.CompletionReason);
            return exhaustedResult;
        }
        catch (OperationCanceledException)
        {
            // CancellationToken — user-initiated stop
            var cancelledResult = Done(TraversalResult.Reasons.Cancelled, _ctx.StepCount,
                stopwatch, traceRecords, visitedPages);
            await FireAsync(h => h.OnAfterRunAsync(cancelledResult));
            await runScope.End(cancelledResult.CompletionReason);
            return cancelledResult;
        }
        catch (Exception ex) when (!_config.ThrowOnError)
        {
            // OnError (fatal) — IsRecoverable=false, engine terminates
            await FireAsync(h => h.OnErrorAsync(
                new TraversalErrorContext(ex.GetType().Name, ex.Message, _ctx.CurrentFrame?.NodeId, IsRecoverable: false), _ctx));

            // Log-and-Continue: catch all, return Error result
            // (GlobalState → Error 由 Done() 统一设置, 避免 Error→Error 重复转换)
            var errorResult = Done(TraversalResult.Reasons.Error, _ctx.StepCount,
                stopwatch, traceRecords, visitedPages, ex);
            await FireAsync(h => h.OnAfterRunAsync(errorResult));
            await runScope.End(errorResult.CompletionReason);
            return errorResult;
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

    // ── D-134 P2 span close helpers (M4: scope-based) ───────

    /// <summary>EndEngineStepSpan — closes the engine.step scope for a completed iteration with
    /// status "ok" (all 7 close sites use "ok", as before). A no-op scope (no recorder) is a no-op;
    /// the tracked CurrentEngineStepSpanId is cleared via the coordinator seam. The AsyncLocal span
    /// context is owned by TraceSpanScope itself (trace-correlated-logging D-1: Push on
    /// construction, Pop on dispose), so no explicit Reset is needed here. The run span is closed
    /// separately by each terminal branch via runScope.End(result.CompletionReason).</summary>
    private static async Task EndEngineStepSpan(ITraceCoordinator trace, TraceSpanScope stepScope, ILogger<TraversalEngine> logger, int stepNumber)
    {
        await stepScope.End("ok");
        if (trace is TraceCoordinator tc)
            tc.UntrackEngineStepSpan(stepScope.SpanId);
        // trace-correlated-logging D-1: Pop restores the parent span on the async stack.
        // DisposeAsync is idempotent (End is already called above, so it's a no-op);
        // the Pop here is the semantic replacement for the old Reset.
        await stepScope.DisposeAsync();
        logger.LogDebug("Step {StepNumber} end", stepNumber);
    }

    // ── IGraphTraversalEngine lifecycle ──

    /// <inheritdoc/>
    /// <remarks>构造器已初始化 — 此方法为 contract validation no-op</remarks>
    public Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>
    /// PauseAsync — 使用 TaskCompletionSource gate 挂起步骤循环。
    /// 前置校验: GlobalState 必须为 Traversing。
    /// 步骤循环在下一个迭代的暂停检查点阻塞。
    /// 如果 CancellationToken 被取消，gate 自动打开（使循环可通过
    /// 后续的 ct.ThrowIfCancellationRequested() 退出）。
    /// </remarks>
    public async Task PauseAsync(CancellationToken ct = default)
    {
        if (_ctx.GlobalState != GlobalState.Traversing)
            throw new DomainValidationException("GlobalState", "Cannot pause when not Traversing");

        _pauseCtRegistration.Dispose();  // dispose previous registration

        var tcs = new TaskCompletionSource();
        _resumeSignal = tcs;  // close gate (volatile write)
        _pauseCtRegistration = ct.Register(() => tcs.TrySetResult());  // link cancellation
        _ctx.SetGlobalState(GlobalState.Paused, "user_pause");
        await FireAsync(h => h.OnPauseAsync(_ctx));  // B1 hook (gate still closed)
    }

    /// <inheritdoc/>
    /// <remarks>
    /// ResumeAsync — 完成 TaskCompletionSource 恢复步骤循环。
    /// 前置校验: GlobalState 必须为 Paused。
    /// 钩子在 gate 打开前触发 — 防止步骤循环与 OnResumeAsync 并发。
    /// </remarks>
    public async Task ResumeAsync(CancellationToken ct = default)
    {
        if (_ctx.GlobalState != GlobalState.Paused)
            throw new DomainValidationException("GlobalState", "Cannot resume when not Paused");

        _ctx.SetGlobalState(GlobalState.Traversing, "user_resume");
        // FireAsync BEFORE TrySetResult — hooks must complete before the step loop resumes
        await FireAsync(h => h.OnResumeAsync(_ctx));
        _resumeSignal.TrySetResult();  // open gate after hooks
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct = default)
    {
        // 矩阵无 Traversing→Terminated 直边 — 两步终止: Traversing→Paused→Terminated
        if (_ctx.GlobalState == GlobalState.Traversing)
            _ctx.SetGlobalState(GlobalState.Paused, "stopping");
        _ctx.SetGlobalState(GlobalState.Terminated, "user_stop");
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
        // GlobalState mapping — reason 作为 GlobalFSM 转换原因 ("all_visited", "error", ...)
        var targetState = reason is TraversalResult.Reasons.AllVisited
                             or TraversalResult.Reasons.AntiLoop
                             or TraversalResult.Reasons.TargetFound
            ? GlobalState.Completed
            : reason is TraversalResult.Reasons.Cancelled
                or TraversalResult.Reasons.Timeout
                ? GlobalState.Terminated
                : GlobalState.Error;

        if (_ctx.GlobalState != targetState)
        {
            // 矩阵无 Traversing→Terminated 直边 — 两步终止: Traversing→Paused→Terminated
            if (targetState == GlobalState.Terminated && _ctx.GlobalState == GlobalState.Traversing)
                _ctx.SetGlobalState(GlobalState.Paused, "stopping");
            _ctx.SetGlobalState(targetState, reason);
        }

        _logger.LogInformation("Engine terminated reason={Reason} steps={Steps}", reason, steps);

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

    // ── Pause/resume gate helpers (P4-B2) ───────────────────

    /// <summary>
    /// CreateCompletedTCS — 创建预完成的 TaskCompletionSource。
    /// 初始状态下 await 立即返回，不阻塞步骤循环。
    /// </summary>
    private static TaskCompletionSource CreateCompletedTCS()
    {
        var tcs = new TaskCompletionSource();
        tcs.SetResult();
        return tcs;
    }

    /// <summary>
    /// FireAsync — 遍历 _hooks 列表执行指定异步操作。
    /// Log-and-Continue: 单个 hook 异常不传播，不影响其他 hook 或主流程。
    /// D-E: Console.WriteLine warning consistent with TraceCoordinator dispatch-table pattern.
    /// </summary>
    private async Task FireAsync(Func<ITraversalHook, Task> selector)
    {
        if (_hooks.Length == 0) return;  // Zero-overhead skip when no hooks

        foreach (var hook in _hooks)
        {
            try
            {
                await selector(hook);
            }
            catch (Exception ex)
            {
                // Log-and-Continue — hook 异常不传播 (D-E)
                Console.WriteLine($"[Hook Warning] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// IDynamicChildManager — 管理 STATIC/DYNAMIC_MATCH 子节点生成 + 缓存 + 跨失效 dedup 持久。
/// </summary>
public interface IDynamicChildManager
{
    TraversalNode? GetNextUnvisitedChild(TraversalNode node, ITraversalContext context);
    void Generate(TraversalNode node, ITraversalContext context);
    void Invalidate(string nodeId);
    /// <summary>
    /// 返回指定节点缓存子节点时的页面指纹, 若未缓存返回 null。
    /// 用于 StepOrchestrator 行为导航检测: 比较缓存指纹与当前指纹判断页面是否变化。
    /// </summary>
    int? GetCachedFingerprint(string nodeId);
    /// <summary>
    /// 返回指定节点缓存的子节点数量, 若未缓存返回 0。
    /// 用于 ContainerHandler CompletionContext 构造。
    /// </summary>
    int GetCachedChildCount(string nodeId);
}

/// <summary>
/// DynamicChildManager — 管理 STATIC/DYNAMIC_MATCH 子节点生成 + 缓存 + 跨失效 dedup 持久。
/// </summary>
public sealed class DynamicChildManager : IDynamicChildManager
{
    private readonly Dictionary<string, (int Fingerprint, List<TraversalNode> Children)> _dynamicChildren = new();
    internal readonly HashSet<(string fingerprint, string name)> _generatedPairs = new();
    private readonly IDynamicMatcher _matcher = new DynamicMatcher();
    private readonly ITemplateInstantiator _instantiator = new TemplateInstantiator();
    private readonly INodeRegistry? _nodeRegistry;
    private readonly ITraceCoordinator? _trace;
    private readonly IPageSnapshotManager _snapshotMgr;

    /// <summary>构造 DynamicChildManager</summary>
    public DynamicChildManager(INodeRegistry? nodeRegistry = null, ITraceCoordinator? trace = null, IPageSnapshotManager? snapshotMgr = null)
    {
        _nodeRegistry = nodeRegistry;
        _trace = trace;
        _snapshotMgr = snapshotMgr ?? new PageSnapshotManager();
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
            // DYNAMIC_MATCH: generate if not cached, then iterate cached.
            // NOTE: Fingerprint-based auto-invalidation REMOVED (D-74).
            // Previously, when the page fingerprint changed (e.g. after navigation),
            // cached children were invalidated and regenerated from the new page,
            // permanently losing sibling navigation branches.
            // Now: scroll invalidates explicitly via TryHandleScroll;
            // navigation is detected behaviorally in StepOrchestrator (fingerprint
            // change after tap → push sub-page frame instead of invalidating parent).

            if (!_dynamicChildren.ContainsKey(node.NodeId))
            {
                Generate(node, context);
            }

            if (_dynamicChildren.TryGetValue(node.NodeId, out var entry))
            {
                // D-74: Before returning a cached child, verify the page hasn't changed.
                // If fingerprint changed (navigation occurred), return null — the cached
                // children belong to a different page.  StepOrchestrator.TryHandleNavigation
                // will detect the mismatch and push a sub-page frame.
                var runtimeCtx = context as TraversalRuntimeContext;
                var currentFingerprint = _snapshotMgr.Fingerprint(runtimeCtx?.CurrentPageAnalysis);
                if (currentFingerprint != 0 && entry.Fingerprint != currentFingerprint)
                {
                    // Page changed — cached children are from a different page.
                    // Return null to let StepOrchestrator handle navigation detection.
                    return null;
                }

                foreach (var child in entry.Children)
                {
                    if (!context.VisitedNodes.Contains(child.NodeId))
                        return child;
                }
            }
            return null; // All dynamic children visited (or page changed)
        }

        return null; // ChildrenStrategyType.None
    }

    /// <summary>
    /// 生成管道 (9 steps + dedup) — DynamicMatch 子节点生成核心流程。
    /// D-134 P2: wraps the pipeline in an entry.generate span; each new child emits entry.observed,
    /// each dedup hit emits entry.ignored.
    /// </summary>
    public void Generate(TraversalNode node, ITraversalContext context)
    {
        // Step 1: Compute page fingerprint
        var runtimeCtx = context as TraversalRuntimeContext;
        var pageAnalysis = runtimeCtx?.CurrentPageAnalysis;
        var fingerprint = _snapshotMgr.Fingerprint(pageAnalysis);

        // D-134 P2: entry.generate — parent = current engine.step (TraceCoordinator passthrough)
        var genSpanId = _trace?.StartSpan(SpanTypes.EntryGenerate, _trace.CurrentEngineStepSpanId,
            new Dictionary<string, object>
            {
                [TraceFields.EntryParentNode] = node.NodeId,
                [TraceFields.EntryFingerprint] = fingerprint,
            });

        var children = new List<TraversalNode>();

        // Step 2: Convert DynamicRules → matcher rules
        var rules = node.ChildrenStrategy.DynamicRules;
        if (rules == null || rules.Count == 0)
        {
            _dynamicChildren[node.NodeId] = (fingerprint, children);
            EndGenerateSpan(_trace, genSpanId, 0, 0);
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
        var ignoredCount = 0;
        foreach (var result in matchResults)
        {
            if (!result.Matched) continue;

            var rule = ruleList.FirstOrDefault(r => r.RuleId == result.MatchRuleId);
            if (rule == null) continue;

            // Step 6: Dedup via _generated_pairs (fingerprint + childName — same-page scope prevents circular nesting)
            var childName = $"{rule.ChildTemplate}_{result.MatchedItem.Text ?? "item"}";
            var pair = (fingerprint.ToString(), childName);
            if (_generatedPairs.Contains(pair))
            {
                // D-134 P2: entry.ignored — dedup hit
                ignoredCount++;
                _trace?.StartSpan(SpanTypes.EntryIgnored, genSpanId,
                    new Dictionary<string, object>
                    {
                        [TraceFields.EntryName] = childName,
                        [TraceFields.EntryReason] = "dedup",
                    });
                continue; // Skip — already generated
            }

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
                    ErrorPolicy: new ErrorPolicy(ErrorPolicyType.Retry, MaxRetries: 1));
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
                _ = _trace.RecordDynamicLifecycleAsync("generate", child.NodeId, node.NodeId, rule.RuleId, "");

            // D-134 P2: entry.observed — new matched item, parent = entry.generate
            _trace?.StartSpan(SpanTypes.EntryObserved, genSpanId,
                new Dictionary<string, object>
                {
                    [TraceFields.EntryName] = itemText,
                    [TraceFields.EntryParent] = node.NodeId,
                    [TraceFields.EntryNodeId] = child.NodeId,
                    [TraceFields.EntryMatchRule] = rule.RuleId,
                    [TraceFields.EntryIndex] = result.MatchedItem.Index,
                });

            // Add dedup pair and child
            _generatedPairs.Add(pair);
            children.Add(child);
        }

        _dynamicChildren[node.NodeId] = (fingerprint, children);
        EndGenerateSpan(_trace, genSpanId, children.Count, ignoredCount);
    }

    /// <summary>
    /// D-134 P2: close the entry.generate span with the match count and ignored (dedup) count.
    /// </summary>
    private static void EndGenerateSpan(ITraceCoordinator? trace, string? genSpanId, int matchCount, int ignoredCount)
    {
        if (trace == null || genSpanId == null) return;
        trace.EndSpan(genSpanId, "ok",
            new Dictionary<string, object>
            {
                [TraceFields.EntryMatchCount] = matchCount,
                [TraceFields.EntryIgnoredCount] = ignoredCount,
            });
    }

    /// <summary>
    /// 返回缓存指纹, 未缓存时返回 null。
    /// </summary>
    public int? GetCachedFingerprint(string nodeId)
    {
        if (_dynamicChildren.TryGetValue(nodeId, out var entry))
            return entry.Fingerprint;
        return null;
    }

    /// <summary>
    /// 返回指定节点缓存的子节点数量, 未缓存返回 0。
    /// </summary>
    public int GetCachedChildCount(string nodeId)
    {
        if (_dynamicChildren.TryGetValue(nodeId, out var entry))
            return entry.Children.Count;
        return 0;
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
        _dynamicChildren[nodeId] = (0, children);
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
/// ITraceCoordinator — 26 members mirroring TraceCoordinator's public API:
/// Active property + 25 methods (16 Record methods + PushSpan/PopSpan/ClearVisitSpan + BuildCorrelation +
/// GetStepSnapshot + ShouldRecordEntryAttempt + ShouldRecordVisionCall + StartSpan/EndSpan).
/// StartSpan/EndSpan (D-134 P2) are synchronous passthroughs to ITraceRecorder.StartSpanAsync/EndSpanAsync
/// for TraceSpan instrumentation (engine.run/step, entry.*).
/// </summary>
public interface ITraceCoordinator
{
    bool Active { get; }
    Task RecordStepStartAsync(string nodeId, string result);
    Task RecordStepEndAsync(string nodeId, string result);
    Task RecordPageAnalysisAsync(PageAnalysis? pageAnalysis);
    Task RecordActionExecutionAsync(string action, string target, bool success);
    Task RecordActionExecutionAsync(Domain.Models.Common.OperationType action, Domain.Models.Common.Target? target, bool success);
    Task RecordMetricsAsSpansAsync(object metrics);
    Task RecordSkipSpanAsync(MatchResult matchResult);
    Task RecordExecutionSpanAsync(object ex);
    Task RecordAICallSpanAsync(string capability, string providerId, bool success, double latencyMs, int? tokens = null, Dictionary<string, object>? metadata = null);
    Task RecordErrorSpanAsync(string errorType, string message, ErrorSeverity severity);
    Task RecordDecisionAsync(string decision, ITraversalContext ctx);
    Task RecordStateTransitionAsync(string fromState, string toState);
    Task RecordRootNodePushedAsync(string nodeId);
    Task RecordPageTransitionAsync(string fromPath, string toPath, string transitionType);
    Task RecordDynamicLifecycleAsync(string @event, string nodeId, string parentId, string ruleId, string elementId);
    Task RecordStateDecisionAsync(string decision, string nodeId, Dictionary<string, string>? metadata);
    string? PushSpan();
    void PopSpan(string? spanId);
    void ClearVisitSpan();
    TraceContext? BuildCorrelation(string? stepSpanIdOverride = null);
    string? StartSpan(string spanType, string? parentSpanId = null, Dictionary<string, object>? attributes = null);
    void EndSpan(string? spanId, string status = "ok", Dictionary<string, object>? attributes = null);

    /// <summary>The TraceSpan id of the innermost open engine.step span, or null when none.</summary>
    string? CurrentEngineStepSpanId { get; }
    ImmutableArray<Observability.SpanType> GetStepSnapshot();
    bool ShouldRecordEntryAttempt(TraceLevel level);
    bool ShouldRecordVisionCall(TraceLevel level);
}

/// <summary>
/// TraceCoordinator — 16+ span type methods, active gate, Log-and-Continue 模式。
/// Refactored with BuildCorrelation() producing TraceContext, SpanId counter,
/// StepSpanId lifecycle, typed RecordActionExecution signature, and StepTraceSnapshot.
/// D-134 P2: StartSpan/EndSpan sync passthroughs power the TraceSpan instrumentation.
/// </summary>
public sealed class TraceCoordinator : ITraceCoordinator
{
    private readonly ITraceRecorder? _recorder;
    private readonly string? _traceId;
    private readonly ITraversalContext? _ctx;
    private int _spanCounter;
    private string? _currentStepSpanId;
    private string? _currentVisitSpanId;
    // D-134: TraceSpan id of the current engine.step span (distinct from _currentStepSpanId,
    // which records the step's first ExecutionRecord SpanId and is cleared by RecordStepEndAsync).
    private string? _currentEngineStepSpanId;
    private readonly Stack<string?> _spanStack = new();
    private readonly HashSet<SpanType> _stepSpanTypes = new();
    private readonly Stopwatch _stepStopwatch = new();

    /// <summary>是否活跃</summary>
    public bool Active => _recorder != null && !string.IsNullOrWhiteSpace(_traceId);

    /// <summary>构造 TraceCoordinator — accepts optional ITraversalContext for BuildCorrelation</summary>
    public TraceCoordinator(ITraceRecorder? recorder = null, string? traceId = null, ITraversalContext? ctx = null)
    {
        _recorder = recorder;
        _traceId = traceId;
        _ctx = ctx;
        _spanCounter = 0;
        _currentStepSpanId = null;
    }

    // ── SpanId generation ─────────────────────────────────

    /// <summary>NextSpanId — incremental counter format "{traceId}-{counter:D6}"</summary>
    private string? NextSpanId()
    {
        if (_traceId == null) return null;
        _spanCounter++;
        return $"{_traceId}-{_spanCounter:D6}";
    }

    // ── BuildCorrelation — produces TraceContext from engine context ──

    /// <summary>
    /// BuildCorrelation — constructs TraceContext from engine context:
    /// NodeId from ctx.CurrentFrame?.NodeId, StepSpanId from _currentStepSpanId,
    /// StepNumber from ctx.StepCount, TraceId from _traceId,
    /// VisitSpanId from _currentVisitSpanId, ParentSpanId from _spanStack.Peek() (or null when stack empty).
    /// When ctx=null, produces TraceContext with TraceId and StepSpanId only (partial correlation).
    /// Optional StepSpanId override for RecordStepStart (StepSpanId = SpanId = step's first record).
    /// </summary>
    public TraceContext? BuildCorrelation(string? stepSpanIdOverride = null)
    {
        var stepSpanId = stepSpanIdOverride ?? _currentStepSpanId;

        if (_ctx == null)
        {
            // Partial correlation when no engine context — TraceId + StepSpanId still available
            if (_traceId == null && stepSpanId == null)
                return null;
            return new TraceContext(
                NodeId: null,
                StepSpanId: stepSpanId,
                StepNumber: null,
                TraceId: _traceId,
                VisitSpanId: _currentVisitSpanId,
                ParentSpanId: _spanStack.Count > 0 ? _spanStack.Peek() : null);
        }

        return new TraceContext(
            NodeId: _ctx.CurrentFrame?.NodeId,
            StepSpanId: stepSpanId,
            StepNumber: _ctx.StepCount,
            TraceId: _traceId,
            VisitSpanId: _currentVisitSpanId,
            ParentSpanId: _spanStack.Count > 0 ? _spanStack.Peek() : null);
    }

    // ── SerializeTarget — typed target serialization ──────

    /// <summary>
    /// SerializeTarget — extracts TargetType and TargetValue from Domain.Common.Target.
    /// Returns (null, null) for null target (Back/NoAction operations).
    /// Coordinate → "{X},{Y}", string → string, int → ToString(), other → ToString().
    /// </summary>
    private static (Domain.Models.Common.TargetType? targetType, string? targetValue) SerializeTarget(Domain.Models.Common.Target? target)
    {
        if (target == null) return (null, null);

        var by = target.By;
        var value = target.Value;
        string? serialized;

        if (value is Domain.Models.Content.Coordinate coord)
            serialized = $"{coord.X},{coord.Y}";
        else if (value is string s)
            serialized = s;
        else if (value is int i)
            serialized = i.ToString();
        else
            serialized = value?.ToString();

        return (by, serialized);
    }

    // ── Span Stack — PushSpan/PopSpan/ClearVisitSpan ─────────

    /// <summary>PushSpan — generates a new SpanId, pushes onto _spanStack, and returns it.
    /// Used for span tree nesting: parent spans push before child work, pop after.</summary>
    public string? PushSpan()
    {
        var spanId = NextSpanId();
        _spanStack.Push(spanId);
        return spanId;
    }

    /// <summary>PopSpan — pops from _spanStack if the top matches spanId.
    /// Mismatch guard prevents stack corruption when spans are popped out of order.</summary>
    public void PopSpan(string? spanId)
    {
        if (_spanStack.Count > 0 && EqualityComparer<string?>.Default.Equals(_spanStack.Peek(), spanId))
            _spanStack.Pop();
    }

    /// <summary>ClearVisitSpan — nulls _currentVisitSpanId.
    /// Called on node exit so subsequent BuildCorrelation() produces VisitSpanId=null.</summary>
    public void ClearVisitSpan()
    {
        _currentVisitSpanId = null;
    }

    // ── TraceSpan passthroughs (D-134 P2) ──────────────────

    /// <summary>StartSpan — synchronous passthrough to ITraceRecorder.StartSpanAsync.
    /// When the spanType is engine.step, tracks the returned spanId as the current engine.step
    /// TraceSpan (source for entry.generate/entry.visited parent attribution). Returns null when
    /// no recorder is attached (span tree disabled).</summary>
    public string? StartSpan(string spanType, string? parentSpanId = null, Dictionary<string, object>? attributes = null)
    {
        if (_recorder == null) return null;
        var spanId = _recorder.StartSpanAsync(spanType, spanType, parentSpanId, attributes).GetAwaiter().GetResult();
        if (spanType == Observability.SpanTypes.EngineStep)
            TrackEngineStepSpan(spanId);
        return spanId;
    }

    /// <summary>EndSpan — synchronous passthrough to ITraceRecorder.EndSpanAsync.
    /// Clears the current engine.step TraceSpan id when closing the engine.step span.</summary>
    public void EndSpan(string? spanId, string status = "ok", Dictionary<string, object>? attributes = null)
    {
        if (_recorder == null || spanId == null) return;
        _recorder.EndSpanAsync(spanId, status, attributes).GetAwaiter().GetResult();
        UntrackEngineStepSpan(spanId);
    }

    /// <summary>CurrentEngineStepSpanId — the TraceSpan id of the innermost open engine.step span.
    /// Used by entry.generate/entry.visited parent attribution. Null when no engine.step span is open
    /// or no recorder is attached.</summary>
    public string? CurrentEngineStepSpanId => _currentEngineStepSpanId;

    // ── Scope seams (trace-span-helpers M4) ─────────────────
    // Internal-only: the scope-based engine.step migration (5.2) feeds the tracked engine.step id
    // from the scope's SpanId, and the entry.visited event migration (5.5) records through the
    // underlying recorder. The public ITraceCoordinator surface stays guard-frozen (27 members).

    /// <summary>TrackEngineStepSpan — records the id of the innermost open engine.step span
    /// (same value the StartSpan passthrough would store; here sourced from the scope's SpanId).</summary>
    internal void TrackEngineStepSpan(string? spanId) => _currentEngineStepSpanId = spanId;

    /// <summary>UntrackEngineStepSpan — clears the tracked engine.step id when its span closes
    /// (no-op unless the given id is the one currently tracked).</summary>
    internal void UntrackEngineStepSpan(string? spanId)
    {
        if (_currentEngineStepSpanId == spanId)
            _currentEngineStepSpanId = null;
    }

    /// <summary>Recorder — the underlying ITraceRecorder (or null when the span tree is disabled).</summary>
    internal ITraceRecorder? Recorder => _recorder;

    // ── 16+ span type methods (all no-op when Active=False) ──

    /// <summary>RecordStepStartAsync — generate SpanId, assign _currentStepSpanId, create ExecutionRecord with Context + StepSpanId override</summary>
    public async Task RecordStepStartAsync(string nodeId, string result)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            _currentStepSpanId = spanId;
            _stepSpanTypes.Clear();
            _stepSpanTypes.Add(Observability.SpanType.StateDecision);
            _stepStopwatch.Restart();
            var context = BuildCorrelation(stepSpanIdOverride: spanId);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: "step_start",
                    Status: result,
                    SpanType: Observability.SpanType.StateDecision,
                    Context: context,
                    SpanId: spanId,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordStepEndAsync — create ExecutionRecord with Context, DurationMs; release _currentStepSpanId</summary>
    public async Task RecordStepEndAsync(string nodeId, string result)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            var context = BuildCorrelation();
            _stepStopwatch.Stop();
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: "step_end",
                    Status: result,
                    SpanType: Observability.SpanType.StateDecision,
                    Context: context,
                    SpanId: spanId,
                    DurationMs: _stepStopwatch.Elapsed.TotalMilliseconds,
                    Timestamp: DateTimeOffset.UtcNow));
            _currentStepSpanId = null;
        });
    }

    /// <summary>RecordPageAnalysisAsync — create ExecutionRecord with Context, SpanId, SpanType=PageAnalysis, Depth</summary>
    public async Task RecordPageAnalysisAsync(PageAnalysis? pageAnalysis)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.PageAnalysis);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: "page_analysis",
                    Status: "ok",
                    SpanType: Observability.SpanType.PageAnalysis,
                    Context: context,
                    SpanId: spanId,
                    Depth: _ctx?.NodeStack.Depth,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordActionExecutionAsync — typed (OperationType, Target?, bool) signature + SerializeTarget</summary>
    public async Task RecordActionExecutionAsync(string action, string target, bool success)
    {
        // Legacy overload — untyped string action/target
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            var context = BuildCorrelation();
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: action,
                    Status: success ? "success" : "fail",
                    SpanType: Observability.SpanType.StateDecision,
                    Context: context,
                    SpanId: spanId,
                    PageId: _ctx?.CurrentFrame?.NodeId,
                    TargetType: Domain.Models.Common.TargetType.Text,
                    TargetValue: target,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordActionExecutionAsync — typed (OperationType, Target?, bool) signature</summary>
    public async Task RecordActionExecutionAsync(Domain.Models.Common.OperationType action, Domain.Models.Common.Target? target, bool success)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            var context = BuildCorrelation();
            var (targetType, targetValue) = SerializeTarget(target);
            _stepSpanTypes.Add(Observability.SpanType.StateDecision);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: action.ToString().ToLowerInvariant(),
                    Status: success ? "success" : "fail",
                    SpanType: Observability.SpanType.StateDecision,
                    Context: context,
                    SpanId: spanId,
                    PageId: _ctx?.CurrentFrame?.NodeId,
                    TargetType: targetType,
                    TargetValue: targetValue,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    public async Task RecordMetricsAsSpansAsync(object metrics) { await LogAndContinueAsync(() => Task.CompletedTask); }

    /// <summary>RecordSkipSpanAsync → DfsForward — create ExecutionRecord with Context, ChildNodeId from matchResult.
    /// Sets _currentVisitSpanId to track the current node visit span.</summary>
    public async Task RecordSkipSpanAsync(MatchResult matchResult)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            _currentVisitSpanId = spanId;
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.DfsForward);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: "dfs_forward",
                    Status: "ok",
                    SpanType: Observability.SpanType.DfsForward,
                    Context: context,
                    SpanId: spanId,
                    ChildNodeId: matchResult.MatchedItem?.Text,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    public async Task RecordExecutionSpanAsync(object ex) { await LogAndContinueAsync(() => Task.CompletedTask); }

    /// <summary>RecordAICallSpanAsync typed — create AICallRecord with Context = BuildCorrelation()</summary>
    public async Task RecordAICallSpanAsync(string capability, string providerId, bool success, double latencyMs, int? tokens = null, Dictionary<string, object>? metadata = null)
    {
        await LogAndContinueAsync(async () =>
        {
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.AICall);
            if (_recorder != null)
                await _recorder.RecordAICallAsync(new AICallRecord(
                    Capability: capability,
                    ProviderId: providerId,
                    Success: success,
                    LatencyMs: latencyMs,
                    Context: context,
                    Tokens: tokens,
                    Timestamp: DateTimeOffset.UtcNow,
                    Metadata: metadata));
        });
    }

    /// <summary>RecordErrorSpanAsync — create ErrorRecord with Context = BuildCorrelation()</summary>
    public async Task RecordErrorSpanAsync(string errorType, string message, ErrorSeverity severity)
    {
        await LogAndContinueAsync(async () =>
        {
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.ErrorHandling);
            if (_recorder != null)
                await _recorder.RecordErrorAsync(new ErrorRecord(
                    ErrorType: errorType,
                    ErrorMessage: message,
                    Severity: severity,
                    Context: context,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordDecisionAsync — create ExecutionRecord with Context = BuildCorrelation()</summary>
    public async Task RecordDecisionAsync(string decision, ITraversalContext ctx)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.StateDecision);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: decision,
                    Status: "ok",
                    SpanType: Observability.SpanType.StateDecision,
                    Context: context,
                    SpanId: spanId,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordStateTransitionAsync — create StateTransition with Context = BuildCorrelation(), FsmType="TraversalFSM"</summary>
    public async Task RecordStateTransitionAsync(string fromState, string toState)
    {
        await LogAndContinueAsync(async () =>
        {
            var context = BuildCorrelation();
            if (_recorder != null)
                await _recorder.RecordTransitionAsync(new StateTransition(
                    FromState: fromState,
                    ToState: toState,
                    Context: context,
                    FsmType: "TraversalFSM",
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordRootNodePushedAsync — create StateTransition with Context=null (before step loop), FsmType="TraversalFSM"</summary>
    public async Task RecordRootNodePushedAsync(string nodeId)
    {
        await LogAndContinueAsync(async () =>
        {
            if (_recorder != null)
                await _recorder.RecordTransitionAsync(new StateTransition(
                    FromState: "init",
                    ToState: "node_select",
                    Context: null, // Before step loop — no engine context available
                    FsmType: "TraversalFSM",
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordPageTransitionAsync — create PageTransition with Context = BuildCorrelation()</summary>
    public async Task RecordPageTransitionAsync(string fromPath, string toPath, string transitionType)
    {
        await LogAndContinueAsync(async () =>
        {
            var context = BuildCorrelation();
            if (_recorder != null)
                await _recorder.RecordPageTransitionAsync(new PageTransition(
                    FromPage: fromPath,
                    ToPage: toPath,
                    TransitionType: transitionType,
                    Context: context,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordDynamicLifecycleAsync → DfsForward — create ExecutionRecord with Context, ChildNodeId, ParentNodeId.
    /// Sets _currentVisitSpanId to track the current node visit span.</summary>
    public async Task RecordDynamicLifecycleAsync(string @event, string nodeId, string parentId, string ruleId, string elementId)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            _currentVisitSpanId = spanId;
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.DfsForward);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: @event,
                    Status: "ok",
                    SpanType: Observability.SpanType.DfsForward,
                    Context: context,
                    SpanId: spanId,
                    ChildNodeId: nodeId,
                    ParentNodeId: parentId,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    /// <summary>RecordStateDecisionAsync — create ExecutionRecord with Context = BuildCorrelation()</summary>
    public async Task RecordStateDecisionAsync(string decision, string nodeId, Dictionary<string, string>? metadata)
    {
        await LogAndContinueAsync(async () =>
        {
            var spanId = NextSpanId();
            var context = BuildCorrelation();
            _stepSpanTypes.Add(Observability.SpanType.StateDecision);
            if (_recorder != null)
                await _recorder.RecordExecutionAsync(new ExecutionRecord(
                    Action: decision,
                    Status: "ok",
                    SpanType: Observability.SpanType.StateDecision,
                    Context: context,
                    SpanId: spanId,
                    Timestamp: DateTimeOffset.UtcNow));
        });
    }

    // ── StepTraceSnapshot ──────────────────────────────────

    /// <summary>GetStepSnapshot — returns accumulated SpanTypes for this step, resets on read</summary>
    public ImmutableArray<Observability.SpanType> GetStepSnapshot()
    {
        var snapshot = _stepSpanTypes.ToImmutableArray();
        _stepSpanTypes.Clear();
        return snapshot;
    }

    // ── Trace level gates ──────────────────────────────────

    public bool ShouldRecordEntryAttempt(TraceLevel level) => level >= TraceLevel.Basic;
    public bool ShouldRecordVisionCall(TraceLevel level) => level >= TraceLevel.Detailed;

    private async Task LogAndContinueAsync(Func<Task> func)
    {
        if (!Active) return;
        try { await func(); }
        catch (Exception ex) { Console.WriteLine($"[TraceCoordinator Warning] {ex.GetType().Name}: {ex.Message}"); }
    }
}

/// <summary>
/// IEntryPolicyExecutor — 入口策略执行接口。
/// 2 methods: ExecuteAsync + BuildChain。
/// </summary>
public interface IEntryPolicyExecutor
{
    Task<EntryResult> ExecuteAsync(
        EntryPolicy policy,
        EntryConfig config,
        string targetApp,
        CancellationToken cancellationToken = default);

    List<EntryStrategy> BuildChain(EntryPolicy policy);
}

/// <summary>
/// Device-owned entry action seam. Core coordinates strategy and wait policy;
/// concrete ADB work remains outside Core.
/// </summary>
public interface IEntryActionDriver
{
    Task<bool> OpenDeepLinkAsync(
        string target,
        CancellationToken cancellationToken = default);

    Task<bool> ColdLaunchAsync(
        string targetApp,
        CancellationToken cancellationToken = default);

    Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default);

    Task<bool> CheckConditionAsync(
        IReadOnlyDictionary<string, object>? waitCondition,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// EntryPolicyExecutor — 3 strategies + fallback chain + fast/polling wait modes。
/// </summary>
public sealed class EntryPolicyExecutor : IEntryPolicyExecutor
{
    private readonly IEntryActionDriver _driver;

    public EntryPolicyExecutor(IEntryActionDriver? driver = null)
    {
        _driver = driver ?? new DelayOnlyEntryActionDriver();
    }

    /// <summary>
    /// 执行入口策略链: primary → fallback → BIND_CURRENT_SCREEN。
    /// </summary>
    public async Task<EntryResult> ExecuteAsync(
        EntryPolicy policy,
        EntryConfig config,
        string targetApp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetApp);
        var chain = BuildChain(policy);
        EntryResult? lastFailure = null;

        foreach (var strategy in chain)
        {
            var actionSucceeded = await ExecuteStrategyAsync(
                strategy,
                targetApp,
                cancellationToken);
            if (!actionSucceeded)
            {
                lastFailure = new EntryResult(
                    false,
                    strategy,
                    $"Entry action failed for {strategy}");
                continue;
            }

            await _driver.WaitAsync(config.ActionDelayMs, cancellationToken);
            var conditionSucceeded = await VerifyWaitConditionAsync(
                policy.WaitCondition,
                config,
                cancellationToken);
            if (conditionSucceeded)
            {
                return new EntryResult(
                    true,
                    strategy,
                    $"Entry action and wait verification succeeded for {strategy}");
            }

            lastFailure = new EntryResult(
                false,
                strategy,
                $"Wait condition timed out for {strategy}");
        }

        return lastFailure
               ?? new EntryResult(
                   false,
                   EntryStrategy.BindCurrentScreen,
                   "Entry strategy chain was empty");
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

    private Task<bool> ExecuteStrategyAsync(
        EntryStrategy strategy,
        string targetApp,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            EntryStrategy.DirectDeeplink => _driver.OpenDeepLinkAsync(
                targetApp,
                cancellationToken),
            EntryStrategy.ColdLaunch => _driver.ColdLaunchAsync(
                targetApp,
                cancellationToken),
            EntryStrategy.BindCurrentScreen => Task.FromResult(true),
            _ => Task.FromResult(false),
        };
    }

    private async Task<bool> VerifyWaitConditionAsync(
        IReadOnlyDictionary<string, object>? waitCondition,
        EntryConfig config,
        CancellationToken cancellationToken)
    {
        if (config.WaitMode == WaitMode.Fast)
            return await _driver.CheckConditionAsync(waitCondition, cancellationToken);

        var timeout = TimeSpan.FromSeconds(config.WaitTimeoutSeconds);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (await _driver.CheckConditionAsync(waitCondition, cancellationToken))
                return true;
            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;
            await _driver.WaitAsync(
                (int)Math.Min(config.WaitIntervalMs, remaining.TotalMilliseconds),
                cancellationToken);
        }

        return false;
    }

    private sealed class DelayOnlyEntryActionDriver : IEntryActionDriver
    {
        public Task<bool> OpenDeepLinkAsync(
            string target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ColdLaunchAsync(
            string targetApp,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task WaitAsync(
            int milliseconds,
            CancellationToken cancellationToken = default) =>
            Task.Delay(milliseconds, cancellationToken);

        public Task<bool> CheckConditionAsync(
            IReadOnlyDictionary<string, object>? waitCondition,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(waitCondition is null || waitCondition.Count == 0);
    }
}

/// <summary>入口执行结果</summary>
public sealed record class EntryResult(
    bool Success,
    EntryStrategy Strategy,
    string Description);

/// <summary>
/// IPageCacheManager — 页面缓存管理接口，使用 ITraversalContext 参数。
/// 2 methods: Update + Restore。
/// </summary>
public interface IPageCacheManager
{
    void Update(string path, PageCacheInfo pageInfo, ITraversalContext context);
    IReadOnlyList<MenuItem>? Restore(string path, ITraversalContext context);
}

/// <summary>
/// PageCacheManager — update + restore, 极简 (Phase 2 不实现 TTL/size limits)。
/// </summary>
public sealed class PageCacheManager : IPageCacheManager
{
    /// <summary>
    /// update — 存储 PageCacheInfo 到 context.page_cache。
    /// Casts ITraversalContext to TraversalRuntimeContext for PageCache internal access.
    /// </summary>
    public void Update(string path, PageCacheInfo pageInfo, ITraversalContext context)
    {
        ((TraversalRuntimeContext)context).PageCache[path] = pageInfo;
    }

    /// <summary>
    /// restore — 返回缓存的 items 或 null。
    /// Casts ITraversalContext to TraversalRuntimeContext for PageCache internal access.
    /// </summary>
    public IReadOnlyList<MenuItem>? Restore(string path, ITraversalContext context)
    {
        if (((TraversalRuntimeContext)context).PageCache.TryGetValue(path, out var cachedObj) && cachedObj is PageCacheInfo info)
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
/// IPageSnapshotManager — 页面快照指纹管理接口。
/// 2 instance methods: Fingerprint + HasChanged。
/// </summary>
public interface IPageSnapshotManager
{
    int Fingerprint(PageAnalysis? pageAnalysis);
    bool HasChanged(PageAnalysis? before, PageAnalysis? after);
}

/// <summary>
/// PageSnapshotManager — 纯函数, 无可变状态。
/// fingerprint() + has_changed()。
/// </summary>
public sealed class PageSnapshotManager : IPageSnapshotManager
{
    /// <summary>
    /// fingerprint — 从 sorted (type, name) tuples 计算确定性整数 hash。
    /// null/empty → 0。
    /// </summary>
    public int Fingerprint(PageAnalysis? pageAnalysis)
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
    public bool HasChanged(PageAnalysis? before, PageAnalysis? after)
    {
        return Fingerprint(before) != Fingerprint(after);
    }
}

/// <summary>
/// INodeStackAdapter — 封装 NodeStack + INodeRegistry for orchestrator。
/// 3 methods: Push, Pop, Peek。
/// </summary>
public interface INodeStackAdapter
{
    void Push(TraversalNode child);
    TraversalNode? Pop();
    TraversalNode? Peek();
}

/// <summary>
/// NodeStackAdapter — 封装 NodeStack + INodeRegistry for orchestrator。
/// </summary>
public sealed class NodeStackAdapter : INodeStackAdapter
{
    private readonly NodeStack _stack;
    private readonly INodeRegistry _registry;

    /// <summary>构造 NodeStackAdapter — casts ITraversalContext to TraversalRuntimeContext for NodeStack internal access</summary>
    public NodeStackAdapter(ITraversalContext context, INodeRegistry registry)
    {
        _stack = (NodeStack)((TraversalRuntimeContext)context).NodeStack;
        _registry = registry;
    }

    /// <summary>Push — 注册节点并推入栈。深度越界时静默跳过。</summary>
    public void Push(TraversalNode child)
    {
        if (!_stack.Push(child))
            return; // Depth >= MaxDepth — don't register the node
        _registry.Register(child);
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
