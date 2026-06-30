using System.Linq;

namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 完整的屏幕视觉分析输出，包含所有识别的元素
/// </summary>
/// <param name="Elements">所有视觉元素列表</param>
/// <param name="ScreenHints">屏幕级元数据</param>
public sealed record class FlattenedScreen(
    List<FlattenedElement> Elements,
    ScreenHints? ScreenHints = null)
{
    /// <summary>
    /// 元素总数
    /// </summary>
    public int ElementCount => Elements.Count;

    /// <summary>
    /// 按区域筛选元素
    /// </summary>
    public List<FlattenedElement> GetElementsInRegion(string regionId) =>
        Elements.Where(e => e.Region == regionId).ToList();

    /// <summary>
    /// 获取选中状态的元素
    /// </summary>
    public List<FlattenedElement> GetSelectedElements() =>
        Elements.Where(e => e.SelectionState == SelectionState.Selected).ToList();

    /// <summary>
    /// 按类型筛选元素
    /// </summary>
    public List<FlattenedElement> GetElementsByType(TypeHint typeHint) =>
        Elements.Where(e => e.TypeHint == typeHint).ToList();

    /// <summary>
    /// 获取可交互元素
    /// </summary>
    public List<FlattenedElement> GetInteractiveElements() =>
        Elements.Where(e => e.IsInteractive).ToList();

    /// <summary>
    /// 按文本模糊搜索元素
    /// </summary>
    public List<FlattenedElement> SearchByText(string searchText, bool caseInsensitive = true)
    {
        var comparison = caseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return Elements
            .Where(e => e.Text.IndexOf(searchText, comparison) >= 0)
            .ToList();
    }

    /// <summary>
    /// 按位置范围获取元素
    /// </summary>
    public List<FlattenedElement> GetElementsInArea(BoundingBox area)
    {
        return Elements
            .Where(e => area.Overlaps(e.BoundingBox))
            .ToList();
    }

    /// <summary>
    /// 获取屏幕提示
    /// </summary>
    public ScreenHints GetScreenHints() => ScreenHints ?? new ScreenHints();

    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["elements"] = Elements.Select(e => e.ToDictionary()).ToList(),
            ["screen_hints"] = ScreenHints?.ToDictionary() ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static FlattenedScreen? FromDictionary(Dictionary<string, object> data)
    {
        try
        {
            List<FlattenedElement> elements = new();

            if (data.TryGetValue("elements", out var e) && e is List<object> elementList)
            {
                foreach (var elementData in elementList)
                {
                    if (elementData is Dictionary<string, object> elementDict)
                    {
                        var element = FlattenedElement.FromDictionary(elementDict);
                        if (element != null)
                            elements.Add(element);
                    }
                }
            }

            ScreenHints? hints = null;
            if (data.TryGetValue("screen_hints", out var h) && h is Dictionary<string, object> hintsDict)
            {
                hints = ScreenHints.FromDictionary(hintsDict);
            }

            return new FlattenedScreen(elements, hints);
        }
        catch
        {
            return null;
        }
    }
}
