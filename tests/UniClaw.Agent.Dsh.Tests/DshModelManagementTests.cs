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

    // ---- CAP-007：per-profile 选择 / 候选偏好 / fail-closed ----

    private static readonly Dictionary<string, ModelConfiguration> TestChoices = new(StringComparer.Ordinal)
    {
        ["glm53Flash"] = new("zai-coding-cn", "glm-5.3-flash"),
        ["deepseekFlash"] = new("opencode-go", "deepseek-flash"),
        ["visionModel"] = new("opencode-go", "vision-latest"),
    };

    [Fact]
    public void FromProfile_PerProfileSelection_OverridesDefaultWithPreferenceOrder()
    {
        var config = UniagentProdConfiguration.Create(
            TestChoices["glm53Flash"],
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080")),
            selectedModelKey: "glm53Flash",
            choices: TestChoices,
            profileSelections: new[]
            {
                new ModelProfileSelection(LogicalProfileId.AgentDecision.Value,
                    new[] { "deepseekFlash", "glm53Flash" }),
                new ModelProfileSelection(LogicalProfileId.Text.Value, new[] { "glm53Flash" }),
            });

        var models = DshModelManagement.FromProfile(config);

        var decision = models.Resolve(LogicalProfileId.AgentDecision);
        Assert.True(decision.IsResolved);
        // 偏好序：首选 deepseek 生效，provider/name 来自该 choice 而非 selected。
        Assert.Equal(("opencode-go", "deepseek-flash"),
            (decision.Binding!.ProviderId, decision.Binding.ModelId));
        Assert.Equal("uniagent-prod:deepseekFlash", decision.Binding.ConfigId);
        // 候选齐全（次选 glm 可在健康摘除后顶上）。
        Assert.True(DshModelManagement.RealizationName is { Length: > 0 });
        Assert.True(models.ApplyHealth(LogicalProfileId.AgentDecision, "opencode-go", "deepseek-flash", healthy: false));
        var degraded = models.Resolve(LogicalProfileId.AgentDecision);
        Assert.Equal("glm-5.3-flash", degraded.Binding!.ModelId);
        Assert.Equal("deepseek-flash", degraded.Binding.FallbackFrom);

        var slowText = models.Resolve(LogicalProfileId.Text);
        Assert.Equal(("zai-coding-cn", "glm-5.3-flash"),
            (slowText.Binding!.ProviderId, slowText.Binding.ModelId));
    }

    [Fact]
    public void FromProfile_ModelOverride_AppliesOnlyToDefaultBindings()
    {
        var config = UniagentProdConfiguration.Create(
            TestChoices["glm53Flash"],
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080")),
            selectedModelKey: "glm53Flash",
            choices: TestChoices,
            profileSelections: new[]
            {
                // 未显式配置的 slow.semantic.text 走缺省（被 override 覆盖）；
                // 显式的 agent.decision 不被 override 覆盖（D3 冻结）。
                new ModelProfileSelection(LogicalProfileId.AgentDecision.Value, new[] { "glm53Flash" }),
            });

        var models = DshModelManagement.FromProfile(config, modelNameOverride: "override-name");

        Assert.Equal("glm-5.3-flash", models.Resolve(LogicalProfileId.AgentDecision).Binding!.ModelId);
        Assert.Equal("override-name", models.Resolve(LogicalProfileId.Text).Binding!.ModelId);
    }

    [Fact]
    public void FromProfile_VisualExplicitChoice_RegistersVisualBinding()
    {
        var config = UniagentProdConfiguration.Create(
            TestChoices["glm53Flash"],
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080")),
            selectedModelKey: "glm53Flash",
            choices: TestChoices,
            profileSelections: new[]
            {
                new ModelProfileSelection(LogicalProfileId.Visual.Value, new[] { "visionModel" }),
            });

        var models = DshModelManagement.FromProfile(config);

        var visual = models.Resolve(LogicalProfileId.Visual);
        Assert.True(visual.IsResolved);
        Assert.Equal(("opencode-go", "vision-latest"),
            (visual.Binding!.ProviderId, visual.Binding.ModelId));
    }

    [Fact]
    public void FromProfile_UnknownOrDuplicateProfile_FailsClosed()
    {
        var endpoint = new DshServiceEndpoint(new Uri("http://127.0.0.1:3080"));
        var bogus = UniagentProdConfiguration.Create(
            TestChoices["glm53Flash"], endpoint, "glm53Flash", TestChoices,
            new[] { new ModelProfileSelection("bogus.profile", new[] { "glm53Flash" }) });
        var unknownError = Assert.Throws<InvalidOperationException>(
            () => DshModelManagement.FromProfile(bogus));
        Assert.Contains("unknown profile 'bogus.profile'", unknownError.Message, StringComparison.Ordinal);
        Assert.Contains("slow.semantic.visual", unknownError.Message, StringComparison.Ordinal);

        var duplicated = UniagentProdConfiguration.Create(
            TestChoices["glm53Flash"], endpoint, "glm53Flash", TestChoices,
            new[]
            {
                new ModelProfileSelection(LogicalProfileId.Text.Value, new[] { "glm53Flash" }),
                new ModelProfileSelection(LogicalProfileId.Text.Value, new[] { "deepseekFlash" }),
            });
        Assert.Throws<InvalidOperationException>(() => DshModelManagement.FromProfile(duplicated));
    }

    [Fact]
    public void FromProfile_ExplicitChoiceWithoutChoicesMap_FailsClosed()
    {
        var config = UniagentProdConfiguration.Create(
            TestChoices["glm53Flash"],
            new DshServiceEndpoint(new Uri("http://127.0.0.1:3080")),
            selectedModelKey: "glm53Flash",
            choices: null,
            profileSelections: new[]
            {
                new ModelProfileSelection(LogicalProfileId.Text.Value, new[] { "deepseekFlash" }),
            });

        Assert.Throws<InvalidOperationException>(() => DshModelManagement.FromProfile(config));
    }

    private static string WriteFixture(string modelSelectionProfiles)
    {
        // profiles 块必须缩进 2 格嵌在 modelSelection: 之下（与 selected/choices 同级）。
        var indented = string.Join("\n",
            modelSelectionProfiles.Split('\n').Select(line => "  " + line.TrimEnd()));
        var path = Path.Combine(Path.GetTempPath(), "uniclaw-model-mgmt-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, $"""
            profileId: uniagent-prod
            profileVersion: "1"
            capabilities:
              - submit_decision
            service:
              baseUrl: http://127.0.0.1:3080
            modelSelection:
              selected: glm53Flash
              choices:
                free:
                  provider: opencode-go
                  name: space-bunny-free
                deepseekFlash:
                  provider: opencode-go
                  name: deepseek-flash
                glm53Flash:
                  provider: zai-coding-cn
                  name: glm-5.3-flash
            {indented}
            """);
        return path;
    }

    [Fact]
    public void YamlProfiles_ScalarAndOrderedList_AreParsedIntoPreference()
    {
        var path = WriteFixture("""
            profiles:
              agent.decision:
                - deepseekFlash
                - free
              slow.semantic.visual: free
            """);
        try
        {
            var config = UniagentProdYaml.Load(path);

            Assert.Equal(3, config.Choices!.Count);
            Assert.Equal(new ModelConfiguration("opencode-go", "deepseek-flash"),
                config.Choices["deepseekFlash"]);
            Assert.Equal(2, config.ProfileSelections!.Count);

            var models = DshModelManagement.FromProfile(config);
            var decision = models.Resolve(LogicalProfileId.AgentDecision);
            Assert.Equal("deepseek-flash", decision.Binding!.ModelId);
            Assert.True(models.ApplyHealth(
                LogicalProfileId.AgentDecision, "opencode-go", "deepseek-flash", healthy: false));
            Assert.Equal("space-bunny-free",
                models.Resolve(LogicalProfileId.AgentDecision).Binding!.ModelId);
            Assert.Equal("space-bunny-free", models.Resolve(LogicalProfileId.Visual).Binding!.ModelId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void YamlProfiles_UnknownChoice_FailsClosedAtLoad()
    {
        var path = WriteFixture("""
            profiles:
              agent.decision: noSuchChoice
            """);
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() => UniagentProdYaml.Load(path));
            Assert.Contains("unknown choice 'noSuchChoice'", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void YamlProfiles_MalformedEntry_FailsClosedAtLoad()
    {
        var path = WriteFixture("""
            profiles:
              agent.decision:
                provider: oops
            """);
        try
        {
            Assert.Throws<InvalidOperationException>(() => UniagentProdYaml.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- CAP-008 R5：可替换性执法——同一 IModelManagement 消费闭包，
    // DSH 注入与裸 Kernel 注入两个实例行为契约可互换 ----

    [Fact]
    public void Realizations_AreInterchangeable_ThroughInterface()
    {
        static string ResolvePrimary(IModelManagement models) =>
            models.Resolve(LogicalProfileId.AgentDecision) is { IsResolved: true, Binding: { } binding }
                ? $"{binding.ProviderId}/{binding.ModelId}"
                : "unavailable";

        var dsh = DshModelManagement.FromProfile(Config());
        var replay = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.AgentDecision, "replay", "slow-replay"),
        });

        // 各自的 realization 数据生效，消费闭包不认识具体类。
        Assert.Equal("zai-coding-cn/glm-5.3-flash", ResolvePrimary(dsh));
        Assert.Equal("replay/slow-replay", ResolvePrimary(replay));

        // 同一失败语义：健康摘除唯一候选后，两者都诚实转 unavailable（零静默降级）。
        Assert.True(dsh.ApplyHealth(LogicalProfileId.AgentDecision, "zai-coding-cn", "glm-5.3-flash", healthy: false));
        Assert.True(replay.ApplyHealth(LogicalProfileId.AgentDecision, "replay", "slow-replay", healthy: false));
        Assert.Equal("unavailable", ResolvePrimary(dsh));
        Assert.Equal("unavailable", ResolvePrimary(replay));
        // 健康聚合同样诚实：dsh 仍有可用的 slow text profile → Degraded；
        // replay 只注册过 agent.decision → Unhealthy（未配置≠不健康的边界各自如实）。
        Assert.Equal(HealthStatus.Degraded, dsh.CheckHealth().Status);
        Assert.Equal(HealthStatus.Unhealthy, replay.CheckHealth().Status);
    }
}
