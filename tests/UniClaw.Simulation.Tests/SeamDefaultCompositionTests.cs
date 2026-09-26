using System.Reflection;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-001 Review/Verify：Seams=null 时六缝工厂默认的 composition 级证明。
/// 与 <see cref="SeamOverrideTests.NullSeams_FactoryDefaultsUsed_BackwardsCompatible"/>
/// （行为级：Completed / 1 effect / 1 consultation）互补——本文件只回答
/// 「每个缝默认到底是什么 realization」，不经由注入路径推断。
/// 只读反射私有组合字段（WorldModel/RuntimeAssurance 未暴露策略面；
/// 不为测试开新公开面——现有可及面纪律）。
/// </summary>
public sealed class SeamDefaultCompositionTests
{
    private static object? StrategyField(Type owner, string field, object instance) =>
        owner.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);

    /// <summary>
    /// 六缝默认组合：Driver=DeterministicEffectDriver、Agent=bundle PhaseScript
    /// 构造的 ScriptedUniAgent、Association=SeedingAssociationStrategy、
    /// Observation=ReplayFrameObservationStrategy、Freshness=SatisfyingFreshness
    /// （SimulationHost 私有嵌套）、Continuity=null（Compose 直传 seams?.Continuity，
    /// 无兜底——见 exact finding：SimContract.cs:519 注释宣称的默认
    /// RoleContinuityStrategy 位于 UniClaw.Kernel.Tests，本组合根不落该默认；
    /// WorldModel 对 null continuity 的语义 = demand 时 fail-closed 抛
    /// InvalidOperationException（WorldModel.cs:741），静默场景不触发）。
    /// </summary>
    [Fact]
    public void NullSeams_AllSixSeams_ResolveDocumentedFactoryDefaults()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var host = SimulationHost.Compose(bundle, new RunOptions { Seams = null });

        // ---- Driver / Agent：现有 internal 直接面 ----
        Assert.IsType<DeterministicEffectDriver>(host.EffectDriver);
        Assert.NotNull(host.ScriptedAgent);
        Assert.Equal("ScriptedUniAgent", host.ScriptedAgent.GetType().Name);

        // ---- Association / Observation / Continuity：WorldModel 私有组合字段 ----
        var worldType = typeof(WorldModel);
        Assert.IsType<SeedingAssociationStrategy>(
            StrategyField(worldType, "_associationStrategy", host.WorldCore));
        Assert.IsType<ReplayFrameObservationStrategy>(
            StrategyField(worldType, "_observationStrategy", host.WorldCore));
        Assert.Null(StrategyField(worldType, "_continuityStrategy", host.WorldCore));

        // ---- Freshness：RuntimeAssurance 私有字段 → SimulationHost 私有嵌套默认 ----
        var freshness = StrategyField(
            typeof(RuntimeAssurance), "_freshnessEvaluator", host.AssuranceCore);
        Assert.NotNull(freshness);
        Assert.Equal("SatisfyingFreshness", freshness.GetType().Name);
    }

    /// <summary>
    /// 默认 Freshness 的行为级证明（SIM-001 owner 指令：Assert.Same → 行为级；
    /// 注入侧行为级已在 SeamOverrideTests.InjectedFreshness_IsUsed）：无注入时
    /// SatisfyingFreshness 真实经 RuntimeAssurance.Judge 执法——其确定性 basis
    /// （"scripted:sufficient"，SimulationHost.cs:281）进入产品 owner facts。
    /// </summary>
    [Fact]
    public void DefaultFreshness_SatisfyingBasis_EntersOwnerJudgments()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var host = SimulationHost.Compose(bundle, new RunOptions { Seams = null });
        Assert.True(host.KernelCore.AdmitContract(bundle.Contract).Accepted);
        Assert.True(host.Driver.Activate().Accepted);

        var result = host.DriveOnce();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        var judgment = Assert.Single(host.Facts.Judgments);
        Assert.Equal(FreshnessSufficiency.Sufficient, judgment.Freshness.Sufficiency);
        Assert.Equal("scripted:sufficient", judgment.Freshness.Reason);
    }
}
