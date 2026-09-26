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

            // 真观察：实屏截图 → 真推理 → switch 检测（bounds 来自当前屏幕）
            var shot = _acquisition.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            var responseJson = _session.Analyze(shot.Artifact.Payload);
            var detection = HostUtilities.ExtractJson(responseJson, "switch", $"live:{_assets.DeviceId}");

            // PER-009 D1：XML 永远并行（缺席即数据）；D8 探测状态机管节奏
            var (dump, xml, xmlDegraded) = TryCoObserveXml(expected);

            // P-3（评审修复）：跨源碰头——XML 节点空间映射到共享层 {role}.state
            // P-6：Focused 时仅映射指令点名 subjects（真裁剪重扫 = Tier 1 管线支持后）
            ObservationProposal? mapped = null;
            if (dump is not null && SubjectInScope(directive))
            {
                mapped = UiAutomatorDump.MapTargetStateClaim(
                    dump, "switch",
                    (detection.X1, detection.Y1, detection.X2, detection.Y2),
                    shot.Width, shot.Height, _clock.Now, expected);
            }

            // P-1/C-2（评审修复）：post 相 XML-first——映射在场即权威定案通道
            // （四门之①③④由构造/序保证：目标态非空、checkable guard、post 相
            // dump 时序必然后于 dispatch；Kernel VerifyPostActionEffect 再执法）。
            // 映射缺席 → 回系统设置独立读态（原通道）。
            string state;
            ObservationProposal? stateClaim;
            if (isPostPhase && mapped is { } mappedClaim)
            {
                state = mappedClaim.Claim.Value;
                stateClaim = mappedClaim;
            }
            else
            {
                state = _readSwitchState();
                stateClaim = isPostPhase
                    ? new ObservationProposal(
                        new ObservationClaim("switch.state", state),
                        IngressKind.Observation, expected,
                        new Provenance("host.live", _clock.Now, "scope:switch.state",
                            new[] { $"live:state:{state}" }))
                    : null;
            }

            var input = Frame(detection, shot.Width, shot.Height, state, expected, stateClaim);

            // 缺席标记：附加到既有 claims 的 lineage（degraded:no-xml）
            // （标记逻辑在 UiAutomatorDump.TagDegradedNoXml——PER-013 A-4 可测缝）
            if (xmlDegraded && input is RunDriverInput.Observation obs)
            {
                input = new RunDriverInput.Observation(
                    UiAutomatorDump.TagDegradedNoXml(obs.Proposals));
            }

            // PER-013 Slice E（M-06 observer cutover）：XML per-node 证据走
            // typed 路由（occurrence-qualified claims，完全替代 legacy
            // dump.Claims per-node 通道）；legacy {role}.state 映射
            // （effect-critical，PER-012 egress surface）按原相位规则保留。
            // typed 路由不可用（无 XML / API level 未知）→ per-node 证据诚实缺席。
            if ((xml is not null || mapped is not null) && input is RunDriverInput.Observation o)
            {
                var extras = UiAutomatorDump.ComposeXmlEvidence(
                    xml,
                    mappedLegacyStateClaim: mapped,
                    isPostPhase,
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

        /// <summary>P-6：Focused 指令点名 subjects 时，仅映射被点名目标。</summary>
        private static bool SubjectInScope(ObservationDirective directive) =>
            directive.Subjects is not { Count: > 0 } subjects
            || subjects.Any(s => s == "switch" || s.StartsWith("switch.", StringComparison.Ordinal));

        /// <summary>
        /// PER-009 D1/D8：XML 并行观察。决策核心在
        /// <see cref="UiAutomatorDump.CoObserveXml"/>（PER-013 Slice B 抽出的
        /// 可测缝，行为不变）；live 侧只注入真传输。Slice E：同时回传原始
        /// XML（typed 路由输入）。
        /// </summary>
        private (UiAutomatorDump.DumpResult? Dump, string? Xml, bool Degraded) TryCoObserveXml(
            ObservationContext context)
        {
            return UiAutomatorDump.CoObserveXml(
                _xmlProbe, _clock.Now, _clock.Now, context,
                transport: () => UiAutomatorDump.TryDumpToDevice(_assets.DeviceId));
        }

        private RunDriverInput Frame(
            HostUtilities.AnchorDetection detection,
            int shotWidth,
            int shotHeight,
            string state,
            ObservationContext context,
            ObservationProposal? stateClaim)
        {
            _clock.Tick();
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
