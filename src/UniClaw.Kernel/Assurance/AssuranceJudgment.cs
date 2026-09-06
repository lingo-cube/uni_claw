namespace UniClaw.Kernel.Assurance;

/// <summary>单项 assurance 检查结果（action-local，逐项留痕）。</summary>
public sealed record AssuranceCheck(string Name, bool Passed);

/// <summary>
/// Assurance Judgment — Runtime Assurance Judgment Authority 的不可变产出
/// （Target §15，不变量 22；§17：inputs/freshness 改变后必须重新判断）。
/// 本切片只含 action-local 判定；Effect Verification / Outcome Proof → OUT-003。
/// </summary>
public sealed record AssuranceJudgment(
    string IntentId,
    bool IsAdmissible,
    IReadOnlyList<AssuranceCheck> Checks,
    string? RejectionReason);
