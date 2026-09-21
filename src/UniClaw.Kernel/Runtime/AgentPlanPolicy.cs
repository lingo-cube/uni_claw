using UniClaw.Kernel.Control;
using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Runtime;

/// <summary>
/// RFS-001：Control 侧跟随已采纳 agent proposal 的 policy realization。
/// proposal 是 advisory 输入（baseline §24.2）：本 policy 只把它翻译为
/// TargetSpec；Control Loop 仍是唯一 Control Intent Authority，intent 选择、
/// desired-state satisfaction 与 visited 簿记全部沿用既有 DescriptorTargetPolicy
/// 语义。未采纳任何 proposal 时返回 Observe（合法：无 plan → 观察）。
/// RUN-003：公开组合缝（Product Host 买方，HOST-001 D8 裁决）；公开面白名单执法（KernelRuntimeSurfaceWhitelistTests）。
/// </summary>
public sealed class AgentPlanPolicy : IControlPolicy
{
    private DescriptorTargetPolicy? _adopted;

    /// <summary>当前已采纳 specs（观察面；null = 尚未采纳）。</summary>
    public IReadOnlyList<TargetSpec>? Adopted { get; private set; }

    /// <summary>采纳（整体替换）经 driver 校验通过的 proposal specs。</summary>
    public void Adopt(IReadOnlyList<TargetSpec> specs)
    {
        Adopted = specs;
        _adopted = new DescriptorTargetPolicy(specs);
    }

    public ControlDecision Decide(ControlInputs inputs) =>
        _adopted?.Decide(inputs)
        ?? new ControlDecision(ControlIntentKind.Observe, EffectClass: null, TargetSubject: null);
}
