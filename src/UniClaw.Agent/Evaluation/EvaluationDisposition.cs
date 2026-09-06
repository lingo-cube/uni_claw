namespace UniClaw.Agent.Evaluation;

/// <summary>
/// Evaluation Disposition — Goal Evaluation 的处置维度（GEV-004 D2/D6）：
/// Final（终局监督结论）/ NeedsFollowUp（需后续澄清、补充契约或重评）。
/// 唯一长期不变量：Undetermined ⇒ NeedsFollowUp（GoalEvaluation 构造强制）。
/// Satisfied→Final / PartiallySatisfied→NeedsFollowUp / Unsatisfied→Final
/// 只是 GEV-004 Empty-context policy，非类型约束；本类型不实现 follow-up
/// 机制。
/// </summary>
public enum EvaluationDisposition
{
    /// <summary>终局监督结论。</summary>
    Final,

    /// <summary>需后续澄清 / 补充契约 / 重评。</summary>
    NeedsFollowUp,
}
