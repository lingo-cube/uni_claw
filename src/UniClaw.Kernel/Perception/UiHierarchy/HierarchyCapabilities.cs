namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-010 §Capabilities：adapter 声明并可被测试的能力（flags）。能力按
/// source/acquirer/version 事实映射，不按 Android major version 硬编码；
/// <c>Unsupported</c> 只表示能力不存在（字段级 Unknown 表示本次不足）。
/// </summary>
[Flags]
public enum HierarchyCapability
{
    /// <summary>无能力声明。</summary>
    None = 0,

    /// <summary>checkedTriState：完整表达 Checked/Unchecked/Partial。</summary>
    CheckedTriState = 1 << 0,

    /// <summary>checkedBooleanExact：三项 proof 下表达二态。</summary>
    CheckedBooleanExact = 1 << 1,

    /// <summary>checkedBooleanCollapsed：lossy boolean。</summary>
    CheckedBooleanCollapsed = 1 << 2,

    /// <summary>drawing order 可观察。</summary>
    DrawingOrder = 1 << 3,

    /// <summary>hint 字段可观察。</summary>
    Hint = 1 << 4,

    /// <summary>window 列表可观察。</summary>
    WindowSupport = 1 << 5,

    /// <summary>visible-to-user 可观察。</summary>
    Visibility = 1 << 6,

    /// <summary>语义文本可观察。</summary>
    SemanticText = 1 << 7,

    /// <summary>content-description 可观察。</summary>
    ContentDescription = 1 << 8,

    /// <summary>Compose semantics 树可观察。</summary>
    ComposeSemantics = 1 << 9,

    /// <summary>WebView inner content 可观察。</summary>
    WebViewInnerContent = 1 << 10,
}

/// <summary>
/// 一次 capture 的能力声明集合（PER-010：adapter 声明并可被测试）。
/// </summary>
/// <param name="Declared">已声明能力位。</param>
public sealed record HierarchyCapabilities(HierarchyCapability Declared)
{
    /// <summary>是否声明了指定能力（None 恒为 false）。</summary>
    public bool Has(HierarchyCapability capability) =>
        capability != HierarchyCapability.None && (Declared & capability) == capability;

    /// <summary>
    /// 解析本次 checked 表达能力（优先级 TriState &gt; Exact &gt; Collapsed）；
    /// 无任何 checked 能力声明时为 null（字段将全部 Unsupported）。
    /// </summary>
    public CheckedCapability? ResolveCheckedCapability() =>
        Has(HierarchyCapability.CheckedTriState) ? CheckedCapability.CheckedTriState
        : Has(HierarchyCapability.CheckedBooleanExact) ? CheckedCapability.CheckedBooleanExact
        : Has(HierarchyCapability.CheckedBooleanCollapsed) ? CheckedCapability.CheckedBooleanCollapsed
        : null;
}
