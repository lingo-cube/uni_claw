using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 步骤 2：滚动分类
/// 计算当前进度、最大阈值和推荐步长。
/// </summary>
public static class ScrollClassifier
{
    /// <summary>
    /// 滚动分类结果
    /// </summary>
    public sealed record class Classification
    {
        /// <summary>当前滚动进度</summary>
        public double CurrentProgress { get; init; }

        /// <summary>最大阈值（最大滚动位置）</summary>
        public double MaxProgress { get; init; }

        /// <summary>推荐步长</summary>
        public double RecommendedStep { get; init; }

        /// <summary>剩余可滚动距离</summary>
        public double RemainingDistance { get; init; }

        public Classification(
            double CurrentProgress,
            double MaxProgress,
            double RecommendedStep,
            double RemainingDistance)
        {
            this.CurrentProgress = CurrentProgress;
            this.MaxProgress = MaxProgress;
            this.RecommendedStep = RecommendedStep;
            this.RemainingDistance = RemainingDistance;
        }
    }

    /// <summary>
    /// 分类滚动情况
    /// </summary>
    /// <param name="currentProgress">当前滚动进度</param>
    /// <param name="maxThreshold">最大分段阈值</param>
    /// <param name="config">滚动配置</param>
    /// <returns>分类结果</returns>
    public static Classification Classify(
        double currentProgress,
        double maxThreshold,
        ScrollHandlerConfig config)
    {
        // 默认最大阈值为 1.0
        var maxProgress = maxThreshold > 0 ? maxThreshold : 1.0;

        // 计算剩余距离
        var remainingDistance = maxProgress - currentProgress;

        // 计算安全步长（不超过剩余距离）
        var safeStep = CalculateSafeStep(config.DefaultScrollStep, remainingDistance);

        return new Classification(
            CurrentProgress: currentProgress,
            MaxProgress: maxProgress,
            RecommendedStep: safeStep,
            RemainingDistance: remainingDistance
        );
    }

    /// <summary>
    /// 计算安全步长（不超过剩余距离）
    /// </summary>
    public static double CalculateSafeStep(double preferredStep, double remainingDistance)
    {
        if (remainingDistance <= 0)
            return 0.0;

        // 步长不能超过剩余距离
        return Math.Min(preferredStep, remainingDistance);
    }

    /// <summary>
    /// 检查是否到达底部（使用 epsilon 容差）
    /// </summary>
    public static bool IsAtBottom(double currentProgress, double maxThreshold, double epsilon)
    {
        return (maxThreshold - currentProgress) <= epsilon;
    }
}
