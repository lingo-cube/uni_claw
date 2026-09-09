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
/// DSE-002 验收（E1–E6）—— DeliveryTarget / EB lowering seam / 首个
/// AdbEffectDriver（dry-run）。E1 使用 PER-002 golden-run 真实检测框像素
/// （reset_button "48,140,1032,188" @1080×1920，FastPerceptionSliceTests
/// 同源数字），测试侧按真实设备尺寸归一化。核心 invariant：
/// WorldModel 认目标，Effect Boundary 出地址，Driver 只送货。
/// </summary>
public sealed class DeliveryTargetAdbTests
{
    private const string SeedValue = "dse-002-delivery-target-seed";
    private static readonly DateTimeOffset FixedTime = new(2026, 9, 9, 14, 0, 0, TimeSpan.Zero);

    // golden-run 真实资产（1080×1920 设备）——corpus 像素 → 归一化
    private const int RealW = 1080, RealH = 1920;
    private static readonly (int X1, int Y1, int X2, int Y2) ResetButtonPx = (48, 140, 1032, 188);
    private static SpatialLocator RealLocator() => new(
        ResetButtonPx.X1 / (double)RealW, ResetButtonPx.Y1 / (double)RealH,
        ResetButtonPx.X2 / (double)RealW, ResetButtonPx.Y2 / (double)RealH,
        AdbEffectDriver.SupportedFrame);

    // ---- doubles / 组装 --------------------------------------------------

