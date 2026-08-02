using System.Text;
using System.Text.Json;
using UniClaw.Core.Observability;

namespace UniClaw.Host.Analysis;

/// <summary>
/// BaselineBuilder — offline per-run aggregate extraction (trace-span-observability D6).
/// After a run completes, reads the run's spans through <see cref="ITraceQuery"/>, computes
/// the nine aggregate fields, and appends one JSON line to
/// <c>artifacts/baselines/&lt;scenarioId&gt;.jsonl</c>.
/// The file is append-only JSONL — one standalone JSON object per historical run, readable
/// by standard text tooling and diffable across runs; existing lines are never rewritten.
/// A run whose span tree cannot be aggregated (no <c>engine.step</c> spans) is skipped with
/// a logged warning and never corrupts the file (spec: 空 run 不写损坏行).
/// </summary>
public sealed class BaselineBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ITraceQuery _trace;
    private readonly string _baselinesRoot;

    /// <summary>
    /// Create a baseline builder over the run's span query surface.
    /// </summary>
    /// <param name="trace">The run's span query surface (InMemoryTraceService implements ITraceQuery).</param>
    /// <param name="artifactsRoot">Root of the artifacts directory (e.g. "artifacts"); baselines live under <c>baselines/</c>.</param>
    public BaselineBuilder(ITraceQuery trace, string artifactsRoot = "artifacts")
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsRoot);
        _baselinesRoot = Path.Combine(artifactsRoot, "baselines");
    }

    /// <summary>
    /// Extract per-run aggregates from the run's spans and append one JSON line to the
    /// scenario's baseline file (<c>baselines/&lt;scenarioId&gt;.jsonl</c>).
    /// When the span tree cannot be aggregated (no <c>engine.step</c> spans), a warning is
    /// logged and no line is appended.
    /// </summary>
    /// <param name="scenarioId">Scenario identifier; must be a single safe path segment.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task AppendRunAsync(string scenarioId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        if (!IsSafeSegment(scenarioId))
        {
            throw new ArgumentException(
                $"Scenario id must be one safe path segment. Got: '{scenarioId}'.",
                nameof(scenarioId));
        }

        var aggregate = ComputeAggregates();
        if (aggregate is null)
        {
            Console.Error.WriteLine(
                $"[BaselineBuilder] Skipping run for scenario '{scenarioId}': " +
                "no engine.step spans to aggregate.");
            return;
        }

        Directory.CreateDirectory(_baselinesRoot);
        var line = JsonSerializer.Serialize(aggregate, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(
            Path.Combine(_baselinesRoot, $"{scenarioId}.jsonl"),
            line,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            ct);
    }

    /// <summary>
    /// The per-run aggregate record — exactly the nine spec fields, serialized camelCase
    /// (itemsObserved, itemsVisited, itemsSkipped, stepsUsed, scrollCount, endOfListDetected,
    /// success, aiLatencyP50, aiLatencyP95).
    /// </summary>
    private sealed record BaselineRunAggregate(
        int ItemsObserved,
        int ItemsVisited,
        int ItemsSkipped,
        int StepsUsed,
        int ScrollCount,
        bool EndOfListDetected,
        bool Success,
        double AiLatencyP50,
        double AiLatencyP95);

    /// <summary>
    /// Compute the nine aggregate fields from the run's spans.
    /// Returns null when the run cannot be aggregated (no engine.step spans).
    /// </summary>
    private BaselineRunAggregate? ComputeAggregates()
    {
        var stepSpans = _trace.GetSpansByType(SpanTypes.EngineStep);
        if (stepSpans.Count == 0)
            return null;

        var endOfListDetected = DetectEndOfList(stepSpans);

        var runSpan = _trace.GetRootSpan();
        var success = runSpan is not null
            && runSpan.SpanType == SpanTypes.EngineRun
            && string.Equals(runSpan.Status, "ok", StringComparison.Ordinal);

        // Single run: aiLatencyP50 and aiLatencyP95 are both the mean ai.call duration.
        // Cross-run p50/p95 percentiles are computed by BaselineProfile.Load().
        var aiCalls = _trace.GetSpansByType(SpanTypes.AiCall);
        var aiLatency = aiCalls.Count == 0
            ? 0.0
            : aiCalls.Average(span => span.DurationMs);

        return new BaselineRunAggregate(
            _trace.GetSpansByType(SpanTypes.EntryObserved).Count,
            _trace.GetSpansByType(SpanTypes.EntryVisited).Count,
            _trace.GetSpansByType(SpanTypes.EntrySkipped).Count,
            stepSpans.Count,
            _trace.GetSpansByType(SpanTypes.ActionScroll).Count,
            endOfListDetected,
            success,
            aiLatency,
            aiLatency);
    }

    /// <summary>
    /// Detect whether the run reached the end of the list:
    /// 1. any engine.step span carries an explicit end_of_list flag (attribute key
    ///    "end_of_list" or its camelCase form "endOfList" set to true); or
    /// 2. an engine.step's child generation produced no entry.observed — an
    ///    entry.generate child under a step with zero entry.observed children means
    ///    that step found nothing new, i.e. the cumulative observed count did not
    ///    increase at that step.
    /// </summary>
    private bool DetectEndOfList(IReadOnlyList<TraceSpan> stepSpans)
    {
        foreach (var step in stepSpans)
        {
            if (step.Attributes is not null
                && (TryGetBool(step.Attributes, "end_of_list")
                    || TryGetBool(step.Attributes, "endOfList")))
            {
                return true;
            }

            foreach (var child in _trace.GetChildSpans(step.SpanId))
            {
                if (child.SpanType != SpanTypes.EntryGenerate)
                    continue;
                var producedObserved = _trace.GetChildSpans(child.SpanId)
                    .Any(grandchild => grandchild.SpanType == SpanTypes.EntryObserved);
                if (!producedObserved)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Read a boolean attribute, tolerating both the in-memory boxed bool and a
    /// JsonElement restored from a JSON round-trip.
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

    /// <summary>Mirrors RunAssetStore.ValidatePathSegment — reject separators and "." / "..".</summary>
    private static bool IsSafeSegment(string value) =>
        string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
        && value is not "." and not "..";
}
