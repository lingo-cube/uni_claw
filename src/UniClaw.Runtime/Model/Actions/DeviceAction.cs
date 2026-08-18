namespace UniClaw.Runtime.Model;

/// <summary>
/// 设备动作（discriminated union）：LaunchApp | Tap | SetSwitch | ScrollForward | ScrollBackward。
/// Tap / SetSwitch 携带 TargetElementIndex——Runtime 侧 grounding 解析出的具体元素引用（SC-P1-001 / SC-P1-005）；
/// Environment 按元素身份应用物理效果，不替 Runtime 做元素选择（§8 / SC-P1-005）。
/// 所有变体均为不可变 sealed record。
/// </summary>
public abstract record DeviceAction
{
    /// <summary>启动应用。</summary>
    /// <param name="ApplicationId">目标应用标识；null = 未指定（由 Environment 上下文决定）。</param>
    /// <param name="LaunchIntentAction">可选机制级启动意图（如公开 Settings intent
    /// "android.settings.WIFI_SETTINGS"），由 Provider 翻译为物理启动命令；null = Phase 1 默认启动方式
    /// （Provider 自身决定）。这是意图级启动描述的机制提示，不携带任何 WiFi 语义/目标/成功标准。</param>
    public sealed record LaunchApp(string? ApplicationId, string? LaunchIntentAction = null) : DeviceAction;

    /// <summary>点击目标元素。</summary>
    /// <param name="TargetElementIndex">目标元素在当前观测内的 Index（grounding 解析结果）；null = 未指定。</param>
    /// <param name="TargetBounds">可选归一化元素边界 [0,1]×[0,1]；null = 空间证据不可用（向后兼容 Index-based 路径）。</param>
    public sealed record Tap(int? TargetElementIndex, ElementBounds? TargetBounds = null) : DeviceAction;

    /// <summary>将开关元素设为期望状态（表达期望状态，非机械翻转语义 Toggle — 裁决 1）。</summary>
    /// <param name="TargetElementIndex">开关元素在当前观测内的 Index（grounding 解析结果）；null = 未指定。</param>
    /// <param name="TargetState">期望开关状态。</param>
    /// <param name="TargetBounds">可选归一化元素边界 [0,1]×[0,1]；null = 空间证据不可用（向后兼容 Index-based 路径）。</param>
    public sealed record SetSwitch(int? TargetElementIndex, bool TargetState, ElementBounds? TargetBounds = null) : DeviceAction;

    /// <summary>
    /// 在当前局部 Container 内执行一次有界 forward viewport movement（SC-P3-003）。
    /// 该动作不选择元素，也不携带方向、坐标、距离、时长或 progress 语义。
    /// </summary>
    public sealed record ScrollForward : DeviceAction;

    /// <summary>
    /// 在当前局部 Container 内执行一次有界 backward viewport movement（bounded
    /// source revisit 的原语；与 ScrollForward 对称）。它让已发现但当前 viewport
    /// 不可见的 source 重新进入 fresh current evidence 后再 dispatch。
    /// 该动作不表示 Back 导航、Recovery 或 historical replay；不选择元素，不携带
    /// 坐标/距离/时长语义。
    /// </summary>
    public sealed record ScrollBackward : DeviceAction;

    private DeviceAction() { }
}
