namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 屏幕级元数据和布局分析提示
/// </summary>
/// <param name="TopBarText">顶部标题栏文本</param>
/// <param name="LayoutType">整体布局类型</param>
/// <param name="Regions">识别的屏幕区域</param>
/// <param name="OverlayDetected">是否检测到弹窗/遮罩</param>
/// <param name="ScrollDetected">页面是否可滚动</param>
/// <param name="Extra">扩展元数据</param>
public sealed record class ScreenHints(
    string? TopBarText = null,
    string? LayoutType = null,
    List<Region>? Regions = null,
    bool OverlayDetected = false,
    bool ScrollDetected = false,
    Dictionary<string, object>? Extra = null)
{
    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>();

        if (TopBarText != null)
            dict["top_bar_text"] = TopBarText;

        if (LayoutType != null)
            dict["layout_type"] = LayoutType;

        if (Regions != null && Regions.Count > 0)
            dict["regions"] = Regions.Select(r => r.ToDictionary()).ToList();

        dict["overlay_detected"] = OverlayDetected;
        dict["scroll_detected"] = ScrollDetected;

        if (Extra != null && Extra.Count > 0)
            foreach (var kvp in Extra)
                dict[kvp.Key] = kvp.Value;

        return dict;
    }

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static ScreenHints FromDictionary(Dictionary<string, object> data)
    {
        List<Region>? regions = null;

        if (data.TryGetValue("regions", out var r) && r is List<object> regionList)
        {
            regions = new List<Region>();
            foreach (var regionData in regionList)
            {
                if (regionData is Dictionary<string, object> regionDict)
                {
                    var region = Region.FromDictionary(regionDict);
                    if (region != null)
                        regions.Add(region);
                }
            }
        }

        Dictionary<string, object>? extra = null;
        var knownKeys = new HashSet<string> { "top_bar_text", "layout_type", "regions", "overlay_detected", "scroll_detected" };
        foreach (var kvp in data)
        {
            if (!knownKeys.Contains(kvp.Key))
            {
                extra ??= new Dictionary<string, object>();
                extra[kvp.Key] = kvp.Value;
            }
        }

        return new ScreenHints(
            TopBarText: data.TryGetValue("top_bar_text", out var t) ? t as string : null,
            LayoutType: data.TryGetValue("layout_type", out var l) ? l as string : null,
            Regions: regions,
            OverlayDetected: data.TryGetValue("overlay_detected", out var od) && Convert.ToBoolean(od),
            ScrollDetected: data.TryGetValue("scroll_detected", out var sd) && Convert.ToBoolean(sd),
            Extra: extra
        );
    }
}
