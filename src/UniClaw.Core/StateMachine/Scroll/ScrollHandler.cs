using System.Collections.Immutable;
using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 滚动处理器：7 步流程编排
/// Detect → Classify → Decide → Execute → Verify → Recover → Statistics
/// </summary>
public sealed class ScrollHandler
{
    private readonly ScrollHandlerConfig _config;
    private readonly ScrollActionExecutor _executor;
    private readonly JumpRecoveryHandler _recoveryHandler;
    private readonly ScrollStatisticsCollector _statistics;

    /// <summary>滚动配置</summary>
    public ScrollHandlerConfig Config => _config;

    /// <summary>统计收集器</summary>
    public ScrollStatisticsCollector Statistics => _statistics;

    /// <summary>
    /// 创建滚动处理器
    /// </summary>
    /// <param name="config">滚动配置（可选，使用默认值）</param>
    public ScrollHandler(ScrollHandlerConfig? config = null)
    {
        _config = config ?? ScrollHandlerConfig.Default();
        _executor = new ScrollActionExecutor();
        _recoveryHandler = new JumpRecoveryHandler(_config);
        _statistics = new ScrollStatisticsCollector();
    }

    /// <summary>
    /// 注册滚动动作处理器
    /// </summary>
    /// <param name="actionType">动作类型</param>
    /// <param name="handler">处理器委托</param>
    public void RegisterActionHandler(ScrollActionType actionType, ScrollActionDelegate handler)
    {
        _executor.RegisterHandler(actionType, handler);
    }

    /// <summary>
    /// 处理滚动（7 步流程）
    /// </summary>
    /// <param name="hasScrollData">是否有滚动数据</param>
    /// <param name="isEndOfList">是否到达列表末尾</param>
    /// <param name="currentProgress">当前滚动进度</param>
    /// <param name="maxThreshold">最大阈值</param>
    /// <param name="beforeElementIds">滚动前元素 ID 集合</param>
    /// <returns>滚动动作结果</returns>
    public ScrollActionResult HandleScroll(
        bool hasScrollData,
        bool isEndOfList,
        double currentProgress,
        double maxThreshold,
        ImmutableArray<string> beforeElementIds)
    {
        // Step 1: Detect (检测滚动能力)
        var scrollability = ScrollabilityDetector.DetectFull(
            hasScrollData, isEndOfList, currentProgress, _config);

        // 如果不可滚动或已到底部，记录跳过并返回
        if (ScrollabilityDetector.IsNotScrollable(scrollability) ||
            ScrollabilityDetector.IsAtBottom(scrollability))
        {
            _statistics.RecordSkip();
            return ScrollActionResult.Skipped(scrollability == Scrollability.AtBottom
                ? "At bottom of list"
                : "No scroll data available");
        }

        // Step 2: Classify (分类滚动情况)
        var classification = ScrollClassifier.Classify(currentProgress, maxThreshold, _config);

        // Step 3: Decide (决定动作类型)
        var context = ScrollDecider.DecideWithStep(scrollability, classification);

        // Step 4: Execute (执行滚动)
        var executeResult = _executor.Execute(context);

        if (!executeResult.Success)
        {
            return executeResult;
        }

        // Step 5: Verify (验证跳跃)
        // 注意：这里需要外部提供 afterElementIds
        // 在实际使用中，需要在滚动后重新获取元素 ID
        // 这里简化处理，假设外部会再次调用 HandleScrollWithVerify

        _statistics.RecordScroll(executeResult.NewProgress - currentProgress, context.StepPercent);

        return executeResult;
    }

    /// <summary>
    /// 处理滚动（完整版本，包含验证和恢复）
    /// </summary>
    /// <param name="hasScrollData">是否有滚动数据</param>
    /// <param name="isEndOfList">是否到达列表末尾</param>
    /// <param name="currentProgress">当前滚动进度</param>
    /// <param name="maxThreshold">最大阈值</param>
    /// <param name="beforeElementIds">滚动前元素 ID 集合</param>
    /// <param name="afterElementIdsFunc">获取滚动后元素 ID 集合的函数</param>
    /// <returns>滚动动作结果</returns>
    public ScrollActionResult HandleScrollWithVerify(
        bool hasScrollData,
        bool isEndOfList,
        double currentProgress,
        double maxThreshold,
        ImmutableArray<string> beforeElementIds,
        Func<ImmutableArray<string>> afterElementIdsFunc)
    {
        // Step 1-4: 执行滚动
        var initialResult = HandleScroll(
            hasScrollData, isEndOfList, currentProgress, maxThreshold, beforeElementIds);

        if (!initialResult.Success || initialResult.Action == ScrollActionType.None)
            return initialResult;

        // Step 5: Verify (验证跳跃)
        var afterElementIds = afterElementIdsFunc();
        var verifyResult = JumpDetector.Detect(beforeElementIds, afterElementIds);

        if (JumpDetector.IsJumpDetected(verifyResult))
        {
            _statistics.RecordJumpDetected();

            // Step 6: Recover (恢复跳跃)
            var recoveryResult = _recoveryHandler.Recover(
                currentProgress,
                initialResult.NewProgress - currentProgress,
                step => _executor.ExecuteDirect(ScrollActionType.ScrollDown, step),
                () => JumpDetector.Detect(beforeElementIds, afterElementIdsFunc()));

            if (recoveryResult.Success)
            {
                _statistics.RecordJumpRecovered();
                return ScrollActionResult.Succeeded(
                    ScrollActionType.ScrollDown,
                    recoveryResult.FinalProgress,
                    recoveryResult.Reason);
            }
            else
            {
                return ScrollActionResult.Failed(ScrollActionType.ScrollDown, recoveryResult.Reason);
            }
        }

        // Step 7: Statistics (已在 HandleScroll 中记录)
        return initialResult;
    }

    /// <summary>
    /// 计算下一个自适应步长
    /// </summary>
    /// <param name="currentStep">当前步长</param>
    /// <param name="verifyResult">验证结果</param>
    /// <returns>下一个步长</returns>
    public double CalculateNextAdaptiveStep(double currentStep, ScrollVerifyResult verifyResult)
    {
        return AdaptiveStepCalculator.CalculateNextStep(currentStep, verifyResult, _config);
    }

    /// <summary>
    /// 重置统计
    /// </summary>
    public void ResetStatistics()
    {
        _statistics.Reset();
    }
}
