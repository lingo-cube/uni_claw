using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 滚动数据存储：管理页面 ID 到滚动分段集合的映射。
/// 线程安全，用于运行时查询滚动数据。
/// </summary>
public sealed class ScrollDataStore
{
    private readonly ImmutableDictionary<string, ImmutableArray<ScrollSegment>> _segments;

    /// <summary>获取所有页面 ID</summary>
    public IEnumerable<string> PageIds => _segments.Keys;

    private ScrollDataStore(ImmutableDictionary<string, ImmutableArray<ScrollSegment>> segments)
    {
        _segments = segments;
    }

    /// <summary>创建空的滚动数据存储</summary>
    public static ScrollDataStore Empty() =>
        new ScrollDataStore(ImmutableDictionary<string, ImmutableArray<ScrollSegment>>.Empty);

    /// <summary>添加页面滚动分段，返回新实例</summary>
    public ScrollDataStore AddSegments(string pageId, ImmutableArray<ScrollSegment> segments)
    {
        var sortedSegments = segments
            .OrderBy(s => s.Threshold)
            .ToImmutableArray();

        return new ScrollDataStore(_segments.SetItem(pageId, sortedSegments));
    }

    /// <summary>获取页面滚动分段集合，不存在返回空数组</summary>
    public ImmutableArray<ScrollSegment> GetSegments(string pageId) =>
        _segments.TryGetValue(pageId, out var segments) ? segments : ImmutableArray<ScrollSegment>.Empty;

    /// <summary>检查页面是否有滚动数据</summary>
    public bool HasScrollData(string pageId) => _segments.ContainsKey(pageId);

    /// <summary>获取页面最大阈值（用于 IsEndOfList 计算），无数据返回 1.0</summary>
    public double GetMaxThreshold(string pageId)
    {
        var segments = GetSegments(pageId);
        return segments.IsEmpty ? 1.0 : segments.Max(s => s.Threshold);
    }

    /// <summary>获取页面最小阈值（用于初始可见性判断），无数据返回 0.0</summary>
    public double GetMinThreshold(string pageId)
    {
        var segments = GetSegments(pageId);
        return segments.IsEmpty ? 0.0 : segments.Min(s => s.Threshold);
    }

    /// <summary>创建包含指定页面数据的构建器</summary>
    public static Builder CreateBuilder() => new Builder();

    /// <summary>构建器：用于批量创建 ScrollDataStore</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, ImmutableArray<ScrollSegment>.Builder> _builders = new();

        /// <summary>添加页面滚动分段</summary>
        public Builder Add(string pageId, params ScrollSegment[] segments)
        {
            if (!_builders.ContainsKey(pageId))
            {
                _builders[pageId] = ImmutableArray.CreateBuilder<ScrollSegment>();
            }

            foreach (var segment in segments)
            {
                _builders[pageId].Add(segment);
            }

            return this;
        }

        /// <summary>构建 ScrollDataStore 实例</summary>
        public ScrollDataStore Build()
        {
            var store = ScrollDataStore.Empty();
            foreach (var kvp in _builders)
            {
                store = store.AddSegments(kvp.Key, kvp.Value.ToImmutable());
            }

            return store;
        }
    }
}
