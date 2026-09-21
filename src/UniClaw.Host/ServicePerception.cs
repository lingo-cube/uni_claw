using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// 路线一 2b — 服务回放：录制截图 → **真感知服务**（Python 视觉管线，
/// UDS）→ 在线推理响应（确定性锚格式）→ 共享提取器 → 帧契约。
/// 真实管线逻辑、非真机输入；与 2a（锚文件）共用
/// ReplayPerception.ExtractJson——同一格式知识只此一处。
/// 缝为同步（RunDriverInputs 契约）；内部 Async 同步等待（console v0
/// 形态，无并发消费者）。fail-closed：推理失败即抛（Diagnostic 随文）。
/// </summary>
public static class ServicePerception
{
    public sealed record ServiceReplayAssets(
        string OffPngPath,
        string OnPngPath,
        string ScreenId,
        string ProviderRoot,
        string PythonExecutable,
        string? CacheRoot = null);

    public sealed class ServiceReplayFrameFeed : IDisposable
    {
        private readonly V0Runtime.VirtualClock _clock;
        private readonly ServiceReplayAssets _assets;
        private readonly string _targetState;
        private VisionServiceHost? _host;
        private VisionServiceClient? _client;
        private string? _socketPath;
        private int _phase;

        public ServiceReplayFrameFeed(
            V0Runtime.VirtualClock clock, ServiceReplayAssets assets, string targetState = "on")
        {
            _clock = clock;
            _assets = assets;
            _targetState = targetState;
        }

        public RunDriverInput? Next(ObservationContext expected)
        {
            string pngPath;
            string state;
            bool includeStateClaim;
            switch (_phase)
            {
                case 0 when expected == ObservationContext.External:
                    _phase = 1;
                    pngPath = _assets.OffPngPath;
                    state = "off";
                    includeStateClaim = false;
                    break;
                case 1 when expected == ObservationContext.PostActionEffectFlow:
                    _phase = 2;
                    pngPath = _assets.OnPngPath;
                    state = _targetState;
                    includeStateClaim = true;
                    break;
                default:
                    return null; // 合法等待
            }

            var detection = Analyze(pngPath);
            return Frame(detection, state, expected, includeStateClaim);
        }

        private ReplayPerception.AnchorDetection Analyze(string pngPath)
        {
            EnsureService();
            var image = PngImage.Decode(File.ReadAllBytes(pngPath));
            var result = _client!.AnalyzeAsync(image.Rgba, image.Width, image.Height, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!result.Success || result.ResponseJson is null)
                throw new InvalidOperationException(
                    $"感知服务推理失败（{pngPath}）：{result.Diagnostic}");
            return ReplayPerception.ExtractJson(result.ResponseJson, "switch", $"service:{pngPath}");
        }

        private void EnsureService()
        {
            if (_client is not null)
                return;
            var cacheRoot = _assets.CacheRoot ?? Path.Combine(Path.GetTempPath(), "uniclaw-perception-cache");
            Directory.CreateDirectory(Path.Combine(cacheRoot, "matplotlib"));
            _socketPath = Path.Combine(Path.GetTempPath(), $"uniclaw-host-{Guid.NewGuid():N}.sock");
            _host = new VisionServiceHost(new VisionServiceHostOptions(
                _assets.PythonExecutable,
                _assets.ProviderRoot,
                new VisionServiceTransport.UnixDomainSocket(_socketPath),
                StartupTimeout: TimeSpan.FromSeconds(180),
                ExtraEnvironment: new Dictionary<string, string>
                {
                    ["XDG_CACHE_HOME"] = cacheRoot,
                    ["MPLCONFIGDIR"] = Path.Combine(cacheRoot, "matplotlib"),
                }));
            var startup = _host.StartAsync().GetAwaiter().GetResult();
            if (!startup.Healthy)
                throw new InvalidOperationException(
                    $"感知服务启动失败：{startup.Error}\n{startup.StderrTail}");
            _client = new VisionServiceClient(
                new VisionServiceTransport.UnixDomainSocket(_socketPath),
                timeout: TimeSpan.FromSeconds(120));
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
                    new Provenance("host.service-replay", _clock.Now, "scope:ui.screen",
                        new[] { $"service-replay:screen:{_assets.ScreenId}" })),
            };
            if (includeStateClaim)
                proposals.Add(new ObservationProposal(
                    new ObservationClaim("switch.state", state),
                    IngressKind.Observation, context,
                    new Provenance("host.service-replay", _clock.Now, "scope:switch.state",
                        new[] { $"service-replay:state:{state}" })));
            proposals.Add(new ObservationProposal(
                new ObservationClaim("screen.frame", frame),
                IngressKind.Observation, context,
                new Provenance("host.service-replay", _clock.Now, "scope:screen.frame",
                    new[] { $"service-replay:frame:{state}" })));
            return new RunDriverInput.Observation(proposals);
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
    }
}
