using System.Collections.Immutable;

namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 屏幕级元数据和布局分析提示。extra 为独立嵌套字段（PRD §5.1）；
/// regions 为 ImmutableArray；无 ToDictionary/FromDictionary。
/// </summary>
public sealed record class ScreenHints
{
    /// <summary>顶部标题栏文本</summary>
    public string? TopBarText { get; init; }

    /// <summary>整体布局类型</summary>
    public string? LayoutType { get; init; }

    /// <summary>识别的屏幕区域</summary>
    public ImmutableArray<Region> Regions { get; init; } = ImmutableArray<Region>.Empty;

    /// <summary>是否检测到弹窗/遮罩</summary>
    public bool OverlayDetected { get; init; }

    /// <summary>页面是否可滚动</summary>
    public bool ScrollDetected { get; init; }

    /// <summary>扩展元数据（独立嵌套字段，不可变）</summary>
    public ImmutableDictionary<string, object>? Extra { get; init; }

    /// <param name="TopBarText">顶部标题栏文本</param>
    /// <param name="LayoutType">整体布局类型</param>
    /// <param name="Regions">识别的屏幕区域</param>
    /// <param name="OverlayDetected">是否检测到弹窗/遮罩</param>
    /// <param name="ScrollDetected">页面是否可滚动</param>
    /// <param name="Extra">扩展元数据（独立嵌套字段）</param>
    public ScreenHints(
        string? TopBarText = null,
        string? LayoutType = null,
        ImmutableArray<Region> Regions = default,
        bool OverlayDetected = false,
        bool ScrollDetected = false,
        ImmutableDictionary<string, object>? Extra = null)
    {
        this.TopBarText = TopBarText;
        this.LayoutType = LayoutType;
        this.Regions = Regions.IsDefault ? ImmutableArray<Region>.Empty : Regions;
        this.OverlayDetected = OverlayDetected;
        this.ScrollDetected = ScrollDetected;
        this.Extra = Extra;
    }
}
