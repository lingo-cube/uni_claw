using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World;

/// <summary>
/// LogicalItemLifecycle（ADR-0015 四轴中的 Lifecycle 轴）：Established = 连续性
/// 在续；Ended = 仅来自 (a) supporting evidence ⊆ basis 的 referent 终止提议，
/// 或 (b) owning container 不在当前 containers 的级联（v0.1 无 container 终止
/// 操作，(b) 分支正常路径不可达，规则保留）。Ambiguous / Insufficient /
/// Contradicted / NoCurrentCandidate / demand 消失一律 ≠ Ended；状态/文本/值
/// 变化是 state claim，永不终止 continuity。
/// </summary>
public enum LogicalItemLifecycle
{
    /// <summary>连续性在续（含状态/值/文本变化——那是 state claim，不是 lifecycle）。</summary>
    Established,

    /// <summary>referent 终止（正证）或 owning container scope 级联终止。</summary>
    Ended,
}

/// <summary>
/// OccurrenceBelief — 一个 revision 内的 observed UI presentation belief
/// （UWM-009 §35 / ADR-0015）：revision-local——每个 revision 的 occurrence
/// 集合派生自触发该 revision 的那条 evidence record（替换，不从 parent 继承），
/// occurrence id 每轮新铸（内容派生、确定性）；provider node id / bbox / OCR /
/// DOM / detection id = evidence only，永不是 identity。跨 revision 携带
/// occurrence-ref 无效（stale anchor → fail-closed）。
/// </summary>
public sealed record OccurrenceBelief(
    string OccurrenceId,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    IReadOnlyList<string> EvidenceBasis);

/// <summary>
/// LogicalItemBelief — owning Container 内、demand-gated + evidence-established
/// 的 actionable logical referent 有界连续性（ADR-0015）：demand 只购买 tracking
/// eligibility，identity 只能由 accepted evidence 经 reconciliation 建立
/// （P-UW-26/32：demand 单独永不铸造）。scope ⊆ owning Container lifetime
/// （跨 Container continuity 不设计）。
/// </summary>
public sealed record LogicalItemBelief(
    string LogicalItemId,
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor,
    LogicalItemLifecycle Lifecycle,
    string? EndedReason,
    IReadOnlyList<string> EvidenceBasis);

/// <summary>
/// ProposedOccurrence — IUiObservationStrategy 提议的 occurrence（owner-internal
/// seam，无 authority）：id 与 EvidenceBasis 由 WorldModel 铸造，strategy 不参与。
/// </summary>
public sealed record ProposedOccurrence(
    string? OwningContainerId,
    string Role,
    string? SemanticDescriptor);

/// <summary>
/// IUiObservationStrategy — owner-internal 确定性 occurrence 派生缝
/// （UWM-009 §35；ADR-0015 三层模型的中间层）。输入 = 触发本 revision 的
/// accepted evidence record + previous revision；输出 proposals 经 WorldModel
/// 铸 id / 绑 basis 后成为本 revision 的 Occurrences（revision-local 替换语义）。
/// 实现必须确定性（同输入同输出——R-UW-07 replay 语义边界的前提）。
/// </summary>
public interface IUiObservationStrategy
{
    IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous);
}
