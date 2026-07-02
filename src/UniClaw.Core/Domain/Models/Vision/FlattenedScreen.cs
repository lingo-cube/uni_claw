using System.Collections.Immutable;
using System.Linq;

namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 完整的屏幕视觉分析输出。elements 为 ImmutableArray，构造即按 (y,x) 排序（PRD §5.1）；
/// with 副本独立；无 ToDictionary/FromDictionary。
/// </summary>
public sealed record class FlattenedScreen
{
    /// <summary>所有视觉元素（已按 (y,x) 排序）</summary>
    public ImmutableArray<FlattenedElement> Elements { get; init; }

    /// <summary>屏幕级元数据</summary>
    public ScreenHints? ScreenHints { get; init; }

    /// <param name="Elements">所有视觉元素（将按 (y,x) 排序存储）</param>
    /// <param name="ScreenHints">屏幕级元数据</param>
    public FlattenedScreen(
        ImmutableArray<FlattenedElement> Elements,
        ScreenHints? ScreenHints = null)
    {
        if (Elements.IsDefault)
            throw new DomainValidationException(nameof(Elements), Elements);

        this.Elements = Elements
            .OrderBy(e => e.BoundingBox?.Y ?? 0.0)
            .ThenBy(e => e.BoundingBox?.X ?? 0.0)
            .ToImmutableArray();
        this.ScreenHints = ScreenHints;
    }

    /// <summary>元素总数</summary>
    public int ElementCount => Elements.Length;

    /// <summary>按区域筛选元素</summary>
    public ImmutableArray<FlattenedElement> GetElementsInRegion(string regionId) =>
        Elements.Where(e => e.Region == regionId).ToImmutableArray();

    /// <summary>获取选中状态的元素</summary>
    public ImmutableArray<FlattenedElement> GetSelectedElements() =>
        Elements.Where(e => e.SelectionState == SelectionState.Selected).ToImmutableArray();

    /// <summary>按类型筛选元素</summary>
    public ImmutableArray<FlattenedElement> GetElementsByType(TypeHint typeHint) =>
        Elements.Where(e => e.TypeHint == typeHint).ToImmutableArray();

    /// <summary>获取可交互元素</summary>
    public ImmutableArray<FlattenedElement> GetInteractiveElements() =>
        Elements.Where(e => e.IsInteractive).ToImmutableArray();

    /// <summary>按文本模糊搜索元素</summary>
    public ImmutableArray<FlattenedElement> SearchByText(string searchText, bool caseInsensitive = true)
    {
        var comparison = caseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return Elements
            .Where(e => e.Text.IndexOf(searchText, comparison) >= 0)
            .ToImmutableArray();
    }

    /// <summary>按位置范围获取元素</summary>
    public ImmutableArray<FlattenedElement> GetElementsInArea(BoundingBox area) =>
        Elements.Where(e => e.BoundingBox is { } bb && area.Overlaps(bb)).ToImmutableArray();

    /// <summary>获取屏幕提示</summary>
    public ScreenHints GetScreenHints() => ScreenHints ?? new ScreenHints();
}
