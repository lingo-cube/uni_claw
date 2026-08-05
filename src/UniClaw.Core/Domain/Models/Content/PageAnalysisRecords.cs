using System.Collections.Immutable;

namespace UniClaw.Core.Domain.Models.Content;

/// <summary>
/// 弹窗信息。PRD §5.2 ported from content_models.py PopupInfo(BaseModel)。
/// Content 层拥有完整版（单一源原则 R-4）。
/// </summary>
public sealed record class PopupInfo
{
    /// <summary>弹窗标题</summary>
    public string? Title { get; init; }

    /// <summary>弹窗内容</summary>
    public string? Content { get; init; }

    /// <summary>关闭按钮坐标</summary>
    public Coordinate? CloseButton { get; init; }

    /// <param name="Title">弹窗标题</param>
    /// <param name="Content">弹窗内容</param>
    /// <param name="CloseButton">关闭按钮坐标</param>
    public PopupInfo(string? Title = null, string? Content = null, Coordinate? CloseButton = null)
    {
        this.Title = Title;
        this.Content = Content;
        this.CloseButton = CloseButton;
    }
}

/// <summary>
/// 页面完整分析。PRD §5.2 ported from content_models.py PageAnalysis(BaseModel)。
/// Content 层拥有完整版（单一源原则 R-4）；AI 层简化版后续阶段替换。
/// </summary>
public sealed record class PageAnalysis
{
    /// <summary>一级菜单方向</summary>
    public Direction Level1Dir { get; init; }

    /// <summary>一级菜单列表</summary>
    public ImmutableArray<MenuInfo> Level1Menus { get; init; } = ImmutableArray<MenuInfo>.Empty;

    /// <summary>二级菜单方向</summary>
    public Direction Level2Dir { get; init; }

    /// <summary>二级菜单列表</summary>
    public ImmutableArray<MenuInfo> Level2Menus { get; init; } = ImmutableArray<MenuInfo>.Empty;

    /// <summary>当前路径</summary>
    public ImmutableArray<string> CurrentPath { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>内容项列表</summary>
    public ImmutableArray<MenuItem> Items { get; init; } = ImmutableArray<MenuItem>.Empty;

    /// <summary>
    /// ROI 密度侧通道 — 视觉检测框，扁平 [x1,y1,x2,y2,...]，像素空间与 items
    /// 坐标同源（server 输入图 = C# 发送的预处理图）。仅 local-vision provider
    /// 填充；AI provider 无检测数据 → Empty（RoiSelector 退化为纹理评分）。
    /// </summary>
    public ImmutableArray<int> YoloBboxes { get; init; } = ImmutableArray<int>.Empty;

    /// <summary>是否为弹窗</summary>
    public bool IsPopup { get; init; }

    /// <summary>弹窗信息</summary>
    public PopupInfo? PopupInfo { get; init; }

    /// <summary>关闭按钮坐标</summary>
    public Coordinate? CloseButton { get; init; }

    /// <summary>返回按钮坐标</summary>
    public Coordinate? BackButton { get; init; }

    /// <summary>是否可滚动</summary>
    public bool HasScroll { get; init; }

    /// <summary>是否为列表末尾</summary>
    public bool IsEndOfList { get; init; }

    /// <param name="Level1Dir">一级菜单方向</param>
    /// <param name="Level2Dir">二级菜单方向</param>
    /// <param name="Level1Menus">一级菜单列表</param>
    /// <param name="Level2Menus">二级菜单列表</param>
    /// <param name="CurrentPath">当前路径</param>
    /// <param name="Items">内容项列表</param>
    /// <param name="YoloBboxes">ROI 密度侧通道（扁平像素框，可选）</param>
    /// <param name="IsPopup">是否为弹窗</param>
    /// <param name="PopupInfo">弹窗信息</param>
    /// <param name="CloseButton">关闭按钮坐标</param>
    /// <param name="BackButton">返回按钮坐标</param>
    /// <param name="HasScroll">是否可滚动</param>
    /// <param name="IsEndOfList">是否为列表末尾</param>
    public PageAnalysis(
        Direction Level1Dir,
        Direction Level2Dir,
        ImmutableArray<MenuInfo> Level1Menus = default,
        ImmutableArray<MenuInfo> Level2Menus = default,
        ImmutableArray<string> CurrentPath = default,
        ImmutableArray<MenuItem> Items = default,
        ImmutableArray<int> YoloBboxes = default,
        bool IsPopup = false,
        PopupInfo? PopupInfo = null,
        Coordinate? CloseButton = null,
        Coordinate? BackButton = null,
        bool HasScroll = false,
        bool IsEndOfList = false)
    {
        this.Level1Dir = Level1Dir;
        this.Level1Menus = Level1Menus.IsDefault ? ImmutableArray<MenuInfo>.Empty : Level1Menus;
        this.Level2Dir = Level2Dir;
        this.Level2Menus = Level2Menus.IsDefault ? ImmutableArray<MenuInfo>.Empty : Level2Menus;
        this.CurrentPath = CurrentPath.IsDefault ? ImmutableArray<string>.Empty : CurrentPath;
        this.Items = Items.IsDefault ? ImmutableArray<MenuItem>.Empty : Items;
        this.YoloBboxes = YoloBboxes.IsDefault ? ImmutableArray<int>.Empty : YoloBboxes;
        this.IsPopup = IsPopup;
        this.PopupInfo = PopupInfo;
        this.CloseButton = CloseButton;
        this.BackButton = BackButton;
        this.HasScroll = HasScroll;
        this.IsEndOfList = IsEndOfList;
    }
}
