using System.Collections.Frozen;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World;

/// <summary>
/// World Model — sole Belief / Reconciliation Authority（Target §12，不变量 14/15）。
/// 唯一拥有 Current WorldBelief、Slice 派生与 Reconciliation。
/// Reconciliation 只接受 accepted Evidence；plan / expectation / goal /
/// hypothesis / intent 没有进入本类型的任何输入路径（不变量 16，验收 7）。
/// </summary>
public sealed class WorldModel
{
    private readonly IReadOnlySet<string> _relevanceScope;
    private readonly List<RelevanceJudgment> _relevanceLog = new();
    private readonly List<WorldBeliefRevision> _revisionHistory = new();

    /// <summary>relevance scope：本 World Model 关注的 subject 集合（结构性判定，非 AI）。</summary>
    public WorldModel(IReadOnlySet<string> relevanceScope)
        => _relevanceScope = relevanceScope;

    /// <summary>Current WorldBelief（首次 Reconciliation 前为 null）。</summary>
    public WorldBeliefRevision? Current =>
        _revisionHistory.Count == 0 ? null : _revisionHistory[^1];

    /// <summary>revision 历史（append-only 只读依据；被取代的 revision 保留为历史）。</summary>
    public IReadOnlyList<WorldBeliefRevision> RevisionHistory => _revisionHistory;

    /// <summary>每次 relevance 判定的留痕。</summary>
    public IReadOnlyList<RelevanceJudgment> RelevanceLog => _relevanceLog;

    /// <summary>Belief Relevance 判定（独立于 Admission 的第二个产出）。
    /// ING-006 D4：kind-aware——AttemptReport 定义性非 world-relevant
    /// （kind 门压过 subject-scope 匹配，不再依赖命名空间巧合）。</summary>
    public RelevanceJudgment JudgeRelevance(EvidenceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string reason;
        bool isRelevant;
        if (record.Kind == IngressKind.AttemptReport)
        {
            isRelevant = false;
            reason = "attempt-report-not-world-relevant";
        }
        else
        {
            isRelevant = _relevanceScope.Contains(record.Claim.Subject);
            reason = isRelevant
                ? "subject-in-relevance-scope"
                : "subject-out-of-relevance-scope";
        }

        var judgment = new RelevanceJudgment(record.EvidenceId, isRelevant, reason);
        _relevanceLog.Add(judgment);
        return judgment;
    }

    /// <summary>
    /// Reconciliation：accepted Evidence → 新 immutable revision（验收 2）。
    /// basis 已含该 EvidenceId → 幂等返回 current，不产生新 revision（验收 8）。
    /// 同 subject 不相容 claim → 显式 Conflicts 条目，不静默覆盖（验收 5）。
    /// </summary>
    public WorldBeliefRevision Reconcile(EvidenceRecord record, RelevanceJudgment judgment)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(judgment);
        if (judgment.EvidenceId != record.EvidenceId)
            throw new ArgumentException("relevance judgment 不属于该 evidence record", nameof(judgment));
        if (!judgment.IsRelevant)
            throw new InvalidOperationException("Reconciliation 只接受 relevant accepted Evidence");

        var parent = Current;

        // 幂等（验收 8）：同一 canonical record 已在 current basis 中
        if (parent is not null && parent.EvidenceBasis.Contains(record.EvidenceId))
            return parent;

        var number = (parent?.RevisionNumber ?? 0) + 1;
        var state = new Dictionary<string, WorldClaim>(parent?.WorldState ?? FrozenDictionary<string, WorldClaim>.Empty);
        var graph = new List<string>(parent?.WorldGraph ?? Array.Empty<string>());
        var conflicts = new List<Conflict>(parent?.Conflicts ?? Array.Empty<Conflict>());
        var basis = new HashSet<string>(parent?.EvidenceBasis ?? Enumerable.Empty<string>()) { record.EvidenceId };

        var subject = record.Claim.Subject;
        if (!graph.Contains(subject))
            graph.Add(subject);

        if (state.TryGetValue(subject, out var established))
        {
            if (established.Value != record.Claim.Value)
            {
                // 显式冲突：保留既存值与其 evidence 溯源，不静默覆盖
                conflicts.Add(new Conflict(
                    subject, established.Value, record.Claim.Value,
                    established.EvidenceId, record.EvidenceId));
            }
        }
        else
        {
            state[subject] = new WorldClaim(record.Claim.Value, record.EvidenceId);
        }

