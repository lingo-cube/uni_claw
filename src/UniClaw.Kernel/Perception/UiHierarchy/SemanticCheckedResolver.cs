using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-014 R1：Kernel 只读 role→typed-checked 解析缝（owner-derived
/// projection，同 PolicyEvaluationView 先例）。把既有 grounding/occurrence
/// 解析（Role 相等 ∧ descriptor 相等[若给] ∧ container 相等[若给]，与
/// ResolveCurrent / DeriveEntityObligationFactKind 同一机械确定性维度）与
/// WorldState 中的 typed checked claim
/// （<c>ui.node.{captureId}#idx.checked</c>，TypedHierarchyProposalProjector
/// 投影）join 起来，解析为 <see cref="ObservedValue{CheckedState}"/>。
///
/// Join 语义（机械确定性、fail-closed、零猜测）：
/// ① occurrence 解析：候选恰一才继续；零 → Unknown(occurrence-absent)、
///    多 → Unknown(occurrence-ambiguous)（Identity never creates information）；
/// ② association：优先用 occurrence 的 EvidenceBasis 精确关联 typed node；
///    若该证据带有既有 resource-id 或 bounds association evidence，才允许
///    对同一 typed node 做确定性碰头。没有 association evidence 不猜；
/// ③ typed checked claims：关联 node 的 claim 值仅接受 checked/unchecked/partial
///    （typed 名，PER-013 投影值域）；无关 node 的 claim 永不参与；
/// ④ 唯一性：关联 node 恰一且 checked claim 恰一才 Observed；多 → Unknown；零
///    → Unknown（collapsed capability 下 checked=false 不产 claim——缺席 ≠ Unchecked）；
/// ⑤ Unsupported：关联 node 的 capability 元数据声明无 checked 能力
///    （ResolveCheckedCapability() == null）→ Unsupported（capability 缺席
///    与本次判定不足分离）。
///
/// 禁止（R1 边界）：写回 / 持久化 / 缝内冲突裁决 / producer 侧 role 绑定；
/// Grounding target binding 权威零变更；Unknown/Unsupported/Partial 永不
/// 折叠为 Unchecked/off。
/// </summary>
internal static class SemanticCheckedResolver
{
    /// <summary>typed claim subject 前缀（occurrence-qualified node claims）。</summary>
    private const string SubjectPrefix = "ui.node.";

    /// <summary>checked 字段后缀。</summary>
    private const string CheckedSuffix = ".checked";

    /// <summary>详细解析结果：值 + 该 checked claim 的 capture 事实（时序门输入）。</summary>
    internal sealed record Resolution(
        ObservedValue<CheckedState> Value,
        string? CaptureId,
        DateTimeOffset? CaptureTimestamp,
        string? ClaimEvidenceId)
    {
        /// <summary>仅 Observed 时为 true（Unknown/Unsupported 一律判别失败）。</summary>
        public bool IsObserved => Value.State == FieldState.Observed;
    }

    /// <summary>值域解析（capture 事实不外扬的消费面）。</summary>
    internal static ObservedValue<CheckedState> Resolve(
        WorldBeliefRevision belief,
        TargetDescriptor scope,
        IReadOnlyDictionary<string, EvidenceRecord>? canonical = null) =>
        ResolveDetailed(belief, scope, canonical).Value;

    /// <summary>详细解析（Slice B 时序门需要 capture timestamp）。</summary>
    internal static Resolution ResolveDetailed(
        WorldBeliefRevision belief,
        TargetDescriptor scope,
        IReadOnlyDictionary<string, EvidenceRecord>? canonical = null)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(scope);

        // ① occurrence 解析（与 ResolveCurrent / DeriveEntityObligationFactKind
        //    同一匹配语义；R1：不新增 Grounding authority，只读消费）。
        var candidates = (belief.Occurrences ?? Array.Empty<OccurrenceBelief>())
            .Where(o => OccurrenceDescriptorMatcher.Matches(
                o.Role, o.SemanticDescriptor, o.OwningContainerId,
                scope.Role, scope.SemanticDescriptor, scope.OwningContainerId,
                ContainerMatchMode.TargetEquality))
            .ToArray();
        if (candidates.Length == 0)
            return Fail(ObservedValue<CheckedState>.Unknown("occurrence-absent"));
        if (candidates.Length > 1)
            return Fail(ObservedValue<CheckedState>.Unknown("occurrence-ambiguous"));

