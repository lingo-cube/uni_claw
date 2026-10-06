using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

/// <summary>
/// CAP-009 — UniPerception L2 可执行实现契约：双协议 marker、canonical 声明、
/// 健康聚合（worst-of + 诚实 Unknown）、注册经协议-接口一致性执法。
/// </summary>
public sealed class UniPerceptionCapabilityTests
{
    private static PerceptionHealthSource Source(string name, HealthStatus status, string? diagnostic = null) =>
        new(name, () => new CapabilityHealthReport(status, diagnostic));

    [Fact]
    public void ImplementsBothPerceptionProtocols_AsExecutableCapability()
    {
        ICapability capability = new UniPerceptionCapability();

        Assert.IsAssignableFrom<ISemanticPerception>(capability);
        Assert.IsAssignableFrom<IUiElementPerception>(capability);
        Assert.IsAssignableFrom<ICapabilityHealthCheckable>(capability);
    }

    [Fact]
    public void CanonicalDescription_MatchesFrozenCompositeDefinition()
    {
        var description = UniPerceptionCapability.CanonicalDescription;

        Assert.Equal("uni.perception", description.CapabilityId);
        Assert.Equal(CapabilityCategory.CompositeProductPerception, description.Category);
        Assert.Equal(CapabilityScope.ProductRuntime, description.Scope);
        Assert.Equal(2, description.Protocols.Length);
        Assert.Contains(description.Protocols, p => p.Name == PerceptionProtocol.Semantic);
        Assert.Contains(description.Protocols, p => p.Name == PerceptionProtocol.UiElement);
        // 依赖-关系镜像（registry 对组合类别的执法输入）。
        var dependencies = description.Dependencies.Select(d => d.CapabilityId).ToHashSet(StringComparer.Ordinal);
        var requires = description.Relationships
            .Where(r => r.Kind == CapabilityRelationshipKind.Requires)
            .Select(r => r.TargetCapabilityId).ToHashSet(StringComparer.Ordinal);
        Assert.True(dependencies.SetEquals(requires));
        Assert.Superset(new HashSet<string>(StringComparer.Ordinal) { "fast.yolo", "fast.ocr", "slow.text" }, dependencies);
    }

    [Fact]
    public void RegistryRegistration_EnforcesProtocolInterfaceConsistency()
    {
        // 生产路径首次行使：Composite 双协议声明 ↔ 实例双接口实现的执法。
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var instance = new UniPerceptionCapability();

        registry.Register(instance, "test");

        Assert.Same(instance, registry.Resolve("uni.perception"));
    }

    [Fact]
    public void CheckHealth_AggregatesWorstOfWithHonestUnknown()
    {
        Assert.Equal(HealthStatus.Unknown,
            new UniPerceptionCapability().CheckHealth().Status);
        Assert.Equal(HealthStatus.Healthy,
            new UniPerceptionCapability(new[] { Source("a", HealthStatus.Healthy), Source("b", HealthStatus.Healthy) })
                .CheckHealth().Status);
        // Healthy + Unknown 混合 → 部分可观测 = Degraded 确定性（不做假健康）。
        Assert.Equal(HealthStatus.Degraded,
            new UniPerceptionCapability(new[] { Source("a", HealthStatus.Healthy), Source("b", HealthStatus.Unknown) })
                .CheckHealth().Status);
        Assert.Equal(HealthStatus.Degraded,
            new UniPerceptionCapability(new[] { Source("a", HealthStatus.Healthy), Source("b", HealthStatus.Degraded) })
                .CheckHealth().Status);
        Assert.Equal(HealthStatus.Unhealthy,
            new UniPerceptionCapability(new[] { Source("a", HealthStatus.Healthy), Source("b", HealthStatus.Unhealthy) })
                .CheckHealth().Status);
    }

    [Fact]
    public void CheckHealth_ListsPerSourceDiagnostics_AndProbeThrowIsHonestDegraded()
    {
        var throwing = new PerceptionHealthSource("boom", () => throw new InvalidOperationException("nope"));
        var healthy = Source("ok", HealthStatus.Healthy);

        var report = new UniPerceptionCapability(new[] { healthy, throwing }).CheckHealth();

        Assert.Equal(HealthStatus.Degraded, report.Status);
        Assert.Contains("ok=Healthy", report.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("boom=Degraded", report.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("probe threw", report.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Construction_RejectsInvalidOrDuplicateSources()
    {
        Assert.Throws<ArgumentException>(() =>
            new UniPerceptionCapability(new[] { new PerceptionHealthSource(" ", () => new CapabilityHealthReport(HealthStatus.Healthy)) }));
        Assert.Throws<ArgumentException>(() =>
            new UniPerceptionCapability(new[] { Source("dup", HealthStatus.Healthy), Source("dup", HealthStatus.Healthy) }));
    }

    [Fact]
    public void WithDescription_SharesHealthSources_ViewSemantics()
    {
        var original = new UniPerceptionCapability(new[] { Source("a", HealthStatus.Healthy) });
        var composed = UniPerceptionCapability.CanonicalDescription with { };

        var redescribed = original.WithDescription(composed);

        Assert.Equal(HealthStatus.Healthy, redescribed.CheckHealth().Status);
        Assert.Equal(composed, redescribed.Description);
    }

    [Fact]
    public void ModelSideHealthSource_ComposesWithModelManagement()
    {
        // 模型端探针的真实形态：直接包 IModelManagement.CheckHealth（CAP-008 缝）。
        var models = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.Text, "zai", "glm"),
        });
        var modelProbe = new PerceptionHealthSource("slow.text-models", models.CheckHealth);

        var report = new UniPerceptionCapability(new[] { modelProbe }).CheckHealth();

        Assert.Equal(HealthStatus.Healthy, report.Status);
        // 摘除唯一候选后，模型端转 Unhealthy，感知健康随之联动（诚实传播）。
        models.ApplyHealth(LogicalProfileId.Text, "zai", "glm", healthy: false);
        var degraded = new UniPerceptionCapability(new[] { modelProbe }).CheckHealth();
        Assert.Equal(HealthStatus.Unhealthy, degraded.Status);
    }
}
