using UniClaw.Kernel.Control;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// 一次现实 Effect 的 post-action verification judgment。它是 Assurance
/// authority 的短生命周期产物，不是 Outcome Proof，也不进入 Run/World truth。
/// </summary>
internal sealed record PostActionEffectVerification(
    string RevisionId,
    TargetSpec Target,
    bool IsVerified,
    IReadOnlyList<AssuranceCheck> Checks,
    string? RejectionReason);

/// <summary>
/// Kernel 编排给 Assurance 的最小验证输入：本轮 P2/P3 处理结果与当前 scoped
/// Slice。Assurance 负责判断是否足以清偿下一次现实 Effect 前的验证屏障。
/// </summary>
internal sealed record PostActionEffectVerificationInput(
    TargetSpec Target,
    Slice Slice,
    IReadOnlyList<KernelResult> ProcessedObservations);
