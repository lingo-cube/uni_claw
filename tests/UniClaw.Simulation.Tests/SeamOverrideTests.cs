using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-001 缝注入旋钮 + SIM-004 seam 收敛证明：注入组件经同一 Product 缝
/// 进入；观测（agent consultations / effect deliveries）对注入 realization
/// 同样成立——effect 计数来自产品 facts（EffectReceipts）、agent 计数来自
/// seam 消费侧 journal（ABS-001/002 关闭的机械证明）。
/// </summary>
public sealed class SeamOverrideTests
{
    private sealed record InjectedFreshness : IFreshnessEvaluator
    {
        public bool WasCalled { get; private set; }
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input)
        {
            WasCalled = true;
            return new FreshnessJudgment(FreshnessSufficiency.Sufficient, "injected");
        }
    }

    private sealed record InjectedDriver : IEffectDriver
    {
        public int Calls { get; private set; }
        public DispatchResult Deliver(DispatchRequest request)
        {
            Calls++;
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "injected-driver",
                DateTimeOffset.UtcNow, null);
        }
    }

    /// <summary>
    /// SIM-001 Backward compat（composition + 行为级）：Seams = null → 工厂
    /// 默认组合照旧——ScriptedUniAgent（bundle PhaseScript）+
    /// DeterministicEffectDriver，场景语义不变（Completed / 1 effect /
    /// 2 consultations——WifiToggle 两轮：InitialPlanning 出 tap 决策 +
    /// StepVerified 终局确认）。不是只断言 options.Seams == null。
    /// </summary>
    [Fact]
    public void NullSeams_FactoryDefaultsUsed_BackwardsCompatible()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var host = SimulationHost.Compose(bundle, new RunOptions { Seams = null });
        // composition inspection：默认 realization 就位
        Assert.NotNull(host.ScriptedAgent); // bundle PhaseScript → ScriptedUniAgent
        Assert.IsType<DeterministicEffectDriver>(host.EffectDriver);
        // default 路径行为与既有回归一致
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        var result = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(1, host.EffectDeliveryCount);
        Assert.Equal(2, host.Consultations.Calls.Count); // InitialPlanning + StepVerified
        Assert.Empty(host.Consultations.Violations);
    }

    /// <summary>
    /// SIM-001 行为级证明：注入 Freshness 经 RuntimeAssurance.Judge 的唯一
    /// 执法点（FRS-007 D4）被真实调用——WasCalled 翻转 + 判定结果
    /// （basis = "injected"）随 AssuranceJudgment.Freshness 进入 owner log。
    /// </summary>
    [Fact]
    public void InjectedFreshness_IsUsed()
    {
        var freshness = new InjectedFreshness();
        Assert.False(freshness.WasCalled);
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var host = SimulationHost.Compose(bundle, new RunOptions
        {
            Seams = new SeamOverrides { Freshness = freshness },
        });
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        var result = host.DriveOnce();

        Assert.True(freshness.WasCalled); // Host 真调用了注入 evaluator
        Assert.Equal(RunDriveStatus.Completed, result.Status);
        // 注入 evaluator 的确定性结果进入产品 owner facts（FRS-007 起 reason 字段名）
        var judgment = Assert.Single(host.Facts.Judgments);
        Assert.Equal(FreshnessSufficiency.Sufficient, judgment.Freshness.Sufficiency);
        Assert.Equal("injected", judgment.Freshness.Reason);
        Assert.Empty(host.Consultations.Violations);
    }

    /// <summary>
    /// SIM-001 行为级证明：注入 Driver 被 EffectBoundary 真实 dispatch——
    /// 完整 drive 后 Calls == 预期 dispatch 数；EffectDeliveryCount /
    /// EffectReceipts 继续来自产品 owner facts。
    /// </summary>
    [Fact]
    public void InjectedDriver_IsUsed()
    {
        var driver = new InjectedDriver();
        Assert.Equal(0, driver.Calls);
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var host = SimulationHost.Compose(bundle, new RunOptions
        {
            Seams = new SeamOverrides { Driver = driver },
        });
        Assert.Same(driver, host.EffectDriver); // 组合采用注入实例
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        var result = host.DriveOnce();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(1, driver.Calls); // 行为级：注入 driver 被真实调用
        Assert.Equal(1, host.EffectDeliveryCount); // 产品 facts 同源核对
        Assert.Equal(1, host.KernelCore.EffectReceipts.Count);
        Assert.Empty(host.Consultations.Violations);
    }

    /// <summary>
    /// SIM-004（ABS-002）：注入 driver 后 EffectDeliveryCount 从产品 owner
    /// facts（EffectReceipts）取数——原 is-DeterministicEffectDriver cast 的
    /// -1 sentinel 洞已删除，注入 driver 的 run 不再破坏报告面。
    /// </summary>
    [Fact]
    public void InjectedDriver_EffectDeliveryCount_ComesFromProductReceipts_NotSentinel()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var host = SimulationHost.Compose(bundle, new RunOptions
        {
            Seams = new SeamOverrides { Driver = new InjectedDriver() },
        });
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        var result = host.DriveOnce();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(1, host.EffectDeliveryCount); // 产品 facts——注入 driver 不再 -1
        Assert.Equal(1, host.KernelCore.EffectReceipts.Count); // 同源核对（owner 投影）
        Assert.Empty(host.Consultations.Violations);
    }

    /// <summary>
    /// SIM-004（ABS-001）crown 证明：任意 Func realization 经 Product Consult
    /// seam 进入 Kernel——ScriptedAgent probe 为 null（组合面不要求
    /// ScriptedUniAgent concrete），AgentConsultations 由 seam journal 计数
    /// （对注入 realization 同律），run 语义（certified 六字段）不变。
    /// </summary>
    [Fact]
    public void InjectedAgentRealization_EntersThroughProductSeam_ObservabilityHolds()
    {
        var bundle = GoldenScenarioBundles.AlreadyOnZeroEffect();
        AgentDecisionContext? observed = null;
        var host = SimulationHost.Compose(bundle, new RunOptions
        {
            Seams = new SeamOverrides
            {
                Agent = context =>
                {
                    observed = context;
                    return new AgentDecision.NoAction(new AgentNoActionProposal(
                        context.DecisionId, "injected-realization"));
                },
            },
        });
        Assert.Null(host.ScriptedAgent); // 组合面不再要求 concrete double

        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);
        var result = host.DriveOnce();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.NotNull(observed); // seam 语义不变：Product context 照常下发
        Assert.Equal(AgentDecisionPhase.InitialPlanning, observed!.Phase);
        Assert.Single(host.Consultations.Calls); // journal 计数——realization 无关
        Assert.Empty(host.Consultations.CheckExpectedConsultations(1));
        Assert.Empty(host.Consultations.Violations);
        Assert.Equal(0, host.EffectDeliveryCount);
    }
}
