namespace UniClaw.Kernel.Effects;

/// <summary>Binding 拒绝原因（D8 三态）。</summary>
public enum BindingRejectionReason
{
    /// <summary>candidate 依据的 revision 不是 current WorldBelief revision。</summary>
    StaleRevision,

    /// <summary>grounding 报告多义/不确定，无法形成唯一 bounded target。</summary>
    Ambiguous,

    /// <summary>target 不存在于 current WorldBelief。</summary>
    UnknownTarget,
}

/// <summary>
/// Candidate Binding — Grounding Provider 的产出（Target §16.2）。
/// 不是 canonical binding，未经 Effect Boundary 认定不得 dispatch
/// （不变量 24，验收 3）。
/// </summary>
public sealed record CandidateBinding(
    string TargetSubject,
    string TargetValue,
    string SourceRevisionId,
    bool IsAmbiguous = false);

/// <summary>
/// Canonical bounded target binding — Effect Boundary 认定后的唯一有效
/// target 绑定（Target §16.1）。绑定 specific WorldBelief revision；失效
/// 为派生判定（revision 更新后即失效，复用 Slice 模式，无 event，D8）。
/// </summary>
public sealed record CanonicalBinding(
    string BindingId,
    string IntentId,
    string EffectClass,
    string TargetSubject,
    string TargetValue,
    string RevisionId,
    int RevisionNumber);

/// <summary>Binding 判定：Canonical 或 Rejected（D8）。</summary>
public sealed record BindingDecision(CanonicalBinding? Canonical, BindingRejectionReason? RejectionReason);
