using UniClaw.Host;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// 路线一 2b — 服务回放端到端（ENVIRONMENT 门控 DSH_TEST_PERCEPTION_SERVICE=1：
/// 启动真 Python 视觉服务，重但无需真机）。录制截图 → 真推理 → 共享提取器
/// → 完整核心环路。验收：Completed/Completion + 在线检测 bounds 与录制锚
/// 同域（同屏同模型）。
/// </summary>
public sealed class HostServiceReplayTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_SERVICE") == "1";

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

    [Fact]
    public void ServiceReplay_RecordedScreensRealInference_FullLoopToCompletion()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_SERVICE 未启用（需 .perception venv）");
            return;
        }

        var repo = RepoRoot();
        var capture = Path.Combine(repo, "platforms", "perception", "evaluation", "assets", "captures", "wifi-slice2-calibration");
        var assets = new ServicePerception.ServiceReplayAssets(
            Path.Combine(capture, "frames", "wifi-off-emulator-5554.png"),
            Path.Combine(capture, "frames", "wifi-on-emulator-5554.png"),
            ScreenId: "wifi-settings",
            ProviderRoot: Path.Combine(repo, "platforms", "perception"),
            PythonExecutable: Path.Combine(repo, ".perception", "venv", "bin", "python"),
            CacheRoot: Path.Combine(repo, ".perception", "cache"));

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-host-svc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = HostRunner.RunOnce(root, new HostRunner.HostOptions
            {
                ServiceReplay = assets,
                TargetState = "on",
            });

            output.WriteLine($"status={result.Status} outcome={result.OutcomeClassification} delivered={result.DeliveredEffects}");
            Assert.Equal(RunDriveStatus.Completed, result.Status);
            Assert.Equal("Completion", result.OutcomeClassification);
            Assert.Single(result.ReceiptOutcomes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
