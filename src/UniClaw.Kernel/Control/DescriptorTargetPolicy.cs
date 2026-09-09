using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Control;

/// <summary>
/// 目标规格（CTL-001 D1）：authoring 侧表达「对什么 affordance 施加什么
/// effect」。Role 必须；SemanticDescriptor 可选收窄（null = 该 role 任意
/// occurrence）；EffectClass 为拟施加的 effect 类。objective→target-spec 的
/// authoring 语义显式 out-of-scope（由调用侧构造）。
/// </summary>
public sealed record TargetSpec(string Role, string? SemanticDescriptor, string EffectClass);

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
                o.Role == spec.Role
                && (spec.SemanticDescriptor is null || o.SemanticDescriptor == spec.SemanticDescriptor)
                && (o.OwningContainerId is null || inScope.Contains(o.OwningContainerId))
                && !_visited.Contains((spec.Role, o.SemanticDescriptor)));
            if (occurrence is null)
                continue;

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
