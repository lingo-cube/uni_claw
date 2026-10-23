using System.Collections.Immutable;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    /// <summary>Accumulation from the selected World's verified scroll chain.
    /// Earlier observations stay explicitly historical; only current occurrences carry current state.</summary>
    private WorldAccumulatedContent DeriveAccumulatedContent(WorldBeliefRevision selected, string containerId)
    {
        var unknown = new HashSet<string>(StringComparer.Ordinal) { "whole-page-coverage-unproven" };
        var items = new List<(OccurrenceFact Item, PageObservationState Page, double X, double Y, bool Current)>();
        var refs = new List<string>();
        if (selected.CurrentPageObservation is not { ContainerId: not null } currentPage
            || currentPage.ContainerId != containerId)
            return new(selected.RevisionId, containerId, null, ImmutableArray<WorldContentItem>.Empty,
                ImmutableArray<string>.Empty, ["page-not-current", "whole-page-coverage-unproven"]);
        var snapshots = _revisionHistory.Where(r => r.RevisionNumber <= selected.RevisionNumber)
            .ToDictionary(r => r.RevisionId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { currentPage.EvidenceId };
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        string Group(string id)
        {
            while (groups.TryGetValue(id, out var parent) && parent != id) id = parent;
            return id;
        }
        void Add(WorldBeliefRevision snapshot, PageObservationState page, double x, double y,
            SpatialLocator? contentRegion, bool current, IReadOnlySet<string>? stationary = null)
        {
            foreach (var o in snapshot.Occurrences ?? [])
            {
                if (o.OwningContainerId != containerId || o.Locator is not { } b
                    || !o.EvidenceBasis.Contains(page.EvidenceId)) continue;
                if (!current && (o.Role == "scrollable" || stationary?.Contains(o.OccurrenceId) == true
                    || contentRegion is null || b.SpatialFrameId != contentRegion.SpatialFrameId
                    || b.X1 < contentRegion.X1 || b.Y1 < contentRegion.Y1
                    || b.X2 > contentRegion.X2 || b.Y2 > contentRegion.Y2)) continue;
                items.Add((new(o.OccurrenceId, o.OwningContainerId, o.Role, o.SemanticDescriptor,
                    o.State, o.Locator, o.Native), page, x, y, current));
            }
        }
        Add(selected, currentPage, 0, 0, null, true);
        var cursor = currentPage;
        double dx = 0, dy = 0;
        while (true)
        {
            var incoming = selected.ViewportTransitions.Where(t => t.ContainerId == containerId
                && snapshots.TryGetValue(t.TargetRevisionId, out var r)
                && r.CurrentPageObservation?.EvidenceId == cursor.EvidenceId).ToArray();
            if (incoming.Length == 0) break;
            if (incoming.Length != 1) { unknown.Add("ambiguous-viewport-predecessor"); break; }
            var edge = incoming[0];
            if (edge.AlignmentStatus != "Aligned" || edge.OffsetX is null || edge.OffsetY is null
                || !snapshots.TryGetValue(edge.SourceRevisionId, out var source)
                || source.CurrentPageObservation is not { ContainerId: not null } oldPage
                || oldPage.ContainerId != containerId || oldPage.Capture.DeviceId != currentPage.Capture.DeviceId
                || oldPage.Capture.SessionCorrelation != currentPage.Capture.SessionCorrelation
                || oldPage.Capture.CaptureTime >= cursor.Capture.CaptureTime
                || !seen.Add(oldPage.EvidenceId))
            { unknown.Add("viewport-registration-incomplete:" + edge.OperationReceiptId); break; }
            // The recorded offset maps target into source; this view's frame is
            // the current viewport, so traversing backward subtracts it.
            dx -= edge.OffsetX.Value; dy -= edge.OffsetY.Value;
            foreach (var pair in edge.Correspondences)
            {
                var a = Group(pair.SourceOccurrenceId); var b = Group(pair.TargetOccurrenceId);
                if (a != b) groups[a] = b;
            }
            Add(source, oldPage, dx, dy, edge.SourceRegion, false,
                edge.Correspondences.Where(p => p.Stationary).Select(p => p.SourceOccurrenceId).ToImmutableHashSet(StringComparer.Ordinal));
            refs.Add(edge.VerificationSourceRef);
            cursor = oldPage;
        }
        var projected = items.GroupBy(p => Group(p.Item.OccurrenceId), StringComparer.Ordinal).Select(group =>
        {
            var latest = group.OrderByDescending(p => p.Current).ThenByDescending(p => p.Page.Capture.CaptureTime).First();
            var b = latest.Item.Locator!;
            return new WorldContentItem("content:" + group.Key, containerId, latest.Item.Role, latest.Item.SemanticDescriptor,
                latest.Current ? latest.Item.State : null, latest.Item.State,
                latest.Current ? "observed-current" : "previously-observed-current-presence-unknown",
                new(b.X1 + latest.X, b.Y1 + latest.Y, b.X2 + latest.X, b.Y2 + latest.Y),
                latest.Page.EvidenceId, latest.Page.Capture.CaptureTime,
                group.Select(p => p.Item.OccurrenceId).Distinct(StringComparer.Ordinal).ToImmutableArray());
        }).OrderBy(p => p.Bounds.Y1).ThenBy(p => p.Bounds.X1).ThenBy(p => p.PresentationRef, StringComparer.Ordinal).ToImmutableArray();
        if (projected.Any(p => p.CurrentState is null && p.CurrentPresence != "observed-current"))
            unknown.Add("historical-dynamic-fields-not-current");
        return new(selected.RevisionId, containerId, currentPage.EvidenceId, projected,
            refs.Distinct(StringComparer.Ordinal).ToImmutableArray(), unknown.Order(StringComparer.Ordinal).ToImmutableArray());
    }
}