        // ② association evidence + typed claims。只在既有 occurrence/claim
        // 证据能确定同一 typed node 时继续；role 全局唯一绝不构成 association。
        var nodes = BuildTypedNodes(belief, canonical);
        if (nodes.Count == 0)
            return Fail(ObservedValue<CheckedState>.Unknown("no-checked-claim"));

        var associated = nodes
            .Where(node => IsAssociated(node, candidates[0]))
            .ToArray();
        if (associated.Length == 0)
            return Fail(ObservedValue<CheckedState>.Unknown("checked-occurrence-unassociated"));
        if (associated.Length > 1)
            return Fail(ObservedValue<CheckedState>.Unknown("checked-occurrence-ambiguous"));
        var node = associated[0];
        if (node.Claims.Count == 0)
            return Fail(ObservedValue<CheckedState>.Unknown("no-checked-claim"));
        if (node.Claims.Count > 1)
            return Fail(ObservedValue<CheckedState>.Unknown("checked-claim-ambiguous"));
        var claim = node.Claims[0];
        var timestamp = TryCaptureTimestamp(claim, canonical, out var captured)
            ? (DateTimeOffset?)captured
            : null;

        // ⑤ Unsupported：最新 capture 声明无 checked 能力（capability 缺席）。
        if (TryCaptureCapability(claim, canonical, out var capability)
            && capability is null)
        {
            return new Resolution(
                ObservedValue<CheckedState>.Unsupported("capability:checked-absent"),
                claim.Node.CaptureId, timestamp,
                null);
        }

