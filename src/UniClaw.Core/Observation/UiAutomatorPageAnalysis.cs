using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Observation;

/// <summary>
/// Deterministic UIAutomator XML → <see cref="PageAnalysis"/> parser (migrated
/// from the Host runner into Core so the observation pipeline owns the UIA leg).
/// UIAutomator supplies complete labels and trusted coordinates from the
/// hierarchy dump; the parser derives menu shape, interactive items and page
/// identity without any model call.
/// </summary>
public static class UiAutomatorPageAnalysis
{
    private static readonly string[] TitleResourceSuffixes =
    [
        "homepage_title",
        "collapsing_toolbar",
        "toolbar_title",
        "action_bar",
    ];

    /// <summary>Parse a UIAutomator hierarchy dump into a PageAnalysis.</summary>
    /// <param name="xml">The hierarchy XML from the device dump.</param>
    /// <param name="screenState">The screen state the dump was captured with
    /// (scroll / end-of-list flags are copied through).</param>
    public static PageAnalysis Parse(
        string xml,
        ScreenStateResult screenState)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var nodes = document.Descendants("node").ToArray();
        var bounds = nodes
            .Select(node => TryParseBounds((string?)node.Attribute("bounds")))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        var width = Math.Max(1, bounds.Select(value => value.Right).DefaultIfEmpty(1080).Max());
        var height = Math.Max(1, bounds.Select(value => value.Bottom).DefaultIfEmpty(1920).Max());
        var items = nodes
            .Where(IsInteractive)
            .Select(node => MapItem(node, width, height))
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(
                item => $"{Normalize(item.Name)}|{item.Coordinate.X:F4}|{item.Coordinate.Y:F4}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToImmutableArray();
        var title = FindPageIdentity(document);

        // Level1Menus: UIAutomator dump 没有显式的层级标注，但顶层可交互项即页面的一级菜单
        // (Settings 首页是该模式的典型场景)。将已派生的顶层 items 映射为 MenuInfo，
        // 使 UIAutomator 路径与 AI 路径 (PageAnalyzer.MapToPageAnalysis) 在 Level1Menus
        // 形状上对齐。Level2Menus 留 Empty —— UIAutomator dump 无二级层级结构，这与
        // AI 路径在 DTO 缺省 level2_menus 时同样产出 Empty 的诚实值一致。
        // 参见 host-target-architecture 决策 C4/D4。
        var level1Menus = items
            .Select(item => new MenuInfo(item.Name, item.Coordinate, Active: false))
            .ToImmutableArray();

        // Direction fallback: 显式对齐 AI 路径 (PageAnalyzer.cs:141-142) ——
        // DTO 缺省 direction 时回落 Direction.Left。UIAutomator dump 不携带方向语义，
        // 故同样回落 Left；这不是未受管辖的硬编码猜测，而是与 AI 路径同一回落规则的显式声明。
        // 参见 host-target-architecture D4。
        var directionFallback = Direction.Left;

        return new PageAnalysis(
            directionFallback,
            directionFallback,
            Level1Menus: level1Menus,
            Level2Menus: ImmutableArray<MenuInfo>.Empty,
            CurrentPath: [title],
            Items: items,
            HasScroll: screenState.HasScroll,
            IsEndOfList: screenState.IsEndOfList);
    }

    /// <summary>Extract the page identity (toolbar/homepage title) from a raw hierarchy dump.</summary>
    public static string FindPageIdentity(string xml) =>
        FindPageIdentity(XDocument.Parse(xml, LoadOptions.None));

