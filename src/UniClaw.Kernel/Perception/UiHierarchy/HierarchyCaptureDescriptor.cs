namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-013 Slice C：随 accepted Evidence 保留的 hierarchy capture 结构 metadata
/// （PER-010 CaptureMetadata 的 evidence-payload 投影）。它是 provenance
/// payload，不是 truth——替代 PER-009 的 lineage 字符串协议（xml-map:/api:NN
/// 禁止回潮）。EvidenceLedger 对其通用透传（admission 检查清单零改动）；
/// 完整性由 TypedHierarchyProposalProjector fail-closed 保证。
/// </summary>
/// <param name="CaptureId">capture 标识（occurrence 作用域键）。</param>
/// <param name="AndroidApiLevel">API level（coverage/capability 事实输入）。</param>
/// <param name="AcquirerKind">采集器类别。</param>
/// <param name="AcquirerVersion">采集器版本。</param>
/// <param name="HierarchyFormat">层级格式。</param>
/// <param name="DeviceId">设备标识。</param>
/// <param name="SessionCorrelation">session/run 相关性。</param>
/// <param name="ObservationCycleId">观察周期（可选；跨 source 对齐输入）。</param>
/// <param name="CaptureTimestamp">采集完成时刻（F-C1）。</param>
/// <param name="CaptureDuration">采集窗口（可选，F-C1 撕裂检测输入）。</param>
/// <param name="Capabilities">能力声明（flags）。</param>
/// <param name="CoverageCompleteness">覆盖完备性。</param>
/// <param name="CoverageLimitation">Partial 时的限制说明。</param>
/// <param name="NodeLocalIndex">本 claim 所属节点 occurrence 的 capture 内局部序号。</param>
/// <param name="ParentLocalIndex">父节点局部序号（根为 null；仅 association feature）。</param>
/// <param name="Field">claim 对应的节点字段名。</param>
public sealed record HierarchyCaptureDescriptor(
    string CaptureId,
    int AndroidApiLevel,
    UiHierarchyAcquirerKind AcquirerKind,
    string AcquirerVersion,
    UiHierarchyFormat HierarchyFormat,
    string DeviceId,
    string SessionCorrelation,
    string? ObservationCycleId,
    DateTimeOffset CaptureTimestamp,
    TimeSpan? CaptureDuration,
    HierarchyCapability Capabilities,
    CoverageCompleteness CoverageCompleteness,
    string? CoverageLimitation,
    int NodeLocalIndex,
    int? ParentLocalIndex,
    string Field)
{
    /// <summary>EvidenceId 参与的确定性 canonical 渲染（与 ComputeEvidenceId 一致）。</summary>
    public string RenderCanonical() =>
        string.Join('\x1E',
            CaptureId,
            AndroidApiLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AcquirerKind.ToString(),
            AcquirerVersion,
            HierarchyFormat.ToString(),
            DeviceId,
            SessionCorrelation,
            ObservationCycleId ?? "-",
            CaptureTimestamp.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            CaptureDuration is { } duration ? duration.ToString("c") : "-",
            Capabilities.ToString("D"),
            CoverageCompleteness.ToString(),
            CoverageLimitation ?? "-",
            NodeLocalIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ParentLocalIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-",
            Field);
}
