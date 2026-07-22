namespace UniClaw.Core.UniBrain;

/// <summary>
/// DecisionResult — 决策结果枚举。
/// 3 值锁定 (Success, Unsure, GiveUp)，新增/删除需 constitution change flow。
/// 从 AI/IAIStrategyAdvisor.cs 迁入 UniBrain/。
/// </summary>
public enum DecisionResult
{
    /// <summary>成功决策 — 找到了可行的操作</summary>
    Success,

    /// <summary>不确定 — 情况模糊，无法明确决策</summary>
    Unsure,

    /// <summary>放弃 — 没有可行的操作</summary>
    GiveUp
}
