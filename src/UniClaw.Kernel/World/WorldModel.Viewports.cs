using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    internal bool RecordObservedViewportTransition(ObservedViewportTransition observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var current = Current;
        if (current is null || string.IsNullOrWhiteSpace(observation.OperationReceiptId)
            || string.IsNullOrWhiteSpace(observation.OperationBindingId)
            || string.IsNullOrWhiteSpace(observation.Scope)
            || string.IsNullOrWhiteSpace(observation.VerificationSourceRef)
            || observation.EffectClass is not ("swipe-up" or "swipe-down")) return false;
        var existing = current.ViewportTransitions.SingleOrDefault(t => t.OperationReceiptId == observation.OperationReceiptId);
        if (existing is not null)
            return existing.SourceRevisionId == observation.SourceRevisionId && existing.TargetRevisionId == observation.TargetRevisionId
                && existing.ContainerId == observation.ContainerId && existing.OperationBindingId == observation.OperationBindingId
                && existing.SourceTargetOccurrenceId == observation.SourceTargetOccurrenceId && existing.EffectClass == observation.EffectClass
                && existing.Scope == observation.Scope && existing.VerificationSourceRef == observation.VerificationSourceRef
                && existing.DispatchedAt == observation.DispatchedAt;
        var source = _revisionHistory.FirstOrDefault(r => r.RevisionId == observation.SourceRevisionId);
        var target = _revisionHistory.FirstOrDefault(r => r.RevisionId == observation.TargetRevisionId);
        static string? Page(WorldBeliefRevision r) => r.WorldState.GetValueOrDefault(CurrentContainerSubject)?.Value;
        if (source is null || target is null || current.RevisionId != target.RevisionId
            || source.RevisionNumber >= target.RevisionNumber
            || Page(source) != observation.ContainerId || Page(target) != observation.ContainerId
            || source.FreshnessBasis.AsOf > observation.DispatchedAt
            || target.FreshnessBasis.AsOf <= observation.DispatchedAt) return false;
        var region = source.Occurrences?.SingleOrDefault(o => o.OccurrenceId == observation.SourceTargetOccurrenceId
            && o.OwningContainerId == observation.ContainerId && o.Role == "scrollable");
        if (region is null) return false;
        var sourceEvidence = source.Occurrences!.SelectMany(o => o.EvidenceBasis).Distinct().Order(StringComparer.Ordinal).ToImmutableArray();
        var targetEvidence = (target.Occurrences ?? []).SelectMany(o => o.EvidenceBasis).Distinct().Order(StringComparer.Ordinal).ToImmutableArray();
        if (sourceEvidence.IsEmpty || targetEvidence.IsEmpty || sourceEvidence.Any(id => !source.EvidenceBasis.Contains(id))
            || targetEvidence.Any(id => !target.EvidenceBasis.Contains(id)) || targetEvidence.All(source.EvidenceBasis.Contains)) return false;
        var targetRegions = (target.Occurrences ?? []).Where(o => o.Role == "scrollable" && o.OwningContainerId == observation.ContainerId
            && o.SemanticDescriptor == region.SemanticDescriptor && o.Native == region.Native).ToArray();
        var destination = targetRegions.Length == 1 ? targetRegions[0] : null;
        var alignment = AlignViewports(source, target, region, destination, observation.EffectClass);
        var transition = new ViewportTransition(observation.ContainerId, source.RevisionId, target.RevisionId,
            observation.OperationReceiptId, observation.OperationBindingId, region.OccurrenceId, observation.EffectClass,
            observation.DispatchedAt, observation.Scope, observation.VerificationSourceRef, sourceEvidence, targetEvidence,
            alignment.Status, alignment.X, alignment.Y, alignment.Anchors, region.Space, region.Locator, destination?.Locator);
        PublishRevision(current with
        {
            RevisionId = $"rev-{current.RevisionNumber + 1}", ParentRevisionId = current.RevisionId,
            RevisionNumber = current.RevisionNumber + 1,
            ViewportTransitions = current.ViewportTransitions.ToImmutableList().Add(transition),
        });
        return true;
    }

    private sealed record ViewportAlignment(string Status, double? X = null, double? Y = null,
        IReadOnlyList<ViewportCorrespondence>? Pairs = null)
    {
        public IReadOnlyList<ViewportCorrespondence> Anchors => Pairs ?? ImmutableArray<ViewportCorrespondence>.Empty;
    }

    private static ViewportAlignment AlignViewports(WorldBeliefRevision source, WorldBeliefRevision target,
        OccurrenceBelief region, OccurrenceBelief? destination, string effectClass)
    {
        if (destination is null) return new("ScrollRegionAmbiguousOrMissing");
        if (region.Space is null || destination.Space is null) return new("CoordinateSpaceUnknown");
        if (!region.Space.Matches(destination.Space)) return new("CoordinateSpaceMismatch");
        if (region.Locator is not { } from || destination.Locator is not { } to) return new("RegionBoundsUnknown");
        if (from.SpatialFrameId != to.SpatialFrameId) return new("FrameMismatch");
        // Occurrences currently have geometry, not scroll-parent membership.
        // Overlapping/nested scroll surfaces cannot safely share these anchors.
        static bool HasOverlappingRegion(WorldBeliefRevision revision, OccurrenceBelief selected) =>
            (revision.Occurrences ?? []).Any(o => o.Role == "scrollable" && o.OccurrenceId != selected.OccurrenceId
                && o.OwningContainerId == selected.OwningContainerId && o.Locator is { } bounds
                && bounds.SpatialFrameId == selected.Locator!.SpatialFrameId
                && Math.Max(bounds.X1, selected.Locator.X1) < Math.Min(bounds.X2, selected.Locator.X2)
                && Math.Max(bounds.Y1, selected.Locator.Y1) < Math.Min(bounds.Y2, selected.Locator.Y2));
        if (HasOverlappingRegion(source, region) || HasOverlappingRegion(target, destination))
            return new("OverlappingScrollRegions");
        static bool Within(SpatialLocator b, SpatialLocator r) => b.X1 >= r.X1 && b.Y1 >= r.Y1 && b.X2 <= r.X2 && b.Y2 <= r.Y2;
        static string Key(OccurrenceBelief o) => JsonSerializer.Serialize(new[] { o.Role, o.SemanticDescriptor, o.State, o.Native?.Kind, o.Native?.Value });
        Dictionary<string, OccurrenceBelief[]> Candidates(WorldBeliefRevision revision, OccurrenceBelief scroll) =>
            (revision.Occurrences ?? []).Where(o => o.OwningContainerId == region.OwningContainerId
                && o.Role != "scrollable" && !string.IsNullOrWhiteSpace(o.SemanticDescriptor)
                && o.Locator is { } bounds && bounds.SpatialFrameId == scroll.Locator!.SpatialFrameId
                && o.Space is { } space && space.Matches(scroll.Space!) && Within(bounds, scroll.Locator!))
                .GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var a = Candidates(source, region); var b = Candidates(target, destination);
        var common = a.Keys.Intersect(b.Keys).Order(StringComparer.Ordinal).ToArray();
        if (common.Any(k => a[k].Length != 1 || b[k].Length != 1)) return new("AmbiguousAnchors");
        if (common.Length < 2) return new("InsufficientOverlap");
        var toleranceX = 1.0 / region.Space.PixelWidth; var toleranceY = 1.0 / region.Space.PixelHeight;
        var moving = new List<(double X, double Y)>(); var pairs = new List<ViewportCorrespondence>();
        foreach (var key in common)
        {
            var p = a[key][0]; var q = b[key][0];
            var dx = p.Locator!.X1 - q.Locator!.X1; var dy = p.Locator.Y1 - q.Locator.Y1;
            if (Math.Abs(p.Locator.X2 - q.Locator.X2 - dx) > toleranceX
                || Math.Abs(p.Locator.Y2 - q.Locator.Y2 - dy) > toleranceY) return new("NotPureTranslation");
            var stationary = Math.Abs(dx) <= toleranceX && Math.Abs(dy) <= toleranceY;
            pairs.Add(new(p.OccurrenceId, q.OccurrenceId, stationary));
            if (!stationary) moving.Add((dx, dy));
        }
        if (moving.Count < 2) return new("InsufficientMovingAnchors");
        var offset = moving[0];
        if (moving.Any(d => Math.Abs(d.X - offset.X) > toleranceX || Math.Abs(d.Y - offset.Y) > toleranceY)) return new("NotPureTranslation");
        if ((effectClass == "swipe-up" && offset.Y <= toleranceY) || (effectClass == "swipe-down" && offset.Y >= -toleranceY))
            return new("DirectionMismatch");
        return new("Aligned", offset.X, offset.Y, pairs.ToImmutableArray());
    }
}
