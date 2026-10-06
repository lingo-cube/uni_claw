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
}
