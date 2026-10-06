using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// CAP-006 — Model Management 缺省（借用 DSH）realization 的契约测试：
/// yaml modelSelection → 产品缝 binding 注册、覆盖透传、DSH 形状转换、
/// visual 诚实 NotConfigured、不可解析 fail-closed。
/// </summary>
public sealed class DshModelManagementTests
{
    private static UniagentProdConfiguration Config(string provider = "zai-coding-cn", string name = "glm-5.3-flash") =>
        UniagentProdConfiguration.Create(
            new ModelConfiguration(provider, name),
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080")),
            selectedModelKey: "glm53Flash");

    [Fact]
    public void FromProfile_RegistersDecisionAndSlowTextFromSelectedChoice()
    {
        var models = DshModelManagement.FromProfile(Config());

        var decision = models.Resolve(LogicalProfileId.AgentDecision);
        var slowText = models.Resolve(LogicalProfileId.Text);

        Assert.True(decision.IsResolved);
        Assert.True(slowText.IsResolved);
        Assert.Equal(("zai-coding-cn", "glm-5.3-flash"),
            (decision.Binding!.ProviderId, decision.Binding.ModelId));
        Assert.Equal(("zai-coding-cn", "glm-5.3-flash"),
            (slowText.Binding!.ProviderId, slowText.Binding.ModelId));
        Assert.Equal(DshModelManagement.RealizationName, decision.Binding.VariantId);
        Assert.False(decision.Binding.Experimental);
        Assert.Equal("uniagent-prod:glm53Flash", decision.Binding.ConfigId);
    }

    [Fact]
    public void FromProfile_ModelNameOverride_IsHonouredOnBothProfiles()
    {
        var models = DshModelManagement.FromProfile(Config(name: "configured-name"),
            modelNameOverride: "override-name");

        Assert.Equal("override-name", models.Resolve(LogicalProfileId.AgentDecision).Binding!.ModelId);
        Assert.Equal("override-name", models.Resolve(LogicalProfileId.Text).Binding!.ModelId);
    }

    [Fact]
    public void FromProfile_VisualStaysUnregistered_HonestNotConfigured()
    {
        var models = DshModelManagement.FromProfile(Config());

        var visual = models.Resolve(LogicalProfileId.Visual);

        Assert.False(visual.IsResolved);
        Assert.Equal(ModelRoutingStatus.RoutingUnavailable, visual.Status);
        Assert.False(models.IsAvailable(LogicalProfileId.Visual));
    }

    [Fact]
    public void ToDshModel_ConvertsSnapshotWithoutLeakingKernelShapes()
    {
        var snapshot = new ModelBindingSnapshot(
            LogicalProfileId.AgentDecision, "zai-coding-cn", "glm-5.3-flash");

        var dshModel = DshModelManagement.ToDshModel(snapshot);

        Assert.Equal(new ModelConfiguration("zai-coding-cn", "glm-5.3-flash"), dshModel);
    }

    [Fact]
    public void ResolveDshModel_UnresolvableProfile_FailsClosed()
    {
        var models = new ModelManagement();

        var error = Assert.Throws<InvalidOperationException>(
            () => DshModelManagement.ResolveDshModel(models, LogicalProfileId.AgentDecision));

        Assert.Contains("ROUTING_UNAVAILABLE", error.Message, StringComparison.Ordinal);
        Assert.Contains("agent.decision", error.Message, StringComparison.Ordinal);
    }
}
