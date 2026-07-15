using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// InterceptionHandler — StepOrchestrator 步骤 8-10 的 FSM 拦截/覆盖逻辑 (D-IV 分解, 方案 A)。
/// 拥有全部 override 决策: Branch 拦截、DynamicMatch 子节点解析 (导航/滚动/PressBack)、
/// FrameComplete 覆盖, 以及 helper (TryHandleNavigation, TryHandleScrollAsync, FromFrame, GetElementIds)。
/// 所有依赖来自 StepContext, 零引用 StepOrchestrator。
/// </summary>
public sealed class InterceptionHandler : IInterceptionHandler
{
    /// <summary>
    /// 追踪最后一个被推入栈的子节点 NodeId, 用于行为导航检测。
    /// 当该子节点执行 (tap) 导致页面指纹变化时, 以此 NodeId 为归属创建子页帧。
    /// </summary>
    private string? _lastPushedChildNodeId;

    /// <summary>
    /// Step 8: BRANCH interception — 推下一个未访问子节点; DynamicMatch 耗尽时
    /// 导航检测 (D-74) 优先于滚动, 都不可行则 frame 完成。
    /// </summary>
    public async Task<InterceptionResult> OnBranch(StepContext ctx, TraversalState fromState)
    {
        var result = new InterceptionResult(TraversalState.Branch, false, false, false);

        var currentFrame = ctx.Context.CurrentFrame;
        if (currentFrame == null)
            return result;

        var nextChild = ctx.ChildMgr.GetNextUnvisitedChild(
            FromFrame(currentFrame), ctx.Context);

        if (nextChild != null)
        {
            result.ChildPushed = true;
            ctx.Stack.Push(nextChild);
            _lastPushedChildNodeId = nextChild.NodeId;
        }
        else if (currentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            // DYNAMIC_MATCH no remaining children (or page changed)
            // D-74: 行为导航检测优先于滚动
            if (_lastPushedChildNodeId != null && TryHandleNavigation(ctx, currentFrame, ref result))
            {
                // navigation sub-frame pushed; result already set
            }
            else
            {
                var (scrolled, fc, cp, ns) = await TryHandleScrollAsync(ctx, currentFrame);
                if (scrolled)
                {
                    result.FrameCompleted = fc;
                    result.ChildPushed = cp;
                    result.NextState = ns;
                }
                else
                {
                    // 无法导航、无法滚动或已到底部 → frame completed
                    result.FrameCompleted = true;
                }
            }
        }
        else
        {
            // Static children exhausted → force frame completion
            result.FrameCompleted = true;
        }

        return result;
    }

