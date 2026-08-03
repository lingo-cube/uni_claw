using System.Collections.Concurrent;
using System.Collections.Generic;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// InterceptionHandler — StepOrchestrator 步骤 8-10 的 FSM 拦截/覆盖逻辑 (D-IV 分解, 方案 A)。
/// 拥有全部 override 决策: Branch 拦截、DynamicMatch 子节点解析 (导航/滚动/PressBack)、
/// FrameComplete 覆盖, 以及 helper (TryHandleNavigation, TryHandleScrollAsync, FromFrame, GetElementIds)。
/// 所有依赖来自 StepContext, 零引用 StepOrchestrator。
/// 容器完成判定委托给 ContainerHandler (sole authority); 只保留事件检测。
/// </summary>
public sealed class InterceptionHandler : IInterceptionHandler
{
    /// <summary>Tracks consecutive empty-scroll-diff counts per frame for R-12 retry logic.</summary>
    private static readonly ConcurrentDictionary<string, int> EmptyScrollRetries = new();

    /// <summary>
    /// 追踪最后一个被推入栈的子节点 NodeId, 用于行为导航检测。
    /// 当该子节点执行 (tap) 导致页面指纹变化时, 以此 NodeId 为归属创建子页帧。
    /// </summary>
    private string? _lastPushedChildNodeId;

    /// <summary>
    /// ContainerHandler — 容器完成判定唯一权威 (3-subcomponent pipeline)。
    /// </summary>
    private readonly ContainerHandler _containerHandler;

    /// <summary>
    /// 构造 InterceptionHandler — 注入 ContainerHandler 或默认构造。
    /// </summary>
    public InterceptionHandler(ContainerHandler? containerHandler = null)
    {
        _containerHandler = containerHandler ?? new ContainerHandler();
    }

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

