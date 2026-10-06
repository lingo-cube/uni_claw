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
    /// <summary>
    /// 一次快感知调用的分段计时。Total 包含懒启动服务的冷启动；Inference
    /// 只表示当前截图从客户端发出到服务响应的时间。两者必须分开，避免把
    /// 首次服务启动误报成单帧视觉推理耗时。
    /// </summary>
    public sealed record AnalyzeTiming(
        TimeSpan Total,
        TimeSpan ServiceStartup,
        TimeSpan PngDecode,
        TimeSpan Inference,
        bool Succeeded);

    public sealed record WarmupTiming(
        TimeSpan Duration,
        bool Succeeded,
        string? Error = null);

    private readonly string _providerRoot;
    private readonly string _python;
    private readonly string? _cacheRoot;
    private VisionServiceHost? _host;
    private VisionServiceClient? _client;
    private string? _socketPath;

    public AnalyzeTiming? LastAnalyzeTiming { get; private set; }
    public WarmupTiming? LastWarmupTiming { get; private set; }

    public VisionServiceSession(string providerRoot, string python, string? cacheRoot = null)
    {
        _providerRoot = providerRoot;
        _python = python;
        _cacheRoot = cacheRoot;
    }

    /// <summary>真推理：PNG 字节 → 在线响应 JSON（确定性锚格式）。</summary>
    public string Analyze(byte[] png)
    {
        var totalStart = Stopwatch.GetTimestamp();
        var startup = TimeSpan.Zero;
        var decode = TimeSpan.Zero;
        var inference = TimeSpan.Zero;
        try
        {
            startup = EnsureService();
            var decodeStart = Stopwatch.GetTimestamp();
            var image = PngImage.Decode(png);
            decode = Stopwatch.GetElapsedTime(decodeStart);

            var inferenceStart = Stopwatch.GetTimestamp();
            var result = _client!.AnalyzeAsync(image.Rgba, image.Width, image.Height, CancellationToken.None)
                .GetAwaiter().GetResult();
            inference = Stopwatch.GetElapsedTime(inferenceStart);
            LastAnalyzeTiming = new AnalyzeTiming(
                Stopwatch.GetElapsedTime(totalStart), startup, decode, inference,
                result.Success && result.ResponseJson is not null);
            if (!result.Success || result.ResponseJson is null)
                throw new InvalidOperationException($"感知服务推理失败：{result.Diagnostic}");
            return result.ResponseJson;
        }
        catch
        {
            LastAnalyzeTiming = new AnalyzeTiming(
                Stopwatch.GetElapsedTime(totalStart), startup, decode, inference, false);
            throw;
        }
    }

    /// <summary>真采集 + 真推理：设备截屏 → 在线响应 JSON。</summary>
    public string CaptureAndAnalyze(AdbScreenshotAcquisition acquisition)
        => Analyze(acquisition.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult()
            .Artifact.Payload);

    /// <summary>
    /// 在首帧进入 Agent 前显式启动视觉服务。失败只记录结果并保留原有
    /// Analyze 的 fail-closed 行为；调用方可继续用层次结构完成观察。
    /// </summary>
    public WarmupTiming Warmup()
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            EnsureService();
            LastWarmupTiming = new WarmupTiming(
                Stopwatch.GetElapsedTime(started), true);
        }
        catch (Exception exception)
        {
            LastWarmupTiming = new WarmupTiming(
                Stopwatch.GetElapsedTime(started), false,
                exception.Message.Length > 512 ? exception.Message[..512] : exception.Message);
        }
        return LastWarmupTiming;
    }

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

    private TimeSpan EnsureService()
    {
        if (_client is not null)
            return TimeSpan.Zero;
        var startupStart = Stopwatch.GetTimestamp();
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
        try
        {
            var startup = _host.StartAsync().GetAwaiter().GetResult();
            if (!startup.Healthy)
                throw new InvalidOperationException($"感知服务启动失败：{startup.Error}\n{startup.StderrTail}");
            _client = new VisionServiceClient(
                new VisionServiceTransport.UnixDomainSocket(_socketPath),
                timeout: TimeSpan.FromSeconds(120));
            return Stopwatch.GetElapsedTime(startupStart);
        }
        catch
        {
            // A failed warmup must not leave an orphaned service behind. The
            // next Analyze attempt may retry once through the same session.
            _client?.Dispose();
            _client = null;
            if (_host is not null)
                _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _host = null;
            if (_socketPath is not null)
            {
                try { File.Delete(_socketPath); } catch (IOException) { }
            }
            throw;
        }
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
        private readonly HostUtilities.VirtualClock _clock;
        private readonly LiveAssets _assets;
        private readonly Func<string> _readSwitchState;
        private readonly VisionServiceSession _session;
        private readonly AdbScreenshotAcquisition _acquisition;
        private readonly UiAutomatorDump.ProbeStateMachine _xmlProbe = new();
        private int _phase;

        public LiveFrameFeed(HostUtilities.VirtualClock clock, LiveAssets assets, Func<string> readSwitchState)
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
            bool isPostPhase;
            switch (_phase)
            {
                case 0 when expected == ObservationContext.External:
                    _phase = 1;
                    isPostPhase = false;
                    break;
                case 1 when expected == ObservationContext.PostActionEffectFlow:
                    _phase = 2;
                    isPostPhase = true;
                    break;
                default:
                    return null; // 合法等待
            }

            // Each external observation owns a fresh virtual capture time. The
            // post-action capture must be strictly later than dispatch; advance
            // before XML/typed evidence and frame claims are assembled.
            // Android Settings applies the Wi-Fi toggle asynchronously; wait for
            // the UI hierarchy to settle before the post-action capture so the
            // typed checked claim and the independent settings probe describe
            // the same real state.
            if (isPostPhase)
                Thread.Sleep(1200);
            _clock.Tick();

            // 真观察：实屏截图 → 真推理 → switch 检测（bounds 来自当前屏幕）
            var shot = _acquisition.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            var responseJson = _session.Analyze(shot.Artifact.Payload);
            var detection = HostUtilities.ExtractJson(responseJson, "switch", $"live:{_assets.DeviceId}");

            // PER-009 D1：XML 永远并行（缺席即数据）；D8 探测状态机管节奏
            var (dump, xml, xmlDegraded) = TryCoObserveXml(expected);

            // Post-action state is read independently from system settings;
            // hierarchy evidence remains on the typed route.
            var state = _readSwitchState();
            ObservationProposal? stateClaim = null;

            var input = Frame(detection, shot.Width, shot.Height, state, expected, stateClaim);

            // 缺席标记：附加到既有 claims 的 lineage（degraded:no-xml）
            // （标记逻辑在 UiAutomatorDump.TagDegradedNoXml——PER-013 A-4 可测缝）
            if (xmlDegraded && input is RunDriverInput.Observation obs)
            {
                input = new RunDriverInput.Observation(
                    UiAutomatorDump.TagDegradedNoXml(obs.Proposals));
            }

            // XML per-node evidence enters only through the typed route.
            if (xml is not null && input is RunDriverInput.Observation o)
            {
                var extras = UiAutomatorDump.ComposeXmlEvidence(
                    xml,
                    typedContext: BuildTypedParseContext(shot.Width, shot.Height),
                    context: expected);
                if (extras.Length > 0)
                    input = new RunDriverInput.Observation(o.Proposals.Concat(extras).ToArray());
            }
            return input;
        }

        /// <summary>
        /// PER-013 Slice E / CSC-002（inventory P3 裁决）：typed 路由的
        /// CaptureMetadata 输入。Space 来自**同窗截图实测**（capture 并流；
        /// 跨源串行 adb 的时序假设已在 PER-013 R5 记录，归 PER-011）。
        /// API level 经 adb getprop 缓存查询（必填事实，不猜；未知 → null，
        /// typed 证据诚实缺席）。
        /// </summary>
        private UiAutomatorDump.UiHierarchyParseContext? BuildTypedParseContext(int shotWidth, int shotHeight) =>
            UiAutomatorDump.TryGetApiLevel(_assets.DeviceId) is { } apiLevel
                ? new UiAutomatorDump.UiHierarchyParseContext(
                    CaptureId: $"cap-{Guid.NewGuid():N}",
                    CaptureTimestamp: _clock.Now,
                    DeviceId: _assets.DeviceId,
                    SessionCorrelation: $"live:{_assets.DeviceId}:{_assets.ScreenId}",
                    AndroidApiLevel: apiLevel,
                    Space: CoordinateSpace.DeviceViewport(shotWidth, shotHeight))
                : null;

        /// <summary>
        /// PER-009 D1/D8：XML 并行观察。决策核心在
        /// <see cref="UiAutomatorDump.CoObserveXml"/>（PER-013 Slice B 抽出的
        /// 可测缝，行为不变）；live 侧只注入真传输。Slice E：同时回传原始
        /// XML（typed 路由输入）。
        /// </summary>
        private (UiAutomatorDump.DumpResult? Dump, string? Xml, bool Degraded) TryCoObserveXml(
            ObservationContext context)
        {
            // API 35 uiautomator may transiently report an empty/timeout dump while
            // Settings finishes its transition. Retry the transport in this same
            // observation window; a structural failure still remains degraded.
            (UiAutomatorDump.DumpResult? Dump, string? Xml, bool Degraded) last = default;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                last = UiAutomatorDump.CoObserveXml(
                    _xmlProbe, _clock.Now, _clock.Now, context,
                    transport: () => UiAutomatorDump.TryDumpToDevice(_assets.DeviceId));
                if (last.Xml is not null || _xmlProbe.Exhausted)
                    return last;
                Thread.Sleep(300);
            }
            return last;
        }

        private RunDriverInput Frame(
            HostUtilities.AnchorDetection detection,
            int shotWidth,
            int shotHeight,
            string state,
            ObservationContext context,
            ObservationProposal? stateClaim)
        {
            // CSC-001 Slice B：frame claim 携带 capture 实测尺寸（w/h）——
            // 归一化坐标自此自描述，投影端可与设备实况机械对拍。
            var frame = $"{{\"role\":\"switch\",\"state\":\"{state}\","
                + $"\"b\":[{detection.X1},{detection.Y1},{detection.X2},{detection.Y2}],"
                + $"\"w\":{shotWidth},\"h\":{shotHeight},"
                + $"\"f\":\"{AdbEffectDriver.SupportedFrame}\"}}";
            var proposals = new List<ObservationProposal>
            {
                new ObservationProposal(
                    new ObservationClaim(ProductAssociationStrategy.ScreenIdentitySubject, _assets.ScreenId),
                    IngressKind.Observation, context,
                    new Provenance("host.live", _clock.Now, "scope:ui.screen",
                        new[] { $"live:screen:{_assets.DeviceId}" })),
            };
            if (stateClaim is not null)
                proposals.Add(stateClaim);
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
