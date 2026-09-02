using System.Collections.Immutable;
using System.Diagnostics;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Observability;

namespace UniClaw.Runtime.Capabilities.Perception;

// NEW_SYMBOL_JUSTIFICATION: no existing Runtime boundary separates transient
// Fast candidates from accepted stable Slice evidence. These values are
// immutable inputs/results around three stateless functions; they own no
// Runtime state and have no action, identity, graph, progress, or completion
// authority.

/// <summary>Freshness and settle evidence for one raw viewport Observation.</summary>
public sealed record ViewportStabilityEvidence(
    string ObservationRef,
    long ObservationSequence,
    StabilityEvidenceRef EvidenceRef,
    bool IsFresh,
    bool IsSettled,
    string? Reason = null);

/// <summary>Pure stability decision for Slice materialization.</summary>
public sealed record SliceAcceptanceDecision(bool Accepted, string Reason);

/// <summary>Transient visual candidate supplied to Runtime Acceptance.</summary>
public sealed record VisualOccurrenceCandidate(
    string CandidateRef,
    string RawEvidenceRef,
    string? Text,
    string? RawProviderType,
    ElementBounds? Bounds,
    string? StabilizerHint = null,
    bool EdgeClipped = false);

/// <summary>Transient auxiliary structured candidate supplied to correspondence.</summary>
public sealed record StructuredEvidenceCandidate(
    StructuredEvidenceRef EvidenceRef,
    StructuredElementEvidence Evidence);

/// <summary>Transient Fast structural hypothesis over visual candidate references.</summary>
public sealed record FastStructuralHypothesis(
    string HypothesisRef,
    ImmutableArray<string> TargetCandidateRefs,
    FastStructureHint StructureHint,
    FastMemberRoleHint MemberRoleHint,
    FastAffordanceHint AffordanceHint,
    string Source);

/// <summary>Deterministic structured-to-visual correspondence pair.</summary>
public sealed record SourceCorrespondencePair(
    StructuredEvidenceRef StructuredEvidenceRef,
    StructuredElementEvidence StructuredEvidence,
    string VisualCandidateRef,
    double IntersectionOverUnion);

/// <summary>Immutable output of the correspondence pure function.</summary>
public sealed record SourceCorrespondenceResult(
    ImmutableArray<SourceCorrespondencePair> Pairs,
    ImmutableArray<StructuredEvidenceCandidate> UnmatchedStructuredEvidence);

/// <summary>Immutable output of visual occurrence materialization.</summary>
public sealed record OccurrenceMaterialization(
    ImmutableArray<Occurrence> Occurrences,
    ImmutableArray<UnmatchedStructuredEvidence> UnmatchedAuxiliaryEvidence,
    ImmutableArray<string> RejectedVisualCandidateRefs);

/// <summary>Complete immutable input to the Runtime Acceptance facade.</summary>
public sealed record RuntimeAcceptanceInput(
    ContainerSliceRef SliceRef,
    SemanticEvidenceRevision EvidenceRevision,
    ElementBounds ViewportBounds,
    ViewportStabilityEvidence StabilityEvidence,
    ImmutableArray<SpatialRegion> SpatialRegions,
    ImmutableArray<VisualOccurrenceCandidate> VisualCandidates,
    ImmutableArray<StructuredEvidenceCandidate> StructuredCandidates,
    ImmutableArray<FastStructuralHypothesis> FastHypotheses);

/// <summary>
/// Runtime Acceptance result. A rejection carries no Slice or partial entity;
/// an acceptance carries exactly one atomic commit candidate.
/// </summary>
public sealed record RuntimeAcceptanceResult(
    bool Accepted,
    SliceAcceptanceCommit? Commit,
    string? RejectionReason);

/// <summary>Pure viewport stability policy; transient observations create no Slice.</summary>
public static class SliceAcceptancePolicy
{
    /// <summary>Assesses freshness and settle evidence without side effects.</summary>
    public static SliceAcceptanceDecision Assess(ViewportStabilityEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(evidence.ObservationRef))
            return new(false, "observation reference is unavailable");
        if (evidence.ObservationSequence < 0)
            return new(false, "observation sequence is invalid");
        if (!evidence.IsFresh)
            return new(false, "observation is stale");
        if (!evidence.IsSettled)
            return new(false, evidence.Reason ?? "observation is settling or transient");
        return new(true, evidence.Reason ?? "fresh settled viewport accepted");
    }
}

