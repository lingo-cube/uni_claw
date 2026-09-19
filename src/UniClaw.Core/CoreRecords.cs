namespace UniClaw.Core;

// 候选语义记录（CORE-002 tracer / CORE-003 对象基线；不冻结最终字段/继承树/
// 包名/存储布局）。
// 2026-09-19 realization 接触后的两处候选精化：
//  1) Slice 观察依据列表化（realization 的 slice 依据是 revision evidence
//     basis 集合，无单一触发 evidence 可诚实选取）；
//  2) Attempt↔Binding 方向改为 Attempt.BindingId（attempt→binding），与
//     已验证 realization 的 receipt↔BindingId 方向一致（binding 先于
//     attempt 存在；同 binding 至多一次投递）。
// CORE-003（对象基线）：
//  - Clause 携带 Kind（Requirement/Permission/Constraint/Criterion）——
//    要求与权限只能以显式 Clause 存在，观察内容永不自动产生授权（Core 内
//    无任何从 Evidence/Claim 导出 Clause 的 API，结构性保证）；
//  - Event 发生语义（发生/来源/时间/参与者/次数/同一性/因果判断）由
//    Evidence + Claim 组合表达，不建独立 Event 类型（CORE-001 §4.4 待验证，
//    独立类型仅在出现表达/演化反例时引入）。
// CORE-006（Effect 执行契约候选）：
//  - Attempt 的请求快照、执行端、授权依据与投递/外部执行/协调三轴保持分离；
//  - TargetBinding 可携带 Slice 或固定非 Slice BasisReference；缺少固定依据的
//    Canonical binding 不放行。以下字段仍是候选语义位置，不冻结最终 API/存储。

public readonly record struct CoreId(string Value)
{
    public override string ToString() => Value;
}

public enum ClaimDisposition
{
    Candidate,
    Accepted,
    Conflict,
    Rejected,
    Unknown
}

public enum BindingDisposition
{
    Candidate,
    Canonical,
    Stale,
    Ambiguous,
    Unauthorized,
    Unverified
}

public enum DeliveryOutcome
{
    Pending,
    Completed,
    Failed,
    Unknown
}

public enum DispatchProgress
{
    NotStarted,
    Started,
    Accepted,
    Rejected,
    Unknown
}

public enum ExternalExecutionStatus
{
    NotObserved,
    Pending,
    Completed,
    Failed,
    Conflicted,
    Unknown
}

public enum CoordinationStatus
{
    Open,
    Satisfied,
    Blocked,
    Unknown
}

/// <summary>Clause 种类（CORE-003）：要求 / 权限 / 约束 / 判据。
/// 四词为候选词汇，不锁最终枚举；权限只能以 ClauseKind.Permission 的
/// 显式 Clause 存在。</summary>
public enum ClauseKind
{
    Requirement,
    Permission,
    Constraint,
    Criterion
}

public sealed record Clause(
    CoreId Id,
    ClauseKind Kind,
    string Statement,
    DateTimeOffset EstablishedAt);

public sealed record Segment(
    CoreId Id,
    string SemanticKind);

public sealed record EvidenceRecord(
    CoreId Id,
    CoreId SubjectId,
    DateTimeOffset ObservedAt,
    string Source,
    string Context,
    IReadOnlyList<string> ProcessingChain);

public sealed record Slice(
    CoreId Id,
    CoreId SegmentId,
    IReadOnlyList<CoreId> ObservationEvidenceIds,
    DateTimeOffset ObservedAt,
    string Scope,
    IReadOnlyList<CoreId> ObservedRecordIds,
    string Coverage);

public sealed record Claim(
    CoreId Id,
    CoreId SubjectId,
    string Predicate,
    string Value,
    ClaimDisposition Disposition,
    IReadOnlyList<CoreId> EvidenceBasis);

/// <summary>固定依据引用：记录标识、引用种类和固定快照/资源版本键。
/// Kind 与 SnapshotKey 由领域契约解释，Core 不解析其词汇。</summary>
public sealed record BasisReference(
    CoreId ReferenceId,
    string Kind,
    string SnapshotKey);

/// <summary>Attempt 的三条独立状态轴。投递进展、外部执行判断和协调状态可以
/// 同时处于不同值，不能压成一个生命周期枚举。</summary>
public sealed record AttemptExecutionState(
    DispatchProgress Dispatch,
    ExternalExecutionStatus ExternalExecution,
    CoordinationStatus Coordination);

public sealed record Effect(
    CoreId Id,
    string Operation,
    CoreId SubjectId);

public sealed record Attempt(
    CoreId Id,
    CoreId EffectId,
    CoreId BindingId,
    DateTimeOffset StartedAt,
    DeliveryOutcome Delivery,
    CoreId? DeliveryEvidenceId,
    CoreId? RequestSnapshotId = null,
    CoreId? ExecutorId = null,
    IReadOnlyList<CoreId>? AuthorizationBasis = null,
    AttemptExecutionState? ExecutionState = null);

public sealed record TargetBinding(
    CoreId Id,
    CoreId TargetSegmentId,
    CoreId? BasisSliceId,
    string LocatorKind,
    string LocatorKey,
    BindingDisposition Disposition,
    IReadOnlyList<BasisReference>? BasisReferences = null);

public static class CoreInvariants
{
    public static bool IsHistoricalBasisStable(TargetBinding binding, CoreId currentSliceId)
        => binding.BasisSliceId == currentSliceId;

    public static bool IsHistoricalBasisStable(TargetBinding binding, BasisReference currentReference)
        => binding.BasisReferences?.Contains(currentReference) == true;

    public static bool HasFixedBasis(TargetBinding binding)
        => binding.BasisSliceId is not null
            || binding.BasisReferences is { Count: > 0 };

    public static bool CanDispatch(TargetBinding binding)
        => binding.Disposition == BindingDisposition.Canonical
            && HasFixedBasis(binding);

    public static bool IsTerminalDelivery(Attempt attempt)
        => attempt.Delivery is DeliveryOutcome.Completed or DeliveryOutcome.Failed;
}