    private sealed class LocatedObservationStrategy(string owner, SpatialLocator locator)
        : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[] { new ProposedOccurrence(owner, "button", "reset_button", State: null, Locator: locator) };
    }

    private sealed class LocatorlessObservationStrategy(string owner) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            new[] { new ProposedOccurrence(owner, "button", "reset_button") };
    }

    private static string ProbeContainerId() =>
        "ctr-" + new EvidenceLedger()
            .Admit(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0))
            .Admission.EvidenceId![3..15];

    private static (UniKernel Kernel, EffectBoundary Effects) NewKernel(
        IUiObservationStrategy observation, IEffectDriver driver)
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(), observation);
        var effects = new EffectBoundary(driver);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(new DescriptorTargetPolicy(
                new[] { new TargetSpec("button", "reset_button", "tap") })),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()), effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1", Objective: "tap-reset",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "tapped" }));
        return (kernel, effects);
    }

    private static void Prime(UniKernel kernel) =>
        kernel.Process(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0));

    private static EffectReceipt ActOnce(UniKernel kernel)
    {
        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var act = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("button", "reset_button"));
        return act.Act!.Receipt ?? throw new InvalidOperationException("dispatch 未发生");
    }

    // ---- E1 端到端：corpus 真实 bounds → adb 命令串 -----------------------

    [Fact]
    public void E1_RealCorpusBoundsFlowToExactAdbTapCommand()
    {
        var owner = ProbeContainerId();
        var driver = new AdbEffectDriver(RealW, RealH, () => FixedTime);
        var (kernel, effects) = NewKernel(new LocatedObservationStrategy(owner, RealLocator()), driver);
        Prime(kernel);

        var receipt = ActOnce(kernel);

        // center = ((48+1032)/2, (140+188)/2) = (540, 164) —— 真实投影
        Assert.Equal(DispatchOutcome.DeliveryCompleted, receipt.Outcome);
        Assert.Equal("adb shell input tap 540 164", receipt.Report);
        // binding 携带了 locator（EB 出地址）；reference 仅溯源
        Assert.NotNull(effects.BindingLog.Single().Canonical!.TargetLocator);
        Assert.Equal("device-viewport", receipt.TargetSubject == null ? null
            : effects.BindingLog.Single().Canonical!.TargetLocator!.SpatialFrameId);
    }

    // ---- E2 规则 A：构造级拒绝 -------------------------------------------

    [Theory]
    [InlineData(-0.1, 0, 1, 1)]        // 越界
    [InlineData(0.6, 0, 0.4, 1)]        // X1 > X2
    [InlineData(0, 0.7, 1, 0.3)]        // Y1 > Y2
    public void E2_InvalidBoundsRejectedAtConstruction(double x1, double y1, double x2, double y2) =>
        Assert.Throws<ArgumentException>(
            () => new SpatialLocator(x1, y1, x2, y2, "device-viewport"));

    [Fact]
    public void E2b_SpatialWithoutFrameIsInvalidPayload() =>
        Assert.Throws<ArgumentException>(
            () => new SpatialLocator(0, 0, 1, 1, " ")); // P-UW-16：无 frame 的空间值 = 无效载荷

    // ---- E3 规则 B：driver 支持集执法 ------------------------------------

    [Fact]
    public void E3a_MissingLocatorFailsClosed()
    {
        var owner = ProbeContainerId();
        var (kernel, _) = NewKernel(new LocatorlessObservationStrategy(owner),
            new AdbEffectDriver(RealW, RealH, () => FixedTime));
        Prime(kernel);

        var receipt = ActOnce(kernel);

        Assert.Equal(DispatchOutcome.DeliveryFailed, receipt.Outcome);
        Assert.StartsWith("no-executable-locator", receipt.Reason);
    }

    [Fact]
    public void E3b_UnsupportedFrameFailsClosed()
    {
        var owner = ProbeContainerId();
        var locator = new SpatialLocator(0, 0, 1, 1, "container-viewport:c1");
        var (kernel, _) = NewKernel(new LocatedObservationStrategy(owner, locator),
            new AdbEffectDriver(RealW, RealH, () => FixedTime));
        Prime(kernel);

        var receipt = ActOnce(kernel);

        Assert.Equal(DispatchOutcome.DeliveryFailed, receipt.Outcome);
        Assert.StartsWith("unsupported-frame", receipt.Reason);
    }

    [Fact]
    public void E3c_UnsupportedEffectFailsClosed()
    {
        var owner = ProbeContainerId();
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(),
            new LocatedObservationStrategy(owner, RealLocator()));
        var driver = new AdbEffectDriver(RealW, RealH, () => FixedTime);
        var effects = new EffectBoundary(driver);
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(new DescriptorTargetPolicy(
                new[] { new TargetSpec("button", "reset_button", "swipe") })),
            new RuntimeAssurance(new FreshnessDoubles.Satisfying()), effects);
        kernel.AdmitContract(new ExecutionContract(
            Version: "c1", Objective: "swipe",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "swipe" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "swiped" }));
        kernel.Process(UIWorldDoubles.Observation(SeedValue, UIWorldDoubles.T0));

        var intent = kernel.SelectIntent(kernel.DeriveSlice(ProbeContainerId()));
        var act = kernel.ActViaCurrentGrounding(intent, new TargetDescriptor("button", "reset_button"));

        Assert.Equal(DispatchOutcome.DeliveryFailed, act.Act!.Receipt!.Outcome);
        Assert.StartsWith("unsupported-effect", act.Act.Receipt.Reason);
    }

    // ---- E4 invariant：reference ≠ locator，authority 不达 driver --------

    [Fact]
    public void E4_ReferenceIdentityIsNotExecutableLocator()
    {
        var request = new DispatchRequest(
            new DeliveryTarget("occ-xyz", RealLocator()), "tap", null, "rev-1");

        // 结构事实：driver 可见类型只有 DeliveryTarget/SpatialLocator——
        // occurrence id 无解析路径（规则 C：足够笨）
        Assert.Equal("occ-xyz", request.Target.OccurrenceReference);
        Assert.NotNull(request.Target.Spatial);
        // DispatchRequest 四字段延续（Target 现为 DeliveryTarget；无 IntentId/BindingId）
        var names = typeof(DispatchRequest).GetProperties()
            .Select(p => p.Name).ToHashSet();
        Assert.Equal(new HashSet<string> { "Target", "EffectClass", "Parameters", "RevisionId" }, names);
    }

    // ---- E5 归一化跨 viewport 稳定 ----------------------------------------

    [Theory]
    [InlineData(540, 164, 1080, 1920)]  // 真实设备：center (540,164)
    [InlineData(540, 410, 1080, 4800)]  // 同 locator × 4K 长屏 → 同 x、按比例 y
    [InlineData(270, 82, 540, 960)]     // 半分辨率 → 半坐标
    public void E5_NormalizedLocatorProjectsAcrossViewports(int expectedX, int expectedY, int w, int h)
    {
        var driver = new AdbEffectDriver(w, h, () => FixedTime);
        var result = driver.Deliver(new DispatchRequest(
            new DeliveryTarget("occ-ref", RealLocator()), "tap", null, "rev-1"));
        Assert.Equal($"adb shell input tap {expectedX} {expectedY}", result.Report);
    }

    // ---- E6 字符串通道 locatorless 回归 -----------------------------------

    [Fact]
    public void E6_StringChannelLocatorlessDeliveryTargetPassesDoubles()
    {
        var driver = new ScriptedOkDriver();
        var result = driver.Deliver(new DispatchRequest(
            new DeliveryTarget("screen.home"), "tap", "idle", "rev-1"));
        Assert.Equal(DispatchOutcome.DeliveryCompleted, result.Outcome);
    }

    private sealed class ScriptedOkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", FixedTime);
    }
}
