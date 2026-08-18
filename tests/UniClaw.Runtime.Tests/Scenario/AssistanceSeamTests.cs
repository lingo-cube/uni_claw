using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Brain;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Capabilities;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// L1 CONSULT seam tests (runtime-assistance-seam): the Agent consults the
/// optional external provider ONLY at the belief adjudication surface
/// (LocalPageBeliefState Contradicted), consumes advice through EXISTING
/// deterministic actions (re-observe), discards stale/uncorrelated advice,
/// fails closed on consult failure, and is bounded. Advice is candidate-only:
/// the Agent keeps final decision authority (I-3).
///
/// NOTE (repository truth): with the current identity rule (IsStillMine always
/// produces Supports or Contradicts, never Insufficient) the fused
/// LocalPageBeliefState can only be Supported or Contradicted — the Unresolved
/// branch of the seam is defensive (reachable if the identity rule ever yields
/// Insufficient), so tests exercise the Contradicted surface.
/// </summary>
public sealed class AssistanceSeamTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ElementBounds TextBounds = new(0.05f, 0.20f, 0.50f, 0.30f);
    private static readonly ElementBounds ToggleBounds = new(0.75f, 0.20f, 0.90f, 0.30f);

    private static SemanticGoalInput Goal() => new("WifiConnectivity", "Enabled", true);

    /// <summary>Contradiction trigger text: present on the contradicting screen,
    /// absent on the clean screen (drives PageAnalysis TEXT_ANCHOR_NEGATIVE).</summary>
    private const string ContradictingText = "CONTRADICTING_TEXT";

    /// <summary>
    /// Wi-Fi off screen; when <paramref name="contradicting"/> is true it also
    /// carries the contradiction trigger text (→ fused belief Contradicted).
    /// </summary>
    private static ScreenConfig Screen(string name, bool contradicting, bool off = false, bool on = false)
    {
        var elements = new List<ElementConfig>
        {
            new("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new("", on ? true : (off ? false : (bool?)null),
                on ? null : new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true),
                ToggleBounds, "toggle"),
        };
        if (contradicting)
        {
            elements.Add(new ElementConfig(ContradictingText, null, null, new ElementBounds(0.1f, 0.9f, 0.5f, 0.95f), "text"));
        }

        return new ScreenConfig(name, "settings", [.. elements]);
    }

    private static RuntimeAgent Build(
        ScriptedEnvironment env,
        IAssistanceProvider? provider)
    {
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", [ContradictingText]));

        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, o => o.ForegroundApplication == "settings", traversal.ExecuteStep);
        return new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), _ => "Settings", Factory, recovery, pages, criteria, provider);
    }

    [Fact]
    public async Task Contradicted_ConsultReObserve_ResolvesWorld_GoalSatisfied()
    {
        // Observation sequence: seq1 = Startup internal observe, seq2 = Agent
        // initial observe (both on the contradicting screen) → fused belief
        // Contradicted → consult → advice "re-observe" → seq3 = external world
        // transitions to the clean screen (deterministic, observeScreenTransitions)
        // → continuity verified → SAME goal re-evaluated → SetSwitch → satisfied.
        var env = new ScriptedEnvironment(
            "Settings",
            "Settings",
            [
                Screen("Settings", contradicting: true, off: true),
                Screen("SettingsClean", contradicting: false, off: true),
                Screen("On", contradicting: false, on: true),
            ],
            observeScreenTransitions: new Dictionary<long, string> { [3] = "SettingsClean" });

        var provider = new FakeAssistanceProvider(ctx =>
            new AssistanceAdvice(ctx.RequestId, ctx.WorldVersion, "re-observe", null, "re-observe per test"));
        var agent = Build(env, provider);

        var result = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-1");

        // Advice resolved the contradiction through the Agent's own deterministic
        // re-observe; the run completed via the normal semantic path.
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.True(provider.Consults >= 1, "the Agent consulted at the adjudication point");
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("assistance consult", StringComparison.Ordinal));
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("assistance re-observe accepted", StringComparison.Ordinal));
        // Advice was candidate-only: the Agent itself dispatched the SetSwitch.
        Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact]
    public async Task Contradicted_NoProvider_FailsClosed_ZeroRegression()
    {
        // Null provider ⇒ today's fail-closed behavior (zero regression).
        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [Screen("Settings", contradicting: true, off: true), Screen("On", contradicting: false, on: true)]);
        var agent = Build(env, provider: null);

        var result = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-2");

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact]
    public async Task Contradicted_StaleAdvice_Discarded_FailsClosed()
    {
        // Advice bound to an ADVANCED world version is stale → discarded → the
        // Agent fails closed exactly as today (never applies stale advice).
        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [Screen("Settings", contradicting: true, off: true), Screen("On", contradicting: false, on: true)]);
        var provider = new FakeAssistanceProvider(ctx =>
            new AssistanceAdvice(ctx.RequestId, ctx.WorldVersion + 1, "re-observe", null, "stale"));
        var agent = Build(env, provider);

        var result = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-3");

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Equal(1, provider.Consults);
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("stale world version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Contradicted_ConsultThrows_FailsClosed()
    {
        // Consult failure is an Agent-side decision input: fail closed, never a
        // process fault, never fabricated progress.
        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [Screen("Settings", contradicting: true, off: true), Screen("On", contradicting: false, on: true)]);
        var provider = FakeAssistanceProvider.Throwing();
        var agent = Build(env, provider);

        var result = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-4");

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Equal(1, provider.Consults);
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("consult failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Contradicted_ActionableAdviceLoop_Bounded_FailsClosedAfterBudget()
    {
        // World stays contradicting; advice "re-observe" is actionable but does
        // not resolve the contradiction. The consult budget (3) bounds the loop;
        // after the budget the Agent fails closed (no unbounded loop).
        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [Screen("Settings", contradicting: true, off: true), Screen("On", contradicting: false, on: true)]);
        var provider = new FakeAssistanceProvider(ctx =>
            new AssistanceAdvice(ctx.RequestId, ctx.WorldVersion, "re-observe", null, "keep trying"));
        var agent = Build(env, provider);

        var result = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-5");

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Equal(3, provider.Consults); // bounded: exactly MaxAssistanceConsults
        Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact]
    public async Task Contradicted_RebindAdvice_ActionableButDoesNotResolve_FailsClosed()
    {
        // Advice "rebind" re-runs binding analysis on the current observation
        // (existing deterministic mechanism); the contradiction persists and the
        // bounded budget then fails closed.
        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [Screen("Settings", contradicting: true, off: true), Screen("On", contradicting: false, on: true)]);
        var provider = new FakeAssistanceProvider(ctx =>
            new AssistanceAdvice(ctx.RequestId, ctx.WorldVersion, "rebind", null, "rebind per test"));
        var agent = Build(env, provider);

        var result = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-6");

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("assistance rebind applied", StringComparison.Ordinal));
        Assert.True(provider.Consults >= 1);
    }

    [Fact]
    public async Task CorrelatedAdvice_ContextCarriesRunAndWorldVersion()
    {
        // The context is a truthful snapshot: run id, page, belief state,
        // world version = observation sequence.
        var env = new ScriptedEnvironment(
            "Settings", "Settings",
            [Screen("Settings", contradicting: true, off: true), Screen("On", contradicting: false, on: true)]);
        var provider = new FakeAssistanceProvider(ctx =>
            new AssistanceAdvice(ctx.RequestId, ctx.WorldVersion, "re-observe", null, "ok"));
        var agent = Build(env, provider);

        _ = await agent.RunSemanticGoalAsync(Goal(), [Wifi], [SetEnabled], "assist-7");

        // The first consult carries the initial adjudication snapshot (later
        // consults in the bounded loop carry advanced world versions).
        var ctx = provider.Received[0];
        Assert.Equal("assist-7", ctx.RunId);
        Assert.Equal("Settings", ctx.SemanticPage);
        Assert.Equal(SemanticBeliefState.Contradicted, ctx.BeliefState);
        Assert.Equal(2, ctx.WorldVersion); // the Agent initial observation sequence
        Assert.NotNull(ctx.Observation);
        Assert.True(ctx.RequestId.StartsWith("assist-assist-7-", StringComparison.Ordinal));
    }
}
