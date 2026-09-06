namespace UniClaw.Kernel.Effects;

/// <summary>机械投递结果（两态，Assumption：scripted driver 确定性产出）。</summary>
public enum DispatchOutcome
{
    /// <summary>下游确认接收/发送。</summary>
    Delivered,

    /// <summary>投递失败（receipt 仍为 attempt evidence）。</summary>
    Failed,
}

/// <summary>Driver 单次投递的完整结果（含下游确认时间，供 attempt evidence 溯源）。</summary>
public sealed record DispatchResult(DispatchOutcome Outcome, string Report, DateTimeOffset CompletedAt);

/// <summary>
/// 机械投递缝（Target §16.2 Dispatch）：只负责 delivery 并如实报告，
/// 不得自行 retry、recover、replan 或改变 strategy（不变量 27）。
/// </summary>
public interface IEffectDriver
{
    DispatchResult Deliver(CanonicalBinding binding);
}

/// <summary>
/// Effect Gate 决定：只执行或拒绝既有 authorization judgment，不重新判断、
/// 不改变 target、不扩大 effect（不变量 26，验收 4）。
/// </summary>
public sealed record GateDecision(bool Allowed, string? Reason);

/// <summary>
/// Effect Receipt — dispatch 后的不可变留痕（Target §16.2，§17）。
/// 是 attempt evidence，不直接证明 Effect（不变量 33/34，验收 5）。
/// </summary>
public sealed record EffectReceipt(
    string ReceiptId,
    string IntentId,
    string BindingId,
    string TargetSubject,
    string RevisionId,
    int RevisionNumber,
    DispatchOutcome Outcome,
    string Report,
    DateTimeOffset DispatchedAt);
