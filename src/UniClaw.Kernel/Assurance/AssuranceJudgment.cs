namespace UniClaw.Kernel.Assurance;

/// <summary>单项 assurance 检查结果（action-local，逐项留痕）。</summary>
public sealed record AssuranceCheck(string Name, bool Passed);

/// <summary>
/// Assurance Judgment — Runtime Assurance Judgment Authority 的不可变产出
/// （Target §15，不变量 22；§17：inputs/freshness 改变后必须重新判断——
/// 时间维度的 freshness 属协议 P4 deferred，本实现的检查名为 currentness
/// 系列：binding-revision-currentness / intent-basis-currentness）。
/// CBA-005 / ADR-0009：三元组 (IntentId, BindingId, RevisionId) 是
/// correlation key——RevisionId 记 Binding.RevisionId（"审的是这个
/// binding"）；三元组不是 canonical identity。
/// </summary>
public sealed record AssuranceJudgment(
    string IntentId,
    string BindingId,
    string RevisionId,
    bool IsAdmissible,
    IReadOnlyList<AssuranceCheck> Checks,
    string? RejectionReason);
