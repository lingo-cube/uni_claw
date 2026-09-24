using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SCN-002 验收面：生成式场景 ≡ 录制版（D7——WiFi off→on 生成版验证
/// 与录制等价）。Acceptance 1/2（等价构造 + 相同结果）、3（Inject 生效）、
/// 5（两次运行同 digest）、6（录制零回归——由既有 DeterministicScenario
/// 全量承载）；4（JSON 入库）由 scenarios/SCN-WIFI-006.json + 覆盖率
/// 工具承载（本测试带其 Scenario trait）。
///
/// SIM-003：SCN-WIFI-006 等价面经 ScenarioLibrary.Load 解析（certified
/// execution 绑定：carrier=wifi-off-to-on-generated，期望由本条目 certified
/// JSON 投影）；Inject/determinism/sugar 测试为 test-local 派生 fixture
/// （非注册场景），保留 ScenarioBuilder 直用与 Expect 变换。
/// </summary>
public sealed class GeneratedScenarioTests
{
    /// <summary>Acceptance 1+2：生成版（certified 载体）与录制版经同一
    /// runner 产生相同的全部语义结果（状态/分类/effect/咨询/未消费/goal +
    /// 消费轨迹）。等价断言是两 run 的关系不变量（Type-B），golden 值由
    /// 各自 AcceptancePassed 承载。</summary>
    [Fact]
    [Trait("Scenario", "SCN-WIFI-006")]
    public void GeneratedWiFiOffToOn_IsEquivalentToRecorded()
    {
        var recorded = ScenarioRunner.Run(ScenarioLibrary.Load("SCN-WIFI-001").Bundle);
        var generated = ScenarioRunner.Run(ScenarioLibrary.Load("SCN-WIFI-006").Bundle);

        Assert.Equal(recorded.Report.RunDriveStatus, generated.Report.RunDriveStatus);
        Assert.Equal(recorded.Report.Outcome?.Classification, generated.Report.Outcome?.Classification);
        Assert.Equal(recorded.Report.EffectDeliveries, generated.Report.EffectDeliveries);
        Assert.Equal(recorded.Report.AgentConsultations, generated.Report.AgentConsultations);
        Assert.Equal(recorded.Report.UnconsumedStimulusIds, generated.Report.UnconsumedStimulusIds);
        Assert.Equal(recorded.Report.GoalEvaluation?.Satisfaction, generated.Report.GoalEvaluation?.Satisfaction);
        Assert.Equal(recorded.Report.ConsumedStimulusIds, generated.Report.ConsumedStimulusIds);
        Assert.True(generated.Report.AcceptancePassed);
    }

    /// <summary>test-local：等价场景的确定性旁证（无注册条目——构造幂等
    /// 与两次运行同 digest）。模板期望来自 SCN-WIFI-001 certified 投影。</summary>
    [Fact]
    public void GeneratedScenario_IsDeterministic_BuildAndRun()
    {
        static MinimalScenarioBundle Build() => ScenarioLibrary.Load("SCN-WIFI-006").Bundle;
        var first = Build();
        var second = Build();
        Assert.Equal(first.BundleDigest, second.BundleDigest); // 层1：构造幂等

        var runA = ScenarioRunner.Run(first);
        var runB = ScenarioRunner.Run(second);
        Assert.Equal(runA.Report.SemanticDigest, runB.Report.SemanticDigest); // 层3
        Assert.True(runA.Report.AcceptancePassed);
    }

    /// <summary>Acceptance 3：Inject(stimulus, afterStep: 1)——第 1 个
    /// effect 的 post-action 观察后到达。WiFi 单 effect 场景在 post 观察
    /// 即终局：注入帧应保持未消费（迟到语义），run 结果不受扰动——
    /// 期望面经 Expect 参数化（unconsumed 0→1）。</summary>
    [Fact]
    public void Inject_AfterStepOne_LatePostFrameStaysUnconsumed_RunUnaffected()
    {
        var late = GoldenScenarioBundles.LatePostActionStimulus();
        var bundle = ScenarioBuilder
            .FromTemplate(() => GoldenScenarioBundles.WifiToggleOffToOn())
            .WithScenarioId("generated-wifi-late", "wifi-off-to-on-late")
            .Inject(late, afterStep: 1)
            .Expect(e => e with { ExpectedUnconsumedStimuli = 1 })
            .Build();

        // 插入位事实：late 位于 obs-2-post（第 1 个 post 帧）之后
        Assert.Equal(2, Array.IndexOf(bundle.Stimuli.ToArray(), late));

        var execution = ScenarioRunner.Run(bundle);
        Assert.Equal(RunDriveStatus.Completed.ToString(), execution.Report.RunDriveStatus);
        Assert.Equal(new[] { late.StimulusId }, execution.Report.UnconsumedStimulusIds);
        Assert.True(execution.Report.AcceptancePassed);
    }

    /// <summary>D3：FixedTimingScheduler 是条件式接口的实现（架构锚点），
    /// afterStep 语法糖与直接传调度器解析出相同插入位。</summary>
    [Fact]
    public void AfterStepSugar_ResolvesSamePositionAsExplicitScheduler()
    {
        var viaSugar = ScenarioBuilder
            .FromTemplate(() => GoldenScenarioBundles.WifiToggleOffToOn())
            .WithScenarioId("a", "sugar")
            .Inject(GoldenScenarioBundles.LatePostActionStimulus(), afterStep: 1)
            .Build();
        var viaScheduler = ScenarioBuilder
            .FromTemplate(() => GoldenScenarioBundles.WifiToggleOffToOn())
            .WithScenarioId("a", "scheduler")
            .Inject(GoldenScenarioBundles.LatePostActionStimulus(), new FixedTimingScheduler(1))
            .Build();

        // 插入位同源：stimulus 序列（按 id）一致，late 同在第 2 位
        Assert.Equal(
            viaSugar.Stimuli.Select(s => s.StimulusId),
            viaScheduler.Stimuli.Select(s => s.StimulusId));
        Assert.Equal(2, viaSugar.Stimuli.ToList().FindIndex(s => s.StimulusId == "obs-2-late"));
        Assert.Equal(viaSugar.Stimuli.Count, viaScheduler.Stimuli.Count);
    }
}
