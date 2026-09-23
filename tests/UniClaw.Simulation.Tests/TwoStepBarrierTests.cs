using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 D22 — 两步串行 scenario（不变量 43 serialization barrier）：
/// proposal 携带有界有序 steps，每步独立完整链，下一步 dispatch 只能在
/// 上一步 post-action 证据被消费并 reconcile 之后发生。
/// </summary>
public sealed class TwoStepBarrierTests
{
    /// <summary>
    /// 两步 happy path（不变量 43）：step1 tap toggle（DesiredState "true"）
    /// → 等待并消费 obs-2-post（barrier 通过）→ step2 tap menuItem
    /// （DesiredState null）→ 消费 obs-3-post → terminal Completion，
    /// 恰好 2 effects、1 consultation、goal Satisfied。
    /// step2 的 "menuItem" role 来自 manifest frame 元素（bounds +
    /// perceptionType "menuItem"，在 case-b-on 与 case-a-before 两帧均有）。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-BARRIER-001")]
    public void TwoStep_EvidenceBetweenSteps_SecondEffectAllowed_TerminalCompletion()
    {
        var bundle = GoldenScenarioBundles.TwoStepToggleThenMenuItem();
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));

        Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);
        Assert.Equal(TerminalClassification.Completion, report.Outcome!.Classification);
        Assert.Equal(2, report.EffectDeliveries);
        Assert.Equal(2, host.EffectDeliveryCount);
        Assert.Equal(2, host.Facts.EffectReceipts.Count);
        Assert.Equal(2, report.AgentConsultations); // RUN-004: E1 返回转移多一次 NoAction 咨询
        Assert.Empty(report.UnconsumedStimulusIds);
        Assert.Equal(new[] { "obs-1-initial", "obs-2-post", "obs-3-post" }, report.ConsumedStimulusIds);
        Assert.Equal(GoalSatisfaction.Satisfied, report.GoalEvaluation!.Satisfaction);
        Assert.True(host.Facts.IsRunTerminal);
        Assert.Empty(report.AgentViolations);
    }

    /// <summary>
    /// 两步缺中间证据（不变量 43 证伪面，phased）：仅初始帧 → DriveOnce 后
    /// WaitingForInput("post-action-evidence")，此时 EffectDeliveries == 1
    /// ——step2 被 serialization barrier 阻塞，第二次 effect 不得发生；
    /// Submit(cancel) → DriveOnce → SafeStop terminal，effects 仍为 1。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-BARRIER-002")]
    public void TwoStep_MissingMiddleEvidence_SecondEffectBlocked_BySerializationBarrier()
    {
        var bundle = GoldenScenarioBundles.TwoStepMissingMiddleEvidence();
        var execution = ScenarioRunner.Run(bundle, new RunOptions { Phased = true });
        var host = execution.Host;

        // 等待中：期望状态（WaitingForInput、1 effect、0 unconsumed）
        var pending = execution.Report;
        Assert.False(pending.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Reason, StringComparison.Ordinal);
        Assert.Equal(RunDriveStatus.WaitingForInput.ToString(), pending.RunDriveStatus);
        Assert.Equal("post-action-evidence", pending.Reason!["PhasedPending:".Length..]);

        // 不变量 43：step1 已 dispatch（1 effect），step2 因证据缺失被阻塞
        Assert.Equal(1, host.EffectDeliveryCount);
        Assert.Equal(1, host.Facts.EffectReceipts.Count);
        Assert.False(host.Facts.IsRunTerminal);

        // cancel 经声明的 SafeStop obligation → SafeStop terminal，仍零第二 effect
        Assert.True(host.SubmitStimulus(GoldenScenarioBundles.SafeStopCancelStimulus()));
        var cancelled = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, cancelled.Status);
        Assert.Equal(TerminalClassification.SafeStop, cancelled.Outcome!.Classification);
        Assert.True(host.Facts.IsRunTerminal);
        Assert.Equal(1, host.EffectDeliveryCount);

        var report = ScenarioRunner.FinalizePhased(bundle, host);
        // bundle.Expected 描述等待中状态；终态字段逐项核对（effects 屏障不破）
        Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);
        Assert.Equal(TerminalClassification.SafeStop, report.Outcome!.Classification);
        Assert.Equal(1, report.EffectDeliveries);
        Assert.Equal(1, report.AgentConsultations); // RUN-004：cancel 路径无 E1 返回转移咨询
        Assert.Empty(report.AgentViolations);
        Assert.Empty(report.UnconsumedStimulusIds);
    }

    /// <summary>
    /// post-action frame 存在不等于上一步已验证：若 toggle 仍是 false，
    /// Assurance verification 必须 fail closed，不得仅凭 context 正确就放行
    /// menuItem 的第二次 Effect。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-BARRIER-003")]
    public void TwoStep_PostActionContradictsDesiredState_SecondEffectBlocked()
    {
        var original = GoldenScenarioBundles.TwoStepToggleThenMenuItem();
        var post = (ScenarioStimulus.ObservationFrame)original.Stimuli[1];
        var contradictory = post with
        {
            ReviewedElements = post.ReviewedElements
                .Select(element => element.Role == "toggle"
                    ? element with { State = "false" }
                    : element)
                .ToList(),
            ReviewedStateClaims = new[] { ("switch.wifi", "false") },
        };
        var bundle = ScenarioBundleDigest.Sealed(original with
        {
            ScenarioId = "wifi-two-step-contradictory-post",
            Stimuli = new[] { original.Stimuli[0], contradictory, original.Stimuli[2] },
        });

        var execution = ScenarioRunner.Run(bundle);

        Assert.Equal("TerminalNotProven", execution.Report.RunDriveStatus);
        Assert.Equal("evidence-insufficient", execution.Report.Reason);
        Assert.Equal(1, execution.Report.EffectDeliveries);
        Assert.False(execution.Host.Facts.IsRunTerminal);
        var verification = Assert.Single(execution.Host.Facts.PostActionVerifications);
        Assert.False(verification.IsVerified);
        Assert.Equal("post-action-desired-state-not-satisfied", verification.RejectionReason);
        Assert.Equal(new[] { original.Stimuli[2].StimulusId }, execution.Report.UnconsumedStimulusIds);
    }
}
