namespace UniClaw.Kernel.Trace;

/// <summary>
/// 唯一词表项（TRC-001 S1）：Operation × ObservedTargetOwner ×
/// AllowedReferenceKinds × AllowedEvents。生产与查询共享同一 catalog，
/// 禁止双份词表（legacy 反例：Runtime 与 DriverHost 各维护一份）。
/// </summary>
public sealed record SpanDefinition(
    string OperationId,
    string ObservedOwner,
    IReadOnlySet<TraceReferenceKind> AllowedReferenceKinds,
    IReadOnlySet<string> AllowedEvents);

/// <summary>
/// TRC-001 binding 子集：evidence.admit / world.reconcile。其余 9 项
/// catalog（run.execute … runtime.emit-outcome）为 provisional，随各自
/// slice 落地逐项冻结（决策见 changes/TRC-001/state.md）。
/// </summary>
public static class TraceCatalog
{
    /// <summary>evidence.admit：EvidenceLedger admission（P2 seam）。</summary>
    public static SpanDefinition EvidenceAdmit { get; } = new(
        "evidence.admit",
        "EvidenceLedger",
        new HashSet<TraceReferenceKind> { TraceReferenceKind.Run, TraceReferenceKind.Evidence },
        new HashSet<string> { "admitted", "admission-rejected" });

    /// <summary>world.reconcile：WorldModel reconciliation（P3 seam）。</summary>
    public static SpanDefinition WorldReconcile { get; } = new(
        "world.reconcile",
        "WorldModel",
        new HashSet<TraceReferenceKind>
        {
            TraceReferenceKind.Run, TraceReferenceKind.Evidence, TraceReferenceKind.WorldRevision,
        },
        new HashSet<string> { "reconciled", "reconcile-idempotent" });
}
