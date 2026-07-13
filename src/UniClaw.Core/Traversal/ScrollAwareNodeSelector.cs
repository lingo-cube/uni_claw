using System.Collections.Immutable;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.StateMachine;
using UniClaw.Core.StateMachine.Scroll;

namespace UniClaw.Core.Traversal;

/// <summary>
/// 滚动感知的节点选择器 — 在动态子节点选择过程中集成滚动逻辑。
/// 当没有更多未访问子节点时，检查是否可以滚动以发现新元素。
///
/// 工作流程：
/// 1. 获取当前可见的未访问子节点
/// 2. 如果没有，检查页面是否有滚动数据
/// 3. 如果有滚动数据且未到底部，执行滚动操作
/// 4. 滚动后重新生成动态子节点
/// 5. 返回新发现的子节点或 null（如果真的到底部）
/// </summary>
public sealed class ScrollAwareNodeSelector
{
    private readonly ScrollableMockVisionService _scrollableVision;
    private readonly ScrollableMockActionExecutor _scrollableAction;
    private readonly ScrollHandler _scrollHandler;
    private readonly DynamicChildManager _childManager;
    private readonly ITraceCoordinator? _trace;

    /// <summary>
    /// 当前滚动统计
    /// </summary>
    public ScrollStatisticsCollector Statistics => _scrollHandler.Statistics;

    /// <summary>
    /// 是否执行了滚动操作
    /// </summary>
    public bool HasScrolled { get; private set; }

    /// <summary>
    /// 创建滚动感知节点选择器
    /// </summary>
    public ScrollAwareNodeSelector(
        ScrollableMockVisionService scrollableVision,
        ScrollableMockActionExecutor scrollableAction,
        DynamicChildManager childManager,
        ITraceCoordinator? trace = null)
    {
        _scrollableVision = scrollableVision;
        _scrollableAction = scrollableAction;
        _childManager = childManager;

        // 设置滚动处理器
        _scrollHandler = new ScrollHandler(ScrollHandlerConfig.Default());

        // 注册滚动动作处理器
        _scrollHandler.RegisterActionHandler(ScrollActionType.ScrollDown, ExecuteScrollDown);
        _scrollHandler.RegisterActionHandler(ScrollActionType.ScrollUp, ExecuteScrollUp);

        _trace = trace;
    }

    /// <summary>
    /// 获取下一个未访问子节点，如果需要则滚动以发现新元素。
    /// </summary>
    /// <param name="node">当前遍历节点</param>
    /// <param name="context">遍历上下文</param>
    /// <returns>下一个未访问子节点，如果真的没有更多则返回 null</returns>
    public TraversalNode? GetNextUnvisitedChildWithScroll(
        TraversalNode node,
        ITraversalContext context)
    {
        HasScrolled = false;

        // 首先尝试获取当前可见的未访问子节点
        var existingChild = _childManager.GetNextUnvisitedChild(node, context);
        if (existingChild != null)
        {
            return existingChild;
        }

        // 没有更多子节点，检查是否可以滚动
        if (!CanScroll(context))
        {
            return null; // 无法滚动，真正完成
        }

        // 执行滚动以发现新元素
        var scrollResult = ExecuteScroll(context);
        if (!scrollResult.Success)
        {
            // 滚动失败，可能已到底部
            _trace?.RecordDecision("scroll_failed_bottom", context);
            return null;
        }

        HasScrolled = true;

        // 记录滚动事件
        _trace?.RecordDecision($"scroll_to_{scrollResult.NewProgress:F2}", context);

        // 滚动后重新生成动态子节点
        _childManager.Generate(node, context);

        // 再次尝试获取未访问子节点
        return _childManager.GetNextUnvisitedChild(node, context);
    }

    /// <summary>
    /// 检查是否可以滚动
    /// </summary>
    private bool CanScroll(ITraversalContext context)
    {
        var currentPageId = _scrollableVision.CurrentPageId;

        // 检查是否有滚动数据
        if (!_scrollableVision.HasScroll)
        {
            return false;
        }

        // 检查是否已到达列表末尾
        if (_scrollableVision.IsEndOfList)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 执行滚动操作
    /// </summary>
    private ScrollActionResult ExecuteScroll(ITraversalContext context)
    {
        var currentPageId = _scrollableVision.CurrentPageId;
        var currentProgress = _scrollableVision.GetScrollProgress(currentPageId);
        var maxThreshold = _scrollableVision.GetMaxThreshold(currentPageId);

        // 获取当前可见元素 ID（用于检测跳跃）
        var currentAnalysis = GetCurrentPageAnalysis();
        var beforeElementIds = currentAnalysis?.Items
            .Select(i => i.Name ?? "")
            .ToImmutableArray() ?? ImmutableArray<string>.Empty;

        // 调用滚动处理器
        return _scrollHandler.HandleScroll(
            hasScrollData: true,
            isEndOfList: false,
            currentProgress: currentProgress,
            maxThreshold: maxThreshold,
            beforeElementIds: beforeElementIds);
    }

    /// <summary>
    /// 获取当前页面分析
    /// </summary>
    private Domain.Models.Content.PageAnalysis? GetCurrentPageAnalysis()
    {
        // 如果 context 是 TraversalRuntimeContext，直接获取 CurrentPageAnalysis
        if (_trace is { } &&
            _trace is ITraceCoordinator trace &&
            trace.GetType().Name.Contains("TraceCoordinator"))
        {
            // 通过 reflection 或其他方式获取...
        }

        // 简化版本：返回 null（在 HandleScroll 中，beforeElementIds 可以为空）
        return null;
    }

    /// <summary>
    /// 向下滚动处理器
    /// </summary>
    private ScrollActionResult ExecuteScrollDown(double stepPercent)
    {
        var success = _scrollableAction.ScrollDown(stepPercent);
        var newProgress = _scrollableVision.GetScrollProgress(_scrollableVision.CurrentPageId);
        return ScrollActionResult.Succeeded(ScrollActionType.ScrollDown, newProgress, $"Scrolled down by {stepPercent:F2}");
    }

    /// <summary>
    /// 向上滚动处理器
    /// </summary>
    private ScrollActionResult ExecuteScrollUp(double stepPercent)
    {
        var success = _scrollableAction.ScrollUp(stepPercent);
        var newProgress = _scrollableVision.GetScrollProgress(_scrollableVision.CurrentPageId);
        return ScrollActionResult.Succeeded(ScrollActionType.ScrollUp, newProgress, $"Scrolled up by {stepPercent:F2}");
    }

    /// <summary>
    /// 重置滚动状态（用于测试或新页面）
    /// </summary>
    public void Reset()
    {
        HasScrolled = false;
    }
}
