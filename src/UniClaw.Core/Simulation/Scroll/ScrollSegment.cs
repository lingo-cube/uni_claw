using System.Collections.Immutable;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动分段：将阈值（0-1）与该阈值下可见的元素集合关联。
/// 用于实现累积模式滚动：所有 Threshold 小于等于 CurrentProgress 的分段元素均可见。
/// </summary>
public sealed record class ScrollSegment
{
    /// <summary>阈值 [0.0, 1.0]，表示滚动到该进度时此分段变为可见</summary>
    public double Threshold { get; init; }

    /// <summary>此分段包含的元素集合</summary>
    public ImmutableArray<Domain.Models.Content.MenuItem> Elements { get; init; }

    /// <param name="Threshold">阈值 [0.0, 1.0]</param>
    /// <param name="Elements">此分段包含的元素集合</param>
    public ScrollSegment(double Threshold, ImmutableArray<Domain.Models.Content.MenuItem> Elements)
    {
        if (Threshold < 0.0 || Threshold > 1.0)
            throw new DomainValidationException(nameof(Threshold), Threshold, "Threshold must be in [0.0, 1.0].");

        this.Threshold = Threshold;
        this.Elements = Elements.IsDefault
            ? ImmutableArray<Domain.Models.Content.MenuItem>.Empty
            : Elements;
    }

    /// <summary>创建空分段（无可见元素）</summary>
    public static ScrollSegment Empty(double threshold) =>
        new ScrollSegment(threshold, ImmutableArray<Domain.Models.Content.MenuItem>.Empty);
}
