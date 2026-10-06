using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Perception;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

/// <summary>
/// CAP-010 — R5 可替换性执法（capability-component skill 协议质量门）：
/// 同一 L1 消费闭包（只写 <see cref="IModelManagement"/>）喂两个 realization
/// 注入的实例，行为契约互换成立——realization 替换只动注入数据与组合根声明，
/// L2/消费方零改动。实例经 <see cref="CapabilityRegistry"/> 注册-取回运行
/// （R1 可执行实例注册同时覆盖）。感知侧 R5 被 L1 未冻结 known gap 阻挡，
/// 归在途 CAP-009 落地后补同形状测试。
/// </summary>
public sealed class ModelRoutingReplaceabilityTests
{
    /// <summary>
    /// 消费闭包：只依赖 L1。对任意 realization 断言同一行为契约——
    /// 初始解析成功；健康翻转当前候选后要么回退（记录 FallbackFrom 轨迹）
    /// 要么 fail-closed，二者互斥且诚实；未知 profile fail-closed 带诊断。
    /// </summary>
    private static (
        bool InitiallyResolved,
        string InitialModel,
        bool AfterFlipResolved,
        string? FallbackFrom,
        bool UnknownProfileFailClosed) ExerciseContract(IModelManagement models)
    {
        var initial = models.Resolve(LogicalProfileId.Text);
        var initiallyResolved = initial.IsResolved;
        var initialModel = initial.Binding?.ModelId ?? "";

        models.ApplyHealth(LogicalProfileId.Text, initial.Binding!.ProviderId, initialModel, healthy: false);
        var afterFlip = models.Resolve(LogicalProfileId.Text);

        var unknown = models.Resolve(new LogicalProfileId("no.such.profile"));

        return (
            initiallyResolved,
            initialModel,
            afterFlip.IsResolved,
            afterFlip.Binding?.FallbackFrom,
            unknown.Status == ModelRoutingStatus.RoutingUnavailable
                && !string.IsNullOrEmpty(unknown.Diagnostic));
    }

    /// <summary>组合根声明：canonical Definition + realization 角色（镜像
    /// ModelManagementCapabilityComposition.Description 的组装方式）。</summary>
    private static CapabilityDescription CompositionDescription(string realizationName) => new(
        ModelManagementProtocol.CapabilityId, "1.0.0", CapabilityScope.ProductRuntime,
        new[] { new CapabilityProtocol(ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version) },
        Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
        CapabilityCategory.ModelRouting,
        new[]
        {
            new CapabilityRole(CapabilityRoleKind.ProductProtocol, ModelManagementProtocol.BindingResolution),
            new CapabilityRole(CapabilityRoleKind.Realization, realizationName),
        },
        Array.Empty<CapabilityRelationship>());

    /// <summary>经 registry 注册可执行实例并取回（R1：Resolve 取回同一事实）。</summary>
    private static IModelManagement RegistryResolvedInstance(ModelManagement instance, string realization)
    {
        var registry = new CapabilityRegistry(TrustDomain.Product);
        registry.Register(instance.WithDescription(CompositionDescription(realization)), "test-composition");
        return (IModelManagement)registry.Resolve(ModelManagementProtocol.CapabilityId)!;
    }

    /// <summary>OpenCode 形状 realization：双候选偏好集（镜像 OpenCode/DSH
    /// adapter 经 RegisterPreference 注入的方式）。</summary>
    private static ModelManagement OpenCodeShapedRealization()
    {
        var models = new ModelManagement();
        models.RegisterPreference(LogicalProfileId.Text, new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.Text, "opencode-go", "glm-5.3-flash", VariantId: "dsh-model-management"),
            new ModelBindingSnapshot(LogicalProfileId.Text, "opencode-go", "glm-5.3", VariantId: "dsh-model-management"),
        });
        return models;
    }

    [Fact]
    public void SameConsumerClosure_ReplayRealization_HoldsContract()
    {
        // realization A：replay 缺省（SlowReplayProfiles 注入；internal 经
        // InternalsVisibleTo 访问，test seam 而非新依赖）。
        var facts = ExerciseContract(RegistryResolvedInstance(
            SlowReplayProfiles.CreateDefault(), "slow-replay-realization"));

        Assert.True(facts.InitiallyResolved);
        Assert.Equal("slow-text-replay", facts.InitialModel);
        // replay 只有单一 Text 候选：健康翻转后 fail-closed，无静默降级。
        Assert.False(facts.AfterFlipResolved);
        Assert.Null(facts.FallbackFrom);
        Assert.True(facts.UnknownProfileFailClosed);
    }

    [Fact]
    public void SameConsumerClosure_OpenCodeShapedRealization_HoldsContract()
    {
        // realization B：双候选偏好集——首选翻转后回退到次选，FallbackFrom
        // 诚实记录轨迹。
        var facts = ExerciseContract(RegistryResolvedInstance(
            OpenCodeShapedRealization(), "opencode-realization"));

        Assert.True(facts.InitiallyResolved);
        Assert.Equal("glm-5.3-flash", facts.InitialModel);
        Assert.True(facts.AfterFlipResolved);
        Assert.Equal("glm-5.3-flash", facts.FallbackFrom);
        Assert.True(facts.UnknownProfileFailClosed);
    }

    [Fact]
    public void RealizationSwap_OnlyChangesCompositionRoot_ConsumerClosureUnchanged()
    {
        // 同一闭包委托依次消费两个 realization：互换后诚实性不变——
        // 翻转后要么回退（Resolved 且有 FallbackFrom 轨迹）要么 fail-closed
        // （不 Resolved 且无 binding），永不静默成功；闭包代码零改动。
        var realizations = new[]
        {
            (Instance: SlowReplayProfiles.CreateDefault(), Name: "slow-replay-realization"),
            (Instance: OpenCodeShapedRealization(), Name: "opencode-realization"),
        };
        foreach (var (instance, name) in realizations)
        {
            var facts = ExerciseContract(RegistryResolvedInstance(instance, name));

            Assert.True(facts.InitiallyResolved);
            Assert.True(facts.UnknownProfileFailClosed);
            Assert.Equal(facts.AfterFlipResolved, facts.FallbackFrom is not null);
        }
    }
}
