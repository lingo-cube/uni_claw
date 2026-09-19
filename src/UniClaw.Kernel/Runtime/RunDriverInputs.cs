using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RFS-001 / P24·P26：internal run driver 可拉取的外部输入 cases。typed、
/// 一次性消费；不是 per-cycle 指令，不携带 expected owner state。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal abstract record RunDriverInput
{
    /// <summary>一轮外部观察（initial=External / post-action=PostActionEffectFlow）。</summary>
    public sealed record Observation(IReadOnlyList<ObservationProposal> Proposals) : RunDriverInput;

    /// <summary>宿主/用户 cancel（有界 lifecycle 输入；正式协议仍 Deferred ⑯）。</summary>
    public sealed record Cancel(string Reason, DateTimeOffset VirtualTime) : RunDriverInput;

    /// <summary>stimulus 消费纪律拒绝（context 失配等）——fail closed 信号。</summary>
    public sealed record Unexpected(string Reason) : RunDriverInput;
}

/// <summary>
/// internal run driver 的最小 concrete 外部 seam（delegate 形态；非冻结公共
/// Interface——buyer 证据待 Phase 6）。Simulation Host 与 Product Host 各自
/// 提供 adapter：录制 replay feed / live acquisition + live UniAgent realization。
/// RFS-001：internal 最小 concrete seam——非公共契约；形状随 Phase 5/6 tracer 证据演进（D23）。
/// </summary>
internal sealed class RunDriverInputs
{
    /// <summary>
    /// 拉取下一外部输入；参数 = driver 当前期望的 observation context（串行
    /// 验证屏障与消费纪律的执行面）。null = 无可用输入（合法等待）。
    /// </summary>
    public required Func<ObservationContext, RunDriverInput?> NextInput { get; init; }

    /// <summary>P25 UniAgent consultation seam（唯一调用方 = internal driver）。</summary>
    public required Func<AgentDecisionContext, AgentDecision?> ConsultAgent { get; init; }
}
