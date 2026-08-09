using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B9 共享 Agent 组合 harness（核心交付）：变体名 → 全接线（ScriptedEnvironment + Startup + Traversal +
/// container factory + Agent + Goal + Plan + runId）。
/// 10 个 Scenario 变体共享同一 Runtime slice（裁决 7）；差异仅为变体数据（B3 工厂——本 harness 不复制变体数据）、
/// Plan（ScenarioPlans）、Goal（ScenarioGoals）。
/// 测试模式：Create(variant) → harness.RunAsync() → 断言 harness.Agent.Trace / State / Reason / RecoveryAnchor
/// 与 harness.Environment.ActionHistory / harness.Evidence / harness.Traversal.Journal。
/// C4（SC-P2-001）：launcher-drift 变体全接线恢复 —— Startup 注入 RestoreRecipe / EntryStrategy 到
/// RecoveryAnchor；Recovery 组件真实接线（配方解析 / 位置恢复 / 验证判据，均为本 harness 内置注入数据）。
/// C5（SC-P2-002）：flicker-target 变体 + maxRetries 参数（Traversal Step-scope retry；无恢复接线）。
/// C6（SC-P2-003）：unrecoverable 变体 — 与 launcher-drift 相同的恢复接线，验证判据因变体数据失败。
/// SC-P3-001：uncertain-action-effect-applied / absent 共用单步 Plan 与 Observation-driven Goal evaluator。
/// </summary>
/// <param name="VariantName">变体名（happy | startup-fg-fail | switch-stuck | missing-target | same-text |
/// launcher-drift | flicker-target | unrecoverable | uncertain-action-effect-applied | uncertain-action-effect-absent）。</param>
/// <param name="Environment">B3 ScriptedEnvironment 实例（ActionHistory 观察面）。</param>
/// <param name="Agent">已装配的 Runtime Agent（Trace / State / Reason / RecoveryAnchor 观察面）。</param>
/// <param name="Traversal">B6 Traversal 实例（Journal 观察面 — retry 条目 / RetryCount 断言）。</param>
/// <param name="Goal">该变体适用的 Goal（evaluator 记录写入 Evidence）。</param>
/// <param name="Plan">该变体适用的 Plan。</param>
/// <param name="RunId">确定性 runId（重放约定 — SC-P1-001 断言 7）。</param>
/// <param name="Evidence">Goal evaluator 的评估序列（断言可观察面）。</param>
public sealed record ScenarioHarness(
    string VariantName,
    ScriptedEnvironment Environment,
    RuntimeAgent Agent,
    RuntimeTraversal Traversal,
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
    /// <param name="variant">变体名：happy | startup-fg-fail | switch-stuck | missing-target | same-text |
    /// launcher-drift | flicker-target | unrecoverable | uncertain-action-effect-applied | uncertain-action-effect-absent。</param>
    /// <param name="maxRetries">Traversal Step-scope Select 失败重试上限（B4 / SC-P2-002；默认 0 = Phase 1 行为）。</param>
    /// <param name="parseRestoreRecipe">恢复配方解析注入（C4：覆盖 B3 惰性接线；恢复场景变体固定使用本 harness 内置解析器）。</param>
    /// <param name="resolveRecoveryAction">位置恢复动作解析注入（C4：同上）。</param>
    /// <param name="verifyCriteria">恢复验证判据注入（C4：同上）。</param>
    /// <returns>新实例（每次调用全新 wiring — fake 是单次 run 状态 owner）。</returns>
    /// <exception cref="ArgumentOutOfRangeException">未知变体名。</exception>
    public static ScenarioHarness Create(
        string variant,
        int maxRetries = 0,
        Func<string, ImmutableArray<DeviceAction>>? parseRestoreRecipe = null,
        Func<PlanStep, Observation, DeviceAction?>? resolveRecoveryAction = null,
        Func<Observation, string, bool>? verifyCriteria = null)
    {
        var (environment, plan) = variant switch
        {
            "happy" => (ScriptedEnvironmentVariants.Happy(), ScenarioPlans.WifiEnableSequence()),
            "switch-stuck" => (ScriptedEnvironmentVariants.SwitchStuck(), ScenarioPlans.WifiEnableSequence()),
            "same-text" => (ScriptedEnvironmentVariants.SameText(), ScenarioPlans.WifiEnableSequence()),
            "missing-target" => (ScriptedEnvironmentVariants.MissingTarget(), ScenarioPlans.WifiNavigationOnly()),
            "startup-fg-fail" => (ScriptedEnvironmentVariants.StartupForegroundFail(), ScenarioPlans.Empty()),
            "launcher-drift" => (ScriptedEnvironmentVariants.LauncherDrift(), ScenarioPlans.WifiEnableSequence()),
            "flicker-target" => (ScriptedEnvironmentVariants.FlickerTarget(), ScenarioPlans.WifiEnableSequence()),
            "unrecoverable" => (ScriptedEnvironmentVariants.Unrecoverable(), ScenarioPlans.WifiEnableSequence()),
            "uncertain-action-effect-applied" => (ScriptedEnvironmentVariants.UncertainActionEffectApplied(), ScenarioPlans.UncertainNetworkTransition()),
            "uncertain-action-effect-absent" => (ScriptedEnvironmentVariants.UncertainActionEffectAbsent(), ScenarioPlans.UncertainNetworkTransition()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant), variant,
                "未知变体名：happy | startup-fg-fail | switch-stuck | missing-target | same-text | launcher-drift | flicker-target | unrecoverable | uncertain-action-effect-applied | uncertain-action-effect-absent"),
        };

        var isRecoveryScenario = variant is "launcher-drift" or "unrecoverable";
        // C4/C6 — SC-P2-001/003：恢复场景变体的 Startup 把恢复规划数据注入 RecoveryAnchor（§20 数据源 — 裁决 8）；
        // 其余变体保持 null（Phase 1 行为，锚点 3 字段向后兼容）
        var startup = new RuntimeStartup(
            environment,
            TargetApplication,
            ScenarioIdentity.ResolveSemanticPage,
            restoreRecipe: isRecoveryScenario ? RecoveryScenarioRestoreRecipe : null,
            entryStrategy: isRecoveryScenario ? RecoveryScenarioEntryStrategy : null);
        var traversal = new RuntimeTraversal(environment, maxRetries);
        var evidence = new List<GoalEvidence>();
        // startup-fg-fail：Startup 失败 → evaluator 不可达；SC-P3-001 只从目标页 Observation 取证；
        // 其余变体保持记录式开关证据 Goal。
        var goal = variant switch
        {
            "startup-fg-fail" => ScenarioGoals.Minimal(),
            "uncertain-action-effect-applied" or "uncertain-action-effect-absent" => ScenarioGoals.ReachNetworkSettings(evidence),
            _ => ScenarioGoals.EnableWifi(evidence),
        };
        // B3 惰性接线：本 harness 场景不触发 drift（前台恒为目标应用）→ 空配方 / 无位置恢复 / 验证恒真；
        // 注入参数可覆盖（C5+ 消费）。C4/C6 — SC-P2-001/003：恢复场景变体固定使用内置真实恢复接线
        // （unrecoverable 的验证失败由变体数据驱动 — 诚实判据，非接线差异）
        var recovery = isRecoveryScenario
            ? new RuntimeRecovery(
                environment,
                RecoveryScenarioParseRestoreRecipe,
                RecoveryScenarioResolveRecoveryAction,
                RecoveryScenarioVerifyCriteria)
            : new RuntimeRecovery(
                environment,
                parseRestoreRecipe ?? (_ => []),
                resolveRecoveryAction ?? ((_, _) => null),
                verifyCriteria ?? ((_, _) => true));
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            ScenarioIdentity.ResolveSemanticPage,
            ScenarioIdentity.ContainerFactory(traversal),
            recovery);
        return new ScenarioHarness(variant, environment, agent, traversal, goal, plan, DefaultRunId, evidence);
    }

    // ── C4/C6 — SC-P2-001/003 恢复场景接线（测试注入数据 — 裁决 8/11：生产 Runtime 不硬编码场景字符串）──

    /// <summary>SC-P2-001/003 恢复配方：Relaunch(Settings) → [LaunchApp(Settings)]（恢复入口 = 启动锚点）。</summary>
    private const string RecoveryScenarioRestoreRecipe = "Relaunch(Settings)";

    /// <summary>SC-P2-001/003 入口策略：恢复到 SettingsMain（RecoveryAnchor.ExpectedSemanticEntry 入口语义页面）。</summary>
    private const string RecoveryScenarioEntryStrategy = "Resolve(SettingsMain)";

    /// <summary>配方解析（组件机制注入）：仅消费恢复场景的配方；未知配方显式失败（测试环境 fail loud）。</summary>
    private static ImmutableArray<DeviceAction> RecoveryScenarioParseRestoreRecipe(string recipe)
    {
        if (string.Equals(recipe, RecoveryScenarioRestoreRecipe, StringComparison.Ordinal))
        {
            return ImmutableArray.Create<DeviceAction>(new DeviceAction.LaunchApp(TargetApplication));
        }
        throw new ArgumentException($"未知恢复配方：{recipe}", nameof(recipe));
    }

    /// <summary>位置恢复动作解析（组件机制注入）：PlanStep 目标文本 → 当前观测中同文本元素的 Tap（Index 引用）。</summary>
    private static DeviceAction? RecoveryScenarioResolveRecoveryAction(PlanStep step, Observation observation)
    {
        foreach (var element in observation.Elements)
        {
            if (string.Equals(element.Text, step.TargetDescription, StringComparison.Ordinal))
            {
                return new DeviceAction.Tap(element.Index);
            }
        }
        return null;
    }

    /// <summary>恢复验证判据（组件机制注入 — 判据被语义消费）：判据文本与锚点一致 + 前台回到目标应用 +
    /// 观测含入口元素（SettingsMain 证据）。unrecoverable 变体的验证失败由此判据对照变体观测（Launcher）产生。</summary>
    private static bool RecoveryScenarioVerifyCriteria(Observation observation, string verificationCriteria)
    {
        if (!string.Equals(verificationCriteria, $"ForegroundApplication == {TargetApplication}", StringComparison.Ordinal))
        {
            return false;
        }
        if (!string.Equals(observation.ForegroundApplication, TargetApplication, StringComparison.Ordinal))
        {
            return false;
        }
        foreach (var element in observation.Elements)
        {
            if (string.Equals(element.Text, "Network & Internet", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
