using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Effects;

/// <summary>
/// EgoBrowserEffectDriver — 第二个产品 IEffectDriver（DSE-003 / 可替换性
/// 第二次实证；dry-run：构造命令串，不真开浏览器——真实 CDP 接入随
/// 观察侧 P2 第二 provider 的全链 change）。与 AdbEffectDriver 对称：
/// 只做物理翻译，不重新 grounding、不 fallback、不自行恢复（规则 C）。
/// 支持集（规则 B，delivery form 由支持集声明决定）：
///   locator = NativeLocator(kind = browser.backend-node-id)——只认 native，
///   **永不 fallback 到 spatial 坐标命中测试**（语义重定位禁令；DOM 重建
///   致 node-id 失效 → DeliveryFailed → 上游 re-observe → re-ground）
///   effect ∈ {click, tap, set-switch}（全部 click 语义——legacy 先例：
///   SetSwitch 物理即 click）→ "ego click node:{id}"。
/// 确定性：clock 注入。
/// </summary>
public sealed class EgoBrowserEffectDriver : IEffectDriver
{
    public const string SupportedKind = "browser.backend-node-id";

    private readonly Func<DateTimeOffset> _clock;

    public EgoBrowserEffectDriver(Func<DateTimeOffset>? clock = null) =>
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public DispatchResult Deliver(DispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 规则 B：driver-supported executable locator（本 driver 只认 native；
        // spatial 在场也不消费——多 locator = 不同 delivery material，无挑选）
        if (request.Target.Native is not { } native)
            return Fail(request, "no-executable-locator",
                "DeliveryTarget 无 NativeLocator（本 driver 支持集 = browser.backend-node-id；不 fallback 到 spatial）");
        if (!string.Equals(native.Kind, SupportedKind, StringComparison.Ordinal))
            return Fail(request, "unsupported-native-kind",
                $"native kind '{native.Kind}' 不在支持集（仅 {SupportedKind}）");
        var effect = request.EffectClass.ToLowerInvariant();
        if (effect is not ("click" or "tap" or "set-switch"))
            return Fail(request, "unsupported-effect",
                $"effect '{request.EffectClass}' 不在支持集（click | tap | set-switch）");

        // dry-run：命令构造即产物（Report = attempt evidence 溯源，非执行证明）
        return new DispatchResult(
            DispatchOutcome.DeliveryCompleted,
            $"ego click node:{native.Value}",
            _clock(),
            Reason: null);
    }

    private DispatchResult Fail(DispatchRequest request, string reason, string detail) =>
        new(DispatchOutcome.DeliveryFailed,
            $"rejected: {request.EffectClass} → {request.Target.OccurrenceReference}",
            _clock(),
            Reason: $"{reason}: {detail}");
}
