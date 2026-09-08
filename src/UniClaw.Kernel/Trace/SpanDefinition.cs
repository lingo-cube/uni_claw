using System.Collections.Frozen;

namespace UniClaw.Kernel.Trace;

/// <summary>
/// 有界事件定义（TRC-001 S1 硬化，评审 Standards V1 / Spec P1-2）：
/// EventId 封闭于所属 SpanDefinition 的 AllowedEvents；ReasonCode 封闭于
/// AllowedReasonCodes（owner 已发布词汇；ReasonCodeRequired=true 时必须
/// 携带成员码，=false 时禁止携带）。构造权收口：private ctor +
/// internal factory，外部不可铸造。
/// </summary>
public sealed class TraceEventDefinition
{
    private TraceEventDefinition(
        string owningOperationId, string eventId, FrozenSet<string> allowedReasonCodes, bool reasonCodeRequired)
    {
        OwningOperationId = owningOperationId;
        EventId = eventId;
        AllowedReasonCodes = allowedReasonCodes;
        ReasonCodeRequired = reasonCodeRequired;
    }

    public string OwningOperationId { get; }
    public string EventId { get; }
    public FrozenSet<string> AllowedReasonCodes { get; }
    public bool ReasonCodeRequired { get; }

    internal static TraceEventDefinition Create(
        string owningOperationId, string eventId, FrozenSet<string> allowedReasonCodes, bool reasonCodeRequired) =>
        new(owningOperationId, eventId, allowedReasonCodes, reasonCodeRequired);
}

/// <summary>
/// 唯一词表项（TRC-001 S1）：Operation × ObservedTargetOwner ×
/// AllowedReferenceKinds × AllowedEvents。构造权收口（评审 Standards
/// V1）：private ctor + internal factory + Frozen 集合——外部既不可自造
/// operation/owner/events，也不可强转篡改静态 catalog。生产与查询共享
/// 同一 catalog，禁止双份词表。
/// </summary>
public sealed class SpanDefinition
{
    private SpanDefinition(
        string operationId,
        string observedOwner,
        FrozenSet<TraceReferenceKind> allowedReferenceKinds,
        FrozenSet<TraceEventDefinition> allowedEvents,
        bool isProvisional)
    {
        OperationId = operationId;
        ObservedOwner = observedOwner;
        AllowedReferenceKinds = allowedReferenceKinds;
        AllowedEvents = allowedEvents;
        IsProvisional = isProvisional;
    }

    public string OperationId { get; }
    public string ObservedOwner { get; }
    public FrozenSet<TraceReferenceKind> AllowedReferenceKinds { get; }
    public FrozenSet<TraceEventDefinition> AllowedEvents { get; }

    /// <summary>provisional 登记：结构已冻结、未启用（recorder 拒绝
    /// 录制）；随各自 slice 落地转 binding。</summary>
    public bool IsProvisional { get; }

    internal static SpanDefinition Create(
        string operationId,
        string observedOwner,
        FrozenSet<TraceReferenceKind> allowedReferenceKinds,
        FrozenSet<TraceEventDefinition> allowedEvents,
        bool isProvisional = false) =>
        new(operationId, observedOwner, allowedReferenceKinds, allowedEvents, isProvisional);
}

/// <summary>
/// TRC-001 词表（唯一 catalog）：binding 2 项（evidence.admit /
/// world.reconcile）+ provisional 9 项 = 全 11 项登记（评审 Spec P1-2）。
/// provisional 项结构冻结、未启用，事件词表随各自 slice 落地冻结。
/// </summary>
public static class TraceCatalog
{
    /// <summary>evidence.admit 拒绝 reason code 封闭集 = EvidenceLedger
    /// 已发布 admission check 词汇（owner 词汇透传，trace 不铸造 domain
    /// 语义；G4 三边界）。</summary>
    private static readonly FrozenSet<string> AdmissionRejectionReasons = new[]
    {
        "record-integrity",
        "provenance-present",
        "source-identity",
        "capture-time",
        "declared-scope",
        "transformation-lineage",
        "kind-recognized",
        "context-recognized",
        "canonicalization",
    }.ToFrozenSet();

