using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 D22 — 两步串行 scenario（不变量 43 serialization barrier）：
/// proposal 携带有界有序 steps，每步独立完整链，下一步 dispatch 只能在
/// 上一步 post-action 证据被消费并 reconcile 之后发生。
///
/// SIM-003 G8：bundle 经 ScenarioLibrary.Load(scenarioId) 解析；BARRIER-003
/// 的 contradictory-post 变换升格为注册 carrier（wifi-two-step-contradictory-
/// post），期望由各自 certified JSON 投影。golden 值断言由 AcceptancePassed
/// 承载；保留断言均为屏障/证据/owner 投影维度的架构不变量（Type-B）。
/// </summary>
public sealed class TwoStepBarrierTests
{
    /// <summary>
    /// 两步 happy path（不变量 43）：step1 tap toggle（DesiredState "true"）
    /// → 等待并消费 obs-2-post（barrier 通过）→ step2 tap menuItem
    /// （DesiredState null）→ 消费 obs-3-post → terminal Completion。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-BARRIER-001")]
    public void TwoStep_EvidenceBetweenSteps_SecondEffectAllowed_TerminalCompletion()
    {
        var bundle = ScenarioLibrary.Load("SCN-BARRIER-001").Bundle;
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));

        // Type-B：barrier 证据链——两次 effect 各有一条 receipt（owner 投影
        // 事实，独立于 report 计数面）
        Assert.Equal(report.EffectDeliveries, host.EffectDeliveryCount);
        Assert.Equal(report.EffectDeliveries, host.Facts.EffectReceipts.Count);
        Assert.True(host.Facts.IsRunTerminal);
        Assert.Empty(report.AgentViolations);

        // Type-B：stimulus 消费顺序（不变量 43 的可观察面：obs-2-post 必须
        // 在 step2 之前被消费）
        Assert.Equal(new[] { "obs-1-initial", "obs-2-post", "obs-3-post" }, report.ConsumedStimulusIds);
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
        var scenario = ScenarioLibrary.Load("SCN-BARRIER-002");
        var bundle = scenario.Bundle;
        var execution = ScenarioRunner.Run(bundle, scenario.Options);
        var host = execution.Host;

        // Type-B：phased 协议标记与等待原因（协议面，非 golden 值复写）
        var pending = execution.Report;
        Assert.False(pending.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Reason, StringComparison.Ordinal);
        Assert.Equal("post-action-evidence", pending.Reason!["PhasedPending:".Length..]);

        // Type-B：不变量 43——step1 已 dispatch，step2 因证据缺失被阻塞
        // （屏障事实由 owner 投影证明，非 report 计数复写）
        Assert.Equal(1, host.EffectDeliveryCount);
        Assert.Equal(1, host.Facts.EffectReceipts.Count);
        Assert.False(host.Facts.IsRunTerminal);

        // Type-B：cancel 经声明的 SafeStop obligation → 终态（协议路径）
        Assert.True(host.SubmitStimulus(GoldenScenarioBundles.SafeStopCancelStimulus()));
        var cancelled = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, cancelled.Status);
        Assert.Equal(TerminalClassification.SafeStop, cancelled.Outcome!.Classification);
        Assert.True(host.Facts.IsRunTerminal);
        Assert.Equal(1, host.EffectDeliveryCount);

        // 终态验收经 certified 期望核对（FinalizePhased 重算）
        var report = ScenarioRunner.FinalizePhased(bundle, host);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));
        Assert.Empty(report.AgentViolations);
    }

    /// <summary>
    /// post-action frame 存在不等于上一步已验证：若 toggle 仍是 false，
    /// Assurance verification 必须 fail closed，不得仅凭 context 正确就放行
    /// menuItem 的第二次 Effect。SIM-003 G6：经注册 carrier
    /// （wifi-two-step-contradictory-post）+ certified 期望承载。
    ///
    /// 注意：本场景不整体断言 AcceptancePassed——run 在 TerminalNotProven
    /// 结束时脚本 step2 未被消费，ScenarioRunner 会把该纪律事实聚合进
    /// acceptance 拒绝（设计内行为：屏障阻断了第二次 effect）。certified
    /// 六字段经对 bundle.Expected 的引用断言逐项核对（值来自投影，
    /// 非第三真源）；纪律事实属 Type-B。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-BARRIER-003")]
    public void TwoStep_PostActionContradictsDesiredState_SecondEffectBlocked()
    {
        var bundle = ScenarioLibrary.Load("SCN-BARRIER-003").Bundle;
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;

        // certified 期望逐字段核对（引用投影值，非字面量复写）
        Assert.Equal(bundle.Expected.ExpectedStatus, report.RunDriveStatus);
        Assert.Equal(bundle.Expected.ExpectedClassification, report.Outcome?.Classification.ToString());
        Assert.Equal(bundle.Expected.ExpectedEffects, report.EffectDeliveries);
        Assert.Equal(bundle.Expected.ExpectedAgentConsultations, report.AgentConsultations);
        Assert.Equal(bundle.Expected.ExpectedUnconsumedStimuli, report.UnconsumedStimulusIds.Count);
        Assert.Equal(bundle.Expected.ExpectedGoalSatisfaction, report.GoalEvaluation?.Satisfaction.ToString());

        // Type-B：Assurance fail-closed 证据——verification 拒绝原因与
        // 未消费身份（协议/身份维度，非 golden 计数复写）
        Assert.Equal("evidence-insufficient", report.Reason);
        Assert.False(execution.Host.Facts.IsRunTerminal);
        var verification = Assert.Single(execution.Host.Facts.PostActionVerifications);
        Assert.False(verification.IsVerified);
        Assert.Equal("post-action-desired-state-not-satisfied", verification.RejectionReason);
        Assert.Equal(new[] { "obs-3-post" }, report.UnconsumedStimulusIds);
    }
}
