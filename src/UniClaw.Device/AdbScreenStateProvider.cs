using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UniClaw.Core.Traversal;

namespace UniClaw.Device;

public sealed record class AdbScreenStateResult(
    string Status,
    bool HasScroll,
    double Progress,
    bool IsEndOfList,
    string HierarchyXml,
    string HierarchyFingerprint,
    AdbCommandFailure? Failure)
{
    public bool Succeeded => Failure is null
                             && Status is "scrollable" or "no_scroll" or "verified_end_of_list";
}

public sealed class AdbScreenStateProvider : IScreenStateProvider
{
    private const string RemotePath = "/sdcard/uniclaw-window-dump.xml";

    private readonly IAdbCommandRunner _runner;
    private readonly TimeSpan _timeout;
    private AdbScreenStateResult? _lastResult;

    public AdbScreenStateResult? LastResult => _lastResult;

    public AdbScreenStateProvider(
        IAdbCommandRunner runner,
        TimeSpan? timeout = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public AdbScreenStateProvider(
        string serial,
        string adbPath = "adb",
        TimeSpan? timeout = null)
        : this(
            new AdbCommandRunner(new AdbCommandRunnerOptions(
                serial,
                adbPath,
                timeout ?? TimeSpan.FromSeconds(20))),
            timeout)
    {
    }

    public bool HasScroll() => _lastResult?.Succeeded == true
                               && _lastResult.HasScroll;

    public double GetScrollProgress() => _lastResult?.Succeeded == true
        ? _lastResult.Progress
        : 0;

    public bool IsEndOfList() => _lastResult?.Succeeded == true
                                && _lastResult.IsEndOfList;

    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;

    public async Task<AdbScreenStateResult> RefreshAsync(
        string? previousHierarchyXml = null,
        bool afterScroll = false,
        CancellationToken cancellationToken = default)
    {
        var dump = await _runner.RunAsync(
            AdbCommandRequest.Create(
                ["shell", "uiautomator", "dump", RemotePath],
                _timeout),
            cancellationToken);
        ThrowIfCancelled(dump, cancellationToken);
        if (!dump.Succeeded)
            return Store(Failed("adb_failure", dump));

        var read = await _runner.RunAsync(
            new AdbCommandRequest(
                ImmutableArray.Create("exec-out", "cat", RemotePath),
                _timeout),
            cancellationToken);
        ThrowIfCancelled(read, cancellationToken);
        if (!read.Succeeded)
            return Store(Failed("adb_failure", read));
        if (string.IsNullOrWhiteSpace(read.StandardOutput))
        {
            return Store(new AdbScreenStateResult(
                "xml_parse_failure",
                false,
                0,
                false,
                string.Empty,
                string.Empty,
                new AdbCommandFailure(
                    "invalid_output",
                    "UIAutomator returned empty XML")));
        }

        try
        {
            var current = Parse(read.StandardOutput);
            var verifiedEnd = afterScroll
                              && previousHierarchyXml is not null
                              && current.HasScroll
                              && string.Equals(
                                  current.HierarchyFingerprint,
                                  Fingerprint(previousHierarchyXml),
                                  StringComparison.Ordinal);
            return Store(current with
            {
                Status = verifiedEnd ? "verified_end_of_list" : current.Status,
                Progress = verifiedEnd ? 1 : current.Progress,
                IsEndOfList = verifiedEnd || current.IsEndOfList,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Store(new AdbScreenStateResult(
                "xml_parse_failure",
                false,
                0,
                false,
                read.StandardOutput,
                string.Empty,
                new AdbCommandFailure(
                    "xml_parse_failure",
                    "UIAutomator XML could not be parsed",
                    ex.GetType().Name)));
        }
    }

    private static AdbScreenStateResult Parse(string xml)
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
            return new AdbScreenStateResult(
                "no_scroll",
                false,
                1,
                true,
                xml,
                Fingerprint(xml),
                null);
        }

        var progress = scrollable
            .Select(GetProgress)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(0)
            .Max();
        return new AdbScreenStateResult(
            "scrollable",
            true,
            Math.Clamp(progress, 0, 1),
            false,
            xml,
            Fingerprint(xml),
            null);
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

    private AdbScreenStateResult Store(AdbScreenStateResult result)
    {
        _lastResult = result;
        return result;
    }

    private static AdbScreenStateResult Failed(
        string status,
        AdbCommandResult command) =>
        new(
            status,
            false,
            0,
            false,
            string.Empty,
            string.Empty,
            command.Failure
            ?? new AdbCommandFailure(
                "non_zero_exit",
                $"ADB exited with code {command.ExitCode}"));

    private static void ThrowIfCancelled(
        AdbCommandResult result,
        CancellationToken cancellationToken)
    {
        if (result.Failure?.Kind == "cancelled")
            throw new OperationCanceledException(
                result.Failure.Message,
                cancellationToken);
    }
}
