using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// TraversalFSM — 8 状态 × 修正转换矩阵 (D-1: 移除 PRECONDITION_CHECK → BRANCH)。
/// step() 通过 enum-based switch 分发到 handler，try-catch 包裹，异常路由到 ERROR_HANDLING。
/// </summary>
public sealed class TraversalFSM : ITraversalStateMachine
{
    private readonly TraversalRuntimeContext _runtimeContext;
    private readonly ILogger<TraversalFSM> _logger;

    /// <summary>
    /// 修正转换矩阵 (D-1)。
    /// PRECONDITION_CHECK → BRANCH 已移除（Python V6.7 handler 从不返回 BRANCH）。
    /// </summary>
    public static readonly IReadOnlyDictionary<TraversalState, ImmutableArray<TraversalState>> TransitionMatrix =
        new Dictionary<TraversalState, ImmutableArray<TraversalState>>
        {
            [TraversalState.NodeSelect] = ImmutableArray.Create(
                TraversalState.PreconditionCheck, TraversalState.Branch),
            [TraversalState.PreconditionCheck] = ImmutableArray.Create(
                TraversalState.Execute, TraversalState.ErrorHandling),
            [TraversalState.Execute] = ImmutableArray.Create(
                TraversalState.ResultVerify, TraversalState.Branch, TraversalState.ErrorHandling),
            [TraversalState.ResultVerify] = ImmutableArray.Create(
                TraversalState.Branch, TraversalState.PopupHandling, TraversalState.ErrorHandling),
            [TraversalState.Branch] = ImmutableArray.Create(
                TraversalState.NodeSelect, TraversalState.PreconditionCheck,
                TraversalState.FrameComplete, TraversalState.ErrorHandling),
            [TraversalState.FrameComplete] = ImmutableArray.Create(
                TraversalState.NodeSelect, TraversalState.ErrorHandling),
            [TraversalState.ErrorHandling] = ImmutableArray.Create(
                TraversalState.NodeSelect, TraversalState.Execute,
                TraversalState.FrameComplete, TraversalState.Branch),
            [TraversalState.PopupHandling] = ImmutableArray.Create(
                TraversalState.ResultVerify, TraversalState.ErrorHandling),
        };

    /// <summary>当前状态</summary>
    public TraversalState CurrentState { get; internal set; } = TraversalState.NodeSelect;

    /// <summary>遍历上下文（只读视图）</summary>
    public ITraversalContext Context => _runtimeContext;

    /// <summary>运行时上下文（可写视图）— 用于内部 mutation</summary>
    public TraversalRuntimeContext RuntimeContext => _runtimeContext;

    /// <summary>当前步骤上下文 — Step(StepContext) 在 DispatchHandler 前设置，之后清除</summary>
    private StepContext? _currentStepContext;

    /// <summary>
    /// 构造 TraversalFSM
    /// </summary>
    public TraversalFSM(TraversalRuntimeContext context, ILogger<TraversalFSM>? logger = null)
    {
        _runtimeContext = context;
        _logger = logger ?? NullLogger<TraversalFSM>.Instance;
    }

    /// <summary>
    /// 转换到目标状态 — 严格转换矩阵校验。
    /// 无效转换抛出 DomainValidationException。
    /// </summary>
    public void TransitionTo(TraversalState targetState)
    {
        if (!TransitionMatrix.TryGetValue(CurrentState, out var allowedTargets)
            || !allowedTargets.Contains(targetState))
        {
            throw new DomainValidationException(
                "transition",
                $"{CurrentState}→{targetState}");
        }

        CurrentState = targetState;
    }

