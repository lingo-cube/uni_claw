using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

/// <summary>
/// CAP-006 — 公开 Model Management 能力组件的契约测试：值域冻结的
/// LogicalProfileId（含 agent.decision）、fail-closed 解析、注册执法。
/// </summary>
public sealed class ModelManagementTests
{
    [Fact]
    public void AgentDecisionProfile_ResolvesRegisteredBinding()
    {
        var models = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.AgentDecision, "opencode-go", "glm-5.3-flash",
                VariantId: "dsh-model-management", Experimental: false),
        });

        var resolution = models.Resolve(LogicalProfileId.AgentDecision);

        Assert.True(resolution.IsResolved);
        Assert.Equal(ModelRoutingStatus.Resolved, resolution.Status);
        Assert.Equal(("opencode-go", "glm-5.3-flash"),
            (resolution.Binding!.ProviderId, resolution.Binding.ModelId));
        Assert.True(models.IsAvailable(LogicalProfileId.AgentDecision));
    }

    [Fact]
    public void LogicalProfileId_FrozenValues()
    {
        Assert.Equal("slow.semantic.text", LogicalProfileId.Text.Value);
        Assert.Equal("slow.semantic.visual", LogicalProfileId.Visual.Value);
        Assert.Equal("agent.decision", LogicalProfileId.AgentDecision.Value);
        Assert.True(new LogicalProfileId("x").IsValid);
        Assert.False(default(LogicalProfileId).IsValid);
    }

    [Fact]
    public void UnknownProfile_FailsClosedWithRoutingUnavailable()
    {
        var models = new ModelManagement();

        var resolution = models.Resolve(new LogicalProfileId("no.such.profile"));

        Assert.False(resolution.IsResolved);
        Assert.Equal(ModelRoutingStatus.RoutingUnavailable, resolution.Status);
        Assert.Null(resolution.Binding);
        Assert.Contains("ROUTING_UNAVAILABLE", resolution.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("no.such.profile", resolution.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_InvalidSnapshot_FailsClosed()
    {
        var models = new ModelManagement();
        var invalid = new ModelBindingSnapshot(LogicalProfileId.Text, " ", "m");

        Assert.Throws<ArgumentException>(() => models.Register(invalid));
        Assert.Empty(models.Bindings);
    }

    [Fact]
    public void Register_SameProfile_ReplacesSnapshot()
    {
        var models = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.Text, "replay", "old"),
        });

        models.Register(new ModelBindingSnapshot(LogicalProfileId.Text, "replay", "new"));
        var resolution = models.Resolve(LogicalProfileId.Text);

        Assert.True(resolution.IsResolved);
        Assert.Equal("new", resolution.Binding!.ModelId);
        Assert.Single(models.Bindings);
    }

    // ---- CAP-007：候选偏好 + fallback 链 ----

    [Fact]
    public void Preference_FirstUsableCandidateWins()
    {
        var profile = LogicalProfileId.AgentDecision;
        var models = new ModelManagement();
        models.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "zai", "glm"),
            new ModelBindingSnapshot(profile, "opencode", "deepseek"),
        });

        var resolution = models.Resolve(profile);

        Assert.True(resolution.IsResolved);
        Assert.Equal("glm", resolution.Binding!.ModelId);
        Assert.Null(resolution.Binding.FallbackFrom);
    }

    [Fact]
    public void Preference_SkipsUnusableCandidate_AndRecordsFallbackFrom()
    {
        var profile = LogicalProfileId.AgentDecision;
        var models = new ModelManagement();
        models.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "zai", "glm", Available: false),
            new ModelBindingSnapshot(profile, "zai", "glm2", Health: false),
            new ModelBindingSnapshot(profile, "opencode", "deepseek"),
        });

        var resolution = models.Resolve(profile);

        Assert.True(resolution.IsResolved);
        Assert.Equal("deepseek", resolution.Binding!.ModelId);
        Assert.Equal("glm", resolution.Binding.FallbackFrom);
    }

    [Fact]
    public void Preference_AllUnusable_FailsClosedListingTriedCandidates()
    {
        var profile = LogicalProfileId.Text;
        var models = new ModelManagement();
        models.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "a", "m1", Health: false),
            new ModelBindingSnapshot(profile, "b", "m2", Available: false),
        });

        var resolution = models.Resolve(profile);

        Assert.False(resolution.IsResolved);
        Assert.Equal(ModelRoutingStatus.RoutingUnavailable, resolution.Status);
        Assert.Contains("ROUTING_UNAVAILABLE", resolution.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("m1,m2", resolution.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Preference_InvalidRegistrations_FailClosed()
    {
        var models = new ModelManagement();

        Assert.Throws<ArgumentException>(() =>
            models.RegisterPreference(LogicalProfileId.Text, Array.Empty<ModelBindingSnapshot>()));
        Assert.Throws<ArgumentException>(() =>
            models.RegisterPreference(LogicalProfileId.Text, new[]
            {
                new ModelBindingSnapshot(LogicalProfileId.Text, "a", "m1"),
                new ModelBindingSnapshot(LogicalProfileId.Visual, "a", "m2"),
            }));
        Assert.Throws<ArgumentException>(() =>
            models.RegisterPreference(LogicalProfileId.Text, new[]
            {
                new ModelBindingSnapshot(LogicalProfileId.Text, "a", "m1"),
                new ModelBindingSnapshot(LogicalProfileId.Text, "a", "m1"),
            }));
    }

    [Fact]
    public void Register_AfterPreference_ReplacesWithSoleCandidate()
    {
        var profile = LogicalProfileId.Text;
        var models = new ModelManagement();
        models.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "a", "m1"),
            new ModelBindingSnapshot(profile, "b", "m2"),
        });

        models.Register(new ModelBindingSnapshot(profile, "c", "m3"));

        // Register 语义 = 设为唯一候选（替换整个偏好列表）。
        Assert.Single(models.Bindings);
        Assert.Equal("m3", models.Resolve(profile).Binding!.ModelId);
    }

    // ---- CAP-007：健康证据协议 ----

    [Fact]
    public void ApplyHealth_RemovesCandidateFromResolution_AndRestores()
    {
        var profile = LogicalProfileId.AgentDecision;
        var models = new ModelManagement();
        models.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "zai", "glm"),
            new ModelBindingSnapshot(profile, "opencode", "deepseek"),
        });

        var applied = models.ApplyHealth(profile, "zai", "glm", healthy: false);
        var degraded = models.Resolve(profile);

        Assert.True(applied);
        Assert.True(degraded.IsResolved);
        Assert.Equal("deepseek", degraded.Binding!.ModelId);
        Assert.Equal("glm", degraded.Binding.FallbackFrom);

        var restored = models.ApplyHealth(profile, "zai", "glm", healthy: true);
        var resolved = models.Resolve(profile);

        Assert.True(restored);
        Assert.Equal("glm", resolved.Binding!.ModelId);
        Assert.Null(resolved.Binding.FallbackFrom);
    }

    [Fact]
    public void ApplyHealth_UnknownIdentity_IsHonestNoOp()
    {
        var profile = LogicalProfileId.Text;
        var models = new ModelManagement();
        models.Register(new ModelBindingSnapshot(profile, "a", "m1"));

        Assert.False(models.ApplyHealth(profile, "a", "no-such-model", healthy: false));
        Assert.False(models.ApplyHealth(new LogicalProfileId("unknown.profile"), "a", "m1", healthy: false));
        Assert.False(models.ApplyHealth(profile, " ", "m1", healthy: false));
        Assert.True(models.Resolve(profile).IsResolved);
    }

    [Fact]
    public void ApplyHealth_AllCandidatesUnhealthy_FailsClosed()
    {
        var profile = LogicalProfileId.Text;
        var models = new ModelManagement();
        models.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "a", "m1"),
            new ModelBindingSnapshot(profile, "b", "m2"),
        });

        Assert.True(models.ApplyHealth(profile, "a", "m1", healthy: false));
        Assert.True(models.ApplyHealth(profile, "b", "m2", healthy: false));

        Assert.False(models.IsAvailable(profile));
        Assert.Equal(ModelRoutingStatus.RoutingUnavailable, models.Resolve(profile).Status);
    }

    // ---- CAP-008：可执行契约（ICapability）+ 健康能力面 ----

    [Fact]
    public void ModelManagement_IsExecutableCapability_WithCanonicalDefinition()
    {
        ICapability capability = new ModelManagement();

        Assert.IsType<ModelManagement>(capability);
        var description = capability.Description;
        Assert.Equal(ModelManagementProtocol.CapabilityId, description.CapabilityId);
        Assert.Equal(CapabilityCategory.ModelRouting, description.Category);
        Assert.Equal(CapabilityScope.ProductRuntime, description.Scope);
        var protocol = Assert.Single(description.Protocols);
        Assert.Equal((ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version),
            (protocol.Name, protocol.Version));
        Assert.Empty(description.Roles);
    }

    [Fact]
    public void WithDescription_PreservesCandidatePreferenceOrder_G6Regression()
    {
        var profile = LogicalProfileId.AgentDecision;
        var original = new ModelManagement();
        original.RegisterPreference(profile, new[]
        {
            new ModelBindingSnapshot(profile, "zai", "glm"),
            new ModelBindingSnapshot(profile, "opencode", "deepseek"),
        });
        var composed = ModelManagementCapabilityCompositionDescription();

        var redescribed = original.WithDescription(composed);

        // 偏好序完整保留：首选解析生效，摘除后次选顶上并带降级轨迹。
        Assert.Equal("glm", redescribed.Resolve(profile).Binding!.ModelId);
        Assert.True(redescribed.ApplyHealth(profile, "zai", "glm", healthy: false));
        var degraded = redescribed.Resolve(profile);
        Assert.Equal("deepseek", degraded.Binding!.ModelId);
        Assert.Equal("glm", degraded.Binding.FallbackFrom);
        // 同一活注册表：经重述视图的健康摘除对原实例可见（无状态分叉）。
        Assert.Equal("deepseek", original.Resolve(profile).Binding!.ModelId);
        // 各自的声明独立（原实例仍 canonical，新视图持组合声明）。
        Assert.Equal(composed, redescribed.Description);
        Assert.Equal(ModelManagement.CanonicalDescription, original.Description);
    }

    private static CapabilityDescription ModelManagementCapabilityCompositionDescription() => new(
        ModelManagementProtocol.CapabilityId, "1.0.0", CapabilityScope.ProductRuntime,
        new[] { new CapabilityProtocol(ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version) },
        Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
        CapabilityCategory.ModelRouting,
        new[] { new CapabilityRole(CapabilityRoleKind.Realization, "test-realization") },
        Array.Empty<CapabilityRelationship>());

    [Fact]
    public void CheckHealth_AggregatesAcrossRegisteredProfiles()
    {
        var empty = new ModelManagement();
        Assert.Equal(HealthStatus.Unknown, empty.CheckHealth().Status);

        var healthy = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.Text, "a", "m1"),
            new ModelBindingSnapshot(LogicalProfileId.AgentDecision, "a", "m2"),
        });
        Assert.Equal(HealthStatus.Healthy, healthy.CheckHealth().Status);

        var degraded = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.Text, "a", "m1"),
            new ModelBindingSnapshot(LogicalProfileId.AgentDecision, "a", "m2", Health: false),
        });
        var degradedReport = degraded.CheckHealth();
        Assert.Equal(HealthStatus.Degraded, degradedReport.Status);
        Assert.Contains("agent.decision", degradedReport.Diagnostic, StringComparison.Ordinal);

        // ApplyHealth 联动：摘除唯一可用候选 → 全部 profile 不可用 → Unhealthy。
        Assert.True(degraded.ApplyHealth(LogicalProfileId.Text, "a", "m1", healthy: false));
        Assert.Equal(HealthStatus.Unhealthy, degraded.CheckHealth().Status);
    }

    // ---- CAP-008：Registry 对 ModelRouting 的协议/实例执法 ----

    [Fact]
    public void Registry_AcceptsModelRoutingExecutableInstance()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var instance = new ModelManagement();

        registry.Register(instance.WithDescription(ModelManagementCapabilityCompositionDescription()), "test");

        var resolved = registry.Resolve(ModelManagementProtocol.CapabilityId);
        Assert.IsAssignableFrom<IModelManagement>(resolved);
        // 重述视图与原实例共享活注册表：经 Resolve 取回的实例与原实例无状态分叉。
        var viaRegistry = (IModelManagement)resolved!;
        viaRegistry.Register(new ModelBindingSnapshot(LogicalProfileId.Text, "a", "m1"));
        Assert.True(instance.IsAvailable(LogicalProfileId.Text));
    }

    [Fact]
    public void Registry_RejectsModelRouting_DescriptionOnly()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);

        Assert.Throws<ArgumentException>(() =>
            registry.Register(ModelManagementCapabilityCompositionDescription(), "test"));
    }

    [Fact]
    public void Registry_RejectsModelRouting_WrongProtocolOrScope()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var wrongProtocol = new CapabilityDescription(
            "x.model", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol("Something Else", "1.0") },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.ModelRouting, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>());
        Assert.Throws<ArgumentException>(() =>
            registry.Register(new ModelManagement(description: wrongProtocol), "test"));

        var wrongScope = new CapabilityDescription(
            "x.model", "1.0.0", CapabilityScope.RuntimeIntegration,
            new[] { new CapabilityProtocol(ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.ModelRouting, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>());
        Assert.Throws<ArgumentException>(() =>
            registry.Register(new ModelManagement(description: wrongScope), "test"));
    }

    private sealed class NonModelManagementCapability : ICapability
    {
        public CapabilityDescription Description { get; } = new(
            "x.model", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version) },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.ModelRouting, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>());
    }

    [Fact]
    public void Registry_RejectsInstanceShapeMismatch_BothDirections()
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        var modelRoutingDescription = ModelManagementCapabilityCompositionDescription();

        // ModelRouting 声明 + 非 IModelManagement 实例 → 拒。
        Assert.Throws<ArgumentException>(() =>
            registry.Register(new NonModelManagementCapability(), "test"));

        // IModelManagement 实例 + 非 ModelRouting 类别声明 → 拒。
        var genericDescription = new CapabilityDescription(
            "x.model", "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol("Whatever", "1.0") },
            Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
            CapabilityCategory.Generic, Array.Empty<CapabilityRole>(), Array.Empty<CapabilityRelationship>());
        Assert.Throws<ArgumentException>(() =>
            registry.Register(new ModelManagement(description: genericDescription), "test"));
    }
}
