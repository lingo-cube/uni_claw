using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;
using UniClaw.Runtime.Model;
using AdmittedSemanticEvidence = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;

namespace UniClaw.Runtime.World;

/// <summary>Bounded prior supplied by the action context.</summary>
public enum FastActionPriorKind
{
    /// <summary>No useful boundary prior is available.</summary>
    UNKNOWN,
    /// <summary>The preceding context may enter another Container.</summary>
    MAY_ENTER,
    /// <summary>The preceding context may return to an earlier Container.</summary>
    MAY_RETURN,
    /// <summary>The preceding context strongly predicts same-Container continuity.</summary>
    STRONG_SAME,
    /// <summary>The preceding context may cross an external Container boundary.</summary>
    MAY_EXTERNAL,
}

/// <summary>Fast evidence interpretation of the observed boundary.</summary>
public enum FastContainerResolutionKind
{
    /// <summary>The fresh evidence supports continuity in the current Container.</summary>
    SAME_CONTAINER,
    /// <summary>The fresh evidence supports an independent Container.</summary>
    NEW_CONTAINER,
    /// <summary>The fresh evidence is transient or intermediate.</summary>
    TRANSIENT,
    /// <summary>The available evidence cannot safely disambiguate the boundary.</summary>
    AMBIGUOUS,
}

/// <summary>
/// Immutable inputs for one synchronous Fast Container interpretation. Graph
/// and action values are priors; fresh evidence and semantic candidates remain
/// evidence, not world or execution authority.
/// </summary>
public sealed record FastContainerResolutionRequest
{
    /// <summary>Creates one immutable, backend-neutral Fast request.</summary>
    public FastContainerResolutionRequest(
        SemanticEvidenceRevision evidenceRevision,
        ContainerSliceRef? freshSliceRef,
        long freshObservationSequence,
        FastActionPriorKind actionPrior,
        ContainerNodeRef? currentNodeRef = null,
        ContainerNodeRef? candidateNodeRef = null,
        bool independentBoundarySupport = false,
        bool freshSameContainerSupport = false,
        bool transientEvidence = false,
        bool hardConflict = false,
        bool triggerDestinationSemanticMatch = false,
        ValidatedSemanticEvidenceResult? validatedSemanticEvidence = null,
        IEnumerable<ContainerGraphNode>? graphCandidates = null,
        SemanticEvidenceRevision? expectedEvidenceRevision = null,
        SemanticEvidenceScope freshEvidenceScope = SemanticEvidenceScope.CurrentObservation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(freshObservationSequence);
        EvidenceRevision = evidenceRevision;
        FreshSliceRef = freshSliceRef;
        FreshObservationSequence = freshObservationSequence;
        ActionPrior = actionPrior;
        CurrentNodeRef = currentNodeRef;
        CandidateNodeRef = candidateNodeRef;
        IndependentBoundarySupport = independentBoundarySupport;
        FreshSameContainerSupport = freshSameContainerSupport;
        TransientEvidence = transientEvidence;
        HardConflict = hardConflict;
        TriggerDestinationSemanticMatch = triggerDestinationSemanticMatch;
        ValidatedSemanticEvidence = validatedSemanticEvidence;
        GraphCandidates = graphCandidates is null
            ? ImmutableArray<ContainerGraphNode>.Empty
            : graphCandidates.ToImmutableArray();
        ExpectedEvidenceRevision = expectedEvidenceRevision;
        FreshEvidenceScope = freshEvidenceScope;
    }

    /// <summary>Gets the fresh evidence revision being interpreted.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the fresh Slice reference, when a fresh window exists.</summary>
    public ContainerSliceRef? FreshSliceRef { get; }
    /// <summary>Gets the observation sequence represented by the fresh Slice.</summary>
    public long FreshObservationSequence { get; }
    /// <summary>Gets the action-context boundary prior.</summary>
    public FastActionPriorKind ActionPrior { get; }
    /// <summary>Gets the current working node prior, when known.</summary>
    public ContainerNodeRef? CurrentNodeRef { get; }
    /// <summary>Gets the independently observed destination candidate, when known.</summary>
    public ContainerNodeRef? CandidateNodeRef { get; }
    /// <summary>Gets whether fresh evidence supports an independent boundary.</summary>
    public bool IndependentBoundarySupport { get; }
    /// <summary>Gets whether fresh evidence supports same-Container continuity.</summary>
    public bool FreshSameContainerSupport { get; }
    /// <summary>Gets whether fresh evidence is transient or intermediate.</summary>
    public bool TransientEvidence { get; }
    /// <summary>Gets whether fresh evidence contains a hard conflict.</summary>
    public bool HardConflict { get; }
    /// <summary>Gets whether trigger and destination semantics mutually support one another.</summary>
    public bool TriggerDestinationSemanticMatch { get; }
    /// <summary>Gets the existing Runtime admission result for semantic evidence.</summary>
    public ValidatedSemanticEvidenceResult? ValidatedSemanticEvidence { get; }
    /// <summary>Gets authority-free Graph candidate evidence copied immutably.</summary>
    public ImmutableArray<ContainerGraphNode> GraphCandidates { get; }
    /// <summary>Gets the optional revision that the caller requires this request to match.</summary>
    public SemanticEvidenceRevision? ExpectedEvidenceRevision { get; }
    /// <summary>Gets the scope that accepted semantic evidence must match.</summary>
    public SemanticEvidenceScope FreshEvidenceScope { get; }
}