    /// <inheritdoc/>
    public StateTransitionResult TransitionTo(
        TraversalState targetState,
        string? nodeId = null,
        Dictionary<string, object>? metadata = null)
    {
        try
        {
            TransitionTo(targetState);
            return StateTransitionResult.Success();
        }
        catch (DomainValidationException ex)
        {
            _logger.LogWarning(ex, "Transition {From}→{To} rejected: {Message}", CurrentState, targetState, ex.Message);
            return StateTransitionResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// StepAsync() — 单步 FSM 执行。无参版本委托给 StepAsync(null)，保持非破坏性兼容。
    /// </summary>
    public Task<TraversalState> StepAsync() => StepAsync(null);

    /// <summary>
    /// StepAsync(StepContext?) — 单步 FSM 执行，携带 StepContext 供 handler 使用。
    /// ctx 在 DispatchHandlerAsync 前存储到 _currentStepContext，之后清除。
    /// 与 StepAsync() 共享同一 try-catch 异常路由逻辑。
    /// </summary>
    public async Task<TraversalState> StepAsync(StepContext? ctx)
    {
        var fromState = CurrentState;
        TraversalState nextState;

        try
        {
            _currentStepContext = ctx;
            nextState = await DispatchHandlerAsync(fromState);
        }
        catch (Exception ex)
        {
            // Exception: route to ERROR_HANDLING regardless of handler
            _logger.LogError(ex, "Step dispatch failed from {FromState}: {ExceptionType} — routing to ErrorHandling", fromState, ex.GetType().Name);
            RuntimeContext.SetLastError(ex);
            RuntimeContext.IncrementConsecutiveErrors();
            nextState = TraversalState.ErrorHandling;
        }
        finally
        {
            _currentStepContext = null;
        }

        TransitionTo(nextState);
        _logger.LogInformation("FSM {From}→{To} step={Step}", fromState, nextState, Context.StepCount);
        return nextState;
    }

    /// <summary>
    /// 按 from_state 分发到 handler — enum-based switch，非 if/elif。
    /// </summary>
    private Task<TraversalState> DispatchHandlerAsync(TraversalState fromState)
    {
        return fromState switch
        {
            TraversalState.NodeSelect => HandleNodeSelectAsync(),
            TraversalState.PreconditionCheck => HandlePreconditionCheckAsync(),
            TraversalState.Execute => HandleExecuteAsync(),
            TraversalState.ResultVerify => HandleResultVerifyAsync(),
            TraversalState.Branch => HandleBranchAsync(),
            TraversalState.FrameComplete => HandleFrameCompleteAsync(),
            TraversalState.ErrorHandling => HandleErrorHandlingAsync(),
            TraversalState.PopupHandling => HandlePopupHandlingAsync(),
            _ => Task.FromResult(TraversalState.ErrorHandling) // Unknown state = error
        };
    }

    private Task<TraversalState> HandleNodeSelectAsync()
    {
        // Node stack empty → BRANCH (need to select a new subtree)
        // Stack has current node → PRECONDITION_CHECK
        if (Context.NodeStack.IsEmpty)
            return Task.FromResult(TraversalState.Branch);
        return Task.FromResult(TraversalState.PreconditionCheck);
    }

    private async Task<TraversalState> HandlePreconditionCheckAsync()
    {
        if (_currentStepContext?.PreconditionChecker is { } checker
            && _currentStepContext.Context is TraversalRuntimeContext rtCtx)
        {
            var ok = await checker.CheckAsync(rtCtx);
            if (!ok)
            {
                RuntimeContext.SetLastError(
                    new InvalidOperationException("Precondition check failed."));
                RuntimeContext.IncrementConsecutiveErrors();
                return TraversalState.ErrorHandling;
            }
        }

        if (_currentStepContext != null)
            await _currentStepContext.Trace.RecordDecisionAsync("precondition_assume_pass", Context);
        return TraversalState.Execute;
    }

    private async Task<TraversalState> HandleExecuteAsync()
    {
        // Stub fallback: no StepContext → return hardcoded ResultVerify (non-breaking)
        if (_currentStepContext?.Action == null)
            return TraversalState.ResultVerify;

        var node = Context.NodeStack.Peek()?.Node;
        if (node == null)
            return TraversalState.ResultVerify;

        // Only TraversalNode has Operation; other ITraversalNode impls (e.g. tests) skip
        if (node is not TraversalNode tNode || tNode.Operation.Action == OperationType.NoAction)
            return TraversalState.ResultVerify;

        try
        {
            // Resolve Text-based click targets → Coordinate using current page analysis
            // DynamicMatch nodes use Target(by=Text, value=item_text) which needs
            // resolution to a Coordinate for OperationDispatcher
            var operation = ResolveTextTarget(tNode.Operation);

            // Execute primary operation via OperationDispatcher
            await OperationDispatcher.DispatchAsync(operation, _currentStepContext.Action);

            // Optional restore (only for the original operation's restore)
            if (tNode.Operation.Restore != null)
            {
                try
                {
                    await OperationDispatcher.DispatchAsync(
                        new Operation(
                            tNode.Operation.Restore.Action,
                            tNode.Operation.Restore.Target,
                            tNode.Operation.Restore.Params),
                        _currentStepContext.Action);
                }
                catch
                {
                    // Restore failure is non-critical — still return ResultVerify
                }
            }

            return TraversalState.ResultVerify;
        }
        catch (Exception ex)
        {
            RuntimeContext.SetLastError(ex);
            RuntimeContext.IncrementConsecutiveErrors();
            return TraversalState.ErrorHandling;
        }
    }

    /// <summary>
    /// Resolve Text-based Click targets → Coordinate using current page analysis.
    /// DynamicMatch nodes generate Click operations with Target(by=Text, value=item_text).
    /// The OperationDispatcher only handles Coordinate targets for Click.
    /// This method finds the matching MenuItem by text and creates a Coordinate-based Operation.
    /// </summary>
    private Operation ResolveTextTarget(Operation operation)
    {
        if (operation.Action != OperationType.Click || operation.Target == null)
            return operation;

        if (operation.Target.By != TargetType.Text)
            return operation; // Already Coordinate or UiIndex → no resolution needed

        var targetText = operation.Target.Value?.ToString();
        if (string.IsNullOrEmpty(targetText))
            return operation;

        var matchingItem = FindMatchingItem(RuntimeContext.CurrentPageAnalysis, targetText);
        if (matchingItem != null)
        {
            // Found matching item → create Coordinate-based Operation
            return new Operation(
                operation.Action,
                new Target(TargetType.Coordinate, matchingItem.Coordinate),
                operation.Params,
                operation.Restore);
        }

        // No matching item found → keep Text target (dispatch will throw → ErrorHandling)
        return operation;
    }

    /// <summary>
    /// 在页面分析中定位与目标文本匹配的 MenuItem，用于 Text→Coordinate 解析。
    /// 视觉模型 (如 sensenova flash) 对同一元素跨调用返回的名称不稳定:
    /// "[icon] Network &amp; internet" vs "Network &amp; internet"、多空白、大小写差异等。
    /// 解析策略链: ① 精确匹配 (大小写不敏感) → ② 归一化匹配 (剥离图标标记 + 折叠空白)
    /// → ③ 包含匹配 (最具体者优先)。精确匹配保持原语义; ②③ 只在精确失败时兜底。
    /// </summary>
    internal static MenuItem? FindMatchingItem(PageAnalysis? analysis, string targetText)
    {
        if (analysis == null || analysis.Items.IsDefault || analysis.Items.Length == 0)
            return null;
        if (string.IsNullOrWhiteSpace(targetText))
            return null;

        // ① Exact match (case-insensitive) — deterministic analyses and mock fixtures
        foreach (var item in analysis.Items)
        {
            if (string.Equals(item.Name, targetText, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        // ② Normalized match — icon markers / whitespace / case variance
        var normalizedTarget = NormalizeTargetText(targetText);
        if (normalizedTarget.Length > 0)
        {
            foreach (var item in analysis.Items)
            {
                if (string.Equals(NormalizeTargetText(item.Name), normalizedTarget, StringComparison.Ordinal))
                    return item;
            }
        }

        // ③ Contains match — model may rephrase labels across calls; longest shared text wins
        MenuItem? best = null;
        var bestScore = -1;
        foreach (var item in analysis.Items)
        {
            var name = item.Name ?? string.Empty;
            if (name.Length == 0)
                continue;
            if (!name.Contains(targetText, StringComparison.OrdinalIgnoreCase)
                && !targetText.Contains(name, StringComparison.OrdinalIgnoreCase))
                continue;
            var score = Math.Min(name.Length, targetText.Length);
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }
        return best;
    }

    /// <summary>
    /// 归一化元素文本用于模糊匹配 — 剥离中括号图标标记 ("[icon] X" → "X")、
    /// 转小写、折叠连续空白。不匹配图标标记的括号文本原样保留。
    /// </summary>
    internal static string NormalizeTargetText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var s = text.ToLowerInvariant();
        while (true)
        {
            var start = s.IndexOf('[');
            if (start < 0)
                break;
            var end = s.IndexOf(']', start);
            if (end < 0)
                break;
            var marker = s[(start + 1)..end].Trim();
            if (!IsIconMarker(marker))
                break;
            s = (s[..start] + " " + s[(end + 1)..]).Trim();
        }

        return string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsIconMarker(string marker)
        => marker is "icon" or "image" or "img" or "ico";


    private async Task<TraversalState> HandleResultVerifyAsync()
    {
        // D2: 3-round retry + vision correction + popup detection
        // No StepContext → stub fallback (backward compat)
        if (_currentStepContext == null)
            return TraversalState.Branch;

        var trace = _currentStepContext.Trace;
        var brain = _currentStepContext.Brain;
        var ctx = _currentStepContext.Context;

        // Get "before" page analysis (from context — snapshot before action execution)
        var beforeAnalysis = ctx.CurrentPageAnalysis;

        // First check — did the page change after action?
        var afterAnalysis = await brain.PageAnalyzer.AnalyzeCurrentPageAsync();
        ctx.SetCurrentPageAnalysis(afterAnalysis);

        if (_currentStepContext.SnapshotMgr.HasChanged(beforeAnalysis, afterAnalysis))
        {
            await trace.RecordDecisionAsync("verification_passed", Context);
            // Verified success breaks the consecutive-error streak — otherwise
            // the consecutive gate (≥3) fires before the page-item gate (≥5
            // distinct failed items) can ever accumulate, making the item gate
            // unreachable for the interleaved deny/success pattern.
            ctx.ResetConsecutiveErrors();
            return TraversalState.Branch;
        }

        // Single retry — one re-analysis after a brief settle window, mainly for
        // popup detection (popups sometimes appear after a short delay).
        await trace.RecordDecisionAsync("verification_retry_single", Context);
        afterAnalysis = await brain.PageAnalyzer.AnalyzeCurrentPageAsync();
        ctx.SetCurrentPageAnalysis(afterAnalysis);

        if (afterAnalysis?.IsPopup == true)
        {
            await trace.RecordDecisionAsync("verification_popup_detected", Context);
            return TraversalState.PopupHandling;
        }

        if (_currentStepContext.SnapshotMgr.HasChanged(beforeAnalysis, afterAnalysis))
        {
            await trace.RecordDecisionAsync("verification_passed_retry", Context);
            ctx.ResetConsecutiveErrors();
            return TraversalState.Branch;
        }

        // Page unchanged — continue traversal.
        await trace.RecordDecisionAsync("verification_page_unchanged", Context);
        return TraversalState.Branch;
    }

    /// <summary>
    /// Extract text from PageAnalysis items for popup detection.
    /// Concatenates all MenuItem names into a single string for PopupDetector regex matching.
    /// </summary>
    private static string ExtractPageText(PageAnalysis? analysis)
    {
        if (analysis == null || analysis.Items.IsDefault || analysis.Items.Length == 0)
            return string.Empty;

        return string.Join(" ", analysis.Items.Select(i => i.Name ?? ""));
    }

    private Task<TraversalState> HandleBranchAsync()
    {
        var frame = Context.NodeStack.Peek();
        var node = frame?.Node;
        int depth = Context.NodeStack.Depth;

        // Null node: FrameComplete if depth > 1, else NodeSelect
        if (node == null)
            return Task.FromResult(depth > 1 ? TraversalState.FrameComplete : TraversalState.NodeSelect);

        var strategy = node.ChildrenStrategy.Type;

        // DYNAMIC_MATCH: 有未访问子节点 → NodeSelect (发现/选择子节点);
        // 子节点耗尽 → 仍返回 NodeSelect, 由 StepOrchestrator.TryHandleScroll (Step 9 拦截)
        // 统一执行"操作+判断"滚动决策。FSM 不再持有滚动职责 (D-57 supersede)。
        if (strategy == ChildrenStrategyType.DynamicMatch)
        {
            return Task.FromResult(TraversalState.NodeSelect);
        }

        // STATIC: 有未访问子节点 → NodeSelect; 全部访问完 → FrameComplete
        // (滚动只适用于 DynamicMatch 发现更多动态子节点; Static 子节点集固定, 无可发现内容)
        if (strategy == ChildrenStrategyType.Static)
        {
            if (HasUnvisitedStaticChildren(node))
                return Task.FromResult(TraversalState.NodeSelect);
            return Task.FromResult(TraversalState.FrameComplete);
        }

        // NONE: leaf or container
        bool isLeaf = node.NodeType != NodeType.Container && node.NodeType != NodeType.Screen;

        if (isLeaf)
        {
            // Leaf at depth 1 (root leaf) → NodeSelect
            // Leaf at depth > 1 → FrameComplete (pop back to parent)
            return Task.FromResult(depth > 1 ? TraversalState.FrameComplete : TraversalState.NodeSelect);
        }

        // Container with NONE strategy → FrameComplete
        return Task.FromResult(TraversalState.FrameComplete);
    }

    /// <summary>
    /// 检查当前节点是否有未访问的静态子节点。
    /// VisitedChildren 中不存在 key 时视为空集（所有子节点未访问）。
    /// </summary>
    private bool HasUnvisitedStaticChildren(ITraversalNode node)
    {
        var visited = Context.VisitedChildren.TryGetValue(node.NodeId, out var v)
            ? v : System.Collections.Immutable.ImmutableHashSet<string>.Empty;

        return node.StaticChildren.Any(childId => !visited.Contains(childId));
    }

    private Task<TraversalState> HandleFrameCompleteAsync()
    {
        // Frame completed → back to parent context
        return Task.FromResult(TraversalState.NodeSelect);
    }

    private async Task<TraversalState> HandleErrorHandlingAsync()
    {
        // D3: Delegate to ErrorHandler pipeline (classify → select → execute)
        // No StepContext → stub fallback (backward compat)
        if (_currentStepContext == null)
            return TraversalState.NodeSelect;

        var errorHandler = _currentStepContext.ErrorHandler ?? new ErrorHandler();
        var ctx = _currentStepContext.Context;
        var trace = _currentStepContext.Trace;

        // Build classification context from last error
        var error = Context.LastError;
        var classificationCtx = new ErrorClassificationContext(
            ErrorMessage: error?.Message,
            ExceptionType: error?.GetType().Name,
            RetryCount: ctx.ConsecutiveErrors,
            MaxRetries: 3);

        // Build strategy selection context (C-3: 透传当前节点 ErrorPolicy；null 走默认)
        var strategyCtx = new StrategySelectionContext(
            RetryCount: ctx.ConsecutiveErrors,
            MaxRetries: 3,
            CanBacktrack: ctx.NodeStack.Depth > 1,
            StackDepth: ctx.NodeStack.Depth,
            CanSkip: true,
            ErrorPolicy: ctx.CurrentFrame?.ErrorPolicy);

        // Advisor: consult the AI traversal advisor before the handler pipeline.
        // The advisor's recommendation is merged into extraMetadata so the
        // strategy selector can consider it alongside the default rules.
        var extraMeta = ctx.ConsecutiveErrors > 0
            ? new Dictionary<string, object> { ["consecutive_errors"] = ctx.ConsecutiveErrors }
            : new Dictionary<string, object>();
        if (_currentStepContext.Brain.Advisor is { } advisor)
        {
            try
            {
                var advisorResult = await advisor.DecideNextActionAsync(
                    classificationCtx.ErrorMessage ?? "error",
                    ctx.CurrentPageAnalysis ?? new PageAnalysis(
                        Direction.Left, Direction.Left),
                    _currentStepContext.Context.CurrentFrame?.NodeId ?? "",
                    ctx.NodeStack.Depth);
                if (advisorResult is { Confidence: >= 0.7 })
                {
                    extraMeta["advisor_confidence"] = advisorResult.Confidence;
                    extraMeta["advisor_result"] = advisorResult.Result.ToString();
                    extraMeta["advisor_action"] = advisorResult.Action;
                    extraMeta["advisor_reasoning"] = advisorResult.Reasoning;
                    await trace.RecordDecisionAsync(
                        $"advisor_recommend_{advisorResult.Result}_{advisorResult.Action}",
                        Context);
                }
            }
            catch
            {
                await trace.RecordDecisionAsync("advisor_unavailable", Context);
            }
        }

        // Execute 3-step pipeline: classify → select → execute (with trace wrapper)
        var result = await errorHandler.HandleErrorTracedAsync(
            classificationCtx, strategyCtx, error,
            handlerTrace: _currentStepContext.HandlerTrace,
            trace: _currentStepContext.Trace,
            extraMetadata: extraMeta.Count > 0 ? extraMeta : null);

        // Map strategy to FSM transition
        var nextState = result.Strategy switch
        {
            ErrorStrategy.Retry => TraversalState.Execute,
            ErrorStrategy.Backtrack => TraversalState.NodeSelect,
            ErrorStrategy.Skip => TraversalState.Branch,
            ErrorStrategy.Continue => TraversalState.NodeSelect,
            ErrorStrategy.Abort => TraversalState.FrameComplete,
            _ => TraversalState.FrameComplete // Unknown strategy fallback
        };

        // Consecutive error tracking: increment on every error, regardless of
        // strategy.  Backtrack, Skip, Continue also represent failed items —
        // resetting on them prevents the PressBack gate from ever triggering
        // on sub-pages where all items are safety-denied.
        ctx.IncrementConsecutiveErrors();

        // Per-page item failure tracking: count distinct failed items.
        // When too many items on the same page fail (all safety-denied or
        // unresolvable), press back instead of looping forever.
        const int backOnPageItemLimit = 5;
        ctx.IncrementNodeFailedItems();
        if (ctx.NodeFailedItems >= backOnPageItemLimit
            && ctx.NodeStack.Depth > 1
            && _currentStepContext.Action is { } pageAction)
        {
            await trace.RecordDecisionAsync(
                $"error_recovery_page_item_limit_{backOnPageItemLimit}",
                Context);
            try { await pageAction.PressBackAsync(); } catch { /* best-effort */ }
            ctx.ResetNodeFailedItems();
            return TraversalState.FrameComplete;
        }

        // Exhausted all items on a sub-page: Press back after 3 consecutive
        // errors regardless of item count.  Independent of the page-item-limit
        // gate above; resets only its own counter.
        if (ctx.ConsecutiveErrors >= 3
            && ctx.NodeStack.Depth > 1
            && _currentStepContext.Action is { } backAction)
        {
            await trace.RecordDecisionAsync("error_recovery_press_back", Context);
            try { await backAction.PressBackAsync(); } catch { /* best-effort */ }
            ctx.ResetConsecutiveErrors();
            return TraversalState.FrameComplete;
        }

        // KEEP RecordErrorSpanAsync — orthogonal ErrorRecord (not ExecutionRecord)
        await trace.RecordErrorSpanAsync(
            error?.GetType().Name ?? "unknown",
            error?.Message ?? "no error",
            result.Strategy == ErrorStrategy.Abort ? ErrorSeverity.Fatal : ErrorSeverity.Error);

        return nextState;
    }

    private async Task<TraversalState> HandlePopupHandlingAsync()
    {
        // D4: Delegate to PopupHandler 6-step pipeline (detect → classify → preserve → handle → restore → validate)
        // No StepContext → stub fallback (backward compat)
        if (_currentStepContext == null)
            return TraversalState.ResultVerify;

        var popupHandler = _currentStepContext.PopupHandler ?? new PopupHandler();
        var ctx = _currentStepContext.Context;
        var trace = _currentStepContext.Trace;

        // Get current page analysis for popup text extraction
        var pageAnalysis = ctx.CurrentPageAnalysis;
        var popupText = ExtractPageText(pageAnalysis);
        var availableButtons = pageAnalysis?.Items
            .Where(i => i.Type == MenuItemType.Button || i.Type == MenuItemType.BackButton)
            .Select(i => i.Name ?? "")
            .ToList();

        // Delegate to PopupHandler 6-step pipeline
        var result = popupHandler.HandlePopup(popupText, Context, availableButtons);

        // Map result to FSM transition
        var nextState = result.Success
            ? TraversalState.ResultVerify   // Popup dismissed → back to verification
            : TraversalState.ErrorHandling;  // Popup dismiss failed → need error recovery

        // Trace — RecordHandlerLifecycleAsync replacing previous RecordStateTransitionAsync + RecordDecisionAsync
        if (_currentStepContext.HandlerTrace != null)
        {
            var traceCtx = _currentStepContext.Trace.BuildCorrelation();
            var metadata = TraceMetadata.Build()
                .Add("handling_action", result.Action)
                .Add("handling_success", result.Success);
            if (result.Classification != null)
            {
                var c = result.Classification;
                metadata.Add("popup_type", c.PopupType)
                    .Add("dismiss_strategy", c.DismissStrategy)
                    .Add("dismiss_target", c.DismissTarget)
                    .Add("urgency", c.Urgency)
                    .Add("blocking_type", c.BlockingType);
            }
            await _currentStepContext.HandlerTrace.RecordHandlerLifecycleAsync(
                "handle_popup", SpanType.PopupHandling,
                result.Success ? "success" : "fail", metadata.ToDict(), traceCtx);
        }

        return nextState;
    }

    /// <inheritdoc/>
    public bool HasUnvisitedChildren(UniClaw.Core.Traversal.IGraphTraversalEngine? engine = null)
    {
        // Simplified: check if current frame has unvisited static children
        if (Context.CurrentFrame == null) return false;
        var staticChildren = Context.CurrentFrame.StaticChildren;
        foreach (var childId in staticChildren)
        {
            if (!Context.VisitedNodes.Contains(childId))
                return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public TraversalState GetNextState()
    {
        return CurrentState;
    }

    /// <summary>
    /// 检查转换是否合法（不执行转换）
    /// </summary>
    public bool CanTransitionTo(TraversalState target)
    {
        return TransitionMatrix.TryGetValue(CurrentState, out var allowed)
            && allowed.Contains(target);
    }
}
