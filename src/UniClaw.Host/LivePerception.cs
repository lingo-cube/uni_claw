using System.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// 感知服务会话（2b/3 共用生命周期）：UDS 服务 + 客户端的启动、复用与
/// 释放收敛一处（高内聚）；消费方只拿 Analyze(png) 与 Capture+Analyze。
/// </summary>
public sealed class VisionServiceSession : IDisposable
{
    private readonly string _providerRoot;
    private readonly string _python;
    private readonly string? _cacheRoot;
    private VisionServiceHost? _host;
    private VisionServiceClient? _client;
    private string? _socketPath;

    public VisionServiceSession(string providerRoot, string python, string? cacheRoot = null)
    {
        _providerRoot = providerRoot;
        _python = python;
        _cacheRoot = cacheRoot;
    }

    /// <summary>真推理：PNG 字节 → 在线响应 JSON（确定性锚格式）。</summary>
    public string Analyze(byte[] png)
    {
        EnsureService();
        var image = PngImage.Decode(png);
        var result = _client!.AnalyzeAsync(image.Rgba, image.Width, image.Height, CancellationToken.None)
            .GetAwaiter().GetResult();
        if (!result.Success || result.ResponseJson is null)
            throw new InvalidOperationException($"感知服务推理失败：{result.Diagnostic}");
        return result.ResponseJson;
    }

    /// <summary>真采集 + 真推理：设备截屏 → 在线响应 JSON。</summary>
    public string CaptureAndAnalyze(AdbScreenshotAcquisition acquisition)
        => Analyze(acquisition.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult()
            .Artifact.Payload);

    public void Dispose()
    {
        _client?.Dispose();
        if (_host is not null)
            _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (_socketPath is not null)
        {
            try { File.Delete(_socketPath); } catch (IOException) { }
        }
    }

    private void EnsureService()
    {
        if (_client is not null)
            return;
        var cacheRoot = _cacheRoot ?? Path.Combine(Path.GetTempPath(), "uniclaw-perception-cache");
        Directory.CreateDirectory(Path.Combine(cacheRoot, "matplotlib"));
        _socketPath = Path.Combine(Path.GetTempPath(), $"uniclaw-host-{Guid.NewGuid():N}.sock");
        _host = new VisionServiceHost(new VisionServiceHostOptions(
            _python,
            _providerRoot,
            new VisionServiceTransport.UnixDomainSocket(_socketPath),
            StartupTimeout: TimeSpan.FromSeconds(180),
            ExtraEnvironment: new Dictionary<string, string>
            {
                ["XDG_CACHE_HOME"] = cacheRoot,
                ["MPLCONFIGDIR"] = Path.Combine(cacheRoot, "matplotlib"),
            }));
        var startup = _host.StartAsync().GetAwaiter().GetResult();
        if (!startup.Healthy)
            throw new InvalidOperationException($"感知服务启动失败：{startup.Error}\n{startup.StderrTail}");
        _client = new VisionServiceClient(
            new VisionServiceTransport.UnixDomainSocket(_socketPath),
            timeout: TimeSpan.FromSeconds(120));
    }
}

/// <summary>
/// 路线一第 3 步 — 全真闭环帧源：实屏截屏 → 真推理 → 在线 switch 检测
/// （bounds 来自**当前屏幕**，非标定）+ 开关态读自系统设置（adb，
/// 独立验证源）→ 帧契约。post-action 帧同样真复查：tap 后实屏再截、
/// 状态再读——验证诚实（成功与否由现实决定，不由仿真自证）。
/// </summary>
public static class LivePerception
{
    public sealed record LiveAssets(
        string DeviceId,
        string ScreenId,
        string ProviderRoot,
        string PythonExecutable,
        string? CacheRoot = null);

    public sealed class LiveFrameFeed : IDisposable
    {
        private readonly V0Runtime.VirtualClock _clock;
        private readonly LiveAssets _assets;
        private readonly Func<string> _readSwitchState;
        private readonly VisionServiceSession _session;
        private readonly AdbScreenshotAcquisition _acquisition;
        private int _phase;

        public LiveFrameFeed(V0Runtime.VirtualClock clock, LiveAssets assets, Func<string> readSwitchState)
        {
            _clock = clock;
            _assets = assets;
            _readSwitchState = readSwitchState;
            _session = new VisionServiceSession(assets.ProviderRoot, assets.PythonExecutable, assets.CacheRoot);
            _acquisition = new AdbScreenshotAcquisition(assets.DeviceId, "adb", () => clock.Now);
        }

        public RunDriverInput? Next(ObservationDirective directive)
        {
            var expected = directive.Context;
            bool includeStateClaim;
            switch (_phase)
            {
                case 0 when expected == ObservationContext.External:
                    _phase = 1;
                    includeStateClaim = false;
                    break;
                case 1 when expected == ObservationContext.PostActionEffectFlow:
                    _phase = 2;
                    includeStateClaim = true;
                    break;
                default:
                    return null; // 合法等待
            }

            // 真观察：实屏截图 → 真推理 → switch 检测；状态读自系统设置
            var responseJson = _session.CaptureAndAnalyze(_acquisition);
            var detection = ReplayPerception.ExtractJson(responseJson, "switch", $"live:{_assets.DeviceId}");
            var state = _readSwitchState();
            return Frame(detection, state, expected, includeStateClaim);
        }

        private RunDriverInput Frame(
            ReplayPerception.AnchorDetection detection,
            string state,
            ObservationContext context,
            bool includeStateClaim)
        {
            _clock.Tick();
            var frame = $"{{\"role\":\"switch\",\"state\":\"{state}\","
                + $"\"b\":[{detection.X1},{detection.Y1},{detection.X2},{detection.Y2}],"
                + $"\"f\":\"{AdbEffectDriver.SupportedFrame}\"}}";
            var proposals = new List<ObservationProposal>
            {
                new ObservationProposal(
                    new ObservationClaim(ProductAssociationStrategy.ScreenIdentitySubject, _assets.ScreenId),
                    IngressKind.Observation, context,
                    new Provenance("host.live", _clock.Now, "scope:ui.screen",
                        new[] { $"live:screen:{_assets.DeviceId}" })),
            };
            if (includeStateClaim)
                proposals.Add(new ObservationProposal(
                    new ObservationClaim(SharedSubjects.State("switch"), state),
                    IngressKind.Observation, context,
                    new Provenance("host.live", _clock.Now, "scope:switch.state",
                        new[] { $"live:state:{state}" })));
            proposals.Add(new ObservationProposal(
                new ObservationClaim(SharedSubjects.Frame, frame),
                IngressKind.Observation, context,
                new Provenance("host.live", _clock.Now, "scope:screen.frame",
                    new[] { $"live:frame:{state}" })));
            return new RunDriverInput.Observation(proposals);
        }

        public void Dispose() => _session.Dispose();

        /// <summary>开关态读取（adb 独立验证源；Host 侧 helper）。</summary>
        public static string ReadWifiState(string deviceId)
        {
            var info = new ProcessStartInfo("adb", $"-s {deviceId} shell settings get global wifi_on")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("adb 启动失败（fail-closed）");
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return stdout.Trim() == "1" ? "on" : "off";
        }
    }
}
