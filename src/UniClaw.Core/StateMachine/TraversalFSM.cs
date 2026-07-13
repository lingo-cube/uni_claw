using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.StateMachine.Scroll;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// TraversalFSM — 8 状态 × 修正转换矩阵 (D-1: 移除 PRECONDITION_CHECK → BRANCH)。
/// step() 通过 enum-based switch 分发到 handler，try-catch 包裹，异常路由到 ERROR_HANDLING。
/// </summary>
public sealed class TraversalFSM : ITraversalStateMachine
{
    private readonly TraversalRuntimeContext _runtimeContext;

    /// <summary>
    /// 跟踪每个节点的已访问滚动进度范围，防止在相同进度范围内重复重置 VisitedChildren。
    /// Key: nodeId, Value: 已访问的滚动进度范围集合 (min, max)
    /// </summary>
    private readonly Dictionary<string, List<(double min, double max)>> _visitedScrollRanges = new();

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
    public TraversalFSM(TraversalRuntimeContext context)
    {
        _runtimeContext = context;
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
            return StateTransitionResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// step() — 单步 FSM 执行。无参版本委托给 Step(null)，保持非破坏性兼容。
    /// </summary>
    public TraversalState Step() => Step(null);

    /// <summary>
    /// step(StepContext?) — 单步 FSM 执行，携带 StepContext 供 handler 使用。
    /// ctx 在 DispatchHandler 前存储到 _currentStepContext，之后清除。
    /// 与 Step() 共享同一 try-catch 异常路由逻辑。
    /// </summary>
    public TraversalState Step(StepContext? ctx)
    {
        var fromState = CurrentState;
        TraversalState nextState;

        try
        {
            _currentStepContext = ctx;
            nextState = DispatchHandler(fromState);
        }
        catch (Exception ex)
        {
            // Exception: route to ERROR_HANDLING regardless of handler
            RuntimeContext.SetLastError(ex);
            RuntimeContext.IncrementConsecutiveErrors();
            nextState = TraversalState.ErrorHandling;
        }
        finally
        {
            _currentStepContext = null;
        }

        TransitionTo(nextState);
        return nextState;
    }

    /// <summary>
    /// 按 from_state 分发到 handler — enum-based switch，非 if/elif。
    /// </summary>
    private TraversalState DispatchHandler(TraversalState fromState)
    {
        return fromState switch
        {
            TraversalState.NodeSelect => HandleNodeSelect(),
            TraversalState.PreconditionCheck => HandlePreconditionCheck(),
            TraversalState.Execute => HandleExecute(),
            TraversalState.ResultVerify => HandleResultVerify(),
            TraversalState.Branch => HandleBranch(),
            TraversalState.FrameComplete => HandleFrameComplete(),
            TraversalState.ErrorHandling => HandleErrorHandling(),
            TraversalState.PopupHandling => HandlePopupHandling(),
            _ => TraversalState.ErrorHandling // Unknown state = error
        };
    }

    private TraversalState HandleNodeSelect()
    {
        // Node stack empty → BRANCH (need to select a new subtree)
        // Stack has current node → PRECONDITION_CHECK
        if (Context.NodeStack.IsEmpty)
            return TraversalState.Branch;
        return TraversalState.PreconditionCheck;
    }

    private TraversalState HandlePreconditionCheck()
    {
        // D1: Assume pass with explicit trace logging (real check in Phase 3)
        // ITraversalNode interface doesn't expose Precondition —
        // the FSM handler only decides which state to go to next
        _currentStepContext?.Trace.RecordDecision("precondition_assume_pass", Context);
        return TraversalState.Execute;
    }

    private TraversalState HandleExecute()
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
            OperationDispatcher.DispatchAsync(operation, _currentStepContext.Action)
                .GetAwaiter().GetResult();

            // Optional restore (only for the original operation's restore)
            if (tNode.Operation.Restore != null)
            {
                try
                {
                    OperationDispatcher.DispatchAsync(
                        new Operation(
                            tNode.Operation.Restore.Action,
                            tNode.Operation.Restore.Target,
                            tNode.Operation.Restore.Params),
                        _currentStepContext.Action)
                        .GetAwaiter().GetResult();
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

        // Find matching MenuItem in current page analysis
        var pageAnalysis = RuntimeContext.CurrentPageAnalysis;
        if (pageAnalysis == null)
            return operation; // No page analysis → can't resolve (will fail at dispatch)

        var matchingItem = pageAnalysis.Items.FirstOrDefault(item =>
            string.Equals(item.Name, targetText, StringComparison.OrdinalIgnoreCase));

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

    private TraversalState HandleResultVerify()
    {
        // D2: 3-round retry + vision correction + popup detection
        // No StepContext → stub fallback (backward compat)
        if (_currentStepContext == null)
            return TraversalState.Branch;

        var trace = _currentStepContext.Trace;
        var vision = _currentStepContext.Vision;
        var ctx = _currentStepContext.Context;

        // Get "before" page analysis (from context — snapshot before action execution)
        var beforeAnalysis = ctx.CurrentPageAnalysis;

        // First check — did the page change after action?
        var afterAnalysis = vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
        ctx.SetCurrentPageAnalysis(afterAnalysis);

        if (_currentStepContext.SnapshotMgr.HasChanged(beforeAnalysis, afterAnalysis))
        {
            trace.RecordDecision("verification_passed_first_check", Context);
            return TraversalState.Branch;
        }

        // Retry loop — up to 3 rounds with vision re-call + popup detection
        for (int round = 1; round <= 3; round++)
        {
            trace.RecordDecision($"verification_retry_round_{round}", Context);

            // Re-call vision for fresh page analysis
            afterAnalysis = vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
            ctx.SetCurrentPageAnalysis(afterAnalysis);

            // Check for popup — PageAnalysis.IsPopup is the authoritative detection
            // from the vision/AI layer. PopupDetector regex matching is only used
            // as supplementary classification when IsPopup is already true.
            if (afterAnalysis?.IsPopup == true)
            {
                trace.RecordDecision("verification_popup_detected_during_retry", Context);
                return TraversalState.PopupHandling;
            }

            // Re-check if page has changed
            if (_currentStepContext.SnapshotMgr.HasChanged(beforeAnalysis, afterAnalysis))
            {
                trace.RecordDecision($"verification_passed_round_{round}", Context);
                return TraversalState.Branch;
            }
        }

        // All 3 rounds failed → Branch (continue traversal, don't block)
        trace.RecordDecision("verification_failed_3_rounds", Context);
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

    private TraversalState HandleBranch()
    {
        var frame = Context.NodeStack.Peek();
        var node = frame?.Node;
        int depth = Context.NodeStack.Depth;

        // Null node: FrameComplete if depth > 1, else NodeSelect
        if (node == null)
            return depth > 1 ? TraversalState.FrameComplete : TraversalState.NodeSelect;

        var strategy = node.ChildrenStrategy.Type;

        // DYNAMIC_MATCH: D3 — check unvisited children first, then try scroll if exhausted.
        // When no StepContext is available, fall back to original optimistic NodeSelect
        // (the DynamicMatcher will discover children from the current page analysis).
        if (strategy == ChildrenStrategyType.DynamicMatch)
        {
            // Check if there are unvisited static children (engine uses this for discovery)
            if (HasUnvisitedStaticChildren(node))
                return TraversalState.NodeSelect;

            // Only attempt scroll when StepContext with scrollable vision is available
            if (_currentStepContext != null)
                return TryHandleScroll(node, depth);

            // No scroll context: optimistic NodeSelect (original DynamicMatch behavior)
            return TraversalState.NodeSelect;
        }

        // STATIC: check unvisited children
        if (strategy == ChildrenStrategyType.Static)
        {
            if (HasUnvisitedStaticChildren(node))
                return TraversalState.NodeSelect;
            // All visited → check scroll (7.1, 7.2)
            return TryHandleScroll(node, depth);
        }

        // NONE: leaf or container
        bool isLeaf = node.NodeType != NodeType.Container && node.NodeType != NodeType.Screen;

        if (isLeaf)
        {
            // Leaf at depth 1 (root leaf) → NodeSelect
            // Leaf at depth > 1 → FrameComplete (pop back to parent)
            return depth > 1 ? TraversalState.FrameComplete : TraversalState.NodeSelect;
        }

        // Container with NONE strategy → FrameComplete
        return TraversalState.FrameComplete;
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

    /// <summary>
    /// 尝试处理滚动：当所有子节点已访问时，检查是否可以滚动以发现更多元素。
    /// (7.1, 7.2, 7.3, 7.4)
    ///
    /// 修复: D1 进度检查 + D2 元素计数 + D4 选择性重置 + D5 早期退出
    /// </summary>
    /// <param name="node">当前节点</param>
    /// <param name="depth">当前深度</param>
    /// <returns>下一个状态</returns>
    private TraversalState TryHandleScroll(ITraversalNode node, int depth)
    {
        // 检查 StepContext 是否可用
        // 如果没有 StepContext，返回原始行为（所有子节点已访问 → FrameComplete）
        if (_currentStepContext == null)
            return TraversalState.FrameComplete;

        // 检查 Vision Provider 是否支持滚动（使用接口方法）
        // 如果不支持滚动，返回原始行为（所有子节点已访问 → FrameComplete）
        if (!_currentStepContext.Vision.HasScroll())
            return TraversalState.FrameComplete;

        // D5: 早期退出 — 检查是否已到达列表末尾（在创建 ScrollHandler 之前）
        // 如果已到底部，返回 FrameComplete，避免不必要的 ScrollHandler 创建
        if (_currentStepContext.Vision.IsEndOfList())
            return TraversalState.FrameComplete;

        // 检查是否为 ScrollableMockVisionService（用于滚动执行）
        // 非 ScrollableMockVisionService 实现不提供滚动执行功能
        if (_currentStepContext.Vision is not ScrollableMockVisionService scrollableVision)
            return TraversalState.FrameComplete;

        // 获取当前页面信息和滚动状态
        var currentProgress = scrollableVision.GetScrollProgress(scrollableVision.CurrentPageId);
        var maxThreshold = scrollableVision.GetMaxThreshold(scrollableVision.CurrentPageId);

        // 获取当前可见元素 ID（滚动前）— 用于 D2 元素计数比较和 D4 选择性重置
        var currentPageAnalysis = RuntimeContext.CurrentPageAnalysis;
        var beforeElementIds = currentPageAnalysis?.Items
            .Select(i => i.Name ?? "")
            .Where(name => !string.IsNullOrEmpty(name))
            .ToImmutableArray() ?? ImmutableArray<string>.Empty;

        // 记录滚动前的唯一元素计数（用于 D2）
        var uniqueBeforeCount = beforeElementIds.Distinct().Count();

        // 直接执行滚动，不使用 ScrollHandler（简化逻辑）
        // 使用默认步长百分比
        var stepPercent = 0.3; // ScrollHandlerConfig.Default().DefaultStepPercent

        // 模拟滚动
        var newProgress = scrollableVision.SimulateScroll(stepPercent);

        // D1: 进度检查 — 如果滚动没有前进，视为失败
        var progressDelta = newProgress - currentProgress;
        if (progressDelta <= scrollableVision.Config.ProgressEpsilon)
        {
            // 进度未前进，滚动失败
            _currentStepContext?.Trace.RecordDecision("scroll_failed_no_progress", Context);
            if (scrollableVision.IsEndOfList)
                return TraversalState.FrameComplete;
            return TraversalState.FrameComplete;
        }

        // 滚动后重新获取元素 ID
        var afterAnalysis = _currentStepContext?.Vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
        RuntimeContext.SetCurrentPageAnalysis(afterAnalysis);

        // 滚动后失效 DynamicChildManager 缓存，强制从新 PageAnalysis 重新生成子节点
        _currentStepContext?.ChildMgr.Invalidate(node.NodeId);

        var afterElementIds = afterAnalysis?.Items
            .Select(i => i.Name ?? "")
            .Where(name => !string.IsNullOrEmpty(name))
            .ToImmutableArray() ?? ImmutableArray<string>.Empty;
        var uniqueAfterCount = afterElementIds.Distinct().Count();

        // D2: 元素计数检查 — 比较滚动前后的唯一元素数量
        var elementsIncreased = uniqueAfterCount > uniqueBeforeCount;

        // 记录滚动决策到 trace
        _currentStepContext?.Trace.RecordDecision(
            elementsIncreased ? "scroll_success_elements_increased" : "scroll_failed_no_new_elements",
            Context);

        // 7.3 + D1 + D2: 进度前进且元素计数增加 → 检查是否已访问此进度范围 → 重置 VisitedChildren → NodeSelect
        if (elementsIncreased)
        {
            // D6: 检查此进度范围是否已访问过
            var progressRange = (min: currentProgress, max: newProgress);

            // 确保节点有跟踪记录
            if (!_visitedScrollRanges.ContainsKey(node.NodeId))
                _visitedScrollRanges[node.NodeId] = new List<(double, double)>();

            var visitedRanges = _visitedScrollRanges[node.NodeId];

            // 检查新进度范围是否与任何已访问范围重叠（考虑 epsilon 容差）
            var epsilon = scrollableVision.Config.ProgressEpsilon;
            var alreadyVisited = visitedRanges.Any(r =>
                // 检查是否有重叠： !(newMax < r.min - epsilon || newMin > r.max + epsilon)
                !(progressRange.max < r.min - epsilon || progressRange.min > r.max + epsilon));

            if (alreadyVisited)
            {
                // 此进度范围已访问过，不重置 VisitedChildren，直接返回 FrameComplete
                _currentStepContext?.Trace.RecordDecision("scroll_range_already_visited", Context);
                return TraversalState.FrameComplete;
            }

            // 新的进度范围，记录并重置 VisitedChildren
            visitedRanges.Add(progressRange);

            // 更新上下文中的滚动进度
            RuntimeContext.UpdateScrollProgress(newProgress);

            // D4: 选择性重置 — 仅重置滚动前存在的元素
            // 保留滚动后才标记访问的元素，避免重新访问新发现的元素
            //
            // 注意：由于 VisitedChildren 使用节点 ID 而 PageAnalysis 使用元素名称，
            // 直接的精确匹配在当前架构下不可行（需要访问完整节点定义）。
            // 暂时使用完全重置，依赖 D1/D2 的循环检测来防止无限循环。
            //
            // TODO: 未来可以通过 TraversalEngine 访问 StaticNodes 来建立精确映射
            RuntimeContext.ResetVisitedChildren(node.NodeId);

            return TraversalState.NodeSelect;
        }

        // 7.4 + D1 + D2: 滚动失败、进度未前进或元素计数未增加
        // 检查是否到达列表末尾 —— 如果已到达末尾，返回 FrameComplete 完成遍历
        // 这解决了根节点滚动耗尽后的循环问题
        if (scrollableVision.IsEndOfList)
            return TraversalState.FrameComplete;

        // 未到末尾但滚动失败（无新元素或进度未前进）—— 返回 FrameComplete 避免无限循环
        // 不论 depth 是多少，都应该终止当前节点的遍历，因为滚动已经无法带来新的进展
        return TraversalState.FrameComplete;
    }

    private TraversalState HandleFrameComplete()
    {
        // Frame completed → back to parent context
        return TraversalState.NodeSelect;
    }

    private TraversalState HandleErrorHandling()
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

        // Build strategy selection context
        var strategyCtx = new StrategySelectionContext(
            RetryCount: ctx.ConsecutiveErrors,
            MaxRetries: 3,
            CanBacktrack: ctx.NodeStack.Depth > 1,
            StackDepth: ctx.NodeStack.Depth,
            CanSkip: true);

        // Execute 3-step pipeline: classify → select → execute
        var result = errorHandler.HandleError(classificationCtx, strategyCtx, error);

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

        // Consecutive error tracking: increment on Retry, reset on non-Retry
        if (result.Strategy == ErrorStrategy.Retry)
            ctx.IncrementConsecutiveErrors();
        else
            ctx.ResetConsecutiveErrors();

        // Trace recording
        trace.RecordStateDecision($"{result.Strategy}→{nextState}",
            Context.CurrentFrame?.NodeId ?? "unknown",
            new Dictionary<string, string>
            {
                ["strategy"] = result.Strategy.ToString(),
                ["outcome"] = result.Outcome.ToString()
            });
        trace.RecordErrorSpan(
            error?.GetType().Name ?? "unknown",
            error?.Message ?? "no error",
            result.Strategy == ErrorStrategy.Abort ? ErrorSeverity.Fatal : ErrorSeverity.Error);

        return nextState;
    }

    private TraversalState HandlePopupHandling()
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

        // Trace recording
        trace.RecordStateTransition("PopupHandling", nextState.ToString());
        trace.RecordDecision($"popup_{result.Action}_→_{nextState}", Context);

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
