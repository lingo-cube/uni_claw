using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Capabilities.Perception.Semantic.Fusion;

/// <summary>
/// Default minimal Runtime Evidence Fusion implementation. Runs the validation
/// pipeline: Freshness, Scope, Reference, Compatibility, then outputs accepted
/// evidence and confidence weights. It performs NO complex reasoning and NEVER
/// converts confidence into Truth (falsifier F4). It does NOT create Fact.
/// </summary>
public sealed class SemanticEvidenceFusion : ISemanticEvidenceFusion
{
    /// <inheritdoc />
    public ValidatedSemanticEvidenceResult Fuse(SemanticEvidenceFusionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var accepted = ImmutableArray.CreateBuilder<SemanticEvidence>();
        var rejected = ImmutableArray.CreateBuilder<SemanticEvidence>();
        var reasons = ImmutableArray.CreateBuilder<SemanticEvidenceRejection>();
        var weights = ImmutableArray.CreateBuilder<SemanticEvidenceWeight>();

        foreach (var evidence in input.SemanticEvidence)
        {
            var rejection = Validate(evidence, input);
            if (rejection is not null)
            {
                rejected.Add(evidence);
                reasons.Add(rejection);
                continue;
            }

            accepted.Add(evidence);
            weights.Add(new SemanticEvidenceWeight(evidence.EvidenceId, evidence.Confidence));
        }

        return new ValidatedSemanticEvidenceResult(
            accepted.ToImmutable(),
            rejected.ToImmutable(),
            reasons.ToImmutable(),
            weights.ToImmutable());
    }

    private static SemanticEvidenceRejection? Validate(
        SemanticEvidence evidence,
        SemanticEvidenceFusionInput input)
    {
        var freshness = ValidateFreshness(evidence, input);
        if (freshness is not null) return freshness;

        if (evidence.Scope is not (SemanticEvidenceScope.CurrentObservation
                                   or SemanticEvidenceScope.CurrentContainer
                                   or SemanticEvidenceScope.HistoricalContext))
        {
            return new SemanticEvidenceRejection(
                evidence.EvidenceId, SemanticEvidenceRejectionReason.InvalidScope,
                $"observed scope '{evidence.Scope}'");
        }

        var reference = ValidateReferences(evidence, input);
        if (reference is not null) return reference;

        if (string.IsNullOrWhiteSpace(evidence.Candidate))
        {
            return new SemanticEvidenceRejection(
                evidence.EvidenceId, SemanticEvidenceRejectionReason.Incompatible,
                "empty candidate");
        }

        return null;
    }

    private static SemanticEvidenceRejection? ValidateFreshness(
        SemanticEvidence evidence,
        SemanticEvidenceFusionInput input)
    {
        if (evidence.Scope == SemanticEvidenceScope.CurrentObservation
            && evidence.ObservationSequence != input.CurrentObservation.SequenceNumber)
        {
            return new SemanticEvidenceRejection(
                evidence.EvidenceId, SemanticEvidenceRejectionReason.StaleObservationSequence,
                $"evidence sequence {evidence.ObservationSequence} != current {input.CurrentObservation.SequenceNumber}");
        }

        if (evidence.Scope is SemanticEvidenceScope.CurrentContainer
                or SemanticEvidenceScope.HistoricalContext
            && !input.KnownObservationSequences.Contains(evidence.ObservationSequence))
        {
            return new SemanticEvidenceRejection(
                evidence.EvidenceId, SemanticEvidenceRejectionReason.StaleObservationSequence,
                $"evidence sequence {evidence.ObservationSequence} not in known observation sequences");
        }

        if (evidence.ValidUntil is { } validUntil && validUntil < DateTimeOffset.UtcNow)
        {
            return new SemanticEvidenceRejection(
                evidence.EvidenceId, SemanticEvidenceRejectionReason.StaleExpired,
                $"validUntil {validUntil:O} passed");
        }

        return null;
    }

    private static SemanticEvidenceRejection? ValidateReferences(
        SemanticEvidence evidence,
        SemanticEvidenceFusionInput input)
    {
        foreach (var reference in evidence.References)
        {
            switch (reference.Kind)
            {
                case "Observation":
                    if (!input.KnownObservationSequences.Any(seq => string.Equals(
                            seq.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            reference.ReferenceId, StringComparison.Ordinal)))
                    {
                        return new SemanticEvidenceRejection(
                            evidence.EvidenceId, SemanticEvidenceRejectionReason.MissingReference,
                            $"Observation reference '{reference.ReferenceId}' not found");
                    }
                    break;

                case "Trace":
                    if (!input.KnownTraceIds.Contains(reference.ReferenceId, StringComparer.Ordinal))
                    {
                        return new SemanticEvidenceRejection(
                            evidence.EvidenceId, SemanticEvidenceRejectionReason.MissingReference,
                            $"Trace reference '{reference.ReferenceId}' not found");
                    }
                    break;

                case "Fact":
                    return new SemanticEvidenceRejection(
                        evidence.EvidenceId, SemanticEvidenceRejectionReason.UnsupportedReferenceKind,
                        "Fact references are reserved; Semantic cannot create Fact");

                default:
                    return new SemanticEvidenceRejection(
                        evidence.EvidenceId, SemanticEvidenceRejectionReason.UnsupportedReferenceKind,
                        $"unsupported reference kind '{reference.Kind}'");
            }
        }

        return null;
    }
}
