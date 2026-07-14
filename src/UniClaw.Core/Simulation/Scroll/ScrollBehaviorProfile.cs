using UniClaw.Core.Domain;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动跳跃描述 (sealed record, 无新 enum): 一次 swipe 过冲/跳页的幅度。
/// </summary>
public sealed record class ScrollJump
{
    /// <summary>过冲因子 (≥1.0; 1.0 = 精确前进 PagesPerSwipe 页; &gt;1.0 = 过冲)</summary>
    public double OvershootFactor { get; init; }

    /// <summary>每次 swipe 额外跳过的整页数 (≥0)</summary>
    public int SkipPages { get; init; }

    /// <summary>无跳跃</summary>
    public static ScrollJump None => new(OvershootFactor: 1.0, SkipPages: 0);

    /// <summary>过冲跳跃 (给定因子)</summary>
    public static ScrollJump Overshoot(double factor) => new(OvershootFactor: factor, SkipPages: 0);

    /// <summary>构造跳跃 — 校验 OvershootFactor ≥ 1.0, SkipPages ≥ 0</summary>
    public ScrollJump(double OvershootFactor = 1.0, int SkipPages = 0)
    {
        if (OvershootFactor < 1.0)
            throw new DomainValidationException(nameof(OvershootFactor), OvershootFactor, "OvershootFactor must be >= 1.0.");
        if (SkipPages < 0)
            throw new DomainValidationException(nameof(SkipPages), SkipPages, "SkipPages must be non-negative.");

        this.OvershootFactor = OvershootFactor;
        this.SkipPages = SkipPages;
    }
}

/// <summary>
/// 滚动行为 profile (sealed record, 无新 enum, 见设计 §7.2): 控制 swipe 如何推进
/// <see cref="IScrollContentSource"/> 视口 + 可见性模型。取代被删的 ScrollHandlerConfig。
/// </summary>
/// <remarks>
/// 字段:
/// <list type="bullet">
/// <item><c>Cumulative</c>: true = 累积可见 (page 0..currentPage 全展); false = 仅当前页 (windowed)。</item>
/// <item><c>PagesPerSwipe</c>: 一次 swipe 前进的页数 (默认 1)。</item>
/// <item><c>Jump</c>: 跳跃 (过冲/跳页), 默认 <see cref="ScrollJump.None"/>。</item>
/// <item><c>ProgressEpsilon</c>: 进度边界比较容差 (从 ScrollHandlerConfig 迁入)。</item>
/// </list>
/// 便捷构造用 static factory: <see cref="Paged"/> / <see cref="PagedWithJump"/> / <see cref="WithCumulative"/>。
/// </remarks>
public sealed record class ScrollBehaviorProfile
{
    /// <summary>true = 累积可见 0..currentPage; false = 仅当前页 (windowed)</summary>
    public bool Cumulative { get; init; }

    /// <summary>一次 swipe 前进的页数</summary>
    public int PagesPerSwipe { get; init; }

    /// <summary>跳跃 (过冲/跳页)</summary>
    public ScrollJump Jump { get; init; }

    /// <summary>进度边界比较容差</summary>
    public double ProgressEpsilon { get; init; }

    private ScrollBehaviorProfile(bool cumulative, int pagesPerSwipe, ScrollJump jump, double progressEpsilon)
    {
        Cumulative = cumulative;
        PagesPerSwipe = pagesPerSwipe;
        Jump = jump;
        ProgressEpsilon = progressEpsilon;
    }

    /// <summary>分页 (windowed) profile: 仅当前页可见, 每次 swipe 前进 1 页, 无跳跃。</summary>
    public static ScrollBehaviorProfile Paged { get; } =
        new(cumulative: false, pagesPerSwipe: 1, jump: ScrollJump.None, progressEpsilon: 0.001);

    /// <summary>累积可见 profile: 0..currentPage 全展, 每次 swipe 前进 N 页, 无跳跃。</summary>
    public static ScrollBehaviorProfile WithCumulative(int pagesPerSwipe = 1) =>
        new(cumulative: true, pagesPerSwipe: pagesPerSwipe, jump: ScrollJump.None, progressEpsilon: 0.001);

    /// <summary>分页 + 跳跃 profile: windowed + 过冲/跳页。</summary>
    public static ScrollBehaviorProfile PagedWithJump(ScrollJump jump) =>
        new(cumulative: false, pagesPerSwipe: 1, jump: jump, progressEpsilon: 0.001);
}
