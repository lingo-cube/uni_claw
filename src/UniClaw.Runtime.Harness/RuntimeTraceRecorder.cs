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
    private readonly string? _traceId;
    private readonly ConcurrentDictionary<string, RecordedSpan> _spans = new();
    private readonly ConcurrentBag<string> _diagnostics = [];
    private readonly ActivityListener _listener;
    private readonly Stopwatch _clock;
    private volatile bool _disposed;
    private volatile bool _finalized;
    private TraceRun? _frozen;

    /// <summary>Creates a recorder for one Runtime invocation.</summary>
    public RuntimeTraceRecorder(string traceRunId, string? traceId = null)
    {
        _traceRunId = traceRunId;
        _traceId = traceId;
        _clock = Stopwatch.StartNew();

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
                        Attributes = [],
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
            var span = new RecordedSpan
            {
                SpanId = activity.Id ?? activity.SpanId.ToString(),
                ParentSpanId = activity.ParentId,
                Name = activity.DisplayName,
                Layer = activity.GetTagItem("layer")?.ToString(),
                Component = activity.GetTagItem("component")?.ToString(),
                StartOffsetNs = _clock.ElapsedTicks * 1_000_000_000L / Stopwatch.Frequency,
            };
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
                span.Outcome = activity.GetTagItem("outcome")?.ToString() ?? "UNKNOWN";
                span.DurationNs = (_clock.ElapsedTicks * 1_000_000_000L / Stopwatch.Frequency) - span.StartOffsetNs;
                span.HasDuration = true;

                // Record events
                foreach (var evt in activity.Events)
                {
                    span.Events.Add(new RecordedEvent
                    {
                        EventId = evt.Name,
                        TimestampOffsetNs = span.StartOffsetNs,
                    });
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
    }
}
