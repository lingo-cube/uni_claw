using UniClaw.Kernel.Evidence;

namespace UniClaw.Kernel.Perception.Fusion;

public sealed class FusionEngine
{
    public TemporalAlignmentResult Align(IReadOnlyList<FusionSourceEvidence> sources, TimeSpan boundedWindow)
    {
        if (sources.Count == 0) return new(TemporalAlignmentDisposition.TemporalUnknown, "no sources", Array.Empty<string>());
        var captures = sources.Select(x => x.Capture).ToArray();
        var ids = captures.Select(x => x.CaptureId).ToArray();
        if (captures.Any(x => !x.IsValid) || captures.Any(x => string.IsNullOrWhiteSpace(x.ObservationCycleId)))
            return new(TemporalAlignmentDisposition.TemporalUnknown, "capture correlation or cycle missing", ids);
        if (captures.Select(x => x.SessionCorrelation).Distinct(StringComparer.Ordinal).Count() != 1
            || captures.Select(x => x.ObservationCycleId).Distinct(StringComparer.Ordinal).Count() != 1
            || captures.Any(x => x.KnownMutationMarker))
            return new(TemporalAlignmentDisposition.Unaligned, "different correlation/cycle or known mutation", ids);
        var spaces = captures.Where(x => x.Space is not null).Select(x => x.Space!).ToArray();
        if (spaces.Length > 1 && spaces.Skip(1).Any(space => !spaces[0].Matches(space)))
            return new(TemporalAlignmentDisposition.Unaligned, "coordinate spaces are incompatible", ids);
        if (boundedWindow < TimeSpan.Zero || captures.Max(x => x.CaptureTimestamp) - captures.Min(x => x.CaptureTimestamp) > boundedWindow)
            return new(TemporalAlignmentDisposition.Unaligned, "capture timestamps exceed bounded window", ids);
        var limited = sources.Any(x => x.Coverage != CoverageDisposition.CompleteWithinDeclaredSurface);
        return new(limited ? TemporalAlignmentDisposition.AlignedWithCoverageLimit : TemporalAlignmentDisposition.Aligned,
            limited ? "eligible with declared coverage limitation" : "same correlation/cycle within bounded window", ids);
    }

    public DerivedObservationProposal Fuse(FusionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sources = request.Sources ?? throw new ArgumentNullException(nameof(request.Sources));
        var alignment = Align(sources, request.AlignmentWindow);
        var malformedSource = sources.Any(source => !source.IsValid);
        var graph = request.EvidenceGraph ?? sources.ToDictionary(x => x.EvidenceId,
            x => new EvidenceNode(x.EvidenceId, Array.Empty<string>(), IsSourceEvidence: true), StringComparer.Ordinal);
        var parentIds = sources.Select(x => x.EvidenceId).Distinct(StringComparer.Ordinal).ToArray();
        var lineage = LineageValidator.Validate(graph, parentIds);
        var coverage = sources.Count == 0 ? CoverageDisposition.SourceUnavailable
            : sources.Any(x => x.Coverage == CoverageDisposition.Unknown) ? CoverageDisposition.Unknown
            : sources.Any(x => x.Coverage is CoverageDisposition.Partial or CoverageDisposition.SourceUnavailable) ? CoverageDisposition.Partial
            : CoverageDisposition.CompleteWithinDeclaredSurface;
        var limitations = new List<string>();
        if (malformedSource) limitations.Add("one or more source evidence records are malformed");
        if (coverage != CoverageDisposition.CompleteWithinDeclaredSurface) limitations.Add("declared coverage is incomplete");
        if (!lineage.IsValid) limitations.Add(lineage.Error!);
        var authority = ResolveAuthority(request.Field, sources, request.Association, alignment);
        var admissible = !malformedSource
            && alignment.EligibleForJointFusion
            && lineage.IsValid
            && request.Association.Disposition == AssociationDisposition.Unique
            && authority.Disposition == FusionDisposition.Supported;
        var disposition = sources.Count == 0 ? FusionDisposition.Unknown
            : malformedSource || !lineage.IsValid ? FusionDisposition.Malformed
            : !alignment.EligibleForJointFusion ? FusionDisposition.Unaligned : authority.Disposition;
        var claimValue = authority.AuthoritySource is { } authoritySource
            ? sources.FirstOrDefault(x => x.SourceKind == authoritySource && x.ValueObserved)?.Value
            : sources.FirstOrDefault(x => x.ValueObserved)?.Value;
        return new DerivedObservationProposal(
            new ObservationClaim(request.Subject, claimValue ?? string.Empty), disposition, alignment,
            request.Association.Disposition, coverage, parentIds, parentIds, lineage.TransitiveEvidenceBasis,
            request.FusionRule, request.FusionRuleVersion, authority.AuthoritySource, authority.Conflict, limitations,
            admissible)
        {
            CaptureTimestamp = sources.Count == 0 ? default : sources.Max(x => x.Capture.CaptureTimestamp),
        };
    }

