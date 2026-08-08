using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// B1 Agent-scope drift 判定单元测试（HG-3：无 DriftStatus 字段 — 纯函数，仅用既有表面）。
/// 直接构造 Observation / Container / WorldBelief（Phase 2C — 不用 ScriptedEnvironment）；
/// 通过反射调用私有方法 IsAgentScopeDrift（Agent 经一次空 Plan 的合法 Run 建立 RecoveryAnchor 基线）。
/// 判定语义：前台 ≠ RecoveryAnchor.ApplicationIdentity 且 !IsStillMine 且 SemanticPage == null
/// ——三条件同时成立才是 Agent-scope drift（不误伤正常导航 / 仅前台切换 / 仍属本页）。
/// </summary>
public class DriftDetectionTests
{
    private const string BaselineApplication = "Settings";

    // ── 测试 1：drift 命中（Launcher 前台 + 非本页 + Unknown）→ true ──────────────────────────────────

    [Fact]
    public void DriftHit_LauncherForeground_NotStillMine_UnknownPage_ReturnsTrue()
    {
        var agent = CreateProbeAgent();

        var observation = new Observation([], "Launcher", 100);
        var container = CreateProbeContainer(stillMine: false);
        var belief = new WorldBelief(null, 0f, null, null);

        Assert.True(InvokeIsAgentScopeDrift(agent, observation, container, belief));
    }

    // ── 测试 2：正常导航（前台与基线相同，页面不同）→ false ───────────────────────────────────────────

    [Fact]
    public void NormalNavigation_ForegroundSameAsBaseline_ReturnsFalse()
    {
        var agent = CreateProbeAgent();

        var observation = new Observation([new ObservedElement("WiFi", null, 0)], BaselineApplication, 100);
        var container = CreateProbeContainer(stillMine: false);
        var belief = new WorldBelief("NetworkSettings", 1f, "navigated", 100);

        Assert.False(InvokeIsAgentScopeDrift(agent, observation, container, belief));
    }

    // ── 测试 3：前台与基线相同但页面 Unknown → false（仅前台一致不算 drift）───────────────────────────

    [Fact]
    public void SameForeground_UnknownPage_ReturnsFalse()
    {
        var agent = CreateProbeAgent();

        var observation = new Observation([], BaselineApplication, 100);
        var container = CreateProbeContainer(stillMine: false);
        var belief = new WorldBelief(null, 0f, null, null);

        Assert.False(InvokeIsAgentScopeDrift(agent, observation, container, belief));
    }

    // ── 测试 4：仍属本页（IsStillMine true）→ false ───────────────────────────────────────────────────

    [Fact]
    public void StillMineObservation_ReturnsFalse()
    {
        var agent = CreateProbeAgent();

        var observation = new Observation([], "Launcher", 100);
        var container = CreateProbeContainer(stillMine: true);
        var belief = new WorldBelief(null, 0f, null, null);

        Assert.False(InvokeIsAgentScopeDrift(agent, observation, container, belief));
    }

    // ── 测试 5：确定性（相同输入 → 相同输出；纯函数无内部状态）───────────────────────────────────────

    [Fact]
    public void Deterministic_SameInputs_SameResult()
    {
        var agent = CreateProbeAgent();
        var observation = new Observation([], "Launcher", 100);
        var container = CreateProbeContainer(stillMine: false);
        var belief = new WorldBelief(null, 0f, null, null);

        var first = InvokeIsAgentScopeDrift(agent, observation, container, belief);
        var second = InvokeIsAgentScopeDrift(agent, observation, container, belief);

        Assert.True(first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 经合法生产路径建立 RecoveryAnchor 基线的 Agent（空 Plan 一次 Run：
    /// Startup Ready → Running → 空循环 → Plan 耗尽 Fail；anchor.ApplicationIdentity == 基线应用）。
    /// </summary>
    private static RuntimeAgent CreateProbeAgent()
    {
        var environment = new DriftProbeEnvironment(BaselineApplication);
        var startup = new RuntimeStartup(
            environment,
            BaselineApplication,
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null);
        var traversal = new RuntimeTraversal(environment);
        // B3：探测 Run（空 Plan）不触发 drift → 恢复组件惰性接线
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            obs => obs.ForegroundApplication == BaselineApplication ? "ProbeEntry" : null,
            _ => CreateProbeContainer(stillMine: false),
            recovery);
        agent.RunAsync(
            new Goal(_ => new GoalEvidence(false, "drift probe: never satisfied", null)),
            new Plan([]),
            "drift-probe",
            CancellationToken.None).GetAwaiter().GetResult();
        return agent;
    }

    /// <summary>drift 输入用 Container（identity rule 注入可控 IsStillMine；executor 不被测试调用）。</summary>
    private static RuntimeContainer CreateProbeContainer(bool stillMine)
        => new(
            "ProbeEntry",
            _ => stillMine,
            (step, observation, candidates) => new TraversalStepResult.Failed("drift probe: executor not used"));

    /// <summary>反射调用私有纯函数 IsAgentScopeDrift（xunit v2 Assert.NotNull 返回 void — 用 ?? throw 承接）。</summary>
    private static bool InvokeIsAgentScopeDrift(RuntimeAgent agent, Observation observation, RuntimeContainer container, WorldBelief belief)
    {
        var method = typeof(RuntimeAgent).GetMethod("IsAgentScopeDrift", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("IsAgentScopeDrift 私有方法缺失（契约违约 — Agent.cs）。");
        return (bool)method.Invoke(agent, [observation, container, belief])!;
    }

    /// <summary>
    /// 最小探测环境（Phase 2C — 直接构造 Observation，不用 ScriptedEnvironment 的屏幕配置机制）：
    /// 前台应用可配置；观测始终为空元素列表（resolve 规则据此产出语义页面）。
    /// </summary>
    private sealed class DriftProbeEnvironment : IEnvironment
    {
        private readonly string _foregroundApplication;
        private long _sequence;

        public DriftProbeEnvironment(string foregroundApplication) => _foregroundApplication = foregroundApplication;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new Observation([], _foregroundApplication, ++_sequence));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.Dispatched, action.ToString(), "drift probe: dispatched"));
        }
    }
}
