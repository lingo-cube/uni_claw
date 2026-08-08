namespace UniClaw.Runtime.Model;

/// <summary>
/// 动作 dispatch 结果（宪章 §25 / 裁决 10）：只表达 dispatch outcome
/// （Dispatched / TimedOut / Rejected），任何 dispatch 结果都不直接证明世界状态或 Goal 完成
/// （dispatch outcome ≠ world success，SC-P1-003：dispatch ≠ completed）。
/// 不携带世界快照——世界状态只能通过 post-action Observation 重新确认（§3）。
/// </summary>
/// <param name="Outcome">dispatch outcome。</param>
/// <param name="ActionDescription">被 dispatch 动作的描述（可读）。</param>
/// <param name="Info">附加信息（如 Rejected 原因 / 超时说明）。</param>
public sealed record ActionResult(ActionResultOutcome Outcome, string? ActionDescription, string? Info);

/// <summary>动作 dispatch outcome（§25 / 裁决 10）。</summary>
public enum ActionResultOutcome
{
    /// <summary>动作已分发执行。</summary>
    Dispatched = 0,

    /// <summary>分发超时——动作是否实际生效未知（非幂等动作不得盲目重试，须先 Observe — §25 / SC-P1-004 无恢复动作）。</summary>
    TimedOut = 1,

    /// <summary>动作被环境拒绝（物理能力语义，如 SetSwitch 作用于非开关承载元素 — SC-P1-005 错误路径）。</summary>
    Rejected = 2,
}
