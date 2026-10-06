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
        Assert.Equal(CapabilityCategory.ModelRouting, description.Category);
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
    public void Register_InstanceRegistration_MakesDeclarationAndRuntimeOneFact()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var instance = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.AgentDecision, "zai", "glm"),
        });

        ModelManagementCapabilityComposition.RegisterModelManagement(
            registry, instance, realization: "dsh-model-management");

        var resolved = registry.Resolve(ModelManagementCapabilityComposition.ModelManagementId);
        Assert.IsAssignableFrom<IModelManagement>(resolved);
        // 注册的是原实例的重述视图（共享活注册表）：声明与运行时同一事实——
        // 经原实例的注册/健康更新对 registry 解回的实例可见，无状态分叉。
        var viaRegistry = (IModelManagement)resolved!;
        Assert.True(viaRegistry.Resolve(LogicalProfileId.AgentDecision).IsResolved);
        instance.ApplyHealth(LogicalProfileId.AgentDecision, "zai", "glm", healthy: false);
        Assert.False(viaRegistry.IsAvailable(LogicalProfileId.AgentDecision));
        // 描述携带组合的 realization 角色。
        Assert.Contains(registry.Get(ModelManagementCapabilityComposition.ModelManagementId)!.Roles,
            r => r.Kind == CapabilityRoleKind.Realization && r.Name == "dsh-model-management");
    }

    [Fact]
    public void Lifecycle_DriveChain_IsLegalThroughCommitFacts()
    {
        // CAP-008 RL 样本：持资源能力的合法驱动链（打在测试 fake 上——
        // ModelManagement 按 RL1 停在 Registered，不走此链）。
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var fake = new FakeResourceCapability();
        registry.Register(fake, "test-composition");

        registry.Commit(fake.Description.CapabilityId, CapabilityLifecycle.Ready, "composition-root");
        registry.Commit(fake.Description.CapabilityId, CapabilityLifecycle.Active, "composition-root");
        registry.Commit(fake.Description.CapabilityId, CapabilityLifecycle.Draining, "teardown");
        registry.Commit(fake.Description.CapabilityId, CapabilityLifecycle.Closed, "teardown");

        var states = registry.Facts.Where(f => f.CapabilityId == fake.Description.CapabilityId)
            .Select(f => f.Lifecycle).ToArray();
        Assert.Equal(
            new[] { CapabilityLifecycle.Registered, CapabilityLifecycle.Ready,
                CapabilityLifecycle.Active, CapabilityLifecycle.Draining, CapabilityLifecycle.Closed },
            states);
        // 非法迁移仍被拒（Closed 无后继）。
        Assert.Throws<InvalidOperationException>(() =>
            registry.Commit(fake.Description.CapabilityId, CapabilityLifecycle.Active, "test"));
    }

    private sealed class FakeResourceCapability : ICapability
    {
        public CapabilityDescription Description { get; } = new(
            "test.resource-capability", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol("observation", "1") },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown);
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
