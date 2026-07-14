using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 支持滚动的 Mock Action Executor — 薄适配器 (见设计 §5): 持有共享 <see cref="SimulatedScreen"/>
/// (非 <see cref="ScrollableMockVisionService"/> 具体类型)。SwipeAsync 委托 <see cref="SimulatedScreen.ApplySwipe"/>
/// 并追加 ActionRecord; 滚动走标准 <see cref="IActionExecutor.SwipeAsync"/> (不再有 ScrollDown/ScrollUp/ScrollHistory)。
/// </summary>
public sealed class ScrollableMockActionExecutor : IActionExecutor
{
    private readonly SimulatedScreen _screen;
    private readonly List<ActionRecord> _history;

    /// <summary>共享模拟屏幕</summary>
    public SimulatedScreen Screen => _screen;

    /// <summary>创建 ScrollableMockActionExecutor</summary>
    /// <param name="screen">与视觉服务共享的 <see cref="SimulatedScreen"/></param>
    public ScrollableMockActionExecutor(SimulatedScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        _history = new List<ActionRecord>();
    }

    // ── IActionExecutor 实现 ──────────────────────────

    /// <inheritdoc />
    public Task<bool> TapAsync(double x, double y, CancellationToken ct = default)
    {
        var element = _screen.FindElementAt(x, y);
        if (element != null)
            _screen.SimulateAction(element.Id, "click");

        _history.Add(new ActionRecord("tap", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["element_id"] = element?.Id ?? "none" },
            element != null));
        return Task.FromResult(element != null);
    }

    /// <inheritdoc />
    public Task<bool> PressBackAsync(CancellationToken ct = default)
    {
        var ok = _screen.NavigateBack();
        _history.Add(new ActionRecord("back", DateTimeOffset.UtcNow, new(), ok));
        return Task.FromResult(ok);
    }

    /// <summary>
    /// Swipe = 滚动操作: 委托 <see cref="SimulatedScreen.ApplySwipe"/> 推进视口, 记录带方向与进度差的 ActionRecord。
    /// 方向由 swipe 坐标判定 (sy&gt;ey = 向下发现更多, sy&lt;ey = 向上回顶)。
    /// </summary>
    public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey,
        int durationMs, CancellationToken ct = default)
    {
        double before = _screen.GetScrollProgress();
        _screen.ApplySwipe(sx, sy, ex, ey);
        double after = _screen.GetScrollProgress();

        _history.Add(new ActionRecord("swipe", DateTimeOffset.UtcNow,
            new()
            {
                ["sx"] = sx, ["sy"] = sy, ["ex"] = ex, ["ey"] = ey,
                ["duration_ms"] = durationMs,
                ["direction"] = sy > ey ? "down" : "up",
                ["before_progress"] = before,
                ["after_progress"] = after
            },
            true));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> InputTextAsync(string text, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("input_text", DateTimeOffset.UtcNow,
            new() { ["text"] = text }, true));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> LongPressAsync(double x, double y, int durationMs,
        CancellationToken ct = default)
    {
        var element = _screen.FindElementAt(x, y);
        _history.Add(new ActionRecord("long_press", DateTimeOffset.UtcNow,
            new() { ["x"] = x, ["y"] = y, ["duration_ms"] = durationMs, ["element_id"] = element?.Id ?? "none" },
            element != null));
        return Task.FromResult(element != null);
    }

    /// <inheritdoc />
    public Task WaitAsync(int milliseconds, CancellationToken ct = default)
    {
        _history.Add(new ActionRecord("wait", DateTimeOffset.UtcNow,
            new() { ["duration_ms"] = milliseconds }, true));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>滚动指标 (ScrollCount/ScrollUpCount) 由基线收集器从此历史按方向统计 (见 baseline-scroll-metrics)。</remarks>
    public List<ActionRecord> GetHistory() => _history;
}
