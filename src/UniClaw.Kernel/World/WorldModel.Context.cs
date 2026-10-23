using System.Collections.Immutable;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    private readonly List<ContainerContextDecision> _contextDecisions = [];
    public IReadOnlyList<ContainerContextDecision> ContextDecisionLog => _contextDecisions.AsReadOnly();

    private WorldBeliefRevision ReconcileContainerContext(EvidenceRecord record, WorldBeliefRevision? parent)
    {
        var reason = "context-source-unproven";
        string? parentContainer = null, childContainer = null;
        var relations = (parent?.Relations ?? []).ToImmutableArray();
        var applied = false;
        if (parent is not null && ContainerContextCodec.TryRead(record.Claim.Value, out var observation)
            && _worldEvidence.TryGetValue(observation!.ParentContextEvidenceId, out var p)
            && _worldEvidence.TryGetValue(observation.ChildContextEvidenceId, out var c)
            && NativeContextSource(p) && NativeContextSource(c)
            && parent.EvidenceBasis.Contains(p.EvidenceId) && parent.EvidenceBasis.Contains(c.EvidenceId)
            && record.DerivedFromEvidenceIds.Contains(p.EvidenceId) && record.DerivedFromEvidenceIds.Contains(c.EvidenceId))
        {
            var ph = p.Provenance.Hierarchy!; var ch = c.Provenance.Hierarchy!;
            var source = _revisionHistory.LastOrDefault(r => r.CurrentPageObservation is { } page
                && page.Capture.CaptureId == ph.CaptureId && page.Capture.ObservationCycleId == ph.ObservationCycleId
                && page.Capture.SessionCorrelation == ph.SessionCorrelation && page.Capture.CaptureTime == ph.CaptureTimestamp
                && page.Capture.DeviceId == ph.DeviceId && page.Capture.Space == ph.Space)?.CurrentPageObservation;
            reason = "context-correlation-or-parentage-unproven";
            if (source is not null && ph.CaptureId == ch.CaptureId && ph.DeviceId == ch.DeviceId
                && ph.SessionCorrelation == ch.SessionCorrelation && ph.ObservationCycleId == ch.ObservationCycleId
                && ph.CaptureTimestamp == ch.CaptureTimestamp && ph.Space == ch.Space
                && ch.ParentLocalIndex == ph.NodeLocalIndex && ph.NodeLocalIndex != ch.NodeLocalIndex
                && record.Provenance.CaptureTime == ph.CaptureTimestamp
                && ProfileToken(p) is { } profile && profile == ProfileToken(c)
                && !parent.Conflicts.Any(conflict => conflict.Subject == p.Claim.Subject || conflict.Subject == c.Claim.Subject))
            {
                var parents = (parent.Containers ?? []).Where(container =>
                    parent.WorldState.GetValueOrDefault(SignatureSubjectPrefix + container.Identity.ContainerId)?.Value == p.Claim.Value
                    && PageSourceScopeKnown(container.Identity.ContainerId, ph.DeviceId, ph.SessionCorrelation)).ToArray();
                var children = (parent.Containers ?? []).Where(container =>
                    parent.WorldState.GetValueOrDefault(SignatureSubjectPrefix + container.Identity.ContainerId)?.Value == c.Claim.Value
                    && PageSourceScopeKnown(container.Identity.ContainerId, ch.DeviceId, ch.SessionCorrelation)).ToArray();
                reason = "context-canonical-endpoints-unproven";
                if (parents.Length == 1 && children.Length == 1 && parents[0].Identity != children[0].Identity)
                {
                    parentContainer = parents[0].Identity.ContainerId; childContainer = children[0].Identity.ContainerId;
                    var parentIdentity = parent.WorldState[SignatureSubjectPrefix + parentContainer].EvidenceId;
                    var childIdentity = parent.WorldState[SignatureSubjectPrefix + childContainer].EvidenceId;
                    var support = new[] { p.EvidenceId, c.EvidenceId, parentIdentity, childIdentity, record.EvidenceId }
                        .Distinct(StringComparer.Ordinal).ToImmutableArray();
                    // Capture-scoped, observed structure. It does not establish
                    // every later visit or a timeless lifecycle/parent relation.
                    var relation = new ContainerRelation(ContainerRelationKind.Contains, parentContainer, childContainer,
                        support, "capture:" + ph.CaptureId, profile, parent.RevisionId, parent.RevisionId);
                    relations = relations.Add(relation);
                    applied = true;
                    reason = "native-direct-context-observed";
                }
            }
        }
        var number = (parent?.RevisionNumber ?? 0) + 1;
        var basis = (parent?.EvidenceBasis ?? ImmutableHashSet<string>.Empty).ToImmutableHashSet(StringComparer.Ordinal).Add(record.EvidenceId);
        var freshness = new FreshnessBasis(parent is not null && parent.FreshnessBasis.AsOf > record.Provenance.CaptureTime
            ? parent.FreshnessBasis.AsOf : record.Provenance.CaptureTime);
        var revision = parent is null
            ? new WorldBeliefRevision($"rev-{number}", null, number, ImmutableDictionary<string, WorldClaim>.Empty,
                ImmutableArray<string>.Empty, basis, freshness, new(0), ImmutableArray<Conflict>.Empty, Relations: relations)
            : parent with { RevisionId = $"rev-{number}", ParentRevisionId = parent.RevisionId, RevisionNumber = number,
                EvidenceBasis = basis, FreshnessBasis = freshness, Relations = relations };
        _worldEvidence[record.EvidenceId] = record;
        revision = PublishRevision(revision);
        _contextDecisions.Add(new(revision.RevisionId, record.EvidenceId, parentContainer, childContainer, applied, reason));
        return revision;
    }

    private static string? ProfileToken(EvidenceRecord record)
    {
        var values = record.Provenance.TransformationLineage.Where(v => v.StartsWith("native-profile-sha256:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private static bool NativeContextSource(EvidenceRecord record) => record.Provenance.Hierarchy is { Field: "resource_id" } h
        && record.Provenance.Producer == ProducerTrust.XmlProducer
        && record.Provenance.TransformationLineage.Contains(PageInterpretationCodec.NativeContextProfile)
        && record.Claim.Subject == $"ui.node.{h.CaptureId}#{h.NodeLocalIndex}.resource_id"
        && record.Provenance.CaptureTime == h.CaptureTimestamp;
}