    /// <summary>
    /// Step 9: NODE_SELECT + DYNAMIC_MATCH — 推子节点或子页完成
    /// (导航检测 → 滚动 → 非根 PressBack+Pop / 根节点帧完成)。
    /// </summary>
    public async Task<InterceptionResult> OnDynamicMatchNodeSelect(StepContext ctx)
    {
        var result = new InterceptionResult(TraversalState.NodeSelect, false, false, false);

        var currentFrame = ctx.Context.CurrentFrame;
        if (currentFrame == null)
            return result;

        var nextChild = ctx.ChildMgr.GetNextUnvisitedChild(
            FromFrame(currentFrame), ctx.Context);

        if (nextChild != null)
        {
            // Normal: push child onto stack
            result.ChildPushed = true;
            ctx.Stack.Push(nextChild);
            _lastPushedChildNodeId = nextChild.NodeId;
        }
        else
        {
            // DYNAMIC_MATCH no remaining children (or page changed)
            // D-74: 行为导航检测优先于滚动 — 指纹变化 = 导航, 推子页帧
            if (_lastPushedChildNodeId != null && TryHandleNavigation(ctx, currentFrame, ref result))
            {
                // navigation sub-frame pushed; result already set
            }
            // 检查是否可以滚动以发现更多元素 (同一页面内)
            else
            {
                var (scrolled, fc, cp, ns) = await TryHandleScrollAsync(ctx, currentFrame);
                if (scrolled)
                {
                    result.FrameCompleted = fc;
                    result.ChildPushed = cp;
                    result.NextState = ns;
                }
                else
                {
                    // 无法导航、无法滚动或已到底部
                    int currentDepth = ctx.Context.NodeStack.Depth;

                    if (currentDepth > 1)
                    {
                        // 非根节点：执行 PressBack 逻辑返回父节点
                        await ctx.Action.PressBackAsync();
                        ctx.Stack.Pop();

                        // Sub-page completed: pop back to parent, continue traversal
                        // The parent node will select its next unvisited child in a subsequent step.
                        result.FrameCompleted = false;
                        result.ChildPushed = false;
                        result.NextState = TraversalState.NodeSelect;
                    }
                    else
                    {
                        // 根节点且无法滚动：标记帧完成，让 RunAsync 检查终止条件
                        result.FrameCompleted = true;
                        result.ChildPushed = false;
                        result.NextState = TraversalState.NodeSelect;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Step 10: FRAME_COMPLETE interception override — DynamicMatch 仍有未访问子节点时
    /// 覆盖为 NodeSelect 并推子节点; 否则放行 FrameComplete。
    /// </summary>
    public InterceptionResult OnFrameComplete(StepContext ctx)
    {
        var result = new InterceptionResult(TraversalState.FrameComplete, false, false, false);

        var currentFrame = ctx.Context.CurrentFrame;
        if (currentFrame == null)
            return result;

        var nextChild = ctx.ChildMgr.GetNextUnvisitedChild(
            FromFrame(currentFrame), ctx.Context);

        if (nextChild != null)
        {
            // Override: push remaining child instead of completing frame
            result.FrameOverrideTriggered = true;
            result.ChildPushed = true;
            result.FrameCompleted = false;
            ctx.Stack.Push(nextChild);
            result.NextState = TraversalState.NodeSelect; // Override state
        }
        else
        {
            // No remaining children → proceed normally with FRAME_COMPLETE
            result.FrameCompleted = true;
        }

        return result;
    }

    /// <summary>
    /// D-74: 行为导航检测 — 比较缓存指纹与当前指纹, 若不同说明页面已变化 (导航)。
    /// 只在 TryHandleScroll 返回 false 后调用, 排除了滑动导致指纹变化的可能。
    /// </summary>
    /// <returns>true = 子页帧已推入; false = 未检测到导航, 走既有深度判断逻辑</returns>
    private bool TryHandleNavigation(
        StepContext ctx,
        ITraversalNode currentFrame,
        ref InterceptionResult result)
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

        _ = ctx.Trace.RecordDecisionAsync(
            $"navigation_detected_push_subframe:{navigatedChildNodeId}",
            ctx.Context);

        result.FrameCompleted = false;
        result.ChildPushed = true;
        // Don't override result.NextState — let RunAsync transition to NodeSelect via
        // StepOrchestrator's step 11. This prevents Step 9 from double-firing in the
        // same ExecuteStep call (D-74).
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

    /// <summary>
    /// 统一滚动处理 (滚动 = 操作 + 判断 模型, 见设计 §6):
    /// ① <see cref="IActionExecutor.SwipeAsync"/> (操作)
    /// ② <see cref="IVisionProvider.AnalyzeCurrentPageAsync"/> (对新截图的判断)
    /// ③ <see cref="IDynamicChildManager.Invalidate"/> (重新生成子节点)
    /// ④ per-frame seen 元素 id 集合差分: 滚出未见元素 → Continue; 全是已见/不可滚动 → Stop。
    /// 不下转 <see cref="IVisionProvider"/>/<see cref="IActionExecutor"/> 到 Simulation 具体类型 ——
    /// mock 与真实服务代码路径完全相同。
    /// 滑动坐标: <see cref="IVisionProvider.GetScrollSwipeConfig"/> (页面级) ?? <see cref="StepContext.ScrollSwipe"/> (引擎默认)。
    /// internal static 保留: ScrollLoopTerminationTests 直接契约测试 (design §5 修正)。
    /// </summary>
    /// <returns>
    /// (scrolled, frameCompleted, childPushed, nextState):
    /// scrolled=true = 滚动揭示了未见元素, 继续 NodeSelect;
    /// scrolled=false = 到底或不可滚动, 由调用方完成帧 (root → FrameComplete; 非 root → PressBack + Pop)。
    /// </returns>
    internal static async Task<(bool scrolled, bool frameCompleted, bool childPushed, TraversalState nextState)> TryHandleScrollAsync(
        StepContext ctx,
        ITraversalNode currentFrame)
    {
        // 不可滚动或已到底 → 不 swipe, 直接完成
        if (!ctx.Vision.HasScroll() || ctx.Vision.IsEndOfList())
            return (false, false, false, TraversalState.NodeSelect);

        // seed: 把滚动前页面元素记入 seen 集合 (首次调用建立 page-0 基线, 后续调用幂等)
        ctx.Context.RecordSeenElementIds(currentFrame.NodeId, GetElementIds(ctx.Context.CurrentPageAnalysis));

        // 滑动坐标: 页面级配置优先, 回退到引擎级默认, 再回退到硬编码默认
        var cfg = ctx.Vision.GetScrollSwipeConfig() ?? ctx.ScrollSwipe ?? new ScrollSwipeConfig();

        // ① 操作: 垂直 swipe (向下滚动发现更多内容)
        await ctx.Action.SwipeAsync(cfg.StartX, cfg.StartY, cfg.EndX, cfg.EndY, cfg.DurationMs);

        // ② 重新截图: 对操作后的新页面分析
        var after = await ctx.Vision.AnalyzeCurrentPageAsync();
        ctx.Context.SetCurrentPageAnalysis(after);

        // ③ 失效子节点缓存, 随后 NodeSelect 从新 PageAnalysis 重新生成/选择子节点
        ctx.ChildMgr.Invalidate(currentFrame.NodeId);

        // ④ 判断: seen-set 差分 —— 本次滚动后是否出现未见元素
        bool revealedNew = after != null
            && ctx.Context.RecordSeenElementIds(currentFrame.NodeId, GetElementIds(after));

        await ctx.Trace.RecordDecisionAsync(
            revealedNew ? "scroll_revealed_new_elements" : "scroll_no_new_elements_end_reached",
            ctx.Context);

        if (revealedNew)
        {
            // 有新内容 → 继续 NodeSelect (生成/选择新子节点)
            return (true, false, false, TraversalState.NodeSelect);
        }

        // 到底: 清理该帧 seen 集合, 由调用方完成帧
        ctx.Context.ClearSeenElementIds(currentFrame.NodeId);
        return (false, false, false, TraversalState.NodeSelect);
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
