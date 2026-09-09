using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Effects;

/// <summary>
/// AdbEffectDriver — 首个产品 IEffectDriver（DSE-002 第一验收后端；dry-run：
/// 只构造 adb 命令串，零进程孵化——真实执行/超时/进程层随真机 buyer）。
/// 只做物理翻译（Human 裁决规则 C：driver 足够笨——不重新 grounding、
/// 不选择语义目标、不 fallback、不自行恢复）：
///   SpatialLocator（归一化 bounds + device-viewport）→ center × viewport
///   → pixel（clamp）→ "adb shell input tap X Y"（legacy
///   CoordinateMapper.ToPixelCenter 数学平移，DIRECT IDEA）。
/// 规则 B（driver-supported executable locator）执法：
///   locator 缺失 / frame ≠ device-viewport / effect ∉ {tap, set-switch}
///   → DeliveryFailed + reason（fail-closed；locator 失效的正确后继 =
///   上游 re-observe → re-ground → 新 binding → 新 DispatchRequest）。
/// 确定性：clock 注入（无 wall-clock；测试传固定值）。
/// </summary>
public sealed class AdbEffectDriver : IEffectDriver
{
    public const string SupportedFrame = "device-viewport";

    private readonly int _viewportWidth;
    private readonly int _viewportHeight;
    private readonly Func<DateTimeOffset> _clock;

    public AdbEffectDriver(int viewportWidth, int viewportHeight, Func<DateTimeOffset>? clock = null)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
            throw new ArgumentException("viewport 尺寸必须为正");
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public DispatchResult Deliver(DispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 规则 B：driver-supported executable locator
        if (request.Target.Spatial is not { } locator)
            return Fail(request, "no-executable-locator",
                "DeliveryTarget 无 SpatialLocator（v0.1 支持集 = NormalizedSpatial × device-viewport；NativeLocator 未建）");
        if (!string.Equals(locator.SpatialFrameId, SupportedFrame, StringComparison.Ordinal))
            return Fail(request, "unsupported-frame",
                $"frame '{locator.SpatialFrameId}' 不在支持集（仅 {SupportedFrame}）");
        var effect = request.EffectClass.ToLowerInvariant();
        if (effect is not ("tap" or "set-switch"))
            return Fail(request, "unsupported-effect",
                $"effect '{request.EffectClass}' 不在支持集（tap | set-switch）");

        // 物理翻译：归一化 center → viewport pixel（clamp 到有效域）
        var x = Math.Clamp((int)(locator.CenterX * _viewportWidth), 0, _viewportWidth - 1);
        var y = Math.Clamp((int)(locator.CenterY * _viewportHeight), 0, _viewportHeight - 1);

        // dry-run：命令构造即产物（Report = attempt evidence 溯源，非执行证明）
        return new DispatchResult(
            DispatchOutcome.DeliveryCompleted,
            $"adb shell input tap {x} {y}",
            _clock(),
            Reason: null);
    }

    private DispatchResult Fail(DispatchRequest request, string reason, string detail) =>
        new(DispatchOutcome.DeliveryFailed,
            $"rejected: {request.EffectClass} → {request.Target.OccurrenceReference}",
            _clock(),
            Reason: $"{reason}: {detail}");
}
