namespace UniClaw.Agent.Evaluation;

/// <summary>
/// Goal Evaluation — UniAgent 消费 Runtime Outcome envelope 后形成的
/// immutable 监督评价记录（Target §3.9；不变量 41：不回写 Runtime
/// Outcome / Run State / WorldBelief / Outcome Proof）。EvaluationId 由输入
/// 身份确定性派生（GEV-004 D8）；结构强制唯一长期不变量
/// Undetermined ⇒ NeedsFollowUp（D6）。唯一产出路径 = UniAgent.Evaluate
/// （验收 1）。
/// </summary>
public sealed record GoalEvaluation
{
    /// <summary>评价身份：由 (GoalId, RunId, OutcomeProofId) 确定性派生（D8）。</summary>
    public string EvaluationId { get; }

    /// <summary>被评价 Goal 的语义身份引用。</summary>
    public string GoalId { get; }

    /// <summary>被消费 envelope 的 Run 身份引用（原样入记录，不做校验）。</summary>
    public string RunId { get; }

    /// <summary>被消费 envelope 的 Outcome Proof 引用。</summary>
    public string OutcomeProofId { get; }

    /// <summary>satisfaction 维度结论（四值封闭）。</summary>
    public GoalSatisfaction Satisfaction { get; }

    /// <summary>处置维度结论（Final / NeedsFollowUp）。</summary>
    public EvaluationDisposition Disposition { get; }

    /// <summary>逐 criterion 判定（Met / Unmet / Unverifiable）。</summary>
    public IReadOnlyList<CriterionResult> CriterionResults { get; }

    /// <summary>机械拼装的判定依据（逐 criterion 陈述，不人写）。</summary>
    public string Rationale { get; }

    /// <summary>构造即校验结构不变量 Undetermined ⇒ NeedsFollowUp（D6）。</summary>
    public GoalEvaluation(
        string evaluationId,
        string goalId,
        string runId,
        string outcomeProofId,
        GoalSatisfaction satisfaction,
        EvaluationDisposition disposition,
        IReadOnlyList<CriterionResult> criterionResults,
        string rationale)
    {
        if (string.IsNullOrWhiteSpace(evaluationId))
            throw new ArgumentException("EvaluationId 不得为空", nameof(evaluationId));
        if (string.IsNullOrWhiteSpace(goalId))
            throw new ArgumentException("GoalId 不得为空", nameof(goalId));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId 不得为空", nameof(runId));
        if (string.IsNullOrWhiteSpace(outcomeProofId))
            throw new ArgumentException("OutcomeProofId 不得为空", nameof(outcomeProofId));
        ArgumentNullException.ThrowIfNull(criterionResults);
        ArgumentNullException.ThrowIfNull(rationale);
        if (satisfaction == GoalSatisfaction.Undetermined
            && disposition != EvaluationDisposition.NeedsFollowUp)
            throw new ArgumentException(
                "结构不变量（GEV-004 D6）：Undetermined ⇒ NeedsFollowUp", nameof(disposition));

        EvaluationId = evaluationId;
        GoalId = goalId;
        RunId = runId;
        OutcomeProofId = outcomeProofId;
        Satisfaction = satisfaction;
        Disposition = disposition;
        CriterionResults = criterionResults;
        Rationale = rationale;
    }
}
