namespace UniClaw.Runtime.Model;

/// <summary>
/// 一次观测中的单个 UI 元素（证据值，I-4：Observation 是 evidence，不是 semantic truth）。
/// Text + SwitchState? 是 grounding 消歧可用的全部证据（裁决 3）；Index 是观测内稳定序位，不是坐标。
/// Bounds 是上游 perception pipeline 已产生的归一化空间证据（canonical frame: full-screenshot,
/// top-left origin, normalized [0,1]×[0,1] — 与 fusion.py → _remap_coords 输出一致）。
/// Bounds 是空间证据，不是元素身份（Coordinate ≠ Element Identity）。
/// PerceptionType 是上游 perception provider 的原始类型标签（如 toggle / menuItem / text / switch）——
/// provider evidence，不是 Runtime semantic truth（PerceptionType ≠ ElementIdentity / SemanticCategory / InteractionCapability）。
/// </summary>
/// <param name="Text">元素文本。</param>
/// <param name="SwitchState">开关状态；null = 非开关承载元素，非 null = 开关状态可用（SC-P1-005 消歧证据）。</param>
/// <param name="Index">当前 Observation 内的稳定序位（grounding 结果与动作目标的引用载体；非坐标 — 裁决 3）。</param>
/// <param name="Bounds">可选归一化元素边界 [0,1]×[0,1]；null = 空间证据不可用（向后兼容）。</param>
/// <param name="PerceptionType">可选上游 perception provider 原始类型标签；null = 不可用（向后兼容）。</param>
/// <param name="StableKey">可选感知层稳定行标识（如 perception row_id）；非 null 时签名构造优先使用它替代 Text，
/// null 时回退到 Text（向后兼容）。不是元素身份的强制来源，仅作为签名稳定性增强。</param>
public sealed record ObservedElement(string Text, bool? SwitchState, int Index, ElementBounds? Bounds = null, string? PerceptionType = null)
{
    /// <summary>感知层稳定行标识（可空，来自 perception row_id）；签名构造在非空时优先使用，空时回退到 Text。</summary>
    public string? StableKey { get; init; }
}
