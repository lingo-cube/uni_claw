using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// CAP-009 — 感知组合根注册形态：uni.perception 实例注册、fast 依赖锚点闭合、
/// slow.visual 维持 description-only（理由在案）。
/// </summary>
public sealed class PerceptionCapabilityCompositionTests
{
    [Fact]
    public void RegisterProductPerception_RegistersInstanceAnchorsAndVisual()
    {
        var registry = PerceptionCapabilityComposition.RegisterProductPerception();

        // uni.perception：可执行实例（Resolve 取回，非 null）。
        var resolved = registry.Resolve(PerceptionCapabilityComposition.UniPerceptionId);
        Assert.IsAssignableFrom<ISemanticPerception>(resolved);
        Assert.IsAssignableFrom<IUiElementPerception>(resolved);
        Assert.IsAssignableFrom<ICapabilityHealthCheckable>(resolved);
        // 缺省无健康源：诚实 Unknown（组合根应注入真实探针）。
        Assert.Equal(HealthStatus.Unknown, ((ICapabilityHealthCheckable)resolved!).CheckHealth().Status);

        // fast 依赖锚点闭合：fast.yolo / fast.ocr 存在（description-only 例外，
        // 理由记录于组合根注释与 CAP-009）。
        Assert.NotNull(registry.Get(PerceptionCapabilityComposition.FastYoloId));
        Assert.NotNull(registry.Get(PerceptionCapabilityComposition.FastOcrId));

        // slow.visual：独立能力，description-only（无运行时实例）。
        var visual = registry.Get(PerceptionCapabilityComposition.SlowVisualId);
        Assert.NotNull(visual);
        Assert.Equal(CapabilityCategory.IndependentProductPerception, visual!.Category);
        Assert.Null(registry.Resolve(PerceptionCapabilityComposition.SlowVisualId));

        // 依赖图闭合：4 个条目、4 条事实。fast.yolo/fast.ocr 锚点在册；
        // slow.text 按 ADR-0035 设计决定不注册（由 uni.model.management 承载
        // slow.semantic.text profile），依赖图以边注表达而非注册条目。
        Assert.Equal(4, registry.Facts.Count);
        var registeredIds = registry.Facts.Select(f => f.CapabilityId).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(PerceptionCapabilityComposition.FastYoloId, registeredIds);
        Assert.Contains(PerceptionCapabilityComposition.FastOcrId, registeredIds);
        Assert.DoesNotContain("slow.text", registeredIds);
    }

    [Fact]
    public void RegisterProductPerception_ComposesInjectedHealthSources()
    {
        var models = new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.Text, "zai", "glm"),
        });
        var uniPerception = new UniPerceptionCapability(new[]
        {
            new PerceptionHealthSource("fast.yolo-ocr-assets",
                () => new CapabilityHealthReport(HealthStatus.Healthy, null)),
            new PerceptionHealthSource("slow.text-models", models.CheckHealth),
        });

        var registry = PerceptionCapabilityComposition.RegisterProductPerception(uniPerception: uniPerception);
        var resolved = (ICapabilityHealthCheckable)registry.Resolve(
            PerceptionCapabilityComposition.UniPerceptionId)!;

        Assert.Equal(HealthStatus.Healthy, resolved.CheckHealth().Status);
        // 健康源共享活状态：摘除模型候选后，经 registry 取回的实例观测到联动
        //（唯一 profile 摘除 → 模型 Unhealthy → 感知 Unhealthy，诚实传播）。
        models.ApplyHealth(LogicalProfileId.Text, "zai", "glm", healthy: false);
        Assert.Equal(HealthStatus.Unhealthy, resolved.CheckHealth().Status);
    }

    [Fact]
    public void RegisterProductPerception_DuplicateRegistration_FailsClosed()
    {
        var registry = PerceptionCapabilityComposition.RegisterProductPerception();

        Assert.Throws<InvalidOperationException>(
            () => PerceptionCapabilityComposition.RegisterProductPerception(registry));
    }
}