    private static FieldAuthorityDecision ResolveAuthority(string field, IReadOnlyList<FusionSourceEvidence> sources,
        AssociationCandidate association, TemporalAlignmentResult alignment)
    {
        if (sources.Count == 0) return new(FusionDisposition.Unsupported, null, ConflictDisposition.None, "no source evidence");
        if (!alignment.EligibleForJointFusion) return new(FusionDisposition.Unaligned, null, ConflictDisposition.None, alignment.Reason);
        if (association.Disposition is AssociationDisposition.Ambiguous or AssociationDisposition.Unassociated)
            return new(FusionDisposition.Unknown, null, ConflictDisposition.Unresolved, "occurrence association is not unique");
        var hierarchyAuthorityField = field is "checked" or "enabled" or "selected" or "focused"
            or "text" or "role" or "resource-id" or "visibility";
        var visualAuthorityField = field.StartsWith("rendered", StringComparison.OrdinalIgnoreCase)
            || field.StartsWith("icon", StringComparison.OrdinalIgnoreCase)
            || field.StartsWith("appearance", StringComparison.OrdinalIgnoreCase);
        var hierarchyCandidates = hierarchyAuthorityField
            ? sources.Where(x => x.SourceKind == FusionSourceKind.Hierarchy).ToArray()
            : Array.Empty<FusionSourceEvidence>();
        var visualCandidates = visualAuthorityField
            ? sources.Where(x => x.SourceKind is FusionSourceKind.Visual or FusionSourceKind.Ocr).ToArray()
            : Array.Empty<FusionSourceEvidence>();
        var hierarchy = hierarchyCandidates.Where(x => x.CapabilityDeclared && x.ValueObserved).ToArray();
        var visual = visualCandidates.Where(x => x.CapabilityDeclared && x.ValueObserved).ToArray();
        var values = sources.Where(x => x.ValueObserved).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
        if (hierarchyAuthorityField && hierarchyCandidates.Length == 0)
            return new(FusionDisposition.Unsupported, null, ConflictDisposition.None,
                "semantic field has no hierarchy authority source");
        if (visualAuthorityField && visualCandidates.Length == 0)
            return new(FusionDisposition.Unsupported, null, ConflictDisposition.None,
                "rendered field has no visual authority source");
        if (hierarchy.Length > 0)
        {
            var same = hierarchy.Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() == 1;
            var conflict = values.Length > 1 ? ConflictDisposition.OverruledSource : ConflictDisposition.None;
            return new(same ? FusionDisposition.Supported : FusionDisposition.Conflicted, FusionSourceKind.Hierarchy, conflict,
                same ? $"hierarchy authority for {field}" : "hierarchy authority sources disagree");
        }
        if (visual.Length > 0)
        {
            var same = visual.Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() == 1;
            var conflict = values.Length > 1 ? ConflictDisposition.OverruledSource : ConflictDisposition.None;
            return new(same ? FusionDisposition.Supported : FusionDisposition.Conflicted,
                FusionSourceKind.Visual, conflict,
                same ? $"visual authority for {field}" : "visual authority sources disagree");
        }
        if (hierarchyCandidates.Length > 0 && hierarchyCandidates.All(x => !x.CapabilityDeclared))
            return new(FusionDisposition.Unsupported, null, ConflictDisposition.None, "hierarchy capability not declared");
        if (visualCandidates.Length > 0 && visualCandidates.All(x => !x.CapabilityDeclared))
            return new(FusionDisposition.Unsupported, null, ConflictDisposition.None, "visual capability not declared");
        if (hierarchyCandidates.Length > 0 && hierarchyCandidates.All(x => !x.ValueObserved))
            return new(FusionDisposition.Unknown, FusionSourceKind.Hierarchy, ConflictDisposition.Unresolved, "hierarchy value was not observed");
        if (visualCandidates.Length > 0 && visualCandidates.All(x => !x.ValueObserved))
            return new(FusionDisposition.Unknown, FusionSourceKind.Visual, ConflictDisposition.Unresolved, "visual value was not observed");
        if (field is "bounds" or "rendered.bounds")
        {
            return values.Length == 1
                ? new(FusionDisposition.Supported, null, ConflictDisposition.None, "geometry claim agrees")
                : new(FusionDisposition.Conflicted, null, ConflictDisposition.Unresolved, "geometry sources disagree");
        }
        if (sources.Any(x => !x.CapabilityDeclared)) return new(FusionDisposition.Unsupported, null, ConflictDisposition.None, "source capability not declared");
        return values.Length == 1 ? new(FusionDisposition.Supported, null, ConflictDisposition.None, "single auxiliary source")
            : values.Length == 0 ? new(FusionDisposition.Unknown, null, ConflictDisposition.Unresolved, "no value was observed")
            : new(FusionDisposition.Conflicted, null, ConflictDisposition.Unresolved, "no valid field authority");
    }
}
