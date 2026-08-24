using System.Collections.Immutable;

namespace UniClaw.Semantic.Infrastructure.Corpus;

/// <summary>Result of Semantic corpus validation.</summary>
public sealed record SemanticCorpusValidationResult(bool IsValid, ImmutableArray<string> Errors);

/// <summary>
/// Validates Semantic corpus shape before benchmark. Metadata is for dataset
/// management / benchmark analysis only and never enters Runtime decisions.
/// </summary>
public static class SemanticCorpusValidator
{
    /// <summary>Validates a corpus and returns any structural errors.</summary>
    public static SemanticCorpusValidationResult Validate(SemanticCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var errors = ImmutableArray.CreateBuilder<string>();

        if (string.IsNullOrWhiteSpace(corpus.CorpusId))
        {
            errors.Add("CorpusId must not be empty.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in corpus.Cases)
        {
            if (!ids.Add(testCase.CaseId))
            {
                errors.Add($"Duplicate CaseId '{testCase.CaseId}'.");
            }

            if (string.IsNullOrWhiteSpace(testCase.ExpectedCandidate))
            {
                errors.Add($"Case '{testCase.CaseId}' has empty ExpectedCandidate.");
            }

            if (testCase.ExpectedCandidate != "None" && string.IsNullOrWhiteSpace(testCase.ExpectedIdentity))
            {
                errors.Add($"Case '{testCase.CaseId}' requires ExpectedIdentity.");
            }

            if (testCase.InputObservation.SequenceNumber < 0)
            {
                errors.Add($"Case '{testCase.CaseId}' has invalid ObservationSequence.");
            }

            if (testCase.ViewportState == SemanticViewportState.Unknown)
            {
                errors.Add($"Case '{testCase.CaseId}' is missing ViewportState.");
            }

            if (testCase.VisibleAnchorState == SemanticVisibleAnchorState.Unknown)
            {
                errors.Add($"Case '{testCase.CaseId}' is missing VisibleAnchorState.");
            }

            if (testCase.NoiseLevel < 0)
            {
                errors.Add($"Case '{testCase.CaseId}' has invalid NoiseLevel.");
            }

            if (testCase.AmbiguityLevel < 0)
            {
                errors.Add($"Case '{testCase.CaseId}' has invalid AmbiguityLevel.");
            }

            if (testCase.ScrollPosition < 0)
            {
                errors.Add($"Case '{testCase.CaseId}' has invalid ScrollPosition.");
            }
        }

        return new SemanticCorpusValidationResult(errors.Count == 0, errors.ToImmutable());
    }
}