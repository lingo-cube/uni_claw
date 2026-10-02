using UniClaw.Kernel.Control;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

/// <summary>
/// 一次现实 Effect 的 post-action verification judgment。它是 Assurance
/// authority 的短生命周期产物，不是 Outcome Proof，也不进入 Run/World truth。
/// AGT-005：升为 public（Host 覆盖遍历 runner 的逐步 trace 消费面——
/// 白名单经 KernelRuntimeSurfaceWhitelistTests 显式修订）。
/// </summary>
public sealed record PostActionEffectVerification(
    string RevisionId,
    TargetSpec Target,
    bool IsVerified,
    IReadOnlyList<AssuranceCheck> Checks,
    string? RejectionReason);

/// <summary>
/// Kernel 编排给 Assurance 的最小验证输入：本轮 P2/P3 处理结果与当前 scoped
/// Slice。Assurance 负责判断是否足以清偿下一次现实 Effect 前的验证屏障。
/// AGT-005 滚动语义：PriorRoute/CurrentRoute/ScrollContentChanged 由 Kernel
/// 从 pre-dispatch belief 与当前 belief 机械派生，仅在 swipe effect 上消费
/// （路由必须未变 ∧ 可见 occurrence 内容集必须变化）。
/// </summary>
internal sealed record PostActionEffectVerificationInput(
    TargetSpec Target,
    Slice Slice,
    IReadOnlyList<KernelResult> ProcessedObservations,
    bool TargetDisappearedAfterRouteChange = false,
    string? PriorRoute = null,
    string? CurrentRoute = null,
    bool? ScrollContentChanged = null);
