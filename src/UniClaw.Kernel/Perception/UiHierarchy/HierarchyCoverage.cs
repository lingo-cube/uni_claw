namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// 覆盖完备性（PER-011 Absence and coverage 的对齐值域，capture 级使用前三项；
/// SourceUnavailable 供融合层对齐复用）。
/// </summary>
public enum CoverageCompleteness
{
    /// <summary>在声明覆盖面内完整。</summary>
    CompleteWithinDeclaredSurface,

    /// <summary>部分覆盖（必须携带 limitation；不可解释为全页面 absence）。</summary>
    Partial,

    /// <summary>覆盖未知。</summary>
    Unknown,

    /// <summary>源不可用（capture 级由 SourceUnavailable outcome 表达）。</summary>
    SourceUnavailable,
}

/// <summary>
/// capture 声明覆盖：completeness + limitation。hierarchy 没看到 ≠ 元素不存在；
/// offscreen/virtualized/clip/overlay/scroll/窗口未覆盖只能是 coverage limitation。
/// </summary>
/// <param name="Completeness">覆盖完备性。</param>
/// <param name="Limitation">Partial 时必填的机器可读限制说明。</param>
public sealed record HierarchyCoverage(
    CoverageCompleteness Completeness,
    string? Limitation = null)
{
    /// <summary>Partial 必须携带 limitation（构造期 fail-closed）。</summary>
    public bool IsValid =>
        Completeness != CoverageCompleteness.Partial
        || !string.IsNullOrWhiteSpace(Limitation);
}
