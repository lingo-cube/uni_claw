namespace UniClaw.Kernel.Assurance;

/// <summary>单项 assurance 检查结果（action-local，逐项留痕）。</summary>
public sealed record AssuranceCheck(string Name, bool Passed);

/// <summary>
/// Assurance Judgment — Runtime Assurance Judgment Authority 的不可变产出
/// （Target §15，不变量 22；§17：inputs 或 freshness 改变后必须重新判断）。
/// CBA-005 / ADR-0009：三元组 (IntentId, BindingId, RevisionId) 是
/// correlation key——RevisionId 记 Binding.RevisionId（"审的是这个
/// binding"）；三元组不是 canonical identity。
/// FRS-007：Freshness 携带本次消费的 FreshnessJudgment（ADR-0010：
/// 消费相对的 sufficiency 判定，只对该次消费有效）；currentness 系列
/// 检查（binding-revision-currentness / intent-basis-currentness）与
/// freshness sufficiency 是两个独立维度——revision currentness ≠
/// freshness。
/// </summary>
public sealed record AssuranceJudgment(
    string IntentId,
    string BindingId,
    string RevisionId,
    bool IsAdmissible,
    IReadOnlyList<AssuranceCheck> Checks,
    string? RejectionReason,
    FreshnessJudgment Freshness);
