using System.Text.Json;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host;

/// <summary>
/// SIM-002 G1：Product Host 共享工具——从 dev/test feed 中提取的
/// 产品侧必需类型（LivePerception / HostRunner 消费）。
/// Replay/Simulated feed 已移至 tests/，此处只留产品能力。
/// </summary>
public static class HostUtilities
{
    /// <summary>
    /// 虚拟时钟：组合根持有时间权威（freshness 注入先例）。
    /// 产品与测试共用——LivePerception 也依赖它。
    /// </summary>
    public sealed class VirtualClock
    {
        private readonly DateTimeOffset _t0;
        private long _ticks;
        public VirtualClock(DateTimeOffset? startTime = null) => _t0 = startTime ?? new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Now => _t0.AddMinutes(_ticks);
        public void Tick() => _ticks++;
    }

    /// <summary>检测锚（bounds 归一化）——Live/Replay 共用提取格式。</summary>
    public sealed record AnchorDetection(string Label, double X1, double Y1, double X2, double Y2);

    /// <summary>
    /// 从感知响应 JSON 提取唯一目标检测——文件锚与服务在线响应共用。
    /// 产品能力（LivePerception 消费），非仿真专用。
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
}
