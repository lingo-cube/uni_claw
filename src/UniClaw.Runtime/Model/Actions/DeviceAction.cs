namespace UniClaw.Runtime.Model;

/// <summary>
/// 设备动作（discriminated union）：LaunchApp | Tap | SetSwitch。
/// Tap / SetSwitch 携带 TargetElementIndex——Runtime 侧 grounding 解析出的具体元素引用（SC-P1-001 / SC-P1-005）；
/// Environment 按元素身份应用物理效果，不替 Runtime 做元素选择（§8 / SC-P1-005）。
/// 所有变体均为不可变 sealed record。
/// </summary>
public abstract record DeviceAction
{
    /// <summary>启动应用。</summary>
    /// <param name="ApplicationId">目标应用标识；null = 未指定（由 Environment 上下文决定）。</param>
    public sealed record LaunchApp(string? ApplicationId) : DeviceAction;

    /// <summary>点击目标元素。</summary>
    /// <param name="TargetElementIndex">目标元素在当前观测内的 Index（grounding 解析结果）；null = 未指定。</param>
    public sealed record Tap(int? TargetElementIndex) : DeviceAction;

    /// <summary>将开关元素设为期望状态（表达期望状态，非机械翻转语义 Toggle — 裁决 1）。</summary>
    /// <param name="TargetElementIndex">开关元素在当前观测内的 Index（grounding 解析结果）；null = 未指定。</param>
    /// <param name="TargetState">期望开关状态。</param>
    public sealed record SetSwitch(int? TargetElementIndex, bool TargetState) : DeviceAction;

    private DeviceAction() { }
}
