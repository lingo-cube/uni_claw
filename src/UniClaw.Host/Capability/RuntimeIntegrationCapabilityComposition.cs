using UniClaw.Kernel.Capability;

namespace UniClaw.Host.Capability;

/// <summary>
/// CAP-012 — Runtime Integration 域能力组合根（ADR-0035 三域独立：本域使用
/// 独立 Registry，与 Product Registry 不共享可变注册状态）。注册
/// `runtime.language-inspector`（Language Inspection@1.0，可执行实例——
/// 确定性 Unicode 脚本规则；R5 可替换：换规则只换 L2 实现与组合根声明）。
/// </summary>
public static class RuntimeIntegrationCapabilityComposition
{
    public const string CompositionSource = "product-composition-root";

    /// <summary>注册语言检查能力**实例**（R1：可执行能力必须实例注册；
    /// LanguageInspection 类别拒绝 description-only）。instance 缺省为
    /// canonical 声明的 LanguageFormatInspector（加载全局术语白名单——
    /// D2/D8：缺文件照跑 + 剖面披露影响）。</summary>
    public static CapabilityRegistry RegisterLanguageInspector(
        CapabilityRegistry? registry = null,
        LanguageFormatInspector? instance = null)
    {
        registry ??= new CapabilityRegistry(TrustDomain.RuntimeIntegration);
        registry.Register(
            instance ?? new LanguageFormatInspector(allowlistTerms: LanguageInspectionAllowlist.LoadDefault()),
            CompositionSource);
        return registry;
    }

    /// <summary>
    /// 把当前 Runtime Integration 注册表中的可执行剖面投影给 Agent。
    /// 每次调用都重新读取生命周期事实和实例剖面，避免把启动时快照当成
    /// 运行期真相；不可用或没有剖面自述的条目不会进入可选择集合。
    /// </summary>
    public static IReadOnlyDictionary<string, CapabilityProfileReport>
        DescribeAvailableProfiles(CapabilityRegistry? registry)
    {
        if (registry is null)
            return new Dictionary<string, CapabilityProfileReport>(StringComparer.Ordinal);

        var current = registry.Facts
            .GroupBy(fact => fact.CapabilityId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .Where(fact => fact.Lifecycle is CapabilityLifecycle.Registered
                or CapabilityLifecycle.Ready
                or CapabilityLifecycle.Active);
        var profiles = new Dictionary<string, CapabilityProfileReport>(StringComparer.Ordinal);
        foreach (var fact in current)
        {
            if (registry.Resolve(fact.CapabilityId) is not ICapabilityProfileReporting reporting)
                continue;
            var profile = reporting.DescribeProfile();
            if (!profile.IsValid)
                throw new InvalidOperationException(
                    $"capability-profile-invalid:{fact.CapabilityId}");
            profiles.Add(fact.CapabilityId, profile);
        }
        return profiles;
    }
}
