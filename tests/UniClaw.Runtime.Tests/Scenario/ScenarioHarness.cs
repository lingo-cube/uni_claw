using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B9 共享 Agent 组合 harness（核心交付）：变体名 → 全接线（ScriptedEnvironment + Startup + Traversal +
/// container factory + Agent + Goal + Plan + runId）。
/// 5 个 Scenario 共享同一 Runtime slice（裁决 7）；差异仅为变体数据（B3 工厂——本 harness 不复制变体数据）、
/// Plan（ScenarioPlans）、Goal（ScenarioGoals）。
/// 测试模式：Create(variant) → harness.RunAsync() → 断言 harness.Agent.Trace / State / Reason / RecoveryAnchor
/// 与 harness.Environment.ActionHistory / harness.Evidence。
/// </summary>
/// <param name="VariantName">变体名（happy | startup-fg-fail | switch-stuck | missing-target | same-text）。</param>
/// <param name="Environment">B3 ScriptedEnvironment 实例（ActionHistory 观察面）。</param>
/// <param name="Agent">已装配的 Runtime Agent（Trace / State / Reason / RecoveryAnchor 观察面）。</param>
/// <param name="Goal">该变体适用的 Goal（evaluator 记录写入 Evidence）。</param>
/// <param name="Plan">该变体适用的 Plan。</param>
/// <param name="RunId">确定性 runId（重放约定 — SC-P1-001 断言 7）。</param>
/// <param name="Evidence">Goal evaluator 的评估序列（断言可观察面）。</param>
public sealed record ScenarioHarness(
    string VariantName,
    ScriptedEnvironment Environment,
    RuntimeAgent Agent,
    Goal Goal,
    Plan Plan,
    string RunId,
    List<GoalEvidence> Evidence)
{
    /// <summary>执行一次 Run（使用本 harness 的 Goal / Plan / RunId）。</summary>
    /// <param name="cancellationToken">取消信号。</param>
    /// <returns>最终 RunState（Completed | Failed）。</returns>
    public Task<RunState> RunAsync(CancellationToken cancellationToken = default)
        => Agent.RunAsync(Goal, Plan, RunId, cancellationToken);

    /// <summary>确定性 runId 约定（同 runId 重放 → 相同 Trace — SC-P1-001 断言 7）。</summary>
    public const string DefaultRunId = "run-1";

    /// <summary>目标应用标识（Startup LaunchApp 与 ForegroundApplication 验证的期望值）。</summary>
    public const string TargetApplication = "Settings";

    /// <summary>变体名 → 全接线 harness（B3 变体工厂是唯一数据源 — 不复制变体数据）。</summary>
    /// <param name="variant">变体名：happy | startup-fg-fail | switch-stuck | missing-target | same-text。</param>
    /// <returns>新实例（每次调用全新 wiring — fake 是单次 run 状态 owner）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">未知变体名。</exception>
    public static ScenarioHarness Create(string variant)
    {
        var (environment, plan) = variant switch
        {
            "happy" => (ScriptedEnvironmentVariants.Happy(), ScenarioPlans.WifiEnableSequence()),
            "switch-stuck" => (ScriptedEnvironmentVariants.SwitchStuck(), ScenarioPlans.WifiEnableSequence()),
            "same-text" => (ScriptedEnvironmentVariants.SameText(), ScenarioPlans.WifiEnableSequence()),
            "missing-target" => (ScriptedEnvironmentVariants.MissingTarget(), ScenarioPlans.WifiNavigationOnly()),
            "startup-fg-fail" => (ScriptedEnvironmentVariants.StartupForegroundFail(), ScenarioPlans.Empty()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant), variant, "未知变体名：happy | startup-fg-fail | switch-stuck | missing-target | same-text"),
        };

        var startup = new RuntimeStartup(environment, TargetApplication, ScenarioIdentity.ResolveSemanticPage);
        var traversal = new RuntimeTraversal(environment);
        var evidence = new List<GoalEvidence>();
        // startup-fg-fail：Startup 失败 → evaluator 不可达，用最小 Goal；其余变体用记录式开关证据 Goal
        var goal = variant == "startup-fg-fail"
            ? ScenarioGoals.Minimal()
            : ScenarioGoals.EnableWifi(evidence);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            ScenarioIdentity.ResolveSemanticPage,
            ScenarioIdentity.ContainerFactory(traversal));
        return new ScenarioHarness(variant, environment, agent, goal, plan, DefaultRunId, evidence);
    }
}
