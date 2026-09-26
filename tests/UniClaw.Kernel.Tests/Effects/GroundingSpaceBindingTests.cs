using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using Xunit;
using static UniClaw.Kernel.Tests.Effects.AdbViewportResolutionTests;

namespace UniClaw.Kernel.Tests.Effects;

/// <summary>
/// CSC-001 Slice C：grounding 空间绑定执法——grounded space 与设备空间
/// 机械 Matches 才可投影；mismatch（rotation/viewport 变化/过期 capture）→
/// RE-GROUND/RE-OBSERVE 不 dispatch；binding 无空间 → fail-closed。
/// 「旧截图坐标 × 当前设备尺寸直接 tap」自此结构性不可能。
/// </summary>
public class GroundingSpaceBindingTests
{
    private static DispatchRequest TapIn(CoordinateSpace space) => new(
        new DeliveryTarget(
            "occ-1",
            new SpatialLocator(0.45, 0.45, 0.55, 0.55, AdbEffectDriver.SupportedFrame),
            Space: space),
        "tap", null, "rev-1");

    [Fact]
    public void GroundedSpace_MatchesDevice_ProjectsAndDelivers()
    {
        var runner = new FakeRunner(); // wm = Override 1080×1920
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        var result = driver.Deliver(TapIn(CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-1")));

        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
        Assert.Contains("input tap 540 960", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewportMismatch_ZeroEffect_ReGroundDiagnostic()
    {
        // E4 形态：grounded（capture 实测）1080×1920 vs 设备 1080×2400
        var runner = new FakeRunner { WmOutput = "Physical size: 1080x2400\n" };
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        var result = driver.Deliver(TapIn(CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-1")));

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("coordinate-space-mismatch", result.Reason, StringComparison.Ordinal);
        Assert.Contains("RE-GROUND", result.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("input tap", result.Report, StringComparison.Ordinal); // zero effect
    }

    [Fact]
    public void RotationChange_ZeroEffect_ReGroundDiagnostic()
    {
        // 设备旋转后（landscape 实况）持旧 portrait grounding dispatch → 拒绝
        var runner = new FakeRunner { WmOutput = "Override size: 1920x1080\n" };
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        var result = driver.Deliver(TapIn(CoordinateSpace.DeviceViewport(
            1080, 1920, ScreenRotation.None, captureId: "cap-old")));

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("coordinate-space-mismatch", result.Reason, StringComparison.Ordinal);
        Assert.Contains("device-viewport:1920x1080", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void BindingWithoutSpace_FailClosed_ReObserve()
    {
        // legacy claim（缺 w/h）→ occurrence/binding 无空间 → 不可验证 → 拒绝
        var runner = new FakeRunner();
        var driver = new AdbLiveEffectDriver("emulator-5554", 1080, 1920, runner: runner);

        var request = new DispatchRequest(
            new DeliveryTarget(
                "occ-legacy",
                new SpatialLocator(0.45, 0.45, 0.55, 0.55, AdbEffectDriver.SupportedFrame)),
            "tap", null, "rev-1");
        var result = driver.Deliver(request);

        Assert.Equal(DispatchOutcome.DeliveryFailed, result.Outcome);
        Assert.Contains("coordinate-space-unknown-on-target", result.Reason, StringComparison.Ordinal);
        Assert.Contains("RE-OBSERVE", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportSetRejections_NotShadowedBySpaceChecks()
    {
        // frozen 语义保持：locator 缺席 / frame 不在支持集，仍是支持集拒绝
        var runner = new FakeRunner();
        var driver = new AdbLiveEffectDriver("emulator-5554", null, null, runner: runner);

        var noLocator = new DispatchRequest(new DeliveryTarget("occ"), "tap", null, "rev");
        Assert.Contains("no-executable-locator", driver.Deliver(noLocator).Reason, StringComparison.Ordinal);

        var wrongFrame = new DispatchRequest(
            new DeliveryTarget("occ",
                new SpatialLocator(0.1, 0.1, 0.2, 0.2, "artifact"),
                Space: CoordinateSpace.DeviceViewport(1080, 1920)),
            "tap", null, "rev");
        Assert.Contains("unsupported-frame", driver.Deliver(wrongFrame).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Chain_SpaceFlowsFromOccurrenceToDispatchRequest()
    {
        // 链路完整性：ProposedOccurrence.Space → OccurrenceBelief → BindingView
        // → CanonicalBinding → DeliveryTarget（经 WorldModel/EffectBoundary 真件）
        var space = CoordinateSpace.DeviceViewport(1080, 1920, captureId: "cap-chain");
        var frame = "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.4,0.4,0.6,0.6],\"w\":1080,\"h\":1920,\"f\":\"device-viewport\"}";
        var ledger = new EvidenceLedger();
        var (_, record) = ledger.Admit(new ObservationProposal(
            new ObservationClaim("screen.frame", frame),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("test", DateTimeOffset.Now, "scope:screen.frame", new[] { "t" })));
        var world = new WorldModel(
            new HashSet<string> { "screen.frame" },
            observationStrategy: new ChainFrameStrategy());
        var judgment = world.JudgeRelevance(record!);
        world.Reconcile(record!, judgment);

        var view = world.DeriveBindingView(subject: null, occurrenceId: world.Current!.Occurrences[0].OccurrenceId);
        Assert.NotNull(view.TargetOccurrenceSpace);
        Assert.Equal(space.CoordinateSpaceId, view.TargetOccurrenceSpace!.CoordinateSpaceId);
    }

    private sealed class ChainFrameStrategy : UniClaw.Kernel.World.UiRealization.IUiObservationStrategy
    {
        public IReadOnlyList<UniClaw.Kernel.World.UiRealization.ProposedOccurrence> Derive(
            UniClaw.Kernel.Evidence.EvidenceRecord record,
            UniClaw.Kernel.World.WorldBeliefRevision? previous)
        {
            using var document = System.Text.Json.JsonDocument.Parse(record.Claim.Value);
            var entry = document.RootElement;
            var bounds = entry.GetProperty("b").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            return new[]
            {
                new UniClaw.Kernel.World.UiRealization.ProposedOccurrence(
                    OwningContainerId: null,
                    Role: entry.GetProperty("role").GetString()!,
                    SemanticDescriptor: null,
                    State: entry.GetProperty("state").GetString(),
                    Locator: new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3], entry.GetProperty("f").GetString()!),
                    Space: CoordinateSpace.DeviceViewport(
                        entry.GetProperty("w").GetInt32(), entry.GetProperty("h").GetInt32())),
            };
        }
    }
}
