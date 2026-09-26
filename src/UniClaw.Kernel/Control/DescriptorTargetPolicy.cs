using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;

using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.Control;

/// <summary>
/// 目标规格（CTL-001 D1）：authoring 侧表达「对什么 affordance 施加什么
/// effect」。Role 必须；SemanticDescriptor 可选收窄（null = 该 role 任意
/// occurrence）；EffectClass 为拟施加的 effect 类。objective→target-spec 的
/// authoring 语义显式 out-of-scope（由调用侧构造）。
/// DesiredState（CDS-001 / ADR-0017；PER-014 R3 值域迁移）：可选期望终态，
/// 值域 = <see cref="CheckedState"/>（typed semantic checked；替代 legacy
/// on/off 字符串域）。非 null 时 policy 在签发 act 前做 desired-state
/// satisfaction 检查——非幂等物理动作（toggle 物理执行 = tap，对已满足目标
/// 再执行会破坏状态）的安全性必须在 action 发出之前由 Control 的世界状态
/// 语义保证（I-3：post-action verification 只能发现破坏，不能防止破坏）。
/// null（Click 等无期望终态型）不做检查。occurrence presentation state 经
/// <see cref="CheckedSemantics.FromPresentation"/> 严格映射后比较；映射
/// 不出（Unknown）不判 satisfied（fail-closed，不折叠、不猜值）。
/// </summary>
public sealed record TargetSpec(
    string Role,
    string? SemanticDescriptor,
    string EffectClass,
    CheckedState? DesiredState = null);

/// <summary>
/// 参考确定性 policy（CTL-001 产品 realization；D9 先例「intent 选择可无需
/// AI」）——重构后 Slice occurrence 景观的第一个真实 reader（ADR-0011）。
/// 按 spec 顺序（顺序即优先级）在 inputs.Slice.Occurrences 中找
/// （Role 相等 ∧ descriptor 相等[若给] ∧ OwningContainerId ∈ InScope 或 null）
/// 且未 visited 的首个 occurrence → Act（TargetSubject = descriptor 序列化
/// "role[:desc]"，EffectClass 取 spec）；全部 spec 无匹配或景观空 → Observe。
/// 确定性（D4）：私有 visited 簿记仅由 Decide 调用序列驱动，零
/// wall-clock / random——同序列同输出（replay 稳定）。
/// </summary>
public sealed class DescriptorTargetPolicy : IControlPolicy
{
    private readonly IReadOnlyList<TargetSpec> _specs;

    /// <summary>traversal 簿记（D2）：键 = (Role, SemanticDescriptor)。
    /// descriptor-keyed——occurrence id 是 revision-local 不能作键；这是
    /// Control 侧 hypothesis 簿记（非 world truth），referent 变化后的误标
    /// 可接受。</summary>
    private readonly HashSet<(string Role, string? SemanticDescriptor)> _visited = new();

    /// <param name="specs">按优先级排序的目标规格；空列表 = 恒 Observe。</param>
    public DescriptorTargetPolicy(IReadOnlyList<TargetSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        if (specs.Any(s => s is null))
            throw new ArgumentException("specs 含 null 条目", nameof(specs));
        _specs = specs;
    }

    /// <summary>visited 簿记只读视图（测试断言面）。</summary>
    public IReadOnlyCollection<(string Role, string? SemanticDescriptor)> Visited => _visited;

    public ControlDecision Decide(ControlInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var inScope = inputs.Slice.InScopeContainerIds.ToHashSet();

        foreach (var spec in _specs)
        {
            var occurrence = inputs.Slice.Occurrences.FirstOrDefault(o =>
                OccurrenceDescriptorMatcher.Matches(
                    o.Role, o.SemanticDescriptor, o.OwningContainerId,
                    spec.Role, spec.SemanticDescriptor, targetOwningContainerId: null,
                    ContainerMatchMode.ScopeMembership, inScope)
                && !_visited.Contains((spec.Role, o.SemanticDescriptor)));
            if (occurrence is null)
                continue;

            // CDS-001 desired-state satisfaction（HD-4 / ADR-0017，I-3 事前保证）：
            // Satisfied  → 该 spec 完成（标 visited、无 intent——「无需行动」是
            //              decision outcome，不是 NoOp effect）；
            // Unknown    → 跳过不标 visited（State=null ≠ false；待新观察可判——
            //              observe/resolve per policy，fail-closed 不 dispatch）；
            // Unsatisfied → Act（全链不变）。
            if (spec.DesiredState is not null)
            {
                // PER-014 R3：presentation state → typed checked 严格映射；
                // Unknown（含 null）不 Act 也不误判 satisfied（零折叠）。
                var observed = CheckedSemantics.FromPresentation(occurrence.State);
                if (observed is null)
                    continue; // Unknown：不 Act、不 visited
                if (observed == spec.DesiredState)
                {
                    // Satisfied：目标已达成，零物理动作（I-3——防 tap 破坏）
                    _visited.Add((spec.Role, occurrence.SemanticDescriptor));
                    continue;
                }
            }

            // D2：descriptor-keyed visited（dispatch 成败不影响——失败恢复走
            // ControlLoop 既有强制 Recovery，本 policy 只管 traversal 推进）
            _visited.Add((spec.Role, occurrence.SemanticDescriptor));
            return new ControlDecision(
                ControlIntentKind.Act,
                EffectClass: spec.EffectClass,
                TargetSubject: SerializeSubject(spec));
        }

        return new ControlDecision(ControlIntentKind.Observe, EffectClass: null, TargetSubject: null);
    }

    private static string SerializeSubject(TargetSpec spec) =>
        spec.SemanticDescriptor is null ? spec.Role : $"{spec.Role}:{spec.SemanticDescriptor}";
}
