using System.Linq;
using System.Text.Json;
using UniClaw.Core.Observability;

namespace UniClaw.Host.Analysis;

/// <summary>
/// EnumerateCompletionAnalyzer — evaluates enumeration progress from the run's span
/// tree and returns a termination verdict via <see cref="ICompletionAnalyzer"/>
/// (trace-span-observability tasks 5.2 / 5.4).
///
/// Progress is derived purely from span counts: <c>observed</c> / <c>visited</c> /
/// <c>skipped</c> come from <c>entry.observed</c> / <c>entry.visited</c> /
/// <c>entry.skipped</c> spans, and <c>pending = observed - visited - skipped</c>
/// (clamped to ≥ 0). End-of-list is detected from the span tree the same way
/// <see cref="BaselineBuilder"/> does (an explicit <c>end_of_list</c> flag on an
/// <c>engine.step</c>, or an <c>entry.generate</c> child under a step whose
/// generation produced zero <c>entry.observed</c>).
///
/// Rules (first match wins, per design §7.4.2):
///   1. Halt      (conf 1.0): pending ≤ 0 AND end-of-list reached.
///   2. Warn      (conf 0.95): visited ≥ p95 × 1.5 (fires regardless of end-of-list).
///   3. Terminate (conf 0.9): visited ≥ p95 AND end-of-list reached.
///   4. Recommend (conf 0.7): visited ≥ p50 AND end-of-list reached.
///   5. Observe   (conf 0.0): otherwise.
///
/// Thresholds come from the scenario's <see cref="BaselineProfile"/> when it is ready
/// (≥ 10 records); otherwise hardcoded defaults (DefaultP50 / DefaultP95) apply.
///
/// Cold-start (5.4): when the baseline is NOT ready, only Halt and Warn fire;
/// Terminate and Recommend are suppressed (downgraded to Observe) so a new scenario
/// does not under-stop. Halt still terminates even in cold-start — by design.
///
/// Every evaluation writes an <c>analyze.completion</c> span (unless no ITraceRecorder
/// was provided) so data accumulates toward the first usable baseline. An observed
/// count exceeding p95 × 2 is flagged abnormal (<c>analyze.abnormal_spike</c>) on the
/// span but never terminates on that basis alone (6.3). EvaluateAsync never throws —
/// exceptions are swallowed and reported as "no signal" (null) so a faulty analyzer
/// cannot break the CompletionMonitor poll loop.
/// </summary>
public sealed class EnumerateCompletionAnalyzer : ICompletionAnalyzer
{
    /// <summary>Hardcoded p50 used when the baseline is not ready (cold-start or none).</summary>
    private const double DefaultP50 = 14.0;

    /// <summary>Hardcoded p95 used when the baseline is not ready (cold-start or none).</summary>
    private const double DefaultP95 = 21.0;

    /// <summary>visited ≥ p95 × this multiplier fires the Warn rule.</summary>
    private const double WarnMultiplier = 1.5;

    /// <summary>observed &gt; p95 × this multiplier flags an abnormal spike (no termination).</summary>
    private const double AbnormalSpikeMultiplier = 2.0;

    private readonly ITraceRecorder? _traceRecorder;
    private readonly BaselineProfile? _baseline;

    /// <summary>
    /// Create the analyzer. Both dependencies are optional:
    /// <list type="bullet">
    ///   <item>traceRecorder — when null, verdicts are still returned but no
    ///        analyze.completion span is emitted.</item>
    ///   <item>baselineProfile — when null or not <see cref="BaselineProfile.IsReady"/>,
    ///        the analyzer operates in cold-start mode with hardcoded default thresholds
    ///        (Terminate/Recommend suppressed).</item>
    /// </list>
    /// </summary>
    /// <param name="traceRecorder">Optional recorder for analyze.completion span emission.</param>
    /// <param name="baselineProfile">Optional scenario baseline; null means cold-start.</param>
    public EnumerateCompletionAnalyzer(
        ITraceRecorder? traceRecorder = null,
        BaselineProfile? baselineProfile = null)
    {
        _traceRecorder = traceRecorder;
        _baseline = baselineProfile;
    }

