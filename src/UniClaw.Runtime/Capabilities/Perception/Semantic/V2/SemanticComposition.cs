using System.Collections.Immutable;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.V2;

/// <summary>
/// Deterministic composition evidence between a primary visual occurrence and
/// a structured container. This helper preserves the visual role supplied by
/// the primary source; it does not classify interaction or semantic meaning.
/// </summary>
public static class SemanticComposition
{
    /// <summary>
    /// Finds a unique, credible structured parent for a visual child.
    ///
    /// A parent must contain the child's center, be larger than the child, and
    /// have a structured hierarchy parent. Multiple equally-specific parents
    /// fail closed. Independent interaction is only credited to a structured
    /// node whose bounds substantially overlap the child itself; a clickable
    /// parent therefore cannot make its child interactive.
    /// </summary>
    public static bool TryVerifyChild(
        IReadOnlyCollection<SemanticObservationFact> primaryFacts,
        IReadOnlyCollection<SemanticObservationFact> auxiliaryFacts,
        out SemanticObservationFact parent,
        out bool independentlyInteractive)
    {
        parent = null!;
        independentlyInteractive = false;

        var childBounds = primaryFacts
            .Where(f => f.Kind == SemanticObservationFactKind.Geometry && f.Bounds is not null)
            .Select(f => f.Bounds!)
            .FirstOrDefault();
        if (childBounds is null || !IsComposableVisualRole(primaryFacts))
            return false;

        var groups = auxiliaryFacts
            .GroupBy(f => f.OccurrenceId, StringComparer.Ordinal)
            .Select(group => new
            {
                Facts = group.ToImmutableArray(),
                // Structured facts split geometry from interaction flags;
                // retain the whole occurrence group so a clickable child is
                // not lost merely because its Geometry fact carries no flags.
                Bounds = group.Select(f => f.Bounds).FirstOrDefault(b => b is not null)!,
            })
            .Where(candidate => candidate.Bounds is not null)
            .ToArray();

        var parents = groups
            .Where(candidate => candidate.Bounds.Width * candidate.Bounds.Height
                > childBounds.Width * childBounds.Height)
            .Where(candidate => ContainsCenter(candidate.Bounds, childBounds))
            .Where(candidate => candidate.Facts.Any(f => f.ParentOccurrenceId is not null))
            .OrderBy(candidate => candidate.Bounds.Width * candidate.Bounds.Height)
            .ToArray();

        if (parents.Length == 0)
            return false;

        var smallestArea = parents[0].Bounds.Width * parents[0].Bounds.Height;
        var equallySpecific = parents
            .TakeWhile(candidate =>
                Math.Abs(candidate.Bounds.Width * candidate.Bounds.Height - smallestArea)
                <= smallestArea * 0.05)
            .ToArray();
        if (equallySpecific.Length != 1)
            return false;

        parent = equallySpecific[0].Facts.First(f => f.Bounds is not null);
        independentlyInteractive = groups.Any(candidate =>
            HasIndependentInteraction(candidate.Facts, candidate.Bounds, childBounds));
        return true;
    }

    private static bool IsComposableVisualRole(IReadOnlyCollection<SemanticObservationFact> facts) =>
        facts.Any(f => string.Equals(f.RawProviderType, "icon", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.RawProviderType, "image", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsCenter(SemanticNormalizedBounds container, SemanticNormalizedBounds child)
    {
        var centerX = child.Left + child.Width / 2.0;
        var centerY = child.Top + child.Height / 2.0;
        return centerX >= container.Left && centerX <= container.Left + container.Width
            && centerY >= container.Top && centerY <= container.Top + container.Height;
    }

    private static bool HasIndependentInteraction(
        IReadOnlyCollection<SemanticObservationFact> facts,
        SemanticNormalizedBounds candidate,
        SemanticNormalizedBounds child)
    {
        var intersection = IntersectionArea(candidate, child);
        var childArea = child.Width * child.Height;
        var candidateArea = candidate.Width * candidate.Height;
        if (childArea <= 0 || candidateArea <= 0
            || intersection / childArea < 0.8
            || candidateArea / childArea > 4.0)
            return false;

        return facts.Any(f => f.Clickable == true || f.Checkable == true || f.Focusable == true);
    }

    private static double IntersectionArea(SemanticNormalizedBounds a, SemanticNormalizedBounds b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Left + a.Width, b.Left + b.Width);
        var bottom = Math.Min(a.Top + a.Height, b.Top + b.Height);
        return Math.Max(0, right - left) * Math.Max(0, bottom - top);
    }
}
