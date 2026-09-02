using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Semantic Perception Provider — the pipeline assembler of the separated
/// Semantic Fast layers:
///
///   Feature → Embedding → Prototype(S) → Retrieval → Candidate Policy → Evidence
///
/// It produces <see cref="SemanticEvidence"/> of kind ContainerIdentity, or
/// empty evidence (ABSTAIN). It never returns Fact / Belief / CurrentContainer /
/// Action. On provider/internal failure it returns empty evidence so Runtime
/// continues unchanged (fail-safe, unchanged from legacy). It never executes an
/// action and never mutates world state.
///
/// A provider is assembled from the separated components; it has no policy,
/// prototype, or threshold of its own:
///   - V1 path        : IContainerIdentityPrototypeStore + DeterministicSemanticMatcher
///   - vector path    : IEmbeddingProvider + IVectorSemanticIndex
///   - both paths share: IContainerIdentityCandidatePolicy + IContainerIdentityEvidenceBuilder
/// </summary>
public sealed class FastSemanticContainerIdentityProvider : ISemanticProvider
{
    private readonly IContainerSemanticFeatureExtractor _extractor;
    private readonly IContainerIdentityPrototypeStore _prototypes;
    private readonly DeterministicSemanticMatcher? _matcher;
    private readonly IEmbeddingProvider? _embedding;
    private readonly IVectorSemanticIndex? _vectorIndex;
    private readonly IContainerIdentityCandidatePolicy _policy;
    private readonly IContainerIdentityEvidenceBuilder _evidence;
    private readonly string _source;

    /// <summary>
    /// V1 separated path: feature extraction → prototype store → deterministic
    /// reference matcher → candidate policy → evidence builder. Default policy
    /// is the V1 policy (threshold + structural + conflict + minimum evidence).
    /// </summary>
    public FastSemanticContainerIdentityProvider(
        IContainerIdentityPrototypeStore prototypes,
        IContainerIdentityCandidatePolicy? candidatePolicy = null,
        IContainerIdentityEvidenceBuilder? evidenceBuilder = null,
        DeterministicSemanticMatcher? matcher = null,
        IContainerSemanticFeatureExtractor? extractor = null,
        string source = "FAST")
        : this(
            extractor ?? new FastSemanticFeatureExtractor(),
            prototypes,
            matcher ?? new DeterministicSemanticMatcher(),
            embedding: null,
            vectorIndex: null,
            candidatePolicy ?? CandidatePolicies.LegacyReference(),
            evidenceBuilder ?? new ContainerIdentityEvidenceBuilder(),
            source)
    {
    }

    /// <summary>
    /// Vector path: feature extraction → embedding provider → vector index →
    /// candidate policy → evidence builder. (Future embedding models such as
    /// BGE-small will enter here; this gate only establishes the composition.)
    /// </summary>
    public FastSemanticContainerIdentityProvider(
        IEmbeddingProvider embedding,
        IVectorSemanticIndex vectorIndex,
        IContainerIdentityPrototypeStore prototypes,
        IContainerIdentityCandidatePolicy? candidatePolicy = null,
        IContainerIdentityEvidenceBuilder? evidenceBuilder = null,
        IContainerSemanticFeatureExtractor? extractor = null,
        string source = "FAST")
        : this(
            extractor ?? new FastSemanticFeatureExtractor(),
            prototypes,
            matcher: null,
            embedding,
            vectorIndex,
            candidatePolicy ?? CandidatePolicies.LegacyReference(),
            evidenceBuilder ?? new ContainerIdentityEvidenceBuilder(),
            source)
    {
    }

    private FastSemanticContainerIdentityProvider(
        IContainerSemanticFeatureExtractor extractor,
        IContainerIdentityPrototypeStore prototypes,
        DeterministicSemanticMatcher? matcher,
        IEmbeddingProvider? embedding,
        IVectorSemanticIndex? vectorIndex,
        IContainerIdentityCandidatePolicy candidatePolicy,
        IContainerIdentityEvidenceBuilder evidenceBuilder,
        string source)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(prototypes);
        ArgumentNullException.ThrowIfNull(candidatePolicy);
        ArgumentNullException.ThrowIfNull(evidenceBuilder);
        if (matcher is null && (embedding is null || vectorIndex is null))
        {
            throw new ArgumentException("A retrieval path is required: deterministic matcher or embedding+vector index.");
        }

        _extractor = extractor;
        _prototypes = prototypes;
        _matcher = matcher;
        _embedding = embedding;
        _vectorIndex = vectorIndex;
        _policy = candidatePolicy;
        _evidence = evidenceBuilder;
        _source = source;
    }

    /// <inheritdoc />
    public Task<ImmutableArray<SemanticEvidence>> ResolveAsync(
        ObservationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var query = _extractor.Extract(context.CurrentObservation);
            var candidates = Retrieve(query);
            var prototypesById = _prototypes.All().ToDictionary(p => p.PrototypeId, p => p);

            var policyContext = new CandidateEvaluationContext(
                RankedCandidates: candidates,
                PrototypesById: prototypesById,
                PreviousVerifiedIdentity: context.PreviousVerifiedIdentity,
                ObservedElementTypes: query.ElementTypes,
                ObservedTextTokenCount: query.TextFragments.Length,
                HasAnyEvidence: query.TextFragments.Length > 0
                                || query.ElementTypes.Length > 0
                                || query.StructuralFeatures.Length > 0,
                ObservedTextFragments: query.TextFragments,
                ObservedStructuralFeatures: query.StructuralFeatures,
                ObservedElementCount: query.VisibleElements.Length);

            var policyResult = _policy.Decide(policyContext);
            if (policyResult.IsAbstain || policyResult.AcceptedCandidate is null)
            {
                // ABSTAIN is a normal success path: no evidence, Runtime unchanged.
                return Task.FromResult(ImmutableArray<SemanticEvidence>.Empty);
            }

            var evidence = _evidence.Build(
                policyResult.AcceptedCandidate,
                context.CurrentObservation,
                _source);
            return Task.FromResult(ImmutableArray.Create(evidence));
        }
        catch
        {
            // Layer failure is safe: return empty evidence and let Runtime
            // continue on the original fail-closed path (unchanged from legacy).
            return Task.FromResult(ImmutableArray<SemanticEvidence>.Empty);
        }
    }

    private IReadOnlyList<SemanticCandidate> Retrieve(ContainerSemanticQuery query)
    {
        if (_matcher is not null)
        {
            return _matcher.Match(query, _prototypes);
        }

        return _vectorIndex!.Retrieve(_embedding!.Embed(query));
    }
}