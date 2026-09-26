namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-010 §Identity and association：occurrence 引用——opaque、仅当前 capture 有效。
/// 不是 canonical entity identity：index / XPath / hierarchy path / bounds /
/// resource-id / Compose key 都不能单独成为跨 revision identity，只能作为
/// association features 交给 WorldModel 既有 occurrence/continuity 语义。
/// equality 含 <see cref="CaptureId"/> ⇒ 同一 local index 跨 capture 永不相等
/// （capture-scoped，构造期即防「同 index 当永久 identity」）。
/// </summary>
/// <param name="CaptureId">所属 capture。</param>
/// <param name="LocalIndex">capture 内局部序号（occurrence 特征，非 identity）。</param>
public readonly record struct OccurrenceRef(string CaptureId, int LocalIndex);

/// <summary>屏幕坐标系 bounds（px，frame 由 capture/correlation 对齐负责）。</summary>
/// <param name="X1">左。</param>
/// <param name="Y1">上。</param>
/// <param name="X2">右。</param>
/// <param name="Y2">下。</param>
public sealed record UiBounds(int X1, int Y1, int X2, int Y2)
{
    /// <summary>非倒置（宽高非负）才是合法观测 bounds。</summary>
    public bool IsValid => X2 >= X1 && Y2 >= Y1;
}

/// <summary>
/// PER-010 §Windows：capture-local window occurrence。没有 window capability 时
/// 相关字段为 Unsupported，不得凭单树假造 single-window truth。
/// </summary>
/// <param name="WindowOccurrenceRef">window 的 occurrence 引用。</param>
/// <param name="Bounds">可观察 bounds。</param>
/// <param name="Package">包名。</param>
/// <param name="Title">标题。</param>
/// <param name="Focused">是否聚焦窗口。</param>
/// <param name="Visible">是否可见窗口。</param>
/// <param name="DrawingOrder">可选绘制顺序。</param>
public sealed record UiWindowOccurrence(
    OccurrenceRef WindowOccurrenceRef,
    ObservedValue<UiBounds> Bounds,
    ObservedValue<string> Package,
    ObservedValue<string> Title,
    ObservedValue<bool> Focused,
    ObservedValue<bool> Visible,
    int? DrawingOrder = null);

/// <summary>
/// PER-010 §Nodes：capture/revision-local node occurrence。除 OccurrenceRef 等结构
/// 字段外全部为 <see cref="ObservedValue{T}"/>；字段缺失 ≠ false，malformed 字段
/// 不回退默认值。<paramref name="Password"/> 是能力/敏感标记而非状态权威，携带
/// 目的 = 下游脱敏执法可审计（grill F-E2：password=true 节点的 Text/ContentDescription/
/// Hint 必须在 normalization/projection 层脱敏并记 provenance）。
/// </summary>
/// <param name="OccurrenceRef">节点 occurrence 引用（capture-scoped）。</param>
/// <param name="ParentOccurrenceRef">父节点引用（仅 association feature）。</param>
/// <param name="WindowOccurrenceRef">所属 window 引用（可空）。</param>
/// <param name="SiblingOrder">兄弟序（occurrence 特征）。</param>
/// <param name="DrawingOrder">绘制顺序（capability 声明时携带）。</param>
/// <param name="Class">class（类别证据，非 identity）。</param>
/// <param name="ResourceId">resource-id（association feature，永不成为 canonical identity）。</param>
/// <param name="Package">包名。</param>
/// <param name="Text">语义文本（Accessibility 语义，非像素渲染文字真相）。</param>
/// <param name="ContentDescription">内容描述。</param>
/// <param name="Hint">hint（capability 缺席时 Unsupported）。</param>
/// <param name="Checkable">能力属性（checked 的 validity guard，非状态）。</param>
/// <param name="Checked">语义 checked 状态（外层 ObservedValue 可表达 Unknown/Unsupported）。</param>
/// <param name="Enabled">状态权威字段。</param>
/// <param name="Selected">状态权威字段。</param>
/// <param name="Focused">状态权威字段（accessibility focus 与 input focus 可能不同）。</param>
/// <param name="Scrollable">能力属性。</param>
/// <param name="Clickable">能力属性。</param>
/// <param name="Focusable">能力属性。</param>
/// <param name="VisibleToUser">可见性（capability 缺席时 Unsupported，不得推 absent）。</param>
/// <param name="Password">敏感字段标记（F-E2 脱敏执法输入；非状态权威）。</param>
/// <param name="Bounds">空间证据（grounding input 候选，非 effect authority）。</param>
public sealed record UiNodeObservation(
    OccurrenceRef OccurrenceRef,
    OccurrenceRef? ParentOccurrenceRef,
    OccurrenceRef? WindowOccurrenceRef,
    int? SiblingOrder,
    int? DrawingOrder,
    ObservedValue<string> Class,
    ObservedValue<string> ResourceId,
    ObservedValue<string> Package,
    ObservedValue<string> Text,
    ObservedValue<string> ContentDescription,
    ObservedValue<string> Hint,
    ObservedValue<bool> Checkable,
    ObservedValue<CheckedState> Checked,
    ObservedValue<bool> Enabled,
    ObservedValue<bool> Selected,
    ObservedValue<bool> Focused,
    ObservedValue<bool> Scrollable,
    ObservedValue<bool> Clickable,
    ObservedValue<bool> Focusable,
    ObservedValue<bool> VisibleToUser,
    ObservedValue<bool> Password,
    ObservedValue<UiBounds> Bounds);

/// <summary>
/// PER-010 §Stable Observation Contract v1：稳定内部 typed observation
/// （parse-independent）。构造期执法 occurrence 的 capture-local 性：任何
/// node/window 的 <see cref="OccurrenceRef.CaptureId"/> 必须等于
/// <see cref="Metadata"/> 的 CaptureId（跨 capture 引用即违规，fail-closed）。
/// </summary>
/// <param name="Metadata">capture 结构化 metadata。</param>
/// <param name="Windows">window occurrences。</param>
/// <param name="Nodes">node occurrences。</param>
public sealed record UiHierarchyObservation(
    CaptureMetadata Metadata,
    IReadOnlyList<UiWindowOccurrence> Windows,
    IReadOnlyList<UiNodeObservation> Nodes)
{
    /// <summary>构造期校验：metadata 有效、列表非 null、occurrence 全部 capture-local。</summary>
    public bool IsValid =>
        Metadata is { IsValid: true }
        && Windows is not null
        && Nodes is not null
        && Windows.All(w => w.WindowOccurrenceRef.CaptureId == Metadata.CaptureId)
        && Nodes.All(n =>
            n.OccurrenceRef.CaptureId == Metadata.CaptureId
            && (n.ParentOccurrenceRef is not { } parent || parent.CaptureId == Metadata.CaptureId)
            && (n.WindowOccurrenceRef is not { } window || window.CaptureId == Metadata.CaptureId));
}
