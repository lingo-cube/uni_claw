using System.Globalization;
using UniClaw.Kernel.World;
using UniClaw.Kernel.Perception.UiHierarchy;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Text.Json;

namespace UniClaw.Host;

/// <summary>Raw brackets from one Host acquisition. Matching brackets are a
/// stability check, never an assertion that the observations were atomic.</summary>
public sealed record SettingsWindowEvidence(string WindowsBefore, string DisplaysBefore,
    string WindowsAfter, string DisplaysAfter, int AndroidApiLevel);

/// <summary>Non-authoritative observation candidate. World owns currentness.</summary>
public sealed record SettingsPopupClassification(string State, string Reason)
{
    public OverlayWindowEvidence? WindowEvidence { get; init; }
}

public static partial class SettingsPopupClassifier
{
    internal sealed record SceneBasis(string WindowKey, int Width, int Height, int Top, int Bottom);
    private sealed record BarInset(string Id, string Type, Frame Frame, string Flags, string Side);

    /// <summary>Host comparison scope, never a canonical page or overlay fact.
    /// The current application's native inset controls must name the same
    /// visible bars as the display source; window rectangles alone are insufficient.</summary>
    internal static SceneBasis? ReadSceneBasis(SettingsWindowEvidence? source)
    {
        if (source?.AndroidApiLevel != 35
            || !TryRead(source.WindowsBefore, source.DisplaysBefore, out var before)
            || !TryRead(source.WindowsAfter, source.DisplaysAfter, out var after)
            || before!.Focus != after!.Focus || before.Display != after.Display
            || !before.Windows.SequenceEqual(after.Windows)) return null;
        var visible = before.Windows;
        if (visible.Any(w => !w.OnScreen || !w.HasSurface)) return null;
        var apps = visible.Where(w => w.Type is "BASE_APPLICATION" or "APPLICATION").ToArray();
        if (apps.Length != 1 || apps[0].Id != before.Focus || apps[0].Type != "BASE_APPLICATION"
            || apps[0].Activity is null || apps[0].Frame != before.Display
            || visible.Any(w => w.Type is not ("BASE_APPLICATION" or "APPLICATION") && !BenignSystemWindow(w))) return null;
        var first = ReadBars(source.DisplaysBefore, before);
        var last = ReadBars(source.DisplaysAfter, after);
        if (first is null || last is null || !first.SequenceEqual(last)) return null;
        var top = first.Single(i => i.Type == "statusBars");
        var bottom = first.Single(i => i.Type == "navigationBars");
        var status = visible.Where(w => w.Type == "STATUS_BAR" && BenignSystemWindow(w)).ToArray();
        var navigation = visible.Where(w => w.Type == "NAVIGATION_BAR" && BenignSystemWindow(w)).ToArray();
        if (status.Length != 1 || navigation.Length != 1 || status[0].Frame != top.Frame || navigation[0].Frame != bottom.Frame
            || top.Side != "TOP" || bottom.Side != "BOTTOM"
            || top.Frame.Left != 0 || top.Frame.Top != 0 || top.Frame.Right != before.Display.Right
            || bottom.Frame.Left != 0 || bottom.Frame.Right != before.Display.Right || bottom.Frame.Bottom != before.Display.Bottom
            || top.Frame.Bottom >= bottom.Frame.Top) return null;
        var key = JsonSerializer.Serialize(new { before.Focus, before.Display, before.Windows, Insets = first });
        return new(key, before.Display.Right, before.Display.Bottom, top.Frame.Bottom, bottom.Frame.Top);
    }

    private static BarInset[]? ReadBars(string text, Snapshot snapshot)
    {
        var state = Regex.Match(text, @"(?ms)^  WindowInsetsStateController\r?\n    InsetsState\r?\n(.*?)(?=^    Control map:)");
        var controls = Regex.Match(text, @"(?ms)^    Control map:\r?\n(.*?)(?=^    InsetsSourceProviders:)");
        if (!state.Success || !controls.Success) return null;
        if (Regex.Matches(state.Groups[1].Value, @"(?m)^      mDisplayFrame=Rect\(0, 0 - "
            + snapshot.Display.Right + ", " + snapshot.Display.Bottom + @"\)\r?$").Count != 1) return null;
        var app = Regex.Match(controls.Groups[1].Value,
            @"(?ms)^      " + Regex.Escape(snapshot.Focus) + @":\r?\n(.*?)(?=^      Window\{|\z)");
        if (!app.Success) return null;
        var rows = Regex.Matches(state.Groups[1].Value,
            @"(?m)^        InsetsSource id=(\w+) type=(statusBars|navigationBars) frame=\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\] visible=true flags=(.*?) sideHint=(\w+) boundingRects=null\r?$");
        if (rows.Count != 2) return null;
        var result = new List<BarInset>();
        foreach (Match row in rows)
        {
            var n = new int[4];
            for (var i = 0; i < 4; i++)
                if (!int.TryParse(row.Groups[i + 3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i])) return null;
            var frame = new Frame(n[0], n[1], n[2], n[3]);
            if (!frame.Inside(snapshot.Display) || frame.Right <= frame.Left || frame.Bottom <= frame.Top) return null;
            var id = row.Groups[1].Value; var type = row.Groups[2].Value; var flags = row.Groups[7].Value;
            if (flags is not ("" or "INSETS_ROUNDED_CORNER")) return null;
            var hint = type == "statusBars"
                ? $"left=0, top={frame.Bottom}, right=0, bottom=0"
                : $"left=0, top=0, right=0, bottom={snapshot.Display.Bottom - frame.Top}";
            if (Regex.Matches(app.Groups[1].Value, @"InsetsSourceControl: \{" + Regex.Escape(id)
                + @" mType=" + type + @" initiallyVisible .*?mInsetsHint=Insets\{" + Regex.Escape(hint) + @"\}").Count != 1) return null;
            result.Add(new(id, type, frame, flags, row.Groups[8].Value));
        }
        if (result.Select(i => i.Type).Distinct().Count() != 2) return null;
        return result.OrderBy(i => i.Type, StringComparer.Ordinal).ToArray();
    }

