using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace UniClaw.Runtime.Harness;

/// <summary>
/// Per-run Harness trace recorder. Subscribes to the approved Runtime
/// ActivitySource, records span/event lifecycle data, and freezes once
/// into an immutable TraceRun projection.
///
/// Owns ONLY its local mutable buffers. References NO Runtime types
/// beyond Activity/ActivitySource. Listener failures latch as Harness
/// diagnostics — Runtime behavior is unaffected.
/// </summary>
public sealed class RuntimeTraceRecorder : IDisposable
{
    private readonly string _traceRunId;
    private volatile string? _traceId;
    // Run-scoped capture: the W3C trace id of the first recorded activity claims
    // this recorder; activities of a different trace are skipped (Diagnostics).
    private volatile string? _captureTraceId;
    private int _foreignSkipCount;
    private readonly ConcurrentDictionary<string, RecordedSpan> _spans = new();
    private readonly ConcurrentBag<string> _diagnostics = [];
    private readonly ActivityListener _listener;
    private readonly Stopwatch _clock;
    // Wall-clock epoch captured once at recorder start. Event wall timestamps are
    // mapped through this single conversion point (documented conversion
    // tolerance — hierarchical-trace-projection spec) and additionally clamped
    // into their containing span interval.
    private readonly long _epochWallTicks;
    private volatile bool _disposed;
    private volatile bool _finalized;
    private TraceRun? _frozen;

