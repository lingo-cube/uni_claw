using UniClaw.Host.Capability;
using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// CAP-006 — uni.model.management 管理面声明：descriptor、缺省 DSH
/// realization 角色、lifecycle fact、重复注册 fail-closed。
/// </summary>
public sealed class ModelManagementCapabilityCompositionTests
{
    [Fact]
    public void Register_DeclaresModelManagement_WithDshDefaultRealization()
    {
        var registry = ModelManagementCapabilityComposition.RegisterModelManagement();

        var description = registry.Get(ModelManagementCapabilityComposition.ModelManagementId);

        Assert.NotNull(description);
        Assert.Equal(CapabilityScope.ProductRuntime, description!.Scope);
        Assert.Equal(CapabilityCategory.Generic, description.Category);
        Assert.Contains(description.Protocols,
            p => p.Name == ModelManagementCapabilityComposition.ModelBindingResolutionProtocol
                && p.Version == ModelManagementCapabilityComposition.ProtocolVersion);
        Assert.Contains(description.Roles,
            r => r.Kind == CapabilityRoleKind.Realization
                && r.Name == ModelManagementCapabilityComposition.DefaultRealization);
        // 角色面只有协议角色 + realization 角色——描述不携带 provider/model 名
        //（具体绑定只存在于运行时快照）。
        Assert.Equal(2, description.Roles.Length);
        Assert.Contains(description.Roles,
            r => r.Kind == CapabilityRoleKind.ProductProtocol
                && r.Name == ModelManagementCapabilityComposition.ModelBindingResolutionProtocol);
    }

    [Fact]
    public void Register_PublishesLifecycleFact()
    {
        var registry = ModelManagementCapabilityComposition.RegisterModelManagement();

        var fact = Assert.Single(registry.Facts);
        Assert.Equal(ModelManagementCapabilityComposition.ModelManagementId, fact.CapabilityId);
        Assert.Equal(TrustDomain.Product, fact.Domain);
        Assert.Equal(CapabilityLifecycle.Registered, fact.Lifecycle);
        Assert.Equal(ModelManagementCapabilityComposition.CompositionSource, fact.Source);
    }

    [Fact]
    public void Register_ComposesIntoExistingProductRegistry()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        registry.Register(new CapabilityDescription("some.other", "1.0.0",
            CapabilityScope.ProductRuntime, Array.Empty<CapabilityProtocol>(),
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown),
            "product-composition-root");

        ModelManagementCapabilityComposition.RegisterModelManagement(registry);

        Assert.NotNull(registry.Get(ModelManagementCapabilityComposition.ModelManagementId));
        Assert.Equal(2, registry.Facts.Count);
    }

    [Fact]
    public void Register_DuplicateId_FailsClosed()
    {
        var registry = ModelManagementCapabilityComposition.RegisterModelManagement();

        Assert.Throws<InvalidOperationException>(
            () => ModelManagementCapabilityComposition.RegisterModelManagement(registry));
    }

    [Fact]
    public void Register_ExplicitRealization_OverridesDefault()
    {
        var registry = ModelManagementCapabilityComposition.RegisterModelManagement(
            realization: "opencode-model-management");

        var description = registry.Get(ModelManagementCapabilityComposition.ModelManagementId);
        Assert.Contains(description!.Roles,
            r => r.Kind == CapabilityRoleKind.Realization && r.Name == "opencode-model-management");
    }
}
