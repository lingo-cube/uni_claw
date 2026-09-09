using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// DSE-003 验收（N1–N4）—— NativeLocator + 第二后端 EgoBrowserEffectDriver
/// （dry-run）+ 双 driver 交叉。核心断言：delivery form 由 driver 支持集
/// 声明决定——多 locator 并存时各 driver 只消费自身支持集，永不挑选/
/// fallback（规则 B/C 行为面）。
/// </summary>
public sealed class EgoBrowserDeliveryTests
{
    private const string SeedValue = "dse-003-native-locator-seed";
    private static readonly DateTimeOffset FixedTime = new(2026, 9, 9, 16, 0, 0, TimeSpan.Zero);

    // golden-run 真实像素（DeliveryTargetAdbTests 同源）：归一化 spatial
    private static readonly SpatialLocator RealSpatial = new(
        48 / 1080.0, 140 / 1920.0, 1032 / 1080.0, 188 / 1920.0,
        AdbEffectDriver.SupportedFrame);
    private static readonly NativeLocator DomNode = NativeLocator.BrowserNode("827");

    // ---- doubles / 组装 ---------------------------------------------------

    private sealed class DualLocatorObservationStrategy(string owner) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[] { new ProposedOccurrence(owner, "button", "target", State: null,
                Locator: RealSpatial, Native: DomNode) };
    }

    private sealed class SpatialOnlyObservationStrategy(string owner) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[] { new ProposedOccurrence(owner, "button", "target", Locator: RealSpatial) };
    }

    private static string ProbeContainerId() =>
        "ctr-" + new EvidenceLedger()
            .Admit(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0))
            .Admission.EvidenceId![3..15];

    private static UniKernel NewKernel(IUiObservationStrategy observation, IEffectDriver driver)
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(), observation);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(new DescriptorTargetPolicy(
                new[] { new TargetSpec("button", "target", "click") })),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            new EffectBoundary(driver));
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1", Objective: "click-target",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "click" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "clicked" }));
        return kernel;
    }

    private static EffectReceipt ActOnce(UniKernel kernel)
    {
        kernel.Process(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0));
        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var act = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("button", "target"));
        return act.Act!.Receipt ?? throw new InvalidOperationException("dispatch 未发生");
    }

    // ---- N1 端到端：native locator → ego click 命令 ----------------------

    [Fact]
    public void N1_NativeLocatorFlowsToEgoClickCommand()
    {
        var owner = ProbeContainerId();
        var kernel = NewKernel(new DualLocatorObservationStrategy(owner),
            new EgoBrowserEffectDriver(() => FixedTime));

        var receipt = ActOnce(kernel);

        Assert.Equal(DispatchOutcome.DeliveryCompleted, receipt.Outcome);
        Assert.Equal("ego click node:827", receipt.Report);
    }

    // ---- N2 双 driver 交叉：同一 DeliveryTarget 各得其所 ------------------

    [Fact]
    public void N2_SameDeliveryTargetServesBothDriversBySupportSet()
    {
        var owner = ProbeContainerId();
        var request = new DispatchRequest(
            new DeliveryTarget("occ-ref", RealSpatial, DomNode), "click", null, "rev-1");

        // adb 只消费 spatial（native 在场不感知）；ego 只消费 native
        var adb = new AdbEffectDriver(1080, 1920, () => FixedTime).Deliver(request);
        var ego = new EgoBrowserEffectDriver(() => FixedTime).Deliver(request);

        Assert.Equal(DispatchOutcome.DeliveryCompleted, adb.Outcome);
        Assert.Equal("adb shell input tap 540 164", adb.Report);   // spatial → pixel
        Assert.Equal(DispatchOutcome.DeliveryCompleted, ego.Outcome);
        Assert.Equal("ego click node:827", ego.Report);            // native → node
    }

    // ---- N3 无 fallback（规则 C 行为面）------------------------------------

    [Fact]
    public void N3a_SpatialOnlyTargetFailsClosedOnEgo_NeverFallsBackToCoordinates()
    {
        var owner = ProbeContainerId();
        var kernel = NewKernel(new SpatialOnlyObservationStrategy(owner),
            new EgoBrowserEffectDriver(() => FixedTime));

        var receipt = ActOnce(kernel);

        Assert.Equal(DispatchOutcome.DeliveryFailed, receipt.Outcome);
        Assert.StartsWith("no-executable-locator", receipt.Reason);
        Assert.DoesNotContain("tap", receipt.Report, StringComparison.Ordinal); // 无坐标猜测
    }

    [Fact]
    public void N3b_NativeOnlyTargetFailsClosedOnAdb()
    {
        var request = new DispatchRequest(
            new DeliveryTarget("occ-ref", Spatial: null, Native: DomNode), "click", null, "rev-1");
        var adb = new AdbEffectDriver(1080, 1920, () => FixedTime).Deliver(request);

        Assert.Equal(DispatchOutcome.DeliveryFailed, adb.Outcome);
        Assert.StartsWith("no-executable-locator", adb.Reason);    // adb 不认 native
    }

    // ---- N4 支持集失败族 ---------------------------------------------------

    [Fact]
    public void N4a_UnsupportedNativeKindFailsClosed()
    {
        var request = new DispatchRequest(
            new DeliveryTarget("occ-ref", Native: new NativeLocator("android.resource-id", "id/switch")),
            "click", null, "rev-1");
        var ego = new EgoBrowserEffectDriver(() => FixedTime).Deliver(request);

        Assert.Equal(DispatchOutcome.DeliveryFailed, ego.Outcome);
        Assert.StartsWith("unsupported-native-kind", ego.Reason);
    }

    [Fact]
    public void N4b_UnsupportedEffectFailsClosed()
    {
        var request = new DispatchRequest(
            new DeliveryTarget("occ-ref", Native: DomNode), "scroll", null, "rev-1");
        var ego = new EgoBrowserEffectDriver(() => FixedTime).Deliver(request);

        Assert.Equal(DispatchOutcome.DeliveryFailed, ego.Outcome);
        Assert.StartsWith("unsupported-effect", ego.Reason);
    }
}