/// <summary>
/// Immutable, revision-bound Fast interpretation. Fast trust is a derived
/// property and never becomes a mutable Runtime slot or execution decision.
/// </summary>
public sealed record FastContainerAssessment
{
    /// <summary>Creates one immutable Fast assessment value.</summary>
    public FastContainerAssessment(
        FastContainerResolutionKind resolution,
        SemanticEvidenceRevision evidenceRevision,
        ContainerNodeRef? currentNodeRef,
        ContainerNodeRef? candidateNodeRef,
        string? identityCandidate,
        ContainerNodeRef? graphPriorNodeRef,
        bool independentBoundarySupport,
        bool semanticSupport,
        bool triggerDestinationSemanticMatch,
        bool hardConflict,
        bool isAbstained,
        string? abstentionReason = null)
    {
        Resolution = resolution;
        EvidenceRevision = evidenceRevision;
        CurrentNodeRef = currentNodeRef;
        CandidateNodeRef = candidateNodeRef;
        IdentityCandidate = string.IsNullOrWhiteSpace(identityCandidate) ? null : identityCandidate;
        GraphPriorNodeRef = graphPriorNodeRef;
        IndependentBoundarySupport = independentBoundarySupport;
        SemanticSupport = semanticSupport;
        TriggerDestinationSemanticMatch = triggerDestinationSemanticMatch;
        HardConflict = hardConflict;
        IsAbstained = isAbstained;
        AbstentionReason = string.IsNullOrWhiteSpace(abstentionReason) ? null : abstentionReason;
    }

    /// <summary>Gets the derived Fast boundary interpretation.</summary>
    public FastContainerResolutionKind Resolution { get; }
    /// <summary>Gets the exact fresh evidence revision assessed.</summary>
    public SemanticEvidenceRevision EvidenceRevision { get; }
    /// <summary>Gets the current node reference supplied as context.</summary>
    public ContainerNodeRef? CurrentNodeRef { get; }
    /// <summary>Gets the observed destination candidate, when available.</summary>
    public ContainerNodeRef? CandidateNodeRef { get; }
    /// <summary>Gets the semantic identity candidate, never as proven identity.</summary>
    public string? IdentityCandidate { get; }
    /// <summary>Gets the Graph prior node selected only when it matches semantic evidence.</summary>
    public ContainerNodeRef? GraphPriorNodeRef { get; }
    /// <summary>Gets whether fresh evidence supports an independent boundary.</summary>
    public bool IndependentBoundarySupport { get; }
    /// <summary>Gets whether accepted semantic evidence supports the interpretation.</summary>
    public bool SemanticSupport { get; }
    /// <summary>Gets whether trigger and destination semantics mutually support one another.</summary>
    public bool TriggerDestinationSemanticMatch { get; }
    /// <summary>Gets whether a hard conflict was observed.</summary>
    public bool HardConflict { get; }
    /// <summary>Gets whether the resolver abstained from a stronger interpretation.</summary>
    public bool IsAbstained { get; }
    /// <summary>Gets the explicit abstention reason, when abstained.</summary>
    public string? AbstentionReason { get; }
    /// <summary>
    /// Gets derived Fast Trust. It requires independent boundary support,
    /// semantic support, and no hard conflict; it grants no execution authority.
    /// </summary>
    public bool FastTrusted => !IsAbstained
        && Resolution == FastContainerResolutionKind.NEW_CONTAINER
        && IndependentBoundarySupport
        && SemanticSupport
        && !HardConflict;
}

