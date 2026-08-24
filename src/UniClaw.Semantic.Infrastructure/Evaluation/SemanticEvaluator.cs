using System.Diagnostics;
using UniClaw.Runtime.Capabilities.Perception.Semantic;
using UniClaw.Semantic.Infrastructure.Configuration;
using UniClaw.Semantic.Infrastructure.Corpus;

namespace UniClaw.Semantic.Infrastructure.Evaluation;

/// <summary>
/// Default Semantic evaluator skeleton. Computes Retrieval Accuracy, Safety,
/// Confidence Calibration, and Performance latency from a provider + corpus.
/// </summary>
public sealed class SemanticEvaluator : ISemanticEvaluator
{
    /// <inheritdoc />
    public async Task<SemanticEvaluationMetrics> EvaluateAsync(
        SemanticEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cases = context.Corpus.Cases;
        var total = cases.Length;
        var top1Hits = 0;
        var top3Hits = 0;
        var top5Hits = 0;
        var topKHits = 0;
        var negativeTotal = 0;
        var falseRecovery = 0;
        var falsePositive = 0;
        var confidenceSum = 0d;
        var latencies = new List<double>();

        var topK = Math.Max(1, context.Options.Evaluation.TopK);
        var threshold = context.Options.Evaluation.RecoveryConfidenceThreshold;
        var measurementRuns = Math.Max(1, context.Options.Benchmark.MeasurementRuns);

        foreach (var testCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contextForCase = new ObservationContext(
                testCase.InputObservation,
                testCase.PreviousVerifiedIdentity);

            SemanticEvidence[]? ranked = null;

            for (var run = 0; run < measurementRuns; run++)
            {
                var sw = Stopwatch.StartNew();
                var evidence = await context.Provider.ResolveAsync(contextForCase, cancellationToken);
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);

                if (run == 0)
                {
                    ranked = evidence.OrderByDescending(e => e.Confidence).ToArray();
                }
            }

            var top = ranked?.FirstOrDefault();
            var topCandidate = top?.Candidate ?? "None";
            var topConfidence = top?.Confidence ?? 0d;

            var expectedNone = testCase.ExpectedCandidate == "None";
            var hit = expectedNone
                ? top is null
                : top is not null && topCandidate == testCase.ExpectedCandidate;

            if (hit)
            {
                top1Hits++;
            }

            var hitK = expectedNone
                ? top is null
                : ranked is not null && ranked.Take(topK).Any(e => e.Candidate == testCase.ExpectedCandidate);

            if (hitK)
            {
                topKHits++;
            }

            var hit3 = expectedNone
                ? top is null
                : ranked is not null && ranked.Take(3).Any(e => e.Candidate == testCase.ExpectedCandidate);

            if (hit3)
            {
                top3Hits++;
            }

            var hit5 = expectedNone
                ? top is null
                : ranked is not null && ranked.Take(5).Any(e => e.Candidate == testCase.ExpectedCandidate);

            if (hit5)
            {
                top5Hits++;
            }

            if (testCase.ExpectedIdentity is null)
            {
                negativeTotal++;
                if (top is not null)
                {
                    falsePositive++;
                }

                if (top is not null && topConfidence >= threshold)
                {
                    falseRecovery++;
                }
            }

            confidenceSum += topConfidence;
        }

        var top1Accuracy = total == 0 ? 0d : (double)top1Hits / total;
        var top3Recall = total == 0 ? 0d : (double)top3Hits / total;
        var top5Recall = total == 0 ? 0d : (double)top5Hits / total;
        var topKRecall = total == 0 ? 0d : (double)topKHits / total;
        var falseRecoveryRate = negativeTotal == 0 ? 0d : (double)falseRecovery / negativeTotal;
        var falsePositiveRate = negativeTotal == 0 ? 0d : (double)falsePositive / negativeTotal;
        var accuracy = top1Accuracy;
        var meanConfidence = total == 0 ? 0d : confidenceSum / total;
        var calibrationError = Math.Abs(meanConfidence - accuracy);
        var performance = Percentiles(latencies);

        return new SemanticEvaluationMetrics(
            new RetrievalAccuracyMetrics(top1Accuracy, top3Recall, top5Recall, topKRecall),
            new SafetyMetrics(falseRecoveryRate, falsePositiveRate),
            new ConfidenceMetrics(calibrationError, meanConfidence, accuracy),
            new PerformanceMetrics(performance.p50, performance.p95, performance.p99, latencies.Count));
    }

    private static (double p50, double p95, double p99) Percentiles(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return (0d, 0d, 0d);
        }

        var ordered = samples.OrderBy(x => x).ToArray();
        return (Percentile(ordered, 0.50), Percentile(ordered, 0.95), Percentile(ordered, 0.99));
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 0)
        {
            return 0d;
        }

        if (ordered.Count == 1)
        {
            return ordered[0];
        }

        var index = (ordered.Count - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var weight = index - lower;
        return ordered[lower] * (1 - weight) + ordered[upper] * weight;
    }
}
