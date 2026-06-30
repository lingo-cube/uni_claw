namespace UniClaw.Core.Domain.Models.Vision;

/// <summary>
/// 屏幕的功能角色
/// </summary>
public enum RegionRole
{
    /// <summary>
    /// 菜单区域
    /// </summary>
    Menu,

    /// <summary>
    /// 内容区域
    /// </summary>
    Content,

    /// <summary>
    /// 标签栏区域
    /// </summary>
    Tabs,

    /// <summary>
    /// 弹窗/遮罩层
    /// </summary>
    Overlay,

    /// <summary>
    /// 未知角色
    /// </summary>
    Unknown
}

/// <summary>
/// 屏幕的功能区域，包含空间边界和功能角色
/// </summary>
/// <param name="Id">区域唯一标识</param>
/// <param name="Bounds">空间边界</param>
/// <param name="Role">功能角色</param>
public sealed record class Region(
    string Id,
    BoundingBox Bounds,
    RegionRole Role)
{
    /// <summary>
    /// 检查点是否在区域内
    /// </summary>
    public bool ContainsPoint(double x, double y) => Bounds.ContainsPoint(x, y);

    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, object> ToDictionary() => new()
    {
        ["id"] = Id,
        ["bounds"] = new { x = Bounds.X, y = Bounds.Y, width = Bounds.Width, height = Bounds.Height },
        ["role"] = Role.ToString().ToLowerInvariant()
    };

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static Region? FromDictionary(Dictionary<string, object> data)
    {
        try
        {
            var bounds = (data.TryGetValue("bounds", out var b) ? b : null) as dynamic;
            if (bounds == null) return null;

            return new Region(
                Id: data["id"] as string ?? "",
                Bounds: new BoundingBox(
                    X: Convert.ToDouble(bounds.x),
                    Y: Convert.ToDouble(bounds.y),
                    Width: Convert.ToDouble(bounds.width),
                    Height: Convert.ToDouble(bounds.height)
                ),
                Role: Enum.Parse<RegionRole>((data["role"] as string ?? "Unknown") ?? "Unknown", true)
            );
        }
        catch
        {
            return null;
        }
    }
}
