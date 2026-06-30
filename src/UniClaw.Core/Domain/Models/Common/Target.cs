namespace UniClaw.Core.Domain.Models.Common;

/// <summary>
/// 目标定位方式枚举
/// </summary>
public enum TargetType
{
    /// <summary>按文本定位</summary>
    Text,

    /// <summary>按坐标定位</summary>
    Coordinate,

    /// <summary>按UI索引定位</summary>
    UiIndex,

    /// <summary>按资源ID定位</summary>
    ResourceId,

    /// <summary>按元素类型定位</summary>
    ElementType
}

/// <summary>
/// 指定如何定位UI元素
/// </summary>
/// <param name="By">定位方式</param>
/// <param name="Value">实际值</param>
/// <param name="Meta">元数据</param>
public sealed record class Target(
    TargetType By,
    object Value,
    Dictionary<string, object>? Meta = null)
{
    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["by"] = By.ToString().ToLowerInvariant(),
            ["value"] = Value
        };

        if (Meta != null && Meta.Count > 0)
            dict["meta"] = new Dictionary<string, object>(Meta);

        return dict;
    }

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static Target? FromDictionary(Dictionary<string, object> data)
    {
        try
        {
            var by = Enum.Parse<TargetType>((data["by"] as string ?? "Text") ?? "Text", true);

            Dictionary<string, object>? meta = null;
            if (data.TryGetValue("meta", out var m) && m is Dictionary<string, object> metaDict)
            {
                meta = new Dictionary<string, object>(metaDict);
            }

            return new Target(
                By: by,
                Value: data["value"] ?? "",
                Meta: meta
            );
        }
        catch
        {
            return null;
        }
    }
}