        // ④ 唯一性定案。
        return new Resolution(
            ObservedValue<CheckedState>.Observed(claim.Value),
            claim.Node.CaptureId,
            timestamp,
            claim.EvidenceId);
    }

    private static Resolution Fail(ObservedValue<CheckedState> value) =>
        new(value, null, null, null);

    /// <summary>typed 值域解析（仅接受 checked/unchecked/partial；其余不猜）。</summary>
    private static CheckedState? ParseChecked(string value) => value switch
    {
        "checked" => CheckedState.Checked,
        "unchecked" => CheckedState.Unchecked,
        "partial" => CheckedState.Partial,
        _ => null,
    };

    private static string CaptureIdOf(string subject)
    {
        // ui.node.{captureId}#{localIndex}.checked → {captureId}
        var body = subject[SubjectPrefix.Length..^CheckedSuffix.Length];
        var hash = body.LastIndexOf('#');
        return hash < 0 ? body : body[..hash];
    }

    private static bool TryCaptureTimestamp(
        TypedClaim claim,
        IReadOnlyDictionary<string, EvidenceRecord>? canonical,
        out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (canonical is null
            || !canonical.TryGetValue(claim.EvidenceId, out var record)
            || record.Provenance.Hierarchy is not { } descriptor)
            return false;
        timestamp = descriptor.CaptureTimestamp;
        return true;
    }

    private static bool TryCaptureCapability(
        TypedClaim claim,
        IReadOnlyDictionary<string, EvidenceRecord>? canonical,
        out CheckedCapability? capability)
    {
        capability = null;
        if (canonical is null
            || !canonical.TryGetValue(claim.EvidenceId, out var record)
            || record.Provenance.Hierarchy is not { } descriptor)
            return false;
        capability = new HierarchyCapabilities(descriptor.Capabilities).ResolveCheckedCapability();
        return true;
    }

    private sealed class TypedNode(NodeRef node)
    {
        public NodeRef Node { get; } = node;
        public List<TypedClaim> Claims { get; } = new();
        public HashSet<string> EvidenceIds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ResourceIds { get; } = new(StringComparer.Ordinal);
        public List<BoundsFact> Bounds { get; } = new();
    }

    private readonly record struct NodeRef(string CaptureId, int LocalIndex);

    private readonly record struct TypedClaim(
        string Subject, CheckedState Value, string EvidenceId, NodeRef Node);

    private readonly record struct BoundsFact(
        string Value, CoordinateSpace? Space, string EvidenceId);

    private static IReadOnlyList<TypedNode> BuildTypedNodes(
        WorldBeliefRevision belief,
        IReadOnlyDictionary<string, EvidenceRecord>? canonical)
    {
        var nodes = new Dictionary<NodeRef, TypedNode>();
        foreach (var (subject, claim) in belief.WorldState)
        {
            if (!TryParseNodeSubject(subject, out var nodeRef, out var field))
                continue;
            if (!nodes.TryGetValue(nodeRef, out var node))
            {
                node = new TypedNode(nodeRef);
                nodes.Add(nodeRef, node);
            }

            EvidenceRecord? evidence = null;
            if (canonical is not null)
                canonical.TryGetValue(claim.EvidenceId, out evidence);
            var descriptor = evidence?.Provenance.Hierarchy;
            if (descriptor is not null
                && (descriptor.CaptureId != nodeRef.CaptureId
                    || descriptor.NodeLocalIndex != nodeRef.LocalIndex))
            {
                // Producer metadata 与 occurrence-qualified subject 矛盾：保留
                // claim 但禁止把这条记录当 association evidence。
                continue;
            }
            node.EvidenceIds.Add(claim.EvidenceId);

            if (field == "checked" && ParseChecked(claim.Value) is { } checkedValue)
            {
                node.Claims.Add(new TypedClaim(subject, checkedValue, claim.EvidenceId, nodeRef));
            }
            else if (field == "resource_id" && !string.IsNullOrWhiteSpace(claim.Value))
            {
                node.ResourceIds.Add(claim.Value);
            }
            else if (field == "bounds")
            {
                node.Bounds.Add(new BoundsFact(claim.Value, descriptor?.Space, claim.EvidenceId));
            }
        }

        return nodes.Values.ToArray();
    }

    private static bool IsAssociated(
        TypedNode node,
        OccurrenceBelief occurrence)
    {
        if (occurrence.EvidenceBasis.Any(node.EvidenceIds.Contains))
            return true;

        if (occurrence.Native is { Kind: "android.resource-id" } native
            && node.ResourceIds.Contains(native.Value, StringComparer.Ordinal))
            return true;

        if (occurrence.Locator is not { } locator)
            return false;
        return node.Bounds.Any(bounds => BoundsMatch(locator, bounds));
    }

    private static bool BoundsMatch(SpatialLocator locator, BoundsFact bounds)
    {
        if (bounds.Space is not { PixelWidth: > 0, PixelHeight: > 0 } space
            || !string.Equals(locator.SpatialFrameId, space.CoordinateSpaceId, StringComparison.Ordinal)
            || !TryParseBounds(bounds.Value, out var x1, out var y1, out var x2, out var y2))
            return false;
        const double epsilon = 1e-6;
        return Math.Abs(locator.X1 - x1 / space.PixelWidth) <= epsilon
            && Math.Abs(locator.Y1 - y1 / space.PixelHeight) <= epsilon
            && Math.Abs(locator.X2 - x2 / space.PixelWidth) <= epsilon
            && Math.Abs(locator.Y2 - y2 / space.PixelHeight) <= epsilon;
    }

    private static bool TryParseNodeSubject(
        string subject, out NodeRef node, out string field)
    {
        node = default;
        field = string.Empty;
        if (!subject.StartsWith(SubjectPrefix, StringComparison.Ordinal))
            return false;
        var body = subject[SubjectPrefix.Length..];
        var hash = body.LastIndexOf('#');
        var dot = body.LastIndexOf('.');
        if (hash <= 0 || dot <= hash + 1 || dot == body.Length - 1
            || !int.TryParse(body[(hash + 1)..dot], out var localIndex))
            return false;
        node = new NodeRef(body[..hash], localIndex);
        field = body[(dot + 1)..];
        return true;
    }

    private static bool TryParseBounds(
        string value, out double x1, out double y1, out double x2, out double y2)
    {
        x1 = y1 = x2 = y2 = default;
        var parts = value.Split(',');
        return parts.Length == 4
            && double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out x1)
            && double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out y1)
            && double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out x2)
            && double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out y2);
    }
}
