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
    /// 追踪最后一个被推入栈的子节点 NodeId, 用于行为导航检测。
    /// 当该子节点执行 (tap) 导致页面指纹变化时, 以此 NodeId 为归属创建子页帧。
    /// </summary>
    private string? _lastPushedChildNodeId;

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
                    _lastPushedChildNodeId = nextChild.NodeId;
                }
                else if (currentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
                {
                    // DYNAMIC_MATCH no remaining children (or page changed)
                    // D-74: 行为导航检测优先于滚动
                    if (_lastPushedChildNodeId != null && TryHandleNavigation(ctx, currentFrame, ref frameCompleted, ref childPushed, ref nextState))
                    {
                        // navigation sub-frame pushed; frameCompleted/childPushed/nextState already set
                    }
                    else if (TryHandleScroll(ctx, currentFrame, ref frameCompleted, ref childPushed, ref nextState))
                    {
                        // scroll executed; frameCompleted/childPushed/nextState already set
                    }
                    else
                    {
                        // 无法导航、无法滚动或已到底部 → frame completed
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
                _lastPushedChildNodeId = nextChild.NodeId;
            }
            else
            {
                // DYNAMIC_MATCH no remaining children (or page changed)
                // D-74: 行为导航检测优先于滚动 — 指纹变化 = 导航, 推子页帧
                if (_lastPushedChildNodeId != null && TryHandleNavigation(ctx, currentFrame, ref frameCompleted, ref childPushed, ref nextState))
                {
                    // navigation sub-frame pushed; frameCompleted/childPushed/nextState already set
                }
                // 检查是否可以滚动以发现更多元素 (同一页面内)
                else if (TryHandleScroll(ctx, currentFrame, ref frameCompleted, ref childPushed, ref nextState))
                {
                    // scroll executed; frameCompleted/childPushed/nextState already set
                }
                else
                {
                    // 无法导航、无法滚动或已到底部
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
    /// D-74: 行为导航检测 — 比较缓存指纹与当前指纹, 若不同说明页面已变化 (导航)。
    /// 只在 TryHandleScroll 返回 false 后调用, 排除了滑动导致指纹变化的可能。
    /// </summary>
    /// <returns>true = 子页帧已推入; false = 未检测到导航, 走既有深度判断逻辑</returns>
    private bool TryHandleNavigation(
        StepContext ctx,
        ITraversalNode currentFrame,
        ref bool frameCompleted,
        ref bool childPushed,
        ref TraversalState nextState)
    {
        var cachedFingerprint = ctx.ChildMgr.GetCachedFingerprint(currentFrame.NodeId);
        if (cachedFingerprint == null)
            return false; // 无缓存 → 首次生成, 非导航

        var runtimeCtx = ctx.Context as TraversalRuntimeContext;
        var currentFingerprint = ctx.SnapshotMgr.Fingerprint(runtimeCtx?.CurrentPageAnalysis);

        if (currentFingerprint == 0 || currentFingerprint == cachedFingerprint.Value)
            return false; // 指纹相同 → 页面未变, 非导航

        // 指纹变化 + 非滚动 → 导航!
        // 推子页帧, 使当前页元素归导航子节点帧而非根帧。
        var navigatedChildNodeId = _lastPushedChildNodeId!;
        _lastPushedChildNodeId = null; // 消费追踪 id

        var subFrameNodeId = $"{navigatedChildNodeId}_subframe";
        var subFrame = new TraversalNode(
            NodeId: subFrameNodeId,
            Name: "nav_sub_page",
            NodeType: NodeType.Container,
            Operation: new Operation(OperationType.NoAction),
            ChildrenStrategy: currentFrame.ChildrenStrategy,
            ExitCondition: new ExitCondition(
                ExitConditionType.AllChildrenVisited,
                Fallback: FallbackAction.AutoEscape));

        // 注册并推入栈
        ctx.NodeRegistry.Register(subFrame);
        ctx.Stack.Push(subFrame);

        ctx.Trace.RecordDecision(
            $"navigation_detected_push_subframe:{navigatedChildNodeId}",
            ctx.Context);

        frameCompleted = false;
        childPushed = true;
        // Don't override nextState — let RunAsync transition to NodeSelect via line 207-209.
        // This prevents Step 9 from double-firing in the same ExecuteStep call (D-74).
        return true;
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

    // v1 默认中心垂直向下 swipe (内容上移 = 向下滚动发现更多)。
    // 滚动区域坐标未来可从 PageAnalysis 精确推导 (见设计 §13 延后项)。
    private const double ScrollSwipeStartX = 0.5;
    private const double ScrollSwipeStartY = 0.7;
    private const double ScrollSwipeEndX = 0.5;
    private const double ScrollSwipeEndY = 0.3;
    private const int ScrollSwipeDurationMs = 300;

    /// <summary>
    /// 统一滚动处理 (滚动 = 操作 + 判断 模型, 见设计 §6):
    /// ① <see cref="IActionExecutor.SwipeAsync"/> (操作)
    /// ② <see cref="IVisionProvider.AnalyzeCurrentPageAsync"/> (对新截图的判断)
    /// ③ <see cref="IDynamicChildManager.Invalidate"/> (重新生成子节点)
    /// ④ per-frame seen 元素 id 集合差分: 滚出未见元素 → Continue; 全是已见/不可滚动 → Stop。
    /// 不下转 <see cref="IVisionProvider"/>/<see cref="IActionExecutor"/> 到 Simulation 具体类型 ——
    /// mock 与真实服务代码路径完全相同。
    /// </summary>
    /// <returns>
    /// true = 滚动揭示了未见元素, 继续 NodeSelect (已设置 frameCompleted=false, nextState=NodeSelect);
    /// false = 到底或不可滚动, 由调用方完成帧 (root → FrameComplete; 非 root → PressBack + Pop)。
    /// </returns>
    internal static bool TryHandleScroll(
        StepContext ctx,
        ITraversalNode currentFrame,
        ref bool frameCompleted,
        ref bool childPushed,
        ref TraversalState nextState)
    {
        // 不可滚动或已到底 → 不 swipe, 直接完成
        if (!ctx.Vision.HasScroll() || ctx.Vision.IsEndOfList())
            return false;

        // seed: 把滚动前页面元素记入 seen 集合 (首次调用建立 page-0 基线, 后续调用幂等)
        ctx.Context.RecordSeenElementIds(currentFrame.NodeId, GetElementIds(ctx.Context.CurrentPageAnalysis));

        // ① 操作: 垂直 swipe (向下滚动发现更多内容)
        ctx.Action.SwipeAsync(
            ScrollSwipeStartX, ScrollSwipeStartY,
            ScrollSwipeEndX, ScrollSwipeEndY,
            ScrollSwipeDurationMs).GetAwaiter().GetResult();

        // ② 重新截图: 对操作后的新页面分析
        var after = ctx.Vision.AnalyzeCurrentPageAsync().GetAwaiter().GetResult();
        ctx.Context.SetCurrentPageAnalysis(after);

        // ③ 失效子节点缓存, 随后 NodeSelect 从新 PageAnalysis 重新生成/选择子节点
        ctx.ChildMgr.Invalidate(currentFrame.NodeId);

        // ④ 判断: seen-set 差分 —— 本次滚动后是否出现未见元素
        bool revealedNew = after != null
            && ctx.Context.RecordSeenElementIds(currentFrame.NodeId, GetElementIds(after));

        ctx.Trace.RecordDecision(
            revealedNew ? "scroll_revealed_new_elements" : "scroll_no_new_elements_end_reached",
            ctx.Context);

        if (revealedNew)
        {
            // 有新内容 → 继续 NodeSelect (生成/选择新子节点)
            frameCompleted = false;
            childPushed = false;
            nextState = TraversalState.NodeSelect;
            return true;
        }

        // 到底: 清理该帧 seen 集合, 由调用方完成帧
        ctx.Context.ClearSeenElementIds(currentFrame.NodeId);
        return false;
    }

    /// <summary>
    /// 从 <see cref="PageAnalysis"/> 提取非空元素 id (用于 seen-set 差分)。
    /// </summary>
    private static IEnumerable<string> GetElementIds(Domain.Models.Content.PageAnalysis? analysis)
    {
        if (analysis == null || analysis.Items.IsDefault)
            yield break;

        foreach (var item in analysis.Items)
        {
            if (!string.IsNullOrEmpty(item.Name))
                yield return item.Name;
        }
    }
}
