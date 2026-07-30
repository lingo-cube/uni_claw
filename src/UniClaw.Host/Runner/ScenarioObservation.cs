using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;

namespace UniClaw.Host.Runner;

public sealed record class ScenarioObservation(
    byte[] Screenshot,
    string UiXml,
    PageAnalysis Analysis,
    string PageIdentity,
    string PackageName,
    string PageFingerprint,
    string ScreenStateStatus,
    DateTimeOffset Timestamp);

public interface IScenarioObservationSource
{
    Task<ScenarioObservation> ObserveAsync(
        string? previousHierarchyXml = null,
        bool afterScroll = false,
        CancellationToken cancellationToken = default);

    Task<string> GetCurrentFingerprintAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ScenarioObservationException : Exception
{
    public string Kind { get; }

    public ScenarioObservationException(string kind, string message)
        : base(message)
    {
        Kind = kind;
    }
}

public sealed class AdbScenarioObservationSource : IScenarioObservationSource
{
    private readonly IAdbCommandRunner _runner;
    private readonly AdbScreenCapture _capture;
    private readonly IObservableScreenStateProvider _screenState;
    private readonly IPageAnalyzer _pageAnalyzer;
    private readonly bool _useUiAutomatorAnalysis;

    public AdbScenarioObservationSource(
        IAdbCommandRunner runner,
        AdbScreenCapture capture,
        IObservableScreenStateProvider screenState,
        IPageAnalyzer pageAnalyzer,
        bool useUiAutomatorAnalysis)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
        _pageAnalyzer = pageAnalyzer
                        ?? throw new ArgumentNullException(nameof(pageAnalyzer));
        _useUiAutomatorAnalysis = useUiAutomatorAnalysis;
    }

    public async Task<ScenarioObservation> ObserveAsync(
        string? previousHierarchyXml = null,
        bool afterScroll = false,
        CancellationToken cancellationToken = default)
    {
        var screenshot = await _capture.CaptureAsync(cancellationToken);
        var state = await _screenState.RefreshAsync(
            previousHierarchyXml,
            afterScroll,
            cancellationToken);
        if (!state.Succeeded)
        {
            throw new ScenarioObservationException(
                state.Failure?.Kind ?? state.Status,
                state.Failure?.Message
                ?? $"Screen-state observation failed: {state.Status}");
        }

        var packageName = await GetCurrentPackageAsync(cancellationToken);
        var analysis = _useUiAutomatorAnalysis
            ? UiAutomatorPageAnalysis.Parse(state.HierarchyXml, state)
            : await _pageAnalyzer.AnalyzeCurrentPageAsync(cancellationToken)
              ?? throw new ScenarioObservationException(
                  "analysis_empty",
                  "Page analyzer returned no analysis.");
        var pageIdentity = analysis.CurrentPath.LastOrDefault()
                           ?? UiAutomatorPageAnalysis.FindPageIdentity(
                               state.HierarchyXml);
        return new ScenarioObservation(
            screenshot,
            state.HierarchyXml,
            analysis,
            pageIdentity,
            packageName,
            state.HierarchyFingerprint,
            state.Status,
            DateTimeOffset.UtcNow);
    }

    public async Task<string> GetCurrentFingerprintAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await _screenState.RefreshAsync(
            cancellationToken: cancellationToken);
        if (!state.Succeeded)
        {
            throw new ScenarioObservationException(
                state.Failure?.Kind ?? state.Status,
                state.Failure?.Message
                ?? $"Fingerprint observation failed: {state.Status}");
        }
        return state.HierarchyFingerprint;
    }

    private async Task<string> GetCurrentPackageAsync(
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            AdbCommandRequest.Create(
                ["shell", "dumpsys", "activity", "activities"],
                TimeSpan.FromSeconds(10)),
            cancellationToken);
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(cancellationToken);
        if (!result.Succeeded)
        {
            throw new ScenarioObservationException(
                result.Failure?.Kind ?? "adb_failure",
                result.Failure?.Message ?? "Could not read current package.");
        }

        var match = Regex.Match(
            result.StandardOutput,
            @"(?:mResumedActivity|topResumedActivity|mCurrentFocus|mFocusedApp)[^\r\n]*?\s(?<package>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)/",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["package"].Value : "unknown";
    }
}

public static class UiAutomatorPageAnalysis
{
    private static readonly string[] TitleResourceSuffixes =
    [
        "homepage_title",
        "collapsing_toolbar",
        "toolbar_title",
        "action_bar",
    ];

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

    private static bool IsInteractive(XElement node) =>
        string.Equals(
            (string?)node.Attribute("clickable"),
            "true",
            StringComparison.OrdinalIgnoreCase);

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
                0,
                1),
            Math.Clamp(
                ((labelBounds ?? bounds.Value).Top
                 + (labelBounds ?? bounds.Value).Bottom) / 2d / height,
                0,
                1));
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
        return (
            int.Parse(match.Groups["left"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["top"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["right"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["bottom"].Value, CultureInfo.InvariantCulture));
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