    public static SettingsPopupClassification ClassifyWithWindows(string? xml,
        SettingsWindowEvidence? windows, IReadOnlyList<string> popupResourceIds)
    {
        // This source realization has been qualified on API35 only. Package
        // equality is association material, never a popup/absence predicate.
        if (windows is null || windows.AndroidApiLevel != 35 || xml is null)
            return Unknown("window-source-unavailable");
        if (!TryRead(windows.WindowsBefore, windows.DisplaysBefore, out var before)
            || !TryRead(windows.WindowsAfter, windows.DisplaysAfter, out var after))
            return Unknown("window-source-malformed");
        if (before!.Focus != after!.Focus || before.Display != after.Display || !before.Windows.SequenceEqual(after.Windows))
            return Unknown("window-brackets-changed");
        XElement? root;
        try { root = XDocument.Parse(xml).Root; }
        catch (System.Xml.XmlException) { return Unknown("hierarchy-malformed"); }
        var nodes = root?.Descendants("node").ToArray() ?? [];
        if (root?.Name != "hierarchy" || nodes.Length == 0) return Unknown("hierarchy-empty");
        var visible = before.Windows.Where(w => w.Visible).ToArray();
        if (visible.Any(w => !w.OnScreen || !w.HasSurface)) return Unknown("window-visibility-inconsistent");
        var application = visible.Where(w => w.Type is "BASE_APPLICATION" or "APPLICATION").ToArray();
        if (visible.Any(w => w.Type is not ("BASE_APPLICATION" or "APPLICATION") && !BenignSystemWindow(w)))
            return Unknown("visible-window-role-unsupported");
        var focused = application.SingleOrDefault(w => w.Id == before.Focus);
        if (focused is null || nodes.Any(n => (string?)n.Attribute("package") != focused.Package))
            return Unknown("window-hierarchy-unpaired");
        var whitelist = popupResourceIds.ToHashSet(StringComparer.Ordinal);
        var xmlDialog = nodes.Any(n => whitelist.Contains((string?)n.Attribute("resource-id") ?? ""));
        if (application.Length == 1 && focused.Type == "BASE_APPLICATION" && focused.Frame == before.Display && !xmlDialog)
            return new("absent", "supported-source-single-focused-base")
            {
                WindowEvidence = focused.Activity is null ? null : new(focused.Id, focused.Activity, 0, null, [])
                    { VisibleWindowCoverage = new(CoverageCompleteness.CompleteWithinDeclaredSurface) },
            };
        if (application.Length == 2 && focused.Type == "APPLICATION" && xmlDialog)
        {
            var basis = application.SingleOrDefault(w => w.Type == "BASE_APPLICATION");
            if (basis is not null && focused.Activity is not null && focused.Activity == basis.Activity
                && focused.Frame!.Inside(basis.Frame!) && focused.Frame != basis.Frame)
                return new("present", "supported-source-dialog-over-same-activity")
                {
                    WindowEvidence = DialogNodes(nodes, whitelist) is { Length: > 0 } members
                        ? new(basis.Id, basis.Activity!, 0, focused.Id, members)
                            { VisibleWindowCoverage = new(CoverageCompleteness.CompleteWithinDeclaredSurface) } : null,
                };
        }
        return Unknown("window-relation-unproven");
    }