/// <summary>
/// Pure synchronous Fast resolver. It produces only a revision-bound
/// assessment and never mutates Graph, CurrentContainer, or any external state.
/// NEW_SYMBOL_JUSTIFICATION: existing semantic fusion validates semantic
/// evidence, while the V2 Graph model records evidence; neither owns the
/// independent action-prior plus fresh-boundary working interpretation. A
/// static resolver is the smallest replacement/test seam and avoids a new
/// mutable owner or external capability contract.
/// </summary>
public static class FastContainerResolver
{
    /// <summary>
    /// Resolves one immutable request into a derived Fast assessment.
    /// Insufficient, stale, or conflicting input returns AMBIGUOUS with an
    /// explicit abstention reason.
    /// </summary>
    /// <param name="request">Immutable action, fresh evidence, semantic and Graph priors.</param>
    /// <returns>A revision-bound, non-authoritative assessment.</returns>
    public static FastContainerAssessment Resolve(FastContainerResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identityCandidate = (request.ValidatedSemanticEvidence?.AcceptedEvidence ?? ImmutableArray<AdmittedSemanticEvidence>.Empty)
            .Where(evidence => evidence.ObservationSequence == request.FreshObservationSequence
                && evidence.Scope == request.FreshEvidenceScope)
            .Where(evidence => !string.IsNullOrWhiteSpace(evidence.Candidate) && evidence.Confidence > 0d)
            .OrderByDescending(evidence => evidence.Confidence)
            .Select(evidence => evidence.Candidate)
            .FirstOrDefault();
        var semanticSupport = identityCandidate is not null || request.TriggerDestinationSemanticMatch;
        var graphPriorNodeRef = identityCandidate is null
            ? null
            : request.GraphCandidates
                .Where(node => string.Equals(node.SemanticIdentityCandidate, identityCandidate, StringComparison.Ordinal))
                .Select(node => (ContainerNodeRef?)node.NodeRef)
                .FirstOrDefault();

        if (request.FreshSliceRef is null)
        {
            return Abstain(request, identityCandidate, graphPriorNodeRef, semanticSupport, "fresh Slice evidence is unavailable");
        }

        if (request.ExpectedEvidenceRevision is { } expected
            && expected != request.EvidenceRevision)
        {
            return Abstain(request, identityCandidate, graphPriorNodeRef, semanticSupport, "evidence revision does not match the expected revision");
        }

        if (!Enum.IsDefined(request.ActionPrior))
        {
            return Abstain(request, identityCandidate, graphPriorNodeRef, semanticSupport, "action prior is invalid");
        }

        if (request.HardConflict)
        {
            return Abstain(request, identityCandidate, graphPriorNodeRef, semanticSupport, "hard conflict takes precedence over priors");
        }

        if (request.TransientEvidence)
        {
            return new FastContainerAssessment(
                FastContainerResolutionKind.TRANSIENT,
                request.EvidenceRevision,
                request.CurrentNodeRef,
                request.CandidateNodeRef,
                identityCandidate,
                graphPriorNodeRef,
                request.IndependentBoundarySupport,
                semanticSupport,
                request.TriggerDestinationSemanticMatch,
                false,
                true,
                "fresh evidence is transient or intermediate");
        }

        if (request.IndependentBoundarySupport
            && request.CandidateNodeRef is { } candidate
            && request.CurrentNodeRef != candidate)
        {
            return Assessment(
                request,
                FastContainerResolutionKind.NEW_CONTAINER,
                identityCandidate,
                graphPriorNodeRef,
                semanticSupport);
        }

        if (request.FreshSameContainerSupport)
        {
            return Assessment(
                request,
                FastContainerResolutionKind.SAME_CONTAINER,
                identityCandidate,
                graphPriorNodeRef,
                semanticSupport);
        }

        return Abstain(request, identityCandidate, graphPriorNodeRef, semanticSupport, "fresh boundary evidence is insufficient");
    }

    private static FastContainerAssessment Assessment(
        FastContainerResolutionRequest request,
        FastContainerResolutionKind resolution,
        string? identityCandidate,
        ContainerNodeRef? graphPriorNodeRef,
        bool semanticSupport)
        => new(
            resolution,
            request.EvidenceRevision,
            request.CurrentNodeRef,
            request.CandidateNodeRef,
            identityCandidate,
            graphPriorNodeRef,
            request.IndependentBoundarySupport,
            semanticSupport,
            request.TriggerDestinationSemanticMatch,
            request.HardConflict,
            false);

    private static FastContainerAssessment Abstain(
        FastContainerResolutionRequest request,
        string? identityCandidate,
        ContainerNodeRef? graphPriorNodeRef,
        bool semanticSupport,
        string reason)
        => new(
            FastContainerResolutionKind.AMBIGUOUS,
            request.EvidenceRevision,
            request.CurrentNodeRef,
            request.CandidateNodeRef,
            identityCandidate,
            graphPriorNodeRef,
            request.IndependentBoundarySupport,
            semanticSupport,
            request.TriggerDestinationSemanticMatch,
            request.HardConflict,
            true,
            reason);
}
