using System.Diagnostics;
using UniClaw.Agent;
using UniClaw.Agent.Evaluation;
using UniClaw.Agent.Goal;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Outcome;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Simulation.Tests;

/// <summary>一次场景执行产物：结构化 report + 已组合 host（后续断言面）。</summary>
internal sealed record ScenarioExecution(ScenarioReport Report, SimulationHost Host);

/// <summary>
/// RFS-001 Scenario Runner：每场景恰好一次 submission——
/// AdmitContract(1) → Activate(1) → Drive(1)。绝不直接调用 kernel.Process /
/// SelectIntent / Act / EvaluateTerminal（driver 内部面）；只读 owner records
/// 聚合 report。S3 重复激活（DuplicateActivation）在 Drive 前做一次幂等
/// re-admit + re-activate（断言同一 RunId）。D21 phased（RunOptions.Phased）：
/// 单次 Drive 后返回 PhasedPending report（AcceptancePassed=false），测试经
/// Host.DriveOnce/Host.SubmitStimulus 完成各 phase，再调 FinalizePhased 重算。
/// D19 纪律：run terminal 后标记 ScriptedAgent（late-call 建模）；
/// 纪律违规聚合进 AgentViolations 并强制 AcceptancePassed=false。
/// </summary>
internal static class ScenarioRunner
{
    public static ScenarioExecution Run(MinimalScenarioBundle bundle, RunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        options ??= new RunOptions();
        var host = SimulationHost.Compose(bundle, options);

        var stopwatch = Stopwatch.StartNew();

        var admission = host.KernelCore.AdmitContract(bundle.Contract);
        if (!admission.Accepted)
            throw new InvalidOperationException(
                "contract admission 被拒: " + string.Join(", ",
                    admission.Checks.Where(c => !c.Passed).Select(c => c.Name)));

        var firstActivation = host.Driver.Activate();
        if (!firstActivation.Accepted)
            throw new InvalidOperationException(
                "activation 被拒: " + firstActivation.Reason);
        host.FirstActivation = firstActivation;

        ActivationResult? secondActivation = null;
        if (options.DuplicateActivation)
        {
            var reAdmission = host.KernelCore.AdmitContract(bundle.Contract);
            if (!reAdmission.Accepted)
                throw new InvalidOperationException("duplicate re-admission 被拒（应幂等接受）");
            secondActivation = host.Driver.Activate();
            if (!secondActivation.Accepted)
                throw new InvalidOperationException("duplicate re-activation 被拒（应幂等接受）");
            if (secondActivation.RunId != firstActivation.RunId)
                throw new InvalidOperationException(
                    $"重复激活铸造了不同 Run: {firstActivation.RunId} vs {secondActivation.RunId}");
            host.SecondActivation = secondActivation;
        }

        var result = host.DriveOnce();
        stopwatch.Stop();

        // D19：terminal 后标记（late-call 建模）+ 纪律聚合
        var discipline = host.ScriptedAgent.CheckDiscipline(bundle.Expected.ExpectedAgentConsultations);

        if (options.Phased)
        {
            var pending = BuildReport(bundle, host, result, stopwatch, discipline,
                reasonOverride: "PhasedPending:" + result.Reason, acceptanceOverride: false);
            return new ScenarioExecution(pending, host);
        }

        var report = BuildReport(bundle, host, result, stopwatch, discipline,
            reasonOverride: null, acceptanceOverride: null);
        return new ScenarioExecution(report, host);
    }

