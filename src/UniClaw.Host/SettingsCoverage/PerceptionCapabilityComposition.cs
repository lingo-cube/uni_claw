using UniClaw.Kernel.Capability;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>PER-019 — 组合根能力注册：Product 域注册 UniPerception（组合，
/// 2026-10-06 命名裁决，旧名 Text Semantic Perception；依赖链 fast.yolo→
/// fast.ocr→slow.text，双协议）与 slow.visual（独立）。描述不含 provider/model
/// 名（模型绑定只属 adapter 层）；Hub 只做管理面，不路由感知数据。</summary>
public static class PerceptionCapabilityComposition
{
    public const string UniPerceptionId = "uni.perception";
    public const string SlowVisualId = "slow.visual";
    public const string CompositionSource = "product-composition-root";

    public static CapabilityRegistry RegisterProductPerception(CapabilityRegistry? registry = null)
    {
        registry ??= new CapabilityRegistry(TrustDomain.Product);
        registry.Register(new CapabilityDescription(
            UniPerceptionId, "1.0.0", CapabilityScope.ProductRuntime,
            new[]
            {
                new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version),
                new CapabilityProtocol(PerceptionProtocol.UiElement, PerceptionProtocol.Version),
            },
            new[]
            {
                new CapabilityDependency("fast.yolo", "1.0"),
                new CapabilityDependency("fast.ocr", "1.0"),
                new CapabilityDependency("slow.text", "1.0"),
            },
            HealthStatus.Unknown,
            CapabilityCategory.CompositeProductPerception,
            new[]
            {
                new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic),
                new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.UiElement),
                new CapabilityRole(CapabilityRoleKind.Realization, "fast-yolo-ocr-to-slow-text"),
            },
            new[]
            {
                new CapabilityRelationship(CapabilityRelationshipKind.Requires, "fast.yolo"),
                new CapabilityRelationship(CapabilityRelationshipKind.Requires, "fast.ocr"),
                new CapabilityRelationship(CapabilityRelationshipKind.Requires, "slow.text"),
            }), CompositionSource);
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
}
