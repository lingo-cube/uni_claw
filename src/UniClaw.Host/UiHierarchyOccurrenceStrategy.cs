using System.Globalization;
using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host;

/// <summary>
/// Live product realization for generic Android hierarchy occurrences. It joins
/// field-level typed hierarchy evidence by capture/node identity and exposes
/// only enabled actionable controls to grounding. No Settings labels or
/// coordinates are encoded here; labels and bounds come from the current dump.
/// </summary>
public sealed class UiHierarchyOccurrenceStrategy : IUiObservationStrategy
{
    private sealed class NodeFields
    {
        public int? ParentIndex { get; set; }
        public string? Class { get; set; }
        public string? ResourceId { get; set; }
        public string? Text { get; set; }
        public string? ContentDescription { get; set; }
        public string? Bounds { get; set; }
        public bool? Clickable { get; set; }
        public bool? Checkable { get; set; }
        public bool? Enabled { get; set; }
        public string? Checked { get; set; }
        public bool? Scrollable { get; set; }
    }

    private readonly Dictionary<string, Dictionary<int, NodeFields>> _captures = new(StringComparer.Ordinal);

    public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
    {
        if (record.Claim.Subject == ProductAssociationStrategy.ScreenIdentitySubject
            || record.Claim.Subject == ProductAssociationStrategy.ScreenRouteSubject)
            return previous?.Occurrences?.Select(Copy).ToArray() ?? Array.Empty<ProposedOccurrence>();

        if (record.Claim.Subject == SharedSubjects.Frame)
            return FrameOccurrence(record, previous);

        if (record.Provenance.Hierarchy is not { } hierarchy)
            return previous?.Occurrences?.Select(Copy).ToArray() ?? Array.Empty<ProposedOccurrence>();

        var nodes = _captures.GetValueOrDefault(hierarchy.CaptureId);
        if (nodes is null)
        {
            nodes = new Dictionary<int, NodeFields>();
            _captures[hierarchy.CaptureId] = nodes;
        }
        var node = nodes.GetValueOrDefault(hierarchy.NodeLocalIndex) ?? new NodeFields();
        node.ParentIndex = hierarchy.ParentLocalIndex;
        Apply(node, hierarchy.Field, record.Claim.Value);
        nodes[hierarchy.NodeLocalIndex] = node;

        var owner = previous?.Containers is { Count: 1 } containers
            ? containers[0].Identity.ContainerId
            : null;
        return Build(nodes, owner, hierarchy.Space);
    }

    private static ProposedOccurrence Copy(OccurrenceBelief o) => new(
        o.OwningContainerId, o.Role, o.SemanticDescriptor, o.State, o.Locator, o.Native, o.Space);

    private static IReadOnlyList<ProposedOccurrence> FrameOccurrence(
        EvidenceRecord record, WorldBeliefRevision? previous)
    {
        try
        {
            using var json = JsonDocument.Parse(record.Claim.Value);
            var root = json.RootElement;
            var bounds = root.GetProperty("b").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var state = root.TryGetProperty("state", out var stateValue) ? stateValue.GetString() : null;
            var frame = root.TryGetProperty("f", out var frameValue)
                ? frameValue.GetString() ?? AdbEffectDriver.SupportedFrame
                : AdbEffectDriver.SupportedFrame;
            var width = root.TryGetProperty("w", out var w) && w.TryGetInt32(out var wi) ? wi : 0;
            var height = root.TryGetProperty("h", out var h) && h.TryGetInt32(out var hi) ? hi : 0;
            var space = width > 0 && height > 0 ? CoordinateSpace.DeviceViewport(width, height) : null;
            var owner = previous?.Containers is { Count: 1 } containers
                ? containers[0].Identity.ContainerId : null;
            return new[] { new ProposedOccurrence(owner, root.GetProperty("role").GetString()!, null,
                state, new SpatialLocator(bounds[0], bounds[1], bounds[2], bounds[3], frame), null, space) };
        }
        catch (Exception) when (record.Claim.Value.Length > 0)
        {
            return Array.Empty<ProposedOccurrence>();
        }
    }

