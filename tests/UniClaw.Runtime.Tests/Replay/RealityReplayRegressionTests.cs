using System.Collections.Immutable;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Environment;
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
/// S2 REALITY_SEEDED REPLAY — reality-derived executable regression scenarios.
///
/// Element text from EP-04 sim-replay (A3) is RECORDED_REALITY.
/// SwitchState is manually synthesized — no recorded ON/OFF pair exists.
/// Overall scenario maturity: REALITY_SEEDED.
/// </summary>
public sealed class RealityReplayRegressionTests
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
            .Add("Settings", ["Wi‑Fi", "Auto-connect", "AndroidWifi"]));

    private static RuntimeAgent BuildAgent(IEnvironment env)
    {
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, SettingsApp, _ => "Settings");
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        return new RuntimeAgent(
            startup, traversal,
            ct => semanticEnv.ObserveAsync(ct),
            _ => "Settings",
            Factory, recovery,
            PageCriteria(), WifiCriteria());
    }

    private static ReplayDispatch D(string kind, int? index = null, bool? state = null,
        ElementBounds? bounds = null, ActionResultOutcome outcome = ActionResultOutcome.Dispatched)
    {
        DeviceAction action = kind switch
        {
            "LaunchApp" => new DeviceAction.LaunchApp(SettingsApp),
            "Tap" => new DeviceAction.Tap(index, bounds),
            "SetSwitch" => new DeviceAction.SetSwitch(index!.Value, state!.Value, bounds),
            _ => throw new ArgumentException($"Unknown action kind: {kind}"),
        };
        return new ReplayDispatch(action,
            new ActionResult(outcome, action.ToString(), "replay: recorded"));
    }

    // ── R1: Already ON — zero mutation (REALITY_SEEDED) ──────────────────

    /// <summary>
    /// Replay a reality-seeded observation where Wi‑Fi is already ON.
    /// Element text from EP-04 A3; switch state mirrors 5.1 calibration pair
    /// (wifi-slice2-calibration — emulator-5554, recorded bounds (0.832,0.407)-(0.96,0.452)).
    /// Maturity: REALITY_SEEDED.
    /// </summary>
    [Fact]
    public async Task R1_AlreadyOn_RealitySeededReplay_ZeroMutation()
    {
        var onPre = new Observation(
            [
                new ObservedElement("Wi‑Fi", true, 0,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 1);

        var onPost = new Observation(  // after LaunchApp, still ON
            [
                new ObservedElement("Wi‑Fi", true, 0,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 2);

        var script = new ReplayScript(
            [onPre, onPost],
            [D("LaunchApp")]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "r1-reality-replay");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.DoesNotContain(env.ActionHistory,
            a => a is DeviceAction.SetSwitch);
    }

    // ── R2: Known OFF → SetSwitch(true) dispatched (REALITY_SEEDED) ──────

    /// <summary>
    /// Replay a reality-seeded pair: OFF observation → dispatch → ON observation.
    /// OFF/ON switch states mirror the 5.1 recorded calibration pair
    /// (wifi-slice2-calibration — emulator-5554, wifi_on 0→1 read-only verified).
    /// </summary>
    [Fact]
    public async Task R2_OffToOn_RealitySeededReplay_DispatchesSetSwitch()
    {
        var offPre = new Observation(
            [
                new ObservedElement("Wi‑Fi", false, 0,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 1);

        var offPost = new Observation(  // after LaunchApp, still OFF
            [
                new ObservedElement("Wi‑Fi", false, 0,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 2);

        var onObs = new Observation(    // after SetSwitch
            [
                new ObservedElement("Wi‑Fi", true, 0,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 3);

        var script = new ReplayScript(
            [offPre, offPost, onObs],
            [
                D("LaunchApp"),
                D("SetSwitch", index: 0, state: true, bounds: new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f)),
            ]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "r2-reality-replay");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.Contains(env.ActionHistory,
            a => a is DeviceAction.SetSwitch s && s.TargetState == true);
    }

    // ── R3: UNKNOWN state → StateEvidenceRequired ────────────────────────

    [Fact]
    public async Task R3_UnknownState_Replay_StateEvidenceRequired()
    {
        var obsPre = new Observation(
            [
                new ObservedElement("Wi‑Fi", null, 0,  // UNKNOWN
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 1);

        var obsPost = new Observation(  // after LaunchApp, still UNKNOWN
            [
                new ObservedElement("Wi‑Fi", null, 0,
                    new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f), "toggle"),
                new ObservedElement("AndroidWifi", null, 1,
                    new ElementBounds(0.08f, 0.20f, 0.50f, 0.30f), "menuItem"),
            ],
            SettingsApp, 2);

        var script = new ReplayScript([obsPre, obsPost], [D("LaunchApp")]);
        var env = new ReplayEnvironment(script);
        var agent = BuildAgent(env);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            Objects, Capabilities, "r3-reality-replay");

        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.DoesNotContain(env.ActionHistory, a => a is DeviceAction.SetSwitch);
    }

    // ── PROVENANCE ───────────────────────────────────────────────────────

    [Fact]
    public void Provenance_FrameAsset_WithoutScreenshot_IsValid()
    {
        var frame = new FrameAsset
        {
            FrameId = "ep04-internet",
            CaptureSessionId = "ep04-sim-replay",
            SequenceIndex = 0,
            Provenance = AssetMaturity.RealitySeeded,
            Observation = new Observation(
                [
                    new ObservedElement("Wi‑Fi", null, 0,
                        new ElementBounds(0.08f, 0.40f, 0.25f, 0.44f), "menuItem"),
                ],
                SettingsApp, 1),
        };

        // Frame without ScreenshotArtifactId is valid for Observation Replay
        Assert.Null(frame.ScreenshotArtifactId);
        Assert.NotNull(frame.Observation);
        Assert.Equal(AssetMaturity.RealitySeeded, frame.Provenance);
        Assert.Equal("ep04-internet", frame.FrameId);
    }

    [Fact]
    public void Provenance_FrameRelation_Explicit_NotFilenameDerived()
    {
        var relation = new FrameRelation
        {
            FrameRelationId = "rel-1",
            Type = FrameRelationType.ObservedAfterAction,
            SourceFrameId = "frame-2",
            TargetFrameId = "frame-1",
        };

        Assert.Equal("frame-1", relation.TargetFrameId);
        Assert.Equal("frame-2", relation.SourceFrameId);
        Assert.Equal(FrameRelationType.ObservedAfterAction, relation.Type);
        // Relation is explicit — not derived from filenames
    }

    [Fact]
    public void Provenance_Manifest_SerializesAndValidates()
    {
        var manifest = new HarnessAssetManifest
        {
            ManifestId = "ep04-reality-seeded",
            Provenance = AssetMaturity.RealitySeeded,
            Source = "EP-04 sim-replay (A3) + synthesized Wi-Fi state",
            CaptureSessions =
            [
                new CaptureSession
                {
                    CaptureSessionId = "ep04-session",
                    Provenance = AssetMaturity.RealitySeeded,
                    Source = "EP-04 sim-replay",
                    FrameIds = ["ep04-wifi-on"],
                },
            ],
            Frames =
            [
                new FrameAsset
                {
                    FrameId = "ep04-wifi-on",
                    CaptureSessionId = "ep04-session",
                    Provenance = AssetMaturity.RealitySeeded,
                    Observation = new Observation(
                        [new ObservedElement("Wi‑Fi", true, 0)],
                        SettingsApp, 1),
                },
            ],
            Replays =
            [
                new ReplayAsset
                {
                    ReplayId = "already-on",
                    Mode = ReplayMode.Observation,
                    Provenance = AssetMaturity.RealitySeeded,
                    FrameIds = ["ep04-wifi-on"],
                    Dispatches =
                    [
                        new RecordedDispatchAsset
                        {
                            DispatchId = "d1",
                            ExpectedActionKind = "LaunchApp",
                            ApplicationId = SettingsApp,
                            Outcome = ActionResultOutcome.Dispatched,
                        },
                    ],
                },
            ],
        };

        var json = HarnessAssetManifestJson.Serialize(manifest);
        var restored = HarnessAssetManifestJson.Deserialize(json);

        Assert.Equal(manifest.ManifestId, restored.ManifestId);
        Assert.Equal(AssetMaturity.RealitySeeded, restored.Provenance);
        Assert.Single(restored.Frames);
        Assert.Single(restored.Replays);

        var errors = HarnessAssetManifestValidator.Validate(manifest);
        Assert.Empty(errors);
    }
}
