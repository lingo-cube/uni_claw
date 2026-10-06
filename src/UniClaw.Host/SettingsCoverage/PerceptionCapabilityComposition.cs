using UniClaw.Kernel.Capability;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>PER-019 / CAP-009 — 感知能力组合根注册。
/// CAP-009 起三层分工：
/// 1. `uni.perception` 注册为**可执行实例**（UniPerceptionCapability 实现双协议
///    marker + 健康聚合 owner；协议-接口一致性经 ValidateImplementation 执法——
///    该执法首次在生产路径行使）。
/// 2. `slow.visual` 维持 description-only（理由：visual 未接线，无运行时实例；
///    R1 例外路径，理由记录在案）。
/// 3. `fast.yolo` / `fast.ocr` 补**声明性依赖锚点**（理由：确定性本地资产、
///    无实例语义；协议词汇留待 fast 能力独立 change 冻结）——闭合依赖图，
///    registry facts 与 capability-facts 落盘随之完整。
/// 描述不含 provider/model 名（模型绑定只在运行时快照）。</summary>
public static class PerceptionCapabilityComposition
{
    public const string UniPerceptionId = "uni.perception";
    public const string SlowVisualId = "slow.visual";
    public const string FastYoloId = "fast.yolo";
    public const string FastOcrId = "fast.ocr";
    public const string CompositionSource = "product-composition-root";

    public static CapabilityRegistry RegisterProductPerception(
        CapabilityRegistry? registry = null,
        UniPerceptionCapability? uniPerception = null)
    {
        registry ??= new CapabilityRegistry(TrustDomain.Product);

        // CAP-009：实例注册（R1）。缺省无健康源（CheckHealth 诚实 Unknown）；
        // 组合根应注入真实探针（fast 资产面 + 模型端 ModelManagement.CheckHealth）。
        var executable = uniPerception ?? new UniPerceptionCapability();
        registry.Register(executable, CompositionSource);

        // CAP-009：fast 依赖锚点（description-only 例外：确定性本地资产，无实例
        // 语义；协议词汇待 fast 能力独立 change 冻结——不预造）。
        registry.Register(FastAnchorDescription(FastYoloId), CompositionSource);
        registry.Register(FastAnchorDescription(FastOcrId), CompositionSource);

        // PER-019 既有：slow.visual 独立能力（description-only 例外：visual 未
        // 接线，无运行时实例可注册）。
        registry.Register(new CapabilityDescription(
            SlowVisualId, "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version) },
            Array.Empty<CapabilityDependency>(),
            HealthStatus.Unknown,
            CapabilityCategory.IndependentProductPerception,
            new[]
            {
                new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic),
                new CapabilityRole(CapabilityRoleKind.Realization, "slow-visual"),
            },
            Array.Empty<CapabilityRelationship>()), CompositionSource);
        return registry;
    }

    private static CapabilityDescription FastAnchorDescription(string capabilityId) => new(
        capabilityId, "1.0.0", CapabilityScope.ProductRuntime,
        Array.Empty<CapabilityProtocol>(),
        Array.Empty<CapabilityDependency>(),
        HealthStatus.Unknown,
        CapabilityCategory.Generic,
        new[] { new CapabilityRole(CapabilityRoleKind.Realization, "local-deterministic-asset") },
        Array.Empty<CapabilityRelationship>());
}
