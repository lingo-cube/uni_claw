namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 屏幕的功能角色（受限集合，PRD §5.1：role 受限）
/// </summary>
public enum RegionRole
{
    /// <summary>菜单区域</summary>
    Menu,

    /// <summary>内容区域</summary>
    Content,

    /// <summary>标签栏区域</summary>
    Tabs,

    /// <summary>弹窗/遮罩层</summary>
    Overlay,

    /// <summary>未知角色（无法判定时）</summary>
    Unknown
}

/// <summary>
/// 屏幕的功能区域，包含空间边界和功能角色。role 由枚举受限（PRD §5.1）。
/// </summary>
/// <param name="Id">区域唯一标识</param>
/// <param name="Bounds">空间边界</param>
/// <param name="Role">功能角色</param>
public sealed record class Region(
    string Id,
    BoundingBox Bounds,
    RegionRole Role)
{
    /// <summary>检查点是否在区域内</summary>
    public bool ContainsPoint(double x, double y) => Bounds.ContainsPoint(x, y);
}
