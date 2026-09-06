namespace UniClaw.Kernel.World;

/// <summary>
/// 显式冲突：同 subject 的既存 belief 遭遇不相容的新 accepted evidence。
/// 双方 evidence 引用都保留；不静默覆盖（Target 验收 5）。
/// </summary>
public sealed record Conflict(
    string Subject,
    string EstablishedValue,
    string ChallengingValue,
    string EstablishedEvidenceId,
    string ChallengingEvidenceId);

/// <summary>
/// Freshness：revision 的证据新鲜度（basis 中最晚 capture time）。
/// </summary>
public sealed record Freshness(DateTimeOffset AsOf);

/// <summary>
/// Uncertainty：revision 显式携带的不确定性（本切片：冲突 claim 计数）。
/// </summary>
public sealed record Uncertainty(int ConflictingClaimCount);

/// <summary>
/// World State 中的单条 claim：值 + 确立该值的 evidence 引用
/// （claim 粒度 evidence basis —— 冲突双方可各自溯源）。
/// </summary>
public sealed record WorldClaim(string Value, string EvidenceId);

/// <summary>
/// WorldBelief Revision — canonical belief aggregate（Target §12）：
/// World Graph + World State + Evidence Basis + Freshness + Uncertainty + Conflicts。
/// 不可变；每次 Reconciliation 产生新 revision，历史保留为只读依据。
/// </summary>
public sealed record WorldBeliefRevision(
    string RevisionId,
    string? ParentRevisionId,
    int RevisionNumber,
    IReadOnlyDictionary<string, WorldClaim> WorldState,
    IReadOnlyList<string> WorldGraph,
    IReadOnlySet<string> EvidenceBasis,
    Freshness Freshness,
    Uncertainty Uncertainty,
    IReadOnlyList<Conflict> Conflicts);
