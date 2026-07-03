using System.Collections.Immutable;
using UniClaw.Core.Domain;
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
    /// step() — 单步 FSM 执行。enum-based switch 分发，try-catch 包裹。
    /// </summary>
    public TraversalState Step()
    {
        var fromState = CurrentState;
        TraversalState nextState;

        try
        {
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
        // After execution → RESULT_VERIFY (check outcome)
        // Placeholder — real execution in Phase 2.3
        return TraversalState.ResultVerify;
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
        // Branch decision → select next child or complete frame
        // Placeholder — real branch logic in Phase 2.3 (StepOrchestrator intercepts)
        return TraversalState.NodeSelect;
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
