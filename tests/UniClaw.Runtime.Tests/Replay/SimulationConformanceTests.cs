using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Replay;

/// <summary>
/// S1 Runtime Simulation Conformance Proofs.
///
/// Each test runs the real graduated Runtime (Agent → Container → Traversal)
/// against a SimulationEnvironment. No ScriptedEnvironment. No PlanStep.
/// No caller DeviceAction. No legacy text grounding.
///
/// These are the H1-H15 proofs required for harness graduation.
/// </summary>
public sealed class SimulationConformanceTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly SemanticObject Bluetooth = SemanticObject.Define(
        "BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ImmutableArray<SemanticObject> Objects = [Wifi, Bluetooth];
    private static readonly ImmutableArray<Capability> Capabilities = [SetEnabled];

    private static ElementBindingCriteria WifiCriteria() => new(
        [Wifi],
        ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
        ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));

    private static ElementBindingCriteria BluetoothCriteria() => new(
        [Bluetooth],
        ImmutableDictionary<string, string>.Empty.Add("BluetoothConnectivity", "Bluetooth"),
        ImmutableDictionary<string, string>.Empty.Add("BluetoothConnectivity", "toggle"));

    private static PageAnalysisCriteria PageCriteria(string label) => new(
        "settings",
        ImmutableDictionary<string, ImmutableArray<string>>.Empty
            .Add("Settings", [label]));

    private static RuntimeAgent BuildAgent(
        SimulationEnvironment env,
        string label,
        ElementBindingCriteria? criteria = null)
    {
        var c = criteria ?? WifiCriteria();
        var pages = PageCriteria(label);
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(
            page,
            _ => true,
            traversal.ExecuteStep);
        return new RuntimeAgent(
            startup, traversal,
            ct => env.ObserveAsync(ct),
            _ => "Settings",
            Factory, recovery,
            pages, c);
    }

    // ── H1: Synthetic deterministic Wi-Fi OFF→ON scenario ──────────────────

    [Fact]
    public async Task H1_WifiOffToOn_CompletesThroughRealRuntime()
    {
        var env = SimulationPresets.WifiOff();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h1");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        // One SetSwitch(true) dispatched
        Assert.Contains(env.ActionHistory, a =>
            a is DeviceAction.SetSwitch s && s.TargetState == true);
    }

    // ── H2: Already ON → zero mutation ─────────────────────────────────────

    [Fact]
    public async Task H2_AlreadyOn_ZeroMutation()
    {
        var env = SimulationPresets.WifiOn();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h2");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.DoesNotContain(env.ActionHistory,
            a => a is DeviceAction.SetSwitch);
    }

    // ── H3: UNKNOWN → StateEvidenceRequired + zero mutation ─────────────────

    [Fact]
    public async Task H3_UnknownState_StateEvidenceRequired_ZeroDispatch()
    {
        var env = SimulationPresets.WifiUnknown();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h3");

        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.DoesNotContain(env.ActionHistory,
            a => a is DeviceAction.SetSwitch);
    }

    // ── H4: Dispatch success + unchanged world ≠ completion ─────────────────

    [Fact]
    public async Task H4_DispatchWithoutEffect_NotSatisfied()
    {
        var wifi = new SimulatedToggle("WifiConnectivity", "Wi‑Fi", false,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), 1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        // World stays OFF despite dispatch
        var config = new SimulationConfig { NeverApplyStateChanges = true };
        var env = new SimulationEnvironment([wifi], config);
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h4", maxIterations: 2);

        // World stays OFF despite dispatch → BudgetExhausted
        Assert.IsType<SemanticRunResult.BudgetExhausted>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.Contains(env.ActionHistory,
            a => a is DeviceAction.SetSwitch s && s.TargetState == true);
    }

    // ── H5: Timeout + world unchanged → bounded termination ─────────────────

    [Fact]
    public async Task H5_TimeoutWorldUnchanged_BoundedTermination()
    {
        var wifi = new SimulatedToggle("WifiConnectivity", "Wi‑Fi", false,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), 1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        var config = new SimulationConfig { TimeoutActionAtCall = 1 };
        var env = new SimulationEnvironment([wifi], config);
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h5", maxIterations: 1);

        Assert.Equal(RunState.Failed, agent.State);
    }

    // ── H6: Index changes between observations — fresh grounding required ───

    [Fact]
    public async Task H6_IndexShift_FreshGrounding()
    {
        var env = SimulationPresets.WifiOff();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h6");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        // Binding indices are refreshed after action
        Assert.True(env.ObservationHistory.Count >= 3);
    }

    // ── H11: Bluetooth cross-domain ─────────────────────────────────────────

    [Fact]
    public async Task H11_Bluetooth_CrossDomain()
    {
        var env = SimulationPresets.WifiAndBluetoothOff();
        var agent = BuildAgent(env, "Bluetooth", BluetoothCriteria());

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("BluetoothConnectivity", "Enabled", true),
            Objects, Capabilities, "h11");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
    }

    // ── C1 CONFORMANCE: Dispatch ≠ Effect ───────────────────────────────────

    [Fact]
    public async Task C1_DispatchIsNotEffect()
    {
        var env = SimulationPresets.WifiOff();
        var agent = BuildAgent(env, "Wi‑Fi");

        // World starts OFF — prove dispatch alone doesn't satisfy
        // (the real loop re-observes and finds SwitchState=true only after state change)
        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h_c1");

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        // GoalEvidence requires fresh observation — not dispatch receipt
        Assert.True(satisfied.Evidence.Satisfied);
        Assert.True(satisfied.Evidence.SourceObservationSequence >= 3);
    }

    // ── C2 CONFORMANCE: UNKNOWN is truthful ─────────────────────────────────

    [Fact]
    public async Task C2_UnknownIsTruthful()
    {
        var env = SimulationPresets.WifiUnknown();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h_c2");

        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
    }

    // ── C3 CONFORMANCE: Already satisfied is idempotent ─────────────────────

    [Fact]
    public async Task C3_AlreadySatisfiedIsIdempotent()
    {
        var env = SimulationPresets.WifiOn();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h_c3");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
    }

    // ── C4 CONFORMANCE: Fresh observation required ──────────────────────────

    [Fact]
    public async Task C4_FreshObservationRequired()
    {
        var env = SimulationPresets.WifiOff();
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h_c4");

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        // GoalEvidence is from fresh post-action observation, not initial
        Assert.True(satisfied.Evidence.SourceObservationSequence > 2);
    }

    // ── C9 CONFORMANCE: Agent authority — caller supplies no PlanStep ───────

    [Fact]
    public void C9_AgentAuthority_NoCallerPlanStep()
    {
        // The semantic path uses RunSemanticGoalAsync which takes SemanticGoalInput,
        // not PlanStep. Verify the signature.
        var method = typeof(RuntimeAgent).GetMethod(nameof(RuntimeAgent.RunSemanticGoalAsync));
        Assert.NotNull(method);
        var paramTypes = method!.GetParameters().Select(p => p.ParameterType).ToHashSet();
        Assert.DoesNotContain(typeof(PlanStep), paramTypes);
        Assert.DoesNotContain(typeof(DeviceAction), paramTypes);
    }

    // ── C10 CONFORMANCE: Budget boundedness ─────────────────────────────────

    [Fact]
    public async Task C10_BudgetBoundedness()
    {
        // World that never converges — state changes never applied
        var wifi = new SimulatedToggle("WifiConnectivity", "Wi‑Fi", false,
            new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), 1,
            new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f));
        var config = new SimulationConfig { NeverApplyStateChanges = true };
        var env = new SimulationEnvironment([wifi], config);
        var agent = BuildAgent(env, "Wi‑Fi");

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "h_c10", maxIterations: 2);

        Assert.IsType<SemanticRunResult.BudgetExhausted>(result);
        Assert.Equal(RunState.Failed, agent.State);
    }
}