    /// <summary>
    /// D21：phased 场景在测试完成全部 phase（Host.DriveOnce/SubmitStimulus）
    /// 之后调用——从 host 状态重算完整 ScenarioReport（status 取 host 记录的
    /// 最后一次 Drive 结果；digest/metrics/验收全部重导出）。不再驱动 Drive。
    /// </summary>
    public static ScenarioReport FinalizePhased(MinimalScenarioBundle bundle, SimulationHost host)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(host);
        // 终态语义取最后一次携带 Outcome 的 Drive（terminal 后的
        // AlreadyTerminal 幂等探针不覆盖终态事实；无 Outcome 时取最后一次）
        var last = host.RecordedDriveResults.LastOrDefault(r => r.Outcome is not null)
            ?? host.RecordedDriveResults.LastOrDefault()
            ?? throw new InvalidOperationException("phased 场景尚未发生任何 Drive");
        var discipline = host.ScriptedAgent.CheckDiscipline(bundle.Expected.ExpectedAgentConsultations);
        return BuildReport(bundle, host, last, Stopwatch.StartNew(), discipline,
            reasonOverride: null, acceptanceOverride: null);
    }

    private static ScenarioReport BuildReport(
        MinimalScenarioBundle bundle, SimulationHost host, RunDriveResult result,
        Stopwatch stopwatch, IReadOnlyList<string> discipline,
        string? reasonOverride, bool? acceptanceOverride)
    {
        var evaluation = result.Outcome is not null
            ? new UniAgent(BuildGoal(bundle.Goal)).Evaluate(result.Outcome)
            : null;

        var digest = SemanticDigest.Of(host, host.ScriptedAgent, host.Feed, result, evaluation);

        var metrics = new ScenarioMetricsSnapshot(
            CriticalPathLatencyMs: Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3, MidpointRounding.AwayFromZero),
            Observations: host.Metrics.AdmissionsAccepted,
            AdmissionsRejected: host.Metrics.AdmissionsRejected,
            ReconciliationsNew: host.Metrics.ReconciliationsNew,
            ReconciliationsIdempotent: host.Metrics.ReconciliationsIdempotent,
            Regrounds: host.Metrics.Stages.TryGetValue(RuntimeStage.CurrentGrounding, out var stage)
                ? stage.Invocations
                : 0,
            VerificationMode: "material-effect:post-action-observation-gate;freshness:scripted-sufficient",
            ModelCalls: "N/A (ScriptedUniAgent; no live model)",
            InputTokens: "N/A (ScriptedUniAgent; no live model)");

        var effectDeliveries = host.EffectDeliveryCount;
        var agentConsultations = host.ScriptedAgent.Calls.Count;
        var unconsumed = host.Feed.Remaining;

        var violations = host.ScriptedAgent.Violations.Concat(discipline).ToList();
        var acceptance = acceptanceOverride
            ?? (violations.Count == 0
                && AcceptancePassed(bundle, result, evaluation, effectDeliveries, agentConsultations, unconsumed.Count));

        return new ScenarioReport(
            ScenarioId: bundle.ScenarioId,
            RunDriveStatus: result.Status.ToString(),
            Reason: reasonOverride ?? result.Reason,
            Outcome: result.Outcome,
            GoalEvaluation: evaluation,
            EffectDeliveries: effectDeliveries,
            AgentConsultations: agentConsultations,
            AgentViolations: violations,
            ConsumedStimulusIds: host.Feed.Consumed.ToList(),
            UnconsumedStimulusIds: unconsumed.ToList(),
            UnexpectedStimuli: host.Feed.Unexpected.ToList(),
            SemanticDigest: digest,
            Metrics: metrics,
            FirstActivation: host.FirstActivation,
            SecondActivation: host.SecondActivation,
            AcceptancePassed: acceptance);
    }

    /// <summary>GoalSpec → 真实 PrimaryGoal（required 空 = authoring 失败，fail closed）。</summary>
    private static PrimaryGoal BuildGoal(GoalSpec goal)
    {
        var required = new List<GoalCriterion>();
        if (goal.RequiredClassification is { } classification)
            required.Add(new GoalCriterion.ClassificationIs(
                Enum.Parse<TerminalClassification>(classification, ignoreCase: false)));
        foreach (var obligationId in goal.RequiredObligationIds)
            required.Add(new GoalCriterion.ObligationFulfilled(obligationId));
        if (required.Count == 0)
            throw new InvalidOperationException(
                $"vacuous goal spec: {goal.GoalId}（required criteria 为空）");
        return new PrimaryGoal(goal.GoalId, goal.Statement, required, Array.Empty<GoalCriterion>());
    }

    private static bool AcceptancePassed(
        MinimalScenarioBundle bundle, RunDriveResult result, GoalEvaluation? evaluation,
        int effectDeliveries, int agentConsultations, int unconsumedCount)
    {
        var expected = bundle.Expected;
        var classification = result.Outcome?.Classification.ToString();
        var goalSatisfaction = evaluation?.Satisfaction.ToString();
        return result.Status.ToString() == expected.ExpectedStatus
            && classification == expected.ExpectedClassification
            && effectDeliveries == expected.ExpectedEffects
            && agentConsultations == expected.ExpectedAgentConsultations
            && unconsumedCount == expected.ExpectedUnconsumedStimuli
            && goalSatisfaction == expected.ExpectedGoalSatisfaction;
    }
}
