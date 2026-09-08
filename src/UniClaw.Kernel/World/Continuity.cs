namespace UniClaw.Kernel.World;

/// <summary>
/// Continuity demand 生产者封闭（ADR-0014：合法性由调用端口 + 运行时 authority
/// 验证，不信载荷自述）。v0.1 物理入口只有 EffectTargetCommitment；
/// EntityScopedObligation 语义保留、物理入口 deferred（无 buyer 不建空协议边）。
/// </summary>
public enum ContinuityDemandSourceKind
{
    /// <summary>EB bind/re-bind 路径（主 buyer 入口）。</summary>
    EffectTargetCommitment,

    /// <summary>contract 侧 entity-scoped obligation（语义保留，物理入口 deferred）。</summary>
    EntityScopedObligation,
}

/// <summary>
/// ContinuityDemand — 经 P23 单缝双模（ResolveContinuity 侧）进入 World Model
/// 的 non-evidentiary 输入（ADR-0014）：≠ EvidenceRecord，不建立 identity、
/// 不强制 Matched/New、不修改事实 claims、不直接证明 Presence/Lifecycle；
/// demand 状态变化不产生 WorldBelief revision（P-UW-32）。timing：anchor
/// occurrence 必须仍属 current revision（过期 fail-closed）；descriptor-scoped
/// 无 anchor 合法；standing demand 可早于 identity，但不能代替 identity evidence。
/// </summary>
public sealed record ContinuityDemand(
    string DemandId,
    ContinuityDemandSourceKind SourceKind,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    string? AnchorOccurrenceId,
    string? AnchorRevisionId,
    string? LogicalItemId);

/// <summary>
/// DemandHandle — 不透明 correlation token（ADR-0014 多 buyer 生命周期）：
/// 非 identity 非 truth，只用于后续 ResolveContinuity / RevokeContinuityDemand
/// 关联到登记的 demand。
/// </summary>
public sealed record DemandHandle(string DemandId);

/// <summary>
/// ContinuityAdjudicationOutcomeKind — continuity 判别轴词汇（ADR-0014：
/// ContinuityResolutionOutcome = ReferenceEstablished | 本四值 | NoCurrentCandidate
/// 三态），与 Container Association 词汇（AssociationDispositionKind）是两个
/// 判别过程、两套枚举，绝不混用。ReferenceEstablished 不入本 enum。
/// Contradicted 只证伪候选，不终止 LogicalItem。
/// </summary>
public enum ContinuityAdjudicationOutcomeKind
{
    /// <summary>判别信息充分：候选 occurrence 与既有 LogicalItem 是同一 referent。</summary>
    SameReferent,

    /// <summary>observation 充分但存在多个成立 interpretation——Identity never creates information。</summary>
    Ambiguous,

    /// <summary>判别信息不足（含 authority-blocked 降级）。</summary>
    Insufficient,

    /// <summary>反证在场：current candidate ≠ existing LogicalItem（不终止 item）。</summary>
    Contradicted,
}

/// <summary>
/// ContinuityResolutionOutcome 的三态轴：ReferenceEstablished（首次铸造，无
/// previous referent）/ Adjudicated（四值判别）/ NoCurrentCandidate（current
/// revision 无候选 occurrence）。ReferenceEstablished 与判别四值分属不同轴，
/// 禁止合并进 ContinuityAdjudicationOutcomeKind。
/// </summary>
public enum ContinuityResolutionOutcomeKind
{
    /// <summary>首次铸造 LogicalItem（带新 item id）。</summary>
    ReferenceEstablished,

    /// <summary>ContinuityAdjudicationOutcomeKind 四值之一（AdjudicationKind 非 null）。</summary>
    Adjudicated,

    /// <summary>current revision 无候选 occurrence，零 belief/lifecycle 副作用。</summary>
    NoCurrentCandidate,
}

/// <summary>
/// ContinuityResolutionOutcome — ResolveContinuity 的判别结果。
/// Kind = ReferenceEstablished 时 LogicalItemId 非 null（新铸 item）；
/// Kind = Adjudicated 时 AdjudicationKind 非 null；Kind = NoCurrentCandidate
/// 时两者皆 null。
/// </summary>
public sealed record ContinuityResolutionOutcome(
    ContinuityResolutionOutcomeKind Kind,
    ContinuityAdjudicationOutcomeKind? AdjudicationKind = null,
    string? LogicalItemId = null);

/// <summary>
/// strategy 提议轴（proposal vocabulary）：ReferenceEstablished + 判别四值。
/// 这是 owner-internal seam 的提议词汇，与结果词汇
/// （ContinuityResolutionOutcome / ContinuityAdjudicationOutcomeKind）分离；
/// NoCurrentCandidate 是 WorldModel gate 产物，strategy 无权提议。
/// </summary>
public enum ContinuityProposedOutcomeKind
{
    /// <summary>提议首次铸造 LogicalItem（须经 authority gates）。</summary>
    ReferenceEstablished,

