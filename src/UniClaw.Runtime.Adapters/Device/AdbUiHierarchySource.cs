using System.Collections.Immutable;
using System.Text;
using System.Xml.Linq;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Device;

/// <summary>
/// Adapter-private UIAutomator hierarchy source.
/// Acquires raw structured Android UI facts from the external device.
/// It does not interpret navigation semantics.
/// </summary>
public interface IStructuredUiHierarchySource
{
    Task<ImmutableArray<StructuredElementEvidence>> CaptureAsync(
        int displayWidth,
        int displayHeight,
        CancellationToken cancellationToken);
}

/// <summary>
/// Concrete UIAutomator hierarchy acquisition through ADB.
/// Runs `uiautomator dump` then reads the dumped XML.
/// </summary>
public sealed class AdbUiHierarchySource : IStructuredUiHierarchySource
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private readonly IAdbProcessRunner _runner;
    private readonly string _adbExecutable;
    private readonly string _serial;

    public AdbUiHierarchySource(string serial, string adbExecutable = "adb")
        : this(new AdbProcessRunner(), serial, adbExecutable) { }

    internal AdbUiHierarchySource(IAdbProcessRunner runner, string serial, string adbExecutable)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _serial = string.IsNullOrWhiteSpace(serial) ? throw new ArgumentException("Resolved device serial is required.", nameof(serial)) : serial;
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? throw new ArgumentException("ADB executable is required.", nameof(adbExecutable)) : adbExecutable;
    }

    public async Task<ImmutableArray<StructuredElementEvidence>> CaptureAsync(
        int displayWidth,
        int displayHeight,
        CancellationToken cancellationToken)
    {
        var dump = await _runner.RunAsync(
            _adbExecutable,
            ["-s", _serial, "shell", "uiautomator", "dump", "/sdcard/window.xml"],
            CommandTimeout,
            cancellationToken);
        if (!dump.Started || dump.ExitCode != 0)
            return [];

        var read = await _runner.RunAsync(
            _adbExecutable,
            ["-s", _serial, "shell", "cat", "/sdcard/window.xml"],
            CommandTimeout,
            cancellationToken);
        if (!read.Started || read.ExitCode != 0 || read.StandardOutput.Length == 0)
            return [];

        var xml = Encoding.UTF8.GetString(read.StandardOutput);
        return Parse(xml, displayWidth, displayHeight);
    }

    public static ImmutableArray<StructuredElementEvidence> Parse(
        string xml,
        int displayWidth,
        int displayHeight)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return [];

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var root = document.Root;
        if (root is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<StructuredElementEvidence>();
        Walk(root, displayWidth, displayHeight, builder, new List<string>());
        return builder.ToImmutable();
    }

    private static void Walk(
        XElement element,
        int displayWidth,
        int displayHeight,
        ImmutableArray<StructuredElementEvidence>.Builder builder,
        List<string> path)
    {
        var index = (string?)element.Attribute("index") ?? "0";
        path.Add(index);

        var className = (string?)element.Attribute("class");
        var resourceId = (string?)element.Attribute("resource-id");
        var clickable = ParseBool((string?)element.Attribute("clickable"));
        var checkable = ParseBool((string?)element.Attribute("checkable"));
        var isChecked = ParseBool((string?)element.Attribute("checked"));
        var enabled = ParseBool((string?)element.Attribute("enabled"));
        var focusable = ParseBool((string?)element.Attribute("focusable"));
        var contentDesc = (string?)element.Attribute("content-desc");
        var bounds = ParseBounds((string?)element.Attribute("bounds"), displayWidth, displayHeight);
        var title = ExtractTitle(element);
        var summary = DescendantText(element, "android:id/summary");
        var hasSwitchChild = DescendantHasSwitchOrCheckable(element);

        var isRelevant =
            clickable == true
            || checkable == true
            || (className is not null && (className.Contains("Switch", StringComparison.Ordinal)
                || className.Contains("CheckBox", StringComparison.Ordinal)));

        // VIEWPORT INTERACTION ELIGIBILITY: an interactive node is admitted as
        // StructuredElementEvidence only when its normalized bounds form a
        // positive-area rectangle inside the current viewport. A clickable /
        // checkable node failing eligibility is a NON_ACTIONABLE_STRUCTURAL_
        // ARTIFACT — e.g. a RecyclerView recycled container captured mid-recycle
        // with negative-height bounds, persistently present in real dumps. It has
        // no actionable standing in the current viewport: it is not a navigation
        // candidate, not a local control, not an UNKNOWN interaction obligation,
        // not a source occurrence, not a dispatch target. It is dropped at this
        // admission boundary (never clamped, never fabricated, never assigned
        // identity from neighbors). Text is NEVER consulted here: a VALID-bounds
        // textless clickable node remains admitted (genuine UNKNOWN, fail
        // closed). The raw dump stays available for diagnostics (callers keep
        // the raw XML); only the Agent's actionable structured evidence excludes
        // the artifact.
        if (isRelevant && IsEligibleViewportInteractionOccurrence(bounds))
        {
            builder.Add(new StructuredElementEvidence(
                className,
                string.IsNullOrEmpty(resourceId) ? null : resourceId,
                clickable,
                checkable,
                isChecked,
                enabled,
                focusable,
                bounds,
                string.IsNullOrEmpty(title) ? null : title,
                string.IsNullOrEmpty(summary) ? null : summary,
                hasSwitchChild,
                string.IsNullOrEmpty(contentDesc) ? null : contentDesc,
                string.Join("/", path)));
        }
        else if (IsPageTitleRoleEvidence(resourceId, contentDesc))
        {
            // PAGE-TITLE-ROLE STRUCTURAL EVIDENCE: the explicit app toolbar
            // title node (non-interactive). On real Settings sub-pages the page
            // title is exposed as the content-desc of the app-bar toolbar
            // (com.android.settings:id/collapsing_toolbar — audited: Location,
            // Location services, Recent access; stable across scrolls; the Root
            // has none). This is STRUCTURAL PAGE evidence (PageClass/PageTitle
            // role), NOT an interaction occurrence: it never counts as an
            // interaction affordance, never enters nav/local/unknown accounting,
            // never becomes an occurrence or dispatch target — it exists so the
            // semantic page identity can be derived from FRESH structured
            // evidence (never OCR, never first-text, never row titles).
            builder.Add(new StructuredElementEvidence(
                className,
                string.IsNullOrEmpty(resourceId) ? null : resourceId,
                Clickable: null,
                Checkable: null,
                Checked: null,
                Enabled: enabled,
                Focusable: null,
                Bounds: bounds,
                TitleText: null,
                SummaryText: null,
                HasSwitchChild: null,
                ContentDescription: contentDesc,
                SourceNodeIdentity: string.Join("/", path)));
        }

        foreach (var child in element.Elements("node"))
            Walk(child, displayWidth, displayHeight, builder, path);

        path.RemoveAt(path.Count - 1);
    }

    /// <summary>
    /// Title extraction precedence — RAW UI node evidence only (deterministic,
    /// never inferred, never OCR):
    ///   1. <c>android:id/title</c> — the element's own resource-id match or the
    ///      first descendant carrying <c>android:id/title</c> (existing
    ///      semantics, preserved);
    ///   2. the element's OWN non-empty <c>text</c> attribute (raw ADB own text —
    ///      e.g. <c>android.widget.Button text="Fixture Root"</c> whose text is
    ///      an attribute, not a descendant);
    ///   3. the first eligible non-summary descendant's non-empty <c>text</c>
    ///      (existing fallback, but explicit summary-role descendants —
    ///      <c>android:id/summary</c> — are excluded: a summary is live
    ///      descriptive evidence, not a title/source identity);
    ///   4. empty.
    /// Distinct texts are NEVER concatenated into a new title. An explicit
    /// summary-role descendant (<c>android:id/summary</c>) is returned as
    /// <c>SummaryText</c> only; it must never leak into <c>TitleText</c> via
    /// the first-descendant fallback.
    /// </summary>
    private static string? ExtractTitle(XElement element)
    {
        var titleId = DescendantText(element, "android:id/title");
        if (!string.IsNullOrWhiteSpace(titleId))
            return titleId;
        var ownText = (string?)element.Attribute("text");
        if (!string.IsNullOrWhiteSpace(ownText))
            return ownText;
        return FirstNonSummaryDescendantText(element);
    }

    /// <summary>Returns the first non-empty text of a descendant that does NOT
    /// carry an explicit summary-role resource-id (<c>android:id/summary</c>).
    /// This prevents a live descriptive summary (e.g. "38% used - 9.97 GB free")
    /// from being promoted to TitleText when the title descendant is temporarily
    /// missing (RecyclerView mid-fling). The summary is still captured as
    /// <see cref="StructuredElementEvidence.SummaryText"/>.</summary>
    private static string? FirstNonSummaryDescendantText(XElement element)
    {
        foreach (var descendant in element.Descendants("node"))
        {
            if (string.Equals((string?)descendant.Attribute("resource-id"), "android:id/summary", StringComparison.Ordinal))
                continue; // explicit summary-role descendant — excluded from title fallback
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

    private static string? FirstDescendantText(XElement element)
    {
        foreach (var descendant in element.Descendants("node"))
        {
            var text = (string?)descendant.Attribute("text");
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        return null;
    }

    private static bool DescendantHasSwitchOrCheckable(XElement element)
    {
        foreach (var descendant in element.Descendants("node"))
        {
            var className = (string?)descendant.Attribute("class");
            if (className is not null && className.Contains("Switch", StringComparison.Ordinal))
                return true;
            if (string.Equals((string?)descendant.Attribute("checkable"), "true", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool? ParseBool(string? value)
        => value is null ? null : string.Equals(value, "true", StringComparison.Ordinal);

    /// <summary>Last path segment of a resource-id (after the last ':' or '/').</summary>
    private static string? ResourceIdLeaf(string? resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return resourceId;
        int colon = resourceId.LastIndexOf(':');
        int slash = resourceId.LastIndexOf('/');
        int cut = Math.Max(colon, slash);
        return cut >= 0 ? resourceId[(cut + 1)..] : resourceId;
    }

    /// <summary>
    /// PAGE-TITLE-ROLE EVIDENCE check: the node is the explicit app-bar toolbar
    /// title (resource-id leaf "collapsing_toolbar" — the Material collapsing
    /// toolbar whose content-desc is the page title on real Settings sub-pages)
    /// carrying a non-empty title. Structural page evidence, not an
    /// interaction occurrence.
    ///
    /// KNOWN-ENVIRONMENT-DEPENDENT (待优化): this rule encodes the toolbar
    /// structure of ONE Android Settings version (Material collapsing toolbar;
    /// the title is exposed as content-desc, not as a title TextView). Older
    /// Settings versions use android:id/action_bar_title; other locales/ROMs
    /// may expose the title differently. Moving to another Android version
    /// requires maintaining this rule (candidate optimizations: a version-
    /// aware toolbar-title-role matcher, or a configurable page-title-role
    /// contract owned by the semantic layer instead of the admission layer).
    /// See openspec/changes/settings-full-tree-enumeration-integration/KNOWN_LIMITATIONS.md.
    /// </summary>
    private static bool IsPageTitleRoleEvidence(string? resourceId, string? contentDesc)
        => !string.IsNullOrWhiteSpace(contentDesc)
            && string.Equals(ResourceIdLeaf(resourceId), "collapsing_toolbar", StringComparison.Ordinal);

    /// <summary>
    /// VIEWPORT INTERACTION ELIGIBILITY contract: a structured interaction
    /// occurrence is eligible to enter the current viewport's interaction-
    /// semantic evidence only when
    ///   Bounds != null
    ///   AND Bounds.IsValid
    ///   AND positive area (Width &gt; 0, Height &gt; 0)
    ///   AND intersects the current viewport (the canonical [0,1]×[0,1] frame).
    /// Bounds are normalized to the full-screenshot frame, so a valid rect is
    /// inside the viewport; the intersection check is stated explicitly per the
    /// contract. Nodes failing this contract are NON_ACTIONABLE_STRUCTURAL_
    /// ARTIFACTS — dropped at the admission boundary, never treated as UNKNOWN
    /// interaction obligations, never allowed to block the evidence-quality
    /// settle. This is a general viewport semantics (bounds-only): it is NOT a
    /// Settings rule, NOT a title/package special-case, and it never consults
    /// text — a valid-bounds textless clickable node remains eligible (genuine
    /// UNKNOWN, fail closed).
    /// </summary>
    private static bool IsEligibleViewportInteractionOccurrence(ElementBounds? bounds)
    {
        if (bounds is null || !bounds.IsValid)
            return false; // no actionable bounds / malformed (negative-height etc.)
        if (bounds.Width <= 0f || bounds.Height <= 0f)
            return false; // zero/negative area — no actionable target
        return bounds.X1 < 1f && bounds.X2 > 0f && bounds.Y1 < 1f && bounds.Y2 > 0f;
    }

    private static ElementBounds? ParseBounds(string? value, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(value) || width <= 0 || height <= 0)
            return null;
        // Format: [x1,y1][x2,y2]
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\[(\d+),(\d+)\]\[(\d+),(\d+)\]");
        if (!match.Success)
            return null;
        var x1 = int.Parse(match.Groups[1].Value);
        var y1 = int.Parse(match.Groups[2].Value);
        var x2 = int.Parse(match.Groups[3].Value);
        var y2 = int.Parse(match.Groups[4].Value);
        var bounds = new ElementBounds(
            (float)x1 / width,
            (float)y1 / height,
            (float)x2 / width,
            (float)y2 / height);
        return bounds.IsValid ? bounds : null;
    }
}
