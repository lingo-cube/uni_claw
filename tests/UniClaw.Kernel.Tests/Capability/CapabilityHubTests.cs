using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class CapabilityHubTests
{
    private sealed class SemanticCapability : ISemanticPerception
    {
        public CapabilityDescription Description { get; } = new(
            "semantic.test", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy,
            CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic) },
            Array.Empty<CapabilityRelationship>());
    }

    private sealed class CapabilityWithoutSemanticInterface : ICapability
    {
        public CapabilityDescription Description { get; } = new(
            "semantic.mismatch", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy,
            CapabilityCategory.IndependentProductPerception,
            new[] { new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic) },
            Array.Empty<CapabilityRelationship>());
    }

    [Fact]
    public void Register_PreservesDescription_AndPublishesCommittedFact()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var description = new CapabilityDescription(
            "screen.reader", "1.2.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol("observation", "1") },
            new[] { new CapabilityDependency("capture", "1") }, HealthStatus.Healthy);

        var fact = registry.Register(description, "product-composition-root");

        Assert.Equal(CapabilityLifecycle.Registered, fact.Lifecycle);
        Assert.Equal(1, fact.Sequence);
        Assert.Equal("product-composition-root", fact.Source);
        Assert.Equal(description, registry.Get("screen.reader"));
    }

    [Fact]
    public void Lifecycle_RejectsIllegalTransition_AndSequenceOnlyAdvancesOnCommit()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        registry.Register(new CapabilityDescription("x", "1", CapabilityScope.ProductRuntime,
            Array.Empty<CapabilityProtocol>(), Array.Empty<CapabilityDependency>(), HealthStatus.Unknown), "root");

        Assert.Throws<InvalidOperationException>(() => registry.Commit("x", CapabilityLifecycle.Closed, "root"));
        Assert.Equal(1, registry.Facts.Single().Sequence);
    }

    [Fact]
    public void Registries_AreTrustDomainScoped()
    {
        var product = new CapabilityRegistry(TrustDomain.Product);
        var harness = new CapabilityRegistry(TrustDomain.Harness);
        product.Register(new CapabilityDescription("x", "1", CapabilityScope.ProductRuntime,
            Array.Empty<CapabilityProtocol>(), Array.Empty<CapabilityDependency>(), HealthStatus.Healthy), "root");

        Assert.Null(harness.Get("x"));
        Assert.Throws<ArgumentException>(() => product.Register(new CapabilityDescription(
            "h", "1", CapabilityScope.Harness, Array.Empty<CapabilityProtocol>(),
            Array.Empty<CapabilityDependency>(), HealthStatus.Healthy), "root"));
    }

    [Fact]
    public void Hub_RegistersAndResolvesExecutableCapabilityInstance()
    {
        ICapabilityHub hub = new CapabilityRegistry(TrustDomain.Product);
        var capability = new SemanticCapability();

        var fact = hub.Register(capability, "product-composition-root");

        Assert.Equal(CapabilityLifecycle.Registered, fact.Lifecycle);
        Assert.Same(capability, hub.Resolve("semantic.test"));
        Assert.Same(capability.Description, hub.Get("semantic.test"));
    }

    [Fact]
    public void Hub_RejectsCapabilityWhoseProtocolDeclarationHasNoMatchingInterface()
    {
        ICapabilityHub hub = new CapabilityRegistry(TrustDomain.Product);

        Assert.Throws<ArgumentException>(() => hub.Register(
            new CapabilityWithoutSemanticInterface(), "product-composition-root"));
        Assert.Empty(hub.Facts);
    }
}
