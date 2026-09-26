using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.Compatibility;

/// <summary>
/// PER-013 Slice D / PER-012 §2：legacy projection 结果（终值——egress-only，
/// 无回转通道）。<paramref name="Value"/> ∈ {on, off, unavailable}；
/// degraded 结果永不携带 on/off（M-05）。
/// </summary>
/// <param name="Value">投影值（on/off/unavailable）。</param>
/// <param name="Degraded">true = 无法无损投影（M-05 标记）。</param>
/// <param name="Reason">机器可读原因（lossless / partial-unrepresentable / conflicted / …）。</param>
public sealed record LegacyStateProjectionResult(
    string Value,
    bool Degraded,
    string Reason)
{
    /// <summary>无损投影（typed ↔ legacy 值域一一对应）。</summary>
    public bool IsLossless => !Degraded;
}

/// <summary>
/// PER-013 Slice D：typed semantic checked → legacy `*.state` 的 egress-only
/// 兼容投影（PER-012 §2 Human Gate 裁决）。迁移期语义（保守读法，owner
/// Slice D 规则）：
/// - Observed(Checked) → on、Observed(Unchecked) → off（无损，M-04）；
/// - Unknown / Unsupported / Observed(Partial) / conflictPresent →
///   `unavailable` + Degraded（M-05）——不得压成 ON/OFF；typed Partial 不投
///   legacy "partial"：该通道是 API 36 前向兼容、从未有真实 legacy consumer
///   验证可消费，迁移期视为无法无损投递（裁决记录 changes/PER-013/plan.md）；
/// - 本类是纯静态映射：不引用 Evidence / World 命名空间、不产生
///   ObservationProposal/ObservationClaim（P2 ingress 形状）——
///   `legacy → typed/P2/Fusion/WorldModel` 方向不存在（architecture
///   closure 反射执法：LegacyStateProjectionTests.Closure_*）。
/// 放置约定：Compatibility 命名空间（不进 World/ 作为 World 组件）。
/// </summary>
public static class LegacyStateProjection
{
    /// <summary>M-05 degraded 投影值。</summary>
    public const string DegradedValue = "unavailable";

    /// <summary>无损投影值：开。</summary>
    public const string LosslessOn = "on";

    /// <summary>无损投影值：关。</summary>
    public const string LosslessOff = "off";

    /// <summary>
    /// egress 投影。conflictPresent = WorldModel 侧存在未销案冲突时强制
    /// degraded（旧 consumer 不得看到单边裁决值）。
    /// </summary>
    public static LegacyStateProjectionResult Project(
        ObservedValue<CheckedState> checkedValue,
        bool conflictPresent = false)
    {
        if (conflictPresent)
        {
            return new LegacyStateProjectionResult(DegradedValue, Degraded: true, "conflicted");
        }

        return checkedValue.State switch
        {
            FieldState.Observed => checkedValue.Value switch
            {
                CheckedState.Checked => new LegacyStateProjectionResult(LosslessOn, Degraded: false, "lossless"),
                CheckedState.Unchecked => new LegacyStateProjectionResult(LosslessOff, Degraded: false, "lossless"),
                CheckedState.Partial => new LegacyStateProjectionResult(
                    DegradedValue, Degraded: true, "typed-partial-no-lossless-legacy-channel"),
                _ => new LegacyStateProjectionResult(DegradedValue, Degraded: true, "unrecognized-checked-state"),
            },
            FieldState.Unknown => new LegacyStateProjectionResult(
                DegradedValue,
                Degraded: true,
                string.IsNullOrWhiteSpace(checkedValue.Reason) ? "unknown" : checkedValue.Reason),
            _ => new LegacyStateProjectionResult(DegradedValue, Degraded: true, "unsupported-capability"),
        };
    }
}
