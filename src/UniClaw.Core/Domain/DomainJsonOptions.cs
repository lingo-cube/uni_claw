using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain;

/// <summary>
/// Domain 层 JSON 序列化全局选项。camelCase 键名 + enum camelCase 字符串值；
/// 仅保证对象→JSON 单向可输出，不保证 JSON→对象往返（PRD §6 已知限制）。
/// </summary>
public static class DomainJsonOptions
{
    /// <summary>
    /// 默认 camelCase 选项。[JsonPropertyName] 仅作为覆盖。
    /// Enum 值以 camelCase 字符串输出；[JsonPropertyName] 覆盖特定映射（如 "menu_item"）。
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                       new ObjectDictionaryConverter(),
                       new ImmutableObjectDictionaryConverter() }
    };
}
