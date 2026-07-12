namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动操作记录：捕获单次滚动的详细信息。
/// </summary>
public sealed record class ScrollAction
{
    /// <summary>操作类型</summary>
    public ScrollActionType Action { get; init; }

    /// <summary>滚动步长百分比（正数表示向下，负数表示向上）</summary>
    public double StepPercent { get; init; }

    /// <summary>滚动前进度</summary>
    public double BeforeProgress { get; init; }

    /// <summary>滚动后进度</summary>
    public double AfterProgress { get; init; }

    /// <summary>操作时间戳（UTC）</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <param name="Action">操作类型</param>
    /// <param name="StepPercent">滚动步长百分比</param>
    /// <param name="BeforeProgress">滚动前进度</param>
    /// <param name="AfterProgress">滚动后进度</param>
    /// <param name="Timestamp">操作时间戳（UTC），默认为当前时间</param>
    public ScrollAction(
        ScrollActionType Action,
        double StepPercent,
        double BeforeProgress,
        double AfterProgress,
        DateTimeOffset? Timestamp = null)
    {
        this.Action = Action;
        this.StepPercent = StepPercent;
        this.BeforeProgress = BeforeProgress;
        this.AfterProgress = AfterProgress;
        this.Timestamp = Timestamp ?? DateTimeOffset.UtcNow;
    }

    /// <summary>创建 ScrollDown 操作记录</summary>
    public static ScrollAction ScrollDown(double step, double before, double after) =>
        new(ScrollActionType.ScrollDown, step, before, after);

    /// <summary>创建 ScrollUp 操作记录</summary>
    public static ScrollAction ScrollUp(double step, double before, double after) =>
        new(ScrollActionType.ScrollUp, -step, before, after);
}

/// <summary>滚动操作类型</summary>
public enum ScrollActionType
{
    /// <summary>向下滚动</summary>
    ScrollDown,

    /// <summary>向上滚动</summary>
    ScrollUp,

    /// <summary>无操作</summary>
    None
}
