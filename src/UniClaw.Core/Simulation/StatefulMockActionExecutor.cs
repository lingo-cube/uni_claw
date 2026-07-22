using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation;

/// <summary>
/// StatefulMockActionExecutor — 状态感知的 IActionExecutor 实现。
/// 联动 StatefulMockVisionService (IPageAnalyzer) 模拟页面跳转和导航。
/// TapAsync → FindElementAt → SimulateAction 链路。
/// </summary>
public sealed class StatefulMockActionExecutor : IActionExecutor
{
    private readonly StatefulMockVisionService _vision;
    private readonly List<ActionRecord> _history = new();

    public StatefulMockActionExecutor(StatefulMockVisionService vision)
    {
        _vision = vision;
    }

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

    public List<ActionRecord> GetHistory() => _history;
}
