namespace UniClaw.Kernel.Capability;

/// <summary>
/// CAP-012 D7 — 影响披露：在某条件下，结果会受到什么影响（"没配白名单会怎样"
/// 的机器可读形态）。事实归能力；表达归 agent（skill 剖面消费模板）。
/// </summary>
public sealed record CapabilityImpactDisclosure(
    string Condition,
    string Impact)
{
    /// <summary>条件与影响均非空。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Condition) && !string.IsNullOrWhiteSpace(Impact);
}

/// <summary>
/// CAP-012 D7 — 运行剖面报告：Summary（语义摘要）+ 生效配置（键值化，含
/// "未加载/未配置"状态照实说）+ 影响披露 + Limitations（已知不做的事）。
/// 拉式只读；Hub 不参与；JSON 可序列化（run 落盘为事实源）。
/// </summary>
public sealed record CapabilityProfileReport(
    string Summary,
    IReadOnlyDictionary<string, string> EffectiveConfiguration,
    IReadOnlyList<CapabilityImpactDisclosure> ImpactDisclosures,
    IReadOnlyList<string> Limitations)
{
    /// <summary>摘要非空；披露项均有效。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Summary)
        && EffectiveConfiguration is not null
        && ImpactDisclosures is not null
        && ImpactDisclosures.All(d => d is { IsValid: true })
        && Limitations is not null;
}

/// <summary>
/// CAP-012 D9 — 运行剖面能力面（第三个可选 mixin，与
/// <see cref="ICapabilityHealthCheckable"/> 同构）：能力自表达"我在什么配置下
/// 运行、缺了什么、对结果有什么影响"。消费面是 uni agent（模板/提示词引导其
/// 表达边界与范围）；缺配置类豁免的缺省行为是照跑 + 披露（D8），不把缺配置
/// 放大成能力不可用。
/// </summary>
public interface ICapabilityProfileReporting
{
    /// <summary>拉式只读：当前生效配置、影响披露与已知边界。</summary>
    CapabilityProfileReport DescribeProfile();
}
