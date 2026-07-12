using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// PageStateBuilder 的滚动分段扩展方法。
/// </summary>
public static class ScrollExtensions
{
    /// <summary>为页面添加滚动分段数据</summary>
    /// <param name="builder">页面构建器</param>
    /// <param name="configure">滚动分段配置动作</param>
    /// <returns>修改后的页面构建器</returns>
    public static Simulation.PageStateBuilder ScrollSegments(
        this Simulation.PageStateBuilder builder,
        Action<ScrollSegmentBuilder> configure)
    {
        var segmentBuilder = ScrollSegmentBuilder.Create();
        configure(segmentBuilder);

        // 将滚动分段数据存储在 PageState 的扩展属性中
        // 注意：这需要修改 PageState 来支持扩展数据，或者使用替代方案
        // 当前实现：我们使用 Tag 属性来存储 ScrollData（如果存在）
        // 或者创建一个带滚动数据的 PageState 版本

        // 由于 PageState 当前没有扩展槽，我们使用命名约定：
        // 在构建 StateFixture 后，通过 ScrollDataStore.AddSegments 手动添加
        // 这是在不修改现有类型的情况下添加滚动数据的临时方案

        // 实际实现中，我们需要：
        // 1. 修改 PageState 添加 ScrollSegments 属性
        // 2. 或者创建一个 ExtendedPageState 包装器
        // 3. 或者使用 StateFixture 的扩展方法在 Build 后添加滚动数据

        return builder;
    }

    /// <summary>为 StateFixtureBuilder 添加滚动数据配置支持</summary>
    /// <param name="builder">Fixture 构建器</param>
    /// <param name="pageId">页面 ID</param>
    /// <param name="configure">滚动分段配置</param>
    /// <returns>修改后的 Fixture 构建器</returns>
    public static StateFixtureBuilder WithScrollData(
        this StateFixtureBuilder builder,
        string pageId,
        Action<ScrollSegmentBuilder> configure)
    {
        var segmentBuilder = ScrollSegmentBuilder.Create();
        configure(segmentBuilder);

        // 使用扩展属性存储滚动分段构建器
        // 在 Build() 时会应用这些数据
        ScrollFixtureBuilderExtensions.RegisterScrollData(pageId, segmentBuilder);

        return builder;
    }
}

/// <summary>
/// StateFixtureBuilder 的扩展，用于管理滚动数据注册。
/// </summary>
internal static class ScrollFixtureBuilderExtensions
{
    private static readonly Dictionary<string, ScrollSegmentBuilder> _scrollData = new();

    public static void RegisterScrollData(string pageId, ScrollSegmentBuilder builder)
    {
        _scrollData[pageId] = builder;
    }

    public static ImmutableArray<ScrollSegment> GetScrollData(string pageId)
    {
        if (_scrollData.TryGetValue(pageId, out var builder))
        {
            return builder.Build();
        }
        return ImmutableArray<ScrollSegment>.Empty;
    }

    public static ScrollDataStore BuildScrollDataStore()
    {
        var store = ScrollDataStore.Empty();
        foreach (var kvp in _scrollData)
        {
            store = store.AddSegments(kvp.Key, kvp.Value.Build());
        }
        return store;
    }

    public static void Clear()
    {
        _scrollData.Clear();
    }
}
