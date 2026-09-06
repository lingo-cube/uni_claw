using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel;

/// <summary>
/// Uni Kernel 组合缝（Target §3.5）：仅组合 Evidence Ledger 与 World Model 两个 L2，
/// 不成为任何 Owner 的兜底（不变量 3）。pipeline 次序固定且不可合并：
/// admission → relevance → reconciliation（验收 1/4）。
/// </summary>
public sealed record KernelResult(
    AdmissionRecord Admission,
    RelevanceJudgment? Relevance,
    WorldBeliefRevision? ResultingRevision);

/// <summary>最小 Uni Kernel 边界（E2B-001 scope：仅组合上述两个 L2，无其他）。</summary>
public sealed class UniKernel
{
    private readonly EvidenceLedger _ledger;
    private readonly WorldModel _world;

    /// <summary>注入两个被组合的 L2 authority；Kernel 不持有任何平行状态。</summary>
    public UniKernel(EvidenceLedger ledger, WorldModel world)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>Current WorldBelief 透传（Kernel 不持有平行 belief）。</summary>
    public WorldBeliefRevision? CurrentBelief => _world.Current;

    /// <summary>
    /// 处理一条观察输入：
    /// rejected → 短路（无 relevance、无 reconciliation、零 belief 变化）；
    /// accepted + irrelevant → 保留 canonical record，不产 revision；
    /// accepted + relevant → Reconciliation（幂等）。
    /// </summary>
    public KernelResult Process(ObservationRecord observation)
    {
        var (admission, record) = _ledger.Admit(observation);

        // fail-closed 短路（验收 4）：rejected 不得触达 Relevance / Reconciliation
        if (admission.Decision != AdmissionDecision.Accepted || record is null)
            return new KernelResult(admission, Relevance: null, ResultingRevision: null);

        // Belief Relevance 判定（验收 1）：与 Admission 是两个独立产出
        var relevance = _world.JudgeRelevance(record);

        // irrelevant（验收 3）：canonical record 保留，不要求 revision
        if (!relevance.IsRelevant)
            return new KernelResult(admission, relevance, ResultingRevision: null);

        var before = _world.Current;
        var revision = _world.Reconcile(record, relevance);

        // 幂等（验收 8）：reconcile 未产生新 revision 时如实报告
        var resulting = ReferenceEquals(revision, before) ? null : revision;
        return new KernelResult(admission, relevance, resulting);
    }

    /// <summary>Slice 派生透传。</summary>
    public Slice DeriveSlice(string scope) => _world.DeriveSlice(scope);

    /// <summary>Slice 有效性透传。</summary>
    public bool IsSliceValid(Slice slice) => _world.IsSliceValid(slice);
}
