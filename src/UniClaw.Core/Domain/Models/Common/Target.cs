using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain.Models.Common;

/// <summary>
/// 目标定位方式枚举（PRD §5.3 受限集合）。无 ResourceId / ElementType。
/// </summary>
public enum TargetType
{
    /// <summary>按文本定位</summary>
    Text,

    /// <summary>按坐标定位</summary>
    Coordinate,

    /// <summary>按UI索引定位</summary>
    UiIndex
}

/// <summary>
/// 指定如何定位UI元素。by 受限（枚举 + 越界校验）；Meta 默认空不可变字典；
/// 无 ToDictionary/FromDictionary（PRD §4.4/§5.3）。
/// </summary>
public sealed record class Target
{
    /// <summary>定位方式</summary>
    [JsonPropertyName("by")]
    public TargetType By { get; init; }

    /// <summary>实际值（不透明：文本/坐标/索引，PRD §4.2 object? 仅限此处）</summary>
    [JsonPropertyName("value")]
    public object Value { get; init; }

    /// <summary>元数据（默认空，不可变）</summary>
    [JsonPropertyName("meta")]
    public ImmutableDictionary<string, object> Meta { get; init; } = ImmutableDictionary<string, object>.Empty;

    /// <param name="By">定位方式（受限集合，越界抛异常）</param>
    /// <param name="Value">实际值</param>
    /// <param name="Meta">元数据（默认空）</param>
    public Target(
        TargetType By,
        object Value,
        ImmutableDictionary<string, object>? Meta = null)
    {
        if (!Enum.IsDefined(By))
            throw new DomainValidationException(nameof(By), By);

        this.By = By;
        this.Value = Value ?? string.Empty;
        this.Meta = Meta ?? ImmutableDictionary<string, object>.Empty;
    }
}
