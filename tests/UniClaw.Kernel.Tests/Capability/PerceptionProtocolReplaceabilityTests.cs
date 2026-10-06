using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

/// <summary>
/// CAP-011 — 感知维度 R5 可替换性执法（所有者澄清 2026-10-07：替换发生在
/// L2 实现类，消费闭包钉死在 L1 双协议面 <see cref="ISemanticPerception"/> +
/// <see cref="IUiElementPerception"/> 上，不混入其他维度）。同一消费闭包
/// 消费两个 L2 实现——产品实现 <see cref="UniPerceptionCapability"/> 与
/// 确定性 replay 形状 L2 替换件——协议契约互换成立；L2 替换只动组合根
/// 注册，消费方零改动。健康聚合（<see cref="ICapabilityHealthCheckable"/>
/// mixin，CAP-009 冻结语义）是当前唯一可观察行为面；协议负载词汇冻结前
/// 不预造（CAP-009 out-of-scope）。
/// </summary>
public sealed class PerceptionProtocolReplaceabilityTests
{
    /// <summary>健康聚合律（协议契约的一部分，与 L2 是谁无关）。null = 探针异常。</summary>
    private static readonly (HealthStatus? First, HealthStatus? Second, HealthStatus Expected)[] AggregationLaw =
    {
        (HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Healthy),
        (HealthStatus.Healthy, HealthStatus.Unknown, HealthStatus.Degraded),   // 部分可观测 = 降级
        (HealthStatus.Unknown, HealthStatus.Unknown, HealthStatus.Degraded),
        (HealthStatus.Degraded, HealthStatus.Healthy, HealthStatus.Degraded),
        (HealthStatus.Healthy, HealthStatus.Unhealthy, HealthStatus.Unhealthy), // worst-of
        (null, HealthStatus.Healthy, HealthStatus.Degraded),                   // 探针异常 = 诚实 Degraded
    };

    /// <summary>
    /// 消费闭包：只经 L1 双协议面消费（语义买方 + UI 元素买方 + 诊断买方
    /// 三个视角），不出现任何 L2 具体类型。force 是组合根/adapter 侧的
    /// 场景驱动钩子（消费者看不到）。
    /// </summary>
    private static void AssertDualProtocolContract(
        ISemanticPerception semantic,
        IUiElementPerception ui,
        Action<HealthStatus?, HealthStatus?> force)
    {
        // 组合根保证：两个协议面来自同一实例（一次 Resolve，双面消费）。
        ICapability capability = semantic;
        Assert.Same(capability, ui);

        // L0 自述（消费者侧核对）：身份与双协议声明。
        var description = capability.Description;
        Assert.Equal("uni.perception", description.CapabilityId);
        Assert.Contains(description.Protocols, p => p.Name == PerceptionProtocol.Semantic);
        Assert.Contains(description.Protocols, p => p.Name == PerceptionProtocol.UiElement);

        // 诊断买方可观察的健康聚合律：换 L2 不换律。
        var health = Assert.IsAssignableFrom<ICapabilityHealthCheckable>(capability);
        foreach (var (first, second, expected) in AggregationLaw)
        {
            force(first, second);
            Assert.Equal(expected, health.CheckHealth().Status);
        }
    }

    /// <summary>产品 L2：生产形状注入（fast 资产面 + 模型端两源，
    /// 镜像 Program.cs 的健康源注入方式）。</summary>
    private static (UniPerceptionCapability Instance, Action<HealthStatus?, HealthStatus?> Force) ProductionShapedL2()
    {
        var fast = HealthStatus.Healthy;
        var model = HealthStatus.Healthy;
        var fastThrows = false;
        var instance = new UniPerceptionCapability(new[]
        {
            new PerceptionHealthSource("fast.assets", () => fastThrows
                ? throw new InvalidOperationException("asset probe unavailable")
                : new CapabilityHealthReport(fast)),
            new PerceptionHealthSource("model.management", () => new CapabilityHealthReport(model)),
        });
        return (instance, (f, m) =>
        {
            fastThrows = f is null;
            fast = f ?? HealthStatus.Healthy;
            model = m ?? HealthStatus.Healthy;
        });
    }

    /// <summary>
    /// 确定性 replay 形状 L2 替换件（test double 先例：
    /// ModelManagementTests.NonModelManagementCapability）。内部结构刻意
    /// 与产品实现不同（字典驱动 + Max 排序聚合，非 Any 链），证明替换只
    /// 要求遵守同一协议契约，不要求复制实现形状。null = 模拟探针故障。
    /// </summary>
    private sealed class ReplayPerceptionCapability : ISemanticPerception, IUiElementPerception, ICapabilityHealthCheckable
    {
        private readonly Dictionary<string, HealthStatus?> _sources = new(StringComparer.Ordinal)
        {
            ["replay.fast"] = HealthStatus.Healthy,
            ["replay.slow"] = HealthStatus.Healthy,
        };

        public CapabilityDescription Description => UniPerceptionCapability.CanonicalDescription;

        public CapabilityHealthReport CheckHealth()
        {
            if (_sources.Count == 0)
                return new(HealthStatus.Unknown, "no replay sources");
            static int Rank(HealthStatus? status) => status switch
            {
                HealthStatus.Unhealthy => 3,
                HealthStatus.Degraded => 2,
                null or HealthStatus.Unknown => 2, // 缺席/不可观测按降级律处理
                _ => 1,
            };
            var ranked = _sources.Select(p => (p.Key, Rank(p.Value))).ToArray();
            var status = (HealthStatus)ranked.Max(r => r.Item2);
            var diagnostic = string.Join("; ", ranked.Select(r =>
                $"{r.Key}={(_sources[r.Key] is { } s ? s.ToString() : "probe threw")}"));
            return new(status, diagnostic);
        }

        public void Force(HealthStatus? fast, HealthStatus? slow)
        {
            _sources["replay.fast"] = fast;
            _sources["replay.slow"] = slow;
        }
    }

    [Fact]
    public void SameConsumerClosure_ProductL2_HoldsDualProtocolContract()
    {
        var (instance, force) = ProductionShapedL2();

        AssertDualProtocolContract(instance, instance, force);
    }

    [Fact]
    public void SameConsumerClosure_ReplayL2Replacement_HoldsDualProtocolContract()
    {
        var replacement = new ReplayPerceptionCapability();

        AssertDualProtocolContract(replacement, replacement, replacement.Force);
    }

    [Fact]
    public void L2Swap_OnlyChangesCompositionRoot_ConsumerClosureUnchanged()
    {
        // L2 替换 = 换组合根注册物：同一闭包委托依次消费 registry 取回的
        // 两个 L2，代码零改动；替换件注册同时首次行使 ValidateImplementation
        // 对非生产 L2 的协议-接口一致性执法。
        var (product, productForce) = ProductionShapedL2();
        var replacement = new ReplayPerceptionCapability();

        var compositions = new (ICapability Instance, Action<HealthStatus?, HealthStatus?> Force)[]
        {
            (product, productForce),
            (replacement, replacement.Force),
        };
        foreach (var (instance, force) in compositions)
        {
            var registry = new CapabilityRegistry(TrustDomain.Product);
            registry.Register(instance, "test-composition");
            var resolved = registry.Resolve("uni.perception");

            Assert.NotNull(resolved);
            AssertDualProtocolContract(
                (ISemanticPerception)resolved!, (IUiElementPerception)resolved!, force);
        }
    }
}
