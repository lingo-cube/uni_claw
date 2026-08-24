namespace UniClaw.Semantic.Infrastructure.Evaluation;

/// <summary>Retrieval accuracy metrics.</summary>
public sealed record RetrievalAccuracyMetrics(
    double Top1Accuracy,
    double Top3Recall,
    double Top5Recall,
    double TopKRecall);

/// <summary>Safety metrics for recovery behavior.</summary>
public sealed record SafetyMetrics(double FalseRecoveryRate, double FalsePositiveRate);

/// <summary>Confidence calibration metrics.</summary>
public sealed record ConfidenceMetrics(double CalibrationError, double MeanConfidence, double Accuracy);

/// <summary>Performance latency metrics in milliseconds.</summary>
public sealed record PerformanceMetrics(double P50Ms, double P95Ms, double P99Ms, int SampleCount);

/// <summary>
/// Standard Semantic evaluation metrics: accuracy, safety, confidence, performance.
/// </summary>
public sealed record SemanticEvaluationMetrics(
    RetrievalAccuracyMetrics Retrieval,
    SafetyMetrics Safety,
    ConfidenceMetrics Confidence,
    PerformanceMetrics Performance);