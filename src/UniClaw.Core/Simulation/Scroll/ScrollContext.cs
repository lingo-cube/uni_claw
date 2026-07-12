namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动上下文：捕获滚动决策、动作类型、步长百分比和遍历上下文。
/// </summary>
public sealed record class ScrollContext
{
    /// <summary>滚动动作类型</summary>
    public ScrollActionType ActionType { get; init; }

    /// <summary>滚动步长百分比</summary>
    public double StepPercent { get; init; }

    /// <summary>当前滚动进度</summary>
    public double CurrentProgress { get; init; }

    /// <summary>最大阈值（用于边界计算）</summary>
    public double MaxThreshold { get; init; }

    /// <summary>是否为列表末尾</summary>
    public bool IsAtBottom { get; init; }

    /// <summary>是否有滚动数据</summary>
    public bool HasScroll { get; init; }

    /// <param name="ActionType">滚动动作类型</param>
    /// <param name="StepPercent">滚动步长百分比</param>
    /// <param name="CurrentProgress">当前滚动进度</param>
    /// <param name="MaxThreshold">最大阈值</param>
    /// <param name="IsAtBottom">是否为列表末尾</param>
    /// <param name="HasScroll">是否有滚动数据</param>
    public ScrollContext(
        ScrollActionType ActionType,
        double StepPercent,
        double CurrentProgress,
        double MaxThreshold,
        bool IsAtBottom,
        bool HasScroll)
    {
        this.ActionType = ActionType;
        this.StepPercent = StepPercent;
        this.CurrentProgress = CurrentProgress;
        this.MaxThreshold = MaxThreshold;
        this.IsAtBottom = IsAtBottom;
        this.HasScroll = HasScroll;
    }

    /// <summary>创建无需滚动的上下文</summary>
    public static ScrollContext NoScroll() =>
        new ScrollContext(
            ActionType: ScrollActionType.None,
            StepPercent: 0.0,
            CurrentProgress: 0.0,
            MaxThreshold: 1.0,
            IsAtBottom: false,
            HasScroll: false);

    /// <summary>创建 ScrollDown 上下文</summary>
    public static ScrollContext ScrollDown(double step, double current, double max) =>
        new ScrollContext(
            ActionType: ScrollActionType.ScrollDown,
            StepPercent: step,
            CurrentProgress: current,
            MaxThreshold: max,
            IsAtBottom: false,
            HasScroll: true);
}
