namespace UniClaw.Kernel.Trace;

/// <summary>
/// 只描述 operation 是否正常结构关闭，不描述业务成功（TRC-001：Admission
/// rejected / Gate denied / Outcome Failure 仍是被引用的 domain 结果，
/// 不映射为 trace failure）。
/// </summary>
public enum StructuralOutcome
{
    Completed,
    Faulted,
    Cancelled,
    Incomplete,
}
