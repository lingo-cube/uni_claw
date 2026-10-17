using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

[Collection("SettingsCoverageConfigSerial")]
public sealed class SettingsCoverageStagnationTests
{
    private const string RootRoute = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private const string PopupClearDescriptor = "com.android.settings:id/composer_container";

    private static readonly string[] TargetPages =
    [
        "Network & internet",
        "Connected devices",
        "Apps",
    ];

    private static void Observe(SettingsCoverageLedger ledger, string route, params string[] descriptors) =>
        ledger.RecordObservation(route,
            descriptors.Select(descriptor => ("ui.element", (string?)descriptor)).ToList());

    private static void VerifiedTap(SettingsCoverageLedger ledger, string descriptor) =>
        ledger.RecordStep(new CoverageStepRecord(
            Index: 0,
            DecisionId: $"decision-{Guid.NewGuid():N}",
            DshSessionId: "dsh-session-1",
            TargetRole: "ui.element",
            TargetDescriptor: descriptor,
            EffectClass: "tap",
            DesiredState: null,
            PreActionTargetUnique: true,
            ReceiptId: $"receipt-{Guid.NewGuid():N}",
            ReceiptOutcome: "ok",
            ReceiptCommand: "tap",
            RouteBefore: RootRoute,
            RouteAfter: RootRoute,
            PostCaptureId: $"capture-{Guid.NewGuid():N}",
            Verified: true,
            FailureReason: null,
            Directive: null));

    /// <summary>TRAJECTORY-001 D3 裁决：停滞判定 = (entered+reached, frontier)
    /// 指纹不变。首快照（prior=null）不计停滞。</summary>
    [Fact]
    public void Stagnation_FirstSnapshot_NeverCounts()
    {
        Assert.False(SettingsCoverageRunner.ShouldIncrementStagnation(null, (0, 3)));
    }

    [Theory]
    [InlineData(0, 3, 0, 3, true)]
    [InlineData(1, 3, 1, 3, true)]
    [InlineData(0, 3, 1, 3, false)]
    [InlineData(0, 3, 0, 2, false)]
    public void Stagnation_FingerprintEquality_Decides(
        int priorEntered, int priorFrontier, int entered, int frontier, bool expected)
    {
        Assert.Equal(expected, SettingsCoverageRunner.ShouldIncrementStagnation(
            (priorEntered, priorFrontier), (entered, frontier)));
    }

    /// <summary>SIM 形停滞回放（RUN-006 F8）：进根页观察后陷入弹窗清障循环——
    /// 每个清障 tap 都验证成功（StepsVerified 恒涨），弹窗声明 present/absent
    /// 交替，但覆盖事实（entered+reached, frontier）不变。新指纹连续相等必须
    /// 累计到 StagnationInjectConsults（默认 3）；旧指纹 (StepsVerified,
    /// frontier) 在同一输入下永远不会触发——这正是修复前停滞不可达的根因。</summary>
    [Fact]
    public void SimPopupLoop_NewFingerprintTripsStagnation_OldFingerprintNeverDoes()
    {
        var ledger = new SettingsCoverageLedger();
        var config = SettingsCoverageConfig.LoadDefault();
        Observe(ledger, RootRoute, TargetPages);

        (int Entered, int Frontier) Fingerprint()
        {
            var entered = ledger.TargetMarks.Values.Count(mark =>
                mark.Kind is TargetCoverageMarkKind.RouteEntered
                    or TargetCoverageMarkKind.ReachedOverlay);
            var frontier = ledger.DynamicRootFrontier(config).Count
                + ledger.DynamicNestedFrontier(config).Count;
            return (entered, frontier);
        }

        var prior = Fingerprint();
        var stagnantCycles = 0;
        var priorStepsVerified = 0;
        (int StepsVerified, int Frontier)? oldPrior = null;
        var oldFingerprintStagnant = false;

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            // 每周期一个验证成功的清障 tap + present/absent 交替弹窗声明。
            VerifiedTap(ledger, PopupClearDescriptor);
            ledger.RecordPopupState(cycle % 2 == 1 ? "present" : "absent");

            var current = Fingerprint();
            // 覆盖事实静止：entered/reached 与 frontier 不变。
            Assert.Equal(prior, current);
            // 每步都"验证成功"：StepsVerified 恒涨（旧指纹因此永远不相等）。
            var stepsVerified = ledger.Snapshot(config).StepsVerified;
            Assert.True(stepsVerified > priorStepsVerified);
            priorStepsVerified = stepsVerified;

            if (SettingsCoverageRunner.ShouldIncrementStagnation(prior, current))
                stagnantCycles++;
            else
                stagnantCycles = 0;
            prior = current;

            // 对照：旧指纹 (StepsVerified, frontier) 在同一回放下从不相等，
            // 停滞永远不触发（RUN-006 F8 修复前根因）。
            var oldCurrent = (stepsVerified, current.Frontier);
            if (oldPrior is { } p && p.Equals(oldCurrent))
                oldFingerprintStagnant = true;
            oldPrior = oldCurrent;
        }

        // 修复后：第 3 周期计数到 3（默认 StagnationInjectConsults）→ 介入触发。
        Assert.Equal(config.Bounds.StagnationInjectConsults, stagnantCycles);
        Assert.False(oldFingerprintStagnant);
    }
}
