using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

/// <summary>
/// 显式冲突：同 subject 的既存 belief 遭遇不相容的新 accepted evidence。
/// 双方 evidence 引用都保留；不静默覆盖（Target 验收 5）。
/// </summary>
public sealed record Conflict(
    string Subject,
    string EstablishedValue,
    string ChallengingValue,
    string EstablishedEvidenceId,
    string ChallengingEvidenceId);

/// <summary>
/// FreshnessBasis：World Model 随 revision 表达的 freshness 判定输入聚合
/// （temporal provenance 层面；当前 realization = basis 中最晚 capture
/// time）。是表达不是裁决，不单独构成 freshness 权威（ADR-0010）；
/// 不保存消费时才能计算的 age——age 属消费侧派生。
/// </summary>
public sealed record FreshnessBasis(DateTimeOffset AsOf);

/// <summary>
/// Uncertainty：revision 显式携带的不确定性（本切片：冲突 claim 计数）。
/// </summary>
public sealed record Uncertainty(int ConflictingClaimCount);

/// <summary>
/// Claim Evolution 判别种类（CLE-001 / ADR-0016 / UWM-009 §18）：
/// Reaffirm = 同值再观察（belief 零变化，log 留痕）；
/// Revise = 同 producer 异 scope 的 presentation re-observation
///（值替换 + 痕迹链，不产生 Conflict）。Conflict 不属演进域（语义保持）。
/// </summary>
public enum ClaimEvolutionKind
{
    Reaffirm,
    Revise,
}

/// <summary>
/// 一次 claim evolution 判定（owner-internal append-only 留痕，同
/// AssociationLog / ContinuityLog 先例）：subject 历代 value / evidence
/// 演进可溯源（R-UW replayability 家族）；不进 revision aggregate、
/// 不构成第二 truth。
/// </summary>
public sealed record ClaimEvolutionDecision(
    string RevisionId,
    string Subject,
    ClaimEvolutionKind Kind,
    string? PreviousValue,
    string NewValue,
    string? SupersededEvidenceId,
    string EstablishingEvidenceId,
    string Producer,
    string Scope);

/// <summary>
/// World State 中的单条 claim：值 + 确立该值的 evidence 引用
/// （claim 粒度 evidence basis —— 冲突双方可各自溯源）
/// +（CLE-001 / ADR-0016）establishing record 的 provenance 摘要
///（Producer / Scope，belief 侧演进域判别输入）与 Revise 痕迹链
///（SupersededEvidenceIds = 旧链 ∪ 被取代的历代 EvidenceId）。
/// </summary>
public sealed record WorldClaim(
    string Value,
    string EvidenceId,
    string? EstablishingProducer = null,
    string? EstablishingScope = null,
    IReadOnlyList<string>? SupersededEvidenceIds = null);

/// <summary>
/// WorldBelief Revision — canonical belief aggregate（Target §12）：
/// World Graph + World State + Evidence Basis + FreshnessBasis + Uncertainty + Conflicts
/// +（UWM-009）ContainerGraph realization：Containers / Relations（revision-bound、
/// evidence-backed；无 strategy 的既有路径两者为空，语义不变）
/// +（UIW-003 / UWM-009 v0.3 §35）ObservationOccurrences（revision-local：派生自
/// 触发本 revision 的 evidence record，替换不继承；无 observation strategy 的
/// 既有路径为 null，语义不变）与 LogicalItems（跨 revision 延续的 demand-gated、
/// evidence-established 有界连续性；mint 之前为 null）。
/// 不可变；每次 Reconciliation 产生新 revision，历史保留为只读依据。
/// </summary>
public sealed record WorldBeliefRevision(
    string RevisionId,
    string? ParentRevisionId,
    int RevisionNumber,
    IReadOnlyDictionary<string, WorldClaim> WorldState,
    IReadOnlyList<string> WorldGraph,
    IReadOnlySet<string> EvidenceBasis,
    FreshnessBasis FreshnessBasis,
    Uncertainty Uncertainty,
    IReadOnlyList<Conflict> Conflicts,
    IReadOnlyList<ContainerBelief>? Containers = null,
    IReadOnlyList<ContainerRelation>? Relations = null,
    IReadOnlyList<OccurrenceBelief>? Occurrences = null,
    IReadOnlyList<LogicalItemBelief>? LogicalItems = null);