    // A dialog structure alone still grants no Effect authority. Only preserve
    // observed membership when one outer dialog subtree can be identified.
    private static int[]? DialogNodes(XElement[] nodes, HashSet<string> whitelist)
    {
        var roots = nodes.Where(node => whitelist.Contains((string?)node.Attribute("resource-id") ?? "")
            && !node.Ancestors("node").Any(parent => whitelist.Contains((string?)parent.Attribute("resource-id") ?? ""))).ToArray();
        if (roots.Length != 1) return null;
        var members = roots[0].DescendantsAndSelf("node").ToHashSet();
        return nodes.Select((node, index) => (node, index)).Where(item => members.Contains(item.node))
            .Select(item => item.index).ToArray();
    }

    private static SettingsPopupClassification Unknown(string reason) => new("unknown", reason);
    private static bool BenignSystemWindow(Window window) =>
        window.Package == "com.android.systemui" && (window.Type == "STATUS_BAR" && window.Id.EndsWith(" StatusBar}", StringComparison.Ordinal)
            || window.Type == "NAVIGATION_BAR_PANEL" && Regex.IsMatch(window.Id, @" ScreenDecorOverlay(?:Bottom)?\}$")
            || window.Type == "NAVIGATION_BAR" && window.Id.EndsWith(" NavigationBar}", StringComparison.Ordinal))
        || window.Package == "com.android.launcher3" && window.Type == "NAVIGATION_BAR" && window.Id.EndsWith(" Taskbar}", StringComparison.Ordinal);
    private sealed record Frame(int Left, int Top, int Right, int Bottom)
    {
        internal bool Inside(Frame outer) => Left >= outer.Left && Top >= outer.Top
            && Right <= outer.Right && Bottom <= outer.Bottom;
    }
    private sealed record Window(string Id, string Package, string Type, bool Visible,
        bool OnScreen, bool HasSurface, Frame? Frame, string? Activity);
    private sealed record Snapshot(string Focus, Frame Display, Window[] Windows);

    private static bool TryRead(string text, string displays, out Snapshot? snapshot)
    {
        snapshot = null;
        if (!text.StartsWith("WINDOW MANAGER WINDOWS", StringComparison.Ordinal)
            || !text.Contains("  Animation settings:", StringComparison.Ordinal)
            || !text.Contains("mSystemBooted=true mDisplayEnabled=true", StringComparison.Ordinal)
            || !displays.StartsWith("WINDOW MANAGER DISPLAY CONTENTS", StringComparison.Ordinal)
            || Regex.Matches(displays, @"(?m)^  Display:").Count != 1
            || !Regex.IsMatch(displays, @"(?m)^  Display: mDisplayId=0\b")) return false;
        var focus = Regex.Match(displays, @"(?m)^  mCurrentFocus=(Window\{[^\r\n]+\})\r?$");
        if (!focus.Success) return false;
        var size = Regex.Match(displays, @"\bcur=(\d+)x(\d+)\b");
        if (!size.Success || !int.TryParse(size.Groups[1].Value, out var width)
            || !int.TryParse(size.Groups[2].Value, out var height) || width <= 0 || height <= 0) return false;
        var blocks = Regex.Matches(text, @"(?ms)^  Window #(\d+) (.*?)(?=^  Window #|\z)");
        var windows = new List<Window>();
        foreach (Match block in blocks)
        {
            if (!int.TryParse(block.Groups[1].Value, out var index) || index != windows.Count) return false;
            var raw = block.Groups[2].Value.Split("\n\n", 2)[0];
            string? Read(string pattern) { var m = Regex.Match(raw, pattern); return m.Success ? m.Groups[1].Value : null; }
            var id = Read(@"^(Window\{[^\r\n]+\}):");
            var package = Read(@"\bpackage=(\S+)");
            var type = Read(@"\bty=(\w+)");
            if (id is null || package is null || type is null
                || !bool.TryParse(Read(@"(?m)^    isVisible=(true|false)\r?$"), out var visible)
                || !bool.TryParse(Read(@"(?m)^    isOnScreen=(true|false)\r?$"), out var onScreen)
                || !bool.TryParse(Read(@"\bmHasSurface=(true|false)"), out var surface)) return false;
            Frame? frame = null;
            if (visible)
            {
                var f = Regex.Match(raw, @"Frames:.*?\bframe=\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]");
                if (!f.Success) return false;
                var coordinates = new int[4];
                for (var i = 0; i < 4; i++)
                    if (!int.TryParse(f.Groups[i + 1].Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out coordinates[i])) return false;
                frame = new(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
                if (frame.Right <= frame.Left || frame.Bottom <= frame.Top) return false;
            }
            windows.Add(new(id, package, type, visible, onScreen, surface, frame,
                Read(@"\bmToken=(ActivityRecord\{[^}]+\})")));
        }
        if (windows.Count == 0 || windows.Select(w => w.Id).Distinct().Count() != windows.Count) return false;
        // Hidden/retired windows cannot establish current relations and may
        // legitimately change while the visible application remains stable.
        snapshot = new(focus.Groups[1].Value, new(0, 0, width, height), windows.Where(w => w.Visible).ToArray());
        return true;
    }
}
