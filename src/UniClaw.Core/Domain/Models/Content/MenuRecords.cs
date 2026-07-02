using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain.Models.Content;

/// <summary>
/// 菜单项信息。PRD §5.2 ported from content_models.py MenuInfo(BaseModel)。
/// </summary>
public sealed record class MenuInfo
{
    /// <summary>菜单项名称</summary>
    public string Name { get; init; }

    /// <summary>归一化坐标</summary>
    public Coordinate Coordinate { get; init; }

    /// <summary>是否激活</summary>
    public bool Active { get; init; }

    /// <param name="Name">菜单项名称</param>
    /// <param name="Coordinate">归一化坐标</param>
    /// <param name="Active">是否激活</param>
    public MenuInfo(string Name, Coordinate Coordinate, bool Active = false)
    {
        this.Name = Name ?? string.Empty;
        this.Coordinate = Coordinate;
        this.Active = Active;
    }
}

/// <summary>
/// 可点击菜单项。PRD §5.2 ported from content_models.py MenuItem(BaseModel)。
/// </summary>
public sealed record class MenuItem
{
    /// <summary>名称</summary>
    public string Name { get; init; }

    /// <summary>类型</summary>
    public MenuItemType Type { get; init; }

    /// <summary>归一化坐标</summary>
    public Coordinate Coordinate { get; init; }

    /// <summary>父级名称</summary>
    public string? Parent { get; init; }

    /// <summary>描述</summary>
    public string? Description { get; init; }

    /// <summary>预期操作</summary>
    public ExpectedAction ExpectedAction { get; init; }

    /// <summary>点击是否会导致页面路径变化</summary>
    public bool ExpectsPageChange { get; init; }

    /// <summary>点击是否会导致 UI 状态变化</summary>
    public bool ExpectsStateChange { get; init; }

    /// <param name="Name">名称</param>
    /// <param name="Coordinate">坐标</param>
    /// <param name="Type">类型</param>
    /// <param name="Parent">父级名称</param>
    /// <param name="Description">描述</param>
    /// <param name="ExpectedAction">预期操作</param>
    /// <param name="ExpectsPageChange">预期页面变化</param>
    /// <param name="ExpectsStateChange">预期状态变化</param>
    public MenuItem(
        string Name,
        Coordinate Coordinate,
        MenuItemType Type = MenuItemType.Item,
        string? Parent = null,
        string? Description = null,
        ExpectedAction ExpectedAction = ExpectedAction.Action,
        bool ExpectsPageChange = false,
        bool ExpectsStateChange = false)
    {
        this.Name = Name ?? string.Empty;
        this.Type = Type;
        this.Coordinate = Coordinate;
        this.Parent = Parent;
        this.Description = Description;
        this.ExpectedAction = ExpectedAction;
        this.ExpectsPageChange = ExpectsPageChange;
        this.ExpectsStateChange = ExpectsStateChange;
    }

    /// <summary>生成指纹字符串</summary>
    public string GetFingerprint(string level1, string level2) => $"{level1}|{level2}|{Name}";
}
