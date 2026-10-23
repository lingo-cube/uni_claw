using System.Collections.Immutable;

namespace UniClaw.Kernel.World;

/// <summary>Owner-produced sources actually supplied in the current page/target view.
/// Other model reasoning and absent legacy lineage are explicitly outside this scope.</summary>
public sealed record WorldConsumptionBasis(string SourceRevisionId, string? ContainerId,
    string? CaptureEvidenceId, string? IdentityEvidenceId, IReadOnlyList<string> EvidenceIds, string Coverage);

public sealed partial class WorldModel
{
    internal WorldConsumptionBasis DeriveConsumptionBasis(string? sourceRevisionId = null, string? targetSubject = null, bool onlyTarget = false)
    {
        var r = sourceRevisionId is null ? Current : _revisionHistory.FirstOrDefault(r => r.RevisionId == sourceRevisionId);
        if (r is null) return new(sourceRevisionId ?? "unknown", null, null, null, [], "not_collected");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var targetOccurrence = targetSubject is null ? null : r.Occurrences?.SingleOrDefault(o => o.OccurrenceId == targetSubject);
        var page = onlyTarget && (targetOccurrence is null
            || targetOccurrence.OwningContainerId != r.CurrentPageObservation?.ContainerId) ? null : r.CurrentPageObservation;
        if (page is not null)
        {
            ids.Add(page.EvidenceId);
            if (page.IdentityEvidenceId is not null) ids.Add(page.IdentityEvidenceId);
        }
        if (targetSubject is not null && r.WorldState.TryGetValue(targetSubject, out var target)) ids.Add(target.EvidenceId);
        foreach (var o in r.Occurrences ?? [])
            if (onlyTarget ? o.OccurrenceId == targetSubject : page is null || o.OwningContainerId == page.ContainerId)
                foreach (var id in o.EvidenceBasis) ids.Add(id);
        foreach (var meaning in onlyTarget ? [] : r.SemanticKnowledge)
        {
            ids.Add(meaning.OriginalSemanticEvidenceId); ids.Add(meaning.VerificationEvidenceId);
            foreach (var id in meaning.CorrespondenceEvidenceIds) ids.Add(id);
        }
        if (!onlyTarget && page?.ContainerId is { } owner && r.WorldState.TryGetValue(owner + ".semantic.page", out var claim))
            ids.Add(claim.EvidenceId);
        return new(r.RevisionId, targetOccurrence?.OwningContainerId ?? page?.ContainerId, page?.EvidenceId, page?.IdentityEvidenceId,
            ids.Where(r.EvidenceBasis.Contains).Order(StringComparer.Ordinal).ToImmutableArray(),
            page is null ? "legacy-page-lineage-unproven" : "page-and-target-inputs;other-dependencies-unproven");
    }
}
