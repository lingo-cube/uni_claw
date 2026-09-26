namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-010 §CaptureMetadata：AcquirerKind 至少区分三类采集器。
/// 格式与采集器可任意组合，不能由 Android major version 猜测。
/// </summary>
public enum UiHierarchyAcquirerKind
{
    /// <summary>legacy uiautomator XML（adb shell uiautomator dump）。</summary>
    LegacyUiAutomatorXml,

    /// <summary>AndroidX UiAutomator。</summary>
    AndroidXUiAutomator,

    /// <summary>AccessibilityNodeInfo / richer source。</summary>
    AccessibilityNodeInfo,
}

/// <summary>PER-010：HierarchyFormat 至少区分四种格式。</summary>
public enum UiHierarchyFormat
{
    /// <summary>传统 uiautomator XML。</summary>
    UiAutomatorXml,

    /// <summary>Accessibility 树。</summary>
    AccessibilityTree,

    /// <summary>Compose semantics 树（merged/unmerged 由 capability/provenance 记录）。</summary>
    ComposeSemantics,

    /// <summary>未知格式。</summary>
    Unknown,
}

/// <summary>
/// PER-010 §CaptureMetadata：每次 capture 的结构化 provenance 与跨 source 对齐输入。
/// grill F-C1 裁决：<see cref="CaptureTimestamp"/> 语义 = 采集完成时刻；
/// <see cref="CaptureDuration"/>（可选）记录采集窗口，供撕裂检测——二者皆为
/// provenance 输入，不是 freshness/truth authority（freshness 由 Assurance 判）。
/// </summary>
/// <param name="CaptureId">capture 标识（occurrence 的作用域键）。</param>
/// <param name="AndroidApiLevel">API level（仅 coverage/capability 事实输入）。</param>
/// <param name="AcquirerKind">采集器类别。</param>
/// <param name="AcquirerVersion">采集器版本。</param>
/// <param name="HierarchyFormat">层级格式。</param>
/// <param name="CaptureTimestamp">采集完成时刻（F-C1）。</param>
/// <param name="CaptureDuration">采集窗口时长（可选，F-C1）。</param>
/// <param name="DeviceId">设备标识。</param>
/// <param name="SessionCorrelation">session/run 相关性标识。</param>
/// <param name="ObservationCycleId">观察周期标识（可选但推荐；跨 source 对齐输入）。</param>
/// <param name="Capabilities">本次 capture 的能力声明。</param>
/// <param name="Coverage">本次 capture 的声明覆盖。</param>
public sealed record CaptureMetadata(
    string CaptureId,
    int AndroidApiLevel,
    UiHierarchyAcquirerKind AcquirerKind,
    string AcquirerVersion,
    UiHierarchyFormat HierarchyFormat,
    DateTimeOffset CaptureTimestamp,
    TimeSpan? CaptureDuration,
    string DeviceId,
    string SessionCorrelation,
    string? ObservationCycleId,
    HierarchyCapabilities Capabilities,
    HierarchyCoverage Coverage)
{
    /// <summary>构造期 fail-closed：必填标识非空、能力/覆盖有效、API level 合理。</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(CaptureId)
        && !string.IsNullOrWhiteSpace(AcquirerVersion)
        && !string.IsNullOrWhiteSpace(DeviceId)
        && !string.IsNullOrWhiteSpace(SessionCorrelation)
        && AndroidApiLevel > 0
        && Capabilities is not null
        && Coverage is { } coverage
        && coverage.IsValid
        && (CaptureDuration is null || CaptureDuration >= TimeSpan.Zero);
}