    private static string FindPageIdentity(XDocument document)
    {
        var title = document.Descendants("node")
            .Select(node => new
            {
                Text = TextOf(node),
                Resource = (string?)node.Attribute("resource-id") ?? string.Empty,
            })
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.Text)
                && TitleResourceSuffixes.Any(
                    suffix => candidate.Resource.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase)))
            ?.Text;
        return string.IsNullOrWhiteSpace(title) ? "Settings" : title.Trim();
    }

    private static readonly string[] InputClasses =
    [
        "edittext",
        "searchview",
        "autocompletetextview",
        "multiautocompletetextview",
        "extractedittext",
    ];

    private static bool IsInteractive(XElement node)
    {
        if (!string.Equals(
                (string?)node.Attribute("clickable"),
                "true",
                StringComparison.OrdinalIgnoreCase))
            return false;

        // Skip input fields — opening the keyboard disrupts traversal.
        var className = ((string?)node.Attribute("class") ?? string.Empty).ToLowerInvariant();
        if (Array.Exists(InputClasses, prefix => className.Contains(prefix)))
            return false;

        // Skip toolbar up/back button and the search trigger.
        // The search bar opens a system search overlay that disrupts traversal.
        var resourceId = ((string?)node.Attribute("resource-id") ?? string.Empty)
            .ToLowerInvariant();
        if (resourceId.Contains("search_action_bar"))
            return false;

        var contentDesc = ((string?)node.Attribute("content-desc") ?? string.Empty)
            .ToLowerInvariant();
        if (className.Contains("imagebutton") && contentDesc.Contains("navigate up"))
            return false;

        return true;
    }

    private static MenuItem? MapItem(XElement node, int width, int height)
    {
        var labelNode = string.IsNullOrWhiteSpace(TextOf(node))
            ? node.Descendants("node")
                .FirstOrDefault(
                    descendant => !string.IsNullOrWhiteSpace(
                        TextOf(descendant)))
            : node;
        var text = labelNode is null ? string.Empty : TextOf(labelNode);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Skip summary/subtitle lines — they are not menu items.
        if (string.Equals(
                (string?)labelNode?.Attribute("resource-id"),
                "android:id/summary",
                StringComparison.OrdinalIgnoreCase))
            return null;

        // Reject when the label text comes from an input field descendant
        // (e.g. a clickable ViewGroup wrapping a SearchView / EditText).
        if (labelNode is not null && labelNode != node)
        {
            var labelClass = ((string?)labelNode.Attribute("class") ?? "")
                .ToLowerInvariant();
            if (Array.Exists(InputClasses, prefix => labelClass.Contains(prefix)))
                return null;
        }
        var bounds = TryParseBounds((string?)node.Attribute("bounds"));
        var labelBounds = TryParseBounds(
            (string?)labelNode?.Attribute("bounds"));
        if (bounds is null)
            return null;
        var className = (string?)node.Attribute("class") ?? string.Empty;
        var isToggle = className.Contains("Switch", StringComparison.OrdinalIgnoreCase)
                       || className.Contains("CheckBox", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(
                           (string?)node.Attribute("checkable"),
                           "true",
                           StringComparison.OrdinalIgnoreCase);
        var coordinate = new Coordinate(
            Math.Clamp(
                ((labelBounds ?? bounds.Value).Left
                 + (labelBounds ?? bounds.Value).Right) / 2d / width,
                0.05,
                0.95),
            Math.Clamp(
                ((labelBounds ?? bounds.Value).Top
                 + (labelBounds ?? bounds.Value).Bottom) / 2d / height,
                0.08,
                0.92));
        return new MenuItem(
            text.Trim(),
            coordinate,
            isToggle ? MenuItemType.Toggle : MenuItemType.MenuItem,
            Description: (string?)node.Attribute("resource-id"),
            ExpectedAction: isToggle
                ? ExpectedAction.Toggle
                : ExpectedAction.Navigate,
            ExpectsPageChange: !isToggle,
            ExpectsStateChange: isToggle);
    }

    private static string TextOf(XElement node)
    {
        var text = (string?)node.Attribute("text");
        if (!string.IsNullOrWhiteSpace(text))
            return text;
        return (string?)node.Attribute("content-desc") ?? string.Empty;
    }

    private static (int Left, int Top, int Right, int Bottom)? TryParseBounds(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var match = Regex.Match(
            value,
            @"^\[(?<left>\d+),(?<top>\d+)\]\[(?<right>\d+),(?<bottom>\d+)\]$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;
        var left = int.Parse(match.Groups["left"].Value, CultureInfo.InvariantCulture);
        var top = int.Parse(match.Groups["top"].Value, CultureInfo.InvariantCulture);
        var right = int.Parse(match.Groups["right"].Value, CultureInfo.InvariantCulture);
        var bottom = int.Parse(match.Groups["bottom"].Value, CultureInfo.InvariantCulture);

        // Normalise: UIAutomator sometimes reports inverted bounds for elements
        // that are partially off-screen or captured mid-animation.  Swap axes so
        // the coordinate centre always lands inside the element.
        return (
            Math.Min(left, right),
            Math.Min(top, bottom),
            Math.Max(left, right),
            Math.Max(top, bottom));
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