    /// <summary>Creates a recorder for one Runtime invocation.</summary>
    public RuntimeTraceRecorder(string traceRunId, string? traceId = null)
    {
        _traceRunId = traceRunId;
        _traceId = traceId;
        _clock = Stopwatch.StartNew();
        _epochWallTicks = DateTimeOffset.UtcNow.Ticks;

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "UniClaw.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = OnStarted,
            ActivityStopped = OnStopped,
        };
        try { ActivitySource.AddActivityListener(_listener); }
        catch (Exception ex) { _diagnostics.Add($"Listener registration failed: {ex.Message}"); }
    }

    public string TraceRunId => _traceRunId;
    public bool IsFinalized => _finalized;
    public TraceRun? FrozenTrace => _frozen;

    /// <summary>Freeze all recorded data into an immutable TraceRun. Idempotent.</summary>
    public TraceRun Finalize()
    {
        if (_finalized) return _frozen!;
        _finalized = true;
        _clock.Stop();

        try
        {
            _listener.Dispose();

            if (_foreignSkipCount > 0)
            {
                _diagnostics.Add(
                    $"{_foreignSkipCount} foreign-trace activity/activities skipped by run-scoped capture.");
            }

            var spans = _spans.Values
                .OrderBy(s => s.StartOffsetNs)
                .Select(s => new TraceSpan
                {
                    SpanId = s.SpanId,
                    ParentSpanId = s.ParentSpanId,
                    Name = s.Name,
                    Layer = s.Layer ?? "",
                    Component = s.Component ?? "",
                    StartOffsetNs = s.StartOffsetNs,
                    DurationNs = s.HasDuration ? s.DurationNs : (_clock.ElapsedTicks * 1_000_000_000L / Stopwatch.Frequency) - s.StartOffsetNs,
                    Outcome = s.Outcome ?? "UNKNOWN",
                    Attributes = [.. s.Attributes.Select(a => new TraceSpanAttribute { Key = a.Key, Value = a.Value })],
                    Events = [.. s.Events.Select(e => new ObservabilityEvent
                    {
                        EventId = e.EventId,
                        SpanId = s.SpanId,
                        TimestampOffsetNs = e.TimestampOffsetNs,
                        Attributes = [.. e.Attributes.Select(a => new TraceSpanAttribute { Key = a.Key, Value = a.Value })],
                    })],
                }).ToImmutableArray();

            _frozen = new TraceRun
            {
                TraceRunId = _traceRunId,
                TraceId = _traceId,
                RunId = _traceRunId,
                Spans = spans,
                Diagnostics = [.. _diagnostics],
            };
            return _frozen;
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"Finalization error: {ex.Message}");
            _frozen = new TraceRun
            {
                TraceRunId = _traceRunId,
                TraceId = _traceId,
                Diagnostics = [.. _diagnostics],
            };
            return _frozen;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (!_finalized) Finalize();
        }
    }

    private void OnStarted(Activity activity)
    {
        try
        {
            // Run-scoped capture: the first recorded activity's trace id claims
            // this recorder; activities of another trace (concurrent runs / other
            // process-global listeners) are skipped and reported at finalization.
            var traceId = activity.TraceId == default ? null : activity.TraceId.ToString();
            if (traceId is not null)
            {
                if (_captureTraceId is null)
                {
                    _captureTraceId = traceId;
                    // Preserve the real trace identity of the recorded evidence when
                    // the caller left it open (RunExecutionCoordinator does not
                    // supply one): the run's trace id is the first recorded activity's.
                    if (_traceId is null)
                        _traceId = traceId;
                }
                else if (!string.Equals(_captureTraceId, traceId, StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _foreignSkipCount);
                    return; // foreign trace — never recorded into this run
                }
            }

            var span = new RecordedSpan
            {
                SpanId = activity.Id ?? activity.SpanId.ToString(),
                ParentSpanId = activity.ParentId,
                Name = activity.DisplayName,
                StartOffsetNs = _clock.ElapsedTicks * 1_000_000_000L / Stopwatch.Frequency,
            };
            // Note: layer/component are read at ActivityStopped, not here —
            // SetTag happens after ActivitySource.StartActivity returns, so the
            // started callback cannot observe the stable attribution yet.
            _spans[span.SpanId] = span;
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"ActivityStarted error: {ex.Message}");
        }
    }

    private void OnStopped(Activity activity)
    {
        try
        {
            var spanId = activity.Id ?? activity.SpanId.ToString();
            if (_spans.TryGetValue(spanId, out var span))
            {
                // Stable attribution is set on the activity AFTER it started, so it
                // is only observable here, at closure.
                span.Layer = activity.GetTagItem("layer")?.ToString();
                span.Component = activity.GetTagItem("component")?.ToString();
                span.Outcome = activity.GetTagItem("outcome")?.ToString() ?? "UNKNOWN";
                span.DurationNs = (_clock.ElapsedTicks * 1_000_000_000L / Stopwatch.Frequency) - span.StartOffsetNs;
                span.HasDuration = true;

                // Record events with their real monotonic offset and carried attributes
                foreach (var evt in activity.Events)
                {
                    var recorded = new RecordedEvent
                    {
                        EventId = evt.Name,
                        TimestampOffsetNs = ClampToSpan(
                            ToMonotonicOffsetNs(evt.Timestamp), span.StartOffsetNs, span.DurationNs),
                    };
                    recorded.Attributes.AddRange(evt.Tags.Select(kv => new TraceSpanAttribute
                    {
                        Key = kv.Key,
                        Value = kv.Value?.ToString(),
                    }));
                    span.Events.Add(recorded);
                }

                // Record tags as attributes
                foreach (var (key, value) in activity.TagObjects)
                {
                    if (key is "layer" or "component" or "outcome") continue; // already handled
                    span.Attributes.Add(new TraceSpanAttribute { Key = key, Value = value?.ToString() });
                }
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Add($"ActivityStopped error: {ex.Message}");
        }
    }

    private sealed class RecordedSpan
    {
        public string SpanId { get; set; } = "";
        public string? ParentSpanId { get; set; }
        public string Name { get; set; } = "";
        public string? Layer { get; set; }
        public string? Component { get; set; }
        public long StartOffsetNs { get; set; }
        public long DurationNs { get; set; }
        public bool HasDuration { get; set; }
        public string? Outcome { get; set; }
        public List<TraceSpanAttribute> Attributes { get; } = [];
        public List<RecordedEvent> Events { get; } = [];
    }

    private sealed class RecordedEvent
    {
        public string EventId { get; set; } = "";
        public long TimestampOffsetNs { get; set; }
        public List<TraceSpanAttribute> Attributes { get; } = [];
    }

    /// <summary>
    /// Convert an event wall-clock timestamp to a monotonic elapsed offset through
    /// the single epoch captured at recorder start. Documented conversion
    /// tolerance: within one run the wall↔Stopwatch drift is below measurement
    /// resolution; future-dated events are capped at the current monotonic elapsed.
    /// </summary>
    private long ToMonotonicOffsetNs(DateTimeOffset timestamp)
    {
        var elapsedTicks = timestamp.UtcTicks - _epochWallTicks;
        if (elapsedTicks <= 0) return 0;
        var ns = elapsedTicks * 100; // 1 tick = 100 ns
        var nowNs = _clock.ElapsedTicks * 1_000_000_000L / Stopwatch.Frequency;
        return Math.Min(ns, nowNs);
    }

    /// <summary>Clamp an event offset into its containing span interval
    /// (child-interval invariant: events stay within [start, start + duration]).</summary>
    private static long ClampToSpan(long offsetNs, long spanStartNs, long spanDurationNs)
    {
        var spanEndNs = spanStartNs + spanDurationNs;
        if (offsetNs < spanStartNs) return spanStartNs;
        if (offsetNs > spanEndNs) return spanEndNs;
        return offsetNs;
    }
}
