using UniClaw.Host;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// 路线一 2a — 感知回放端到端（无门控：确定性锚资产，不依赖服务/真机，
/// 进默认回归）。真实管线输出（录制 YOLO 锚）→ 帧契约 → 完整核心环路。
/// </summary>
public sealed class HostReplayTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static string Anchor(string name) => Path.Combine(
        RepoRoot(), "platforms", "perception", "evaluation", "assets", "captures",
        "wifi-slice2-calibration", "perception", name);

    private static ReplayPerception.ReplayAssets WifiAssets => new(
        Anchor("wifi-off-emulator-5554.json"),
        Anchor("wifi-on-emulator-5554.json"),
        ScreenId: "wifi-settings");

    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), "uniclaw-host-replay-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReplayAnchors_DriveFullLoopToCompletion()
    {
        var root = TempRoot();
        try
        {
            var result = HostRunner.RunOnce(root, new HostRunner.HostOptions
            {
                Replay = WifiAssets,
                TargetState = "on",
            });

            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);
            Assert.Single(result.ReceiptOutcomes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplayIsDeterministic_TwoRunsSameDigest()
    {
        var rootA = TempRoot();
        var rootB = TempRoot();
        try
        {
            var a = HostRunner.RunOnce(rootA, new HostRunner.HostOptions { Replay = WifiAssets });
            var b = HostRunner.RunOnce(rootB, new HostRunner.HostOptions { Replay = WifiAssets });

            Assert.Equal(a.FactsDigest, b.FactsDigest);
        }
        finally
        {
            if (Directory.Exists(rootA)) Directory.Delete(rootA, recursive: true);
            if (Directory.Exists(rootB)) Directory.Delete(rootB, recursive: true);
        }
    }

    [Fact]
    public void RecordedBoundsFlowThrough_FrameCarriesAnchorCoordinates()
    {
        // 录制锚 bounds 原文应进入派发请求（证明帧来自资产而非代码常量）
        var detection = ReplayPerception.Extract(Anchor("wifi-off-emulator-5554.json"));

        Assert.Equal("switch", detection.Label);
        Assert.InRange(detection.X1, 0.80, 0.86);
        Assert.InRange(detection.Y1, 0.38, 0.44);
        Assert.InRange(detection.X2, 0.93, 0.99);
        Assert.InRange(detection.Y2, 0.42, 0.48);
    }
}
