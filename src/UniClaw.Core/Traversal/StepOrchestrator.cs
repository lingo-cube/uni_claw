using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// StepOrchestrator — 14-step execute_step() 流程, 拦截层包装 TraversalFSM。
/// 只保留生命周期编排 (trace + FSM dispatch + visited 记账);
/// 步骤 8-10 的拦截/覆盖逻辑委托 <see cref="IInterceptionHandler"/> (D-IV 分解, 方案 A)。
/// StepContext 封装 13 个依赖字段 (sealed record class, 构造后不可变)。
/// </summary>
public sealed class StepOrchestrator
{
    // BRANCH interception allowed source states (D-1: NOT PreconditionCheck)
    // 编排条件 ("是否触发拦截"), 非拦截逻辑本身 — 留在 orchestrator (design §4)
    internal static readonly HashSet<TraversalState> BranchAllowedSources =
        new() { TraversalState.Execute, TraversalState.ResultVerify, TraversalState.NodeSelect };

    private readonly IInterceptionHandler _handler;

    public StepOrchestrator(IInterceptionHandler? handler = null)
    {
        _handler = handler ?? new InterceptionHandler();
    }

    /// <summary>
    /// execute_step — 14-step interception layer wrapping TraversalFSM。
    /// 严格顺序执行，无步骤跳过（除非前置条件不满足，如 path 未变化）。
    /// </summary>
    public async Task<StepResult> ExecuteStepAsync(StepContext ctx)
    {
        bool pathChanged = false;
        bool antiLoopTriggered = false;
        TraversalState nextState;

        // Step 1: Create NodeStackAdapter — already in ctx.Stack
        // NodeStackAdapter is constructed once per step from ctx.Context + ctx.NodeRegistry

        // Step 2: Record step start via trace (no-op when ctx.Trace.active=False)
        var currentNodeId = ctx.Stack.Peek()?.NodeId ?? "";
        await ctx.Trace.RecordStepStartAsync(currentNodeId, "");

        // Step 3: Call state_machine.step and capture transition result
        var fromState = ctx.StateMachine.CurrentState;
        nextState = await ctx.StateMachine.StepAsync(ctx);

        // Step 4: Record page snapshot when path changed
        var currentPathStr = string.Join("/", ctx.Context.CurrentPath);
        if (currentPathStr != ctx.LastKnownPath)
        {
            pathChanged = true;
            // Trace: record page_analysis (path changed)
            if (ctx.Context is TraversalRuntimeContext rtc && rtc.CurrentPageAnalysis != null)
            {
                await ctx.Trace.RecordPageAnalysisAsync(rtc.CurrentPageAnalysis);
            }
        }

        // Step 5: Record action execution from handler metrics
        if (ctx.LastRecordedAction != null)
        {
            await ctx.Trace.RecordActionExecutionAsync(ctx.LastRecordedAction, currentNodeId, true);
        }

        // Step 6: Record metrics spans (placeholder — sub-span data from handler)
        // ctx.Trace.RecordMetricsAsSpans(metrics); // When metrics are available

        // Step 7: Record state transition
        await ctx.Trace.RecordStateTransitionAsync(fromState.ToString(), nextState.ToString());

        // Steps 8-10: FSM interception delegated to IInterceptionHandler。
        // intercepted flag 守卫: 仅当 handler 实际被调用时才应用 override,
        // 防止 default(InterceptionResult) 污染 FSM 的有效 nextState。
        // nextState 逐步立即应用 (步骤 8 滚动 → NodeSelect 可级联触发步骤 9, D-74)。
        var intercepted = false;
        var interception = default(InterceptionResult);

        // Step 8: BRANCH interception — only from EXECUTE/RESULT_VERIFY/NODE_SELECT (D-1: NOT PreconditionCheck)
        if (nextState == TraversalState.Branch && BranchAllowedSources.Contains(fromState))
        {
            interception = await _handler.OnBranch(ctx, fromState);
            intercepted = true;
            nextState = interception.NextState;
        }

        // Step 9: NODE_SELECT + DYNAMIC_MATCH → push child or sub-page completion
        if (nextState == TraversalState.NodeSelect && ctx.Context.CurrentFrame != null
            && ctx.Context.CurrentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            interception = await _handler.OnDynamicMatchNodeSelect(ctx);
            intercepted = true;
            nextState = interception.NextState;
        }

        // Step 10: FRAME_COMPLETE interception override — DYNAMIC_MATCH has remaining children
        if (nextState == TraversalState.FrameComplete && ctx.Context.CurrentFrame != null
            && ctx.Context.CurrentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            interception = _handler.OnFrameComplete(ctx);
            intercepted = true;
            nextState = interception.NextState;
        }

        // Step 11: Determine next state considering overrides
        // (nextState already applied per-step above; bool overrides applied only when intercepted)
        bool childPushed = intercepted && interception.ChildPushed;
        bool frameCompleted = intercepted && interception.FrameCompleted;
        bool frameOverrideTriggered = intercepted && interception.FrameOverrideTriggered;

        // Step 12: Update visited_nodes
        if (ctx.Context.CurrentFrame != null)
        {
            ctx.Context.MarkNodeVisited(ctx.Context.CurrentFrame.NodeId);
        }

        // Step 13: Cache invalidation moved to TraversalEngine.RunAsync
        // (fingerprint-based invalidation instead of broken LastKnownPath comparison)
        // StepContext.LastKnownPath is immutable (record), so pathChanged was always true,
        // causing premature cache invalidation every step.
        // Now: TraversalEngine tracks page fingerprint and invalidates only on actual page change.

        // Step 14: Record step end via trace (no-op when ctx.Trace.active=False)
        await ctx.Trace.RecordStepEndAsync(currentNodeId, nextState.ToString());

        return new StepResult(nextState, pathChanged, childPushed, frameCompleted, antiLoopTriggered, frameOverrideTriggered);
    }
}
