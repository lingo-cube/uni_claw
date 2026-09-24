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
///
/// SIM-003 G8：bundle 一律经 ScenarioLibrary.Load(scenarioId) 解析——
/// certified JSON（execution 绑定 + 期望投影）驱动构造。golden 期望值
/// （六字段）由 AcceptancePassed 承载，不再以字面量复述（Type-A 清除）；
/// 保留的断言均为期望模型之外的架构不变量（Type-B：协议格式/顺序/身份/
/// host 侧事实）。
/// </summary>
public sealed class DeterministicScenarioTests
{
    /// <summary>
    /// S1 happy path：wifi off → tap（唯一授权 Effect）→ post-action on →
    /// 终态 Completion。跑两次独立执行，验收经 certified 期望核对，
    /// 并验证是 driver（而非测试）完成了工作。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-001")]
    public void S1_OffToOn_OneAuthorizedEffect_VerifiedTerminalOutcome()
    {
        var bundle = ScenarioLibrary.Load("SCN-WIFI-001").Bundle;

        foreach (var _ in new[] { 1, 2 })
        {
            var execution = ScenarioRunner.Run(bundle);
            var report = execution.Report;
            var host = execution.Host;

            Assert.True(report.AcceptancePassed,
                ScenarioReport.DescribeAcceptance(bundle, report));

            // Type-B：agent consultation 决策 id 为 run 关联确定格式
            // （decision-{RunId 末 12 字符}-1——协议不变量，非 golden 值复写）
            var call = host.ScriptedAgent.Calls[0];
            Assert.Equal(AgentDecisionPhase.InitialPlanning, call.Phase);
            Assert.StartsWith("decision-", call.DecisionId, StringComparison.Ordinal);
            Assert.EndsWith("-1", call.DecisionId, StringComparison.Ordinal);
            Assert.Equal($"decision-{host.Facts.RunId[^12..]}-1", call.DecisionId);

            // Type-B：stimulus 消费顺序与身份（期望模型只约束计数）
            Assert.Equal(new[] { "obs-1-initial", "obs-2-post" }, report.ConsumedStimulusIds);
            Assert.Empty(report.UnexpectedStimuli);
            Assert.Empty(report.AgentViolations);

            // Type-B：goal evaluation 与 outcome 同 run（run 关联不变量）
            Assert.NotNull(report.GoalEvaluation);
            Assert.Equal(report.Outcome!.RunId, report.GoalEvaluation!.RunId);

            // Type-B：driver 自驱证明——kernel 已 terminal，Effect receipt
            // 由 driver 链路产出（owner 投影事实，独立于 report 面）
            Assert.True(host.Facts.IsRunTerminal);
            Assert.Single(host.Facts.EffectReceipts);
            Assert.Equal(report.EffectDeliveries, host.EffectDeliveryCount);
        }
    }

    /// <summary>
    /// S2 零 Effect：目标已达成（switch 已 on）→ agent NoAction →
    /// 直接终态 Completion，零 Effect、单次 consultation。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-002")]
    public void S2_AlreadyOn_ZeroEffect_TerminalCompletion()
    {
        var bundle = ScenarioLibrary.Load("SCN-WIFI-002").Bundle;
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(bundle, report));

