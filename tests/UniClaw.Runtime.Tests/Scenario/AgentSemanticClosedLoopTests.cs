using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Phase 5 production-seam closed-loop proofs. No test-owned semantic runner.</summary>
public sealed class AgentSemanticClosedLoopTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly SemanticObject Bluetooth = SemanticObject.Define("BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ElementBounds TextBounds = new(0.05f, 0.20f, 0.50f, 0.30f);
    private static readonly ElementBounds ToggleBounds = new(0.75f, 0.20f, 0.90f, 0.30f);

    [Fact] public async Task P1_AlreadyOn_ZeroSemanticMutation() {
        var (agent, env) = Build("Wi‑Fi", true);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p1");
        Assert.IsType<SemanticRunResult.Satisfied>(result); Assert.Equal(RunState.Completed, agent.State); Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact] public async Task P2_OffToOn_OneSetSwitch_FreshEvidence() {
        var (agent, env) = Build("Wi‑Fi", false, true);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p2");
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.True(satisfied.Evidence.Satisfied); Assert.Equal(RunState.Completed, agent.State); Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.True(satisfied.Evidence.SourceObservationSequence >= 3);
    }

    [Fact] public async Task P3_StuckWorld_Budget_NoCompletion() {
        var (agent, env) = Build("Wi‑Fi", false);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p3", maxIterations: 2);
        Assert.IsType<SemanticRunResult.BudgetExhausted>(result); Assert.Equal(RunState.Failed, agent.State); Assert.Equal(2, env.ActionHistory.OfType<DeviceAction.SetSwitch>().Count());
    }

    [Fact] public async Task P4_UnknownState_ZeroSetSwitch() {
        var (agent, env) = Build("Wi‑Fi", null);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p4");
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result); Assert.Equal(RunState.Failed, agent.State); Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact] public async Task P5_RefreshesBindingIndex_AfterAction() {
        RuntimeContainer? current = null;
        var (agent, env) = Build("Wi‑Fi", false, true, shiftedOn: true, created: container => current = container);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p5");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(1, Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>()).TargetElementIndex);
        Assert.Contains(env.ObservationHistory[^1].Elements, e => e.Index == 2 && e.SwitchState == true);
        Assert.Equal(RunState.Completed, agent.State); Assert.Contains(1, current!.ObjectBindings.Single().ElementIndices); Assert.Contains(2, current.ObjectBindings.Single().ElementIndices);
    }

    [Fact] public async Task P6_BindingLost_NoFabricatedSuccess() {
        var (agent, _) = Build("Wi‑Fi", false, nextScreen: "Lost");
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p6");
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result); Assert.Equal(RunState.Failed, agent.State);
    }

    [Fact] public async Task P7_WrongCapability_ZeroSemanticDispatch() {
        var (agent, env) = Build("Wi‑Fi", false);
        var result = await agent.RunSemanticGoalAsync(new SemanticGoalInput("WifiConnectivity", "Brightness", true), [Wifi], [SetEnabled], "p7");
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result); Assert.Equal(RunState.Failed, agent.State); Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact] public async Task P8_AmbiguousCapability_ZeroSemanticDispatch() {
        var (agent, env) = Build("Wi‑Fi", false);
        var caps = ImmutableArray.Create(SetEnabled, Capability.Define("Other", "ConnectivitySetting", "Enabled"));
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], caps, "p8");
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result); Assert.Equal(RunState.Failed, agent.State); Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact] public async Task P9_CapabilitySelection_IsTraceable() {
        var (agent, _) = Build("Wi‑Fi", false, true);
        await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p9");
        Assert.Contains(agent.Trace, t => t.Reason == "semantic capability selected: SetEnabled");
    }

    [Fact] public async Task P10_ContainerOwnsRefreshedState() {
        RuntimeContainer? current = null;
        var (agent, _) = Build("Wi‑Fi", false, true, created: container => current = container);
        await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p10");
        Assert.DoesNotContain(typeof(RuntimeAgent).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), f => f.Name.Contains("ObjectState", StringComparison.Ordinal));
        Assert.True(current!.ObjectStateBeliefs["WifiConnectivity.Enabled"]);
    }

    [Fact] public async Task P11_PageContradiction_ZeroSetSwitch() {
        var (agent, env) = Build("Wi‑Fi", false, pageContradiction: true);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p11");
        Assert.IsType<SemanticRunResult.SemanticContradiction>(result); Assert.Equal(RunState.Failed, agent.State); Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact] public async Task P12_BudgetIsBounded() {
        var (agent, env) = Build("Wi‑Fi", false);
        var result = await agent.RunSemanticGoalAsync(Goal("WifiConnectivity"), [Wifi], [SetEnabled], "p12", maxIterations: 1);
        Assert.IsType<SemanticRunResult.BudgetExhausted>(result); Assert.Equal(RunState.Failed, agent.State); Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    [Fact] public async Task P13_Bluetooth_FullClosedLoop() {
        var (agent, env) = Build("Bluetooth", false, true);
        var result = await agent.RunSemanticGoalAsync(Goal("BluetoothConnectivity"), [Bluetooth], [SetEnabled], "p13");
        Assert.IsType<SemanticRunResult.Satisfied>(result); Assert.Equal(RunState.Completed, agent.State); Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    private static SemanticGoalInput Goal(string identity) => new(identity, "Enabled", true);

    private static (RuntimeAgent Agent, ScriptedEnvironment Environment) Build(string label, bool? initial, bool changeToOn = false, bool shiftedOn = false, string? nextScreen = null, bool pageContradiction = false, Action<RuntimeContainer>? created = null)
    {
        var identity = label == "Bluetooth" ? "BluetoothConnectivity" : "WifiConnectivity";
        var obj = identity == "BluetoothConnectivity" ? Bluetooth : Wifi;
        var criteria = new ElementBindingCriteria([obj],
            ImmutableDictionary<string, string>.Empty.Add(identity, label),
            ImmutableDictionary<string, string>.Empty.Add(identity, "toggle"));
        var pages = new PageAnalysisCriteria("settings", ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", [label]),
            pageContradiction ? ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", [label]) : null);
        var onScreen = shiftedOn ? Screen("On", label, true, null, true) : Screen("On", label, true);
        var offTransition = changeToOn || nextScreen is not null
            ? new TransitionConfig(ScreenTransitionAction.SetSwitch, nextScreen ?? "On", true) : null;
        var env = new ScriptedEnvironment("Settings", "Settings", [Screen("Settings", label, initial, offTransition), onScreen, new ScreenConfig("Lost", "settings", [])]);
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page)
        {
            var container = new RuntimeContainer(page, o => o.ForegroundApplication == "settings", traversal.ExecuteStep);
            created?.Invoke(container);
            return container;
        }
        return (new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), _ => "Settings", Factory, recovery, pages, criteria), env);
    }

    private static ScreenConfig Screen(string name, string label, bool? value, TransitionConfig? transition = null, bool shifted = false) => new(name, "settings",
        shifted
            ? [new ElementConfig("other", null, null, new ElementBounds(0.1f, 0.1f, 0.2f, 0.2f), "text"), new ElementConfig(label, null, null, new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"), new ElementConfig("", value, transition, new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle")]
            : [new ElementConfig(label, null, null, TextBounds, "menuItem"), new ElementConfig("", value, transition, ToggleBounds, "toggle")]);
}
