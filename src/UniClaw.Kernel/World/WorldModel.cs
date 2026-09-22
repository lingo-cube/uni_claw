using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;

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
    private readonly Dictionary<string, int> _continuityDemandPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _activeDemandCountsByItem = new(StringComparer.Ordinal);
    private readonly List<ContinuityDecision> _continuityLog = new();
    private readonly List<ClaimEvolutionDecision> _claimEvolutionLog = new();
    private readonly Dictionary<WorldBeliefRevision, WorldRevisionIndex> _revisionIndexes =
        new(ReferenceEqualityComparer.Instance);
    private RuntimeStageMetrics? _performanceMetrics;

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

    /// <summary>
    /// 每次 claim evolution 判定的留痕（CLE-001 / ADR-0016，owner-internal
    /// append-only，同 AssociationLog / ContinuityLog 先例）：Reaffirm /
    /// Revise 均留痕（subject 历代 value / evidence 可溯源，D2「不静默
    /// 覆盖」三件套之一）；不进 revision aggregate、不构成第二 truth。
    /// </summary>
    public IReadOnlyList<ClaimEvolutionDecision> ClaimEvolutionLog => _claimEvolutionLog;

    /// <summary>
    /// WMP-001 internal instrumentation attachment. UniKernel calls this at the
    /// existing composition seam; no Product Runtime caller-facing interface is
    /// added and the metrics never influence domain decisions.
    /// </summary>
    internal void AttachPerformanceMetrics(RuntimeStageMetrics? metrics) =>
        _performanceMetrics = metrics;

    /// <summary>
    /// 销案记录（PER-009 mechanism ④：统一销案{key,tier,依据}留档不删）。
    /// internal：不进公开面（白名单零变更）。
    /// </summary>
    internal sealed record ConflictResolution(
        string Subject,
        string Tier,
        string ResolvedValue,
        string? OverruledProducer,
        string Basis,
        string ResolvedAtRevisionId,
        DateTimeOffset ResolvedAt);

    private readonly List<ConflictResolution> _conflictResolutionLog = new();

    /// <summary>销案留痕（append-only，owner-internal；同 AssociationLog 先例）。</summary>
    internal IReadOnlyList<ConflictResolution> ConflictResolutionLog => _conflictResolutionLog;

    /// <summary>
    /// PER-009 D13 销案（评审 C-1 修复）：ConflictResolver 定案后由 owner 执行——
    /// 清除该 subject 冲突条目 + 定案值生效（痕迹链沿用 CLE-001 Revise 语义）
    /// + 双留痕（ClaimEvolutionLog + ConflictResolutionLog）。非观察：不经 P2、
    /// EvidenceBasis 不变；occurrences/containers 等 belief 原样携带（销案后
    /// Act 可直接继续，无需观察恢复）。无该 subject 冲突 → null（幂等）。
    /// </summary>
    internal WorldBeliefRevision? ResolveConflict(
        string subject,
        string resolvedValue,
        string tier,
        string? overruledProducer,
        string basis,
        DateTimeOffset resolvedAt)
    {
        var parent = Current;
        if (parent is null || parent.Conflicts.All(c => c.Subject != subject))
            return null;

        var state = parent.WorldState.ToDictionary(
            kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var subjectConflicts = parent.Conflicts.Where(c => c.Subject == subject).ToArray();
        string? previousValue = null;
        string establishingEvidenceId;
        if (state.TryGetValue(subject, out var established))
        {
            previousValue = established.Value;
            establishingEvidenceId = established.EvidenceId;
            var superseded = (established.SupersededEvidenceIds ?? Array.Empty<string>())
                .Append(established.EvidenceId)
                .Concat(subjectConflicts.SelectMany(
                    c => new[] { c.EstablishedEvidenceId, c.ChallengingEvidenceId }))
                .Distinct(StringComparer.Ordinal).ToArray();
            state[subject] = new WorldClaim(
                resolvedValue, established.EvidenceId,
                "kernel.conflict-resolver", established.EstablishingScope, superseded);
        }
        else
        {
            establishingEvidenceId = subjectConflicts[0].EstablishedEvidenceId;
            state[subject] = new WorldClaim(
                resolvedValue, establishingEvidenceId,
                "kernel.conflict-resolver", "scope:" + subject);
        }

        var remaining = parent.Conflicts.Where(c => c.Subject != subject).ToArray();
        var number = parent.RevisionNumber + 1;
        var revision = new WorldBeliefRevision(
            $"rev-{number}", parent.RevisionId, number,
            state,
            parent.WorldGraph,
            parent.EvidenceBasis, // 销案非观察：证据集不变
            parent.FreshnessBasis,
            new Uncertainty(remaining.Length),
            remaining,
            parent.Containers, parent.Relations, parent.Occurrences, parent.LogicalItems);

        _claimEvolutionLog.Add(new ClaimEvolutionDecision(
            revision.RevisionId, subject, ClaimEvolutionKind.Revise,
            previousValue ?? subjectConflicts[0].EstablishedValue, resolvedValue,
            SupersededEvidenceId: establishingEvidenceId,
            establishingEvidenceId,
            Producer: "kernel.conflict-resolver",
            Scope: "scope:conflict-resolution"));
        _conflictResolutionLog.Add(new ConflictResolution(
            subject, tier, resolvedValue, overruledProducer, basis,
            revision.RevisionId, resolvedAt));
        PublishRevision(revision);
        return revision;
    }

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

        var operationStart = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        long scannedEntries = 0;
        long copiedEntries = 0;
        var parent = Current;

        // 幂等（验收 8）：同一 canonical record 已在 current basis 中
        if (parent is not null && parent.EvidenceBasis.Contains(record.EvidenceId))
        {
            RecordReconcilePerformance(
                operationStart, allocationStart, scannedEntries: 1,
                copiedEntries: 0, outputEntries: 0);
            return parent;
        }

        // P-UW-16 owner 侧执法：裸 spatial subject（无 SpatialFrame）不得进入 belief
        ValidateSpatialSubject(record.Claim.Subject);

        var state = PersistentRevisionDictionary<string, WorldClaim>.Next(
            parent?.WorldState as PersistentRevisionDictionary<string, WorldClaim>,
            StringComparer.Ordinal);
        var graph = parent?.WorldGraph as ImmutableList<string>
            ?? (parent?.WorldGraph.ToImmutableList() ?? ImmutableList<string>.Empty);
        var conflicts = parent?.Conflicts as ImmutableList<Conflict>
            ?? (parent?.Conflicts.ToImmutableList() ?? ImmutableList<Conflict>.Empty);
        var basis = PersistentRevisionSet<string>.Next(
            parent?.EvidenceBasis as PersistentRevisionSet<string>,
            StringComparer.Ordinal);
        basis.Add(record.EvidenceId);
        copiedEntries++;

        void ApplyClaim(string subject, string value, string evidenceId)
        {
            scannedEntries++;
            if (!state.TryGetValue(subject, out var established))
            {
                // 建立（CLE-001）：carrying establishing record 的 provenance 摘要
                state.Add(subject, new WorldClaim(value, evidenceId,
                    record.Provenance.Producer, record.Provenance.Scope));
                graph = graph.Add(subject);
                copiedEntries += 2;
                return;
            }

            if (established.Value == value)
            {
                // Reaffirm（ADR-0016）：同值再观察——belief 零变化（幂等路径照旧），
                // ClaimEvolutionLog 留痕
                _claimEvolutionLog.Add(new ClaimEvolutionDecision(
                    $"rev-{(parent?.RevisionNumber ?? 0) + 1}", subject, ClaimEvolutionKind.Reaffirm,
                    established.Value, value,
                    SupersededEvidenceId: null, established.EvidenceId,
                    record.Provenance.Producer, record.Provenance.Scope));
                return;
            }

            var sameProducer = established.EstablishingProducer == record.Provenance.Producer;
            var scopeDiffers = established.EstablishingScope != record.Provenance.Scope;
            if (sameProducer && scopeDiffers)
            {
                // Revise（ADR-0016）：同 producer 异 scope = 同一观察流对呈现的
                // 再观察——新值生效 + 痕迹链（旧链 ∪ 旧 EvidenceId）+ establishing
                // provenance 更新 + log 留痕；不产生 Conflict（值替换非静默覆盖）
                var superseded = (established.SupersededEvidenceIds ?? Array.Empty<string>())
                    .Append(established.EvidenceId).ToArray();
                state.SetItem(subject, new WorldClaim(value, evidenceId,
                    record.Provenance.Producer, record.Provenance.Scope, superseded));
                copiedEntries++;
                _claimEvolutionLog.Add(new ClaimEvolutionDecision(
                    $"rev-{(parent?.RevisionNumber ?? 0) + 1}", subject, ClaimEvolutionKind.Revise,
                    established.Value, value,
                    established.EvidenceId, evidenceId,
                    record.Provenance.Producer, record.Provenance.Scope));
                return;
            }

            // 显式冲突：同 producer 同 scope（同帧矛盾）或异 producer（跨源矛盾）
            // ——语义保持（latest 不获胜），保留既存值与其 evidence 溯源
            conflicts = conflicts.Add(
                new Conflict(subject, established.Value, value, established.EvidenceId, evidenceId));
            copiedEntries++;
        }

        // UIW-001（UWM-009 §18 Revise 语义的最小 realization）：owner-derived
        // claims（CurrentContainer / signature）承载的是 association 决定的
        // 合法迁移，不是互相矛盾的观察——值随新 decision 替换、溯源到本次
        // evidence，decision context 由 AssociationLog 留痕；不产生 conflict。
        void ApplyDerivedClaim(string subject, string value, string evidenceId)
        {
            scannedEntries++;
            if (!state.ContainsKey(subject))
            {
                graph = graph.Add(subject);
                copiedEntries++;
            }
            state.SetItem(subject, new WorldClaim(value, evidenceId));
            copiedEntries++;
        }

        ApplyClaim(record.Claim.Subject, record.Claim.Value, record.EvidenceId);

        // Container Association（UWM-009 §9 合法输入冻结：accepted evidence +
        // previous revision + P22 prior；Authority gates 见 Associate）
        var containers = parent?.Containers as ImmutableList<ContainerBelief>
            ?? (parent?.Containers?.ToImmutableList() ?? ImmutableList<ContainerBelief>.Empty);
        var relations = parent?.Relations as ImmutableList<ContainerRelation>
            ?? (parent?.Relations?.ToImmutableList() ?? ImmutableList<ContainerRelation>.Empty);
        if (_associationStrategy is not null)
        {
            var decision = Associate(
                record, transitionContext, parent,
                ref containers, ref relations,
                ref scannedEntries, ref copiedEntries,
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
        IReadOnlyList<OccurrenceBelief>? occurrences = parent?.Occurrences;
        if (_observationStrategy is not null)
        {
            var proposed = _observationStrategy.Derive(record, parent);
            scannedEntries += proposed.Count;
            occurrences = proposed
                .Select((proposed, index) => new OccurrenceBelief(
                    MintOccurrenceIdentity(record.EvidenceId, index),
                    proposed.OwningContainerId, proposed.Role, proposed.SemanticDescriptor,
                    new[] { record.EvidenceId },
                    State: proposed.State,
                    Locator: proposed.Locator,
                    Native: proposed.Native))
                .ToImmutableArray();
            copiedEntries += occurrences.Count;
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
            WorldState: state.Build(),
            WorldGraph: graph,
            EvidenceBasis: basis.Build(),
            FreshnessBasis: freshness,
            Uncertainty: new Uncertainty(conflicts.Count),
            Conflicts: conflicts,
            Containers: containers,
            Relations: relations,
            Occurrences: occurrences,
            LogicalItems: logicalItems);

        PublishRevision(revision);
        RecordReconcilePerformance(
            operationStart, allocationStart, scannedEntries, copiedEntries,
            outputEntries: 1);
        return revision;
    }

    private void RecordReconcilePerformance(
        long operationStart,
        long allocationStart,
        long scannedEntries,
        long copiedEntries,
        long outputEntries) =>
        _performanceMetrics?.RecordWorldModel(
            WorldModelOperation.Reconcile,
            scannedEntries,
            copiedEntries,
            outputEntries,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - operationStart);

    private void PublishRevision(WorldBeliefRevision revision, WorldRevisionIndex? continuityParentIndex = null)
    {
        var parentRevision = Current;
        var index = continuityParentIndex is null
            ? WorldRevisionIndex.Create(
                revision,
                parentRevision,
                parentRevision is null ? null : IndexFor(parentRevision),
                _performanceMetrics)
            : WorldRevisionIndex.ForContinuityRevision(revision, continuityParentIndex, _performanceMetrics);
        _revisionIndexes.Add(revision, index);
        _revisionHistory.Add(revision);
    }

    private WorldRevisionIndex IndexFor(WorldBeliefRevision revision) =>
        _revisionIndexes.TryGetValue(revision, out var index)
            ? index
            : throw new InvalidOperationException("World revision index missing");

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
        ref ImmutableList<ContainerBelief> containers,
        ref ImmutableList<ContainerRelation> relations,
        ref long scannedEntries,
        ref long copiedEntries,
        out string? currentContainerId)
    {
        currentContainerId = null;
        var proposal = _associationStrategy!.Propose(new AssociationInput(parent, record, transitionContext));
        var number = (parent?.RevisionNumber ?? 0) + 1;

        var effective = proposal.Kind;
        string? matchedId = null;
        string? establishedId = null;
        var reason = proposal.Reason;
        bool IsValidEvidence(string evidenceId) => evidenceId == record.EvidenceId
            || (parent?.EvidenceBasis.Contains(evidenceId) ?? false);

        if (proposal.Kind == AssociationDispositionKind.Matched)
        {
            var id = proposal.MatchedContainerId;
            var candidate = proposal.Candidates.FirstOrDefault(c => c.CandidateContainerId == id);
            scannedEntries += proposal.Candidates.Count;
            var blocked = id is null
                || parent is null
                || !IndexFor(parent).ContainerPositions.ContainsKey(id)
                || candidate is null
                || candidate.SupportingEvidenceIds.Count == 0
                || candidate.ContradictingEvidenceIds.Count > 0
                || !candidate.SupportingEvidenceIds.All(IsValidEvidence);
            if (blocked)
            {
                effective = AssociationDispositionKind.Insufficient;
                reason = $"authority-blocked-matched:{proposal.Reason}";
            }
            else
            {
                matchedId = id!;
                var index = IndexFor(parent!).ContainerPositions[id!];
                var existingBasis = containers[index].EvidenceBasis as ImmutableList<string>
                    ?? containers[index].EvidenceBasis.ToImmutableList();
                if (!existingBasis.Contains(record.EvidenceId))
                    existingBasis = existingBasis.Add(record.EvidenceId);
                containers = containers.SetItem(index, new ContainerBelief(
                    containers[index].Identity, existingBasis));
                copiedEntries++;
                currentContainerId = id;
            }
        }
        else if (proposal.Kind == AssociationDispositionKind.New)
        {
            scannedEntries += proposal.Candidates.Sum(c => c.SupportingEvidenceIds.Count);
            var backed = proposal.Candidates.Any(c => c.SupportingEvidenceIds.Contains(record.EvidenceId));
            if (!backed)
            {
                effective = AssociationDispositionKind.Insufficient;
                reason = $"authority-blocked-new:{proposal.Reason}";
            }
            else
            {
                establishedId = MintContainerIdentity(record.EvidenceId);
                containers = containers.Add(new ContainerBelief(
                    new ContainerIdentity(establishedId), ImmutableList.Create(record.EvidenceId)));
                copiedEntries++;
                currentContainerId = establishedId;
            }
        }

        bool IsKnownContainer(string id) => id == establishedId
            || (parent is not null && IndexFor(parent).ContainerPositions.ContainsKey(id));
        foreach (var relation in proposal.Relations)
        {
            scannedEntries++;
            if (relation.SupportingEvidenceIds.Count == 0
                || !relation.SupportingEvidenceIds.All(IsValidEvidence)
                || !IsKnownContainer(relation.SourceContainerId)
                || !IsKnownContainer(relation.TargetContainerId))
                continue;
            relations = relations.Add(new ContainerRelation(
                relation.Kind, relation.SourceContainerId, relation.TargetContainerId,
                relation.SupportingEvidenceIds.ToImmutableList()));
            copiedEntries++;
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
        var operationStart = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        if (_continuityDemandPositions.TryGetValue(demand.DemandId, out var existingPosition))
        {
            RecordDemandLookup(operationStart, allocationStart, outputEntries: 1);
            var existing = _continuityDemands[existingPosition];
            return new DemandHandle(existing.DemandId);
        }

        if (demand.AnchorOccurrenceId is not null)
        {
            var current = Current;
            if (current?.Occurrences is null
                || !IndexFor(current).OccurrencesById.ContainsKey(demand.AnchorOccurrenceId))
                throw new InvalidOperationException(
                    $"continuity demand anchor occurrence '{demand.AnchorOccurrenceId}' 不在 current revision"
                    + "（stale anchor：demand 必须在源 occurrence 仍属 current revision 时登记，ADR-0014 timing）");
        }

        _continuityDemandPositions.Add(demand.DemandId, _continuityDemands.Count);
        _continuityDemands.Add(demand);
        IncrementActiveDemand(demand.LogicalItemId);
        RecordDemandLookup(operationStart, allocationStart, outputEntries: 1);
        return new DemandHandle(demand.DemandId);
    }

    /// <summary>
    /// 撤销 demand：只删该 demand 本身（ADR-0014）——不删除 LogicalItem、不改历史
    /// belief、不产生 Ended、零 revision 副作用。不存在的 demandId 为 no-op
    /// （幂等撤销，本实现选择；Revoke 不是 identity 变异路径，无需 fail-closed）。
    /// </summary>
    public void RevokeContinuityDemand(string demandId)
    {
        var operationStart = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        if (!_continuityDemandPositions.Remove(demandId, out var position))
        {
            RecordDemandLookup(operationStart, allocationStart, outputEntries: 0);
            return;
        }

        var removed = _continuityDemands[position];
        DecrementActiveDemand(removed.LogicalItemId);
        _continuityDemands.RemoveAt(position);
        for (var i = position; i < _continuityDemands.Count; i++)
            _continuityDemandPositions[_continuityDemands[i].DemandId] = i;
        RecordDemandLookup(
            operationStart, allocationStart,
            outputEntries: 1, scannedEntries: _continuityDemands.Count - position);
    }

    /// <summary>
    /// Maintenance 派生查询（ADR-0015 四轴中的 Maintenance 轴）：active demand
    /// 引用该 item 即 Hot。纯计算，不落任何存储字段、不产生副作用。
    /// </summary>
    public bool IsHotItem(string logicalItemId)
    {
        var operationStart = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        var hot = _activeDemandCountsByItem.ContainsKey(logicalItemId);
        RecordDemandLookup(operationStart, allocationStart, hot ? 1 : 0);
        return hot;
    }

    private void IncrementActiveDemand(string? logicalItemId)
    {
        if (logicalItemId is null)
            return;
        _activeDemandCountsByItem.TryGetValue(logicalItemId, out var count);
        _activeDemandCountsByItem[logicalItemId] = count + 1;
    }

    private void DecrementActiveDemand(string? logicalItemId)
    {
        if (logicalItemId is null
            || !_activeDemandCountsByItem.TryGetValue(logicalItemId, out var count))
            return;
        if (count == 1)
            _activeDemandCountsByItem.Remove(logicalItemId);
        else
            _activeDemandCountsByItem[logicalItemId] = count - 1;
    }

    private void RecordDemandLookup(
        long operationStart,
        long allocationStart,
        long outputEntries,
        long scannedEntries = 0) =>
        _performanceMetrics?.RecordWorldModel(
            WorldModelOperation.DemandLookup,
            scannedEntries,
            copiedEntries: 0,
            outputEntries,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - operationStart);

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
        var operationStart = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        if (!_continuityDemandPositions.TryGetValue(handle.DemandId, out var demandPosition))
            throw new InvalidOperationException(
                $"continuity demand '{handle.DemandId}' 不存在或已撤销");
        var demand = _continuityDemands[demandPosition];
        RecordDemandLookup(operationStart, allocationStart, outputEntries: 1);
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法 ResolveContinuity");
        var currentIndex = IndexFor(current);

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
            RecordContinuityPerformance(
                operationStart, allocationStart,
                scannedEntries: 0, copiedEntries: 0, outputEntries: 0);
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
            && currentIndex.OccurrencesById.ContainsKey(proposal.MatchedOccurrenceId);
        bool ItemExists() => proposal.MatchedLogicalItemId is not null
            && currentIndex.LogicalItemPositions.ContainsKey(proposal.MatchedLogicalItemId);

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
        var items = current.LogicalItems as ImmutableList<LogicalItemBelief>
            ?? (current.LogicalItems?.ToImmutableList() ?? ImmutableList<LogicalItemBelief>.Empty);
        var canonicalItemCopies = 0L;
        string? matchedOccurrenceId = null;
        string? matchedItemId = null;
        string? resultItemId = null;
        var changed = false;

        if (effective == ContinuityProposedOutcomeKind.ReferenceEstablished)
        {
            matchedOccurrenceId = proposal.MatchedOccurrenceId;
            var occurrence = currentIndex.OccurrencesById[matchedOccurrenceId!];
            var itemId = MintLogicalItemIdentity(matchedOccurrenceId!);
            var index = currentIndex.LogicalItemPositions.TryGetValue(itemId, out var existingPosition)
                ? existingPosition
                : -1;
            if (index < 0)
            {
                items = items.Add(new LogicalItemBelief(
                    itemId, occurrence.OwningContainerId, demand.Role, demand.SemanticDescriptor,
                    LogicalItemLifecycle.Established, EndedReason: null,
                    proposal.SupportingEvidenceIds.ToImmutableList()));
                canonicalItemCopies++;
            }
            else
            {
                // 同 occurrence 的重铸（多 demand 场景）：合并 basis，不重复铸造
                var existingBasis = items[index].EvidenceBasis as ImmutableList<string>
                    ?? items[index].EvidenceBasis.ToImmutableList();
                var mergedBasis = AppendDistinct(existingBasis, proposal.SupportingEvidenceIds);
                items = items.SetItem(index, items[index] with
                {
                    EvidenceBasis = mergedBasis
                });
                canonicalItemCopies++;
            }
            resultItemId = itemId;
            matchedItemId = itemId;
            changed = true;
        }
        else if (effective == ContinuityProposedOutcomeKind.SameReferent)
        {
            matchedOccurrenceId = proposal.MatchedOccurrenceId;
            matchedItemId = proposal.MatchedLogicalItemId;
            var index = currentIndex.LogicalItemPositions[matchedItemId!];
            var existingBasis = items[index].EvidenceBasis as ImmutableList<string>
                ?? items[index].EvidenceBasis.ToImmutableList();
            var merged = AppendDistinct(existingBasis, proposal.SupportingEvidenceIds);
            changed = merged.Count > items[index].EvidenceBasis.Count;
            if (changed)
            {
                items = items.SetItem(index, items[index] with { EvidenceBasis = merged });
                canonicalItemCopies++;
            }
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
                var index = currentIndex.LogicalItemPositions.TryGetValue(
                    termination.LogicalItemId, out var position)
                    ? position
                    : -1;
                if (index < 0
                    || termination.SupportingEvidenceIds.Count == 0
                    || !termination.SupportingEvidenceIds.All(validEvidence.Contains))
                    continue;
                if (items[index].Lifecycle != LogicalItemLifecycle.Ended)
                {
                    items = items.SetItem(index, items[index] with
                    {
                        Lifecycle = LogicalItemLifecycle.Ended,
                        EndedReason = "referent-terminated"
                    });
                    canonicalItemCopies++;
                    changed = true;
                }
            }
        }

        // owning container 缺失级联（§38 scope ⊆ Container lifetime；RVR-001 F2：
        // 对**所有** effective 结果统一执行——Contradicted/Ambiguous/Insufficient
        // 也级联）。级联是独立的第二触发源，与判别结果解耦：Contradicted 本身
        // 不终止 item（UWM-009 §37 / P-UW-30：七者 ≠ Ended；P-UW-31：Ended 仅
        // 两类正面 lifecycle evidence，级联属第 2 类）；级联产生变化 →
        // changed=true → commit revision。v0.1 无 container 终止操作，正常路径
        // 不可达；item 的 OwningContainerId 不在当前 containers 时触发（手工
        // 构造缺失 container 时可达，见 S10c/S10d）。
        foreach (var i in currentIndex.MissingContainerLogicalItemPositions)
        {
            if (items[i].OwningContainerId is { } owner
                && !currentIndex.ContainerPositions.ContainsKey(owner)
                && items[i].Lifecycle != LogicalItemLifecycle.Ended)
            {
                items = items.SetItem(i, items[i] with
                {
                    Lifecycle = LogicalItemLifecycle.Ended,
                    EndedReason = "container-scope-ended"
                });
                canonicalItemCopies++;
                changed = true;
            }
        }
        if (items.Count > (current.LogicalItems?.Count ?? 0))
        {
            var i = items.Count - 1;
            if (items[i].OwningContainerId is { } owner
                && !currentIndex.ContainerPositions.ContainsKey(owner)
                && items[i].Lifecycle != LogicalItemLifecycle.Ended)
            {
                items = items.SetItem(i, items[i] with
                {
                    Lifecycle = LogicalItemLifecycle.Ended,
                    EndedReason = "container-scope-ended"
                });
                canonicalItemCopies++;
                changed = true;
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
            _continuityDemands[demandPosition] = demand with { LogicalItemId = resultItemId };
            IncrementActiveDemand(resultItemId);
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
        RecordContinuityPerformance(
            operationStart, allocationStart,
            scannedEntries: candidates.Length + existingItems.Length
                + currentIndex.MissingContainerLogicalItemPositions.Count,
            copiedEntries: candidates.Length + existingItems.Length + canonicalItemCopies,
            outputEntries: changed ? items.Count : 0);
        return new ContinuityResolution(outcome, resultItemId, revisionId);
    }

    private void RecordContinuityPerformance(
        long operationStart,
        long allocationStart,
        long scannedEntries,
        long copiedEntries,
        long outputEntries) =>
        _performanceMetrics?.RecordWorldModel(
            WorldModelOperation.ResolveContinuity,
            scannedEntries,
            copiedEntries,
            outputEntries,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - operationStart);

    private static ImmutableList<string> AppendDistinct(
        ImmutableList<string> established,
        IReadOnlyList<string> additions)
    {
        var result = established;
        foreach (var addition in additions)
            if (!result.Contains(addition))
                result = result.Add(addition);
        return result;
    }

    /// <summary>
    /// Continuity commit：EvidenceBasis 集合不变、WorldState / Graph / Conflicts /
    /// Containers / Relations / Occurrences 均沿用 Current，仅 LogicalItems 变化；
    /// ParentRevisionId 链正确，历史保留。
    /// </summary>
    private WorldBeliefRevision CommitContinuityRevision(IReadOnlyList<LogicalItemBelief> logicalItems)
    {
        var parent = Current!;
        var parentIndex = IndexFor(parent);
        var number = parent.RevisionNumber + 1;
        var revision = new WorldBeliefRevision(
            RevisionId: $"rev-{number}",
            ParentRevisionId: parent.RevisionId,
            RevisionNumber: number,
            parent.WorldState, parent.WorldGraph, parent.EvidenceBasis,
            parent.FreshnessBasis, parent.Uncertainty, parent.Conflicts,
            parent.Containers, parent.Relations,
            parent.Occurrences, logicalItems);
        PublishRevision(revision, parentIndex);
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
                OwningContainerId: null,
                CurrentCandidateSetResultKind.ScopeProjectionUnavailable,
                Array.Empty<CandidateOccurrenceFact>());

        var start = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        var index = IndexFor(current);
        var roleCandidates = index.OccurrencesByRole.TryGetValue(descriptor.Role, out var bucket)
            ? bucket
            : Array.Empty<OccurrenceBelief>();
        var candidates = roleCandidates
            .Where(o => OccurrenceDescriptorMatcher.Matches(
                o.Role, o.SemanticDescriptor, o.OwningContainerId,
                descriptor.Role, descriptor.SemanticDescriptor, descriptor.OwningContainerId,
                ContainerMatchMode.TargetEquality))
            .Select(o => new CandidateOccurrenceFact(
                o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                current.RevisionId))
            .ToArray();
        _performanceMetrics?.RecordWorldModel(
            WorldModelOperation.ResolveCurrent,
            scannedEntries: roleCandidates.Count,
            copiedEntries: candidates.Length,
            outputEntries: candidates.Length,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - start);
        var result = candidates.Length switch
        {
            0 => CurrentCandidateSetResultKind.NoCandidate,
            1 => CurrentCandidateSetResultKind.UniqueCandidate,
            _ => CurrentCandidateSetResultKind.MultipleCandidates,
        };
        // RVR-001 F1（ADR-0011 原则 4）：OwningContainerId 是派生 owner fact，
        // 不回显 consumer 输入——匹配候选的 owner 值集合恰好一个非 null → 该值；
        // 否则（全 null / ≥2 个不同非 null / 零候选）→ null。
        var derivedOwner = candidates
            .Select(c => c.OwningContainerId).OfType<string>().Distinct().ToArray();
        return new CurrentGroundingView(
            current.RevisionId,
            derivedOwner.Length == 1 ? derivedOwner[0] : null,
            result, candidates);
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
        var start = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        var index = IndexFor(current);
        if (!index.ContainerPositions.ContainsKey(rootContainerId))
            throw new InvalidOperationException(
                $"root container '{rootContainerId}' 不存在于 current revision（fail-closed）");
        var inScope = inScopeContainerIds ?? new[] { rootContainerId };
        if (inScope.Any(id => !index.ContainerPositions.ContainsKey(id)))
            throw new InvalidOperationException(
                "in-scope container 不存在于 current revision（fail-closed）");
        var distinctScope = inScope.Distinct(StringComparer.Ordinal).ToArray();
        var indexedOccurrences = distinctScope
            .SelectMany(id => index.OccurrencesByContainer.TryGetValue(id, out var entries)
                ? entries
                : Array.Empty<WorldRevisionIndex.IndexedOccurrence>())
            .OrderBy(entry => entry.Ordinal)
            .ToArray();
        var occurrences = indexedOccurrences
            .Select(entry => entry.Occurrence)
            .Select(o => new OccurrenceFact(
                o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                o.State, o.Locator, o.Native))
            .ToArray();
        var indexedClaims = distinctScope
            .SelectMany(id => index.ClaimsByContainerPrefix.TryGetValue(id, out var entries)
                ? entries
                : ImmutableList<WorldRevisionIndex.IndexedClaim>.Empty)
            .OrderBy(entry => entry.Ordinal)
            .ToArray();
        var scopedClaims = indexedClaims.ToFrozenDictionary(
            entry => entry.Subject,
            entry => entry.Claim.Value);
        var slice = new Slice(
            current.RevisionId, rootContainerId, current.FreshnessBasis,
            inScope.ToArray(), occurrences, scopedClaims);
        _performanceMetrics?.RecordWorldModel(
            WorldModelOperation.DeriveSlice,
            scannedEntries: distinctScope.Length + indexedOccurrences.Length + indexedClaims.Length,
            copiedEntries: inScope.Count + occurrences.Length + scopedClaims.Count,
            outputEntries: occurrences.Length + scopedClaims.Count,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - start);
        return slice;
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
        var start = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        var targetOccurrence = occurrenceId is null
            ? null
            : IndexFor(current).OccurrencesById.GetValueOrDefault(occurrenceId);
        var view = new BindingView(
            current.RevisionId,
            current.RevisionNumber,
            HasTargetSubjectClaim: subject is not null && current.WorldState.ContainsKey(subject),
            HasTargetOccurrence: targetOccurrence is not null,
            TargetOccurrenceLocator: targetOccurrence?.Locator,
            TargetOccurrenceNative: targetOccurrence?.Native);
        RecordConsumerViewPerformance(
            start, allocationStart,
            scannedEntries: (subject is null ? 0 : 1) + (occurrenceId is null ? 0 : 1),
            copiedEntries: 1,
            outputEntries: 1);
        return view;
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
        var start = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        var view = new ActionAssuranceView(
            current.RevisionId,
            current.RevisionNumber,
            current.FreshnessBasis,
            HasConflictOnTarget: subject is not null && IndexFor(current).ConflictSubjects.Contains(subject));
        RecordConsumerViewPerformance(
            start, allocationStart,
            scannedEntries: subject is null ? 0 : 1,
            copiedEntries: 1,
            outputEntries: 1);
        return view;
    }

    /// <summary>
    /// PER-009 S5：为 Control 派生冲突可见性（internal，零公开面变更）。
    /// 悬案 subjects 来自 current revision 的 ConflictSubjects 索引
    /// （与 Assurance 的 HasConflictOnTarget 同源）。无 current → fail-closed。
    /// </summary>
    internal ControlBeliefView DeriveControlBeliefView()
    {
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 ControlBeliefView");
        var subjects = IndexFor(current).ConflictSubjects;
        return new ControlBeliefView(subjects.ToArray());
    }

    /// <summary>
    /// PER-009 S6b：driver 组合接线用——无 current revision 时返回 null
    /// （= 无悬案可聚焦；fail-closed 语义保留给消费侧显式派生）。
    /// </summary>
    internal ControlBeliefView? DeriveControlBeliefViewOrNull() =>
        Current is { } current
            ? new ControlBeliefView(IndexFor(current).ConflictSubjects.ToArray())
            : null;

    /// <summary>
    /// 为 Assurance obligation / outcome 路径派生 OutcomeAssuranceView
    /// （EXP-008 D8）：claims / conflicts 按 obligation subjects scope
    /// （空 subject 占位 obligation 不入 scope，查找本来即 miss，行为
    /// 等价）；BasisEvidenceIds 为全量 refs（proof 载荷真实 buyer）。
    /// entityObligations（ESO-002 D1/D5）：entity-scoped obligation 的
    /// owner-derived tri-state fact 纯派生——Current.Occurrences 按
    /// （Role 相等 ∧ descriptor 相等[若给] ∧ container 相等[若给]）匹配：
    /// 恰一且 State==required → Satisfied；恰一且 State 有值但 ≠ →
    /// Unsatisfied；零/多候选/State null → Unknown（fail-closed，Identity
    /// never creates information）。零 commit / 零 strategy 调用 / 不重复
    /// continuity adjudication（D3）。无 current → fail-closed。
    /// </summary>
    public OutcomeAssuranceView DeriveOutcomeAssuranceView(
        IEnumerable<string> subjects,
        IEnumerable<(string ObligationId, TargetDescriptor Scope, string RequiredState)>? entityObligations = null)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        var current = Current ?? throw new InvalidOperationException("尚无 WorldBelief revision，无法派生 OutcomeAssuranceView");
        var start = _performanceMetrics is null ? 0 : Stopwatch.GetTimestamp();
        var allocationStart = _performanceMetrics is null ? 0 : GC.GetAllocatedBytesForCurrentThread();
        long scannedEntries = 0;

        var scope = subjects
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet();
        scannedEntries += current.WorldState.Count;
        var claims = current.WorldState
            .Where(kv => scope.Contains(kv.Key))
            .ToFrozenDictionary(
                kv => kv.Key,
                kv => new ScopedClaim(kv.Value.Value, kv.Value.EvidenceId));
        var currentIndex = IndexFor(current);
        var indexedConflicts = scope
            .SelectMany(subject => currentIndex.ConflictsBySubject.TryGetValue(subject, out var bucket)
                ? bucket
                : Array.Empty<WorldRevisionIndex.IndexedConflict>())
            .OrderBy(entry => entry.Ordinal)
            .ToArray();
        scannedEntries += indexedConflicts.Length;
        var conflicts = indexedConflicts.Select(entry => entry.Conflict).ToArray();

        IReadOnlyList<EntityObligationFact>? entityFacts = null;
        if (entityObligations is not null)
        {
            var facts = new List<EntityObligationFact>();
            foreach (var obligation in entityObligations)
            {
                var kind = DeriveEntityObligationFactKind(
                    obligation.Scope, obligation.RequiredState, out var scannedCandidates);
                scannedEntries += scannedCandidates;
                facts.Add(new EntityObligationFact(obligation.ObligationId, kind));
            }
            entityFacts = facts.ToArray();
        }

        var view = new OutcomeAssuranceView(
            current.RevisionId,
            current.Uncertainty.ConflictingClaimCount,
            Claims: claims,
            Conflicts: conflicts,
            BasisEvidenceIds: current.EvidenceBasis,
            EntityFacts: entityFacts);
        RecordConsumerViewPerformance(
            start, allocationStart,
            scannedEntries,
            copiedEntries: claims.Count + conflicts.Length + (entityFacts?.Count ?? 0),
            outputEntries: claims.Count + conflicts.Length + (entityFacts?.Count ?? 0));
        return view;
    }

    /// <summary>ESO-002 D1：单条 entity obligation 的 tri-state fact 匹配
    ///（与 ResolveCurrent 同一机械确定性维度；Unknown 兜底 fail-closed）。</summary>
    private EntityObligationFactKind DeriveEntityObligationFactKind(
        TargetDescriptor scope,
        string requiredState,
        out int scannedEntries)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var current = Current!;
        var index = IndexFor(current);
        var roleCandidates = index.OccurrencesByRole.TryGetValue(scope.Role, out var bucket)
            ? bucket
            : Array.Empty<OccurrenceBelief>();
        scannedEntries = roleCandidates.Count;
        var candidates = roleCandidates
            .Where(o => OccurrenceDescriptorMatcher.Matches(
                o.Role, o.SemanticDescriptor, o.OwningContainerId,
                scope.Role, scope.SemanticDescriptor, scope.OwningContainerId,
                ContainerMatchMode.TargetEquality))
            .ToArray();
        if (candidates.Length != 1)
            return EntityObligationFactKind.Unknown; // 零/多候选：不铸信息
        var state = candidates[0].State;
        if (state is null)
            return EntityObligationFactKind.Unknown; // 无 state 证据 ≠ false
        return state == requiredState
            ? EntityObligationFactKind.Satisfied
            : EntityObligationFactKind.Unsatisfied;
    }

    private void RecordConsumerViewPerformance(
        long operationStart,
        long allocationStart,
        long scannedEntries,
        long copiedEntries,
        long outputEntries) =>
        _performanceMetrics?.RecordWorldModel(
            WorldModelOperation.ConsumerViewDerivation,
            scannedEntries,
            copiedEntries,
            outputEntries,
            allocatedBytes: GC.GetAllocatedBytesForCurrentThread() - allocationStart,
            elapsedTicks: Stopwatch.GetTimestamp() - operationStart);

    /// <summary>Slice 有效性 = 派生判定（source revision 是否仍为 current；
    /// currency 属 World Model 侧；freshness 充分性属消费侧 Freshness
    /// Judgment，不在此判定——ADR-0010 / FRS-007 D4；验收 6）。</summary>
    public bool IsSliceValid(Slice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);
        return Current is not null && slice.SourceRevisionId == Current.RevisionId;
    }
}
