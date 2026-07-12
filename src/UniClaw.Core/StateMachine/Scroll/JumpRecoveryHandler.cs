using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 步骤 6：跳跃恢复
/// 通过回滚进度和减小步长重试来恢复跳跃。
/// </summary>
public sealed class JumpRecoveryHandler
{
    private readonly ScrollHandlerConfig _config;

    /// <summary>
    /// 创建跳跃恢复处理器
    /// </summary>
    /// <param name="config">滚动配置</param>
    public JumpRecoveryHandler(ScrollHandlerConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 尝试恢复跳跃
    /// </summary>
    /// <param name="originalProgress">回退前的原始进度</param>
    /// <param name="originalStep">原始步长</param>
    /// <param name="executeFunc">执行滚动的函数</param>
    /// <param name="verifyFunc">验证跳跃的函数</param>
    /// <returns>恢复结果</returns>
    public JumpRecoveryResult Recover(
        double originalProgress,
        double originalStep,
        Func<double, ScrollActionResult> executeFunc,
        Func<ScrollVerifyResult> verifyFunc)
    {
        // 如果不需要恢复（前为空或正常滚动），直接返回成功
        var initialVerify = verifyFunc();
        if (JumpDetector.IsSafeInitialState(initialVerify) || !JumpDetector.IsJumpDetected(initialVerify))
        {
            return JumpRecoveryResult.Skipped("No jump recovery needed");
        }

        double currentStep = originalStep * _config.JumpRecoveryFactor;
        int retryCount = 0;

        while (retryCount < _config.MaxJumpRetryCount)
        {
            // 确保步长不低于最小值
            currentStep = Math.Max(currentStep, _config.MinScrollStep);

            // 计算安全步长（不超过剩余距离）
            var remainingDistance = 1.0 - originalProgress;
            var safeStep = Math.Min(currentStep, remainingDistance);

            if (safeStep < _config.MinScrollStep)
            {
                // 步长太小，无法继续恢复
                return JumpRecoveryResult.Failed(retryCount, originalProgress);
            }

            // 执行滚动
            var result = executeFunc(safeStep);

            if (!result.Success)
            {
                // 执行失败，继续重试
                currentStep *= _config.JumpRecoveryFactor;
                retryCount++;
                continue;
            }

            // 验证结果
            var verify = verifyFunc();

            if (!JumpDetector.IsJumpDetected(verify))
            {
                // 恢复成功
                return JumpRecoveryResult.Succeeded(retryCount + 1, safeStep, result.NewProgress);
            }

            // 仍然跳跃，减小步长继续重试
            currentStep *= _config.JumpRecoveryFactor;
            retryCount++;
        }

        // 超过最大重试次数
        return JumpRecoveryResult.Failed(_config.MaxJumpRetryCount, originalProgress);
    }

    /// <summary>
    /// 计算恢复步长
    /// </summary>
    /// <param name="originalStep">原始步长</param>
    /// <param name="retryCount">重试次数</param>
    /// <returns>恢复步长</returns>
    public double CalculateRecoveryStep(double originalStep, int retryCount)
    {
        var factor = Math.Pow(_config.JumpRecoveryFactor, retryCount + 1);
        var step = originalStep * factor;
        return Math.Max(step, _config.MinScrollStep);
    }

    /// <summary>
    /// 检查是否可以继续恢复
    /// </summary>
    /// <param name="step">当前步长</param>
    /// <param name="remainingDistance">剩余距离</param>
    /// <returns>是否可以继续</returns>
    public bool CanContinueRecovery(double step, double remainingDistance)
    {
        var safeStep = Math.Min(step, remainingDistance);
        return safeStep >= _config.MinScrollStep;
    }
}
