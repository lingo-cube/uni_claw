using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    private readonly IReadOnlyList<NativeContextSignatureProfile> _nativeSemanticProfiles;
    private readonly List<SemanticVerificationDecision> _semanticVerifications = [];
    private readonly HashSet<string> _revokedInterpretations = new(StringComparer.Ordinal);
    public IReadOnlyList<SemanticVerificationDecision> SemanticVerificationLog => _semanticVerifications.AsReadOnly();

    private WorldBeliefRevision ReconcileSemanticVerification(EvidenceRecord record, WorldBeliefRevision? parent)
    {
        var parsed = SemanticVerificationCodec.TryRead(record.Claim.Value, out var observation);
        var applied = parent is not null && parsed && record.Provenance.Producer == "native-context-verifier"
            && record.Provenance.TransformationLineage.Contains("semantic-native-profile-verification:v1")
            && VerificationSources(observation!, parent, out _, out _, out var capture, out _)
            && record.Provenance.CaptureTime == capture!.Capture.CaptureTime
            && new[] { observation!.SemanticEvidenceId, observation.SourceCaptureEvidenceId,
                observation.SourceIdentityEvidenceId, observation.NativeContextEvidenceId }
                .All(id => record.DerivedFromEvidenceIds.Contains(id) || record.TransitiveEvidenceBasis.Contains(id));
        var number = (parent?.RevisionNumber ?? 0) + 1;
        var revision = parent is null
            ? new WorldBeliefRevision($"rev-{number}", null, number, ImmutableDictionary<string, WorldClaim>.Empty,
                ImmutableArray<string>.Empty, ImmutableHashSet<string>.Empty.Add(record.EvidenceId),
                new(record.Provenance.CaptureTime), new(0), ImmutableArray<Conflict>.Empty)
            : parent with { RevisionId = $"rev-{number}", ParentRevisionId = parent.RevisionId, RevisionNumber = number,
                EvidenceBasis = parent.EvidenceBasis.ToImmutableHashSet(StringComparer.Ordinal).Add(record.EvidenceId) };
        _semanticVerifications.Add(new(revision.RevisionId, record.EvidenceId, observation, applied,
            applied ? "independent-native-profile-match" : "independent-semantic-validation-unproven"));
        _worldEvidence[record.EvidenceId] = record;
        return PublishRevision(revision);
    }

    private bool VerificationSources(SemanticVerificationObservation v, WorldBeliefRevision revision,
        out NativeContextSignatureProfile? profile, out SemanticPageClaim? meaning,
        out PageObservationState? sourcePage, out EvidenceRecord? anchor)
    {
        profile = _nativeSemanticProfiles.SingleOrDefault(p => p.Fingerprint == v.ProfileFingerprint);
        meaning = null; sourcePage = null; anchor = null;
        if (profile is null || _revokedInterpretations.Contains(v.SourceIdentityEvidenceId)
            || new[] { v.SemanticEvidenceId, v.SourceCaptureEvidenceId, v.SourceIdentityEvidenceId, v.NativeContextEvidenceId }
                .Any(id => !revision.EvidenceBasis.Contains(id))
            || !_worldEvidence.TryGetValue(v.SemanticEvidenceId, out var semantic)
            || !_worldEvidence.TryGetValue(v.SourceIdentityEvidenceId, out var identity)
            || !_worldEvidence.TryGetValue(v.NativeContextEvidenceId, out anchor)
            || semantic.Claim.Subject != SemanticPageSubject) return false;
        sourcePage = _revisionHistory.LastOrDefault(r => r.RevisionNumber <= revision.RevisionNumber
            && r.CurrentPageObservation is { } page && page.EvidenceId == v.SourceCaptureEvidenceId
            && page.IdentityEvidenceId == v.SourceIdentityEvidenceId && page.ContainerId is not null)?.CurrentPageObservation;
        if (sourcePage is null || !TryIdentitySignature(identity, out var signature)
            || signature != anchor.Claim.Value || !NativeAnchorMatches(anchor, sourcePage, profile, revision)
            || !profile.StablePageMeanings.TryGetValue(anchor.Claim.Value, out var expected)) return false;
        try { meaning = JsonSerializer.Deserialize<SemanticPageClaim>(semantic.Claim.Value, SemanticJson); }
        catch (JsonException) { return false; }
        return meaning is not null && meaning.CaptureId == sourcePage.Capture.CaptureId
            && meaning.ObservationCycleId == sourcePage.Capture.ObservationCycleId
            && meaning.SessionCorrelation == sourcePage.Capture.SessionCorrelation
            && semantic.Provenance.CaptureTime == sourcePage.Capture.CaptureTime
            && (semantic.DerivedFromEvidenceIds.Contains(v.SourceCaptureEvidenceId)
                || semantic.TransitiveEvidenceBasis.Contains(v.SourceCaptureEvidenceId))
            && meaning.PageKey == expected.Key && meaning.PageName == expected.Name && meaning.PagePurpose == expected.Purpose
            && !revision.Conflicts.Any(c => c.EstablishedEvidenceId == semantic.EvidenceId
                || c.ChallengingEvidenceId == semantic.EvidenceId || c.EstablishedEvidenceId == identity.EvidenceId
                || c.ChallengingEvidenceId == identity.EvidenceId);
    }

    private bool NativeAnchorMatches(EvidenceRecord anchor, PageObservationState page,
        NativeContextSignatureProfile profile, WorldBeliefRevision revision)
    {
        if (!NativeContextSource(anchor) || anchor.Provenance.Hierarchy is not { } h
            || h.CaptureId != page.Capture.CaptureId || h.CaptureTimestamp != page.Capture.CaptureTime
            || h.ObservationCycleId != page.Capture.ObservationCycleId || h.SessionCorrelation != page.Capture.SessionCorrelation
            || h.DeviceId != page.Capture.DeviceId || h.DeviceId != profile.DeviceId || h.Space != page.Capture.Space
            || h.AcquirerKind != profile.AcquirerKind || h.AcquirerVersion != profile.AcquirerVersion
            || ProfileToken(anchor) != "native-profile-sha256:" + profile.Fingerprint
            || !anchor.Provenance.TransformationLineage.Contains("native-profile-ref:" + profile.ProfileRef)
            || !profile.ContextResourceIds.Contains(anchor.Claim.Value)
            || revision.Conflicts.Any(c => c.Subject == anchor.Claim.Subject)) return false;
        // Package is checked against the actual typed sibling observation,
        // rather than trusting an arbitrary marker in the verification payload.
        return _worldEvidence.Values.Any(package => revision.EvidenceBasis.Contains(package.EvidenceId)
            && package.Claim.Subject == $"ui.node.{h.CaptureId}#{h.NodeLocalIndex}.package"
            && package.Claim.Value == profile.PackageName && package.Provenance.Producer == anchor.Provenance.Producer
            && package.Provenance.Hierarchy is { Field: "package" } p
            && p.CaptureId == h.CaptureId && p.NodeLocalIndex == h.NodeLocalIndex && p.DeviceId == h.DeviceId
            && p.SessionCorrelation == h.SessionCorrelation && p.ObservationCycleId == h.ObservationCycleId
            && p.CaptureTimestamp == h.CaptureTimestamp && p.Space == h.Space
            && p.AcquirerKind == h.AcquirerKind && p.AcquirerVersion == h.AcquirerVersion
            && !revision.Conflicts.Any(c => c.Subject == package.Claim.Subject));
    }

    private static bool TryIdentitySignature(EvidenceRecord record, out string? signature)
    {
        signature = null;
        if (record.Claim.Subject == PageObservationCodec.IdentitySubject
            && PageObservationCodec.TryReadIdentity(record.Claim.Value, out var identity))
            signature = identity!.Signature;
        else if (record.Claim.Subject == PageInterpretationCodec.Subject
            && PageInterpretationCodec.TryRead(record.Claim.Value, out var correction))
            signature = correction!.ReplacementIdentity.Signature;
        return signature is not null;
    }

    private WorldBeliefRevision RefreshSemanticKnowledge(WorldBeliefRevision revision)
    {
        if (_nativeSemanticProfiles.Count == 0) return revision;
        var result = new List<WorldSemanticKnowledge>();
        var page = revision.CurrentPageObservation;
        if (page is { ContainerId: not null, IdentityEvidenceId: not null } && revision.ObservationNeed is null
            && revision.EvidenceBasis.Contains(page.IdentityEvidenceId)
            && !_revokedInterpretations.Contains(page.IdentityEvidenceId)
            && _worldEvidence.TryGetValue(page.IdentityEvidenceId, out var currentIdentity)
            && TryIdentitySignature(currentIdentity, out var signature))
        {
            foreach (var validation in _semanticVerifications.Where(v => v.Applied && v.Observation is not null
                         && revision.EvidenceBasis.Contains(v.EvidenceId)))
            {
                var v = validation.Observation!;
                if (!VerificationSources(v, revision, out var profile, out var meaning, out var source, out var oldAnchor)
                    || source!.ContainerId != page.ContainerId || source.Capture.SessionCorrelation != page.Capture.SessionCorrelation) continue;
                var anchors = _worldEvidence.Values.Where(r => revision.EvidenceBasis.Contains(r.EvidenceId)
                    && r.Claim.Value == oldAnchor!.Claim.Value && r.Claim.Value == signature
                    && NativeAnchorMatches(r, page, profile!, revision)).ToArray();
                if (anchors.Length != 1) continue;
                result.Add(new(page.ContainerId, page.EvidenceId, page.Capture.CaptureId, page.Capture.ObservationCycleId,
                    page.Capture.SessionCorrelation, meaning!.PageKey, meaning.PageName, meaning.PagePurpose,
                    v.SemanticEvidenceId, validation.EvidenceId,
                    new[] { page.EvidenceId, page.IdentityEvidenceId, anchors[0].EvidenceId }.ToImmutableArray(),
                    validation.SourceRevisionId, revision.RevisionId, page.EvidenceId != v.SourceCaptureEvidenceId));
            }
        }
        // Conflicting independently validated meanings are not silently voted on.
        var meanings = result.Select(k => (k.PageKey, k.PageName, k.PagePurpose)).Distinct().Count();
        return revision with { SemanticKnowledge = meanings > 1 ? ImmutableArray<WorldSemanticKnowledge>.Empty
            : result.TakeLast(1).ToImmutableArray() };
    }
}