        var freshness = new FreshnessBasis(
            (parent?.FreshnessBasis.AsOf ?? DateTimeOffset.MinValue) > record.Provenance.CaptureTime
                ? parent!.FreshnessBasis.AsOf
                : record.Provenance.CaptureTime);

        var revision = new WorldBeliefRevision(
            RevisionId: $"rev-{number}",
            ParentRevisionId: parent?.RevisionId,
            RevisionNumber: number,
            WorldState: state.ToFrozenDictionary(),
            WorldGraph: graph.ToArray(),
            EvidenceBasis: basis.ToFrozenSet(),
            FreshnessBasis: freshness,
            Uncertainty: new Uncertainty(conflicts.Count),
            Conflicts: conflicts.ToArray());

        _revisionHistory.Add(revision);
        return revision;
    }

    /// <summary>从 Current revision 派生 scoped 只读 Slice；无 current 时无法派生。</summary>
    public Slice DeriveSlice(string scope)
    {
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 Slice");
        var projection = current.WorldState
            .Where(kv => kv.Key == scope || kv.Key.StartsWith(scope + ".", StringComparison.Ordinal))
            .ToFrozenDictionary(kv => kv.Key, kv => kv.Value.Value);
        return new Slice(current.RevisionId, scope, current.FreshnessBasis, projection);
    }

    /// <summary>
    /// 为 Effect Boundary 派生 BindingView（ADR-0011 / EXP-008 D6）：
    /// 消费点即时派生的 ephemeral projection，scope = candidate target
    /// subject（null → 无 claim fact）。只表达 Owner-owned facts，
    /// 四态拒绝判定权在 Effect Boundary。无 current → fail-closed。
    /// </summary>
    public BindingView DeriveBindingView(string? subject)
    {
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 BindingView");
        return new BindingView(
            current.RevisionId,
            current.RevisionNumber,
            HasTargetSubjectClaim: subject is not null && current.WorldState.ContainsKey(subject));
    }

    /// <summary>
    /// 为 RuntimeAssurance.Judge 派生 ActionAssuranceView（EXP-008 D7）：
    /// scope = intent target subject（null → 无冲突 fact）。HasConflictOnTarget
    /// 是 belief fact（该 subject 上存在冲突条目），no-unresolved-conflict
    /// 判定权在 Assurance。无 current → fail-closed。
    /// </summary>
    public ActionAssuranceView DeriveActionAssuranceView(string? subject)
    {
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 ActionAssuranceView");
        return new ActionAssuranceView(
            current.RevisionId,
            current.RevisionNumber,
            current.FreshnessBasis,
            HasConflictOnTarget: subject is not null && current.Conflicts.Any(c => c.Subject == subject));
    }

    /// <summary>
    /// 为 Assurance obligation / outcome 路径派生 OutcomeAssuranceView
    /// （EXP-008 D8）：claims / conflicts 按 obligation subjects scope
    /// （空 subject 占位 obligation 不入 scope，查找本来即 miss，行为
    /// 等价）；BasisEvidenceIds 为全量 refs（proof 载荷真实 buyer）。
    /// 无 current → fail-closed。
    /// </summary>
    public OutcomeAssuranceView DeriveOutcomeAssuranceView(IEnumerable<string> subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 OutcomeAssuranceView");

        var scope = subjects
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet();
        var claims = current.WorldState
            .Where(kv => scope.Contains(kv.Key))
            .ToFrozenDictionary(
                kv => kv.Key,
                kv => new ScopedClaim(kv.Value.Value, kv.Value.EvidenceId));
        var conflicts = current.Conflicts
            .Where(c => scope.Contains(c.Subject))
            .ToArray();

        return new OutcomeAssuranceView(
            current.RevisionId,
            current.Uncertainty.ConflictingClaimCount,
            Claims: claims,
            Conflicts: conflicts,
            BasisEvidenceIds: current.EvidenceBasis);
    }

    /// <summary>Slice 有效性 = 派生判定（source revision 是否仍为 current；
    /// currency 属 World Model 侧；freshness 充分性属消费侧 Freshness
    /// Judgment，不在此判定——ADR-0010 / FRS-007 D4；验收 6）。</summary>
    public bool IsSliceValid(Slice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        return Current is not null && slice.SourceRevisionId == Current.RevisionId;
    }
}
