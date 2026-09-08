namespace UniClaw.Kernel.Trace;

/// <summary>
/// TraceReference 封闭 union 成员（TRC-001 决策：全 10 项词表现在冻结；
/// bullet 只铸造 Run / Evidence / WorldRevision 三种，其余随各自 slice
/// 落地启用）。
/// </summary>
public enum TraceReferenceKind
{
    Run,
    Evidence,
    WorldRevision,
    AssociationDecision,
    Intent,
    Binding,
    AssuranceJudgment,
    Receipt,
    OutcomeProof,
    RuntimeOutcome,
}

/// <summary>
/// 指向某 Owner 已产生 immutable fact 的 typed locator（string 值 = 该
/// Owner 的稳定标识，或已冻结的 correlation tuple 串联）。Trace 只持
/// 引用，不铸造 canonical-looking identity，不复制事实。
/// </summary>
public sealed record TraceReference(TraceReferenceKind Kind, string Value);
