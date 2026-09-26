using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-013 Slice C：UiHierarchyObservation v1 → ObservationProposal[] 的确定性
/// 表示投影（5.3 裁决落档 changes/PER-013/plan.md）。职责边界：
/// 没有 belief authority、没有 conflict authority、没有 identity authority——
/// 只做 deterministic representation mapping。投影规则：
/// - 只投影 Observed 字段；Unknown/Unsupported/空串不产 claim（A8：缺检测不产
///   观察——missing 不降级为 false，也不伪造 "unknown" 值 claim）；
/// - subject occurrence-qualified：ui.node.{captureId}#{localIndex}.{field}
///   （grill F-D1：capture 限定，防跨 revision 误连续）；
/// - checked 值域 = checked/unchecked/partial（typed 名；on/off 投影是 Slice D
///   LegacyStateProjection 的 egress 职责）；
/// - lineage 仅进程标记 typed-hierarchy:v1——零结构语义字符串（CaptureId 等
///   只走 Provenance.Hierarchy descriptor，xml-map:/api:NN 协议禁止回潮）；
/// - 无效 observation → 抛 InvalidOperationException（fail-closed，不产部分
///   claims；descriptor 完整性由本投影保证，Ledger 不做新判定）。
/// PER-013 Slice E：公开面——真实 buyer = Host live feed（LivePerception
/// XML per-node evidence cutover）；ADR-0026 形状由该 buyer 验证后冻结。
/// </summary>
public static class TypedHierarchyProposalProjector
{
    internal const string Producer = "platform.uiautomator";
    internal const string LineageMarker = "typed-hierarchy:v1";

    /// <summary>投影一个 capture 的全部 Observed 字段 claims（Empty capture → 零 claims）。</summary>
    public static IReadOnlyList<ObservationProposal> Project(
        UiHierarchyObservation observation,
        ObservationContext context,
        string producer = Producer)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.IsValid || !observation.Metadata.IsValid)
        {
            throw new InvalidOperationException(
                "typed hierarchy observation failed capture-local/metadata validity (projector fail-closed)");
        }

        var metadata = observation.Metadata;
        var proposals = new List<ObservationProposal>();
        foreach (var node in observation.Nodes)
        {
            var parentIndex = node.ParentOccurrenceRef is { } parent ? (int?)parent.LocalIndex : null;

            void Emit<T>(
                string field,
                ObservedValue<T> value,
                string render)
            {
                if (value.State != FieldState.Observed || string.IsNullOrWhiteSpace(render))
                {
                    return; // Unknown/Unsupported/空串：不产 claim（A8）
                }

                var subject = $"ui.node.{metadata.CaptureId}#{node.OccurrenceRef.LocalIndex}.{field}";
                var descriptor = new HierarchyCaptureDescriptor(
                    CaptureId: metadata.CaptureId,
                    AndroidApiLevel: metadata.AndroidApiLevel,
                    AcquirerKind: metadata.AcquirerKind,
                    AcquirerVersion: metadata.AcquirerVersion,
                    HierarchyFormat: metadata.HierarchyFormat,
                    DeviceId: metadata.DeviceId,
                    SessionCorrelation: metadata.SessionCorrelation,
                    ObservationCycleId: metadata.ObservationCycleId,
                    CaptureTimestamp: metadata.CaptureTimestamp,
                    CaptureDuration: metadata.CaptureDuration,
                    Capabilities: metadata.Capabilities.Declared,
                    CoverageCompleteness: metadata.Coverage.Completeness,
                    CoverageLimitation: metadata.Coverage.Limitation,
                    NodeLocalIndex: node.OccurrenceRef.LocalIndex,
                    ParentLocalIndex: parentIndex,
                    Field: field);
                proposals.Add(new ObservationProposal(
                    new ObservationClaim(subject, render),
                    IngressKind.Observation,
                    context,
                    new Provenance(
                        producer,
                        metadata.CaptureTimestamp,
                        $"scope:{subject}",
                        new[] { LineageMarker },
                        Hierarchy: descriptor)));
            }

            Emit("class", node.Class, node.Class.Value ?? string.Empty);
            Emit("resource_id", node.ResourceId, node.ResourceId.Value ?? string.Empty);
            Emit("package", node.Package, node.Package.Value ?? string.Empty);
            Emit("text", node.Text, node.Text.Value ?? string.Empty);
            Emit("content_desc", node.ContentDescription, node.ContentDescription.Value ?? string.Empty);
            Emit("hint", node.Hint, node.Hint.Value ?? string.Empty);
            Emit("checked", node.Checked, node.Checked.Value is { } checkedState ? checkedState.ToString().ToLowerInvariant() : string.Empty);
            Emit("checkable", node.Checkable, node.Checkable.Value is { } checkable ? checkable ? "true" : "false" : string.Empty);
            Emit("clickable", node.Clickable, node.Clickable.Value is { } clickable ? clickable ? "true" : "false" : string.Empty);
            Emit("enabled", node.Enabled, node.Enabled.Value is { } enabled ? enabled ? "true" : "false" : string.Empty);
            Emit("focusable", node.Focusable, node.Focusable.Value is { } focusable ? focusable ? "true" : "false" : string.Empty);
            Emit("focused", node.Focused, node.Focused.Value is { } focused ? focused ? "true" : "false" : string.Empty);
            Emit("scrollable", node.Scrollable, node.Scrollable.Value is { } scrollable ? scrollable ? "true" : "false" : string.Empty);
            Emit("selected", node.Selected, node.Selected.Value is { } selected ? selected ? "true" : "false" : string.Empty);
            Emit("visible_to_user", node.VisibleToUser, node.VisibleToUser.Value is { } visible ? visible ? "true" : "false" : string.Empty);
            Emit(
                "bounds",
                node.Bounds,
                node.Bounds.Value is { } bounds
                    ? $"{bounds.X1},{bounds.Y1},{bounds.X2},{bounds.Y2}"
                    : string.Empty);
        }

        return proposals;
    }
}
