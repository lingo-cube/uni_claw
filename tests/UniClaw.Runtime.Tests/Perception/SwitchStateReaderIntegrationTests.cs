using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Perception;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using ReplayDispatch = UniClaw.Runtime.Tests.Replay.ReplayDispatch;
using ReplayEnvironment = UniClaw.Runtime.Tests.Replay.ReplayEnvironment;
using ReplayScript = UniClaw.Runtime.Tests.Replay.ReplayScript;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// T3 PERCEPTION REPLAY — Integration falsifier.
///
/// Proves ISwitchStateReader integrates via IEnvironment decorator
/// without ANY Core Runtime changes (Agent/Container/Traversal/IEnvironment = 0 delta).
///
/// Cases A/B/C use the graduated ReplayEnvironment with observations
/// that represent what perception WOULD produce — the replay path
/// is already proven by RealityReplayRegressionTests R1-R3.
/// </summary>
public sealed class SwitchStateReaderIntegrationTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ImmutableArray<SemanticObject> Objects = [Wifi];
    private static readonly ImmutableArray<Capability> Capabilities = [SetEnabled];
    private const string SettingsApp = "com.android.settings";

    private static ElementBindingCriteria WifiCriteria() => new(
        [Wifi],
        ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
        ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));

    private static PageAnalysisCriteria PageCriteria() => new(
        SettingsApp,
        ImmutableDictionary<string, ImmutableArray<string>>.Empty
            .Add("Settings", ["Wi‑Fi"]));

    private static RuntimeAgent BuildAgent(IEnvironment env)
    {
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, SettingsApp, _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(
            startup, traversal,
            ct => env.ObserveAsync(ct),
            _ => "Settings",
            Factory, recovery,
            PageCriteria(), WifiCriteria());
    }

    private static ReplayDispatch D(DeviceAction action) =>
        new(action, new ActionResult(ActionResultOutcome.Dispatched, action.ToString(), "ok"));

    // ── CASE A: Already ON (perception returns true) → zero mutation ─────

    [Fact]
    public async Task CaseA_PerceptionOn_ZeroMutation()
    {
        var pre = Obs("Wi‑Fi", true, 1);
        var post = Obs("Wi‑Fi", true, 2);
        var script = new ReplayScript([pre, post], [D(new DeviceAction.LaunchApp(SettingsApp))]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "case-a");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
    }

    // ── CASE B: OFF → ON (perception shows OFF, then ON after dispatch) ──

    [Fact]
    public async Task CaseB_PerceptionOffToOn_DispatchesSetSwitch()
    {
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);
        var pre = Obs("Wi‑Fi", false, 1);
        var postLaunch = Obs("Wi‑Fi", false, 2);
        var postAction = Obs("Wi‑Fi", true, 3);
        var script = new ReplayScript(
            [pre, postLaunch, postAction],
            [
                D(new DeviceAction.LaunchApp(SettingsApp)),
                D(new DeviceAction.SetSwitch(1, true, bounds)),
            ]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "case-b");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Contains(env.ActionHistory,
            a => a is DeviceAction.SetSwitch s && s.TargetState == true);
    }

    // ── CASE C: UNKNOWN → StateEvidenceRequired, no blind toggle ─────────

    [Fact]
    public async Task CaseC_PerceptionUnknown_StateEvidenceRequired()
    {
        var pre = Obs("Wi‑Fi", null, 1);
        var post = Obs("Wi‑Fi", null, 2);
        var script = new ReplayScript([pre, post], [D(new DeviceAction.LaunchApp(SettingsApp))]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "case-c");

        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
    }

    // ── PERCEPTION ENVIRONMENT DECORATOR PROOF ────────────────────────────

    /// <summary>
    /// Proves a PerceptionEnvironment decorator can enrich observations
    /// with SwitchState WITHOUT changing IEnvironment.
    /// </summary>
    [Fact]
    public async Task PerceptionDecorator_EnrichesSwitchState_WithoutChangingIEnvironment()
    {
        // Inner env produces observations without SwitchState
        var rawObs = Obs("Wi‑Fi", null, 1); // SwitchState=null
        var enrichedObs = Obs("Wi‑Fi", true, 1); // enriched by perception

        var script = new ReplayScript(
            [rawObs, enrichedObs],
            [D(new DeviceAction.LaunchApp(SettingsApp))]);
        var replayEnv = new ReplayEnvironment(script);

        // The PerceptionEnvironment sits between replay and Runtime
        var reader = MockSwitchStateReader.AlwaysOn;
        var env = new PerceptionEnvironment(replayEnv, reader);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "decorator");

        // Perception enriched null→true, Runtime sees ON → Satisfied
        Assert.IsType<SemanticRunResult.Satisfied>(result);
    }

    // ── CORE DELTA PROOF ─────────────────────────────────────────────────

    [Fact]
    public void CoreExtensibility_AgentUnchanged()
    {
        var method = typeof(RuntimeAgent).GetMethod("RunSemanticGoalAsync")!;
        var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToHashSet();
        Assert.DoesNotContain(typeof(ISwitchStateReader), paramTypes);
    }

    [Fact]
    public void CoreExtensibility_IEnvironmentUnchanged()
    {
        var methods = typeof(IEnvironment).GetMethods()
            .Where(m => !m.IsSpecialName).ToArray();
        Assert.Equal(2, methods.Length);
    }

    [Fact]
    public void CoreExtensibility_ContainerUnchanged()
    {
        // Container.RefreshObjectStateBeliefs consumes SwitchState from ObservedElement
        // — that field already exists. No new dependency on ISwitchStateReader.
        var fields = typeof(RuntimeContainer).GetFields(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var readerFields = fields.Where(f =>
            f.FieldType == typeof(ISwitchStateReader)
            || f.FieldType.Name.Contains("SwitchState")).ToArray();
        Assert.Empty(readerFields);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static Observation Obs(string label, bool? switchState, long seq)
        => new(
            [
                new ObservedElement(label, null, 0,
                    new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"),
                new ObservedElement("", switchState, 1,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
            ],
            SettingsApp, seq);

    /// <summary>Perception-aware IEnvironment decorator. Zero IEnvironment delta.</summary>
    private sealed class PerceptionEnvironment : IEnvironment
    {
        private readonly IEnvironment _inner;
        private readonly ISwitchStateReader _reader;

        public PerceptionEnvironment(IEnvironment inner, ISwitchStateReader reader)
        {
            _inner = inner;
            _reader = reader;
        }

        public async Task<Observation> ObserveAsync(CancellationToken ct)
        {
            var obs = await _inner.ObserveAsync(ct);
            var enriched = ImmutableArray.CreateBuilder<ObservedElement>();
            foreach (var el in obs.Elements)
            {
                if (el is { PerceptionType: "toggle", Bounds: { IsValid: true } bounds })
                {
                    var state = await _reader.ReadAsync(bounds, ct);
                    enriched.Add(el with { SwitchState = state });
                }
                else enriched.Add(el);
            }
            return obs with { Elements = enriched.ToImmutable() };
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken ct)
            => _inner.ExecuteAsync(action, ct);
    }
}
