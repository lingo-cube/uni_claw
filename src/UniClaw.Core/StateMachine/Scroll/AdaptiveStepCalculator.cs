using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 步骤 8（纯函数）：自适应步长计算
/// 根据重复元素比例增加滚动步长以提高效率。
/// </summary>
public static class AdaptiveStepCalculator
{
    /// <summary>
    /// 计算下一个滚动步长
    /// </summary>
    /// <param name="currentStep">当前步长</param>
    /// <param name="verifyResult">滚动验证结果</param>
    /// <param name="config">滚动配置</param>
    /// <returns>下一个步长</returns>
    public static double CalculateNextStep(
        double currentStep,
        ScrollVerifyResult verifyResult,
        ScrollHandlerConfig config)
    {
        // 如果未启用自适应步长，返回当前步长
        if (!config.EnableAdaptiveStep)
            return currentStep;

        // 如果后元素集合为空，不增加步长
        if (verifyResult.AfterElementIds.IsEmpty)
            return currentStep;

        // 检查是否应该增加步长
        if (ShouldIncreaseStep(verifyResult, config))
        {
            var increasedStep = currentStep * config.AdaptiveStepIncreaseFactor;
            return Clamp(increasedStep, config.MinScrollStep, config.MaxScrollStep);
        }

        return currentStep;
    }

    /// <summary>
    /// 判断是否应该增加步长
    /// </summary>
    /// <param name="verifyResult">滚动验证结果</param>
    /// <param name="config">滚动配置</param>
    /// <returns>是否应该增加</returns>
    public static bool ShouldIncreaseStep(ScrollVerifyResult verifyResult, ScrollHandlerConfig config)
    {
        // 重复比例达到阈值
        var ratioMet = verifyResult.DuplicateRatio >= config.AdaptiveStepIncreaseThreshold;

        // 新元素数量达到最小样本量
        var sampleSizeMet = verifyResult.NewElementCount >= config.MinSampleSize;

        return ratioMet && sampleSizeMet;
    }

    /// <summary>
    /// 计算安全步长（不超过剩余距离）
    /// </summary>
    /// <param name="preferredStep">首选步长</param>
    /// <param name="currentProgress">当前进度</param>
    /// <param name="maxThreshold">最大阈值</param>
    /// <returns>安全步长</returns>
    public static double CalculateSafeStep(
        double preferredStep,
        double currentProgress,
        double maxThreshold)
    {
        var remainingDistance = maxThreshold - currentProgress;
        return Math.Min(preferredStep, remainingDistance);
    }

    /// <summary>
    /// 限制步长在 [min, max] 范围内
    /// </summary>
    public static double Clamp(double step, double min, double max)
    {
        if (step < min) return min;
        if (step > max) return max;
        return step;
    }

    /// <summary>
    /// 使用 epsilon 比较进度是否相等
    /// </summary>
    public static bool ProgressEquals(double a, double b, double epsilon)
    {
        return Math.Abs(a - b) <= epsilon;
    }

    /// <summary>
    /// 使用 epsilon 比较进度是否到达边界
    /// </summary>
    public static bool IsAtBoundary(double current, double max, double epsilon)
    {
        return (max - current) <= epsilon;
    }
}
