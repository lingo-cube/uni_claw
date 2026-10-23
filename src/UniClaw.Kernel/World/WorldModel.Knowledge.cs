using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Kernel.World;

public sealed partial class WorldModel
{
    /// <summary>Current, consumer-scoped missing support; no enumeration of unmodeled relationships.</summary>
    public WorldKnowledgeView DeriveKnowledgeGaps(string? requiredParentContextId = null, string? sourceRevisionId = null)
    {
        var revision = sourceRevisionId is null ? Current : _revisionHistory.FirstOrDefault(r => r.RevisionId == sourceRevisionId);
        if (sourceRevisionId is not null && revision is null) throw new InvalidOperationException("unknown source revision");
        if (requiredParentContextId is not null && revision?.Containers?.Any(c => c.Identity.ContainerId == requiredParentContextId) != true)
            throw new InvalidOperationException("context request requires an existing canonical parent");
        if (revision?.CurrentPageObservation is not { } page)
            return new(revision?.RevisionId, "not_collected", ImmutableArray<WorldKnowledgeGap>.Empty);
        var gaps = new List<WorldKnowledgeGap>();
        var scope = "capture:" + page.Capture.CaptureId + ":viewport";
        var cause = "capture:" + page.EvidenceId;
        void Add(WorldKnowledgeGapKind kind, string reason, bool conflict = false)
        {
            var objectRef = page.ContainerId ?? page.EvidenceId;
            var input = JsonSerializer.SerializeToUtf8Bytes(new { kind, CaptureEvidenceId = page.EvidenceId, scope, requiredParentContextId });
            var id = "world-gap:" + Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
            gaps.Add(new(id, kind, objectRef, scope, reason, revision.RevisionId,
                new[] { page.EvidenceId }.ToImmutableArray(), conflict, cause));
        }
        var identityConflict = revision.Conflicts.Any(c => c.Subject == PageObservationCodec.IdentitySubject + ":" + page.Capture.CaptureId);
        if (page.ContainerId is null) Add(WorldKnowledgeGapKind.Identity, "capture-identity-unproven", identityConflict);
        if (page.ContainerId is null || (revision.Occurrences ?? []).Any(o => o.OwningContainerId is null))
            Add(WorldKnowledgeGapKind.Attribution, "observed-content-owner-unproven", identityConflict);
        if (page.Capture.Coverage?.Completeness != CoverageCompleteness.CompleteWithinDeclaredSurface)
            Add(WorldKnowledgeGapKind.ObservationCoverage, page.Capture.Coverage?.Limitation ?? "capture-coverage-not-collected");
        if (requiredParentContextId is not null && !(revision.Relations ?? []).Any(r => r.Kind == ContainerRelationKind.Contains
            && r.SourceContainerId == requiredParentContextId && r.TargetContainerId == page.ContainerId
            && r.Scope == "capture:" + page.Capture.CaptureId && r.EvidenceBasis.All(revision.EvidenceBasis.Contains)))
            Add(WorldKnowledgeGapKind.RelationBasis, page.ContainerId is null
                ? "context-child-identity-unproven" : "requested-current-context-relation-unproven", identityConflict);
        var pageView = page.ContainerId is not null ? DerivePageView(page.ContainerId, revision.RevisionId) : null;
        return new(revision.RevisionId, "collected", gaps.ToImmutableArray())
        { SemanticReferences = pageView?.SemanticReferences ?? ImmutableArray<WorldSemanticReference>.Empty,
          CurrentSemantics = revision.SemanticKnowledge,
          Corrections = _pageCorrections.Where(c => revision.EvidenceBasis.Contains(c.CorrectionEvidenceId))
              .ToImmutableArray() };
    }
}
