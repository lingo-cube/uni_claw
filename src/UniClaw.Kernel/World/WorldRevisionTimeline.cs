namespace UniClaw.Kernel.World;

/// <summary>
/// WORLD-VISUALIZATION-001：Owner 侧 join 的节点稳定语义标签（固定 revision 内）。
/// 无条目 = 语义未知（UI 降级显示），不伪装成"已确认但无名称"；标签不是执行
/// handle，也不越出来源 revision 表达当前事实。Conflicted 只由选定 revision
/// 自身 Conflicts 快照与 RevisionNumber 界定内的 Applied 纠错决定。
/// </summary>
public sealed record WorldNodeLabel(
    string ContainerId,
    string? PageName,
    string? PagePurpose,
    string? PageKey,
    bool Reused,
    bool Conflicted,
    string? KnowledgeSourceRevisionId,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>
/// WORLD-VISUALIZATION-001：revision 时间线只读投影。序数脊柱是 RevisionNumber；
/// LatestCaptureTime 是采集时间高水位（FreshnessBasis.AsOf），不是提交时间——
/// 提交墙钟未记录，不伪造。
/// </summary>
public sealed record WorldRevisionTimeline(
    IReadOnlyList<WorldRevisionSummary> Revisions)
{
    public string? CurrentRevisionId { get; init; }
}

public sealed record WorldRevisionSummary(
    string RevisionId,
    string? ParentRevisionId,
    int RevisionNumber,
    DateTimeOffset? LatestCaptureTime,
    WorldRevisionChangeSummary Changes);

/// <summary>
/// 相对父 revision 的多标记派生变更摘要；全部可从 revision 快照与 Owner 日志
/// 机械推导，无启发式，不是单值 cause 猜测。语义知识比较键排除每 revision
/// 重算字段（ApplicableRevisionId / CurrentCaptureEvidenceId /
/// CorrespondenceEvidenceIds），否则标记恒真。
/// </summary>
public sealed record WorldRevisionChangeSummary(
    bool PageObservation,
    IReadOnlyList<string> ObservedContainerIds,
    bool NavigationVerified,
    bool CorrectionApplied,
    bool CorrectionRejected,
    IReadOnlyList<string> CorrectedCaptureEvidenceIds,
    bool SemanticKnowledgeUpdated,
    bool SemanticKnowledgeReused,
    IReadOnlyList<string> StructuralRelationChanges,
    IReadOnlyList<string> NewEvidenceIds);
