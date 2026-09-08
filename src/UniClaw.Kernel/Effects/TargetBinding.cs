namespace UniClaw.Kernel.Effects;

/// <summary>Binding 拒绝原因（D8 三态 + CBA-005 D2 第四态）。</summary>
public enum BindingRejectionReason
{
    /// <summary>candidate 缺失：act pipeline 无候选绑定可认定（显式 decision，非异常）。</summary>
    NoCandidate,

    /// <summary>candidate 依据的 revision 不是 current WorldBelief revision。</summary>
    StaleRevision,

    /// <summary>grounding 报告多义/不确定，无法形成唯一 bounded target。</summary>
    Ambiguous,

    /// <summary>target 不存在于 current WorldBelief。</summary>
    UnknownTarget,
}

/// <summary>
/// UI 通道的已解析 target 引用（UIW-004：UI binding 恒绑 occurrence）。
/// OccurrenceId 是唯一绑定目标；SourceRevisionId 是候选依据的 revision
/// （Bind 时对照 view.RevisionId 判 stale）；LogicalItemId 仅 provenance
/// 列（continuity 溯源，不参与绑定认定）。
/// </summary>
public sealed record UiTargetReference(string OccurrenceId, string SourceRevisionId, string? LogicalItemId = null);

/// <summary>
/// Candidate Binding — Grounding Provider 的产出（Target §16.2）。
/// 不是 canonical binding，未经 Effect Boundary 认定不得 dispatch
/// （不变量 24，验收 3）。UIW-004：UiTarget 非 null = UI 通道 candidate
/// （恒绑已解析 occurrence 引用）；null = 既有字符串通道（行为不变，
/// 无任何 UI→字符串 fallback）。
/// </summary>
public sealed record CandidateBinding(
    string TargetSubject,
    string TargetValue,
    string SourceRevisionId,
    bool IsAmbiguous = false,
    UiTargetReference? UiTarget = null);

/// <summary>
/// Canonical bounded target binding — Effect Boundary 认定后的唯一有效
/// target 绑定（Target §16.1）。绑定 specific WorldBelief revision；失效
/// 为派生判定（revision 更新后即失效，复用 Slice 模式，无 event，D8）。
/// UIW-004 尾部可选字段：UI 通道 canonical 的目标 = TargetOccurrenceId
/// （TargetSubject 沿载 occurrence id 字符串供 attempt 留痕约定）；
/// LogicalItemId 为 provenance；OwningContainerId 尽力而为（view 无该
/// fact 时 null）。字符串通道三者恒 null。
/// </summary>
public sealed record CanonicalBinding(
    string BindingId,
    string IntentId,
    string EffectClass,
    string TargetSubject,
    string TargetValue,
    string RevisionId,
    int RevisionNumber,
    string? TargetOccurrenceId = null,
    string? OwningContainerId = null,
    string? LogicalItemId = null);

/// <summary>Binding 判定：Canonical 或 Rejected（D8）。</summary>
public sealed record BindingDecision(CanonicalBinding? Canonical, BindingRejectionReason? RejectionReason);
