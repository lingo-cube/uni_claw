namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-010 §Checked：checked 的值轴（语义 checked 状态，与 rendered appearance 分轴）。
/// 替代 PER-009 string 值域 {on,off,partial} 的运行时执法缺口（closure audit A-3）。
/// </summary>
public enum CheckedState
{
    /// <summary>选中。</summary>
    Checked,

    /// <summary>未选中（仅 Exact 证明成立时才允许由 boolean false 得出）。</summary>
    Unchecked,

    /// <summary>部分选中（tri-state 语义；不得由 boolean 伪造）。</summary>
    Partial,
}

/// <summary>
/// PER-010 checked capability：acquirer 对当前 claim domain 的 checked 表达能力。
/// 运行时优先级 capability &gt; acquirer &gt; Android API level。
/// </summary>
public enum CheckedCapability
{
    /// <summary>checkedTriState：完整表达 Checked/Unchecked/Partial。</summary>
    CheckedTriState,

    /// <summary>checkedBooleanExact：仅在三项 exact proof 满足时表达二态。</summary>
    CheckedBooleanExact,

    /// <summary>checkedBooleanCollapsed：lossy boolean；false 不可表达为 Unchecked。</summary>
    CheckedBooleanCollapsed,
}

/// <summary>
/// PER-010 checkedBooleanExact proof rule 的三项证明（缺一即 Collapsed）：
/// (1) 当前 semantic claim domain 有明确 two-state contract；
/// (2) adapter capability metadata 明确声明 Exact；
/// (3) 有可追溯 contract / fixture evidence 支撑该声明。
/// 禁止从 AndroidApiLevel、checked attribute presence、class/role name alone、
/// historical absence of Partial、empirical samples alone 推断——
/// <see cref="CheckedSemantics"/> 的公开面没有任何上述参数（编译期执法）。
/// </summary>
/// <param name="TwoStateContractId">两态契约标识（claim domain 级）。</param>
/// <param name="AdapterCapabilityDeclaration">adapter capability metadata 的 Exact 声明。</param>
/// <param name="TraceableEvidenceRef">可追溯 contract/fixture evidence 引用。</param>
public sealed record CheckedExactProof(
    string TwoStateContractId,
    string AdapterCapabilityDeclaration,
    string TraceableEvidenceRef)
{
    /// <summary>三项证明全部非空才成立。</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(TwoStateContractId)
        && !string.IsNullOrWhiteSpace(AdapterCapabilityDeclaration)
        && !string.IsNullOrWhiteSpace(TraceableEvidenceRef);
}

/// <summary>
/// PER-010 checked 语义纯映射（parse-independent，Slice A 域类型执法）：
/// TriState 无损；BooleanExact true/false → Checked/Unchecked（仅当三项 proof 完整）；
/// BooleanCollapsed true → Checked、false → Unknown(partial-unrepresentable)。
/// 不得把 boolean 伪造为 Partial；不得在字段缺失时伪造 Unchecked。
/// </summary>
public static class CheckedSemantics
{
    /// <summary>M-02：collapsed/未证明 exact 的 boolean false 之 Unknown 原因。</summary>
    public const string PartialUnrepresentableReason = "partial-unrepresentable";

    /// <summary>
    /// M-03：tri-state 源三值无损映射（source capability 声明 checkedTriState）。
    /// </summary>
    public static ObservedValue<CheckedState> MapTriState(
        CheckedState value,
        FieldProvenance? provenance = null) =>
        ObservedValue<CheckedState>.Observed(value, provenance);

    /// <summary>
    /// M-01/M-02：boolean 源映射。<paramref name="rawChecked"/> 为 true 时任何能力都
    /// 得 Checked；为 false 时仅 <see cref="CheckedCapability.CheckedBooleanExact"/>
    /// 且 <paramref name="exactProof"/> 三项完整才得 Unchecked，否则一律
    /// Unknown(<see cref="PartialUnrepresentableReason"/>)。
    /// tri-state 源必须走 <see cref="MapTriState"/>；本入口收到
    /// <see cref="CheckedCapability.CheckedTriState"/> 属契约违规，fail-closed 抛出
    /// （parser 不猜值）。
    /// </summary>
    public static ObservedValue<CheckedState> MapBoolean(
        bool rawChecked,
        CheckedCapability declaredCapability,
        CheckedExactProof? exactProof,
        FieldProvenance? provenance = null)
    {
        if (declaredCapability == CheckedCapability.CheckedTriState)
        {
            throw new ArgumentOutOfRangeException(
                nameof(declaredCapability),
                "tri-state source must use MapTriState; boolean path cannot represent Partial (fail-closed)");
        }

        if (rawChecked)
        {
            return ObservedValue<CheckedState>.Observed(CheckedState.Checked, provenance);
        }

        return ExactProofSatisfied(declaredCapability, exactProof)
            ? ObservedValue<CheckedState>.Observed(CheckedState.Unchecked, provenance)
            : ObservedValue<CheckedState>.Unknown(PartialUnrepresentableReason, provenance);
    }

    /// <summary>
    /// Exact 判定：capability 必须声明 Exact 且 proof 三项完整；任一不满足即视为
    /// Collapsed（false → Unknown(partial-unrepresentable)）。
    /// </summary>
    public static bool ExactProofSatisfied(
        CheckedCapability declaredCapability,
        CheckedExactProof? exactProof) =>
        declaredCapability == CheckedCapability.CheckedBooleanExact
        && exactProof is { } proof
        && proof.IsComplete;
}
