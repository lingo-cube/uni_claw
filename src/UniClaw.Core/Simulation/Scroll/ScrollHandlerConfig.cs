using UniClaw.Core.Domain;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动处理器配置：提供所有滚动相关参数的配置。
/// </summary>
public sealed record class ScrollHandlerConfig
{
    /// <summary>默认滚动步长（百分比）</summary>
    public double DefaultScrollStep { get; init; }

    /// <summary>最小滚动步长（百分比）</summary>
    public double MinScrollStep { get; init; }

    /// <summary>最大滚动步长（百分比）</summary>
    public double MaxScrollStep { get; init; }

    /// <summary>跳跃恢复最大重试次数</summary>
    public int MaxJumpRetryCount { get; init; }

    /// <summary>跳跃恢复步长缩减因子（每次重试乘以此因子）</summary>
    public double JumpRecoveryFactor { get; init; }

    /// <summary>进度边界比较的 epsilon 容差</summary>
    public double ProgressEpsilon { get; init; }

    /// <summary>是否启用自适应步长</summary>
    public bool EnableAdaptiveStep { get; init; }

    /// <summary>自适应步长增加因子</summary>
    public double AdaptiveStepIncreaseFactor { get; init; }

    /// <summary>自适应步长增加阈值（重复比例超过此值时增加步长）</summary>
    public double AdaptiveStepIncreaseThreshold { get; init; }

    /// <summary>自适应步长增加的最小样本量（新增元素数需达到此值才增加步长）</summary>
    public int MinSampleSize { get; init; }

    /// <summary>创建默认配置</summary>
    public static ScrollHandlerConfig Default() => new();

    /// <summary>
    /// 创建滚动处理器配置
    /// </summary>
    public ScrollHandlerConfig(
        double DefaultScrollStep = 0.3,
        double MinScrollStep = 0.01,
        double MaxScrollStep = 0.5,
        int MaxJumpRetryCount = 3,
        double JumpRecoveryFactor = 0.5,
        double ProgressEpsilon = 0.001,
        bool EnableAdaptiveStep = true,
        double AdaptiveStepIncreaseFactor = 1.5,
        double AdaptiveStepIncreaseThreshold = 0.7,
        int MinSampleSize = 3)
    {
        if (DefaultScrollStep < 0.0 || DefaultScrollStep > 1.0)
            throw new DomainValidationException(nameof(DefaultScrollStep), DefaultScrollStep, "DefaultScrollStep must be in [0.0, 1.0].");
        if (MinScrollStep < 0.0 || MinScrollStep > 1.0)
            throw new DomainValidationException(nameof(MinScrollStep), MinScrollStep, "MinScrollStep must be in [0.0, 1.0].");
        if (MaxScrollStep < 0.0 || MaxScrollStep > 1.0)
            throw new DomainValidationException(nameof(MaxScrollStep), MaxScrollStep, "MaxScrollStep must be in [0.0, 1.0].");
        if (MinScrollStep > MaxScrollStep)
            throw new DomainValidationException(nameof(MinScrollStep), MinScrollStep, "MinScrollStep must not exceed MaxScrollStep.");
        if (MaxJumpRetryCount < 0)
            throw new DomainValidationException(nameof(MaxJumpRetryCount), MaxJumpRetryCount, "MaxJumpRetryCount must be non-negative.");
        if (JumpRecoveryFactor <= 0.0 || JumpRecoveryFactor > 1.0)
            throw new DomainValidationException(nameof(JumpRecoveryFactor), JumpRecoveryFactor, "JumpRecoveryFactor must be in (0.0, 1.0].");
        if (ProgressEpsilon < 0.0 || ProgressEpsilon > 1.0)
            throw new DomainValidationException(nameof(ProgressEpsilon), ProgressEpsilon, "ProgressEpsilon must be in [0.0, 1.0].");
        if (AdaptiveStepIncreaseFactor <= 1.0)
            throw new DomainValidationException(nameof(AdaptiveStepIncreaseFactor), AdaptiveStepIncreaseFactor, "AdaptiveStepIncreaseFactor must be > 1.0.");
        if (AdaptiveStepIncreaseThreshold < 0.0 || AdaptiveStepIncreaseThreshold > 1.0)
            throw new DomainValidationException(nameof(AdaptiveStepIncreaseThreshold), AdaptiveStepIncreaseThreshold, "AdaptiveStepIncreaseThreshold must be in [0.0, 1.0].");
        if (MinSampleSize < 0)
            throw new DomainValidationException(nameof(MinSampleSize), MinSampleSize, "MinSampleSize must be non-negative.");

        this.DefaultScrollStep = DefaultScrollStep;
        this.MinScrollStep = MinScrollStep;
        this.MaxScrollStep = MaxScrollStep;
        this.MaxJumpRetryCount = MaxJumpRetryCount;
        this.JumpRecoveryFactor = JumpRecoveryFactor;
        this.ProgressEpsilon = ProgressEpsilon;
        this.EnableAdaptiveStep = EnableAdaptiveStep;
        this.AdaptiveStepIncreaseFactor = AdaptiveStepIncreaseFactor;
        this.AdaptiveStepIncreaseThreshold = AdaptiveStepIncreaseThreshold;
        this.MinSampleSize = MinSampleSize;
    }
}
