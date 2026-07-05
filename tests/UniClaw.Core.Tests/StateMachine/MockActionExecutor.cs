using UniClaw.Core.Traversal;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// Mock IActionExecutor for handler testing.
/// Configurable NextResult / ThrowsOnNext per method; CallLog records all invocations.
/// </summary>
public sealed class MockActionExecutor : IActionExecutor
{
    /// <summary>Default return value for all methods when no per-method override is set</summary>
    public bool NextResult { get; set; } = true;

    /// <summary>If set, the next call throws this exception instead of returning NextResult</summary>
    public Exception? ThrowsOnNext { get; set; }

    /// <summary>Records all invocations with parameters and results</summary>
    public List<ActionRecord> CallLog { get; } = new();

    public Task<bool> TapAsync(double x, double y, CancellationToken cancellationToken = default)
    {
        RecordCall("tap", new() { ["x"] = x, ["y"] = y });
        return ExecuteAsync();
    }

    public Task<bool> SwipeAsync(
        double startX, double startY,
        double endX, double endY,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        RecordCall("swipe", new()
        {
            ["start_x"] = startX, ["start_y"] = startY,
            ["end_x"] = endX, ["end_y"] = endY,
            ["duration_ms"] = durationMs
        });
        return ExecuteAsync();
    }

    public Task<bool> PressBackAsync(CancellationToken cancellationToken = default)
    {
        RecordCall("back", new());
        return ExecuteAsync();
    }

    public Task<bool> InputTextAsync(string text, CancellationToken cancellationToken = default)
    {
        RecordCall("input_text", new() { ["text"] = text });
        return ExecuteAsync();
    }

    public Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken cancellationToken = default)
    {
        RecordCall("long_press", new() { ["x"] = x, ["y"] = y, ["duration_ms"] = durationMs });
        return ExecuteAsync();
    }

    public Task WaitAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        RecordCall("wait", new() { ["duration_ms"] = milliseconds });
        return Task.CompletedTask;
    }

    public List<ActionRecord> GetHistory() => CallLog;

    private void RecordCall(string action, Dictionary<string, object> parameters)
    {
        if (ThrowsOnNext != null)
        {
            CallLog.Add(new ActionRecord(action, DateTimeOffset.UtcNow, parameters, false));
            throw ThrowsOnNext;
        }
        CallLog.Add(new ActionRecord(action, DateTimeOffset.UtcNow, parameters, NextResult));
    }

    private Task<bool> ExecuteAsync()
    {
        if (ThrowsOnNext != null)
            throw ThrowsOnNext;
        return Task.FromResult(NextResult);
    }
}
