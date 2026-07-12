using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动分段构建器：用于流畅地构建 ScrollSegment 集合。
/// </summary>
public sealed class ScrollSegmentBuilder
{
    private readonly List<(double threshold, ImmutableArray<MenuItem> elements)> _segments = new();

    /// <summary>添加分段</summary>
    public ScrollSegmentBuilder Add(double threshold, params MenuItem[] elements)
    {
        _segments.Add((threshold, elements.ToImmutableArray()));
        return this;
    }

    /// <summary>添加分段</summary>
    public ScrollSegmentBuilder Add(double threshold, IEnumerable<MenuItem> elements)
    {
        _segments.Add((threshold, elements.ToImmutableArray()));
        return this;
    }

    /// <summary>添加空分段（无可见元素）</summary>
    public ScrollSegmentBuilder AddEmpty(double threshold)
    {
        _segments.Add((threshold, ImmutableArray<MenuItem>.Empty));
        return this;
    }

    /// <summary>构建 ScrollSegment 数组（按阈值排序）</summary>
    public ImmutableArray<ScrollSegment> Build()
    {
        return _segments
            .OrderBy(s => s.threshold)
            .Select(s => new ScrollSegment(s.threshold, s.elements))
            .ToImmutableArray();
    }

    /// <summary>创建新的构建器</summary>
    public static ScrollSegmentBuilder Create() => new();
}
