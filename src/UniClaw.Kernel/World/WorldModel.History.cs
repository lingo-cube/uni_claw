using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    private bool IdentitySourceScopeMatches(UniClaw.Kernel.Evidence.EvidenceRecord record, string? containerId)
    {
        PageCaptureObservation? source = null;
        if (record.Claim.Subject == PageObservationCodec.IdentitySubject)
            source = Current?.CurrentPageObservation?.Capture;
        else if (record.Claim.Subject == PageInterpretationCodec.Subject
            && PageInterpretationCodec.TryRead(record.Claim.Value, out var correction))
            source = _revisionHistory.LastOrDefault(r =>
                r.CurrentPageObservation?.EvidenceId == correction!.TargetCaptureEvidenceId)?.CurrentPageObservation?.Capture;
        else return true; // Legacy screen identity uses its existing signature contract.
        return source is not null && PageSourceScopeKnown(containerId, source.DeviceId, source.SessionCorrelation);
    }

    private bool PageSourceScopeKnown(string? containerId, string? deviceId, string session) =>
        !string.IsNullOrWhiteSpace(deviceId) && _revisionHistory.Any(r => r.CurrentPageObservation is { } page
            && page.ContainerId == containerId && page.Capture.DeviceId == deviceId
            && page.Capture.SessionCorrelation == session);

    /// <summary>Derives known fragments from one fixed World snapshot. No renderer-side history fusion.</summary>
    public WorldPageView DerivePageView(string containerId, string? sourceRevisionId = null)
    {
        var selected = sourceRevisionId is null ? Current
            : _revisionHistory.FirstOrDefault(r => r.RevisionId == sourceRevisionId);
        if (selected is null || selected.Containers?.Any(c => c.Identity.ContainerId == containerId) != true)
            throw new InvalidOperationException("page view requires a known revision and container");
        var history = _revisionHistory.TakeWhile(r => r.RevisionNumber <= selected.RevisionNumber).ToArray();
        var revisionIds = history.Select(r => r.RevisionId).ToHashSet(StringComparer.Ordinal);
        var corrections = _pageCorrections.Where(c => revisionIds.Contains(c.SourceRevisionId)).ToArray();
        var observations = new List<WorldPageObservation>();
        var references = new List<WorldSemanticReference>();
        var unknown = new HashSet<string>(StringComparer.Ordinal) { "whole-page-coverage-unproven" };
        foreach (var group in history.Where(r => r.CurrentPageObservation is not null)
                     .GroupBy(r => r.CurrentPageObservation!.EvidenceId, StringComparer.Ordinal))
        {
            var reinterpreted = corrections.LastOrDefault(c => c.Applied && c.TargetCaptureEvidenceId == group.Key);
            if (reinterpreted is not null ? reinterpreted.CorrectedContainerId != containerId
                : !group.Any(r => r.CurrentPageObservation!.ContainerId == containerId)) continue;
            var last = group.Last();
            var page = last.CurrentPageObservation!;
            var capture = page.Capture with { Items = page.Capture.Items.ToImmutableArray() };
            var owner = reinterpreted?.CorrectedContainerId ?? page.ContainerId;
            var selectedIdentity = reinterpreted?.CorrectionEvidenceId ?? page.IdentityEvidenceId;
            var identityId = owner is not null && selectedIdentity is { } established
                && (reinterpreted is not null ? selected.EvidenceBasis : last.EvidenceBasis).Contains(established) ? established : null;
            // Later unrelated evidence may replace the occurrence landscape.
            // Only the exact accepted capture basis can contribute this fragment.
            var landscape = group.LastOrDefault(r => r.Occurrences?.Any(o => o.EvidenceBasis.Contains(group.Key)) == true);
            var items = (landscape?.Occurrences ?? []).Where(o => o.EvidenceBasis.Contains(group.Key))
                .Select(o => new OccurrenceFact(o.OccurrenceId, owner, o.Role, o.SemanticDescriptor,
                    o.State, o.Locator, o.Native)).ToImmutableArray();
            var navigation = (selected.Relations ?? []).Where(r => r.Kind == ContainerRelationKind.NavigatesTo
                    && r.TargetContainerId == owner && r.TargetRevisionId is not null)
                .Select(r => (Relation: r, Target: history.FirstOrDefault(h => h.RevisionId == r.TargetRevisionId)))
                .Where(pair => pair.Target?.CurrentPageObservation is { IdentityEvidenceId: not null } arrived
                    && pair.Target.RevisionNumber <= last.RevisionNumber
                    && arrived.EvidenceId == group.Key && pair.Relation.EvidenceBasis.Contains(arrived.IdentityEvidenceId))
                .OrderBy(pair => pair.Target!.RevisionNumber).LastOrDefault();
            var visit = navigation.Relation is null ? null
                : $"navigation:{navigation.Relation.TargetRevisionId}:{navigation.Relation.VerificationSourceRef}";
            if (visit is null) unknown.Add("visit-occurrence-unproven");
            if (owner is null) unknown.Add("observation-attribution-conflicted:" + group.Key);
            if (identityId is null) unknown.Add("identity-source-unavailable:" + group.Key);
            if (capture.Coverage is null) unknown.Add("capture-coverage-not-collected:" + group.Key);
            observations.Add(new(last.RevisionId, group.Key, identityId, capture, owner, items,
                selected.CurrentPageObservation?.EvidenceId == group.Key, visit));

            foreach (var revision in group)
            {
                if (!revision.WorldState.TryGetValue(containerId + ".semantic.page", out var claim)) continue;
                SemanticPageClaim? meaning;
                try { meaning = JsonSerializer.Deserialize<SemanticPageClaim>(claim.Value, SemanticJson); }
                catch (JsonException) { continue; }
                if (meaning is null || meaning.CaptureId != capture.CaptureId
                    || meaning.ObservationCycleId != capture.ObservationCycleId
                    || meaning.SessionCorrelation != capture.SessionCorrelation
                    || !revision.EvidenceBasis.Contains(claim.EvidenceId)
                    || references.Any(r => r.EvidenceId == claim.EvidenceId)) continue;
                var conflicted = owner is null || selected.Conflicts.Any(c =>
                    c.EstablishedEvidenceId == claim.EvidenceId || c.ChallengingEvidenceId == claim.EvidenceId);
                references.Add(new(claim.EvidenceId, group.Key, meaning,
                    selected.CurrentPageObservation?.EvidenceId != group.Key, conflicted)
                { VerificationEvidenceId = _semanticVerifications.LastOrDefault(v => v.Applied
                    && v.Observation?.SemanticEvidenceId == claim.EvidenceId && selected.EvidenceBasis.Contains(v.EvidenceId)
                    && !corrections.Any(c => c.Applied && c.SupersededIdentityEvidenceId == v.Observation.SourceIdentityEvidenceId))?.EvidenceId });
            }
        }
        if (observations.Count == 0) unknown.Add("capture-history-not-collected");
        var transitions = (selected.ViewportTransitions ?? []).Where(t => t.ContainerId == containerId)
            .Select(t => t with { SourceEvidenceIds = t.SourceEvidenceIds.ToImmutableArray(),
                TargetEvidenceIds = t.TargetEvidenceIds.ToImmutableArray(),
                Correspondences = t.Correspondences.ToImmutableArray() }).ToImmutableArray();
        if (transitions.Any(t => t.OffsetX is null || t.OffsetY is null)) unknown.Add("viewport-registration-incomplete");
        var accumulated = DeriveAccumulatedContent(selected, containerId);
        // Pure presentation content can share a digest while every view still
        // declares its own immutable source revision. This is never currency.
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            containerId,
            Fragments = observations.Select(o => new { o.ContainerId, o.IsCurrentCapture,
                Items = o.Occurrences.Select(i => new { i.Role, i.SemanticDescriptor, i.State, i.Locator }) }),
            References = references.Select(r => new { r.Meaning.PageName, r.Meaning.PagePurpose, r.Conflicted }),
            Unknown = unknown.Order(StringComparer.Ordinal),
            Accumulated = accumulated.Items.Select(i => new { i.ContainerId, i.Role, i.SemanticDescriptor,
                i.CurrentState, i.ObservedState, i.CurrentPresence, i.Bounds }),
            AccumulationUnknown = accumulated.Unknown,
            Context = (selected.Relations ?? []).Where(r => r.Kind != ContainerRelationKind.NavigatesTo
                && (r.SourceContainerId == containerId || r.TargetContainerId == containerId))
                .Select(r => new { r.Kind, r.SourceContainerId, r.TargetContainerId, r.Scope }),
        }))).ToLowerInvariant();
        return new(selected.RevisionId, containerId, observations.ToImmutableArray(), references.ToImmutableArray(),
            transitions, unknown.Order(StringComparer.Ordinal).ToImmutableArray(), digest)
        { AccumulatedContent = accumulated,
          CurrentSemantics = selected.SemanticKnowledge.Where(k => k.ContainerId == containerId).ToImmutableArray(),
          Corrections = corrections.Where(c => c.PreviousContainerId == containerId || c.CorrectedContainerId == containerId)
            .Select(c => c with { SupportingEvidenceIds = c.SupportingEvidenceIds.ToImmutableArray() }).ToImmutableArray(),
          ContextRelations = (selected.Relations ?? []).Where(r => r.Kind != ContainerRelationKind.NavigatesTo
              && (r.SourceContainerId == containerId || r.TargetContainerId == containerId))
              .Select(r => r with { EvidenceBasis = r.EvidenceBasis.ToImmutableArray() }).ToImmutableArray() };
    }
}
