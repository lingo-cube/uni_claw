namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 数值参考锚点 (D-E4: numeric_anchor 维度 — informational, 非 CI-blocking)。
/// 对照 TraversalResult 数值指标，±5% tolerance。
/// </summary>
/// <param name="TotalSteps">预期总执行步数</param>
/// <param name="VisitedPagesCount">预期已访问页面数量</param>
/// <param name="ActionHistoryCount">预期操作历史条目数</param>
/// <param name="ElapsedSecondsMax">预期最大耗时 (秒)</param>
public sealed record class NumericAnchor(
    int TotalSteps,
    int VisitedPagesCount,
    int ActionHistoryCount,
    double ElapsedSecondsMax);
