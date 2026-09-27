namespace UniClaw.Kernel.Perception.Fusion;

/// <summary>
/// Deterministic feature-only association. The result is an evidence relation;
/// it never allocates or mutates a canonical identity.
/// </summary>
internal static class OccurrenceAssociator
{
    internal sealed record Features(string Id, string? ResourceId = null, string? Role = null,
        string? Bounds = null, string? CaptureId = null);

    internal static AssociationCandidate Associate(
        Features? hierarchy,
        IReadOnlyList<Features> visuals,
        IReadOnlyList<Features> ocrTokens)
    {
        return Associate(hierarchy is null ? Array.Empty<Features>() : new[] { hierarchy }, visuals, ocrTokens);
    }

    internal static AssociationCandidate Associate(
        IReadOnlyList<Features> hierarchies,
        IReadOnlyList<Features> visuals,
        IReadOnlyList<Features> ocrTokens)
    {
        if (hierarchies.Count == 0 || visuals.Count == 0 && ocrTokens.Count == 0)
            return new(string.Empty, Array.Empty<string>(), Array.Empty<string>(), AssociationDisposition.Unassociated);

        var scored = visuals
            .Select(candidate => (candidate, score: hierarchies.Max(hierarchy => Score(hierarchy, candidate))))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var visualIds = scored.Length == 0
            ? Array.Empty<string>()
            : scored.Where(x => x.score == scored[0].score).Select(x => x.candidate.Id).ToArray();
        var ocrIds = ocrTokens
            .Where(token => hierarchies.Any(hierarchy => Score(hierarchy, token) > 0))
            .Select(token => token.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var disposition = hierarchies.Count > 1 && visualIds.Length == 1
            ? AssociationDisposition.ManyToOne
            : hierarchies.Count == 1 && visualIds.Length > 1 && scored.All(x =>
                EqualNonEmpty(hierarchies[0].ResourceId, x.candidate.ResourceId)
                || EqualNonEmpty(hierarchies[0].Role, x.candidate.Role))
                ? AssociationDisposition.OneToMany
            : visualIds.Length == 1
            ? AssociationDisposition.Unique
            : visualIds.Length > 1 ? AssociationDisposition.Ambiguous
            : ocrIds.Length == 1 ? AssociationDisposition.Unique
            : ocrIds.Length > 1 ? AssociationDisposition.Ambiguous
            : AssociationDisposition.Unassociated;
        return new(hierarchies[0].Id, visualIds, ocrIds, disposition,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["method"] = "deterministic-feature-intersection" });
    }

    private static int Score(Features left, Features right)
    {
        var score = 0;
        if (EqualNonEmpty(left.ResourceId, right.ResourceId)) score += 4;
        if (EqualNonEmpty(left.Role, right.Role)) score += 2;
        if (EqualNonEmpty(left.Bounds, right.Bounds)) score += 1;
        if (EqualNonEmpty(left.CaptureId, right.CaptureId)) score += 1;
        return score;
    }

    private static bool EqualNonEmpty(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.Ordinal);
}
