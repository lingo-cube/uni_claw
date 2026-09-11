using System.Diagnostics;
using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests.Perception;

/// <summary>
/// PER-005 ENVIRONMENT 验收（A4/A5）。按 test-emulator 注册约定：默认显式
/// 跳过（DSH_TEST_PERCEPTION_LIVE 未启用 = 测试体不执行，零进程调用）；
/// <c>DSH_TEST_PERCEPTION_LIVE=1</c> 启用，启用后前置缺失（模拟器离线 /
/// adb 缺席 / 环境未拉起）直接 FAIL，不静默跳过（fail-closed）。
/// 生命周期外部管理：模拟器由调用方预先启动（adb devices 应见
/// emulator-5554 device）；感知环境由 tools/perception-env/setup.sh 拉起。
/// 不做性能断言、不依赖时序精度（轮询/等待均为宽松常量）。
/// </summary>
public sealed class PerceptionLiveEnvironmentTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") == "1";

    private static string CorpusRoot => Path.Combine(AppContext.BaseDirectory, "Perception", "Corpus");

    /// <summary>从测试程序集向上定位仓库根（AGENTS.md + UniClaw.Kernel.slnx 标记，任意 cwd）。</summary>
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
        throw new InvalidOperationException("未定位到仓库根（AGENTS.md + UniClaw.Kernel.slnx）");
    }

    private static string ProviderRoot => Path.Combine(RepoRoot(), ".perception", "provider");
    private static string VenvPython => Path.Combine(RepoRoot(), ".perception", "venv", "bin", "python");

    private void RequireEnvironment()
    {
        if (!File.Exists(VenvPython))
            throw new InvalidOperationException(
                $"感知环境未拉起：{VenvPython} 不在场。先运行 bash tools/perception-env/setup.sh");
    }

    private sealed record LiveServiceContext(VisionServiceHost Host, VisionServiceTransport Transport)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            if (Transport is VisionServiceTransport.UnixDomainSocket uds)
            {
                try { File.Delete(uds.SocketPath); } catch (IOException) { }
            }
        }
    }

    private LiveServiceContext StartHost(IReadOnlyDictionary<string, string>? env = null)
    {
        // provider 缓存重定向（sandbox 只允许写 workspace）：ultralytics →
        // matplotlib → fontconfig 需要可写缓存目录，默认 ~/.cache 被拒
        //（实测 Fontconfig error + exit 3）。
        var cacheRoot = Path.Combine(RepoRoot(), ".perception", "cache");
        Directory.CreateDirectory(Path.Combine(cacheRoot, "matplotlib"));
        var environment = new Dictionary<string, string>(env ?? new Dictionary<string, string>())
        {
            ["XDG_CACHE_HOME"] = cacheRoot,
            ["MPLCONFIGDIR"] = Path.Combine(cacheRoot, "matplotlib"),
        };

        var socketPath = Path.Combine(Path.GetTempPath(), $"uniclaw-per005-{Guid.NewGuid():N}.sock");
        var host = new VisionServiceHost(new VisionServiceHostOptions(
            VenvPython,
            ProviderRoot,
            new VisionServiceTransport.UnixDomainSocket(socketPath),
            StartupTimeout: TimeSpan.FromSeconds(180), // 首次模型 warmup（YOLO + OCR）可达分钟级（宽松常量）
            ExtraEnvironment: environment));
        return new LiveServiceContext(host, new VisionServiceTransport.UnixDomainSocket(socketPath));
    }

    // ---- A4：环境验收（/version + warmup + 一次非空推理） -------------------

    [Fact]
    public async Task A4_Environment_ServiceHealthyAndNonEmptyInference()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用（环境由 tools/perception-env/setup.sh 拉起）");
            return;
        }
        RequireEnvironment();

        await using var hostContext = StartHost();
        var startup = await hostContext.Host.StartAsync();
        Assert.True(startup.Healthy, $"启动失败：{startup.Error}\nstderr: {startup.StderrTail}");

        using var client = new VisionServiceClient(hostContext.Transport, timeout: TimeSpan.FromSeconds(120));
        var png = await File.ReadAllBytesAsync(
            Path.Combine(CorpusRoot, "legacy-direct", "golden-run-v1", "case-a-before.png"));
        var image = PngImage.Decode(png);

        var result = await client.AnalyzeAsync(image.Rgba, image.Width, image.Height, CancellationToken.None);
        Assert.True(result.Success, $"推理失败：{result.Diagnostic}");
        using var document = JsonDocument.Parse(result.ResponseJson!);
        var yolo = document.RootElement.GetProperty("yolo").GetArrayLength();
        var ocr = document.RootElement.GetProperty("ocr").GetArrayLength();
        output.WriteLine($"非空推理：yolo={yolo}, ocr={ocr}");
        Assert.True(yolo + ocr > 0,
            $"推理为空（yolo={yolo}, ocr={ocr}）——安装成功 ≠ 运行证明，非空推理是 A4 必要条件");
    }

    [Fact]
    public async Task A4_PaddleConfiguredButAbsent_StartupFailsLoud()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用");
            return;
        }
        RequireEnvironment();

        await using var hostContext = StartHost(env: new Dictionary<string, string>
        {
            ["UNICLAW_OCR_BACKEND"] = "paddle",
        });
        var startup = await hostContext.Host.StartAsync();
        Assert.False(startup.Healthy);
        Assert.NotEmpty(startup.StderrTail); // fail-loud 第一现场（import paddle 失败栈）
    }

    // ---- A5：P-2 现场全链（模拟器截屏 → 服务 → admitted 非空 proposal） -----

    [Fact]
    public async Task A5_LiveFullChain_EmulatorScreenshotToAdmittedNonEmptyProposal()
    {
        if (!Enabled)
        {
            output.WriteLine("跳过：DSH_TEST_PERCEPTION_LIVE 未启用（模拟器生命周期外部管理，见 docs/agents/test-emulator.md）");
            return;
        }
        RequireEnvironment();
        if (Environment.GetEnvironmentVariable("DSH_TEST_NO_ADB") == "1")
        {
            output.WriteLine("跳过：DSH_TEST_NO_ADB=1 显式跳过 ADB 面");
            return;
        }

        // 前置 fail-closed：adb 在场 + 模拟器在线（生命周期外部管理）
        var devices = await RunAdb("devices");
        Assert.Contains("emulator-5554", devices, StringComparison.Ordinal);

        await using var hostContext = StartHost();
        var startup = await hostContext.Host.StartAsync();
        Assert.True(startup.Healthy, $"服务启动失败：{startup.Error}\n{startup.StderrTail}");

        // 注册环境入口：Wi-Fi Settings（ADB-002 同款）
        await RunAdb("-s emulator-5554 shell am start -a android.settings.WIFI_SETTINGS");
        await Task.Delay(TimeSpan.FromSeconds(2)); // 页面稳定（宽松常量，非时序断言）

        // ① 截屏 acquisition → PNG capture artifact
        var acquisition = new AdbScreenshotAcquisition(
            "emulator-5554", "adb", clock: () => DateTimeOffset.UtcNow);
        var capture = await acquisition.CaptureAsync(CancellationToken.None);
        Assert.True(capture.Width > 0 && capture.Height > 0);
        output.WriteLine($"截屏：{capture.Width}×{capture.Height}（{capture.Artifact.ArtifactId}）");

        // ② 服务推理 → 响应 JSON（derived artifact，确定性锚）
        using var client = new VisionServiceClient(hostContext.Transport, timeout: TimeSpan.FromSeconds(120));
        var analysis = await client.AnalyzeAsync(
            PngImage.Decode(capture.Artifact.Payload).Rgba,
            capture.Width, capture.Height, CancellationToken.None);
        Assert.True(analysis.Success, $"现场推理失败：{analysis.Diagnostic}");

        var derived = RawArtifact.Capture(
            System.Text.Encoding.UTF8.GetBytes(analysis.ResponseJson!),
            new ArtifactMetadata(
                capture.Width, capture.Height, "artifact",
                CaptureTime: capture.Artifact.Metadata.CaptureTime,                CaptureScope: $"derived:vision-service:{capture.Artifact.ArtifactId}"));

        // ③ 确定性解析 → ObservationProposal → EvidenceLedger admitted（≥1 非空）
        var perception = new FastPerception("perception.live.vision", new LiveVisionStrategy());
        var proposals = perception.Observe(derived);
        output.WriteLine($"proposals：{proposals.Count}");
        Assert.NotEmpty(proposals);

        var ledger = new EvidenceLedger();
        foreach (var proposal in proposals)
        {
            var (admission, _) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
        }
    }

    private static async Task<string> RunAdb(string arguments)
    {
        var startInfo = new ProcessStartInfo("adb", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("adb 启动失败（前置缺失，fail-closed）");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        Assert.Equal(0, process.ExitCode);
        return stdout;
    }
}
