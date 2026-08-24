using System.Collections.Immutable;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Evaluation;

namespace UniClaw.Semantic.Infrastructure.Benchmark;

/// <summary>One case result in a Semantic benchmark report.</summary>
public sealed record SemanticCaseResult(
    string CaseId,
    string ExpectedCandidate,
    string ActualCandidate,
    double Confidence,
    bool IsMatch);

/// <summary>
/// Standard Semantic Benchmark Report. Contains provider id, corpus id, aggregate
/// metrics, and per-case results.
/// </summary>
public sealed record SemanticBenchmarkReport
{
    /// <summary>Provider identity string.</summary>
    public string Provider { get; }

    /// <summary>Corpus id.</summary>
    public string CorpusId { get; }

    /// <summary>Aggregate metrics.</summary>
    public SemanticEvaluationMetrics Metrics { get; }

    /// <summary>Options used for the run.</summary>
    public SemanticOptions Options { get; }

    /// <summary>Per-case results.</summary>
    public ImmutableArray<SemanticCaseResult> CaseResults { get; }

    /// <summary>Vector backend identifier used for the run (e.g. InMemory).</summary>
    public string Backend { get; init; } = "InMemory";

    /// <summary>Creates a benchmark report.</summary>
    public SemanticBenchmarkReport(
        string provider,
        string corpusId,
        SemanticEvaluationMetrics metrics,
        SemanticOptions options,
        ImmutableArray<SemanticCaseResult> caseResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusId);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        Provider = provider;
        CorpusId = corpusId;
        Metrics = metrics;
        Options = options;
        CaseResults = caseResults;
    }
}