using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// StepOrchestrator — 14-step execute_step() 流程, 拦截层包装 TraversalFSM。
/// Steps 8-10 是拦截 overlay, 不是替换 FSM 逻辑。
/// StepContext 封装 13 个依赖字段 (sealed record class, 构造后不可变)。
/// </summary>
public sealed class StepOrchestrator
{
    // BRANCH interception allowed source states (D-1: NOT PreconditionCheck)
    internal static readonly HashSet<TraversalState> BranchAllowedSources =
        new() { TraversalState.Execute, TraversalState.ResultVerify, TraversalState.NodeSelect };

    /// <summary>
    /// execute_step — 14-step interception layer wrapping TraversalFSM。
    /// 严格顺序执行，无步骤跳过（除非前置条件不满足，如 path 未变化）。
    /// </summary>
    public StepResult ExecuteStep(StepContext ctx)
    {
        bool pathChanged = false;
        bool childPushed = false;
        bool frameCompleted = false;
        bool antiLoopTriggered = false;
        bool frameOverrideTriggered = false;
        TraversalState nextState;

        // Step 1: Create NodeStackAdapter — already in ctx.Stack
        // NodeStackAdapter is constructed once per step from ctx.Context + ctx.NodeRegistry

        // Step 2: Record step start via trace (no-op when ctx.Trace.active=False)
        var currentNodeId = ctx.Stack.Peek()?.NodeId ?? "";
        ctx.Trace.RecordStepStart(currentNodeId, "");

        // Step 3: Call state_machine.step and capture transition result
        var fromState = ctx.StateMachine.CurrentState;
        nextState = ctx.StateMachine.Step(ctx);

        // Step 4: Record page snapshot when path changed
        var currentPathStr = string.Join("/", ctx.Context.CurrentPath);
        if (currentPathStr != ctx.LastKnownPath)
        {
            pathChanged = true;
            // Trace: record page_analysis (path changed)
            if (ctx.Context is TraversalRuntimeContext rtc && rtc.CurrentPageAnalysis != null)
            {
                ctx.Trace.RecordPageAnalysis(rtc.CurrentPageAnalysis);
            }
        }

        // Step 5: Record action execution from handler metrics
        if (ctx.LastRecordedAction != null)
        {
            ctx.Trace.RecordActionExecution(ctx.LastRecordedAction, currentNodeId, true);
        }

        // Step 6: Record metrics spans (placeholder — sub-span data from handler)
        // ctx.Trace.RecordMetricsAsSpans(metrics); // When metrics are available

        // Step 7: Record state transition
        ctx.Trace.RecordStateTransition(fromState.ToString(), nextState.ToString());

        // Step 8: BRANCH interception — only from EXECUTE/RESULT_VERIFY/NODE_SELECT (D-1: NOT PreconditionCheck)
        if (nextState == TraversalState.Branch && BranchAllowedSources.Contains(fromState))
        {
            var currentFrame = ctx.Context.CurrentFrame;
            if (currentFrame != null)
            {
                var nextChild = ctx.ChildMgr.GetNextUnvisitedChild(
                    FromFrame(currentFrame), ctx.Context);

                if (nextChild != null)
                {
                    childPushed = true;
                    ctx.Stack.Push(nextChild);
                }
                else if (currentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
                {
                    // DYNAMIC_MATCH no remaining children — check scroll before frame completion
                    if (TryHandleScroll(ctx, currentFrame, ref frameCompleted, ref childPushed, ref nextState))
                    {
                        // scroll executed; frameCompleted/childPushed/nextState already set
                    }
                    else
                    {
                        // 无法滚动或已到底部 → frame completed
                        frameCompleted = true;
                    }
                }
                else
                {
                    // Static children exhausted → force frame completion
                    frameCompleted = true;
                }
            }
        }

        // Step 9: NODE_SELECT + DYNAMIC_MATCH → push child or sub-page completion
        if (nextState == TraversalState.NodeSelect && ctx.Context.CurrentFrame != null
            && ctx.Context.CurrentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            var currentFrame = ctx.Context.CurrentFrame;
            var nextChild = ctx.ChildMgr.GetNextUnvisitedChild(
                FromFrame(currentFrame), ctx.Context);

            if (nextChild != null)
            {
                // Normal: push child onto stack
                childPushed = true;
                ctx.Stack.Push(nextChild);
            }
            else
            {
                // DYNAMIC_MATCH no remaining children
                // 检查是否可以滚动以发现更多元素
                if (TryHandleScroll(ctx, currentFrame, ref frameCompleted, ref childPushed, ref nextState))
                {
                    // scroll executed; frameCompleted/childPushed/nextState already set
                }
                else
                {
                    // 无法滚动或已到底部
                    int currentDepth = ctx.Context.NodeStack.Depth;

                    if (currentDepth > 1)
                    {
                        // 非根节点：执行 PressBack 逻辑返回父节点
                        ctx.Action.PressBackAsync().GetAwaiter().GetResult();
                        ctx.Stack.Pop();

                        // Sub-page completed: pop back to parent, continue traversal
                        // The parent node will select its next unvisited child in a subsequent step.
                        frameCompleted = false;
                        childPushed = false;
                        nextState = TraversalState.NodeSelect;
                    }
                    else
                    {
                        // 根节点且无法滚动：标记帧完成，让 RunAsync 检查终止条件
                        frameCompleted = true;
                        childPushed = false;
                        nextState = TraversalState.NodeSelect;
                    }
                }
            }
        }

        // Step 10: FRAME_COMPLETE interception override — DYNAMIC_MATCH has remaining children
        if (nextState == TraversalState.FrameComplete && ctx.Context.CurrentFrame != null
            && ctx.Context.CurrentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            var currentFrame = ctx.Context.CurrentFrame;
            var nextChild = ctx.ChildMgr.GetNextUnvisitedChild(
                FromFrame(currentFrame), ctx.Context);

            if (nextChild != null)
            {
                // Override: push remaining child instead of completing frame
                frameOverrideTriggered = true;
                childPushed = true;
                frameCompleted = false;
                ctx.Stack.Push(nextChild);
                nextState = TraversalState.NodeSelect; // Override state
            }
            else
            {
                // No remaining children → proceed normally with FRAME_COMPLETE
                frameCompleted = true;
            }
        }

        // Step 11: Determine next state considering overrides
        // (handled in steps 9/10 — nextState already reflects overrides)

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
        ctx.Trace.RecordStepEnd(currentNodeId, nextState.ToString());

        return new StepResult(nextState, pathChanged, childPushed, frameCompleted, antiLoopTriggered, frameOverrideTriggered);
    }

    /// <summary>
    /// 从 ITraversalNode 构建 TraversalNode 用于 DynamicChildManager。
    /// ITraversalNode 不暴露 Operation, 但 DynamicChildManager 需要完整 TraversalNode。
    /// </summary>
    private static TraversalNode FromFrame(ITraversalNode frame)
    {
        return new TraversalNode(
            NodeId: frame.NodeId,
            Name: frame.Name,
            NodeType: frame.NodeType,
            Operation: new Operation(OperationType.NoAction),
            ChildrenStrategy: frame.ChildrenStrategy);
    }

    /// <summary>
    /// 尝试滚动以发现更多 DynamicMatch 子节点。
    /// 优先通过 ScrollableMockActionExecutor.ScrollDown 执行（以记录滚动指标），
    /// 回退到 ScrollableMockVisionService.SimulateScroll 直接调用。
    /// </summary>
    /// <returns>true 表示滚动已执行；false 表示无法滚动（已到底或不可滚动）</returns>
    private static bool TryHandleScroll(
        StepContext ctx,
        ITraversalNode currentFrame,
        ref bool frameCompleted,
        ref bool childPushed,
        ref TraversalState nextState)
    {
        bool hasScroll = ctx.Vision.HasScroll();
        bool isEnd = ctx.Vision.IsEndOfList();
        if (!hasScroll || isEnd)
            return false;

        if (ctx.Vision is not Simulation.Scroll.ScrollableMockVisionService scrollableVision)
            return false;

        var stepPercent = 0.3; // ScrollHandlerConfig.Default().DefaultStepPercent

        // 优先通过 action executor 执行滚动（记录 ScrollHistory 指标）
        if (ctx.Action is Simulation.Scroll.ScrollableMockActionExecutor scrollableAction)
        {
            scrollableAction.ScrollDown(stepPercent);
        }
        else
        {
            scrollableVision.SimulateScroll(stepPercent);
        }

        // 更新上下文中的滚动进度
        var newProgress = scrollableVision.GetScrollProgress(scrollableVision.CurrentPageId);
        ctx.Context.UpdateScrollProgress(newProgress);

        // 记录滚动决策
        ctx.Trace.RecordDecision("scroll_to_discover_more", ctx.Context);

        // 重新分析页面以获取新元素
        var afterAnalysis = ctx.Vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
        ctx.Context.SetCurrentPageAnalysis(afterAnalysis);

        // 滚动后失效 DynamicChildManager 缓存，强制从新 PageAnalysis 重新生成子节点
        ctx.ChildMgr.Invalidate(currentFrame.NodeId);

        // 滚动后继续遍历
        frameCompleted = false;
        childPushed = false;
        nextState = TraversalState.NodeSelect;
        return true;
    }
}
