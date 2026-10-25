using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    // World's provenance index over records it reconciled. Admission and the
    // canonical store remain exclusively in EvidenceLedger.
    public IReadOnlyList<PageCorrectionDecision> PageCorrectionLog => _pageCorrections.AsReadOnly();

    private WorldBeliefRevision ReconcilePageInterpretation(EvidenceRecord record, WorldBeliefRevision? parent)
    {
        var parsed = PageInterpretationCodec.TryRead(record.Claim.Value, out var correction);
        var original = parsed ? _revisionHistory.LastOrDefault(r =>
            r.CurrentPageObservation?.EvidenceId == correction!.TargetCaptureEvidenceId)?.CurrentPageObservation : null;
        var previousCorrection = parsed ? _pageCorrections.LastOrDefault(c => c.Applied
            && c.TargetCaptureEvidenceId == correction!.TargetCaptureEvidenceId) : null;
        var established = previousCorrection?.CorrectionEvidenceId
            ?? (parsed ? _revisionHistory.LastOrDefault(r => r.CurrentPageObservation?.EvidenceId == correction!.TargetCaptureEvidenceId
                && r.CurrentPageObservation.IdentityEvidenceId is not null)?.CurrentPageObservation?.IdentityEvidenceId : null);
        var previousContainer = previousCorrection?.CorrectedContainerId ?? (parsed ? _revisionHistory.LastOrDefault(r =>
            r.CurrentPageObservation?.EvidenceId == correction!.TargetCaptureEvidenceId
            && r.CurrentPageObservation.IdentityEvidenceId == established)?.CurrentPageObservation?.ContainerId : null);
        var reason = parsed ? "target-observation-unproven" : "malformed-correction";
        AssociationProposal? proposal = null;
        if (parsed && parent is not null && original is not null && previousContainer is not null)
        {
            reason = "superseded-interpretation-mismatch";
            var replacement = correction!.ReplacementIdentity;
            if (established == correction.SupersededIdentityEvidenceId)
            {
                reason = "original-capture-correlation-mismatch";
                var capture = original.Capture;
                if (replacement.CaptureId == capture.CaptureId
                    && replacement.ObservationCycleId == capture.ObservationCycleId
                    && replacement.SessionCorrelation == capture.SessionCorrelation
                    && replacement.CaptureTime == capture.CaptureTime && record.Provenance.CaptureTime == capture.CaptureTime
                    && PageObservationEvidence.Matches(replacement, original, record))
                {
                    reason = "native-context-support-unproven";
                    bool ValidSupport(string id)
                    {
                        if (id == correction.TargetCaptureEvidenceId || id == established
                            || !_worldEvidence.TryGetValue(id, out var support) || !parent.EvidenceBasis.Contains(id)
                            || !record.DerivedFromEvidenceIds.Contains(id) && !record.TransitiveEvidenceBasis.Contains(id)
                            || support.Provenance.Producer != ProducerTrust.XmlProducer
                            || !support.Provenance.TransformationLineage.Contains(PageInterpretationCodec.NativeContextProfile)
                            || support.Provenance.Hierarchy is not { Field: "resource_id" } h
                            || h.CaptureId != capture.CaptureId || h.ObservationCycleId != capture.ObservationCycleId
                            || h.SessionCorrelation != capture.SessionCorrelation || h.CaptureTimestamp != capture.CaptureTime
                            || support.Claim.Subject != $"ui.node.{h.CaptureId}#{h.NodeLocalIndex}.resource_id"
                            || support.Provenance.CaptureTime != capture.CaptureTime || h.Space != capture.Space
                            || parent.Conflicts.Any(c => c.Subject == support.Claim.Subject)
                            || string.IsNullOrWhiteSpace(capture.DeviceId) || h.DeviceId != capture.DeviceId
                            || support.Claim.Value != replacement.Signature) return false;
                        // A repeated dependency of the original interpretation
                        // is not a new discriminator, regardless of model score.
                        return _worldEvidence.TryGetValue(established!, out var old)
                            && !old.DerivedFromEvidenceIds.Contains(id) && !old.TransitiveEvidenceBasis.Contains(id);
                    }
                    if (correction.SupportingEvidenceIds.All(ValidSupport) && _associationStrategy is not null)
                    {
                        proposal = _associationStrategy.Propose(new(parent, record, null));
                        reason = proposal.Kind == AssociationDispositionKind.Matched
                            ? "association-gate-pending" : "known-native-context-candidate-unproven";
                    }
                }
            }
        }

        var number = (parent?.RevisionNumber ?? 0) + 1;
        var revisionId = $"rev-{number}";
        var state = (parent?.WorldState ?? ImmutableDictionary<string, WorldClaim>.Empty).ToImmutableDictionary(StringComparer.Ordinal);
        var containers = (parent?.Containers ?? []).ToImmutableList();
        var relations = (parent?.Relations ?? []).ToImmutableList();
        var occurrences = parent?.Occurrences;
        var page = parent?.CurrentPageObservation;
        var need = parent?.ObservationNeed;
        var conflicts = (parent?.Conflicts ?? []).ToImmutableArray();
        var viewports = (parent?.ViewportTransitions ?? []).ToImmutableArray();
        var graph = (parent?.WorldGraph ?? []).ToImmutableArray();
        var applied = false;
        var revokedBasis = new HashSet<string>(StringComparer.Ordinal);
        string? target = null;
        if (proposal?.Kind == AssociationDispositionKind.Matched && parent is not null)
        {
            long scanned = 0, copied = 0;
            var candidateRelations = relations;
            var decision = Associate(record, null, parent, ref containers, ref candidateRelations,
                ref scanned, ref copied, out target, proposed: proposal);
            _associationLog.Add(decision);
            applied = decision.EffectiveKind == AssociationDispositionKind.Matched && target is not null;
            reason = applied ? "native-context-correction-accepted" : decision.Reason;
            if (applied)
            {
                var originalId = correction!.TargetCaptureEvidenceId;
                var oldIdentity = correction.SupersededIdentityEvidenceId;
                bool Depends(IReadOnlyList<string> basis) => basis.Contains(oldIdentity) || basis.Contains(originalId);
                _revokedInterpretations.Add(oldIdentity);
                revokedBasis.Add(oldIdentity);
                var oldSignature = SignatureSubjectPrefix + previousContainer;
                if (state.TryGetValue(oldSignature, out var signatureClaim) && signatureClaim.EvidenceId == oldIdentity)
                {
                    // Keep the historical entity, but its withdrawn sole
                    // signature cannot establish a future native match.
                    state = previousContainer == target
                        ? state.SetItem(oldSignature, new(correction.ReplacementIdentity.Signature, record.EvidenceId))
                        : state.Remove(oldSignature);
                    if (previousContainer != target) graph = graph.Where(s => s != oldSignature).ToImmutableArray();
                }
                relations = relations.Where(r => !r.EvidenceBasis.Contains(oldIdentity)
                    && !(r.EvidenceBasis.Contains(originalId)
                        && (r.SourceContainerId == previousContainer || r.TargetContainerId == previousContainer))).ToImmutableList();
                viewports = viewports.Select(t => Depends(t.SourceEvidenceIds) || Depends(t.TargetEvidenceIds)
                    ? t with { AlignmentStatus = "InterpretationCorrectedUnresolved", OffsetX = null, OffsetY = null,
                        Correspondences = ImmutableArray<ViewportCorrespondence>.Empty } : t).ToImmutableArray();
                // Misattributed meaning is not silently moved to the new page.
                // Its original observation remains in the old immutable revision.
                foreach (var pair in state.Where(p => p.Key.EndsWith(".semantic.page", StringComparison.Ordinal)).ToArray())
                {
                    try
                    {
                        var meaning = JsonSerializer.Deserialize<UniClaw.Kernel.Capability.SemanticPageClaim>(pair.Value.Value, SemanticJson);
                        if (meaning?.CaptureId == original!.Capture.CaptureId
                            && meaning.ObservationCycleId == original.Capture.ObservationCycleId
                            && meaning.SessionCorrelation == original.Capture.SessionCorrelation)
                        {
                            revokedBasis.Add(pair.Value.EvidenceId);
                            _revokedInterpretations.Add(pair.Value.EvidenceId);
                            state = state.Remove(pair.Key); graph = graph.Where(s => s != pair.Key).ToImmutableArray();
                        }
                    }
                    catch (JsonException) { /* unrelated scoped claims retain their existing interpretation */ }
                }
                if (page?.EvidenceId == originalId && (page.IdentityEvidenceId == oldIdentity || page.IdentityEvidenceId is null))
                {
                    page = page with { ContainerId = target, IdentityEvidenceId = record.EvidenceId };
                    state = state.SetItem(CurrentContainerSubject, new(target!, record.EvidenceId));
                    occurrences = (occurrences ?? []).Select((o, i) => o.EvidenceBasis.Contains(originalId)
                        ? o with { OccurrenceId = MintOccurrenceIdentity(record.EvidenceId, i), OwningContainerId = target }
                        : o with { OwningContainerId = (string?)null }).ToImmutableArray();
                    need = null;
                    conflicts = conflicts.Where(c => c.Subject != PageObservationCodec.IdentitySubject + ":" + page.Capture.CaptureId).ToImmutableArray();
                }
            }
        }
        var basis = (parent?.EvidenceBasis ?? ImmutableHashSet<string>.Empty).ToImmutableHashSet(StringComparer.Ordinal).Add(record.EvidenceId);
        var revision = new WorldBeliefRevision(revisionId, parent?.RevisionId, number, state, graph, basis,
            new FreshnessBasis(parent?.FreshnessBasis.AsOf > record.Provenance.CaptureTime
                ? parent.FreshnessBasis.AsOf : record.Provenance.CaptureTime), new(conflicts.Length), conflicts,
            containers, relations, occurrences, parent?.LogicalItems)
        { CurrentPageObservation = page, ObservationNeed = need, ViewportTransitions = viewports };
        _worldEvidence[record.EvidenceId] = record;
        revision = PublishRevision(revision);
        _pageCorrections.Add(new(revisionId, record.EvidenceId, correction?.TargetCaptureEvidenceId ?? "unknown",
            correction?.SupersededIdentityEvidenceId ?? "unknown", previousContainer, applied ? target : null,
            correction?.ReplacementIdentity, correction?.SupportingEvidenceIds.ToImmutableArray() ?? ImmutableArray<string>.Empty,
            correction?.ProcessingSourceRef ?? "not_collected", applied, reason)
        { RevokedInterpretationEvidenceIds = revokedBasis.Order(StringComparer.Ordinal).ToImmutableArray() });
        return revision;
    }
}
