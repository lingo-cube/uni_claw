using System.Collections.Immutable;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 支持滚动的 Mock Action Executor。
/// 扩展 StatefulMockActionExecutor，添加 ScrollDown 和 ScrollUp 操作。
/// </summary>
public sealed class ScrollableMockActionExecutor : IActionExecutor
{
    private readonly ScrollableMockVisionService _vision;
    private readonly List<ActionRecord> _history;
    private readonly ImmutableArray<ScrollAction>.Builder _scrollHistory;

    /// <summary>获取滚动操作历史记录</summary>
    public ImmutableArray<ScrollAction> ScrollHistory => _scrollHistory.ToImmutable();

    /// <summary>
    /// 创建 ScrollableMockActionExecutor
    /// </summary>
    /// <param name="vision">关联的 ScrollableMockVisionService</param>
    public ScrollableMockActionExecutor(ScrollableMockVisionService vision)
    {
        _vision = vision;
        _history = new List<ActionRecord>();
        _scrollHistory = ImmutableArray.CreateBuilder<ScrollAction>();
    }

    // ── IActionExecutor 实现 ──────────────────────────

    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
    {
        var element = _vision.FindElementAt(x, y);
        if (element != null)
            _vision.SimulateAction(element.Id, "click");

        _history.Add(new ActionRecord("tap", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["element_id"] = element?.Id ?? "none" },
            element != null));
        return Task.FromResult(element != null);
    }

    public Task<bool> PressBackAsync(CancellationToken ct = default)
    {
        var ok = _vision.NavigateBack();
        _history.Add(new ActionRecord("back", DateTimeOffset.UtcNow, new(), ok));
        return Task.FromResult(ok);
    }

    public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey,
        int durationMs, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("swipe", DateTimeOffset.UtcNow,
            new() { ["sx"] = sx, ["sy"] = sy, ["ex"] = ex, ["ey"] = ey, ["duration_ms"] = durationMs },
            true));
        return Task.FromResult(true);
    }

    public Task<bool> InputTextAsync(string text, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("input_text", DateTimeOffset.UtcNow,
            new() { ["text"] = text }, true));
        return Task.FromResult(true);
    }

    public Task<bool> LongPressAsync(double x, double y, int durationMs,
        CancellationToken ct = default)
    {
        var element = _vision.FindElementAt(x, y);
        _history.Add(new ActionRecord("long_press", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["duration_ms"] = durationMs, ["element_id"] = element?.Id ?? "none" },
            element != null));
        return Task.FromResult(element != null);
    }

    public Task WaitAsync(int milliseconds, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("wait", DateTimeOffset.UtcNow,
            new() { ["duration_ms"] = milliseconds }, true));
        return Task.CompletedTask;
    }

    // ── 滚动操作 ──────────────────────────────────────

    /// <summary>向下滚动</summary>
    /// <param name="stepPercent">滚动步长百分比（0-1）</param>
    /// <returns>是否成功执行</returns>
    public bool ScrollDown(double stepPercent)
    {
        if (stepPercent < 0.0 || stepPercent > 1.0)
            return false;

        var beforeProgress = _vision.GetScrollProgress(_vision.CurrentPageId);
        var afterProgress = _vision.SimulateScroll(stepPercent);

        var action = ScrollAction.ScrollDown(stepPercent, beforeProgress, afterProgress);
        _scrollHistory.Add(action);

        _history.Add(new ActionRecord("scroll_down", DateTimeOffset.UtcNow,
            new() { ["step_percent"] = stepPercent, ["before_progress"] = beforeProgress, ["after_progress"] = afterProgress },
            true));

        return true;
    }

    /// <summary>向上滚动</summary>
    /// <param name="stepPercent">滚动步长百分比（0-1）</param>
    /// <returns>是否成功执行</returns>
    public bool ScrollUp(double stepPercent)
    {
        if (stepPercent < 0.0 || stepPercent > 1.0)
            return false;

        var beforeProgress = _vision.GetScrollProgress(_vision.CurrentPageId);
        var afterProgress = _vision.SimulateScroll(-stepPercent);

        var action = ScrollAction.ScrollUp(stepPercent, beforeProgress, afterProgress);
        _scrollHistory.Add(action);

        _history.Add(new ActionRecord("scroll_up", DateTimeOffset.UtcNow,
            new() { ["step_percent"] = stepPercent, ["before_progress"] = beforeProgress, ["after_progress"] = afterProgress },
            true));

        return true;
    }

    /// <summary>获取通用操作历史记录</summary>
    public List<ActionRecord> GetHistory() => _history;

    /// <summary>获取页面滚动操作次数</summary>
    public int GetScrollCount(string pageId)
    {
        return ScrollHistory.Count(s => s.Timestamp > DateTimeOffset.MinValue); // 所有滚动操作都算
    }

    /// <summary>获取向上滚动次数</summary>
    public int GetScrollUpCount()
    {
        return ScrollHistory.Count(s => s.Action == ScrollActionType.ScrollUp);
    }
}
