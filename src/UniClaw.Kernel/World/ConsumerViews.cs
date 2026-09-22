namespace UniClaw.Kernel.World;

/// <summary>
/// Scoped Claim：claim 粒度语义（value + 确立该值的 evidence 引用）的
/// 协议侧表示（EXP-008 / ADR-0011）。WorldClaim 是 World Model 内部类型、
/// 未升格 protocol vocabulary，不跨边界；消费方（Assurance obligation
/// 路径）只见此协议表示。
/// </summary>
public sealed record ScopedClaim(string Value, string EvidenceId);

/// <summary>
/// BindingView — World Model 为 Effect Boundary（Canonical Binding
/// Authority）派生的 consumer view（ADR-0011；EXP-008 D6；UIW-004 增
/// HasTargetOccurrence）。只携带 Owner-owned belief facts：revision 锚 +
/// scope subject 是否存在 claim（字符串通道）+ occurrenceId 是否 ∈ 当前
/// occurrence 投影（UI 通道 owner fact）。四态拒绝（stale / ambiguous /
/// unknown-target / 认定）全部是 Effect Boundary 的判定权，从 fact 推出，
/// 不在 view 内。ephemeral：消费点即时派生、单次消费用毕即弃，不是第二
/// truth。
/// </summary>
public sealed record BindingView(
    string RevisionId,
    int RevisionNumber,
    bool HasTargetSubjectClaim,
    bool HasTargetOccurrence = false,
    SpatialLocator? TargetOccurrenceLocator = null,
    NativeLocator? TargetOccurrenceNative = null);

/// <summary>
/// ActionAssuranceView — World Model 为 RuntimeAssurance.Judge
/// （action-local 判定）派生的 consumer view（EXP-008 D7）。
/// 携带 revision 锚 + FreshnessBasis（已批准跨边界词汇）+ scope
/// subject 上是否存在冲突条目（belief fact）。no-unresolved-conflict /
/// no-blind-retry / currentness 系列检查是 Assurance 的判定权。
/// </summary>
public sealed record ActionAssuranceView(
    string RevisionId,
    int RevisionNumber,
    FreshnessBasis FreshnessBasis,
    bool HasConflictOnTarget);

/// <summary>
/// entity-scoped obligation 的 owner-derived fulfillment fact（ESO-002 D1/D5）：
/// 纯派生 tri-state——Satisfied 是 belief fact；fulfilled 判定权在 Assurance。
/// </summary>
public enum EntityObligationFactKind { Satisfied, Unsatisfied, Unknown }

/// <summary>单条 entity-scoped obligation 的 fulfillment fact（obligation id + tri-state）。</summary>
public sealed record EntityObligationFact(string ObligationId, EntityObligationFactKind Kind);

/// <summary>
/// OutcomeAssuranceView — World Model 为 RuntimeAssurance 的
/// obligation / outcome 路径（EvaluateObligations / JudgeOutcome）派生的
/// consumer view（EXP-008 D8）。claims / conflicts 按 obligation subjects
/// scope；BasisEvidenceIds 为全量 refs——两个真实 buyer：backing
/// membership 检查与 OutcomeProof.BasisEvidenceIds 载荷（OUT-003 锁定
/// 语义，收窄即改 proof 语义，超出 EXP-008 边界）。
/// EntityFacts（ESO-002 D1/D5）：entity-scoped obligation 的 owner-derived
/// tri-state facts（尾部可选，源兼容；无 entity obligation 输入时 null）。
/// </summary>
public sealed record OutcomeAssuranceView(
    string RevisionId,
    int ConflictingClaimCount,
    IReadOnlyDictionary<string, ScopedClaim> Claims,
    IReadOnlyList<Conflict> Conflicts,
    IReadOnlySet<string> BasisEvidenceIds,
    IReadOnlyList<EntityObligationFact>? EntityFacts = null);

/// <summary>
/// ControlBeliefView — World Model 为 Control Loop（PER-009 S5，
/// mechanism.md ⑤）派生的冲突可见性视图。internal：不进公开驱动面
/// （RUN-003 白名单零变更）；owner-derived / ephemeral（ADR-0011 纪律），
/// 只携带 conflicted subjects 列表——聚焦复查的发起依据。
/// </summary>
internal sealed record ControlBeliefView(IReadOnlyList<string> ConflictedSubjects);
