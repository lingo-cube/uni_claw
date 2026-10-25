using System.Collections.Immutable;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    private static bool MatchesOverlay(OverlayObservation value, PageObservationState page) =>
        value.CaptureId == page.Capture.CaptureId
        && value.ObservationCycleId == page.Capture.ObservationCycleId
        && value.SessionCorrelation == page.Capture.SessionCorrelation
        && value.CaptureTime == page.Capture.CaptureTime;

    private WorldBeliefRevision ReconcileOverlay(EvidenceRecord record, WorldBeliefRevision? parent)
    {
        var number = (parent?.RevisionNumber ?? 0) + 1;
        var basis = (parent?.EvidenceBasis ?? ImmutableHashSet<string>.Empty).ToImmutableHashSet().Add(record.EvidenceId);
        var revision = parent is null
            ? new WorldBeliefRevision($"rev-{number}", null, number, ImmutableDictionary<string, WorldClaim>.Empty,
                [], basis, new(record.Provenance.CaptureTime), new(0), [])
            : parent with { RevisionId = $"rev-{number}", ParentRevisionId = parent.RevisionId,
                RevisionNumber = number, EvidenceBasis = basis };
        if (parent?.CurrentPageObservation is { Capture.RequiresOverlayEvidence: true } page
            && OverlayObservationCodec.TryRead(record.Claim.Value, out var observed)
            && MatchesOverlay(observed!, page) && record.Provenance.CaptureTime == page.Capture.CaptureTime)
        {
            var state = PersistentRevisionDictionary<string, WorldClaim>.Next(
                parent.WorldState as PersistentRevisionDictionary<string, WorldClaim>, StringComparer.Ordinal);
            var conflicts = parent.Conflicts.ToImmutableList();
            OverlayObservation? previous = null;
            var hasCurrent = parent.WorldState.TryGetValue(OverlayObservationCodec.Subject, out var prior)
                && OverlayObservationCodec.TryRead(prior.Value, out previous) && MatchesOverlay(previous!, page);
            var resolved = observed!.State;
            if (hasCurrent && (parent.WorldState.GetValueOrDefault(ProductAssociationStrategy.PopupSubject)?.Value == "conflict"
                || (previous!.State is "present" or "absent") && (resolved is "present" or "absent")
                && previous.State != resolved
                || previous?.WindowEvidence is { } previousWindow && observed.WindowEvidence is { } observedWindow
                    && !OverlayObservationCodec.SameWindowEvidence(previousWindow, observedWindow)))
            {
                resolved = "conflict";
                if (!conflicts.Any(c => c.Subject == ProductAssociationStrategy.PopupSubject))
                    conflicts = conflicts.Add(new Conflict(ProductAssociationStrategy.PopupSubject,
                        previous!.State, observed.State, prior!.EvidenceId, record.EvidenceId));
            }
            // Same-capture contradiction is sticky. A fresh capture provides
            // the only route back to a current, consistent declaration.
            state.SetItem(OverlayObservationCodec.Subject, new(record.Claim.Value, record.EvidenceId,
                record.Provenance.Producer, record.Provenance.Scope));
            state.SetItem(ProductAssociationStrategy.PopupSubject, new(resolved, record.EvidenceId,
                record.Provenance.Producer, record.Provenance.Scope));
            revision = revision with { WorldState = state.Build(), Conflicts = conflicts,
                Uncertainty = new(conflicts.Count),
                WorldGraph = parent.WorldGraph.Concat(new[] { OverlayObservationCodec.Subject, ProductAssociationStrategy.PopupSubject })
                    .Distinct(StringComparer.Ordinal).ToImmutableArray() };
        }
        // Mismatched and retired observations remain in the accepted ledger
        // and revision basis, but cannot replace current overlay state.
        _worldEvidence[record.EvidenceId] = record;
        return PublishRevision(revision);
    }

    private static WorldBeliefRevision RequireCurrentOverlay(WorldBeliefRevision revision)
    {
        if (revision.CurrentPageObservation is not { Capture.RequiresOverlayEvidence: true } page) return revision;
        OverlayObservation? observed = null;
        var correlated = revision.WorldState.TryGetValue(OverlayObservationCodec.Subject, out var claim)
            && OverlayObservationCodec.TryRead(claim.Value, out observed) && MatchesOverlay(observed!, page);
        var conflicts = !correlated
            ? revision.Conflicts.Where(c => c.Subject != ProductAssociationStrategy.PopupSubject).ToImmutableList()
            : revision.Conflicts;
        var value = !correlated ? "unknown" : conflicts.Any(c => c.Subject == ProductAssociationStrategy.PopupSubject)
            ? "conflict" : observed!.State;
        var evidence = correlated ? claim!.EvidenceId : page.EvidenceId;
        if (revision.WorldState.GetValueOrDefault(ProductAssociationStrategy.PopupSubject) is not { } popup
            || popup.Value != value || popup.EvidenceId != evidence)
        {
            var state = PersistentRevisionDictionary<string, WorldClaim>.Next(
                revision.WorldState as PersistentRevisionDictionary<string, WorldClaim>, StringComparer.Ordinal);
            state.SetItem(ProductAssociationStrategy.PopupSubject, new(value, evidence,
                "world.overlay", "capture:" + page.Capture.CaptureId));
            revision = revision with { WorldState = state.Build(), Conflicts = conflicts, Uncertainty = new(conflicts.Count),
                WorldGraph = revision.WorldGraph.Contains(ProductAssociationStrategy.PopupSubject) ? revision.WorldGraph
                    : revision.WorldGraph.Append(ProductAssociationStrategy.PopupSubject).ToImmutableArray() };
        }
        var known = value is "present" or "absent";
        // Identity and overlay consume one Control quota. Resolving identity
        // must not accidentally release an unresolved overlay barrier.
        var identityPending = page.ContainerId is null;
        return revision with { ObservationNeed = identityPending
            ? new(revision.RevisionId, page.EvidenceId, page.Capture)
            : !known ? new(revision.RevisionId, page.EvidenceId, page.Capture, OverlayObservationCodec.NeedKind)
            : revision.ObservationNeed?.Kind == OverlayObservationCodec.NeedKind ? null : revision.ObservationNeed };
    }
}
