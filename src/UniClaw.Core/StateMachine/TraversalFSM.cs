using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.StateMachine;

/// <summary>
/// TraversalFSM — 8 状态 × 修正转换矩阵 (D-1: 移除 PRECONDITION_CHECK → BRANCH)。
/// step() 通过 enum-based switch 分发到 handler，try-catch 包裹，异常路由到 ERROR_HANDLING。
/// </summary>
public sealed class TraversalFSM : ITraversalStateMachine
{
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
                TraversalState.Branch, TraversalState.PopupHandling),
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

    /// <summary>遍历上下文</summary>
    public ITraversalContext Context { get; }

    /// <summary>当前步骤上下文 — Step(StepContext) 在 DispatchHandler 前设置，之后清除</summary>
    private StepContext? _currentStepContext;

    /// <summary>
    /// 构造 TraversalFSM
    /// </summary>
    public TraversalFSM(ITraversalContext context)
    {
        Context = context;
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
            Context.LastError = ex;
            if (Context is TraversalRuntimeContext rtc)
                rtc.IncrementConsecutiveErrors();
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
        // Precondition check: assume pass (real check in Phase 2.3)
        // ITraversalNode interface doesn't expose Precondition —
        // the FSM handler only decides which state to go to next
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
            // Execute primary operation via OperationDispatcher
            OperationDispatcher.DispatchAsync(tNode.Operation, _currentStepContext.Action)
                .GetAwaiter().GetResult();

            // Optional restore
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
            Context.LastError = ex;
            if (Context is TraversalRuntimeContext rtc)
                rtc.IncrementConsecutiveErrors();
            return TraversalState.ErrorHandling;
        }
    }

    private TraversalState HandleResultVerify()
    {
        // Verification passed → BRANCH (select next)
        // Popup detected → POPUP_HANDLING
        // Placeholder — real verification in Phase 2.3
        return TraversalState.Branch;
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

        // DYNAMIC_MATCH: optimistic — return NodeSelect (engine gates actual availability)
        if (strategy == ChildrenStrategyType.DynamicMatch)
            return TraversalState.NodeSelect;

        // STATIC: check unvisited children
        if (strategy == ChildrenStrategyType.Static)
        {
            if (HasUnvisitedStaticChildren(node))
                return TraversalState.NodeSelect;
            // All visited → container complete
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
        // Error recovery → retry, backtrack, or abort
        // Placeholder — real error handling in Phase 2.3
        return TraversalState.NodeSelect;
    }

    private TraversalState HandlePopupHandling()
    {
        // Popup handled → resume verification
        // Placeholder — real popup handling in Phase 2.3
        return TraversalState.ResultVerify;
    }

    /// <inheritdoc/>
    public bool HasUnvisitedChildren(IGraphTraversalEngine? engine = null)
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
