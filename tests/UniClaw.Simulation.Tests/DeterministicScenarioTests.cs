using UniClaw.Agent.Evaluation;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// RFS-001 Phase 1 确定性场景 tracer：deterministic scenario 的语义验收。
/// 每个 bundle 只经 ScenarioRunner 的合法单次提交面（Admit→Activate→Drive），
/// 测试只读 report / KernelFacts 观察面做断言——绝不直接驱动 per-cycle
/// kernel 内部面（Driver.Activate/Drive 与 Host.DriveOnce/SubmitStimulus 是
/// host 一次性提交面，属例外；phased 场景经 Host 驱动后由
/// FinalizePhased 重算验收）。
/// </summary>
public sealed class DeterministicScenarioTests
{
    /// <summary>
    /// S1 happy path：wifi off → tap（唯一授权 Effect）→ post-action on →
    /// 终态 Completion。跑两次独立执行，逐一核对全套语义断言，
    /// 并验证是 driver（而非测试）完成了工作。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-001")]
    public void S1_OffToOn_OneAuthorizedEffect_VerifiedTerminalOutcome()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();

        foreach (var _ in new[] { 1, 2 })
        {
            var execution = ScenarioRunner.Run(bundle);
            var report = execution.Report;
            var host = execution.Host;

            Assert.True(report.AcceptancePassed,
                ScenarioReport.DescribeAcceptance(bundle, report));

            Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);

            Assert.NotNull(report.Outcome);
            Assert.Equal(TerminalClassification.Completion, report.Outcome!.Classification);
            Assert.Equal(1, report.EffectDeliveries);
            Assert.Equal(1, host.EffectDeliveryCount);
            Assert.Equal(2, report.AgentConsultations);
            Assert.Empty(report.AgentViolations);

            // agent consultation：单一 InitialPlanning 边界；decision id 为
            // run-correlated 确定格式（decision-{RunId 末 12 字符}-1）
            var call = host.ScriptedAgent.Calls[0];
            Assert.Equal(AgentDecisionPhase.InitialPlanning, call.Phase);
            Assert.StartsWith("decision-", call.DecisionId, StringComparison.Ordinal);
            Assert.EndsWith("-1", call.DecisionId, StringComparison.Ordinal);
            Assert.Equal($"decision-{host.Facts.RunId[^12..]}-1", call.DecisionId);

            // stimulus 消费顺序（context 纪律）
            Assert.Equal(new[] { "obs-1-initial", "obs-2-post" }, report.ConsumedStimulusIds);
            Assert.Empty(report.UnconsumedStimulusIds);
            Assert.Empty(report.UnexpectedStimuli);

            // Goal Evaluation（真实 UniAgent）
            Assert.NotNull(report.GoalEvaluation);
            Assert.Equal(GoalSatisfaction.Satisfied, report.GoalEvaluation!.Satisfaction);
            Assert.Equal(report.Outcome!.RunId, report.GoalEvaluation!.RunId);

