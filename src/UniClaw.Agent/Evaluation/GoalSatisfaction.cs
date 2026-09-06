namespace UniClaw.Agent.Evaluation;

/// <summary>
/// Goal Satisfaction — Goal Evaluation 的 satisfaction 维度（GEV-004 D2；
/// baseline §3.9 字面四标签拆分后的封闭四值，enum 名属 §22 L4 开放项）。
/// 判据不可验证 → Undetermined（不得伪装满足或不满足）。
/// _Avoid_: NeedsFollowUp（处置义，见 EvaluationDisposition）。
/// </summary>
public enum GoalSatisfaction
{
    /// <summary>required 与 preferred 全部满足。</summary>
    Satisfied,

    /// <summary>required 全满足、≥1 preferred 未满足。</summary>
    PartiallySatisfied,

    /// <summary>≥1 required 未满足（required 支配）。</summary>
    Unsatisfied,

    /// <summary>存在不可验证判据；诚实出口，非猜测。</summary>
    Undetermined,
}
