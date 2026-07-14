using System.Collections.Immutable;
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

        // DYNAMIC_MATCH: 有未访问子节点 → NodeSelect (发现/选择子节点);
        // 子节点耗尽 → 仍返回 NodeSelect, 由 StepOrchestrator.TryHandleScroll (Step 9 拦截)
        // 统一执行"操作+判断"滚动决策。FSM 不再持有滚动职责 (D-57 supersede)。
        if (strategy == ChildrenStrategyType.DynamicMatch)
        {
            return TraversalState.NodeSelect;
        }

        // STATIC: 有未访问子节点 → NodeSelect; 全部访问完 → FrameComplete
        // (滚动只适用于 DynamicMatch 发现更多动态子节点; Static 子节点集固定, 无可发现内容)
        if (strategy == ChildrenStrategyType.Static)
        {
            if (HasUnvisitedStaticChildren(node))
                return TraversalState.NodeSelect;
            return TraversalState.FrameComplete;
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