/// <summary>
/// Pure deterministic structured-to-visual correspondence. Structured nodes
/// are auxiliary: unmatched nodes remain auxiliary and never create an
/// Occurrence.
/// </summary>
public static class SourceCorrespondence
{
    /// <summary>Default V1 IoU threshold for deterministic correspondence.</summary>
    public const double DefaultIntersectionOverUnionThreshold = 0.5d;

    /// <summary>Matches each structured candidate to at most one accepted visual candidate.</summary>
    public static SourceCorrespondenceResult Match(
        IReadOnlyList<VisualOccurrenceCandidate> visualCandidates,
        IReadOnlyList<StructuredEvidenceCandidate> structuredCandidates,
        double intersectionOverUnionThreshold = DefaultIntersectionOverUnionThreshold)
    {
        ArgumentNullException.ThrowIfNull(visualCandidates);
        ArgumentNullException.ThrowIfNull(structuredCandidates);
        if (intersectionOverUnionThreshold is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(intersectionOverUnionThreshold));

        var pairs = ImmutableArray.CreateBuilder<SourceCorrespondencePair>();
        var unmatched = ImmutableArray.CreateBuilder<StructuredEvidenceCandidate>();
        foreach (var structured in structuredCandidates)
        {
            VisualOccurrenceCandidate? best = null;
            var bestIou = 0d;
            foreach (var visual in visualCandidates)
            {
                if (!TextMatches(visual.Text, structured.Evidence)
                    || visual.Bounds is not { IsValid: true } visualBounds
                    || structured.Evidence.Bounds is not { IsValid: true } structuredBounds)
                {
                    continue;
                }

                var iou = IntersectionOverUnion(visualBounds, structuredBounds);
                if (iou > bestIou
                    || iou == bestIou && best is not null
                        && string.CompareOrdinal(visual.CandidateRef, best.CandidateRef) < 0)
                {
                    best = visual;
                    bestIou = iou;
                }
            }

            if (best is null || bestIou < intersectionOverUnionThreshold)
            {
                unmatched.Add(structured);
                continue;
            }

            pairs.Add(new SourceCorrespondencePair(
                structured.EvidenceRef,
                structured.Evidence,
                best.CandidateRef,
                bestIou));
        }

        return new SourceCorrespondenceResult(pairs.ToImmutable(), unmatched.ToImmutable());
    }

