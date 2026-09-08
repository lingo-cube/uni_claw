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
    /// <summary>CurrentContainer claim subject（WorldState 语义，UWM-009 §23；subject 约定 = realization）。</summary>
    public const string CurrentContainerSubject = "ui.container.current";

    /// <summary>per-container signature claim subject 前缀（owner-derived claim；realization 约定）。</summary>
    public const string SignatureSubjectPrefix = "ui.container.signature.";

    private readonly IReadOnlySet<string> _relevanceScope;
    private readonly IAssociationStrategy? _associationStrategy;
    private readonly List<RelevanceJudgment> _relevanceLog = new();
    private readonly List<WorldBeliefRevision> _revisionHistory = new();
    private readonly List<AssociationDecision> _associationLog = new();

    /// <summary>relevance scope：本 World Model 关注的 subject 集合（结构性判定，非 AI）。</summary>
    public WorldModel(IReadOnlySet<string> relevanceScope)
        : this(relevanceScope, associationStrategy: null)
    {
    }

    /// <summary>
    /// UIWorld path（UIW-001 / UWM-009）：注入确定性 association seam。
    /// null = 既有 E2B 路径，Container Association 不启用，行为与旧版完全一致。
    /// strategy 是 owner 内部缝（非跨组件 view），无 authority——proposal 须经
    /// WorldModel Authority gates 才能影响 canonical belief。
    /// </summary>
    public WorldModel(IReadOnlySet<string> relevanceScope, IAssociationStrategy? associationStrategy)
    {
        _relevanceScope = relevanceScope;
        _associationStrategy = associationStrategy;
    }

    /// <summary>Current WorldBelief（首次 Reconciliation 前为 null）。</summary>
    public WorldBeliefRevision? Current =>
        _revisionHistory.Count == 0 ? null : _revisionHistory[^1];

    /// <summary>revision 历史（append-only 只读依据；被取代的 revision 保留为历史）。</summary>
    public IReadOnlyList<WorldBeliefRevision> RevisionHistory => _revisionHistory;

    /// <summary>每次 relevance 判定的留痕。</summary>
    public IReadOnlyList<RelevanceJudgment> RelevanceLog => _relevanceLog;

    /// <summary>
    /// 每次 Container Association 判定的留痕（owner-internal append-only，
    /// 同 RelevanceLog 先例；R-UW-02/03：revision causality 与 identity 变异
    /// decision context 的可引用载体——供未来 Trace/Replay 引用，
    /// 不进 revision aggregate，不构成第二 truth）。
    /// </summary>
    public IReadOnlyList<AssociationDecision> AssociationLog => _associationLog;

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
    /// UIW-001（UWM-009 / ADR-0012）：可选 P22 TransitionContext 仅作为
    /// Container Association 的 non-evidentiary prior；canonical belief
    /// mutation 仍只由 accepted world evidence 触发（P-UW-23），Ambiguous /
    /// Insufficient 不产生 identity 变异。
    /// </summary>
    public WorldBeliefRevision Reconcile(EvidenceRecord record, RelevanceJudgment judgment,
        TransitionContext? transitionContext = null)
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

        // P-UW-16 owner 侧执法：裸 spatial subject（无 SpatialFrame）不得进入 belief
        ValidateSpatialSubject(record.Claim.Subject);

        var state = new Dictionary<string, WorldClaim>(parent?.WorldState ?? FrozenDictionary<string, WorldClaim>.Empty);
        var graph = new List<string>(parent?.WorldGraph ?? Array.Empty<string>());
        var conflicts = new List<Conflict>(parent?.Conflicts ?? Array.Empty<Conflict>());
        var basis = new HashSet<string>(parent?.EvidenceBasis ?? Enumerable.Empty<string>()) { record.EvidenceId };

        void ApplyClaim(string subject, string value, string evidenceId)
        {
            if (!graph.Contains(subject))
                graph.Add(subject);
            if (state.TryGetValue(subject, out var established))
            {
                if (established.Value != value)
                    // 显式冲突：保留既存值与其 evidence 溯源，不静默覆盖
                    conflicts.Add(new Conflict(subject, established.Value, value, established.EvidenceId, evidenceId));
            }
            else
            {
                state[subject] = new WorldClaim(value, evidenceId);
            }
        }

        // UIW-001（UWM-009 §18 Revise 语义的最小 realization）：owner-derived
        // claims（CurrentContainer / signature）承载的是 association 决定的
        // 合法迁移，不是互相矛盾的观察——值随新 decision 替换、溯源到本次
        // evidence，decision context 由 AssociationLog 留痕；不产生 conflict。
        void ApplyDerivedClaim(string subject, string value, string evidenceId)
        {
            if (!graph.Contains(subject))
                graph.Add(subject);
            state[subject] = new WorldClaim(value, evidenceId);
        }

        ApplyClaim(record.Claim.Subject, record.Claim.Value, record.EvidenceId);

        // Container Association（UWM-009 §9 合法输入冻结：accepted evidence +
        // previous revision + P22 prior；Authority gates 见 Associate）
        var containers = new List<ContainerBelief>(parent?.Containers ?? Array.Empty<ContainerBelief>());
        var relations = new List<ContainerRelation>(parent?.Relations ?? Array.Empty<ContainerRelation>());
        if (_associationStrategy is not null)
        {
            var decision = Associate(record, transitionContext, parent, containers, relations,
                out var currentContainerId);
            if (currentContainerId is not null)
            {
                // CurrentContainer / signature 是 owner-derived WorldState claims
                // （UWM-009 §23：CurrentContainer 属 WorldState，不属 graph）
                ApplyDerivedClaim(CurrentContainerSubject, currentContainerId, record.EvidenceId);
                ApplyDerivedClaim(SignatureSubjectPrefix + currentContainerId, record.Claim.Value, record.EvidenceId);
            }
            _associationLog.Add(decision);
        }

        var number = (parent?.RevisionNumber ?? 0) + 1;
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
            Conflicts: conflicts.ToArray(),
            Containers: containers.ToArray(),
            Relations: relations.ToArray());

        _revisionHistory.Add(revision);
        return revision;
    }

    /// <summary>
    /// Container Association（UWM-009 §9/§13）：strategy proposal 经 Authority
    /// gates 后作用于 container / relation belief。gates（协议级，不委托
    /// strategy）：Matched 需 previous 存在该 identity + candidate 有非空且
    /// 全部合法的 supporting evidence + 零 contradicting evidence；New 需当前
    /// record 被 supporting evidence 背书（prior-only 一律降级 Insufficient，
    /// P-UW-21）；违规 fail-closed 降级，不产生 identity 变异。
    /// Ambiguous / Insufficient 永不 create/replace canonical identity（D6）。
    /// </summary>
    private AssociationDecision Associate(
        EvidenceRecord record,
        TransitionContext? transitionContext,
        WorldBeliefRevision? parent,
        List<ContainerBelief> containers,
        List<ContainerRelation> relations,
        out string? currentContainerId)
    {
        currentContainerId = null;
        var proposal = _associationStrategy!.Propose(new AssociationInput(parent, record, transitionContext));
        var number = (parent?.RevisionNumber ?? 0) + 1;

        var effective = proposal.Kind;
        string? matchedId = null;
        string? establishedId = null;
        var reason = proposal.Reason;
        var validEvidence = new HashSet<string>(parent?.EvidenceBasis ?? Enumerable.Empty<string>())
            { record.EvidenceId };

        if (proposal.Kind == AssociationDispositionKind.Matched)
        {
            var id = proposal.MatchedContainerId;
            var candidate = proposal.Candidates.FirstOrDefault(c => c.CandidateContainerId == id);
            var blocked = id is null
                || parent is null
                || containers.All(c => c.Identity.ContainerId != id)
                || candidate is null
                || candidate.SupportingEvidenceIds.Count == 0
                || candidate.ContradictingEvidenceIds.Count > 0
                || !candidate.SupportingEvidenceIds.All(validEvidence.Contains);
            if (blocked)
            {
                effective = AssociationDispositionKind.Insufficient;
                reason = $"authority-blocked-matched:{proposal.Reason}";
            }
            else
            {
                matchedId = id!;
                var index = containers.FindIndex(c => c.Identity.ContainerId == id);
                containers[index] = new ContainerBelief(
                    containers[index].Identity,
                    containers[index].EvidenceBasis.Append(record.EvidenceId).Distinct().ToArray());
                currentContainerId = id;
            }
        }
        else if (proposal.Kind == AssociationDispositionKind.New)
        {
            var backed = proposal.Candidates.SelectMany(c => c.SupportingEvidenceIds)
                .Contains(record.EvidenceId);
            if (!backed)
            {
                effective = AssociationDispositionKind.Insufficient;
                reason = $"authority-blocked-new:{proposal.Reason}";
            }
            else
            {
                establishedId = MintContainerIdentity(record.EvidenceId);
                containers.Add(new ContainerBelief(
                    new ContainerIdentity(establishedId), new[] { record.EvidenceId }));
                currentContainerId = establishedId;
            }
        }

        var knownIds = containers.Select(c => c.Identity.ContainerId).ToHashSet();
        foreach (var relation in proposal.Relations)
        {
            if (relation.SupportingEvidenceIds.Count == 0
                || !relation.SupportingEvidenceIds.All(validEvidence.Contains)
                || !knownIds.Contains(relation.SourceContainerId)
                || !knownIds.Contains(relation.TargetContainerId))
                continue;
            relations.Add(new ContainerRelation(
                relation.Kind, relation.SourceContainerId, relation.TargetContainerId,
                relation.SupportingEvidenceIds.ToArray()));
        }

        return new AssociationDecision(
            $"rev-{number}", record.EvidenceId,
            transitionContext?.AttemptCorrelation, transitionContext?.Strength,
            proposal.Kind, effective, matchedId, establishedId,
            proposal.Candidates.ToArray(), reason);
    }

    /// <summary>
    /// ContainerIdentity 铸造（sole authority 内部）：establishing EvidenceId
    /// 内容派生，确定性（R-UW-01：replay / counterfactual 分支语义稳定）；
    /// 铸造算法本身属 realization。
    /// </summary>
    private static string MintContainerIdentity(string evidenceId) => "ctr-" + evidenceId[3..15];

    /// <summary>
    /// P-UW-16 owner 侧执法：spatial claim subject 必须显式命名 SpatialFrame
    /// （约定 realization 形式 spatial.&lt;frame&gt;.*）；裸 spatial subject
    /// fail-closed，不进入 belief。
    /// </summary>
    private static void ValidateSpatialSubject(string subject)
    {
        if (subject.StartsWith("spatial.", StringComparison.Ordinal)
            && subject.Split('.').Length < 3)
            throw new ArgumentException(
                "spatial value without SpatialFrame is invalid protocol semantics (P-UW-16)",
                nameof(subject));
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
