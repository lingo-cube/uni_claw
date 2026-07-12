namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 跳跃恢复结果：记录跳跃恢复尝试的最终结果。
/// </summary>
public sealed record class JumpRecoveryResult
{
    /// <summary>恢复是否成功</summary>
    public bool Success { get; init; }

    /// <summary>重试次数</summary>
    public int RetryCount { get; init; }

    /// <summary>最终使用的滚动步长</summary>
    public double FinalStep { get; init; }

    /// <summary>恢复后的滚动进度</summary>
    public double FinalProgress { get; init; }

    /// <summary>恢复原因/描述</summary>
    public string Reason { get; init; }

    /// <param name="Success">恢复是否成功</param>
    /// <param name="RetryCount">重试次数</param>
    /// <param name="FinalStep">最终使用的滚动步长</param>
    /// <param name="FinalProgress">恢复后的滚动进度</param>
    /// <param name="Reason">恢复原因/描述</param>
    public JumpRecoveryResult(
        bool Success,
        int RetryCount,
        double FinalStep,
        double FinalProgress,
        string Reason)
    {
        this.Success = Success;
        this.RetryCount = RetryCount;
        this.FinalStep = FinalStep;
        this.FinalProgress = FinalProgress;
        this.Reason = Reason ?? string.Empty;
    }

    /// <summary>创建成功恢复结果</summary>
    public static JumpRecoveryResult Succeeded(int retryCount, double finalStep, double finalProgress) =>
        new JumpRecoveryResult(
            Success: true,
            RetryCount: retryCount,
            FinalStep: finalStep,
            FinalProgress: finalProgress,
            Reason: $"Jump recovery succeeded after {retryCount} retries with step {finalStep:F3}.");

    /// <summary>创建失败结果（超过最大重试次数）</summary>
    public static JumpRecoveryResult Failed(int maxRetries, double originalProgress) =>
        new JumpRecoveryResult(
            Success: false,
            RetryCount: maxRetries,
            FinalStep: 0.0,
            FinalProgress: originalProgress,
            Reason: $"Jump recovery failed after {maxRetries} retries. Maximum retries exceeded.");

    /// <summary>创建跳过恢复结果（无需恢复）</summary>
    public static JumpRecoveryResult Skipped(string reason) =>
        new JumpRecoveryResult(
            Success: true,
            RetryCount: 0,
            FinalStep: 0.0,
            FinalProgress: 0.0,
            Reason: reason);
}
