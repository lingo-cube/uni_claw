namespace UniClaw.Kernel.World;

/// <summary>
/// PER-009 冲突裁决器（changes/PER-009/mechanism.md 冻结规则的纯函数实现）。
/// 只裁决、不观察（六原则）；confidence 结构性缺席——ObservationClaim 无
/// 分数字段，"confidence 盲"由类型系统保证（0.55 与 0.999 同等待遇）。
/// 输出两种处置：CategoryAuthority（权威域定案，销案、不升档）或
/// VisionDomain（XML 失去本次裁判资格 ≠ 视觉获胜，视觉阶梯继续）。
/// 升档决策归 Control（S5/S6），本类型不发起观察。
/// internal：不进公开驱动面（RUN-003 白名单零变更）；跨程序集消费
/// 出现时经授权提升（SharedSubjects 先例）。
/// </summary>
internal static class ConflictResolver
{
    public sealed record ConflictingClaim(string Producer, string Value, DateTimeOffset CaptureTime);

    public sealed record ConflictCase(string Subject, IReadOnlyList<ConflictingClaim> Claims);

    /// <summary>
    /// XML 侧权威快照：由 dump 索引（Host 侧 UiAutomatorDump.DumpResult）
    /// 投影构造；Kernel 不依赖 Host 类型。Checked ∈ {false,true,partial}。
    /// </summary>
    public sealed record XmlAuthoritySnapshot(
        string LocalId,
        bool IdentityUnique,
        DateTimeOffset DumpTime,
        bool Checkable,
        string Checked,
        bool? Enabled,
        bool? Selected,
        bool? Focused);

    public enum Tier
    {
        /// <summary>权威域定案：三道门通过，XML 类别权威生效，销案不升档。</summary>
        CategoryAuthority,

        /// <summary>XML 失去本次裁判资格（非权威域 / 三道门任一不过）。</summary>
        VisionDomain,
    }

    public sealed record Disposition(
        string Subject,
        Tier Tier,
        string? ResolvedValue,     // 定案值，值域 {on, off, partial}（D7）
        string? OverruledProducer, // 被裁掉的 producer（confusion 盲，留档）
        string Basis);             // 审计依据（人读）

    /// <summary>
    /// 冻结裁决链：字段分类（D12）→ 三道门（D3/D14）→ 定案或交还视觉域。
    /// </summary>
    public static Disposition Resolve(ConflictCase conflict, XmlAuthoritySnapshot? xml, TimeSpan freshnessWindow)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        var field = FieldOf(conflict.Subject);
        if (field is null)
            return ToVisionDomain(conflict.Subject, "非权威域（D12 字段表：颜色/图标/canvas/像素文字等）");
        if (xml is null)
            return ToVisionDomain(conflict.Subject, "无 XML 快照（缺席即数据，不参战）");

        // ---- 三道门（D3，顺序固定）----
        if (!xml.IdentityUnique)
            return ToVisionDomain(conflict.Subject, "IdentityMatched 失败：subject→节点非唯一解析（D3）");

        var newestClaim = conflict.Claims.Count == 0
            ? null
            : conflict.Claims.Aggregate((a, b) => a.CaptureTime >= b.CaptureTime ? a : b);
        if (newestClaim is { } claim && (claim.CaptureTime - xml.DumpTime).Duration() > freshnessWindow)
            return ToVisionDomain(conflict.Subject, "FreshEnough 失败：dump 与争议时刻可能描述两个时刻（D14）");

        var guard = field switch
        {
            "checked" => xml.Checkable ? null : "PropertyValid 失败：checked 需 checkable=true（D12，官方语义）",
            _ => (string?)null,
        };
        if (guard is { } g)
            return ToVisionDomain(conflict.Subject, g);

        // ---- 权威域定案：confusion 盲，不升档（D13）----
        var raw = field switch
        {
            "checked" => xml.Checked,
            "enabled" => Bool(xml.Enabled),
            "selected" => Bool(xml.Selected),
            "focused" => Bool(xml.Focused),
            _ => null,
        };
        if (raw is null)
            return ToVisionDomain(conflict.Subject, $"权威字段 {field} 在快照中缺失");
        // 值域映射仅 *.state（D7：{on,off,partial}）；其余权威字段透传布尔
        var resolved = field == "checked"
            ? raw switch { "true" => "on", "false" => "off", _ => raw }
            : raw;
        var overruled = conflict.Claims.FirstOrDefault(c => c.Value != resolved)?.Producer;
        return new Disposition(
            conflict.Subject, Tier.CategoryAuthority, resolved, overruled,
            $"权威域 {field}·三道门通过（身份唯一 ∧ 新鲜 ∧ 属性有效）·销案不升档");
    }

    /// <summary>subject 后缀 → 权威字段（D12 状态权威集）。text 权威在共享层
    /// 引入 *.text subject 时经同三门接入；当前共享三件套中仅 *.state 生效。</summary>
    private static string? FieldOf(string subject)
    {
        if (subject.EndsWith(".state", StringComparison.Ordinal))
            return "checked"; // *.state 的 canonical 支柱字段（开关类）
        if (subject.EndsWith(".enabled", StringComparison.Ordinal))
            return "enabled";
        if (subject.EndsWith(".selected", StringComparison.Ordinal))
            return "selected";
        if (subject.EndsWith(".focused", StringComparison.Ordinal))
            return "focused";
        return null;
    }

    private static string? Bool(bool? value) =>
        value switch { true => "true", false => "false", _ => null };

    private static Disposition ToVisionDomain(string subject, string basis) =>
        new(subject, Tier.VisionDomain, null, null, basis);
}
