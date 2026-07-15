namespace UniClaw.Core.Traversal;

/// <summary>
/// ScrollSwipeConfig — 滑动坐标配置 (归一化 0-1 + 持续时间 ms)。
/// 引擎级默认 + IVisionProvider 页面级覆盖。
/// 默认值 = v1 硬编码常量: (0.5, 0.7) → (0.5, 0.3), 300ms。
/// </summary>
public sealed record class ScrollSwipeConfig(
    double StartX = 0.5,
    double StartY = 0.7,
    double EndX = 0.5,
    double EndY = 0.3,
    int DurationMs = 300);
