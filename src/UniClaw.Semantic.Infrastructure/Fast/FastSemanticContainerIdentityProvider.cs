using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Fast Semantic Container Identity provider. It consumes only ObservationContext
/// (Current Observation, Visible Elements via Observation, Container History via
/// context, Previous Verified Identity), queries an <see cref="IVectorSemanticIndex"/>,
/// and returns <see cref="SemanticEvidence"/> of kind ContainerIdentity. It never
/// returns Fact / Belief / CurrentContainer / Action. On miss it returns empty
/// evidence so Runtime continues unchanged.
/// </summary>
public sealed class FastSemanticContainerIdentityProvider : ISemanticProvider
{
    private readonly IVectorSemanticIndex _index;
    private readonly string _source;

    /// <summary>Creates the provider with a read-only vector index.</summary>
    public FastSemanticContainerIdentityProvider(IVectorSemanticIndex index, string source = "FAST")
    {
        ArgumentNullException.ThrowIfNull(index);
        _index = index;
        _source = source;
    }

    /// <inheritdoc />
    public Task<ImmutableArray<SemanticEvidence>> ResolveAsync(
        ObservationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        var query = FastSemanticFeatureExtractor.Extract(context.CurrentObservation);
        SemanticCandidate? candidate;
        try
        {
            candidate = _index.Retrieve(query);
        }
        catch
        {
            // Vector backend failure is safe: return empty evidence and let
            // Runtime continue on the original fail-closed path.
            return Task.FromResult(ImmutableArray<SemanticEvidence>.Empty);
        }

        if (candidate is null)
        {
            return Task.FromResult(ImmutableArray<SemanticEvidence>.Empty);
        }

        var sequence = context.CurrentObservation.SequenceNumber;
        var evidence = new SemanticEvidence(
            evidenceId: $"fast-{candidate.PatternReference}-{sequence}",
            version: "1",
            source: _source,
            kind: SemanticEvidenceKind.ContainerIdentity,
            candidate: candidate.IdentityCandidate,
            confidence: candidate.SimilarityScore,
            scope: SemanticEvidenceScope.CurrentContainer,
            observationSequence: sequence,
            createdAt: DateTimeOffset.UtcNow)
        {
            References = ImmutableArray.Create(
                new SemanticEvidenceReference(
                    "Observation",
                    sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        };

        return Task.FromResult(ImmutableArray.Create(evidence));
    }
}