    private static FrozenSet<TraceReferenceKind> Kinds(params TraceReferenceKind[] kinds) => kinds.ToFrozenSet();

    // ---- binding 事件单例（UniKernel 组合缝使用；与所属 Definition 的
    // AllowedEvents 内实例同一——引用同一性即词表同一性）----

    public static TraceEventDefinition Admitted { get; } = TraceEventDefinition.Create(
        "evidence.admit", "admitted", FrozenSet<string>.Empty, reasonCodeRequired: false);

    public static TraceEventDefinition AdmissionRejected { get; } = TraceEventDefinition.Create(
        "evidence.admit", "admission-rejected", AdmissionRejectionReasons, reasonCodeRequired: true);

    public static TraceEventDefinition Reconciled { get; } = TraceEventDefinition.Create(
        "world.reconcile", "reconciled", FrozenSet<string>.Empty, reasonCodeRequired: false);

    public static TraceEventDefinition ReconcileIdempotent { get; } = TraceEventDefinition.Create(
        "world.reconcile", "reconcile-idempotent", FrozenSet<string>.Empty, reasonCodeRequired: false);

    // ---- binding 子集 ----

    /// <summary>evidence.admit：EvidenceLedger admission（P2 seam）。</summary>
    public static SpanDefinition EvidenceAdmit { get; } = SpanDefinition.Create(
        "evidence.admit",
        "EvidenceLedger",
        Kinds(TraceReferenceKind.Run, TraceReferenceKind.Evidence),
        new[] { Admitted, AdmissionRejected }.ToFrozenSet());

    /// <summary>world.reconcile：WorldModel reconciliation（P3 seam）。</summary>
    public static SpanDefinition WorldReconcile { get; } = SpanDefinition.Create(
        "world.reconcile",
        "WorldModel",
        Kinds(TraceReferenceKind.Run, TraceReferenceKind.Evidence, TraceReferenceKind.WorldRevision),
        new[] { Reconciled, ReconcileIdempotent }.ToFrozenSet());

    /// <summary>binding = UniKernel 组合缝当前使用的 operation。</summary>
    public static FrozenSet<SpanDefinition> Binding { get; } =
        new[] { EvidenceAdmit, WorldReconcile }.ToFrozenSet();

    /// <summary>provisional 登记（TRC-001：结构冻结、未启用；reference
    /// kind 集按 v0.1 设计登记，事件词表随各自 slice 落地冻结）。</summary>
    public static FrozenSet<SpanDefinition> Provisional { get; } = new[]
    {
        SpanDefinition.Create("run.execute", "UniKernel",
            Kinds(TraceReferenceKind.Run), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("world.judge-relevance", "WorldModel",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.Evidence), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("control.select-intent", "ControlLoop",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.Intent), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("effect.bind", "EffectBoundary",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.Intent, TraceReferenceKind.Binding), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("assurance.judge-action", "RuntimeAssurance",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.Intent, TraceReferenceKind.Binding, TraceReferenceKind.AssuranceJudgment), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("effect.dispatch", "EffectBoundary",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.Binding, TraceReferenceKind.Receipt), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("assurance.judge-outcome", "RuntimeAssurance",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.AssuranceJudgment, TraceReferenceKind.OutcomeProof), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("run.record-terminal", "RunModel",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.OutcomeProof), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
        SpanDefinition.Create("runtime.emit-outcome", "UniKernel",
            Kinds(TraceReferenceKind.Run, TraceReferenceKind.OutcomeProof, TraceReferenceKind.RuntimeOutcome), FrozenSet<TraceEventDefinition>.Empty, isProvisional: true),
    }.ToFrozenSet();

    /// <summary>全词表：binding + provisional = 11 项，OperationId 无重复。</summary>
    public static FrozenSet<SpanDefinition> All { get; } = Binding.Concat(Provisional).ToFrozenSet();
}
