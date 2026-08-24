using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;
using UniClaw.Semantic.Infrastructure.Evaluation;

namespace UniClaw.Semantic.Infrastructure.Benchmark;

/// <summary>
/// Unified Semantic benchmark runner. Accepts Provider + Corpus + Configuration
/// and produces a standard <see cref="SemanticBenchmarkReport"/>.
/// </summary>
public sealed class SemanticBenchmarkRunner
{
    private readonly ISemanticEvaluator _evaluator;

    /// <summary>Creates the benchmark runner with the default evaluator.</summary>
    public SemanticBenchmarkRunner(ISemanticEvaluator? evaluator = null)
    {
        _evaluator = evaluator ?? new SemanticEvaluator();
    }

    /// <summary>Runs a semantic benchmark.</summary>
    public async Task<SemanticBenchmarkReport> RunAsync(
        ISemanticProvider provider,
        SemanticCorpus corpus,
        SemanticOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(options);

        var metrics = await _evaluator.EvaluateAsync(
            new SemanticEvaluationContext(provider, corpus, options),
            cancellationToken);

        var caseResults = ImmutableArray.CreateBuilder<SemanticCaseResult>();
        foreach (var testCase in corpus.Cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evidence = await provider.ResolveAsync(
                new ObservationContext(testCase.InputObservation, testCase.PreviousVerifiedIdentity),
                cancellationToken);
            var top = evidence.OrderByDescending(e => e.Confidence).FirstOrDefault();
            var actual = top?.Candidate ?? "None";
            caseResults.Add(new SemanticCaseResult(
                testCase.CaseId,
                testCase.ExpectedCandidate,
                actual,
                top?.Confidence ?? 0d,
                string.Equals(actual, testCase.ExpectedCandidate, StringComparison.Ordinal)));
        }

        return new SemanticBenchmarkReport(
            provider.GetType().Name,
            corpus.CorpusId,
            metrics,
            options,
            caseResults.ToImmutable())
        {
            Backend = options.VectorBackend,
        };
    }
}
