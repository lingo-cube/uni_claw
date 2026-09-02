using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using SemanticEvidenceValue = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class FastContainerResolverTests
{
    [Fact]
    public void MayEnterWithIndependentFreshSemanticSupportProducesTrustedNewAssessment()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "wallpaper",
            independentBoundarySupport: true,
            triggerDestinationSemanticMatch: true,
            semanticCandidate: "Wallpaper",
            graphCandidate: Node("wallpaper", "Wallpaper")));

        Assert.Equal(FastContainerResolutionKind.NEW_CONTAINER, assessment.Resolution);
        Assert.True(assessment.FastTrusted);
        Assert.Equal("Wallpaper", assessment.IdentityCandidate);
        Assert.Equal(new ContainerNodeRef("wallpaper"), assessment.GraphPriorNodeRef);
        Assert.False(assessment.IsAbstained);
    }

    [Fact]
    public void StrongSamePriorWithFreshContinuityProducesSameContainer()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.STRONG_SAME,
            current: "settings",
            freshSameContainerSupport: true));

        Assert.Equal(FastContainerResolutionKind.SAME_CONTAINER, assessment.Resolution);
        Assert.False(assessment.FastTrusted);
        Assert.False(assessment.IsAbstained);
    }

    [Fact]
    public void TransientEvidenceRemainsTransientAndAbstained()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "overlay",
            independentBoundarySupport: true,
            transientEvidence: true,
            semanticCandidate: "Wallpaper"));

        Assert.Equal(FastContainerResolutionKind.TRANSIENT, assessment.Resolution);
        Assert.True(assessment.IsAbstained);
        Assert.False(assessment.FastTrusted);
    }

    [Fact]
    public void InsufficientEvidenceProducesAmbiguousAssessment()
    {
        var assessment = FastContainerResolver.Resolve(Request(FastActionPriorKind.MAY_ENTER));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.True(assessment.IsAbstained);
        Assert.False(assessment.FastTrusted);
    }

    [Fact]
    public void HardConflictPrecedesAllSupportAndPrior()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.STRONG_SAME,
            current: "desktop",
            candidate: "wallpaper",
            independentBoundarySupport: true,
            freshSameContainerSupport: true,
            triggerDestinationSemanticMatch: true,
            semanticCandidate: "Wallpaper",
            hardConflict: true));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.True(assessment.HardConflict);
        Assert.True(assessment.IsAbstained);
        Assert.False(assessment.FastTrusted);
    }

    [Fact]
    public void ActionPriorAloneDoesNotBecomeBoundaryTruth()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            semanticCandidate: "Wallpaper"));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.True(assessment.IsAbstained);
    }

    [Fact]
    public void GraphPriorAloneDoesNotBecomeBoundaryTruth()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.UNKNOWN,
            graphCandidate: Node("wallpaper", "Wallpaper")));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.Null(assessment.IdentityCandidate);
        Assert.Null(assessment.GraphPriorNodeRef);
    }

    [Fact]
    public void SemanticCandidateAloneDoesNotBecomeBoundaryTruth()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.UNKNOWN,
            semanticCandidate: "Wallpaper"));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.Equal("Wallpaper", assessment.IdentityCandidate);
        Assert.True(assessment.SemanticSupport);
    }

    [Fact]
    public void TriggerDestinationSupportDoesNotProveIdentityTruth()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "working-node",
            independentBoundarySupport: true,
            triggerDestinationSemanticMatch: true));

        Assert.Equal(FastContainerResolutionKind.NEW_CONTAINER, assessment.Resolution);
        Assert.True(assessment.SemanticSupport);
        Assert.True(assessment.TriggerDestinationSemanticMatch);
        Assert.Null(assessment.IdentityCandidate);
        Assert.True(assessment.FastTrusted);
    }

    [Fact]
    public void MismatchedExpectedRevisionAbstainsFailClosed()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "wallpaper",
            independentBoundarySupport: true,
            semanticCandidate: "Wallpaper",
            expectedRevision: 2));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.True(assessment.IsAbstained);
        Assert.Contains("revision", assessment.AbstentionReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolverAbstentionDoesNotMutateGraphOrCurrentInputs()
    {
        var node = Node("desktop", "Desktop");
        var graph = new ContainerGraphSnapshot(nodes: [node]);
        var current = new CurrentContainer(new ContainerNodeRef("desktop"), new ContainerSliceRef("slice:1"));
        var state = new ContainerRuntimeV2State(graph, current, evidenceRevision: new SemanticEvidenceRevision(1));
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            semanticCandidate: "Wallpaper",
            graphCandidate: node));

        Assert.True(assessment.IsAbstained);
        Assert.Same(graph, state.Graph);
        Assert.Same(current, state.CurrentContainer);
        Assert.Single(state.Graph.Nodes);
    }

    [Fact]
    public void SemanticSimilarityCandidateIsEvidenceNotBoundaryTruth()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.UNKNOWN,
            semanticCandidate: "VectorNearestCandidate"));

        Assert.Equal(FastContainerResolutionKind.AMBIGUOUS, assessment.Resolution);
        Assert.False(assessment.FastTrusted);
    }

    [Fact]
    public void StaleAcceptedSemanticEvidenceDoesNotProvideSemanticSupport()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "wallpaper",
            independentBoundarySupport: true,
            semanticCandidate: "Wallpaper",
            freshObservationSequence: 2));

        Assert.Equal(FastContainerResolutionKind.NEW_CONTAINER, assessment.Resolution);
        Assert.False(assessment.SemanticSupport);
        Assert.False(assessment.FastTrusted);
    }

    [Fact]
    public void ScopeMismatchedAcceptedSemanticEvidenceDoesNotProvideSupport()
    {
        var assessment = FastContainerResolver.Resolve(Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "wallpaper",
            independentBoundarySupport: true,
            semanticCandidate: "Wallpaper",
            semanticScope: SemanticEvidenceScope.CurrentContainer));

        Assert.Equal(FastContainerResolutionKind.NEW_CONTAINER, assessment.Resolution);
        Assert.False(assessment.SemanticSupport);
        Assert.False(assessment.FastTrusted);
    }

    [Fact]
    public void UnvalidatedRawSemanticEvidenceCannotEnterFastRequest()
    {
        var rawEvidence = new SemanticEvidenceValue(
            "evidence:raw",
            "v1",
            "FAST",
            SemanticEvidenceKind.ContainerIdentity,
            "Wallpaper",
            0.99,
            SemanticEvidenceScope.CurrentObservation,
            1,
            DateTimeOffset.UtcNow);
        var request = Request(
            FastActionPriorKind.MAY_ENTER,
            current: "desktop",
            candidate: "wallpaper",
            independentBoundarySupport: true);

        Assert.NotNull(rawEvidence);
        var assessment = FastContainerResolver.Resolve(request);
        Assert.False(assessment.SemanticSupport);
        Assert.False(assessment.FastTrusted);
    }

    private static FastContainerResolutionRequest Request(
        FastActionPriorKind prior,
        string? current = null,
        string? candidate = null,
        bool independentBoundarySupport = false,
        bool freshSameContainerSupport = false,
        bool transientEvidence = false,
        bool hardConflict = false,
        bool triggerDestinationSemanticMatch = false,
        string? semanticCandidate = null,
        ContainerGraphNode? graphCandidate = null,
        long expectedRevision = 0,
        long freshObservationSequence = 1,
        SemanticEvidenceScope semanticScope = SemanticEvidenceScope.CurrentObservation)
    {
        var semanticEvidence = semanticCandidate is null
            ? null
            : new[] { new SemanticEvidenceValue(
                "evidence:fast",
                "v1",
                "FAST",
                SemanticEvidenceKind.ContainerIdentity,
                semanticCandidate,
                0.95,
                semanticScope,
                1,
                DateTimeOffset.UtcNow) };
        var validatedSemanticEvidence = semanticEvidence is null
            ? ValidatedSemanticEvidenceResult.Empty
            : new ValidatedSemanticEvidenceResult(
                semanticEvidence.ToImmutableArray(),
                ImmutableArray<SemanticEvidenceValue>.Empty,
                ImmutableArray<SemanticEvidenceRejection>.Empty,
                [new SemanticEvidenceWeight("evidence:fast", 0.95)]);
        return new FastContainerResolutionRequest(
            new SemanticEvidenceRevision(1),
            new ContainerSliceRef("slice:1"),
            freshObservationSequence,
            prior,
            current is null ? null : new ContainerNodeRef(current),
            candidate is null ? null : new ContainerNodeRef(candidate),
            independentBoundarySupport,
            freshSameContainerSupport,
            transientEvidence,
            hardConflict,
            triggerDestinationSemanticMatch,
            validatedSemanticEvidence,
            graphCandidate is null ? null : new[] { graphCandidate },
            expectedRevision == 0 ? null : new SemanticEvidenceRevision(expectedRevision),
            SemanticEvidenceScope.CurrentObservation);
    }

    private static ContainerGraphNode Node(string nodeRef, string identity)
        => new(new ContainerNodeRef(nodeRef), identity);
}
