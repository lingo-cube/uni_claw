using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class PerceptionCapabilityRegistryTests
{
    [Fact]
    public void ProductRegistry_RegistersCompositeTextSemantic_WithTypedRolesAndDependencies()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var description = new CapabilityDescription(
            "text.semantic", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version), new CapabilityProtocol(PerceptionProtocol.UiElement, PerceptionProtocol.Version) },
            new[] { new CapabilityDependency("fast.yolo", "1.0"), new CapabilityDependency("fast.ocr", "1.0"), new CapabilityDependency("slow.text", "1.0") },
            HealthStatus.Healthy, CapabilityCategory.CompositeProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic), new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.UiElement), new CapabilityRole(CapabilityRoleKind.Realization, "fast-yolo-ocr-to-slow-text") },
            new[] { new CapabilityRelationship(CapabilityRelationshipKind.Requires, "fast.yolo"), new CapabilityRelationship(CapabilityRelationshipKind.Requires, "fast.ocr"), new CapabilityRelationship(CapabilityRelationshipKind.Requires, "slow.text") });

        registry.Register(description, "product-composition-root");

        Assert.Equal(description, registry.Get("text.semantic"));
        Assert.Equal(CapabilityCategory.CompositeProductPerception, registry.Get("text.semantic")!.Category);
        Assert.DoesNotContain(registry.Facts, f => f.CapabilityId == "slow.text");
    }

    [Fact]
    public void ProductRegistry_RegistersIndependentSlowVisual_WithOneOrTwoProtocols()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var description = new CapabilityDescription(
            "slow.visual", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy, CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic), new CapabilityRole(CapabilityRoleKind.Realization, "slow-visual") },
            Array.Empty<CapabilityRelationship>());

        registry.Register(description, "product-composition-root");

        Assert.Single(registry.Get("slow.visual")!.Protocols);
    }

    [Fact]
    public void ProductRegistry_RejectsProtocolMismatchWithoutPublishingFact()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var description = new CapabilityDescription(
            "slow.visual", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, "9.0") },
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy, CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic) }, Array.Empty<CapabilityRelationship>());

        Assert.Throws<ArgumentException>(() => registry.Register(description, "root"));
        Assert.Empty(registry.Facts);
        Assert.Null(registry.Get("slow.visual"));
    }

    [Fact]
    public void ProductRegistry_RejectsSourceOrAdapterClaimOnProductDescription()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var description = new CapabilityDescription(
            "xml.source", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.UiElement, PerceptionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy, CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.UiElement), new CapabilityRole(CapabilityRoleKind.Source, "xml-hierarchy") }, Array.Empty<CapabilityRelationship>());

        Assert.Throws<ArgumentException>(() => registry.Register(description, "root"));
        Assert.Empty(registry.Facts);
    }

    [Fact]
    public void ProductRegistry_RejectsUnknownProtocol_AndDanglingAssemblyRelationship()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var unknownProtocol = new CapabilityDescription(
            "unknown.protocol", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol("Unknown Perception", PerceptionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy,
            CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, "Unknown Perception") },
            Array.Empty<CapabilityRelationship>());

        Assert.Throws<ArgumentException>(() => registry.Register(unknownProtocol, "root"));

        var danglingRelationship = new CapabilityDescription(
            "dangling.relationship", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version) },
            new[] { new CapabilityDependency("slow.text", "1.0") }, HealthStatus.Healthy,
            CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic) },
            new[] { new CapabilityRelationship(CapabilityRelationshipKind.Requires, "unlisted.provider") });

        Assert.Throws<ArgumentException>(() => registry.Register(danglingRelationship, "root"));
        Assert.Empty(registry.Facts);
    }
}
