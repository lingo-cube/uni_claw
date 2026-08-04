using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

public sealed class AdbScreenStateProvider : IObservableScreenStateProvider,
    IUiAutomatorAvailability
{
    private readonly IAdbSession _session;
    private ScreenStateResult? _lastResult;
    private double _lastProgress;
    private bool _uiAutomatorAvailable = true;

    public ScreenStateResult? LastResult => _lastResult;

    /// <inheritdoc />
    /// <remarks>
    /// Set false on the first <see cref="RefreshAsync"/> failure and never
    /// re-enabled afterwards (core-observation-pipeline D6/AC5): once the
    /// device's UIAutomator is known unreliable, UIA-first analysis is skipped
    /// for the remainder of the session.
    /// </remarks>
    public bool IsUiAutomatorAvailable => _uiAutomatorAvailable;

    public AdbScreenStateProvider(
        IAdbSession session,
        TimeSpan? timeout = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (timeout is TimeSpan t && t <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public AdbScreenStateProvider(
        string serial,
        string adbPath = "adb",
        TimeSpan? timeout = null)
        : this(
            new ProcessAdbSession(new AdbCommandRunnerOptions(
                serial,
                adbPath,
                timeout ?? TimeSpan.FromSeconds(20))),
            timeout)
    {
    }

    public bool HasScroll() => _lastResult?.Succeeded == true
                               && _lastResult.HasScroll;

    public double GetScrollProgress() => _lastResult?.Succeeded == true
        ? _lastProgress
        : 0;

    public bool IsEndOfList() => _lastResult?.Succeeded == true
                                && _lastResult.IsEndOfList;

    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;

    public async Task<ScreenStateResult> RefreshAsync(
        string? previousHierarchyXml = null,
        bool afterScroll = false,
        CancellationToken cancellationToken = default)
    {
        string xml;
        try
        {
            xml = await _session.DumpUiHierarchyAsync(cancellationToken);
        }
        catch (AdbCommandException ex)
        {
            _uiAutomatorAvailable = false;
            return Store(Failed("adb_failure", ex.Result), 0);
        }

        if (string.IsNullOrWhiteSpace(xml))
        {
            _uiAutomatorAvailable = false;
            return Store(new ScreenStateResult(
                Succeeded: false,
                "xml_parse_failure",
                string.Empty,
                string.Empty,
                false,
                false,
                new ScreenFailure(
                    "invalid_output",
                    "UIAutomator returned empty XML")),
                0);
        }

        try
        {
            var current = Parse(xml);
            var verifiedEnd = afterScroll
                              && previousHierarchyXml is not null
                              && current.HasScroll
                              && string.Equals(
                                  current.HierarchyFingerprint,
                                  Fingerprint(previousHierarchyXml),
                                  StringComparison.Ordinal);
            var finalProgress = verifiedEnd ? 1 : current.Progress;
            return Store(new ScreenStateResult(
                Succeeded: true,
                verifiedEnd ? "verified_end_of_list" : current.Status,
                current.HierarchyXml,
                current.HierarchyFingerprint,
                current.HasScroll,
                verifiedEnd || current.IsEndOfList,
                null),
                finalProgress);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _uiAutomatorAvailable = false;
            return Store(new ScreenStateResult(
                Succeeded: false,
                "xml_parse_failure",
                xml,
                string.Empty,
                false,
                false,
                new ScreenFailure(
                    "xml_parse_failure",
                    "UIAutomator XML could not be parsed",
                    ex.GetType().Name)),
                0);
        }
    }

    private record struct ParsedScreen(
        string Status,
        bool HasScroll,
        double Progress,
        bool IsEndOfList,
        string HierarchyXml,
        string HierarchyFingerprint);

    private static ParsedScreen Parse(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var nodes = document.Descendants("node").ToArray();
        var scrollable = nodes.Where(node =>
                string.Equals(
                    (string?)node.Attribute("scrollable"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (scrollable.Length == 0)
        {
            return new ParsedScreen(
                "no_scroll",
                false,
                1,
                true,
                xml,
                Fingerprint(xml));
        }

        var progress = scrollable
            .Select(GetProgress)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(0)
            .Max();
        return new ParsedScreen(
            "scrollable",
            true,
            Math.Clamp(progress, 0, 1),
            false,
            xml,
            Fingerprint(xml));
    }

    private static double? GetProgress(XElement node)
    {
        if (!double.TryParse(
                (string?)node.Attribute("scrollY"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var scrollY)
            || !double.TryParse(
                (string?)node.Attribute("maxScrollY"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var maxScrollY)
            || maxScrollY <= 0)
        {
            return null;
        }

        return scrollY / maxScrollY;
    }

    private static string Fingerprint(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var normalized = string.Join(
            "\n",
            document.Descendants("node")
                .Select(node => string.Join(
                    "|",
                    (string?)node.Attribute("class") ?? string.Empty,
                    (string?)node.Attribute("resource-id") ?? string.Empty,
                    (string?)node.Attribute("text") ?? string.Empty,
                    (string?)node.Attribute("content-desc") ?? string.Empty))
                .OrderBy(value => value, StringComparer.Ordinal));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
    }

    private ScreenStateResult Store(ScreenStateResult result, double progress)
    {
        _lastResult = result;
        _lastProgress = progress;
        return result;
    }

    private static ScreenStateResult Failed(
        string status,
        ShellResult command)
    {
        var message = string.IsNullOrWhiteSpace(command.StandardError)
            ? "ADB command failed"
            : command.StandardError.Trim();
        return new ScreenStateResult(
            Succeeded: false,
            status,
            string.Empty,
            string.Empty,
            false,
            false,
            new ScreenFailure("non_zero_exit", message));
    }
}
