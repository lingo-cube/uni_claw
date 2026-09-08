using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World;

/// <summary>
/// ContainerIdentity — World Model 对持续存在 UI world entity 的 canonical
/// identity（UWM-009 §5，P-UW-01/20）。≠ screenshot / OCR / bounding-box /
/// DOM / Observation identity。由 World Model（sole authority）铸造；
/// 铸造算法（当前 = establishing EvidenceId 内容派生前缀）属 realization，
/// 保证确定性（R-UW-01：replay 语义稳定）。
/// </summary>
public sealed record ContainerIdentity(string ContainerId);

/// <summary>
/// ContainerBelief — 一个 revision 内的 container belief：identity + 支撑其
/// 存在/延续的 accepted evidence basis（R-UW-03：identity 变异可追溯）。
/// </summary>
public sealed record ContainerBelief(
    ContainerIdentity Identity,
    IReadOnlyList<string> EvidenceBasis);

/// <summary>
/// Container relation kind（UWM-009 §6 / D3 最小语义；taxonomy 不冻结）。
/// </summary>
public enum ContainerRelationKind
{
    /// <summary>Source contains Target（graph relation，即使生命周期很短）。</summary>
    Contains,

    /// <summary>Source overlays Target（graph relation，即使生命周期很短）。</summary>
    Overlays,
}

/// <summary>
/// ContainerRelation — revision-bound、evidence-backed 的世界实体间关系
/// belief（D3：按 semantic kind 分界；graph relation ≠ timeless structural
/// truth）。随 revision 携带，不成为跨组件公共协议。
/// </summary>
public sealed record ContainerRelation(
    ContainerRelationKind Kind,
    string SourceContainerId,
    string TargetContainerId,
    IReadOnlyList<string> EvidenceBasis);

/// <summary>
/// AssociationDisposition 四分类（UWM-009 §13 / D6）：一次 Container
/// Association 的判别结果轴，与 claim 认知轴（Known/Absent/Unknown/
/// Conflicting）分属两条 epistemic 轴。
/// </summary>
public enum AssociationDispositionKind
{
    /// <summary>observation 足以支持 = 既有 canonical ContainerIdentity。</summary>
    Matched,

    /// <summary>observation 足以支持 ≠ relevant known candidate set（低相似 ≠ New）。</summary>
    New,

    /// <summary>observation 充分，但存在多个成立的 identity interpretation。</summary>
    Ambiguous,

    /// <summary>observation 缺乏完成 identity discrimination 的判别信息。</summary>
    Insufficient,
}

/// <summary>
/// AssociationCandidate（UWM-009 §11）：仅 UIWorld 内部由 accepted Evidence
/// 推导；外部 capability 直连 ingress = DEFER — NO CURRENT BUYER。
/// Supporting / Contradicting 分开携带（R-UW-05：不得压平成单一 score）。
/// </summary>
public sealed record AssociationCandidate(
    string CandidateContainerId,
    IReadOnlyList<string> SupportingEvidenceIds,
    IReadOnlyList<string> ContradictingEvidenceIds);

/// <summary>strategy 提议的 relation（gate 校验 evidence backing 后入 belief）。</summary>
public sealed record ProposedRelation(
    ContainerRelationKind Kind,
    string SourceContainerId,
    string TargetContainerId,
    IReadOnlyList<string> SupportingEvidenceIds);

/// <summary>
/// Association 输入（owner 内部 seam 的输入，非跨组件 view）：previous
/// revision + 当前 accepted evidence + 可选 P22 TransitionContext（仅 prior）。
/// </summary>
public sealed record AssociationInput(
    WorldBeliefRevision? Previous,
    EvidenceRecord Current,
    TransitionContext? Transition);

/// <summary>
/// Strategy proposal：候选判别结果 + decision context。Authority gates
/// （evidence backing / contradiction-blocks-matched / prior-only blocked）
/// 在 WorldModel 边界强制，不委托 strategy。
/// </summary>
public sealed record AssociationProposal(
    AssociationDispositionKind Kind,
    string? MatchedContainerId,
    IReadOnlyList<AssociationCandidate> Candidates,
    IReadOnlyList<ProposedRelation> Relations,
    string Reason);

/// <summary>
/// IAssociationStrategy — UIWorld 内部的确定性 association seam（UWM-009
/// §10/§16：识别算法/模型/阈值/Top-K 全部不冻结；可替换 realization）。
/// 实现必须确定性（同输入同 proposal——R-UW-07 replay 语义边界的前提）；
/// strategy 无 authority：proposal 须经 WorldModel gates 才影响 canonical belief。
/// </summary>
public interface IAssociationStrategy
{
    AssociationProposal Propose(AssociationInput input);
}

/// <summary>
/// AssociationDecision — owner-internal append-only decision log 条目
/// （同 RelevanceLog 先例；R-UW-02/03：revision causality 与 identity 变异
/// 的 decision context 载体，供未来 Trace/Replay 引用，不进 revision
/// aggregate、不构成第二 truth）。
/// </summary>
public sealed record AssociationDecision(
    string RevisionId,
    string EvidenceId,
    string? TransitionCorrelation,
    TransitionStrength? TransitionStrength,
    AssociationDispositionKind ProposedKind,
    AssociationDispositionKind EffectiveKind,
    string? MatchedContainerId,
    string? EstablishedContainerId,
    IReadOnlyList<AssociationCandidate> Candidates,
    string Reason);
