using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 滚动能力枚举
/// </summary>
public enum Scrollability
{
    /// <summary>不可滚动（无滚动数据）</summary>
    NotScrollable,

    /// <summary>可以向下滚动</summary>
    CanScrollDown,

    /// <summary>已到达底部</summary>
    AtBottom,

    /// <summary>可以向上滚动</summary>
    CanScrollUp
}

/// <summary>
/// 步骤 1：滚动能力检测
/// 根据页面数据、末尾状态和当前进度判断滚动能力。
/// </summary>
public static class ScrollabilityDetector
{
    /// <summary>
    /// 检测滚动能力
    /// </summary>
    /// <param name="hasScrollData">是否有滚动数据</param>
    /// <param name="isEndOfList">是否到达列表末尾</param>
    /// <param name="currentProgress">当前滚动进度</param>
    /// <param name="config">滚动配置</param>
    /// <returns>滚动能力状态</returns>
    public static Scrollability Detect(
        bool hasScrollData,
        bool isEndOfList,
        double currentProgress,
        ScrollHandlerConfig config)
    {
        if (!hasScrollData)
            return Scrollability.NotScrollable;

        if (isEndOfList)
            return Scrollability.AtBottom;

        // 可以向上滚动（进度大于 0）
        if (currentProgress > config.ProgressEpsilon)
            return Scrollability.CanScrollDown;

        // 初始状态，可以向下滚动
        return Scrollability.CanScrollDown;
    }

    /// <summary>
    /// 检测滚动能力（完整版本，考虑向上滚动）
    /// </summary>
    public static Scrollability DetectFull(
        bool hasScrollData,
        bool isEndOfList,
        double currentProgress,
        ScrollHandlerConfig config)
    {
        if (!hasScrollData)
            return Scrollability.NotScrollable;

        if (isEndOfList)
        {
            // 到达底部但进度不为 0，可以向上滚动
            if (currentProgress > config.ProgressEpsilon)
                return Scrollability.CanScrollUp;
            return Scrollability.AtBottom;
        }

        // 进度大于 0，既可以向下也可以向上
        if (currentProgress > config.ProgressEpsilon)
            return Scrollability.CanScrollDown;

        return Scrollability.CanScrollDown;
    }

    /// <summary>检查是否可以向下滚动</summary>
    public static bool CanScrollDown(Scrollability scrollability) =>
        scrollability == Scrollability.CanScrollDown;

    /// <summary>检查是否可以向上滚动</summary>
    public static bool CanScrollUp(Scrollability scrollability) =>
        scrollability == Scrollability.CanScrollUp;

    /// <summary>检查是否到达底部</summary>
    public static bool IsAtBottom(Scrollability scrollability) =>
        scrollability == Scrollability.AtBottom;

    /// <summary>检查是否不可滚动</summary>
    public static bool IsNotScrollable(Scrollability scrollability) =>
        scrollability == Scrollability.NotScrollable;
}
