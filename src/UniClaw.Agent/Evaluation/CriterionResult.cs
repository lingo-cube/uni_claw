using UniClaw.Agent.Goal;

namespace UniClaw.Agent.Evaluation;

/// <summary>
/// criterion 级判定三值（GEV-004 D5）：Met（判据成立）/ Unmet（判据明确
/// 不成立，id 在而 Satisfied=false）/ Unverifiable（id 缺席，无法判定——
/// ≠ false）。
/// </summary>
public enum CriterionOutcome
{
    /// <summary>判据成立。</summary>
    Met,

    /// <summary>判据明确不成立（id 在而 Satisfied=false）。</summary>
    Unmet,

    /// <summary>id 缺席，无法判定（≠ false）。</summary>
    Unverifiable,
}

/// <summary>单条 Goal Criterion 的判定结果。</summary>
public sealed record CriterionResult(GoalCriterion Criterion, CriterionOutcome Outcome);