            // driver 自驱证明：kernel 已 terminal，Effect receipt 由 driver 链路产出
            Assert.True(host.Facts.IsRunTerminal);
            Assert.Single(host.Facts.EffectReceipts);
        }
    }

    /// <summary>
    /// S2 零 Effect：目标已达成（switch 已 on）→ agent NoAction →
    /// 直接终态 Completion，零 Effect、单次 consultation。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-002")]
    public void S2_AlreadyOn_ZeroEffect_TerminalCompletion()
    {
        var bundle = GoldenScenarioBundles.AlreadyOnZeroEffect();
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(bundle, report));

        Assert.Equal(0, report.EffectDeliveries);
        Assert.Equal(0, host.EffectDeliveryCount);
        Assert.Equal(TerminalClassification.Completion, report.Outcome!.Classification);
        Assert.Equal(1, report.AgentConsultations); // no-action 决策也是一次真实 consultation
        Assert.Equal(GoalSatisfaction.Satisfied, report.GoalEvaluation!.Satisfaction);
        Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);
    }

    /// <summary>
    /// S3 单一 Primary Run：重复 admit/activate（Drive 前后共三次激活）全部
    /// 幂等接受且指向同一 RunId，零额外副作用、零额外 outcome。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-003")]
    public void S3_DuplicateActivation_SinglePrimaryRun()
    {
        var bundle = GoldenScenarioBundles.WifiToggleOffToOn();
        var execution = ScenarioRunner.Run(bundle, new RunOptions { DuplicateActivation = true });
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(bundle, report));

        // runner 内的重复激活：幂等接受、同一 RunId
        Assert.NotNull(report.SecondActivation);
        Assert.True(report.SecondActivation!.Accepted);
        Assert.True(report.SecondActivation.AlreadyActivated);
        Assert.Equal(report.FirstActivation.RunId, report.SecondActivation.RunId);

        // Drive 后第三次激活：仍幂等、零副作用
        var third = host.Driver.Activate();
        Assert.True(third.Accepted);
        Assert.True(third.AlreadyActivated);
        Assert.Equal(report.FirstActivation.RunId, third.RunId);

        Assert.Equal(1, host.EffectDeliveryCount);
        Assert.True(host.Facts.IsRunTerminal);

        // 恰好一个 RuntimeOutcome：第二次 Drive 幂等返回 AlreadyTerminal、不再发射
        Assert.NotNull(report.Outcome);
        Assert.Equal(RunDriveStatus.AlreadyTerminal, host.Driver.Drive().Status);
        Assert.Equal(1, host.EffectDeliveryCount);

        // Run identity 全程一致
        Assert.Equal(report.FirstActivation.RunId, host.Facts.RunId);
        Assert.Equal(report.FirstActivation.RunId, report.Outcome!.RunId);
    }

    /// <summary>
    /// S4 缺 post-action stimulus：合法等待（WaitingForInput），已授权的
    /// 一次 Effect 发生但 receipt 不是 proof——run 非终态、obligation 未满足、
    /// 无 proof 对象形成。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-004")]
    public void S4_MissingPostActionStimulus_LegalWaiting_NoSecondEffect_NoTerminalSuccess()
    {
        var bundle = GoldenScenarioBundles.MissingPostActionStimulus();
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(bundle, report));

        Assert.Equal(RunDriveStatus.WaitingForInput.ToString(), report.RunDriveStatus);
        Assert.Equal("post-action-evidence", report.Reason);

        // 唯一一次授权 Effect 已发生
        Assert.Equal(1, report.EffectDeliveries);
        Assert.Equal(1, host.EffectDeliveryCount);

        // 非终态、无 outcome
        Assert.False(host.Facts.IsRunTerminal);
        Assert.Null(host.Facts.RunHistory[^1].Outcome);
        Assert.Equal(1, report.AgentConsultations); // RUN-004：driver 自主等待

        // receipt ≠ proof：尝试证据已入账（act 计数记录了该动作），
        // 但 MaterialEffect obligation 未满足、无 proof 对象形成
        Assert.Equal(1, host.Facts.RunHistory[^1].Progress.Acts);
        Assert.Empty(host.Facts.OutcomeProofs);
    }

    /// <summary>
    /// S5（D21 rework，phased）：初始帧 → DriveOnce → WaitingForInput
    /// ("post-action-evidence") → Submit(cancel-1) + Submit(obs-2-late) →
    /// DriveOnce → cancel 经声明 SafeStop obligation → SafeStop 终态；
    /// late stimulus 保持 unconsumed、零 late effect；terminal 后再 Drive →
    /// AlreadyTerminal。验收在 FinalizePhased 之后核对。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-005")]
    public void S5_WaitingCancelThenLateStimulus_ZeroLateEffect_NoRevival()
    {
        var bundle = GoldenScenarioBundles.CancelThenLateStimulus();
        var execution = ScenarioRunner.Run(bundle, new RunOptions { Phased = true });
        var host = execution.Host;

        // Phase 1：单次 Drive → 合法等待 post-action 证据（PhasedPending）
        var pending = execution.Report;
        Assert.False(pending.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Reason, StringComparison.Ordinal);
        Assert.Equal(RunDriveStatus.WaitingForInput.ToString(), pending.RunDriveStatus);
        Assert.Equal("post-action-evidence", pending.Reason!["PhasedPending:".Length..]);
        Assert.Equal(1, pending.EffectDeliveries);
        Assert.False(host.Facts.IsRunTerminal);

        // Phase 2：cancel + late 注入（真实 feed Submit 面）
        Assert.True(host.SubmitStimulus(GoldenScenarioBundles.SafeStopCancelStimulus()));
        Assert.True(host.SubmitStimulus(GoldenScenarioBundles.LatePostActionStimulus()));

        var second = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, second.Status);
        Assert.NotNull(second.Outcome);
        // RUN-004：cancel 由 driver 自主处理（预算截断），分类 Completed
        Assert.Equal(UniClaw.Kernel.Run.TerminalClassification.SafeStop, second.Outcome!.Classification);
        Assert.True(host.Facts.IsRunTerminal);
        Assert.Equal(1, host.EffectDeliveryCount);

        // Phase 3：terminal 后再 Drive → AlreadyTerminal（late 不复活 run）
        var terminal = host.DriveOnce();
        Assert.Equal(RunDriveStatus.AlreadyTerminal, terminal.Status);
        Assert.Equal(1, host.EffectDeliveryCount);

        // 验收在最终 phase 后重算
        var report = ScenarioRunner.FinalizePhased(bundle, host);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));
        Assert.Equal(RunDriveStatus.Completed.ToString(), report.RunDriveStatus);
        Assert.Equal(1, report.EffectDeliveries);
        Assert.Empty(report.AgentViolations);

        Assert.Equal(new[] { "obs-2-late" }, report.UnconsumedStimulusIds);
        Assert.Equal(2, report.ConsumedStimulusIds.Count);
        Assert.Equal("obs-1-initial", report.ConsumedStimulusIds[0]);
        Assert.Equal("cancel-1", report.ConsumedStimulusIds[1]);

        Assert.Equal(GoalSatisfaction.Unsatisfied, report.GoalEvaluation!.Satisfaction);
    }
}
