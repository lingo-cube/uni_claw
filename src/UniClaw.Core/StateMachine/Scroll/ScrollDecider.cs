using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 步骤 3：滚动决策
/// 将滚动能力分类映射到具体的动作类型。
/// </summary>
public static class ScrollDecider
{
    /// <summary>
    /// 决定滚动动作类型
    /// </summary>
    /// <param name="scrollability">滚动能力</param>
    /// <returns>滚动动作类型</returns>
    public static ScrollActionType Decide(Scrollability scrollability)
    {
        return scrollability switch
        {
            Scrollability.CanScrollDown => ScrollActionType.ScrollDown,
            Scrollability.CanScrollUp => ScrollActionType.ScrollUp,
            Scrollability.AtBottom => ScrollActionType.None,
            Scrollability.NotScrollable => ScrollActionType.None,
            _ => ScrollActionType.None
        };
    }

    /// <summary>
    /// 决定滚动动作（带步长）
    /// </summary>
    /// <param name="scrollability">滚动能力</param>
    /// <param name="classification">分类结果</param>
    /// <returns>滚动上下文</returns>
    public static ScrollContext DecideWithStep(
        Scrollability scrollability,
        ScrollClassifier.Classification classification)
    {
        var actionType = Decide(scrollability);

        return new ScrollContext(
            ActionType: actionType,
            StepPercent: classification.RecommendedStep,
            CurrentProgress: classification.CurrentProgress,
            MaxThreshold: classification.MaxProgress,
            IsAtBottom: scrollability == Scrollability.AtBottom,
            HasScroll: scrollability != Scrollability.NotScrollable
        );
    }

    /// <summary>检查是否需要执行滚动</summary>
    public static bool ShouldScroll(ScrollActionType actionType) =>
        actionType == ScrollActionType.ScrollDown || actionType == ScrollActionType.ScrollUp;
}
