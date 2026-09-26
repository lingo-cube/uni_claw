namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-010 §Nodes：节点属性的字段认识论状态。三态不可折叠：
/// <see cref="Observed"/>（本次采到值）/ <see cref="Unknown"/>（本次无法判定）/
/// <see cref="Unsupported"/>（acquirer 不具该能力）。字段缺失 ≠ Observed(false)；
/// source unavailable ≠ element missing；无能力 ≠ 未判定。
/// </summary>
public enum FieldState
{
    /// <summary>本次 capture 采到该字段值（值可为合法空串等）。</summary>
    Observed,

    /// <summary>字段本次无法判定（缺失 / malformed 按字段策略降级 / 语义不可表达）。</summary>
    Unknown,

    /// <summary>acquirer 声明不具该能力（capability 缺席，与本次判定不足不同）。</summary>
    Unsupported,
}

/// <summary>
/// PER-010 ObservedValue&lt;T&gt; 的字段级 provenance（capture / field / source /
/// normalization 的最小载体；capture 级完整结构 metadata 在
/// <see cref="CaptureMetadata"/>）。
/// </summary>
/// <param name="CaptureId">值所属 capture 的标识。</param>
/// <param name="SourceField">来源字段（如 XML attribute 名），未映射为 null。</param>
/// <param name="Normalization">归一化规则标识（如 redaction / decode），未应用为 null。</param>
public sealed record FieldProvenance(
    string CaptureId,
    string? SourceField = null,
    string? Normalization = null)
{
    /// <summary>CaptureId 必须非空（fail-closed 构造）。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(CaptureId);
}

/// <summary>
/// PER-010 ObservedValue&lt;T&gt;：State + 可选 Value + 原因 + provenance。
/// <c>Unknown</c> 与 <c>Unsupported</c> 不携带值；<c>Observed</c> 的空串文本是
/// 合法观测值，与 <c>Unknown</c>（属性缺席/无法判定）语义不同（grill F-B1）。
/// </summary>
/// <typeparam name="T">字段值类型。</typeparam>
/// <param name="State">字段认识论状态。</param>
/// <param name="Value">仅 <see cref="FieldState.Observed"/> 携带。</param>
/// <param name="Reason">Unknown/Unsupported 的机器可读原因（如 partial-unrepresentable）。</param>
/// <param name="Provenance">字段级 provenance（可选）。</param>
public sealed record ObservedValue<T>(
    FieldState State,
    T? Value,
    string? Reason = null,
    FieldProvenance? Provenance = null)
{
    /// <summary>构造 Observed 值。</summary>
    public static ObservedValue<T> Observed(T value, FieldProvenance? provenance = null) =>
        new(FieldState.Observed, value, null, provenance);

    /// <summary>构造 Unknown（本次无法判定；非 false、非空、非 absent）。</summary>
    public static ObservedValue<T> Unknown(string? reason = null, FieldProvenance? provenance = null) =>
        new(FieldState.Unknown, default, reason, provenance);

    /// <summary>构造 Unsupported（acquirer 不具该能力）。</summary>
    public static ObservedValue<T> Unsupported(string? reason = null, FieldProvenance? provenance = null) =>
        new(FieldState.Unsupported, default, reason, provenance);

    /// <summary>仅 Observed 时返回 true 并给出值；Unknown/Unsupported 一律判别失败。</summary>
    public bool TryGetObserved([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value)
    {
        if (State == FieldState.Observed)
        {
            value = Value!;
            return true;
        }

        value = default;
        return false;
    }
}
