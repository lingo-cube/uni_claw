using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    private const string SemanticPageSubject = "ui.semantic.page";
    private static readonly JsonSerializerOptions SemanticJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    // This is an index of the accepted observation last associated with each
    // container, not a second evidence ledger or a new identity authority.
    private readonly Dictionary<string, EvidenceRecord> _containerObservations = new(StringComparer.Ordinal);

    private static bool CurrentScopedMeaning(string subject, WorldClaim claim, WorldBeliefRevision revision)
    {
        // Legacy observations have their own current-only gate. With an exact
        // page capture, an older scoped claim is history rather than current meaning.
        if (revision.CurrentPageObservation is not { } page) return true;
        if (page.ContainerId is null || subject != page.ContainerId + ".semantic.page"
            || revision.ObservationNeed is not null) return false;
        try
        {
            var meaning = JsonSerializer.Deserialize<SemanticPageClaim>(claim.Value, SemanticJson);
            return meaning is not null && meaning.CaptureId == page.Capture.CaptureId
                && meaning.ObservationCycleId == page.Capture.ObservationCycleId
                && meaning.SessionCorrelation == page.Capture.SessionCorrelation;
        }
        catch (JsonException) { return false; }
    }

    private string? SemanticContainer(EvidenceRecord record, WorldBeliefRevision? previous, out string? reason)
    {
        reason = "semantic-current-observation-unproven";
        SemanticPageClaim? claim;
        try { claim = JsonSerializer.Deserialize<SemanticPageClaim>(record.Claim.Value, SemanticJson); }
        catch (JsonException) { return null; }
        if (claim is not null && previous?.CurrentPageObservation is { } page)
        {
            // Identity recovery and meaning can be siblings derived from the
            // original capture. Do not require an invented parent edge between
            // the two interpretations. The current capture gate stays exact.
            if (page.ContainerId is not { } container || page.Capture.CaptureId != claim.CaptureId
                || previous.WorldState.GetValueOrDefault(CurrentContainerSubject)?.Value != container
                || previous.ObservationNeed is not null
                || page.Capture.ObservationCycleId != claim.ObservationCycleId
                || page.Capture.SessionCorrelation != claim.SessionCorrelation
                || page.Capture.CaptureTime != record.Provenance.CaptureTime
                || !record.DerivedFromEvidenceIds.Contains(page.EvidenceId)
                    && !record.TransitiveEvidenceBasis.Contains(page.EvidenceId)
                || string.IsNullOrWhiteSpace(claim.PageKey) || claim.PageKey == "unknown"
                || string.IsNullOrWhiteSpace(claim.PageName) || string.IsNullOrWhiteSpace(claim.PagePurpose))
                return null;
            reason = null;
            return container;
        }
        if (claim is null || string.IsNullOrWhiteSpace(claim.PageKey) || claim.PageKey == "unknown"
            || string.IsNullOrWhiteSpace(claim.PageName) || string.IsNullOrWhiteSpace(claim.PagePurpose)
            || string.IsNullOrWhiteSpace(claim.CaptureId) || string.IsNullOrWhiteSpace(claim.ObservationCycleId)
            || string.IsNullOrWhiteSpace(claim.SessionCorrelation)
            || previous?.WorldState.TryGetValue(CurrentContainerSubject, out var current) != true
            || !_containerObservations.TryGetValue(current.Value, out var observed)
            || previous.WorldState.TryGetValue(SignatureSubjectPrefix + current.Value, out var signature) != true
            || signature.EvidenceId != observed.EvidenceId
            || record.Provenance.CaptureTime != observed.Provenance.CaptureTime
            || !(record.DerivedFromEvidenceIds.Contains(observed.EvidenceId)
                || record.TransitiveEvidenceBasis.Contains(observed.EvidenceId)))
            return null;

        // Existing source observations expose capture correlation through typed
        // provenance, or the live feed's explicit capture lineage reference.
        // Mere temporal proximity or the same semantic category is insufficient.
        if (observed.Provenance.Hierarchy is { } capture)
        {
            if (capture.CaptureId != claim.CaptureId || capture.CaptureTimestamp != record.Provenance.CaptureTime
                || capture.SessionCorrelation != claim.SessionCorrelation
                || capture.ObservationCycleId is { } cycle && cycle != claim.ObservationCycleId)
                return null;
        }
        else if (!observed.Provenance.TransformationLineage.Contains("capture:" + claim.CaptureId))
            return null;

        reason = null;
        return current.Value;
    }
}
