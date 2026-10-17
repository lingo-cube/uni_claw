using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// TRAJECTORY-001 D3 — 进度形式化（清障验证不算 Coverage Progress）的
/// 纯函数测试：obstacle 类 directive（"Clear the popup" / "Dismiss the
/// popup" 前缀）的步骤即使 Verified=true 也不计入覆盖进度指纹的推进轴；
/// enter 步骤（Verified）计入。
/// </summary>
public sealed class CoverageProgressTests
{
    private static readonly SettingsCoverageConfig Config =
        SettingsCoverageConfig.LoadDefault();

    private static CoverageStepRecord Step(string directive, bool verified) => new(
        Index: 1,
        DecisionId: "d-1",
        DshSessionId: null,
        TargetRole: "ui.element",
        TargetDescriptor: "CANCEL",
        EffectClass: "tap",
        DesiredState: null,
        PreActionTargetUnique: true,
        ReceiptId: "r-1",
        ReceiptOutcome: "success",
        ReceiptCommand: "input tap 1 1",
        RouteBefore: "root",
        RouteAfter: "root",
        PostCaptureId: "cap-1",
        Verified: verified,
        FailureReason: null,
        Directive: directive);

    [Fact]
    public void ObstacleClearStep_Verified_DoesNotCountAsCoverageProgress()
    {
        Assert.False(SettingsCoverageLedger.CountsAsCoverageProgress(
            Step("Clear the popup overlay by tapping '<CANCEL>' before resuming traversal.", verified: true),
            Config));
    }

    [Fact]
    public void ObstacleDismissBackStep_Verified_DoesNotCountAsCoverageProgress()
    {
        Assert.False(SettingsCoverageLedger.CountsAsCoverageProgress(
            Step("Dismiss the popup overlay by tapping '<Navigate up>' (back). Confirm-side buttons are not allowed for this task.", verified: true),
            Config));
    }

    [Fact]
    public void EnterStep_Verified_CountsAsCoverageProgress()
    {
        Assert.True(SettingsCoverageLedger.CountsAsCoverageProgress(
            Step("Enter the Settings first-level entry '<Network & internet>' by tapping it.", verified: true),
            Config));
    }

    [Fact]
    public void EnterStep_NotVerified_DoesNotCountAsCoverageProgress()
    {
        Assert.False(SettingsCoverageLedger.CountsAsCoverageProgress(
            Step("Enter the Settings first-level entry '<Network & internet>' by tapping it.", verified: false),
            Config));
    }
}
