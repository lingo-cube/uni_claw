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
    /// <param name="LaunchIntentAction">可选机制级启动意图，由 Provider 翻译为物理启动命令；
    /// null 表示使用 Provider 自身决定的默认启动方式。该描述不携带领域语义、目标或成功标准。</param>
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
    /// 该动作不选择元素，也不携带方向、坐标、时长或 progress 语义。
    /// <see cref="StepFraction"/>（默认 1.0 = 既有固定步长）是证据驱动的自适应
    /// 滚动距离缩放：Agent 在视口探索中按相邻帧导航签名重叠动态调整（重叠充足
    /// 缓增、重叠不足砍半），保证观察连续性并避免丢失 grounding anchors。它仍是
    /// 距离缩放机制参数，不是语义、页面或场景知识。
    /// </summary>
    /// <param name="StepFraction">滚动距离缩放（0,∞)；1.0 = 既有固定步长；
    /// &lt;1.0 = 更小步长。默认 1.0 保持既有调用与行为不变。</param>
    public sealed record ScrollForward(float StepFraction = 1.0f) : DeviceAction;

    /// <summary>
    /// 在当前局部 Container 内执行一次有界 backward viewport movement（bounded
    /// source revisit 的原语；与 ScrollForward 对称）。它让已发现但当前 viewport
    /// 不可见的 source 重新进入 fresh current evidence 后再 dispatch。
    /// 该动作不表示 Back 导航、Recovery 或 historical replay；不选择元素，不携带
    /// 坐标/时长语义。<see cref="StepFraction"/> 与 ScrollForward 同为证据驱动的
    /// 距离缩放参数（默认 1.0 = 既有固定步长）。
    /// </summary>
    /// <param name="StepFraction">滚动距离缩放（0,∞)；1.0 = 既有固定步长。</param>
    public sealed record ScrollBackward(float StepFraction = 1.0f) : DeviceAction;

    /// <summary>
    /// SYSTEM BACK — the bounded external-boundary return primitive (EBD).
    /// A single platform Back (Android `input keyevent 4`). It is ONLY an
    /// execution primitive: it carries NO destination semantics, does NOT judge
    /// return success, and is never retried/auto-multiplied. Distinct from
    /// ScrollBackward (which is a swipe-based bounded-viewport revisit, NOT a
    /// Back navigation). Exactly ONE authorized SystemBack is dispatched per
    /// boundary disposition.
    /// </summary>
    public sealed record SystemBack : DeviceAction;

    private DeviceAction() { }
}