    private static bool TextMatches(string? visualText, StructuredElementEvidence structured)
    {
        var visual = Normalize(visualText);
        var structuredText = Normalize(structured.RawText);
        var description = Normalize(structured.ContentDescription);
        return visual.Length > 0
            && (string.Equals(visual, structuredText, StringComparison.Ordinal)
                || string.Equals(visual, description, StringComparison.Ordinal));
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static double IntersectionOverUnion(ElementBounds first, ElementBounds second)
    {
        var width = Math.Min(first.X2, second.X2) - Math.Max(first.X1, second.X1);
        var height = Math.Min(first.Y2, second.Y2) - Math.Max(first.Y1, second.Y1);
        if (width <= 0f || height <= 0f)
            return 0d;
        var intersection = (double)width * height;
        var union = (double)first.Width * first.Height + (double)second.Width * second.Height - intersection;
        return union <= 0d ? 0d : intersection / union;
    }
}

/// <summary>Pure mapper from accepted visual candidates to Occurrence evidence.</summary>
public static class OccurrenceMaterializer
{
    /// <summary>Materializes only valid visual candidates and retains unmatched structured evidence separately.</summary>
    public static OccurrenceMaterialization Materialize(
        ContainerSliceRef sliceRef,
        IReadOnlyList<SpatialRegion> spatialRegions,
        IReadOnlyList<VisualOccurrenceCandidate> visualCandidates,
        SourceCorrespondenceResult correspondence)
    {
        ArgumentNullException.ThrowIfNull(spatialRegions);
        ArgumentNullException.ThrowIfNull(visualCandidates);
        ArgumentNullException.ThrowIfNull(correspondence);

        var occurrences = ImmutableArray.CreateBuilder<Occurrence>();
        var rejected = ImmutableArray.CreateBuilder<string>();
        foreach (var visual in visualCandidates)
        {
            if (string.IsNullOrWhiteSpace(visual.CandidateRef)
                || string.IsNullOrWhiteSpace(visual.RawEvidenceRef)
                || visual.Bounds is not { IsValid: true } bounds)
            {
                rejected.Add(visual.CandidateRef ?? string.Empty);
                continue;
            }

            var occurrenceRef = ToOccurrenceRef(sliceRef, visual.CandidateRef);
            var assessed = SpatialRegionBinding.Assess(bounds, spatialRegions);
            var binding = new OccurrenceRegionBinding(
                occurrenceRef,
                assessed.PrimarySpatialRegionRef,
                assessed.OverlapRatio,
                assessed.Ambiguous);
            var matched = correspondence.Pairs
                .Where(pair => string.Equals(pair.VisualCandidateRef, visual.CandidateRef, StringComparison.Ordinal))
                .OrderBy(pair => pair.StructuredEvidenceRef.Value, StringComparer.Ordinal)
                .ToImmutableArray();
            var matchedStructured = matched
                .Select(pair => pair.StructuredEvidence)
                .ToImmutableArray();
            var regionRelativeBounds = binding.PrimarySpatialRegionRef is { } regionRef
                ? ToRegionRelativeBounds(bounds, spatialRegions.Single(region => region.RegionRef == regionRef).Bounds)
                : null;

            occurrences.Add(new Occurrence(
                occurrenceRef,
                sliceRef,
                PrimitiveTaxonomy.Map(visual.RawProviderType),
                bounds,
                binding,
                visual.RawEvidenceRef,
                regionRelativeBounds,
                MergeStateHints(matchedStructured),
                matched.Select(pair => pair.StructuredEvidenceRef),
                visual.StabilizerHint,
                visual.EdgeClipped));
        }

        var auxiliary = correspondence.UnmatchedStructuredEvidence
            .Select(candidate => new UnmatchedStructuredEvidence(candidate.EvidenceRef, sliceRef, candidate.Evidence))
            .ToImmutableArray();
        return new OccurrenceMaterialization(
            occurrences.ToImmutable(),
            auxiliary,
            rejected.ToImmutable());
    }

    /// <summary>Builds the deterministic Run-local occurrence reference for a visual candidate.</summary>
    public static ViewportOccurrenceRef ToOccurrenceRef(ContainerSliceRef sliceRef, string candidateRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRef);
        return new ViewportOccurrenceRef($"occurrence:{sliceRef.Value}:{candidateRef}");
    }

    private static ElementBounds? ToRegionRelativeBounds(ElementBounds occurrence, ElementBounds region)
    {
        if (region.Width <= 0f || region.Height <= 0f)
            return null;
        return new ElementBounds(
            Math.Clamp((occurrence.X1 - region.X1) / region.Width, 0f, 1f),
            Math.Clamp((occurrence.Y1 - region.Y1) / region.Height, 0f, 1f),
            Math.Clamp((occurrence.X2 - region.X1) / region.Width, 0f, 1f),
            Math.Clamp((occurrence.Y2 - region.Y1) / region.Height, 0f, 1f));
    }

    private static OccurrenceStateHints MergeStateHints(IEnumerable<StructuredElementEvidence> evidence)
    {
        var values = evidence.ToImmutableArray();
        return new OccurrenceStateHints(
            Merge(values.Select(value => value.Clickable)),
            Merge(values.Select(value => value.Checkable)),
            Merge(values.Select(value => value.Checked)),
            Merge(values.Select(value => value.Enabled)),
            Merge(values.Select(value => value.Focusable)));
    }

    private static bool? Merge(IEnumerable<bool?> values)
    {
        var known = values.Where(value => value.HasValue).Select(value => value!.Value).Distinct().ToArray();
        return known.Length == 1 ? known[0] : null;
    }

}

