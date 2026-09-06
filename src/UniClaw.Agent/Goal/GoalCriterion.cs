using UniClaw.Kernel.Run;

namespace UniClaw.Agent.Goal;

/// <summary>
/// Goal Criterion — Primary Goal 携带的结构性 satisfaction 判据（GEV-004
/// D3）。只携带语义身份（terminal classification 词汇 / obligation id），
/// 不耦合 envelope 字段布局：对象图不得出现 RuntimeOutcome /
/// ObligationStatus 结构类型（验收 5；反例 A）。复用 Kernel 公开 enum 是
/// baseline L0/§13.2 稳定词汇复用，非结构耦合。封闭种类：ClassificationIs /
/// ObligationFulfilled。
/// </summary>
public abstract record GoalCriterion
{
    private GoalCriterion() { }

    /// <summary>terminal classification 语义期望。</summary>
    public sealed record ClassificationIs(TerminalClassification Expected) : GoalCriterion;

    /// <summary>
    /// obligation 语义身份满足期望。envelope 内 id 缺席 = Unverifiable
    /// （≠ false）；id 在而 Satisfied=false = Unmet（GEV-004 D5）。
    /// </summary>
    public sealed record ObligationFulfilled(string ObligationId) : GoalCriterion;
}
