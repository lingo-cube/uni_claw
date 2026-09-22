using System.Text.Json;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// 路线一 2a — 感知回放（2026-09-20 定性：「感知回放模拟真实感知功能」）。
/// 锚 = PER-005 D9 确定性锚（推理响应 JSON，非 PNG——编码器漂移不进回归）。
/// 本源加载两态录制锚（off/on），提取 switch 检测（YOLO label=switch，
/// 每锚恰一个），以**录制 bounds 原文**产出帧——真实管线输出、非真机输入。
/// 资产先例：platforms/perception/evaluation/assets/captures/
/// wifi-slice2-calibration/perception/*.json。
/// </summary>
public static class ReplayPerception
{
    /// <summary>录制锚路径对（确定性锚 JSON）。</summary>
    public sealed record ReplayAssets(string OffAnchorPath, string OnAnchorPath, string ScreenId);

    public sealed record AnchorDetection(string Label, double X1, double Y1, double X2, double Y2);

    /// <summary>解析录制锚文件：提取 label==detectionLabel 的唯一检测（bounds 归一化原文）。</summary>
    public static AnchorDetection Extract(string anchorPath, string detectionLabel = "switch")
        => ExtractJson(File.ReadAllText(anchorPath), detectionLabel, anchorPath);

    /// <summary>
    /// 从感知响应 JSON（确定性锚格式）提取唯一目标检测——文件锚与
    /// 服务在线响应共用同一提取器（高内聚：格式知识只此一处）。
    /// </summary>
    public static AnchorDetection ExtractJson(string responseJson, string detectionLabel, string sourceDescription)
    {
        using var document = JsonDocument.Parse(responseJson);
        var matches = document.RootElement
            .GetProperty("yolo")
            .EnumerateArray()
            .Where(e => e.GetProperty("label").GetString() == detectionLabel)
            .ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException(
                $"{sourceDescription} 应恰含一个 '{detectionLabel}' 检测，实得 {matches.Count}");
        var bounds = matches[0].GetProperty("bounds");
        return new AnchorDetection(
            detectionLabel,
            bounds.GetProperty("x1").GetDouble(),
            bounds.GetProperty("y1").GetDouble(),
            bounds.GetProperty("x2").GetDouble(),
            bounds.GetProperty("y2").GetDouble());
    }

    /// <summary>回放帧源：与 FrameFeed 同契约（屏幕身份稳定 + 帧批尾承载
    /// occurrence + post 帧目标态 claim），bounds/state 来自录制锚。</summary>
    public sealed class ReplayFrameFeed
    {
        private readonly V0Runtime.VirtualClock _clock;
        private readonly ReplayAssets _assets;
        private readonly string _targetState;
        private int _phase;

        public ReplayFrameFeed(
            V0Runtime.VirtualClock clock, ReplayAssets assets, string targetState = "on")
        {
            _clock = clock;
            _assets = assets;
            _targetState = targetState;
        }

        public RunDriverInput? Next(ObservationContext expected)
        {
            AnchorDetection detection;
            string state;
            bool includeStateClaim;
            switch (_phase)
            {
                case 0 when expected == ObservationContext.External:
                    _phase = 1;
                    detection = Extract(_assets.OffAnchorPath);
                    state = "off";
                    includeStateClaim = false;
                    break;
                case 1 when expected == ObservationContext.PostActionEffectFlow:
                    _phase = 2;
                    detection = Extract(_assets.OnAnchorPath);
                    state = _targetState;
                    includeStateClaim = true;
                    break;
                default:
                    return null; // 合法等待
            }

            _clock.Tick();
            // 录制 bounds 与 RUN-002 同为「捕获尺寸归一化」坐标系 → Adb 支持集
            var frame = $"{{\"role\":\"switch\",\"state\":\"{state}\","
                + $"\"b\":[{detection.X1},{detection.Y1},{detection.X2},{detection.Y2}],"
                + $"\"f\":\"{AdbEffectDriver.SupportedFrame}\"}}";
            var proposals = new List<ObservationProposal>
            {
                new ObservationProposal(
                    new ObservationClaim(ProductAssociationStrategy.ScreenIdentitySubject, _assets.ScreenId),
                    IngressKind.Observation, expected,
                    new Provenance("host.replay", _clock.Now, "scope:ui.screen",
                        new[] { $"replay:screen:{_assets.ScreenId}" })),
            };
            if (includeStateClaim)
                proposals.Add(new ObservationProposal(
                    new ObservationClaim(SharedSubjects.State("switch"), state),
                    IngressKind.Observation, expected,
                    new Provenance("host.replay", _clock.Now, "scope:switch.state",
                        new[] { $"replay:state:{state}" })));
            proposals.Add(new ObservationProposal(
                new ObservationClaim(SharedSubjects.Frame, frame),
                IngressKind.Observation, expected,
                new Provenance("host.replay", _clock.Now, "scope:screen.frame",
                    new[] { $"replay:frame:{state}" })));
            return new RunDriverInput.Observation(proposals);
        }
    }
}