/// <summary>Pure V1 mapping from raw provider labels to visual primitive hints.</summary>
public static class PrimitiveTaxonomy
{
    /// <summary>Maps raw provider evidence without creating semantic identity or affordance authority.</summary>
    public static VisualPrimitiveKind Map(string? rawProviderType)
        => rawProviderType?.Trim().ToLowerInvariant() switch
        {
            "text" or "text_block" or "menuitem" or "menu_item" => VisualPrimitiveKind.Text,
            "icon" => VisualPrimitiveKind.Icon,
            "toggle" or "switch" or "checkbox" => VisualPrimitiveKind.Toggle,
            "image" => VisualPrimitiveKind.Image,
            "divider" or "separator" => VisualPrimitiveKind.Divider,
            "group" or "container" => VisualPrimitiveKind.Group,
            _ => VisualPrimitiveKind.Unknown,
        };
}

/// <summary>
/// Acceptance facade coordinating three pure functions and observability. It
/// creates one immutable commit candidate but owns no mutable Runtime state.
/// </summary>
public static class RuntimeAcceptance
{
    /// <summary>Evaluates one raw viewport and returns either no entity or one atomic commit candidate.</summary>
    public static RuntimeAcceptanceResult Evaluate(RuntimeAcceptanceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var span = RuntimeObservability.StartSpan(
            "RuntimeAcceptance",
            ObservabilityLayer.Capability,
            ObservabilityComponent.PerceptionAcceptance);
        RuntimeObservability.SetTag(span, "observation.ref", input.StabilityEvidence.ObservationRef);

        var decision = SliceAcceptancePolicy.Assess(input.StabilityEvidence);
        if (!decision.Accepted
            || input.ViewportBounds is not { IsValid: true }
            || input.SpatialRegions.IsDefaultOrEmpty
            || input.SpatialRegions.Any(region => region is null || !region.IsValid)
            || input.SpatialRegions.Select(region => region.RegionRef).Distinct().Count() != input.SpatialRegions.Length
            || input.VisualCandidates.IsDefault
            || input.VisualCandidates.Any(candidate => candidate is null)
            || input.VisualCandidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate.CandidateRef))
                .Select(candidate => candidate.CandidateRef).Distinct(StringComparer.Ordinal).Count()
                != input.VisualCandidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.CandidateRef))
            || input.StructuredCandidates.IsDefault
            || input.StructuredCandidates.Any(candidate => candidate is null || candidate.Evidence is null))
        {
            var reason = decision.Accepted
                ? "viewport bounds or spatial regions are invalid"
                : decision.Reason;
            EmitDecision(span, ObservabilityEvidenceEvent.SliceAcceptanceRejected, input.StabilityEvidence.ObservationRef, "slice", reason, "REJECT");
            RuntimeObservability.Complete(span, ObservabilityOutcome.Succeeded);
            return new RuntimeAcceptanceResult(false, null, reason);
        }

        var correspondence = SourceCorrespondence.Match(input.VisualCandidates, input.StructuredCandidates);
        var materialization = OccurrenceMaterializer.Materialize(
            input.SliceRef,
            input.SpatialRegions,
            input.VisualCandidates,
            correspondence);
        foreach (var candidateRef in materialization.RejectedVisualCandidateRefs)
        {
            EmitDecision(span, ObservabilityEvidenceEvent.AcceptanceCandidateRejected, input.StabilityEvidence.ObservationRef, candidateRef, "invalid visual candidate", "REJECT");
        }
        foreach (var occurrence in materialization.Occurrences.Where(value => value.PrimitiveKind == VisualPrimitiveKind.Unknown))
        {
            EmitDecision(span, ObservabilityEvidenceEvent.AcceptanceCandidateDegraded, input.StabilityEvidence.ObservationRef, occurrence.OccurrenceRef.Value, "primitive unresolved", "ACCEPT_UNKNOWN");
        }
        foreach (var auxiliary in materialization.UnmatchedAuxiliaryEvidence)
        {
            EmitDecision(span, ObservabilityEvidenceEvent.StructuredEvidenceUnmatched, input.StabilityEvidence.ObservationRef, auxiliary.EvidenceRef.Value, "no deterministic visual correspondence", "AUXILIARY_ONLY");
        }

        var occurrenceByCandidate = input.VisualCandidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.CandidateRef))
            .ToDictionary(
                candidate => candidate.CandidateRef,
                candidate => OccurrenceMaterializer.ToOccurrenceRef(input.SliceRef, candidate.CandidateRef),
                StringComparer.Ordinal);
        var acceptedOccurrenceRefs = materialization.Occurrences
            .Select(occurrence => occurrence.OccurrenceRef)
            .ToHashSet();
        var assessments = ImmutableArray.CreateBuilder<FastAssessment>();
        var hypotheses = input.FastHypotheses.IsDefault
            ? ImmutableArray<FastStructuralHypothesis>.Empty
            : input.FastHypotheses;
        foreach (var hypothesis in hypotheses)
        {
            if (hypothesis is null
                || string.IsNullOrWhiteSpace(hypothesis.Source)
                || hypothesis.TargetCandidateRefs.IsDefault)
                continue;
            var targets = hypothesis.TargetCandidateRefs
                .Where(occurrenceByCandidate.ContainsKey)
                .Select(candidateRef => occurrenceByCandidate[candidateRef])
                .Where(acceptedOccurrenceRefs.Contains)
                .Distinct()
                .ToImmutableArray();
            if (string.IsNullOrWhiteSpace(hypothesis.HypothesisRef) || targets.IsDefaultOrEmpty)
                continue;
            assessments.Add(new FastAssessment(
                new FastAssessmentRef($"fast:{input.SliceRef.Value}:{hypothesis.HypothesisRef}"),
                input.SliceRef,
                targets,
                hypothesis.StructureHint,
                hypothesis.MemberRoleHint,
                hypothesis.AffordanceHint,
                hypothesis.Source));
        }

        var acceptedAssessments = assessments.ToImmutable();
        var slice = new ContainerSlice(
            input.SliceRef,
            input.EvidenceRevision,
            evidenceRefs: [input.StabilityEvidence.EvidenceRef.Value],
            observationRef: input.StabilityEvidence.ObservationRef,
            viewportBounds: input.ViewportBounds,
            spatialRegionRefs: input.SpatialRegions.Select(region => region.RegionRef),
            occurrenceRefs: materialization.Occurrences.Select(occurrence => occurrence.OccurrenceRef),
            fastAssessmentRefs: acceptedAssessments.Select(assessment => assessment.AssessmentRef),
            stabilityEvidenceRef: input.StabilityEvidence.EvidenceRef);
        var commit = new SliceAcceptanceCommit(
            slice,
            input.SpatialRegions,
            materialization.Occurrences,
            acceptedAssessments,
            materialization.UnmatchedAuxiliaryEvidence);
        RuntimeObservability.Complete(span, ObservabilityOutcome.Succeeded);
        return new RuntimeAcceptanceResult(true, commit, null);
    }

    /// <summary>Evaluates and prepares the commit through the sole V2 reducer replacement seam.</summary>
    public static ContainerRuntimeV2Preparation Prepare(
        ContainerRuntimeV2State previous,
        RuntimeAcceptanceInput input)
    {
        var acceptance = Evaluate(input);
        if (!acceptance.Accepted)
        {
            return ContainerRuntimeV2Preparation.Rejected(
                previous,
                acceptance.RejectionReason ?? "Runtime Acceptance rejected the viewport");
        }
        return ContainerRuntimeV2Reducer.PrepareAcceptedEvidence(previous, acceptance.Commit);
    }

    private static void EmitDecision(
        Activity? span,
        string eventName,
        string observationRef,
        string candidateSummary,
        string rejectReason,
        string validatorDecision)
        => RuntimeObservability.AddEvent(
            span,
            eventName,
            ("observation.ref", observationRef),
            ("candidate.summary", candidateSummary),
            ("reject.reason", rejectReason),
            ("validator.decision", validatorDecision));
}
