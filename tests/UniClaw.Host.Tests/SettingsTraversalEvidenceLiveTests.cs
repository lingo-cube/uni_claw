using UniClaw.Host;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-011 §4 — Wi-Fi 闭环（HostRunner SettingsTraversal 模式）的证据
/// 持久化活体验证：真实设备 + 真实 feed + 确定性咨询 double。断言 run
/// 目录落有 evidence/{captureId}.png 与 .xml（AGT-008 同款工件）。
/// 门控 DSH_TEST_PERCEPTION_LIVE=1（与 HostLiveFullTests 同纪律）。
/// </summary>
[Collection("LiveDevice")]
public sealed class SettingsTraversalEvidenceLiveTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1";

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
    public void SettingsTraversalMode_PersistsEvidenceArtifacts()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用");
            return;
        }
        var repo = RepoRoot();
        var selected = LiveDeviceSelector.Resolve();
        Assert.True(selected.IsUsable, $"{selected.Status}: {selected.Detail}");
        var deviceId = selected.Serial!;

        var info = new System.Diagnostics.ProcessStartInfo(
            "adb", $"-s {deviceId} shell am start -S -a android.settings.SETTINGS")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        using var start = System.Diagnostics.Process.Start(info)!;
        start.WaitForExit();
        Thread.Sleep(TimeSpan.FromSeconds(3));

        var root = Path.Combine(Path.GetTempPath(), "uniclaw-st-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = HostRunner.RunOnce(root, new HostRunner.HostOptions
            {
                DeviceId = deviceId,
                // 确定性 double：首轮直接 NoAction（无 completion）→ 终局
                // TerminalNotProven；本测试只证明证据工件落盘，不证明任务完成。
                ConsultAgent = context => new AgentDecision.NoAction(
                    new AgentNoActionProposal(context.DecisionId, "evidence-wiring-probe")),
                SettingsTraversal = true,
                TargetState = "checked",
                Live = new LivePerception.LiveAssets(
                    deviceId,
                    "wifi-settings",
                    Path.Combine(repo, "platforms", "perception"),
                    Path.Combine(repo, ".perception", "venv", "bin", "python"),
                    Path.Combine(repo, ".perception", "cache")),
            });
            output.WriteLine($"status={result.Status}({result.Reason}) runDir={result.RunDir}");

            var evidenceDir = Path.Combine(result.RunDir, "evidence");
            Assert.True(Directory.Exists(evidenceDir), $"evidence 目录缺失: {evidenceDir}");
            var pngs = Directory.GetFiles(evidenceDir, "*.png");
            var xmls = Directory.GetFiles(evidenceDir, "*.xml");
            Assert.NotEmpty(pngs);
            Assert.NotEmpty(xmls);
            Assert.All(pngs.Concat(xmls), f => Assert.True(new FileInfo(f).Length > 0));
            output.WriteLine($"evidence: {pngs.Length} png, {xmls.Length} xml");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
