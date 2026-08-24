using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Device;

public interface IStructuredUiHierarchySource
{
    Task<ImmutableArray<StructuredElementEvidence>> CaptureAsync(int displayWidth, int displayHeight, CancellationToken cancellationToken);
}

public sealed class AdbUiHierarchySource : IStructuredUiHierarchySource
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private readonly IAdbProcessRunner _runner;
    private readonly string _adbExecutable;
    private readonly string _serial;

    public AdbUiHierarchySource(string serial, string adbExecutable = "adb") : this(new AdbProcessRunner(), serial, adbExecutable) { }
    internal AdbUiHierarchySource(IAdbProcessRunner runner, string serial, string adbExecutable)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _serial = string.IsNullOrWhiteSpace(serial) ? throw new ArgumentException("Resolved device serial is required.", nameof(serial)) : serial;
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? throw new ArgumentException("ADB executable is required.", nameof(adbExecutable)) : adbExecutable;
    }

    public async Task<ImmutableArray<StructuredElementEvidence>> CaptureAsync(int displayWidth, int displayHeight, CancellationToken cancellationToken)
    {
        var dump = await _runner.RunAsync(_adbExecutable, ["-s", _serial, "shell", "uiautomator", "dump", "/sdcard/window.xml"], CommandTimeout, cancellationToken);
        if (!dump.Started || dump.ExitCode != 0) return [];
        var read = await _runner.RunAsync(_adbExecutable, ["-s", _serial, "shell", "cat", "/sdcard/window.xml"], CommandTimeout, cancellationToken);
        if (!read.Started || read.ExitCode != 0 || read.StandardOutput.Length == 0) return [];
        return Parse(Encoding.UTF8.GetString(read.StandardOutput), displayWidth, displayHeight);
    }

    public static ImmutableArray<StructuredElementEvidence> Parse(string xml, int displayWidth, int displayHeight)
    {
        if (string.IsNullOrWhiteSpace(xml)) return [];
        XDocument document;
        try { document = XDocument.Parse(xml); } catch (System.Xml.XmlException) { return []; }
        var root = document.Root;
        if (root is null) return [];
        var builder = ImmutableArray.CreateBuilder<StructuredElementEvidence>();
        Walk(root, displayWidth, displayHeight, builder, []);
        return builder.ToImmutable();
    }

    private static void Walk(XElement element, int displayWidth, int displayHeight, ImmutableArray<StructuredElementEvidence>.Builder builder, List<string> path)
    {
        path.Add((string?)element.Attribute("index") ?? "0");
        var className = (string?)element.Attribute("class");
        var resourceId = (string?)element.Attribute("resource-id");
        var clickable = ParseBool((string?)element.Attribute("clickable"));
        var checkable = ParseBool((string?)element.Attribute("checkable"));
        var isChecked = ParseBool((string?)element.Attribute("checked"));
        var enabled = ParseBool((string?)element.Attribute("enabled"));
        var focusable = ParseBool((string?)element.Attribute("focusable"));
        var contentDesc = (string?)element.Attribute("content-desc");
        var bounds = ParseBounds((string?)element.Attribute("bounds"), displayWidth, displayHeight);
        var sourceNodeIdentity = string.Join("/", path);
        // The XML hierarchy element is a virtual container root, not a UI
        // occurrence parent: a node directly under <hierarchy> has no parent.
        var parentSourceNodeIdentity = path.Count > 2 ? string.Join("/", path.Take(path.Count - 1)) : null;

        var isInteractionRelevant = clickable == true
            || checkable == true
            || (className is not null && (className.Contains("Switch", StringComparison.Ordinal)
                || className.Contains("CheckBox", StringComparison.Ordinal)));

        // RAW-TEXT RESOLUTION (primitive, deterministic, never OCR): an
        // interaction-relevant node's RawText is the first non-empty raw text by
        // precedence android:id/title descendant -> own text -> first plain
        // descendant text -> null. Non-interactive top-level nodes keep only
        // their OWN text (their text children are structural children, not row
        // content). This is acquisition-level merging of raw node text only; it
        // performs no title/summary/row/widget role interpretation.
        var rawText = isInteractionRelevant ? ExtractRawText(element) : (string?)element.Attribute("text");

        // ADMISSION GATE: interaction-capable nodes (clickable/checkable/
        // switch-family) and top-level nodes (hierarchy preservation — a parent
        // container with interactive descendants is retained as structure) are
        // admitted. Plain non-top-level text-leaf nodes are merged into their
        // interactive parent and are not emitted as separate occurrences. The
        // adapter performs no page-title/toolbar/row role interpretation; any
        // page identity evidence belongs to the external semantic capability.
        var isTopLevel = path.Count == 2; // direct child of the <hierarchy> root
        if ((isInteractionRelevant || isTopLevel) && IsEligible(bounds))
        {
            builder.Add(new StructuredElementEvidence(className, string.IsNullOrEmpty(resourceId) ? null : resourceId, clickable, checkable, isChecked, enabled, focusable, bounds,
                ContentDescription: string.IsNullOrEmpty(contentDesc) ? null : contentDesc,
                SourceNodeIdentity: sourceNodeIdentity,
                RawText: rawText,
                ParentSourceNodeIdentity: parentSourceNodeIdentity));
        }
        foreach (var child in element.Elements("node")) Walk(child, displayWidth, displayHeight, builder, path);
        path.RemoveAt(path.Count - 1);
    }

    /// <summary>Raw text precedence: title-role descendant, own text, first plain descendant text, null.</summary>
    private static string? ExtractRawText(XElement element)
    {
        var titleText = DescendantText(element, "android:id/title");
        if (!string.IsNullOrWhiteSpace(titleText))
            return titleText;
        var ownText = (string?)element.Attribute("text");
        if (!string.IsNullOrWhiteSpace(ownText))
            return ownText;
        foreach (var descendant in element.Descendants("node"))
        {
            if (string.Equals((string?)descendant.Attribute("resource-id"), "android:id/summary", StringComparison.Ordinal))
                continue; // summary-role descendant is raw summary evidence, never merged into row text
            var text = (string?)descendant.Attribute("text");
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        return null;
    }

    private static string? DescendantText(XElement element, string resourceId)
    {
        if (string.Equals((string?)element.Attribute("resource-id"), resourceId, StringComparison.Ordinal))
            return (string?)element.Attribute("text");
        foreach (var child in element.Elements("node"))
        {
            var result = DescendantText(child, resourceId);
            if (!string.IsNullOrWhiteSpace(result))
                return result;
        }
        return null;
    }

    private static bool? ParseBool(string? value) => value is null ? null : string.Equals(value, "true", StringComparison.Ordinal);
    private static bool IsEligible(ElementBounds? bounds) => bounds is { IsValid: true } && bounds.Width > 0f && bounds.Height > 0f && bounds.X1 < 1f && bounds.X2 > 0f && bounds.Y1 < 1f && bounds.Y2 > 0f;
    private static ElementBounds? ParseBounds(string? value, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(value) || width <= 0 || height <= 0) return null;
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\[(\d+),(\d+)\]\[(\d+),(\d+)\]");
        if (!match.Success) return null;
        var bounds = new ElementBounds(float.Parse(match.Groups[1].Value) / width, float.Parse(match.Groups[2].Value) / height, float.Parse(match.Groups[3].Value) / width, float.Parse(match.Groups[4].Value) / height);
        return bounds.IsValid ? bounds : null;
    }
}