            // D-134 P2: entry.visited — records the push of an unvisited child entry.
            // Parent = the current engine.step TraceSpan (via TraceCoordinator passthrough).
            await RecordEntryVisitedAsync(ctx, nextChild);
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
                    // 无法导航、无法滚动或已到底部 → delegate to ContainerHandler
                    var (frameDone, childAdded, nextSt) = await DecideFrameCompletionAsync(ctx, currentFrame, canContinue: false);
                    result.FrameCompleted = frameDone;
                    result.ChildPushed = childAdded;
                    result.NextState = nextSt;
                }
            }
        }
        else
        {
            // Static children exhausted → delegate to ContainerHandler
            var (frameDone, childAdded, nextSt) = await DecideFrameCompletionAsync(ctx, currentFrame, canContinue: false);
            result.FrameCompleted = frameDone;
            result.ChildPushed = childAdded;
            result.NextState = nextSt;
        }

        return result;
    }

    /// <summary>
    /// D-134 P2: entry.visited — emit an entry.visited event TraceSpan when an unvisited child is
    /// pushed onto the stack. Parent is the current engine.step TraceSpan (via TraceCoordinator),
    /// read at runtime. M4 5.5: recorded as a point-in-time event via RecordEventAsync — opened
    /// with spanName == spanType and left unclosed (EndTime null), same as the manual open it
    /// replaces. Attributes: entry.name, entry.node_id, entry.step, entry.depth.
    /// </summary>
    private static async Task RecordEntryVisitedAsync(StepContext ctx, ITraversalNode child)
    {
        if (ctx.Trace is not TraceCoordinator tc) return;
        var parentSpanId = ctx.Trace.CurrentEngineStepSpanId;
        // D-134 §3.4: entry.name 与 entry.observed 一致 — 用匹配到的 item 名
        // (operation target 值)；动态节点 child.Name 是模板名（menu_container），非 item 名。
        // ITraversalNode 无 Operation 成员，仅具体 TraversalNode 携带。
        var itemName = child is TraversalNode tn
            ? tn.Operation.Target?.Value.ToString()
            : null;
        await tc.Recorder.RecordEventAsync(SpanTypes.EntryVisited, parentSpanId,
            new Dictionary<string, object>
            {
                [TraceFields.EntryName] = string.IsNullOrEmpty(itemName) ? child.Name : itemName,
                [TraceFields.EntryNodeId] = child.NodeId,
                [TraceFields.EntryStep] = ctx.Context.StepCount,
                [TraceFields.EntryDepth] = ctx.Context.NodeStack.Depth,
            },
            // trace-parent-linkage M2: EntryVisited profile（Basic: name；Extended: node_id/step/depth）。
            // StepContext 无 plan/EntryConfig 通道，level 保持缺省 Detailed（= 现状全量行为）。
            profile: TraceSpanFields.EntryVisited);
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
                        // D-90: 非 root 子节点耗尽时, 比较父帧指纹与当前页面指纹决定 Pop-only vs PressBack+Pop
                        // 父帧指纹 == 当前页面 → Pop-only (父帧页面与当前物理页面相同, Pop 后可继续访问父帧子节点)
                        // 爹帧指纹 != 当前页面 → PressBack+Pop (物理页面是子页, 需回退到父帧页面)
                        var parentFrame = ctx.Context.NodeStack.Peek(1); // offset 1 = parent frame (second from top)
                        var parentCachedFingerprint = parentFrame != null
                            ? ctx.ChildMgr.GetCachedFingerprint(parentFrame.NodeId)
                            : null;

                        var runtimeCtx = ctx.Context as TraversalRuntimeContext;
                        var currentFingerprint = ctx.SnapshotMgr.Fingerprint(runtimeCtx?.CurrentPageAnalysis);

                        if (parentCachedFingerprint != null && parentCachedFingerprint == currentFingerprint)
                        {
                            // 父帧页面 = 当前物理页面 → Pop-only (无 PressBack)
                            // Pop 后父帧成为栈顶, 其 DynamicMatch 缓存与当前页面匹配, 可继续访问剩余子节点
                            ctx.Stack.Pop();

                            // DfsBacktrack trace — pop_only_parent_frame_matches
                            if (ctx.HandlerTrace != null)
                            {
                                var traceCtx = ctx.Trace.BuildCorrelation();
                                var meta = new Dictionary<string, object> { ["backtrack_reason"] = "pop_only_parent_frame_matches" };
                                await ctx.HandlerTrace.RecordHandlerLifecycleAsync(
                                    "dfs_backtrack", SpanType.DfsBacktrack, "ok", meta, traceCtx);
                            }

                            result.FrameCompleted = false;
                            result.ChildPushed = false;
                            result.NextState = TraversalState.NodeSelect;
                        }
                        else
                        {
                            // 父帧页面 ≠ 当前物理页面 (或无缓存) → PressBack+Pop
                            // 物理回退到父帧页面, Pop 使父帧成为栈顶。
                            await ctx.Action.PressBackAsync();

                            // D-G6: After PressBack, wait for the Android page transition
                            // animation to complete.  If we analyze the page too soon the AI
                            // may capture a mid-transition screenshot, causing D-74 to
                            // incorrectly detect a navigation on the next step.
                            await ctx.Action.WaitAsync(1500);
                            var stabilizedAnalysis = await ctx.Brain.PageAnalyzer.AnalyzeCurrentPageAsync();
                            runtimeCtx?.SetCurrentPageAnalysis(stabilizedAnalysis);

                            ctx.Stack.Pop();

                            // DfsBacktrack trace — press_back_parent_frame_differs
                            if (ctx.HandlerTrace != null)
                            {
                                var traceCtx = ctx.Trace.BuildCorrelation();
                                var meta = new Dictionary<string, object> { ["backtrack_reason"] = "press_back_parent_frame_differs" };
                                await ctx.HandlerTrace.RecordHandlerLifecycleAsync(
                                    "dfs_backtrack", SpanType.DfsBacktrack, "ok", meta, traceCtx);
                            }

                            // PageTransition — press_back
                            if (parentFrame?.NodeId != null)
                            {
                                await ctx.Trace.RecordPageTransitionAsync(
                                    currentFrame.NodeId, parentFrame.NodeId, "press_back");
                            }

                            result.FrameCompleted = false;
                            result.ChildPushed = false;
                            result.NextState = TraversalState.NodeSelect;
                        }
                    }
                    else
                    {
                        // 根节点且无法滚动：委托 ContainerHandler 判定帧完成
                        var (frameDone, childAdded, nextSt) = await DecideFrameCompletionAsync(ctx, currentFrame, canContinue: false);
                        result.FrameCompleted = frameDone;
                        result.ChildPushed = childAdded;
                        result.NextState = nextSt;
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
    public async Task<InterceptionResult> OnFrameComplete(StepContext ctx)
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
            // No remaining children → delegate to ContainerHandler for completion decision
            var (frameDone, childAdded, nextSt) = await DecideFrameCompletionAsync(ctx, currentFrame, canContinue: false);
            result.FrameCompleted = frameDone;
            result.ChildPushed = childAdded;
            result.NextState = nextSt;
        }

        return result;
    }

    /// <summary>
    /// 构建 CompletionContext 并从 ContainerHandler 获取容器完成判定。
    /// ContainerHandler 是容器完成唯一权威；Back/AutoEscape/Skip → FrameCompleted=true; Abort → FrameCompleted=false。
    /// </summary>
    private async Task<(bool frameCompleted, bool childPushed, TraversalState nextState)> DecideFrameCompletionAsync(
        StepContext ctx, ITraversalNode currentFrame, bool canContinue)
    {
        // Compute TotalChildren from children strategy
        int totalChildren = currentFrame.ChildrenStrategy.Type switch
        {
            ChildrenStrategyType.Static => currentFrame.ChildrenStrategy.StaticChildren?.Count ?? 0,
            ChildrenStrategyType.DynamicMatch => ctx.ChildMgr.GetCachedChildCount(currentFrame.NodeId),
            _ => 0
        };

        // Compute VisitedChildCount from VisitedNodes ∩ children
        int visitedChildCount = 0;
        if (currentFrame.ChildrenStrategy.Type == ChildrenStrategyType.DynamicMatch)
        {
            // For dynamic children, we count how many cached children are in VisitedNodes
            // (approximation: we use total - remaining unvisited)
            var next = ctx.ChildMgr.GetNextUnvisitedChild(
                FromFrame(currentFrame), ctx.Context);
            visitedChildCount = next == null ? totalChildren : Math.Max(0, totalChildren - 1);
        }

        var completionCtx = new CompletionContext(
            ElapsedMs: 0,                               // engine-level timeout handled separately
            TimeoutMs: 300_000,                          // 5 min default (won't trigger at container level)
            CurrentDepth: ctx.Context.NodeStack.Depth,
            MaxDepth: ctx.EffectiveMaxDepth,
            TotalChildren: totalChildren,
            VisitedChildCount: visitedChildCount);

        var result = await _containerHandler.HandleContainerTracedAsync(
            completionCtx, canContinue, currentFrame.NodeId, ctx.Context,
            handlerTrace: ctx.HandlerTrace,
            trace: ctx.Trace);

        // Translate ContainerActionResult → FrameCompleted
        bool frameCompleted = result.Action != FallbackAction.Abort;
        bool childPushed = false;
        var nextState = TraversalState.NodeSelect;

        return (frameCompleted, childPushed, nextState);
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
        // D-G7: When the plan specifies a MaxDepth, do not push subframes
        // beyond that depth.  This prevents the engine from exploring sub-page
        // children during shallow-sampling modes (e.g. enumerate_first_level).
        if (runtimeCtx != null
            && ctx.EffectiveMaxDepth > 0
            && runtimeCtx.NodeStack.Depth >= ctx.EffectiveMaxDepth)
        {
            return false;
        }

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
            Meta: new Dictionary<string, object>
            {
                ["is_nav_subframe"] = true,
                ["fallback_action"] = "auto_escape"
            });

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
        if (!ctx.ScreenState.HasScroll() || ctx.ScreenState.IsEndOfList())
            return (false, false, false, TraversalState.NodeSelect);

        // seed: 把滚动前页面元素记入 seen 集合 (首次调用建立 page-0 基线, 后续调用幂等)
        ctx.Context.RecordSeenElementIds(currentFrame.NodeId, GetElementIds(ctx.Context.CurrentPageAnalysis));

        // 滑动坐标: 页面级配置优先, 回退到引擎级默认, 再回退到硬编码默认
        var cfg = ctx.ScreenState.GetScrollSwipeConfig() ?? ctx.ScrollSwipe ?? new ScrollSwipeConfig();

        // ── Fingerprint-gated fast path (D5): one UIAutomator dump before swipe,
        // one after; if the hierarchy fingerprint hasn't changed the swipe didn't
        // reveal new content and we skip the expensive AI visual analysis.
        ScreenStateResult? preSwipe = null;
        if (ctx.ScreenState is IObservableScreenStateProvider observable)
        {
            preSwipe = await observable.RefreshAsync();
        }

        // ① 操作: 垂直 swipe (向下滚动发现更多内容)
        await ctx.Action.SwipeAsync(cfg.StartX, cfg.StartY, cfg.EndX, cfg.EndY, cfg.DurationMs);

        if (preSwipe is not null
            && !string.IsNullOrWhiteSpace(preSwipe.HierarchyXml)
            && ctx.ScreenState is IObservableScreenStateProvider observableAfter)
        {
            var postSwipe = await observableAfter.RefreshAsync(
                previousHierarchyXml: preSwipe.HierarchyXml,
                afterScroll: true);
            if (postSwipe.IsEndOfList)
            {
                ctx.Context.ClearSeenElementIds(currentFrame.NodeId);
                await ctx.Trace.RecordDecisionAsync(
                    "scroll_fingerprint_unchanged_end_reached",
                    ctx.Context);
                return (false, false, false, TraversalState.NodeSelect);
            }
        }

        // ② 重新截图: 对操作后的新页面分析
        var after = await ctx.Brain.PageAnalyzer.AnalyzeCurrentPageAsync();
        ctx.Context.SetCurrentPageAnalysis(after);

        // ③ 失效子节点缓存, 随后 NodeSelect 从新 PageAnalysis 重新生成/选择子节点
        ctx.ChildMgr.Invalidate(currentFrame.NodeId);

        // ④ 判断: seen-set 差分 —— 本次滚动后是否出现未见元素
        bool revealedNew = after != null
            && ctx.Context.RecordSeenElementIds(currentFrame.NodeId, GetElementIds(after));

        if (revealedNew)
        {
            // 有新内容 → 重置重试计数, 继续 NodeSelect (生成/选择新子节点)
            EmptyScrollRetries.TryRemove(currentFrame.NodeId, out _);
            await ctx.Trace.RecordDecisionAsync("scroll_revealed_new_elements", ctx.Context);
            return (true, false, false, TraversalState.NodeSelect);
        }

        // 无新元素 → 检查重试计数 (R-12: 连续 N 次差分无新增才到底)
        int retries = EmptyScrollRetries.GetOrAdd(currentFrame.NodeId, 0);
        int maxRetries = cfg.MaxEmptyScrollRetries;
        if (retries < maxRetries)
        {
            EmptyScrollRetries[currentFrame.NodeId] = retries + 1;
            await ctx.Trace.RecordDecisionAsync(
                $"scroll_empty_retry_{retries + 1}_of_{maxRetries + 1}", ctx.Context);
            // 返回 scrolled=true 触发重试 (空帧不消耗预算)
            return (true, false, false, TraversalState.NodeSelect);
        }

        // 到底: 清理该帧 seen 集合与重试计数, 由调用方完成帧
        EmptyScrollRetries.TryRemove(currentFrame.NodeId, out _);
        ctx.Context.ClearSeenElementIds(currentFrame.NodeId);
        await ctx.Trace.RecordDecisionAsync("scroll_no_new_elements_end_reached", ctx.Context);
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
