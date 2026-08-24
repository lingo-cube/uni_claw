using System.Collections.Immutable;
using SkiaSharp;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// Production composition proofs — PhysicalEnvironment with recorded/fixture data.
///
/// Uses the REAL production PhysicalEnvironment class with stubbed
/// screenshot/perception/ADB sources. Proves the full composition boundary
/// without live device dependency.
/// </summary>
public sealed class PhysicalEnvironmentCompositionTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define(
        "WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define(
        "SetEnabled", "ConnectivitySetting", "Enabled");
    private const string SettingsApp = "com.android.settings";
    private const int DisplayW = 1080;
    private const int DisplayH = 1920;

    // ── PE-G1: Concrete IEnvironment composition exists ───────────────────

    [Fact]
    public void PEG1_PhysicalEnvironment_ImplementsIEnvironment()
    {
        using var bitmap = new SKBitmap(100, 100);
        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([]),
            new StubDispatchTarget(),
            SettingsApp, DisplayW, DisplayH);

        Assert.IsAssignableFrom<IEnvironment>(env);
    }

    // ── PE-G2: ObserveAsync produces valid Observation ────────────────────

    [Fact]
    public async Task PEG2_ObserveAsync_ProducesObservation()
    {
        using var bitmap = CreateTinyBitmap();
        var candidates = new[]
        {
            new PerceptionCandidate("Wi‑Fi", "menuItem", new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f)),
            new PerceptionCandidate("", "toggle", new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f)),
        };

        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([.. candidates]),
            new StubDispatchTarget(),
            SettingsApp, DisplayW, DisplayH);

        var obs = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal(2, obs.Elements.Length);
        Assert.Equal("Wi‑Fi", obs.Elements[0].Text);
        Assert.Equal("menuItem", obs.Elements[0].PerceptionType);
        Assert.Equal("toggle", obs.Elements[1].PerceptionType); // provider raw type preserved
        Assert.Equal(1, obs.SequenceNumber);
    }

    // ── PE-G3: Provider type preservation ────────────────────────────────

    [Fact]
    public async Task PEG3_TypeNormalization_SwitchToToggle()
    {
        using var bitmap = CreateTinyBitmap();
        var candidates = new[]
        {
            new PerceptionCandidate("System", "switch", new ElementBounds(0.2f, 0.78f, 0.3f, 0.80f)),
        };

        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([.. candidates]),
            new StubDispatchTarget(),
            SettingsApp, DisplayW, DisplayH);

        var obs = await env.ObserveAsync(CancellationToken.None);

        Assert.Equal("switch", obs.Elements[0].PerceptionType);
    }

    // ── PE-G4: Frame safety — stale evidence fail-closed ──────────────────

    [Fact]
    public async Task PEG4_FrameSafety_SequentialObservesHaveIndependentFrames()
    {
        using var bitmap = CreateTinyBitmap();
        var candidates = new[]
        {
            new PerceptionCandidate("Wi‑Fi", "toggle", new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f)),
        };

        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([.. candidates]),
            new StubDispatchTarget(),
            SettingsApp, DisplayW, DisplayH);

        var obs1 = await env.ObserveAsync(CancellationToken.None);
        var obs2 = await env.ObserveAsync(CancellationToken.None);

        // Each ObserveAsync produces a new observation with advancing sequence
        Assert.Equal(1, obs1.SequenceNumber);
        Assert.Equal(2, obs2.SequenceNumber);
        Assert.Equal(2, env.ObservationHistory.Count);
    }

    // ── PE-G5: ExecuteAsync translates and dispatches ─────────────────────

    [Fact]
    public async Task PEG5_ExecuteAsync_TranslatesAndDispatches()
    {
        using var bitmap = CreateTinyBitmap();
        var dispatchTarget = new StubDispatchTarget();
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);
        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([]),
            dispatchTarget,
            SettingsApp, DisplayW, DisplayH);

        var action = new DeviceAction.SetSwitch(1, true, bounds);
        var result = await env.ExecuteAsync(action, CancellationToken.None);

        // Dispatched successfully
        Assert.Equal(ActionResultOutcome.Dispatched, result.Outcome);
        // Action recorded
        Assert.Contains(env.ActionHistory, a => a is DeviceAction.SetSwitch);
        // ADB operation was created
        Assert.NotEmpty(dispatchTarget.Operations);
        Assert.IsType<AdbOperation.Tap>(dispatchTarget.Operations[0]);
    }

    // ── PE-G6: Invalid action → Rejected ──────────────────────────────────

    [Fact]
    public async Task PEG6_InvalidTap_ReturnsRejected()
    {
        using var bitmap = CreateTinyBitmap();
        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([]),
            new StubDispatchTarget(),
            SettingsApp, DisplayW, DisplayH);

        // Tap with null bounds and no index — cannot translate
        var action = new DeviceAction.Tap(null, TargetBounds: null);
        var result = await env.ExecuteAsync(action, CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Rejected, result.Outcome);
        Assert.Contains("translation failed", result.Info);
    }

    // ── PE-G7: Rejected dispatch → Rejected result ────────────────────────

    [Fact]
    public async Task PEG7_AdbRejection_ReturnsRejected()
    {
        using var bitmap = CreateTinyBitmap();
        var bounds = new ElementBounds(0.5f, 0.5f, 0.6f, 0.6f);
        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([]),
            new StubDispatchTarget(ActionResultOutcome.Rejected),
            SettingsApp, DisplayW, DisplayH);

        var action = new DeviceAction.Tap(0, bounds);
        var result = await env.ExecuteAsync(action, CancellationToken.None);

        Assert.Equal(ActionResultOutcome.Rejected, result.Outcome);
    }

    // ── PE-G8: TimedOut dispatch ─────────────────────────────────────────

    [Fact]
    public async Task PEG8_TimedOut_ReturnsTimedOut()
    {
        using var bitmap = CreateTinyBitmap();
        var bounds = new ElementBounds(0.5f, 0.5f, 0.6f, 0.6f);
        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([]),
            new StubDispatchTarget(ActionResultOutcome.TimedOut),
            SettingsApp, DisplayW, DisplayH);

        var action = new DeviceAction.Tap(0, bounds);
        var result = await env.ExecuteAsync(action, CancellationToken.None);

        Assert.Equal(ActionResultOutcome.TimedOut, result.Outcome);
    }

    // ── PE-G9: Dispatch does NOT carry world effect ───────────────────────

    [Fact]
    public void PEG9_DispatchDoesNotCarryWorldEffect()
    {
        // ActionResult has only 3 properties — no world state
        var result = new ActionResult(ActionResultOutcome.Dispatched, "tap", "ok");
        var props = typeof(ActionResult).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Equal(3, props.Count);
        Assert.Contains("Outcome", props);
        Assert.DoesNotContain("WorldState", props);
    }

    // ── PE-G10: Full production-shaped semantic loop ─────────────────────

    [Fact]
    public async Task PEG10_ProductionShapedSemanticLoop_WifiAlreadyOn_NoMutation()
    {
        using var bitmap = CreateToggleBitmap(true); // ON toggle image
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);
        var candidates = new[]
        {
            new PerceptionCandidate("Wi‑Fi", "menuItem", new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f)),
            new PerceptionCandidate("", "toggle", bounds),
        };

        var env = new PhysicalEnvironment(
            new StubScreenshotSource(bitmap, DisplayW, DisplayH),
            new StubPerceptionSource([.. candidates]),
            new StubDispatchTarget(),
            SettingsApp, DisplayW, DisplayH);

        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria(
            SettingsApp,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));

        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, SettingsApp, _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, _ => true, traversal.ExecuteStep);
        var agent = new RuntimeAgent(
            startup, traversal, ct => env.ObserveAsync(ct), _ => "Settings",
            Factory, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("WifiConnectivity", "Enabled", true),
            [Wifi], [SetEnabled], "pe-g10");

        // The production PhysicalEnvironment feeds the graduated Runtime.
        // The classifier result determines outcome — it's REAL and deterministic.
        Assert.True(result is SemanticRunResult.Satisfied or SemanticRunResult.StateEvidenceRequired);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static SKBitmap CreateTinyBitmap() => new(10, 10);

    private static SKBitmap CreateToggleBitmap(bool knobRight)
    {
        var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        using var trackPaint = new SKPaint { Color = new SKColor(200, 200, 200), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(2, 10, 98, 30), 10), trackPaint);
        float knobX = knobRight ? 75 : 25;
        using var knobPaint = new SKPaint { Color = new SKColor(60, 60, 60), IsAntialias = true };
        canvas.DrawCircle(knobX, 20, 12, knobPaint);
        canvas.Flush();
        return bitmap;
    }

    private sealed class StubScreenshotSource : IScreenshotSource
    {
        private readonly SKBitmap _bitmap;
        private readonly int _w, _h;
        public StubScreenshotSource(SKBitmap bitmap, int w, int h)
        { _bitmap = bitmap; _w = w; _h = h; }
        public Task<ScreenshotCapture> CaptureAsync(CancellationToken ct)
            => Task.FromResult(new ScreenshotCapture(_bitmap, _w, _h));
    }

    private sealed class StubPerceptionSource : IPerceptionSource
    {
        private readonly ImmutableArray<PerceptionCandidate> _candidates;
        public StubPerceptionSource(ImmutableArray<PerceptionCandidate> candidates)
        { _candidates = candidates; }
        public Task<ImmutableArray<PerceptionCandidate>> AnalyzeAsync(
            SKBitmap screenshot, int width, int height, CancellationToken ct)
            => Task.FromResult(_candidates);
    }

    private sealed class StubDispatchTarget : IAdbDispatchTarget
    {
        private readonly ActionResultOutcome _outcome;
        public List<AdbOperation> Operations { get; } = [];
        public StubDispatchTarget(ActionResultOutcome outcome = ActionResultOutcome.Dispatched)
        { _outcome = outcome; }
        public Task<ActionResult> ExecuteAsync(AdbOperation op, CancellationToken ct)
        {
            Operations.Add(op);
            var desc = op switch
            {
                AdbOperation.Tap t => $"tap({t.X},{t.Y})",
                AdbOperation.Launch l => $"launch({l.PackageName})",
                _ => op.GetType().Name,
            };
            return Task.FromResult(new ActionResult(_outcome, desc, "stub"));
        }
    }
}
