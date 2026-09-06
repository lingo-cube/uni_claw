namespace UniClaw.Kernel.Evidence;

/// <summary>admission 结论：仅判 canonical record eligibility。</summary>
public enum AdmissionDecision
{
    /// <summary>输入成为（或复用）canonical Evidence Record。</summary>
    Accepted,

    /// <summary>输入被拒；不产生 canonical record，不得进入 Relevance / Reconciliation。</summary>
    Rejected,
}

/// <summary>单项 admission 检查结果（Target §11.1 检查清单逐项留痕）。</summary>
public sealed record AdmissionCheck(string Name, bool Passed);

/// <summary>
/// Admission Record：输入是否可成为 canonical Evidence Record 的判定留痕。
/// 每次 Admit 调用产生一条（append-oriented）；accepted 时携带 EvidenceId。
/// </summary>
public sealed record AdmissionRecord(
    AdmissionDecision Decision,
    IReadOnlyList<AdmissionCheck> Checks,
    string? EvidenceId,
    string? RejectionReason);
