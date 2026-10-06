using UniClaw.Host.Capability;
using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// CAP-012 — Runtime Integration 域组合根：独立注册表 + 实例注册。
/// </summary>
public sealed class RuntimeIntegrationCapabilityCompositionTests
{
    [Fact]
    public void RegisterLanguageInspector_UsesRuntimeIntegrationDomainWithInstance()
    {
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();

        Assert.Equal(TrustDomain.RuntimeIntegration, registry.Domain);
        var resolved = registry.Resolve("runtime.language-inspector");
        Assert.IsAssignableFrom<ILanguageInspector>(resolved);
        var fact = Assert.Single(registry.Facts);
        Assert.Equal("runtime.language-inspector", fact.CapabilityId);
        Assert.Equal(TrustDomain.RuntimeIntegration, fact.Domain);
        Assert.Equal(RuntimeIntegrationCapabilityComposition.CompositionSource, fact.Source);
    }

    [Fact]
    public void RegisterLanguageInspector_AcceptsExplicitInstance_AndRejectsDuplicates()
    {
        var instance = new LanguageFormatInspector();
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector(instance: instance);

        Assert.Same(instance, registry.Resolve("runtime.language-inspector"));

        Assert.Throws<InvalidOperationException>(
            () => RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector(registry));
    }

    [Fact]
    public void RuntimeIntegrationRegistry_IsolatesFromProductDomain()
    {
        // ADR-0035：跨域注册 fail-closed（Product scope 声明不得进 RuntimeIntegration 域）。
        var runtimeRegistry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();

        Assert.Throws<ArgumentException>(() => runtimeRegistry.Register(
            new CapabilityDescription("product.thing", "1.0.0", CapabilityScope.ProductRuntime,
                Array.Empty<CapabilityProtocol>(), Array.Empty<CapabilityDependency>(), HealthStatus.Unknown),
            "test"));
    }
}
