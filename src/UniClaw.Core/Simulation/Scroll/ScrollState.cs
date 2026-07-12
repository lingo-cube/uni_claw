using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动状态：跟踪当前滚动进度、滚动操作次数和历史进度记录。
/// 进度值在 [0.0, 1.0] 范围内，自动 clamp 到有效范围。
/// </summary>
public sealed record class ScrollState
{
    /// <summary>当前滚动进度 [0.0, 1.0]</summary>
    public double CurrentProgress { get; init; }

    /// <summary>已执行的滚动操作次数</summary>
    public int ScrollCount { get; init; }

    /// <summary>滚动历史记录，包含每次操作后的进度值</summary>
    public ImmutableArray<double> ScrollHistory { get; init; }

    /// <param name="CurrentProgress">当前滚动进度 [0.0, 1.0]</param>
    /// <param name="ScrollCount">已执行的滚动操作次数</param>
    /// <param name="ScrollHistory">滚动历史记录</param>
    public ScrollState(
        double CurrentProgress = 0.0,
        int ScrollCount = 0,
        ImmutableArray<double> ScrollHistory = default)
    {
        this.CurrentProgress = Clamp(CurrentProgress);
        this.ScrollCount = ScrollCount;
        this.ScrollHistory = ScrollHistory.IsDefault
            ? ImmutableArray<double>.Empty
            : ScrollHistory;
    }

    /// <summary>创建初始滚动状态（0 进度，0 次数，空历史）</summary>
    public static ScrollState Initial() => new();

    /// <summary>应用滚动增量，返回新状态</summary>
    public ScrollState ApplyDelta(double delta)
    {
        var newProgress = Clamp(CurrentProgress + delta);
        var newHistory = ScrollHistory.Add(CurrentProgress);
        return this with
        {
            CurrentProgress = newProgress,
            ScrollCount = ScrollCount + 1,
            ScrollHistory = newHistory
        };
    }

    /// <summary>直接设置进度（用于 jump recovery 回滚），返回新状态</summary>
    public ScrollState SetProgress(double progress)
    {
        var clampedProgress = Clamp(progress);
        var newHistory = ScrollHistory.Add(clampedProgress);
        return this with
        {
            CurrentProgress = clampedProgress,
            ScrollHistory = newHistory
        };
    }

    private static double Clamp(double value) =>
        value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
}
