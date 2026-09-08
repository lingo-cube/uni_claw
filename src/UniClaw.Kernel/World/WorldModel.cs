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
    private readonly IUiObservationStrategy? _observationStrategy;
    private readonly IContinuityStrategy? _continuityStrategy;
    private readonly List<RelevanceJudgment> _relevanceLog = new();
    private readonly List<WorldBeliefRevision> _revisionHistory = new();
    private readonly List<AssociationDecision> _associationLog = new();
    private readonly List<ContinuityDemand> _continuityDemands = new();
    private readonly List<ContinuityDecision> _continuityLog = new();

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
    /// UIW-003（UWM-009 v0.3 §35 / ADR-0015/0014）追加两个可选 seam：
    /// observationStrategy（occurrence 派生）与 continuityStrategy（continuity
    /// adjudication）；均 null（既有调用形态）时行为逐字节不变。
    /// </summary>
    public WorldModel(IReadOnlySet<string> relevanceScope, IAssociationStrategy? associationStrategy = null,
        IUiObservationStrategy? observationStrategy = null, IContinuityStrategy? continuityStrategy = null)
    {
        _relevanceScope = relevanceScope;
        _associationStrategy = associationStrategy;
        _observationStrategy = observationStrategy;
        _continuityStrategy = continuityStrategy;
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

    /// <summary>
    /// Continuity demand registry（owner-internal、非 revision 化，ADR-0014）：
    /// List + 只读视图；demand 状态变化不产生 revision（P-UW-32）。
    /// </summary>
    public IReadOnlyList<ContinuityDemand> ContinuityDemands => _continuityDemands;

    /// <summary>
    /// 每次 continuity adjudication 判定的留痕（owner-internal append-only，
    /// 同 AssociationLog 先例；含 demand correlation 与 proposed/effective
    /// outcome，不进 revision aggregate、不构成第二 truth）。
    /// </summary>
    public IReadOnlyList<ContinuityDecision> ContinuityLog => _continuityLog;

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

        // UIW-003（UWM-009 v0.3 §35 / ADR-0015）：ObservationOccurrence 是
        // revision-local——每个 revision 的 occurrence 集合派生自触发本 revision 的
        // 这条 evidence record（替换，不从 parent 继承），occurrence id 每轮新铸
        // （内容派生、确定性）；EvidenceBasis = {该 EvidenceId}。无 strategy →
        // null（旧语义不变）。LogicalItem belief 跨 evidence revision 延续
        //（continuity 是例外路径，ADR-0015）。
        IReadOnlyList<OccurrenceBelief>? occurrences = null;
        if (_observationStrategy is not null)
        {
            occurrences = _observationStrategy.Derive(record, parent)
                .Select((proposed, index) => new OccurrenceBelief(
                    MintOccurrenceIdentity(record.EvidenceId, index),
                    proposed.OwningContainerId, proposed.Role, proposed.SemanticDescriptor,
                    new[] { record.EvidenceId }))
                .ToArray();
        }
        var logicalItems = parent?.LogicalItems;

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
            Relations: relations.ToArray(),
            Occurrences: occurrences,
            LogicalItems: logicalItems);

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
    /// ObservationOccurrence identity 铸造（sole authority 内部）：establishing
    /// EvidenceId 内容派生 + proposal 序位，确定性；每轮 revision 新铸
    /// （revision-local，R-UW-01）。
    /// </summary>
    private static string MintOccurrenceIdentity(string evidenceId, int index) =>
        $"occ-{evidenceId[3..15]}-{index}";

    /// <summary>
    /// LogicalItem identity 铸造（sole authority 内部）：源自 matched occurrence 的
    /// 内容派生 id（确定性 replay 稳定）；铸造算法本身属 realization。
    /// </summary>
    private static string MintLogicalItemIdentity(string occurrenceId) => "li-" + occurrenceId[4..];

    // ---- UIW-003：continuity demand registry + adjudication（UWM-009 v0.3 §36–§38 / ADR-0015/0014）----

    /// <summary>
    /// 登记 continuity demand（P23 ResolveContinuity 侧的 standing 状态）。
    /// owner-internal、非 revision 化：不产生任何 WorldBeliefRevision（P-UW-32），
    /// 零 belief 副作用。同 DemandId 幂等（复用既有登记）。AnchorOccurrenceId
    /// 非 null 时必须存在于 Current.Occurrences——跨 revision 携带的 occurrence-ref
    /// 无效，stale anchor fail-closed（ADR-0014 timing）；无 anchor
    /// （descriptor-scoped）合法；LogicalItemId 允许为空（standing demand 可早于
    /// identity，但不能代替 identity evidence）。
    /// </summary>
    public DemandHandle RegisterContinuityDemand(ContinuityDemand demand)
    {
        ArgumentNullException.ThrowIfNull(demand);
        var existing = _continuityDemands.FirstOrDefault(d => d.DemandId == demand.DemandId);
        if (existing is not null)
            return new DemandHandle(existing.DemandId);

        if (demand.AnchorOccurrenceId is not null)
        {
            var current = Current;
            if (current?.Occurrences is null
                || current.Occurrences.All(o => o.OccurrenceId != demand.AnchorOccurrenceId))
                throw new InvalidOperationException(
                    $"continuity demand anchor occurrence '{demand.AnchorOccurrenceId}' 不在 current revision"
                    + "（stale anchor：demand 必须在源 occurrence 仍属 current revision 时登记，ADR-0014 timing）");
        }

        _continuityDemands.Add(demand);
        return new DemandHandle(demand.DemandId);
    }

    /// <summary>
    /// 撤销 demand：只删该 demand 本身（ADR-0014）——不删除 LogicalItem、不改历史
    /// belief、不产生 Ended、零 revision 副作用。不存在的 demandId 为 no-op
    /// （幂等撤销，本实现选择；Revoke 不是 identity 变异路径，无需 fail-closed）。
    /// </summary>
    public void RevokeContinuityDemand(string demandId) =>
        _continuityDemands.RemoveAll(d => d.DemandId == demandId);

    /// <summary>
    /// Maintenance 派生查询（ADR-0015 四轴中的 Maintenance 轴）：active demand
    /// 引用该 item 即 Hot。纯计算，不落任何存储字段、不产生副作用。
    /// </summary>
    public bool IsHotItem(string logicalItemId) =>
        _continuityDemands.Any(d => d.LogicalItemId == logicalItemId);

    /// <summary>
    /// ResolveContinuity（P23 双模缝的 demand 侧，ADR-0014）：demand + accepted
    /// evidence → continuity adjudication。Authority gates（协议级，不委托
    /// strategy）：ReferenceEstablished 需候选 occurrence + supporting 非空且
    /// ⊆ Current.EvidenceBasis + 零 contradicting；SameReferent 需既有 item +
    /// 候选 + 同上（反证在场 → Contradicted，其余违规 → Insufficient）；
    /// Contradicted 需非空 supporting ⊆ basis（无据反证 → Insufficient）；
    /// prior / demand-only 一律不 mint（P-UW-26/32）。mint / SameReferent 延伸 /
    /// Ended = 新 revision commit（EvidenceBasis 集合不变，仅 LogicalItems 变化，
    /// Occurrences 沿用 Current）；无 belief 变化的判别不 commit，但决策 append
    /// ContinuityLog。无候选 occurrence → NoCurrentCandidate（pre-gate，不经
    /// strategy——无合法 adjudication 输入，零 belief/lifecycle 副作用）。
    /// </summary>
    public ContinuityResolution ResolveContinuity(DemandHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var demand = _continuityDemands.FirstOrDefault(d => d.DemandId == handle.DemandId)
            ?? throw new InvalidOperationException(
                $"continuity demand '{handle.DemandId}' 不存在或已撤销");
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法 ResolveContinuity");

        var candidates = (current.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Select(o => new ContinuityCandidateOccurrence(
                o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                o.EvidenceBasis.ToArray(), Array.Empty<string>()))
            .ToArray();
        var existingItems = (current.LogicalItems ?? Array.Empty<LogicalItemBelief>())
            .Select(i => new ContinuityCandidateItem(
                i.LogicalItemId, i.OwningContainerId, i.Role, i.SemanticDescriptor,
                i.Lifecycle, i.EvidenceBasis.ToArray()))
            .ToArray();

        // pre-gate：无候选 occurrence → NoCurrentCandidate（零副作用，S8）
        if (candidates.Length == 0)
        {
            var none = new ContinuityResolutionOutcome(ContinuityResolutionOutcomeKind.NoCurrentCandidate);
            _continuityLog.Add(new ContinuityDecision(
                RevisionId: null, demand.DemandId, demand.SourceKind,
                ProposedOutcome: null, none, MatchedOccurrenceId: null, MatchedLogicalItemId: null,
                Reason: "no-current-candidate"));
            return new ContinuityResolution(none, LogicalItemId: null, current.RevisionId);
        }

        if (_continuityStrategy is null)
            throw new InvalidOperationException("未注入 IContinuityStrategy，无法 ResolveContinuity");

        var proposal = _continuityStrategy.Propose(
            new ContinuityAdjudicationInput(demand, current, candidates, existingItems));
        var validEvidence = current.EvidenceBasis;

        bool SupportBacked() => proposal.SupportingEvidenceIds.Count > 0
            && proposal.SupportingEvidenceIds.All(validEvidence.Contains);
        bool HasContradiction() => proposal.ContradictingEvidenceIds.Count > 0;
        bool OccurrenceExists() => proposal.MatchedOccurrenceId is not null
            && candidates.Any(c => c.OccurrenceId == proposal.MatchedOccurrenceId);
        bool ItemExists() => proposal.MatchedLogicalItemId is not null
            && existingItems.Any(i => i.LogicalItemId == proposal.MatchedLogicalItemId);

        // Authority gates（fail-closed 降级，不产生 identity / lifecycle 变异）
        var effective = proposal.Outcome;
        var reason = proposal.Reason;
        if (proposal.Outcome == ContinuityProposedOutcomeKind.ReferenceEstablished)
        {
            if (!OccurrenceExists() || !SupportBacked() || HasContradiction())
            {
                effective = ContinuityProposedOutcomeKind.Insufficient;
                reason = $"authority-blocked-reference-established:{proposal.Reason}";
            }
        }
        else if (proposal.Outcome == ContinuityProposedOutcomeKind.SameReferent)
        {
            if (HasContradiction())
            {
                effective = ContinuityProposedOutcomeKind.Contradicted;
                reason = $"authority-contradicted:{proposal.Reason}";
            }
            else if (!ItemExists() || !OccurrenceExists() || !SupportBacked())
            {
                effective = ContinuityProposedOutcomeKind.Insufficient;
                reason = $"authority-blocked-same-referent:{proposal.Reason}";
            }
        }
        else if (proposal.Outcome == ContinuityProposedOutcomeKind.Contradicted)
        {
            if (!SupportBacked())
            {
                effective = ContinuityProposedOutcomeKind.Insufficient;
                reason = $"authority-blocked-contradicted:{proposal.Reason}";
            }
        }

        // 作用面：仅 mint / SameReferent 延伸 / Ended 改动 LogicalItems
        var items = (current.LogicalItems ?? Array.Empty<LogicalItemBelief>()).ToList();
        string? matchedOccurrenceId = null;
        string? matchedItemId = null;
        string? resultItemId = null;
        var changed = false;

        if (effective == ContinuityProposedOutcomeKind.ReferenceEstablished)
        {
            matchedOccurrenceId = proposal.MatchedOccurrenceId;
            var occurrence = candidates.First(c => c.OccurrenceId == matchedOccurrenceId);
            var itemId = MintLogicalItemIdentity(matchedOccurrenceId!);
            var index = items.FindIndex(i => i.LogicalItemId == itemId);
            if (index < 0)
            {
                items.Add(new LogicalItemBelief(
                    itemId, occurrence.OwningContainerId, demand.Role, demand.SemanticDescriptor,
                    LogicalItemLifecycle.Established, EndedReason: null,
                    proposal.SupportingEvidenceIds.ToArray()));
            }
            else
            {
                // 同 occurrence 的重铸（多 demand 场景）：合并 basis，不重复铸造
                items[index] = items[index] with
                {
                    EvidenceBasis = items[index].EvidenceBasis
                        .Concat(proposal.SupportingEvidenceIds).Distinct().ToArray()
                };
            }
            resultItemId = itemId;
            matchedItemId = itemId;
            changed = true;
        }
        else if (effective == ContinuityProposedOutcomeKind.SameReferent)
        {
            matchedOccurrenceId = proposal.MatchedOccurrenceId;
            matchedItemId = proposal.MatchedLogicalItemId;
            var index = items.FindIndex(i => i.LogicalItemId == matchedItemId);
            var merged = items[index].EvidenceBasis
                .Concat(proposal.SupportingEvidenceIds).Distinct().ToArray();
            changed = merged.Length > items[index].EvidenceBasis.Count;
            items[index] = items[index] with { EvidenceBasis = merged };
            resultItemId = matchedItemId;
        }
        else if (effective == ContinuityProposedOutcomeKind.Contradicted)
        {
            // Contradicted 只证伪候选：item 不 Ended、presence 不动、不 commit
            matchedItemId = proposal.MatchedLogicalItemId;
        }

        if (effective is ContinuityProposedOutcomeKind.ReferenceEstablished
            or ContinuityProposedOutcomeKind.SameReferent)
        {
            // referent 终止（ADR-0015：Ended 仅来自正面 lifecycle evidence）：
            // supporting 非空且 ⊆ basis 才生效；无据提议静默不生效（item 保持 Established）
            foreach (var termination in proposal.TerminatedItems)
            {
                var index = items.FindIndex(i => i.LogicalItemId == termination.LogicalItemId);
                if (index < 0
                    || termination.SupportingEvidenceIds.Count == 0
                    || !termination.SupportingEvidenceIds.All(validEvidence.Contains))
                    continue;
                if (items[index].Lifecycle != LogicalItemLifecycle.Ended)
                {
                    items[index] = items[index] with
                    {
                        Lifecycle = LogicalItemLifecycle.Ended,
                        EndedReason = "referent-terminated"
                    };
                    changed = true;
                }
            }

            // owning container 缺失级联（§38 scope ⊆ Container lifetime）：v0.1 无
            // container 终止操作，正常路径不可达；item 的 OwningContainerId 不在
            // 当前 containers 时触发（手工构造缺失 container 时可达，见 S10c）
            var knownContainers = (current.Containers ?? Array.Empty<ContainerBelief>())
                .Select(c => c.Identity.ContainerId).ToHashSet();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].OwningContainerId is { } owner
                    && !knownContainers.Contains(owner)
                    && items[i].Lifecycle != LogicalItemLifecycle.Ended)
                {
                    items[i] = items[i] with
                    {
                        Lifecycle = LogicalItemLifecycle.Ended,
                        EndedReason = "container-scope-ended"
                    };
                    changed = true;
                }
            }
        }

        string revisionId = current.RevisionId;
        string? committedRevisionId = null;
        if (changed)
        {
            var revision = CommitContinuityRevision(items);
            revisionId = revision.RevisionId;
            committedRevisionId = revision.RevisionId;
        }

        // registry 维护（owner-internal，非 belief）：demand 绑定结果 item
        //（IsHotItem 派生与多 buyer 生命周期依据，ADR-0014）
        if (resultItemId is not null && demand.LogicalItemId is null)
        {
            var dIndex = _continuityDemands.FindIndex(d => d.DemandId == demand.DemandId);
            _continuityDemands[dIndex] = demand with { LogicalItemId = resultItemId };
        }

        ContinuityResolutionOutcome outcome = effective switch
        {
            ContinuityProposedOutcomeKind.ReferenceEstablished => new(
                ContinuityResolutionOutcomeKind.ReferenceEstablished, LogicalItemId: resultItemId),
            ContinuityProposedOutcomeKind.SameReferent => new(
                ContinuityResolutionOutcomeKind.Adjudicated,
                ContinuityAdjudicationOutcomeKind.SameReferent, resultItemId),
            ContinuityProposedOutcomeKind.Ambiguous => new(
                ContinuityResolutionOutcomeKind.Adjudicated, ContinuityAdjudicationOutcomeKind.Ambiguous),
            ContinuityProposedOutcomeKind.Insufficient => new(
                ContinuityResolutionOutcomeKind.Adjudicated, ContinuityAdjudicationOutcomeKind.Insufficient),
            ContinuityProposedOutcomeKind.Contradicted => new(
                ContinuityResolutionOutcomeKind.Adjudicated, ContinuityAdjudicationOutcomeKind.Contradicted),
            _ => throw new InvalidOperationException($"未知 ContinuityProposedOutcomeKind: {effective}"),
        };

        _continuityLog.Add(new ContinuityDecision(
            committedRevisionId, demand.DemandId, demand.SourceKind,
            proposal.Outcome, outcome, matchedOccurrenceId, matchedItemId, reason));
        return new ContinuityResolution(outcome, resultItemId, revisionId);
    }

    /// <summary>
    /// Continuity commit：EvidenceBasis 集合不变、WorldState / Graph / Conflicts /
    /// Containers / Relations / Occurrences 均沿用 Current，仅 LogicalItems 变化；
    /// ParentRevisionId 链正确，历史保留。
    /// </summary>
    private WorldBeliefRevision CommitContinuityRevision(IReadOnlyList<LogicalItemBelief> logicalItems)
    {
        var parent = Current!;
        var number = parent.RevisionNumber + 1;
        var revision = new WorldBeliefRevision(
            RevisionId: $"rev-{number}",
            ParentRevisionId: parent.RevisionId,
            RevisionNumber: number,
            parent.WorldState, parent.WorldGraph, parent.EvidenceBasis,
            parent.FreshnessBasis, parent.Uncertainty, parent.Conflicts,
            parent.Containers, parent.Relations,
            parent.Occurrences, logicalItems);
        _revisionHistory.Add(revision);
        return revision;
    }

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

    /// <summary>
    /// P23 缝出面（UIW-004 / UWM-009 v0.3 §41）：按 TargetDescriptor 对
    /// Current.Occurrences 做机械确定性匹配（Role 相等 ∧ container 相等(若给)
    /// ∧ descriptor 相等(若给)），0/1/N → NoCandidate / UniqueCandidate /
    /// MultipleCandidates；Current null 或 Occurrences null（无 observation
    /// seam）→ ScopeProjectionUnavailable（fail-closed：投影不可用，不是
    /// 「不存在」）。纯只读：零 log、零 registry、零 revision 副作用
    /// （candidates 的 SourceRevisionId = Current.RevisionId，消费方据此判
    /// stale）。绑定认定权在 Effect Boundary，不在此。
    /// </summary>
    public CurrentGroundingView ResolveCurrent(TargetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var current = Current;
        if (current is null || current.Occurrences is null)
            return new CurrentGroundingView(
                current?.RevisionId ?? string.Empty,
                descriptor.OwningContainerId,
                CurrentCandidateSetResultKind.ScopeProjectionUnavailable,
                Array.Empty<CandidateOccurrenceFact>());

        var candidates = current.Occurrences
            .Where(o => o.Role == descriptor.Role
                && (descriptor.OwningContainerId is null || o.OwningContainerId == descriptor.OwningContainerId)
                && (descriptor.SemanticDescriptor is null || o.SemanticDescriptor == descriptor.SemanticDescriptor))
            .Select(o => new CandidateOccurrenceFact(
                o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                current.RevisionId))
            .ToArray();
        var result = candidates.Length switch
        {
            0 => CurrentCandidateSetResultKind.NoCandidate,
            1 => CurrentCandidateSetResultKind.UniqueCandidate,
            _ => CurrentCandidateSetResultKind.MultipleCandidates,
        };
        return new CurrentGroundingView(current.RevisionId, descriptor.OwningContainerId, result, candidates);
    }

    /// <summary>
    /// 从 Current revision 派生 container-anchored 只读 Slice（UIW-004：
    /// scope 锚定 RootContainerIdentity）。root 必须存在于 Current.Containers，
    /// InScope（默认 {root}）须 ⊆ Current containers id——违规 fail-closed
    /// （InvalidOperationException），无 current 同现 fail-closed。
    /// Occurrences = owner ∈ InScope 的 occurrence 景观；ScopedClaims =
    /// subject 以 &lt;containerId&gt;. 为前缀的 WorldState 条目（分区兼容
    /// 通道，realization 约定）。
    /// </summary>
    public Slice DeriveSlice(string rootContainerId, IReadOnlyList<string>? inScopeContainerIds = null)
    {
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 Slice");
        var known = (current.Containers ?? Array.Empty<ContainerBelief>())
            .Select(c => c.Identity.ContainerId).ToHashSet();
        if (!known.Contains(rootContainerId))
            throw new InvalidOperationException(
                $"root container '{rootContainerId}' 不存在于 current revision（fail-closed）");
        var inScope = inScopeContainerIds ?? new[] { rootContainerId };
        if (inScope.Any(id => !known.Contains(id)))
            throw new InvalidOperationException(
                "in-scope container 不存在于 current revision（fail-closed）");
        var scopeSet = inScope.ToHashSet();
        var occurrences = (current.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Where(o => o.OwningContainerId is not null && scopeSet.Contains(o.OwningContainerId))
            .Select(o => new OccurrenceFact(o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor))
            .ToArray();
        var scopedClaims = current.WorldState
            .Where(kv => inScope.Any(id => kv.Key.StartsWith(id + ".", StringComparison.Ordinal)))
            .ToFrozenDictionary(kv => kv.Key, kv => kv.Value.Value);
        return new Slice(current.RevisionId, rootContainerId, current.FreshnessBasis, inScope.ToArray(), occurrences, scopedClaims);
    }

    /// <summary>
    /// 为 Effect Boundary 派生 BindingView（ADR-0011 / EXP-008 D6；UIW-004
    /// 增 HasTargetOccurrence）：消费点即时派生的 ephemeral projection。
    /// HasTargetSubjectClaim = subject claim 存在性（字符串通道 fact）；
    /// HasTargetOccurrence = occurrenceId ∈ Current.Occurrences（UI 通道
    /// owner fact）。两者都只表达 Owner-owned facts，四态拒绝判定权在
    /// Effect Boundary。无 current → fail-closed。
    /// </summary>
    public BindingView DeriveBindingView(string? subject, string? occurrenceId = null)
    {
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 BindingView");
        return new BindingView(
            current.RevisionId,
            current.RevisionNumber,
            HasTargetSubjectClaim: subject is not null && current.WorldState.ContainsKey(subject),
            HasTargetOccurrence: occurrenceId is not null
                && (current.Occurrences ?? Array.Empty<OccurrenceBelief>()).Any(o => o.OccurrenceId == occurrenceId));
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
