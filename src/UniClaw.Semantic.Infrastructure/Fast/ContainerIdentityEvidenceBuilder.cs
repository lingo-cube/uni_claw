using UniClaw.Runtime.Model;
using SemanticEvidence = UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidence;

namespace UniClaw.Semantic.Infrastructure.Fast;

/// <summary>
/// Evidence Builder boundary: converts a POLICY-ACCEPTED candidate into
/// <see cref="SemanticEvidence"/>. It must never be reached from raw retrieval
/// top-1 — only from an accepted <see cref="CandidatePolicyResult"/>.
/// </summary>
public interface IContainerIdentityEvidenceBuilder
{
    /// <summary>Builds Container Identity evidence for an accepted candidate.</summary>
    SemanticEvidence Build(SemanticCandidate accepted, Observation observation, string source);
}

/// <summary>Default Container Identity evidence builder.</summary>
public sealed class ContainerIdentityEvidenceBuilder : IContainerIdentityEvidenceBuilder
{
    /// <inheritdoc />
    public SemanticEvidence Build(SemanticCandidate accepted, Observation observation, string source)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(observation);

        var sequence = observation.SequenceNumber;
        return new SemanticEvidence(
            evidenceId: $"fast-{accepted.PatternReference}-{sequence}",
            version: "1",
            source: source,
            kind: UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidenceKind.ContainerIdentity,
            candidate: accepted.IdentityCandidate,
            confidence: accepted.SimilarityScore,
            scope: UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidenceScope.CurrentContainer,
            observationSequence: sequence,
            createdAt: DateTimeOffset.UtcNow)
        {
            References = System.Collections.Immutable.ImmutableArray.Create(
                new UniClaw.Runtime.Capabilities.Perception.Semantic.SemanticEvidenceReference(
                    "Observation",
                    sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        };
    }
}