        // Type-B：host 侧计数与 report 面一致（owner 投影事实）
        Assert.Equal(report.EffectDeliveries, host.EffectDeliveryCount);
        Assert.True(host.Facts.IsRunTerminal);
    }

    /// <summary>
    /// S3 单一 Primary Run：重复 admit/activate（Drive 前后共三次激活）全部
    /// 幂等接受且指向同一 RunId，零额外副作用、零额外 outcome。
    /// 期望由 SCN-WIFI-003 certified JSON 投影（duplicateActivation 经
    /// execution.options 声明）。
    /// </summary>
    [Fact, Trait("Scenario", "SCN-WIFI-003")]
    public void S3_DuplicateActivation_SinglePrimaryRun()
    {
        var scenario = ScenarioLibrary.Load("SCN-WIFI-003");
        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(scenario.Bundle, report));

        // Type-B：幂等探针——runner 内的重复激活幂等接受、同一 RunId
        Assert.NotNull(report.SecondActivation);
        Assert.True(report.SecondActivation!.Accepted);
        Assert.True(report.SecondActivation.AlreadyActivated);
        Assert.Equal(report.FirstActivation.RunId, report.SecondActivation.RunId);

        // Type-B：Drive 后第三次激活仍幂等、零副作用
        var third = host.Driver.Activate();
        Assert.True(third.Accepted);
        Assert.True(third.AlreadyActivated);
        Assert.Equal(report.FirstActivation.RunId, third.RunId);
        Assert.True(host.Facts.IsRunTerminal);

        // Type-B：terminal 后再 Drive → AlreadyTerminal 幂等返回，不再发射
        // （SCN-WIFI-003 认证迁移后，此事实由本断言承载，非 JSON status）
        Assert.Equal(RunDriveStatus.AlreadyTerminal, host.Driver.Drive().Status);

        // Type-B：Run identity 全程一致
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
        var bundle = ScenarioLibrary.Load("SCN-WIFI-004").Bundle;
        var execution = ScenarioRunner.Run(bundle);
        var report = execution.Report;
        var host = execution.Host;

        Assert.True(report.AcceptancePassed,
            ScenarioReport.DescribeAcceptance(bundle, report));

        // Type-B：等待原因（协议 Reason，不在期望六字段模型内）
        Assert.Equal("post-action-evidence", report.Reason);

        // Type-B：非终态、无 outcome、receipt ≠ proof（owner 投影事实）
        Assert.False(host.Facts.IsRunTerminal);
        Assert.Null(host.Facts.RunHistory[^1].Outcome);
        Assert.Equal(1, host.Facts.RunHistory[^1].Progress.Acts);
        Assert.Empty(host.Facts.OutcomeProofs);
        Assert.Equal(report.EffectDeliveries, host.EffectDeliveryCount);
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
        var scenario = ScenarioLibrary.Load("SCN-WIFI-005");
        var bundle = scenario.Bundle;
        var execution = ScenarioRunner.Run(bundle, scenario.Options);
        var host = execution.Host;

        // Phase 1：单次 Drive → 合法等待 post-action 证据（PhasedPending——
        // Type-B：phased 协议标记与等待原因）
        var pending = execution.Report;
        Assert.False(pending.AcceptancePassed);
        Assert.StartsWith("PhasedPending:", pending.Reason, StringComparison.Ordinal);
        Assert.Equal("post-action-evidence", pending.Reason!["PhasedPending:".Length..]);
        Assert.False(host.Facts.IsRunTerminal);

        // Phase 2：cancel + late 注入（真实 feed Submit 面）
        Assert.True(host.SubmitStimulus(GoldenScenarioBundles.SafeStopCancelStimulus()));
        Assert.True(host.SubmitStimulus(GoldenScenarioBundles.LatePostActionStimulus()));

        var second = host.DriveOnce();
        Assert.Equal(RunDriveStatus.Completed, second.Status);
        Assert.NotNull(second.Outcome);
        // Type-B：cancel 由 driver 自主处理（RUN-004 协议——分类事实在
        // certified 期望中钉扎，此处断言协议路径本身）
        Assert.True(host.Facts.IsRunTerminal);

        // Phase 3：terminal 后再 Drive → AlreadyTerminal（late 不复活 run）
        var terminal = host.DriveOnce();
        Assert.Equal(RunDriveStatus.AlreadyTerminal, terminal.Status);

        // 验收在最终 phase 后重算（certified 期望核对）
        var report = ScenarioRunner.FinalizePhased(bundle, host);
        Assert.True(report.AcceptancePassed, ScenarioReport.DescribeAcceptance(bundle, report));
        Assert.Empty(report.AgentViolations);

        // Type-B：消费顺序与未消费身份（期望模型只约束计数）
        Assert.Equal(new[] { "obs-2-late" }, report.UnconsumedStimulusIds);
        Assert.Equal(2, report.ConsumedStimulusIds.Count);
        Assert.Equal("obs-1-initial", report.ConsumedStimulusIds[0]);
        Assert.Equal("cancel-1", report.ConsumedStimulusIds[1]);
    }
}
