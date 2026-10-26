using System.Collections.Immutable;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

/// <summary>
/// WORLD-VISUALIZATION-001 切片 0 的只读派生：图投影增补（结构关系 + 节点语义
/// 标签）与 revision 时间线。全部纯读取，不修改 World 状态、不产生 revision；
/// 形状与不变量见 changes/WORLD-VISUALIZATION-001/read-seam-interface-draft.md
/// （rev.2，独立审阅 APPROVE）。
/// </summary>
public sealed partial class WorldModel
{
    private static IReadOnlyList<ContainerRelation> DeriveStructuralRelations(WorldBeliefRevision revision) =>
        (revision.Relations ?? []).Where(relation => relation.Kind != ContainerRelationKind.NavigatesTo)
            .Select(relation => relation with { EvidenceBasis = relation.EvidenceBasis.ToImmutableArray() })
            .ToImmutableArray();

    /// <summary>
    /// 标签冲突判定：只用选定 revision 自身 Conflicts 快照 + RevisionNumber 界定
    /// 内的 Applied 纠错（DerivePageView 的 revisionIds 界定先例）；其后发生的
    /// 纠错不回填历史标签。join 谓词按 RevokedInterpretationEvidenceIds 与语义
    /// 证据引用求交（复审 MINOR 处置）。
    /// </summary>
    private bool IsLabelConflicted(WorldSemanticKnowledge knowledge, WorldBeliefRevision revision,
        IReadOnlyList<PageCorrectionDecision> boundedAppliedCorrections) =>
        revision.Conflicts.Any(conflict =>
            conflict.EstablishedEvidenceId == knowledge.OriginalSemanticEvidenceId
            || conflict.ChallengingEvidenceId == knowledge.OriginalSemanticEvidenceId
            || conflict.EstablishedEvidenceId == knowledge.VerificationEvidenceId
            || conflict.ChallengingEvidenceId == knowledge.VerificationEvidenceId)
        || boundedAppliedCorrections.Any(correction =>
            correction.RevokedInterpretationEvidenceIds.Contains(knowledge.OriginalSemanticEvidenceId)
            || knowledge.CorrespondenceEvidenceIds.Any(id =>
                correction.RevokedInterpretationEvidenceIds.Contains(id)));

    private IReadOnlyList<WorldNodeLabel> DeriveNodeLabels(WorldBeliefRevision revision)
    {
        var boundedRevisionIds = _revisionHistory
            .Where(item => item.RevisionNumber <= revision.RevisionNumber)
            .Select(item => item.RevisionId).ToHashSet(StringComparer.Ordinal);
        var boundedApplied = _pageCorrections
            .Where(correction => correction.Applied && boundedRevisionIds.Contains(correction.SourceRevisionId))
            .ToArray();
        var labels = new List<WorldNodeLabel>();
        foreach (var group in revision.SemanticKnowledge.GroupBy(knowledge => knowledge.ContainerId, StringComparer.Ordinal))
        {
            // 最后一条非冲突条目优先；当前实现单 revision ≤1 条（前向兼容保留）。
            WorldSemanticKnowledge? chosen = null;
            foreach (var knowledge in group)
            {
                if (IsLabelConflicted(knowledge, revision, boundedApplied)) continue;
                chosen = knowledge;
            }
            var conflicted = false;
            if (chosen is null)
            {
                foreach (var knowledge in group) chosen = knowledge;
                conflicted = chosen is not null;
            }
            if (chosen is null) continue;
            labels.Add(new(chosen.ContainerId, chosen.PageName, chosen.PagePurpose, chosen.PageKey,
                chosen.Reused, conflicted, chosen.SourceRevisionId,
                new[] { chosen.OriginalSemanticEvidenceId, chosen.VerificationEvidenceId }
                    .Concat(chosen.CorrespondenceEvidenceIds).ToImmutableArray()));
        }
        return labels.ToImmutableArray();
    }

    /// <summary>
    /// 签名撤回推导（WORLD-VISUALIZATION-001）：Applied 纠错（RevisionNumber
    /// 界定内）把 previousContainer 的唯一 signature 撤走（corrected 是别的
    /// 容器），且选定 revision 的 WorldState 中该容器无新 signature → 记入。
    /// 历史安全：只用界定内纠错 + 选定 revision 自身快照，其后事件不回填。
    /// </summary>
    private IReadOnlyList<string> DeriveSignatureWithdrawn(WorldBeliefRevision revision)
    {
        var boundedRevisionIds = _revisionHistory
            .Where(item => item.RevisionNumber <= revision.RevisionNumber)
            .Select(item => item.RevisionId).ToHashSet(StringComparer.Ordinal);
        var withdrawnCandidates = _pageCorrections
            .Where(correction => correction.Applied && boundedRevisionIds.Contains(correction.SourceRevisionId)
                && correction.PreviousContainerId is not null
                && correction.CorrectedContainerId is not null
                && correction.PreviousContainerId != correction.CorrectedContainerId)
            .Select(correction => correction.PreviousContainerId!)
            .ToHashSet(StringComparer.Ordinal);
        if (withdrawnCandidates.Count == 0) return System.Collections.Immutable.ImmutableArray<string>.Empty;
        var result = withdrawnCandidates
            .Where(containerId => revision.Containers?.Any(c => c.Identity.ContainerId == containerId) == true
                && !revision.WorldState.ContainsKey(SignatureSubjectPrefix + containerId))
            .Order(StringComparer.Ordinal).ToImmutableArray();
        return result;
    }

