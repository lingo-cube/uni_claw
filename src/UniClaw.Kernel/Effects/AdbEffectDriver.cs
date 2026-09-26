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

        if (!TryBuildTap(request, _viewportWidth, _viewportHeight,
                out var x, out var y, out _, out var reason, out var detail))
            return Fail(request, reason!, detail!);

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

    /// <summary>
    /// CSC-001 Slice C：driver 支持集校验（locator 在场 / frame 在支持集 /
    /// effect class 在支持集）——独立于投影。live driver 在坐标空间检查
    /// **之前**调用，保持 frozen 拒绝语义（no-executable-locator /
    /// unsupported-frame / unsupported-effect 不被空间检查遮蔽）。
    /// </summary>
    internal static bool ValidateSupport(
        DispatchRequest request, out string? reason, out string? detail)
    {
        reason = detail = null;
        if (request.Target.Spatial is not { } locator)
        {
            reason = "no-executable-locator";
            detail = "DeliveryTarget 无 SpatialLocator（本 driver 支持集 = NormalizedSpatial × device-viewport；不消费 native）";
            return false;
        }

        if (!string.Equals(locator.SpatialFrameId, SupportedFrame, StringComparison.Ordinal))
        {
            reason = "unsupported-frame";
            detail = $"frame '{locator.SpatialFrameId}' 不在支持集（仅 {SupportedFrame}）";
            return false;
        }

        var effect = request.EffectClass.ToLowerInvariant();
        if (effect is not ("tap" or "click" or "set-switch"))
        {
            reason = "unsupported-effect";
            detail = $"effect '{request.EffectClass}' 不在支持集（tap | click | set-switch；click 与 tap 物理同义 → input tap）";
            return false;
        }

        return true;
    }

    /// <summary>共享命令构造（ADB-001 提取；dry-run 与 live 同源——支持集与
    /// 投影的单一事实源）。失败输出 (reason, detail) 与 driver 拒绝语义对齐。</summary>
    internal static bool TryBuildTap(
        DispatchRequest request, int viewportWidth, int viewportHeight,
        out int x, out int y, out IReadOnlyList<string> deviceArgs,
        out string? reason, out string? detail)
    {
        x = y = 0;
        deviceArgs = Array.Empty<string>();
        reason = detail = null;

        // 规则 B：driver-supported executable locator
        if (request.Target.Spatial is not { } locator)
        {
            reason = "no-executable-locator";
            detail = "DeliveryTarget 无 SpatialLocator（本 driver 支持集 = NormalizedSpatial × device-viewport；不消费 native）";
            return false;
        }
        if (!string.Equals(locator.SpatialFrameId, SupportedFrame, StringComparison.Ordinal))
        {
            reason = "unsupported-frame";
            detail = $"frame '{locator.SpatialFrameId}' 不在支持集（仅 {SupportedFrame}）";
            return false;
        }
        var effect = request.EffectClass.ToLowerInvariant();
        if (effect is not ("tap" or "click" or "set-switch"))
        {
            reason = "unsupported-effect";
            detail = $"effect '{request.EffectClass}' 不在支持集（tap | click | set-switch；click 与 tap 物理同义 → input tap）";
            return false;
        }

        // 物理翻译：归一化 center → viewport pixel（clamp 到有效域）
        x = Math.Clamp((int)(locator.CenterX * viewportWidth), 0, viewportWidth - 1);
        y = Math.Clamp((int)(locator.CenterY * viewportHeight), 0, viewportHeight - 1);
        deviceArgs = ["shell", "input", "tap", x.ToString(), y.ToString()];
        return true;
    }
}
