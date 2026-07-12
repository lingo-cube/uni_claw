namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动动作执行结果：报告动作类型、成功标志、新进度和描述。
/// </summary>
public sealed record class ScrollActionResult
{
    /// <summary>执行的动作类型</summary>
    public ScrollActionType Action { get; init; }

    /// <summary>执行是否成功</summary>
    public bool Success { get; init; }

    /// <summary>执行后的新进度</summary>
    public double NewProgress { get; init; }

    /// <summary>结果描述</summary>
    public string Description { get; init; }

    /// <param name="Action">执行的动作类型</param>
    /// <param name="Success">执行是否成功</param>
    /// <param name="NewProgress">执行后的新进度</param>
    /// <param name="Description">结果描述</param>
    public ScrollActionResult(
        ScrollActionType Action,
        bool Success,
        double NewProgress,
        string Description)
    {
        this.Action = Action;
        this.Success = Success;
        this.NewProgress = NewProgress;
        this.Description = Description ?? string.Empty;
    }

    /// <summary>创建成功结果</summary>
    public static ScrollActionResult Succeeded(ScrollActionType action, double newProgress, string description) =>
        new ScrollActionResult(action, true, newProgress, description);

    /// <summary>创建失败结果</summary>
    public static ScrollActionResult Failed(ScrollActionType action, string reason) =>
        new ScrollActionResult(action, false, 0.0, $"Scroll failed: {reason}");

    /// <summary>创建跳过结果（无需滚动）</summary>
    public static ScrollActionResult Skipped(string reason) =>
        new ScrollActionResult(ScrollActionType.None, true, 0.0, $"Scroll skipped: {reason}");

    /// <summary>创建默认的 None 结果（无操作）</summary>
    public static ScrollActionResult DefaultNone() =>
        new ScrollActionResult(ScrollActionType.None, true, 0.0, "No scroll action performed.");
}