    /// <inheritdoc/>
    public async Task<CompletionVerdict?> EvaluateAsync(
        ITraceQuery trace,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Count spans from the span tree ─────────────────────────
            var observed = trace.GetSpansByType(SpanTypes.EntryObserved).Count;
            var visited = trace.GetSpansByType(SpanTypes.EntryVisited).Count;
            var skipped = trace.GetSpansByType(SpanTypes.EntrySkipped).Count;
            var pending = Math.Max(0, observed - visited - skipped);

            // ── 2. End-of-list detection (same pattern as BaselineBuilder) ─
            var endReached = DetectEndOfList(trace);

            // ── 3. Thresholds: baseline when ready, defaults otherwise ─────
            var isColdStart = _baseline is null || !_baseline.IsReady;
            var p50 = isColdStart ? DefaultP50 : _baseline!.ItemsVisitedP50;
            var p95 = isColdStart ? DefaultP95 : _baseline!.ItemsVisitedP95;

            // ── 4. Rules (priority order, first match wins) ────────────────
            CompletionVerdict verdict;

            // Rule 1 — Halt: nothing pending and end of list reached.
            if (pending <= 0 && endReached)
            {
                verdict = CompletionVerdict.Halt("pending=0 and end reached");
            }
            // Rule 2 — Warn: visited spike; fires regardless of end-of-list.
            else if (visited >= p95 * WarnMultiplier)
            {
                verdict = CompletionVerdict.Warn(
                    $"visited spike: {visited} >= {p95 * WarnMultiplier}");
            }
            // Rule 3 — Terminate: visited at/beyond p95 with end of list.
            else if (visited >= p95 && endReached)
            {
                var rule = CompletionVerdict.Terminate($"visited {visited} >= p95 {p95}");
                verdict = isColdStart ? SuppressColdStart(rule) : rule;
            }
            // Rule 4 — Recommend: visited at/beyond p50 with end of list.
            else if (visited >= p50 && endReached)
            {
                var rule = CompletionVerdict.Recommend($"visited {visited} >= p50 {p50}");
                verdict = isColdStart ? SuppressColdStart(rule) : rule;
            }
            // Rule 5 — Observe: keep going.
            else
            {
                verdict = CompletionVerdict.Observe($"pending={pending} endReached={endReached}");
            }

            // ── 5. Abnormal spike flag (6.3): observed > p95×2 is flagged, never terminating ─
            var abnormalSpike = observed > p95 * AbnormalSpikeMultiplier;

            // ── 6. Write the analyze.completion span for every evaluation ──
            if (_traceRecorder is not null)
            {
                var attributes = new Dictionary<string, object>
                {
                    ["analyze.observed"] = observed,
                    ["analyze.visited"] = visited,
                    ["analyze.skipped"] = skipped,
                    ["analyze.pending"] = pending,
                    ["analyze.end_reached"] = endReached,
                    ["analyze.p50"] = p50,
                    ["analyze.p95"] = p95,
                    ["analyze.cold_start"] = isColdStart,
                    ["analyze.rule"] = verdict.Reason,
                };
                if (abnormalSpike)
                    attributes["analyze.abnormal_spike"] = true;

                var spanId = await _traceRecorder.StartSpanAsync(
                    SpanTypes.AnalyzeCompletion,
                    "enumerate completion check",
                    parentSpanId: null,
                    attributes: attributes,
                    cancellationToken: ct);
                await _traceRecorder.EndSpanAsync(spanId, "ok", cancellationToken: ct);
            }

            return verdict;
        }
        catch (Exception)
        {
            // Analyze must never throw — a faulty analyzer reports "no signal".
            return null;
        }
    }

    /// <summary>
    /// Detect whether the run reached the end of the list — same logic as
    /// <see cref="BaselineBuilder"/>:
    /// 1. any engine.step span carries an explicit end_of_list flag (attribute key
    ///    "end_of_list" or its camelCase form "endOfList" set to true); or
    /// 2. an engine.step's child generation produced no entry.observed — an
    ///    entry.generate child under a step with zero entry.observed children means
    ///    that step found nothing new.
    /// </summary>
    private static bool DetectEndOfList(ITraceQuery trace)
    {
        foreach (var step in trace.GetSpansByType(SpanTypes.EngineStep))
        {
            if (step.Attributes is not null
                && (TryGetBool(step.Attributes, "end_of_list")
                    || TryGetBool(step.Attributes, "endOfList")))
            {
                return true;
            }

            foreach (var child in trace.GetChildSpans(step.SpanId))
            {
                if (child.SpanType != SpanTypes.EntryGenerate)
                    continue;
                var producedObserved = trace.GetChildSpans(child.SpanId)
                    .Any(grandchild => grandchild.SpanType == SpanTypes.EntryObserved);
                if (!producedObserved)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Cold-start suppression: a would-be Terminate/Recommend is downgraded to Observe
    /// (confidence 0.0) carrying the suppressed rule in the reason. Halt and Warn are
    /// never routed here.
    /// </summary>
    private static CompletionVerdict SuppressColdStart(CompletionVerdict verdict) =>
        CompletionVerdict.Observe($"cold-start: {verdict.Reason}");

    /// <summary>
    /// Read a boolean attribute, tolerating both the in-memory boxed bool and a
    /// JsonElement restored from a JSON round-trip (same helper as BaselineBuilder).
    /// </summary>
    private static bool TryGetBool(Dictionary<string, object> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value))
            return false;
        return value switch
        {
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            _ => false,
        };
    }
}
