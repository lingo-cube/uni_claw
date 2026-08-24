using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;

namespace UniClaw.Semantic.Infrastructure.Evaluation;

/// <summary>Inputs to a Semantic evaluation run.</summary>
public sealed record SemanticEvaluationContext(
    ISemanticProvider Provider,
    SemanticCorpus Corpus,
    SemanticOptions Options);

/// <summary>
/// Semantic evaluation port. Supports Retrieval Accuracy, Safety, Confidence,
/// and Performance evaluation.
/// </summary>
public interface ISemanticEvaluator
{
    /// <summary>Evaluates a provider against a corpus.</summary>
    Task<SemanticEvaluationMetrics> EvaluateAsync(
        SemanticEvaluationContext context,
        CancellationToken cancellationToken = default);
}
