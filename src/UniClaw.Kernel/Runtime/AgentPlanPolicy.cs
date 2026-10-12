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

    // ---- PROTOCOL-FIX-003（ADR-0050 D1/D5，FROZEN v0.3.1 参数面窄修订）：
    //      预算两层顶——契约预算 = 初始额度，本 policy 声明的 HardCap =
    //      续授不可突破的天花板（步数/轮次各自独立）。缺省（全 null）=
    //      续授通道整体关闭，行为与既有完全一致（A5 回归锚定）。

    /// <summary>续授不可突破的步数硬顶（有限）；null = 步数维度不可续授。</summary>
    public int? HardCapTotalSteps { get; init; }

    /// <summary>续授不可突破的轮次硬顶（有限）；null = 轮次维度不可续授。</summary>
    public int? HardCapConsultations { get; init; }

    /// <summary>续授窗口阈值：步数剩余 ≤ 该值时窗口开启（默认 0 = 不开窗）。</summary>
    public int RenewalThresholdSteps { get; init; }

    /// <summary>续授窗口阈值：轮次剩余 ≤ 该值时窗口开启（默认 0 = 不开窗）。</summary>
    public int RenewalThresholdRounds { get; init; }

    /// <summary>当前已采纳 specs（观察面；null = 尚未采纳）。</summary>
    public IReadOnlyList<TargetSpec>? Adopted { get; private set; }

    /// <summary>
    /// PER-009 S5：冲突可见性（driver 在 Decide 前推送；internal 视图，
    /// 零公开面变更）。悬案与已采纳目标相交 → Observe + TargetSubject
    /// （聚焦复查语义：复用既有意图面表达"observe focused on this subject"，
    /// 不新增 ControlIntentKind 成员——mechanism.md ⑤ 的最小实现）。
    /// </summary>
    internal IReadOnlyList<string>? ConflictedSubjects { get; set; }

    /// <summary>采纳（整体替换）经 driver 校验通过的 proposal specs。</summary>
    public void Adopt(IReadOnlyList<TargetSpec> specs)
    {
        Adopted = specs;
        _adopted = new DescriptorTargetPolicy(specs);
    }

    public ControlDecision Decide(ControlInputs inputs)
    {
        if (ConflictedSubjects is { Count: > 0 } conflicts
            && FirstConflictIntersectingTarget(conflicts) is { } conflictedSubject)
        {
            return new ControlDecision(ControlIntentKind.Observe, EffectClass: null, TargetSubject: conflictedSubject);
        }
        return _adopted?.Decide(inputs)
            ?? new ControlDecision(ControlIntentKind.Observe, EffectClass: null, TargetSubject: null);
    }

    /// <summary>
    /// 相交判定：conflicted subject == 目标序列化（"role[:desc]"）或以其为
    /// 目标前缀。无采纳目标 → 不聚焦（null）。
    /// </summary>
    private string? FirstConflictIntersectingTarget(IReadOnlyList<string> conflicts)
    {
        if (Adopted is not { Count: > 0 } specs)
            return null;
        foreach (var spec in specs)
        {
            var target = spec.SemanticDescriptor is null ? spec.Role : $"{spec.Role}:{spec.SemanticDescriptor}";
            foreach (var conflicted in conflicts)
            {
                if (conflicted == target
                    || conflicted.StartsWith(target + ".", StringComparison.Ordinal))
                {
                    return conflicted;
                }
            }
        }
        return null;
    }
}
