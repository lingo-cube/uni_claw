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
    /// canonical 声明的 LanguageFormatInspector。</summary>
    public static CapabilityRegistry RegisterLanguageInspector(
        CapabilityRegistry? registry = null,
        LanguageFormatInspector? instance = null)
    {
        registry ??= new CapabilityRegistry(TrustDomain.RuntimeIntegration);
        registry.Register(instance ?? new LanguageFormatInspector(), CompositionSource);
        return registry;
    }
}
