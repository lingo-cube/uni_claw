using System.Collections.Immutable;

namespace UniClaw.Kernel.World;

public sealed record WorldPresentationElement(string OccurrenceId, string OwningContainerId,
    string Role, string SemanticDescriptor, string? State, SpatialLocator? Locator);

/// <summary>Pure, non-authoritative presentation shared with Replay. No Oracle or mutation.</summary>
public sealed record WorldPresentation(string SourceRevisionId, string RootContainerId,
    bool IsAuthoritative, IReadOnlyList<WorldPresentationElement> Elements);

public static class SliceContentPresentation
{
    public static WorldPresentation Project(Slice source, string spatialFrameId, SpatialLocator region)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(spatialFrameId);
        ArgumentNullException.ThrowIfNull(region);
        if (string.IsNullOrWhiteSpace(source.SourceRevisionId) || region.SpatialFrameId != spatialFrameId
            || region.X1 >= region.X2 || region.Y1 >= region.Y2)
            throw new ArgumentException("presentation requires a fixed source and positive same-frame region");
        var elements = source.Occurrences.Where(o => o.Locator is { } b && b.SpatialFrameId == spatialFrameId
                && b.X1 >= region.X1 && b.Y1 >= region.Y1 && b.X2 <= region.X2 && b.Y2 <= region.Y2)
            .Select(o => new WorldPresentationElement(o.OccurrenceId, o.OwningContainerId ?? "<missing-parent>",
                o.Role, o.SemanticDescriptor ?? "<missing-descriptor>", o.State, o.Locator))
            .OrderBy(e => e.Locator!.Y1).ThenBy(e => e.Locator!.X1)
            .ThenBy(e => e.Role, StringComparer.Ordinal).ThenBy(e => e.SemanticDescriptor, StringComparer.Ordinal)
            .ThenBy(e => e.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
        return new(source.SourceRevisionId, source.RootContainerId, false, elements);
    }
}
