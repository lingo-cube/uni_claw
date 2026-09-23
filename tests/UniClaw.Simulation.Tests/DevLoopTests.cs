using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G1：原 UniClaw.Host.Tests 端到端回归迁入（HostRunner 剥离仿真
/// 档后，这些断言经 DevLoopRunner 组合同一 Kernel 真件承载——双 Host
/// 对称）。v0 默认档 + 感知锚回放档；无门控（确定性资产）。
/// 原 HOST-001 验收语义保持：完整最小闭环（观察→决策→effect→Outcome）
/// + journal 必注入产物 + 仿真可复现（两次 run digest 一致）。
/// </summary>
public sealed class DevLoopTests
{
    private static string TempRoot(string prefix)
        => Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");

    private static DevLoopRunner.MakeFeed V0Feed(string targetState = "on") =>
        clock => (new V0Runtime.FrameFeed(clock, targetState: targetState).Next, null);

    [Fact]
    public void V0FullLoop_DeliversOnce_WritesAllArtifacts()
    {
        var root = TempRoot("uniclaw-dev-v0");
        try
        {
            var result = DevLoopRunner.RunOnce(root, V0Feed(), runName: "v0");

            // 闭环证据（原 HOST-001 Acceptance #1/#2）：Completed + Completion
            // outcome + journal 有 pre-dispatch 记录
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);
            Assert.Single(result.ReceiptOutcomes);
            Assert.Equal("DeliveryCompleted", result.ReceiptOutcomes[0]);
            Assert.True(result.JournalBytes > 0, "journal 应有 pre-dispatch 记录");
            Assert.True(File.Exists(Path.Combine(result.RunDir, "exec.journal")));
            Assert.True(File.Exists(Path.Combine(result.RunDir, "trace.json")));
            Assert.True(File.ReadAllText(Path.Combine(result.RunDir, "facts.json")).Length > 0);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void V0IsReproducible_TwoRunsSameDigest()
    {
        var rootA = TempRoot("uniclaw-dev-v0-a");
        var rootB = TempRoot("uniclaw-dev-v0-b");
        try
        {
            var a = DevLoopRunner.RunOnce(rootA, V0Feed(), runName: "a");
            var b = DevLoopRunner.RunOnce(rootB, V0Feed(), runName: "b");

            Assert.Equal(a.Status, b.Status);
            Assert.Equal(a.ReceiptOutcomes, b.ReceiptOutcomes);
            Assert.Equal(a.FactsDigest, b.FactsDigest); // 仿真流程=正式能力：可复现
        }
        finally
        {
            if (Directory.Exists(rootA)) Directory.Delete(rootA, recursive: true);
            if (Directory.Exists(rootB)) Directory.Delete(rootB, recursive: true);
        }
    }

    // ---- 感知锚回放档（原 HostReplayTests）----------------------------

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

    private static DevLoopRunner.MakeFeed ReplayFeed(string targetState = "on") =>
        clock => (new ReplayPerception.ReplayFrameFeed(clock, WifiAssets, targetState).Next, null);

    [Fact]
    public void ReplayAnchors_DriveFullLoopToCompletion()
    {
        var root = TempRoot("uniclaw-dev-replay");
        try
        {
            var result = DevLoopRunner.RunOnce(root, ReplayFeed("on"), targetState: "on");

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
        var rootA = TempRoot("uniclaw-dev-replay-a");
        var rootB = TempRoot("uniclaw-dev-replay-b");
        try
        {
            var a = DevLoopRunner.RunOnce(rootA, ReplayFeed());
            var b = DevLoopRunner.RunOnce(rootB, ReplayFeed());

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
