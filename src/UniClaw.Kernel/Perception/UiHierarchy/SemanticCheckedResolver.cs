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
/// ② typed checked claims：取 current WorldState 全部 typed checked claims，
///    claim 值仅接受 checked/unchecked/partial（typed 名，PER-013 投影值域）；
/// ③ capture 收敛：有 canonical records 时按 provenance.Hierarchy descriptor
///    的 CaptureTimestamp 取最新 capture（多 capture 并存是常态——旧 capture
///    的 occurrence-qualified subject 永不删除）；无 canonical（或 descriptor
///    缺席）时 >1 个 captureId 即 Unknown(checked-claim-ambiguous)；
/// ④ 唯一性：收敛后 checked claim 恰一才 Observed；多 → Unknown；零 → Unknown
///   （collapsed capability 下 checked=false 不产 claim——缺席 ≠ Unchecked）；
/// ⑤ Unsupported：最新 capture 的 capability 元数据声明无 checked 能力
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

        // ② typed checked claims（只认 occurrence-qualified typed 值域）。
        var claims = belief.WorldState
            .Where(kv => kv.Key.StartsWith(SubjectPrefix, StringComparison.Ordinal)
                && kv.Key.EndsWith(CheckedSuffix, StringComparison.Ordinal))
            .Select(kv => (Subject: kv.Key, Value: ParseChecked(kv.Value.Value), kv.Value.EvidenceId))
            .Where(c => c.Value is { })
            .Select(c => new TypedClaim(c.Subject, c.Value!.GetValueOrDefault(), c.EvidenceId))
            .ToArray();
        if (claims.Length == 0)
            return Fail(ObservedValue<CheckedState>.Unknown("no-checked-claim"));

        // ③ capture 收敛：最新 capture 优先（canonical descriptor 提供时序）。
        var stamped = new List<(TypedClaim Claim, DateTimeOffset? Timestamp)>();
        foreach (var claim in claims)
        {
            stamped.Add(TryCaptureTimestamp(claim, canonical, out var ts)
                ? (claim, (DateTimeOffset?)ts)
                : (claim, null));
        }
        DateTimeOffset? newest = stamped
            .Where(s => s.Timestamp is { })
            .Select(s => s.Timestamp!.Value)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        if (newest > DateTimeOffset.MinValue)
        {
            stamped = stamped.Where(s => s.Timestamp == newest).ToList();
        }
        else
        {
            // 无时序可依：多 capture 并存即不可判（不猜新旧）。
            var captures = stamped.Select(s => CaptureIdOf(s.Claim.Subject)).Distinct().ToArray();
            if (captures.Length > 1)
                return Fail(ObservedValue<CheckedState>.Unknown("checked-claim-ambiguous"));
        }

        // ⑤ Unsupported：最新 capture 声明无 checked 能力（capability 缺席）。
        if (TryCaptureCapability(stamped[^1].Claim, canonical, out var capability)
            && capability is null)
        {
            return new Resolution(
                ObservedValue<CheckedState>.Unsupported("capability:checked-absent"),
                CaptureIdOf(stamped[^1].Claim.Subject), newest == DateTimeOffset.MinValue ? null : newest,
                null);
        }

        // ④ 唯一性定案。
        if (stamped.Count > 1)
            return Fail(ObservedValue<CheckedState>.Unknown("checked-claim-ambiguous"));
        var resolved = stamped[0];
        return new Resolution(
            ObservedValue<CheckedState>.Observed(resolved.Claim.Value),
            CaptureIdOf(resolved.Claim.Subject),
            newest == DateTimeOffset.MinValue ? null : newest,
            resolved.Claim.EvidenceId);
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

    private readonly record struct TypedClaim(string Subject, CheckedState Value, string EvidenceId);
}