    internal WorldRevisionTimeline DeriveRevisionTimeline()
    {
        var correctionsBySource = _pageCorrections
            .ToLookup(correction => correction.SourceRevisionId, StringComparer.Ordinal);
        var summaries = new List<WorldRevisionSummary>();
        foreach (var revision in _revisionHistory)
        {
            var parent = revision.ParentRevisionId is null ? null
                : _revisionHistory.FirstOrDefault(item => item.RevisionId == revision.ParentRevisionId);
            var currentKeys = SemanticKnowledgeKeys(revision.SemanticKnowledge);
            var previousKeys = parent is null
                ? ImmutableHashSet<string>.Empty : SemanticKnowledgeKeys(parent.SemanticKnowledge);
            var addedKeys = currentKeys.Except(previousKeys);
            var structuralNow = StructuralRelationKeys(revision.Relations ?? []);
            var structuralPrev = parent is null
                ? ImmutableHashSet<string>.Empty : StructuralRelationKeys(parent.Relations ?? []);
            var structuralChanges = structuralNow.Except(structuralPrev).Select(key => "+" + key)
                .Concat(structuralPrev.Except(structuralNow).Select(key => "-" + key))
                .Order(StringComparer.Ordinal).ToImmutableArray();
            var page = revision.CurrentPageObservation;
            var pageCorrections = correctionsBySource[revision.RevisionId].ToArray();
            var newEvidenceIds = parent is null ? revision.EvidenceBasis
                : revision.EvidenceBasis.Except(parent.EvidenceBasis);
            // 已验证导航进入世界知识的标记：本 revision 相对父新增 NavigatesTo
            // 关系（快照一致；到达锚由关系携带的 TargetRevisionId 表达）。
            var parentRelations = parent?.Relations ?? [];
            bool NavigationArrived(ContainerRelation relation) =>
                relation.Kind == ContainerRelationKind.NavigatesTo
                && parentRelations.All(previous => previous.Kind != ContainerRelationKind.NavigatesTo
                    || previous.SourceContainerId != relation.SourceContainerId
                    || previous.TargetContainerId != relation.TargetContainerId
                    || previous.TargetRevisionId != relation.TargetRevisionId);
            var navigationVerified = (revision.Relations ?? []).Any(NavigationArrived);
            summaries.Add(new(revision.RevisionId, revision.ParentRevisionId, revision.RevisionNumber,
                revision.FreshnessBasis.AsOf,
                new WorldRevisionChangeSummary(
                    page is not null,
                    page?.ContainerId is { } container
                        ? ImmutableArray.Create(container) : ImmutableArray<string>.Empty,
                    navigationVerified,
                    pageCorrections.Any(correction => correction.Applied),
                    pageCorrections.Any(correction => !correction.Applied),
                    pageCorrections.Where(correction => correction.Applied)
                        .Select(correction => correction.TargetCaptureEvidenceId)
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
                    !currentKeys.SetEquals(previousKeys),
                    revision.SemanticKnowledge.Any(knowledge =>
                        addedKeys.Contains(SemanticKnowledgeKey(knowledge)) && knowledge.Reused),
                    structuralChanges,
                    newEvidenceIds.Order(StringComparer.Ordinal).ToImmutableArray())));
        }
        return new(summaries.ToImmutableArray()) { CurrentRevisionId = Current?.RevisionId };
    }

    private static string SemanticKnowledgeKey(WorldSemanticKnowledge knowledge) => string.Join('\u0001',
        knowledge.ContainerId, knowledge.OriginalSemanticEvidenceId, knowledge.VerificationEvidenceId,
        knowledge.PageKey, knowledge.PageName, knowledge.PagePurpose, knowledge.Reused);

    private static ImmutableHashSet<string> SemanticKnowledgeKeys(IEnumerable<WorldSemanticKnowledge> knowledge) =>
        knowledge.Select(SemanticKnowledgeKey).ToImmutableHashSet(StringComparer.Ordinal);

    private static string StructuralRelationKey(ContainerRelation relation) =>
        relation.Kind switch
        {
            ContainerRelationKind.Contains => "contains",
            ContainerRelationKind.Overlays => "overlays",
            _ => "navigates-to",
        } + ":" + relation.SourceContainerId + ">" + relation.TargetContainerId;

    private static ImmutableHashSet<string> StructuralRelationKeys(IEnumerable<ContainerRelation> relations) =>
        relations.Where(relation => relation.Kind != ContainerRelationKind.NavigatesTo)
            .Select(StructuralRelationKey).ToImmutableHashSet(StringComparer.Ordinal);
}
