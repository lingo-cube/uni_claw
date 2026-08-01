using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Domain.Models.Content;
using Xunit.Sdk;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// 单张图片识别的预期结果（golden）DTO。
/// 坐标与 tolerance 均为归一化 (0-1)。Type/Action 为 null 表示不校验该项。
/// </summary>
public sealed record class VisionExpected
{
    public string[] CurrentPath { get; init; } = [];
    public string[] Level1Menus { get; init; } = [];
    public ExpectedVisionItem[] Items { get; init; } = [];
    public bool? HasScroll { get; init; }
    public bool? IsEndOfList { get; init; }
}

public sealed record class ExpectedVisionItem
{
    public string Name { get; init; } = "";
    public string[] Aliases { get; init; } = [];
    public string? Type { get; init; }
    public string? Action { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Tolerance { get; init; } = 0.08;
}

/// <summary>
/// 最小粒度 golden 比较器：PageAnalysis 与预期 JSON 做容差匹配。
/// 语义：预期项必须被识别到（名称或坐标容差匹配）；识别结果中额外项允许存在
/// （真实模型存在合理方差）。Type/Action 指定时校验映射是否与预期一致。
/// </summary>
public static class VisionGoldenComparer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static VisionExpected Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VisionExpected>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Empty golden file: {path}");
    }

    public static string Serialize(VisionExpected expected) =>
        JsonSerializer.Serialize(expected, JsonOptions);

    /// <summary>从实际 PageAnalysis 生成 golden（校准模式用）。</summary>
    public static VisionExpected FromPageAnalysis(PageAnalysis page) =>
        new()
        {
            CurrentPath = page.CurrentPath.ToArray(),
            Level1Menus = page.Level1Menus.Select(m => m.Name).ToArray(),
            Items = page.Items.Select(item => new ExpectedVisionItem
            {
                Name = item.Name,
                Type = EnumStringValue(item.Type),
                Action = EnumStringValue(item.ExpectedAction),
                X = item.Coordinate.X,
                Y = item.Coordinate.Y,
            }).ToArray(),
            HasScroll = page.HasScroll,
            IsEndOfList = page.IsEndOfList,
        };

    public static void AssertMatches(VisionExpected expected, PageAnalysis page)
    {
        var failures = new List<string>();

        if (expected.CurrentPath.Length > 0
            && !page.CurrentPath.SequenceEqual(
                expected.CurrentPath,
                StringComparer.OrdinalIgnoreCase))
        {
            failures.Add(
                $"CurrentPath 不一致：预期 [{string.Join(", ", expected.CurrentPath)}]，"
                + $"实际 [{string.Join(", ", page.CurrentPath)}]");
        }

        foreach (var name in expected.Level1Menus)
        {
            if (!page.Level1Menus.Any(m => NameMatches(m.Name, [name])))
            {
                failures.Add($"Level1 菜单缺失：'{name}'（实际: "
                    + string.Join(", ", page.Level1Menus.Select(m => $"'{m.Name}'")) + "）");
            }
        }

        foreach (var expectedItem in expected.Items)
        {
            var match = FindMatch(expectedItem, page.Items);
            if (match is null)
            {
                failures.Add(
                    $"交互项未识别：'{expectedItem.Name}' (期望坐标 {expectedItem.X:F2},{expectedItem.Y:F2})"
                    + $"。实际项: {string.Join(", ", page.Items.Select(i => $"'{i.Name}'"))}");
                continue;
            }

            if (expectedItem.Type is { } expectedType
                && !string.Equals(
                    EnumStringValue(match.Type),
                    expectedType,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"'{expectedItem.Name}' 类型不一致：预期 '{expectedType}'，"
                    + $"实际 '{EnumStringValue(match.Type)}'");
            }

            if (expectedItem.Action is { } expectedAction
                && !string.Equals(
                    EnumStringValue(match.ExpectedAction),
                    expectedAction,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"'{expectedItem.Name}' 动作不一致：预期 '{expectedAction}'，"
                    + $"实际 '{EnumStringValue(match.ExpectedAction)}'");
            }
        }

        if (expected.HasScroll is { } hasScroll && page.HasScroll != hasScroll)
        {
            failures.Add(
                $"HasScroll 不一致：预期 {hasScroll}，实际 {page.HasScroll}");
        }

        if (expected.IsEndOfList is { } endOfList && page.IsEndOfList != endOfList)
        {
            failures.Add(
                $"IsEndOfList 不一致：预期 {endOfList}，实际 {page.IsEndOfList}");
        }

        if (failures.Count > 0)
        {
            throw new XunitException(
                "视觉识别与预期结果不一致：\n- " + string.Join("\n- ", failures));
        }
    }

    private static MenuItem? FindMatch(
        ExpectedVisionItem expected,
        ImmutableArray<MenuItem> items)
    {
        var names = expected.Aliases
            .Prepend(expected.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToArray();

        // 名称匹配优先；坐标容差兜底。
        return items.FirstOrDefault(item => NameMatches(item.Name, names))
               ?? items.FirstOrDefault(item =>
               {
                   var dx = item.Coordinate.X - expected.X;
                   var dy = item.Coordinate.Y - expected.Y;
                   return Math.Sqrt(dx * dx + dy * dy) <= expected.Tolerance;
               });
    }

    private static bool NameMatches(string actual, string[] expectedNames)
    {
        var normalizedActual = Normalize(actual);
        if (normalizedActual.Length == 0) return false;
        return expectedNames.Any(name =>
        {
            var normalized = Normalize(name);
            return normalized.Length > 0
                   && (normalizedActual.Contains(normalized, StringComparison.Ordinal)
                       || normalized.Contains(normalizedActual, StringComparison.Ordinal));
        });
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string EnumStringValue<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var field = value.GetType().GetField(value.ToString())!;
        return field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
               ?? value.ToString().ToLowerInvariant();
    }
}
