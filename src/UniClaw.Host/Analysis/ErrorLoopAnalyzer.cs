using UniClaw.Core.Observability;

namespace UniClaw.Host.Analysis;

/// <summary>
/// ErrorLoopAnalyzer — detects error-loop conditions from the span tree and returns
/// termination verdicts via ICompletionAnalyzer (trace-span-observability tasks 5.3/5.4).
///
/// Rules:
///   1. stuck_in_error_loop (confidence 0.9): 5+ consecutive engine.step spans whose
///      children are ALL entry.skipped with NO entry.visited among them.
///   2. skip_rate_too_high (confidence 0.7): entry.skipped count exceeds
///      entry.visited count × 4. No page-transition spans exist yet and TraceContext
///      carries no PageId, so all steps are treated as a single page.
///
/// Cold-start (5.4): this analyzer does NOT depend on a baseline profile — it always
/// operates, so cold-start has no effect on it.
///
/// On a terminating verdict an "analyze.error_loop" span is recorded (unless no
/// ITraceRecorder was provided); normal runs write no span. EvaluateAsync never throws —
/// exceptions are swallowed and reported as "no signal" (null) so a faulty analyzer
/// cannot break the CompletionMonitor poll loop.
/// </summary>
public sealed class ErrorLoopAnalyzer : ICompletionAnalyzer
{
    /// <summary>Consecutive all-skipped engine.step spans required to declare stuck_in_error_loop.</summary>
    public const int StuckThreshold = 5;

    /// <summary>skip_rate_too_high fires when skipped &gt; visited × this multiplier.</summary>
    public const int SkipRateMultiplier = 4;

    private const string StuckInErrorLoop = "stuck_in_error_loop";
    private const string SkipRateTooHigh = "skip_rate_too_high";
    private const double StuckConfidence = 0.9;
    private const double SkipRateConfidence = 0.7;

    private readonly ITraceRecorder? _traceRecorder;

    /// <summary>
    /// Create the analyzer. traceRecorder is optional — when null, verdicts are still
    /// returned but no analyze.error_loop span is emitted.
    /// </summary>
    /// <param name="traceRecorder">Optional recorder for analyze.error_loop span emission.</param>
    public ErrorLoopAnalyzer(ITraceRecorder? traceRecorder)
    {
        _traceRecorder = traceRecorder;
    }

    /// <inheritdoc/>
    public async Task<CompletionVerdict?> EvaluateAsync(
        ITraceQuery trace,
        CancellationToken ct = default)
    {
        try
        {
            var steps = trace.GetSpansByType(SpanTypes.EngineStep);
            var visited = trace.GetSpansByType(SpanTypes.EntryVisited);
            var skipped = trace.GetSpansByType(SpanTypes.EntrySkipped);

            // Rule 1 — stuck_in_error_loop: 5+ consecutive all-skipped steps.
            var consecutive = LongestConsecutiveAllSkippedRun(trace, steps);
            if (consecutive >= StuckThreshold)
            {
                var verdict = CompletionVerdict.ErrorLoop(StuckInErrorLoop, StuckConfidence);
                await EmitErrorLoopSpanAsync(verdict, new Dictionary<string, object>
                {
                    [TraceFields.ErrorReason] = verdict.Reason,
                    [TraceFields.ErrorConsecutiveSteps] = consecutive,
                }, ct);
                return verdict;
            }

            // Rule 2 — skip_rate_too_high: skipped > visited × 4 (all steps = one page).
            if (skipped.Count > visited.Count * SkipRateMultiplier)
            {
                var verdict = CompletionVerdict.ErrorLoop(SkipRateTooHigh, SkipRateConfidence);
                await EmitErrorLoopSpanAsync(verdict, new Dictionary<string, object>
                {
                    [TraceFields.ErrorReason] = verdict.Reason,
                    [TraceFields.ErrorSkipped] = skipped.Count,
                    [TraceFields.ErrorVisited] = visited.Count,
                }, ct);
                return verdict;
            }

            // Normal run — no signal; no analyze.error_loop span is written.
            return CompletionVerdict.Observe("no error loop detected");
        }
        catch (Exception)
        {
            // Analyze must never throw — a faulty analyzer reports "no signal".
            return null;
        }
    }

    /// <summary>
    /// Length of the longest run of consecutive engine.step spans (in insertion order)
    /// whose children are ALL entry.skipped with no entry.visited among them.
    /// Steps with no children do not qualify.
    /// </summary>
    private static int LongestConsecutiveAllSkippedRun(
        ITraceQuery trace,
        IReadOnlyList<TraceSpan> steps)
    {
        var longest = 0;
        var current = 0;

        foreach (var step in steps)
        {
            var children = trace.GetChildSpans(step.SpanId);
            var allChildrenSkipped = children.Count > 0
                && children.All(c => c.SpanType == SpanTypes.EntrySkipped);

            current = allChildrenSkipped ? current + 1 : 0;
            if (current > longest)
                longest = current;
        }

        return longest;
    }

    /// <summary>
    /// Record the terminating verdict as an analyze.error_loop span (root span).
    /// No-op when no ITraceRecorder was provided.
    /// </summary>
    private async Task EmitErrorLoopSpanAsync(
        CompletionVerdict verdict,
        Dictionary<string, object> attributes,
        CancellationToken ct)
    {
        await using var scope = await _traceRecorder.BeginSpanAsync(
            SpanTypes.AnalyzeErrorLoop,
            $"error loop: {verdict.Reason}",
            attributes: attributes,
            // trace-parent-linkage M2: ErrorLoop profile（Basic: reason；Extended: consecutive_steps/skipped/visited）。
            // 无 EntryConfig 注入，level 保持缺省 Detailed（= 现状全量行为）。
            profile: TraceSpanFields.ErrorLoop,
            ct: ct);
        await scope.End("ok", attributes, ct);
    }
}
