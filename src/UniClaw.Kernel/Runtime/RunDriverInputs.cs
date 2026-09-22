using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RFS-001 / P24·P26：internal run driver 可拉取的外部输入 cases。typed、
/// 一次性消费；不是 per-cycle 指令，不携带 expected owner state。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public abstract record RunDriverInput
{
    /// <summary>一轮外部观察（initial=External / post-action=PostActionEffectFlow）。</summary>
    public sealed record Observation(IReadOnlyList<ObservationProposal> Proposals) : RunDriverInput;

    /// <summary>宿主/用户 cancel（有界 lifecycle 输入；正式协议仍 Deferred ⑯）。</summary>
    public sealed record Cancel(string Reason, DateTimeOffset VirtualTime) : RunDriverInput;

    /// <summary>stimulus 消费纪律拒绝（context 失配等）——fail closed 信号。</summary>
    public sealed record Unexpected(string Reason) : RunDriverInput;
}

/// <summary>
/// 观察深度档位（PER-009 mechanism ①，台账 #20 A 方案）。Focused = Tier 1
/// 定向复查；Deep 留给 Tier 2 change（枚举成员扩展非破坏，届时按 D6 触发）。
/// </summary>
public enum ObservationDepth
{
    /// <summary>常规周期观察（快档常开）。</summary>
    Normal,

    /// <summary>定向复查（悬案聚焦：Subjects 必非空）。</summary>
    Focused,
}

/// <summary>
/// 观察指令：driver 当前期望的观察方向（context + 深度 + 聚焦 subjects）。
/// PER-009 A 方案（台账 #20 用户裁决）：聚焦复查经 Kernel 驱动面传导——
/// Observation Control 权威保持在 Control Loop（baseline §14），双 Host
/// 生而同构（§24.8）。Subjects 非空 ⇔ Depth=Focused。
/// </summary>
public sealed record ObservationDirective(
    ObservationContext Context,
    ObservationDepth Depth = ObservationDepth.Normal,
    IReadOnlyList<string>? Subjects = null);

/// <summary>
/// internal run driver 的最小 concrete 外部 seam（delegate 形态；非冻结公共
/// Interface——buyer 证据待 Phase 6）。Simulation Host 与 Product Host 各自
/// 提供 adapter：录制 replay feed / live acquisition + live UniAgent realization。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed class RunDriverInputs
{
    /// <summary>
    /// 拉取下一外部输入；参数 = 观察指令（context + 深度 + 聚焦 subjects——
    /// 串行验证屏障与消费纪律的执行面；Focused 为 PER-009 聚焦复查通道）。
    /// null = 无可用输入（合法等待）。
    /// </summary>
    public required Func<ObservationDirective, RunDriverInput?> NextInput { get; init; }

    /// <summary>P25 UniAgent consultation seam（唯一调用方 = internal driver）。</summary>
    public required Func<AgentDecisionContext, AgentDecision?> ConsultAgent { get; init; }
}
