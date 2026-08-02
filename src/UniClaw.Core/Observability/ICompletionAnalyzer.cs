namespace UniClaw.Core.Observability;

/// <summary>
/// ICompletionAnalyzer — real-time run-completion analysis contract.
/// Implementations read ITraceQuery (span tree) and return a CompletionVerdict
/// indicating whether the run should terminate. The contract lives in Core so
/// CompletionMonitor composition can vary; implementations live in Host.
///
/// Analysers SHALL read only ITraceQuery and SHALL NOT depend on engine internals
/// or the engine instance. A null verdict means "no signal / continue observing".
/// ShouldTerminate == false does NOT by itself stop the engine — only the
/// CompletionMonitor acts on verdicts.
/// </summary>
public interface ICompletionAnalyzer
{
    /// <summary>
    /// Evaluate the current span tree and return a verdict, or null for "no signal".
    /// Called by CompletionMonitor on each poll tick.
    /// </summary>
    Task<CompletionVerdict?> EvaluateAsync(
        ITraceQuery trace,
        CancellationToken ct = default);
}

/// <summary>
/// CompletionVerdict — analyzer output carrying a termination recommendation,
/// a human-readable reason, and a confidence score clamped to 0.0–1.0.
/// </summary>
/// <param name="ShouldTerminate">Whether the CompletionMonitor should cancel the engine.</param>
/// <param name="Reason">Human-readable classification (e.g. "halt", "terminate", "recommend", "warn", "observe").</param>
/// <param name="Confidence">Confidence score 0.0–1.0. Clamped on construction.</param>
public sealed record class CompletionVerdict(
    bool ShouldTerminate,
    string Reason,
    double Confidence)
{
    /// <summary>Clamped confidence to [0.0, 1.0].</summary>
    public double Confidence { get; } = double.IsFinite(Confidence)
        ? Math.Clamp(Confidence, 0.0, 1.0)
        : 0.0;

    /// <summary>Deconstruct for pattern matching.</summary>
    public void Deconstruct(out bool shouldTerminate, out string reason, out double confidence)
    {
        shouldTerminate = ShouldTerminate;
        reason = Reason;
        confidence = Confidence;
    }

    // ── Factory helpers for the standard verdicts ──────────────────────

    public static CompletionVerdict Halt(string detail = "") =>
        new(true, string.IsNullOrEmpty(detail) ? "halt" : $"halt: {detail}", 1.0);

    public static CompletionVerdict Terminate(string detail = "") =>
        new(true, string.IsNullOrEmpty(detail) ? "terminate" : $"terminate: {detail}", 0.9);

    public static CompletionVerdict Recommend(string detail = "") =>
        new(false, string.IsNullOrEmpty(detail) ? "recommend" : $"recommend: {detail}", 0.7);

    public static CompletionVerdict Warn(string detail = "") =>
        new(false, string.IsNullOrEmpty(detail) ? "warn" : $"warn: {detail}", 0.95);

    public static CompletionVerdict Observe(string detail = "") =>
        new(false, string.IsNullOrEmpty(detail) ? "observe" : $"observe: {detail}", 0.0);

    public static CompletionVerdict ErrorLoop(string reason, double confidence) =>
        new(true, reason, confidence);
}
