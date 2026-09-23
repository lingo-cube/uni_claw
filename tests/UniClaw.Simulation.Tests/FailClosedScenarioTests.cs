using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 Phase 1 fail-closed 场景：agent 无响应 → 零 Effect 非终态；
/// stimulus context 失配 → 消费纪律拒绝；metrics 显式 N/A 而非伪造 0；
/// 重复 Submit 的 stimulus id → Rejected 且不被消费。
/// 变体 bundle 全部经 digest 重封（ScenarioBundleDigest.Sealed）。
/// </summary>
public sealed class FailClosedScenarioTests
{
    /// <summary>
    /// agent 无响应（NoResponse 脚本）：driver fail closed——
    /// AgentDecisionFailed、Reason=no-response、零 Effect、非终态，
    /// consultation 恰好发生一次（失败也计入真实边界调用）。
    /// </summary>
    [Fact]
    public void AgentNoResponse_FailsClosed_ZeroEffects_NonTerminal()
    {
        var bundle = ScenarioBundleDigest.Sealed(
            GoldenScenarioBundles.WifiToggleOffToOn() with
            {
                AgentScript = new AgentScriptStep(AgentScriptKind.NoResponse, Array.Empty<ScriptActionStep>(), "no response"),
                // 适配：初始观察在 consultation 前已被消费，obs-2-post 保持 unconsumed（=1）
                Expected = new ScenarioExpectation("AgentDecisionFailed", null, 0, 1, 1, null),
                ScenarioId = "wifi-no-response",
            });

        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));
        Assert.Equal(RunDriveStatus.AgentDecisionFailed.ToString(), report.RunDriveStatus);
        Assert.Equal("no-response", report.Reason);
        Assert.Equal(0, host.EffectDeliveryCount);
        Assert.False(host.Facts.IsRunTerminal);
        Assert.Equal(1, report.AgentConsultations);
    }

    /// <summary>
    /// stimulus context 失配：初始 stimulus 携带 PostActionEffectFlow context
    /// （期望 External）→ feed 判 Unexpected → driver UnexpectedInput、
    /// 零 Effect、非终态；第二个 stimulus 保持 unconsumed。
    /// </summary>
    [Fact]
    public void ContextMismatchedStimulus_FailsClosed_UnexpectedInput()
    {
        var s1 = GoldenScenarioBundles.WifiToggleOffToOn();
        var initial = (ScenarioStimulus.ObservationFrame)s1.Stimuli[0];
        var bundle = ScenarioBundleDigest.Sealed(s1 with
        {
            ScenarioId = "wifi-ctx-mismatch",
            Stimuli = new ScenarioStimulus[]
            {
                initial with { Context = ObservationContext.PostActionEffectFlow },
                s1.Stimuli[1],
            },
            // 适配：context 失配帧被记为 unexpected 但不dequeue，两条 stimulus 均保持 unconsumed（=2）
            Expected = new ScenarioExpectation("UnexpectedInput", null, 0, 0, 2, null),
        });

        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));
        Assert.Equal(RunDriveStatus.UnexpectedInput.ToString(), report.RunDriveStatus);
        Assert.Equal(0, host.EffectDeliveryCount);
        Assert.False(host.Facts.IsRunTerminal);

        Assert.Single(report.UnexpectedStimuli);
        Assert.Contains(initial.StimulusId, report.UnexpectedStimuli[0]);

        // 两条 stimulus 均保持 unconsumed（失配帧不 dequeue，第二条从未被拉取）
        Assert.Equal(2, report.UnconsumedStimulusIds.Count);
        Assert.Contains("obs-1-initial", report.UnconsumedStimulusIds);
        Assert.Contains("obs-2-post", report.UnconsumedStimulusIds);
    }

    /// <summary>
    /// metrics 诚实性：无 live model 的计数显式 N/A（绝不伪造为 0）；
    /// 结构计数（observations / reconciliations / regrounds）达到
    /// happy path 的最小语义量；verification mode 声明 post-action gate。
    /// </summary>
    [Fact]
    public void Metrics_ExplicitNA_NotZero()
    {
        var execution = ScenarioRunner.Run(GoldenScenarioBundles.WifiToggleOffToOn());
        var metrics = execution.Report.Metrics;

        Assert.StartsWith("N/A", metrics.ModelCalls, System.StringComparison.Ordinal);
        Assert.StartsWith("N/A", metrics.InputTokens, System.StringComparison.Ordinal);

        Assert.True(metrics.Observations >= 2, $"observations={metrics.Observations}");
        Assert.True(metrics.ReconciliationsNew >= 2, $"reconciliationsNew={metrics.ReconciliationsNew}");
        Assert.True(metrics.Regrounds >= 1, $"regrounds={metrics.Regrounds}");
        Assert.Contains("post-action-observation-gate", metrics.VerificationMode, System.StringComparison.Ordinal);
        Assert.True(metrics.CriticalPathLatencyMs >= 0);
    }

    /// <summary>
    /// D21 feed Submit 纪律：运行期提交重复 StimulusId → 记入 Rejected、
    /// 返回 false、不消费（fail closed 不上抛）；原 stimulus 语义不受影响。
    /// </summary>
    [Fact]
    public void DuplicateSubmitStimulusId_Rejected_NotConsumed()
    {
        var bundle = GoldenScenarioBundles.MissingPostActionStimulus();
        var execution = ScenarioRunner.Run(bundle, new RunOptions { Phased = true });
        var host = execution.Host;

        // 重复 id（已消费的 obs-1-initial）→ Rejected、不消费
        Assert.False(host.SubmitStimulus(((ScenarioStimulus.ObservationFrame)bundle.Stimuli[0])
            with { StimulusId = "obs-1-initial" }));
        Assert.Equal(new[] { "obs-1-initial" }, host.Feed.Rejected);
        Assert.DoesNotContain("obs-1-initial", host.Feed.Remaining);
        Assert.Equal(1, host.Feed.Consumed.Count(id => id == "obs-1-initial"));
    }
}
