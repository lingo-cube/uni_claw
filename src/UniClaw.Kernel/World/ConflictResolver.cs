using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.World;

/// <summary>
/// PER-009 冲突裁决器的 typed semantic.checked 实现。
/// 本类型只消费已经由 SemanticCheckedResolver 解析出的 typed 结果；身份、
/// 关联、能力和 capture provenance 仍由 typed 解析缝负责，这里只应用既有
/// 的冲突权威规则。
/// </summary>
internal static class ConflictResolver
{
    public enum ClaimDomain
    {
        Other,
        SemanticChecked,
        RenderedAppearance,
    }

    public sealed record ConflictingClaim(
        string Producer,
        string Value,
        DateTimeOffset CaptureTime,
        ClaimDomain Domain = ClaimDomain.Other);

    public sealed record ConflictCase(string Subject, IReadOnlyList<ConflictingClaim> Claims);

    public enum Tier
    {
        CategoryAuthority,
        VisionDomain,
    }

    public sealed record Disposition(
        string Subject,
        Tier Tier,
        string? ResolvedValue,
        CheckedState? ResolvedSemantic,
        string? OverruledProducer,
        string Basis);

    /// <summary>
    /// Checked/Unchecked 可定案；Partial、Unknown、Unsupported 和缺失时序一律
    /// 返回 unresolved，不伪造二态值。
    /// </summary>
    public static Disposition Resolve(
        ConflictCase conflict,
        ObservedValue<CheckedState> semantic,
        DateTimeOffset? semanticCaptureTime,
        TimeSpan freshnessWindow)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        ArgumentNullException.ThrowIfNull(semantic);

        if (!semantic.TryGetObserved(out var checkedState))
        {
            return Unresolved(
                conflict.Subject,
                $"typed semantic.checked 不足：{semantic.State}"
                    + (semantic.Reason is null ? string.Empty : $" ({semantic.Reason})"));
        }

        if (checkedState == CheckedState.Partial)
        {
            return Unresolved(conflict.Subject,
                "typed semantic.checked=partial；不压缩为 Checked/Unchecked");
        }

        if (semanticCaptureTime is not { } captured)
        {
            return Unresolved(conflict.Subject,
                "typed semantic.checked 缺少 capture timestamp（FreshEnough 失败）");
        }

        // rendered appearance never participates in the semantic freshness or
        // conflict comparison. If no semantic peer exists, the typed capture
        // itself is the only time anchor available.
        var semanticClaims = conflict.Claims
            .Where(c => c.Domain == ClaimDomain.SemanticChecked)
            .ToArray();
        var newestClaim = semanticClaims.Length == 0
            ? captured
            : semanticClaims.Max(c => c.CaptureTime);
        if ((newestClaim - captured).Duration() > freshnessWindow)
        {
            return Unresolved(conflict.Subject,
                "typed semantic.checked 与争议时刻不新鲜（FreshEnough 失败）");
        }

        var resolved = checkedState.ToString().ToLowerInvariant();
        var overruled = conflict.Claims
            .Where(c => c.Domain == ClaimDomain.SemanticChecked)
            .FirstOrDefault(c => !string.Equals(c.Value, resolved, StringComparison.Ordinal))
            ?.Producer;

        return new Disposition(
            conflict.Subject,
            Tier.CategoryAuthority,
            resolved,
            checkedState,
            overruled,
            $"typed semantic.checked={resolved}·既有冲突规则定案（confidence 盲、不升档）");
    }

    private static Disposition Unresolved(string subject, string basis) =>
        new(subject, Tier.VisionDomain, null, null, null, basis);
}
