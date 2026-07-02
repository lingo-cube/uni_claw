using System.Reflection;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain.Models.Content;

/// <summary>
/// 归一化坐标 (0-1)。PRD §5.2：Coordinate ported from content_models.py。
/// </summary>
public sealed record class Coordinate
{
    /// <summary>X 坐标 (0-1)</summary>
    public double X { get; init; }

    /// <summary>Y 坐标 (0-1)</summary>
    public double Y { get; init; }

    /// <param name="X">X 坐标 [0,1]</param>
    /// <param name="Y">Y 坐标 [0,1]</param>
    public Coordinate(double X, double Y)
    {
        if (X < 0.0 || X > 1.0) throw new DomainValidationException(nameof(X), X);
        if (Y < 0.0 || Y > 1.0) throw new DomainValidationException(nameof(Y), Y);
        this.X = X;
        this.Y = Y;
    }
}

/// <summary>
/// 菜单方向枚举。PRD §5.2 ported from content_models.py Direction(str,Enum)。
/// </summary>
public enum Direction
{
    /// <summary>左</summary>
    [JsonPropertyName("left")] Left,
    /// <summary>右</summary>
    [JsonPropertyName("right")] Right,
    /// <summary>上</summary>
    [JsonPropertyName("top")] Top,
    /// <summary>下</summary>
    [JsonPropertyName("bottom")] Bottom
}

/// <summary>
/// Direction 扩展方法：Values / FromValue / IsValid
/// </summary>
public static class DirectionExtensions
{
    private static readonly IReadOnlyList<string> _values = Enum.GetValues<Direction>()
        .Select(GetStringValue).ToList();

    /// <summary>所有枚举字符串值（从 [JsonPropertyName] 反射构建）</summary>
    public static IReadOnlyList<string> Values => _values;

    /// <summary>从字符串值创建 Direction</summary>
    public static Direction FromValue(string value) => value.ToLowerInvariant() switch
    {
        "left" => Direction.Left,
        "right" => Direction.Right,
        "top" => Direction.Top,
        "bottom" => Direction.Bottom,
        _ => throw new DomainValidationException(nameof(Direction), value)
    };

    /// <summary>判断字符串是否为合法 Direction 值</summary>
    public static bool IsValid(string value) => Values.Contains(value.ToLowerInvariant());

    private static string GetStringValue(Direction d)
    {
        var attr = d.GetType().GetField(d.ToString())!
            .GetCustomAttributes<JsonPropertyNameAttribute>().FirstOrDefault();
        return attr?.Name ?? d.ToString().ToLowerInvariant();
    }
}

/// <summary>
/// 菜单项类型枚举。PRD §5.2 ported from content_models.py MenuItemType(str,Enum)。
/// </summary>
public enum MenuItemType
{
    [JsonPropertyName("menu_item")] MenuItem,
    [JsonPropertyName("tab")] Tab,
    [JsonPropertyName("back_button")] BackButton,
    [JsonPropertyName("switch")] Switch,
    [JsonPropertyName("toggle")] Toggle,
    [JsonPropertyName("button")] Button,
    [JsonPropertyName("icon")] Icon,
    [JsonPropertyName("link")] Link,
    [JsonPropertyName("text")] Text,
    [JsonPropertyName("readonly")] Readonly,
    [JsonPropertyName("item")] Item
}

/// <summary>
/// MenuItemType 扩展方法
/// </summary>
public static class MenuItemTypeExtensions
{
    private static readonly IReadOnlyList<string> _values = Enum.GetValues<MenuItemType>()
        .Select(GetStringValue).ToList();

    /// <summary>所有枚举字符串值</summary>
    public static IReadOnlyList<string> Values => _values;

    /// <summary>从字符串值创建 MenuItemType</summary>
    public static MenuItemType FromValue(string value)
    {
        var lower = value.ToLowerInvariant();
        foreach (var mt in Enum.GetValues<MenuItemType>())
            if (GetStringValue(mt) == lower) return mt;
        throw new DomainValidationException(nameof(MenuItemType), value);
    }

    /// <summary>判断字符串是否为合法 MenuItemType 值</summary>
    public static bool IsValid(string value) => Values.Contains(value.ToLowerInvariant());

    private static string GetStringValue(MenuItemType mt)
    {
        var attr = mt.GetType().GetField(mt.ToString())!
            .GetCustomAttributes<JsonPropertyNameAttribute>().FirstOrDefault();
        return attr?.Name ?? mt.ToString().ToLowerInvariant();
    }
}

/// <summary>
/// 预期操作类型枚举。PRD §5.2 ported from content_models.py ExpectedAction(str,Enum)。
/// </summary>
public enum ExpectedAction
{
    [JsonPropertyName("navigate")] Navigate,
    [JsonPropertyName("toggle")] Toggle,
    [JsonPropertyName("action")] Action,
    [JsonPropertyName("none")] None ///<summary>无预期操作</summary>
}

/// <summary>
/// ExpectedAction 扩展方法
/// </summary>
public static class ExpectedActionExtensions
{
    private static readonly IReadOnlyList<string> _values = Enum.GetValues<ExpectedAction>()
        .Select(GetStringValue).ToList();

    /// <summary>所有枚举字符串值</summary>
    public static IReadOnlyList<string> Values => _values;

    /// <summary>从字符串值创建 ExpectedAction</summary>
    public static ExpectedAction FromValue(string value)
    {
        var lower = value.ToLowerInvariant();
        foreach (var ea in Enum.GetValues<ExpectedAction>())
            if (GetStringValue(ea) == lower) return ea;
        throw new DomainValidationException(nameof(ExpectedAction), value);
    }

    /// <summary>判断字符串是否为合法 ExpectedAction 值</summary>
    public static bool IsValid(string value) => Values.Contains(value.ToLowerInvariant());

    private static string GetStringValue(ExpectedAction ea)
    {
        var attr = ea.GetType().GetField(ea.ToString())!
            .GetCustomAttributes<JsonPropertyNameAttribute>().FirstOrDefault();
        return attr?.Name ?? ea.ToString().ToLowerInvariant();
    }
}
