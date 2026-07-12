namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 数值参考锚点 (D-E4: numeric_anchor 维度 — informational, 非 CI-blocking)。
/// 对照 TraversalResult 数值指标，±5% tolerance。
/// Extended with scroll-specific metrics for scroll-enabled baseline scenarios.
/// </summary>
/// <param name="TotalSteps">预期总执行步数</param>
/// <param name="VisitedPagesCount">预期已访问页面数量</param>
/// <param name="ActionHistoryCount">预期操作历史条目数</param>
/// <param name="ElapsedSecondsMax">预期最大耗时 (秒)</param>
/// <param name="ScrollCount">向下滚动次数 (scroll scenarios)</param>
/// <param name="ScrollDistance">总滚动距离 0.0-1.0 (scroll scenarios)</param>
/// <param name="ScrollUpCount">向上滚动次数 (scroll scenarios)</param>
/// <param name="JumpDetected">检测到跳跃次数 (scroll scenarios)</param>
/// <param name="JumpRecovered">成功恢复跳跃次数 (scroll scenarios)</param>
/// <param name="FinalProgress">最终进度 0.0-1.0 (scroll scenarios)</param>
/// <param name="AdaptiveStepIncreases">步长增长次数 (scroll scenarios)</param>
public sealed record class NumericAnchor(
    int TotalSteps,
    int VisitedPagesCount,
    int ActionHistoryCount,
    double ElapsedSecondsMax,
    int ScrollCount = 0,
    double ScrollDistance = 0.0,
    int ScrollUpCount = 0,
    int JumpDetected = 0,
    int JumpRecovered = 0,
    double FinalProgress = 0.0,
    int AdaptiveStepIncreases = 0);
