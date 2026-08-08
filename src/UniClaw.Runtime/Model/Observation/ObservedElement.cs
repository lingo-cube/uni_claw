namespace UniClaw.Runtime.Model;

/// <summary>
/// 一次观测中的单个 UI 元素（证据值，I-4：Observation 是 evidence，不是 semantic truth）。
/// Text + SwitchState? 是 grounding 消歧可用的全部证据（裁决 3）；Index 是观测内稳定序位，不是坐标。
/// 本阶段不引入独立 ElementKind 枚举（裁决 9），也不引入 coordinate / hierarchy 字段（裁决 3）。
/// </summary>
/// <param name="Text">元素文本。</param>
/// <param name="SwitchState">开关状态；null = 非开关承载元素，非 null = 开关状态可用（SC-P1-005 消歧证据）。</param>
/// <param name="Index">当前 Observation 内的稳定序位（grounding 结果与动作目标的引用载体；非坐标 — 裁决 3）。</param>
public sealed record ObservedElement(string Text, bool? SwitchState, int Index);