    private static void Apply(NodeFields node, string field, string value)
    {
        switch (field)
        {
            case "class": node.Class = value; break;
            case "resource_id": node.ResourceId = value; break;
            case "text": node.Text = value; break;
            case "content_desc": node.ContentDescription = value; break;
            case "bounds": node.Bounds = value; break;
            case "clickable": node.Clickable = bool.TryParse(value, out var clickable) ? clickable : null; break;
            case "checkable": node.Checkable = bool.TryParse(value, out var checkable) ? checkable : null; break;
            case "enabled": node.Enabled = bool.TryParse(value, out var enabled) ? enabled : null; break;
            case "checked": node.Checked = value; break;
            case "scrollable": node.Scrollable = bool.TryParse(value, out var scrollable) ? scrollable : null; break;
        }
    }

    private static IReadOnlyList<ProposedOccurrence> Build(
        IReadOnlyDictionary<int, NodeFields> nodes, string? owner, CoordinateSpace? space)
    {
        var result = new List<ProposedOccurrence>();
        foreach (var pair in nodes.OrderBy(p => p.Key))
        {
            var node = pair.Value;
            if (node.Enabled != true || node.Bounds is null)
                continue;

            var childText = nodes
                .Where(child => HasAncestor(child.Key, pair.Key, nodes))
                .Select(child => child.Value.Text ?? child.Value.ContentDescription)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            var siblingText = FindContextText(pair.Key, node, nodes);
            // A clickable container owns the text in its subtree. Prefer that
            // local label before an ancestor context label; otherwise every
            // row under the same RecyclerView inherits the first row's text
            // and becomes an ambiguous grounding candidate.
            var label = FirstText(node.Text, node.ContentDescription, childText,
                siblingText, node.ResourceId);
            var isSwitch = node.Checkable == true
                || (node.Class?.EndsWith("Switch", StringComparison.Ordinal) ?? false);
            // AGT-005: a scrollable container is a grounded effect target on its
            // own (swipe). Its stable identity is the resource id — subtree text
            // would collide across nested scrollables on the same page.
            if (node.Scrollable == true)
            {
                result.Add(new ProposedOccurrence(owner, "scrollable",
                    node.ResourceId ?? label, null,
                    TryBounds(node.Bounds, space), null, space));
                continue;
            }
            if (node.Clickable != true && !isSwitch)
                continue;

            var role = isSwitch ? "switch" : "ui.element";
            var state = isSwitch && node.Checked == "checked" ? "checked" : null;
            var native = string.IsNullOrWhiteSpace(node.ResourceId)
                ? null
                : new NativeLocator("android.resource-id", node.ResourceId);
            result.Add(new ProposedOccurrence(owner, role, label, state,
                TryBounds(node.Bounds, space), native, space));
        }
        return result;
    }

    private static string? FindContextText(
        int nodeIndex, NodeFields node,
        IReadOnlyDictionary<int, NodeFields> nodes)
    {
        var ancestor = node.ParentIndex;
        var visited = new HashSet<int>();
        while (ancestor is { } parent && visited.Add(parent))
        {
            var text = nodes
                .Where(candidate => candidate.Key != nodeIndex
                    && (candidate.Value.Text is not null
                        || candidate.Value.ContentDescription is not null))
                .Where(candidate => candidate.Key == parent
                    || HasAncestor(candidate.Key, parent, nodes))
                .Where(candidate => !HasAncestor(candidate.Key, nodeIndex, nodes))
                .Select(candidate => candidate.Value.Text ?? candidate.Value.ContentDescription)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(text))
                return text;
            ancestor = nodes.GetValueOrDefault(parent)?.ParentIndex;
        }
        return null;
    }

    private static string? FirstText(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static bool HasAncestor(int candidate, int ancestor,
        IReadOnlyDictionary<int, NodeFields> nodes)
    {
        var current = candidate;
        var seen = new HashSet<int>();
        while (nodes.TryGetValue(current, out var node) && node.ParentIndex is { } parent
            && seen.Add(current))
        {
            if (parent == ancestor) return true;
            current = parent;
        }
        return false;
    }

    private static SpatialLocator? TryBounds(string raw, CoordinateSpace? space)
    {
        if (space is null) return null;
        var values = raw.Split(',');
        if (values.Length != 4 || !values.All(v => double.TryParse(v, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _))) return null;
        var px = values.Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return new SpatialLocator(px[0] / space.PixelWidth, px[1] / space.PixelHeight,
            px[2] / space.PixelWidth, px[3] / space.PixelHeight, AdbEffectDriver.SupportedFrame);
    }
}