    /// <summary>提议候选 occurrence 与既有 LogicalItem 同 referent。</summary>
    SameReferent,

    /// <summary>提议歧义（不强制选择）。</summary>
    Ambiguous,

    /// <summary>提议判别信息不足。</summary>
    Insufficient,

    /// <summary>提议反证（只证伪候选，不终止 item）。</summary>
    Contradicted,
}

/// <summary>
/// Continuity 候选 occurrence（adjudication input 的一部分）：current revision
/// 的 occurrence belief + 各自 evidence refs（supporting / contradicting 分携，
/// R-UW-05：不得压平成单一 score）。
/// </summary>
public sealed record ContinuityCandidateOccurrence(
    string OccurrenceId,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    IReadOnlyList<string> SupportingEvidenceIds,
    IReadOnlyList<string> ContradictingEvidenceIds);

/// <summary>Continuity 候选既有 LogicalItem（adjudication input 的一部分）。</summary>
public sealed record ContinuityCandidateItem(
    string LogicalItemId,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    LogicalItemLifecycle Lifecycle,
    IReadOnlyList<string> EvidenceBasis);

/// <summary>
/// Continuity adjudication 输入（owner-internal seam 的输入，非跨组件 view）：
/// demand（role / descriptor / container / anchor item）+ current revision 的
/// 候选 occurrences + 既有候选 LogicalItems（含 basis）。
/// </summary>
public sealed record ContinuityAdjudicationInput(
    ContinuityDemand Demand,
    WorldBeliefRevision Current,
    IReadOnlyList<ContinuityCandidateOccurrence> Candidates,
    IReadOnlyList<ContinuityCandidateItem> ExistingItems);

/// <summary>
/// strategy 提议的 referent 终止（ADR-0015：Ended 仅来自正面 lifecycle
/// evidence）：须经 WorldModel gate（supporting ⊆ basis 且非空、item 存在）
/// 才生效为 Ended(referent-terminated)；gate 失败则 item 保持 Established。
/// </summary>
public sealed record ProposedTermination(
    string LogicalItemId,
    IReadOnlyList<string> SupportingEvidenceIds,
    string Reason);

/// <summary>
/// Strategy proposal：提议判别结果 + matched ids + supporting / contradicting
/// evidence ids（分携，不压平）+ 可选 terminated items。Authority gates 在
/// WorldModel 边界强制，不委托 strategy；prior / demand-only 一律不 mint。
/// </summary>
public sealed record ContinuityProposal(
    ContinuityProposedOutcomeKind Outcome,
    string? MatchedOccurrenceId,
    string? MatchedLogicalItemId,
    IReadOnlyList<string> SupportingEvidenceIds,
    IReadOnlyList<string> ContradictingEvidenceIds,
    IReadOnlyList<ProposedTermination> TerminatedItems,
    string Reason);

/// <summary>
/// IContinuityStrategy — owner-internal 确定性 continuity adjudication seam
/// （UWM-009 §36 / ADR-0015/0014）。实现必须确定性（同输入同 proposal）；
/// strategy 无 authority：proposal 须经 WorldModel Authority gates 才影响
/// canonical belief。
/// </summary>
public interface IContinuityStrategy
{
    ContinuityProposal Propose(ContinuityAdjudicationInput input);
}

/// <summary>
/// ContinuityDecision — owner-internal append-only decision log 条目
/// （同 AssociationLog 先例）：revision id（若 commit；null = 无 belief 变化
/// 不 commit，决策仍留痕）/ demand correlation（DemandId + SourceKind）/
/// proposed outcome（null = strategy 未被咨询——pre-gate NoCurrentCandidate）/
/// effective outcome / matched ids / reason。不进 revision aggregate，
/// 不构成第二 truth。
/// </summary>
public sealed record ContinuityDecision(
    string? RevisionId,
    string DemandId,
    ContinuityDemandSourceKind SourceKind,
    ContinuityProposedOutcomeKind? ProposedOutcome,
    ContinuityResolutionOutcome EffectiveOutcome,
    string? MatchedOccurrenceId,
    string? MatchedLogicalItemId,
    string Reason);

/// <summary>
/// ResolveContinuity 的返回：判别结果 + 结果 item id（mint / SameReferent 时
/// 非 null）+ RevisionId（结果所依据的 current revision，或 commit 后的新
/// revision id）。
/// </summary>
public sealed record ContinuityResolution(
    ContinuityResolutionOutcome Outcome,
    string? LogicalItemId,
    string RevisionId);
