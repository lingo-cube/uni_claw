using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Observability;

/// <summary>
/// InMemoryTraceStorage — ITraceStorage implementation with 5 flat lists + 2 indexes.
/// Index keys use TraceContext encapsulation: _byNodeId uses r.Context?.NodeId,
/// _bySpanType uses r.SpanType (direct ExecutionRecord field).
/// Index methods (GetByNodeId, GetBySpanType) are on the concrete class only (ISP principle — D-2b).
/// Not all ITraceStorage implementations need memory indexes; different backends have different query strategies.
/// </summary>
public sealed class InMemoryTraceStorage : ITraceStorage
{
    // ── 5 flat lists ──────────────────────────────────────
    private readonly List<ExecutionRecord> _executions = new();
    private readonly List<StateTransition> _transitions = new();
    private readonly List<ErrorRecord> _errors = new();
    private readonly List<PageTransition> _pageTransitions = new();
    private readonly List<AICallRecord> _aiCalls = new();

    // ── Span list + lookups (D-134, trace-span-observability P1) ──
    private readonly List<TraceSpan> _spans = new();
    private readonly Dictionary<string, int> _spanIndex = new(); // spanId → list index
    private readonly Dictionary<string, List<int>> _spanIndexByType = new(); // spanType → indices

    // ── 2 incrementally-built indexes ─────────────────────
    private readonly Dictionary<string, List<ExecutionRecord>> _byNodeId = new();
    private readonly Dictionary<SpanType, List<ExecutionRecord>> _bySpanType = new();

    // ── Session state ─────────────────────────────────────
    private TraceSession? _session;

    // ── SpanId generation ─────────────────────────────────
    private int _spanCounter;

    // ── ITraceStorage: Session lifecycle ───────────────────

    public TraceSession? CurrentSession => _session;

    public void SetSession(TraceSession session)
    {
        _session = session;
    }

    public void EndSession()
    {
        if (_session != null)
            _session = _session with { EndTime = DateTimeOffset.UtcNow };
    }

    // ── ITraceStorage: Synchronous write ───────────────────

    public void AddExecution(ExecutionRecord record)
    {
        _executions.Add(record);

        // Index by Context?.NodeId — null Context or null NodeId not indexed
        var nodeId = record.Context?.NodeId;
        if (!string.IsNullOrEmpty(nodeId))
        {
            if (!_byNodeId.TryGetValue(nodeId, out var list))
            {
                list = new List<ExecutionRecord>();
                _byNodeId[nodeId] = list;
            }
            list.Add(record);
        }

        // Index by SpanType — null SpanType not indexed
        if (record.SpanType.HasValue)
        {
            var spanType = record.SpanType.Value;
            if (!_bySpanType.TryGetValue(spanType, out var list))
            {
                list = new List<ExecutionRecord>();
                _bySpanType[spanType] = list;
            }
            list.Add(record);
        }
    }

    public void AddTransition(StateTransition transition)
    {
        _transitions.Add(transition);
    }

    public void AddError(ErrorRecord record)
    {
        _errors.Add(record);
    }

    public void AddPageTransition(PageTransition transition)
    {
        _pageTransitions.Add(transition);
    }

    public void AddAICall(AICallRecord record)
    {
        _aiCalls.Add(record);
    }

    // ── ITraceStorage: Synchronous read ────────────────────

    public IReadOnlyList<ExecutionRecord> GetExecutions() => _executions;
    public IReadOnlyList<StateTransition> GetTransitions() => _transitions;
    public IReadOnlyList<ErrorRecord> GetErrors() => _errors;
    public IReadOnlyList<PageTransition> GetPageTransitions() => _pageTransitions;
    public IReadOnlyList<AICallRecord> GetAICalls() => _aiCalls;

    public string Export()
    {
        var data = new
        {
            Session = _session,
            Executions = _executions,
            Transitions = _transitions,
            Errors = _errors,
            PageTransitions = _pageTransitions,
            AICalls = _aiCalls,
            Spans = _spans,
        };
        return JsonSerializer.Serialize(data, DomainJsonOptions.Default);
    }

    // ── ITraceStorage: TraceSpan write/read (D-134) ───────

    /// <summary>Open a span — create and index a new TraceSpan, return its spanId.</summary>
    public string OpenSpan(string spanType, string spanName, string spanId,
        string? parentSpanId, DateTimeOffset startTime, TraceContext? context,
        Dictionary<string, object>? attributes)
    {
        var span = new TraceSpan(spanId, parentSpanId, spanType, spanName,
            startTime, null, "ok", context, attributes);
        // fail-fast: status validation (only checked for explicitly-created spans)
        span.Validate();
        AddSpanInternal(span);
        return spanId;
    }

    /// <summary>Close a span — set EndTime, Status, and merged attributes.</summary>
    public void CloseSpan(string spanId, DateTimeOffset endTime, string status,
        Dictionary<string, object>? attributes)
    {
        if (!_spanIndex.TryGetValue(spanId, out var index))
            return; // no-op for unknown spanId

        var existing = _spans[index];
        if (existing.EndTime.HasValue)
            return; // no-op for already-closed span

        // Merge attributes: EndSpan attributes override StartSpan attributes on key conflict.
        Dictionary<string, object>? merged = null;
        if (attributes != null || existing.Attributes != null)
        {
            merged = new(existing.Attributes ?? new Dictionary<string, object>());
            if (attributes != null)
            {
                foreach (var kv in attributes)
                    merged[kv.Key] = kv.Value;
            }
            if (merged.Count == 0) merged = null;
        }

        _spans[index] = existing with { EndTime = endTime, Status = status, Attributes = merged };
    }

    /// <summary>Find a span by its SpanId. Null if not found.</summary>
    public TraceSpan? FindSpan(string spanId)
    {
        return _spanIndex.TryGetValue(spanId, out var index) ? _spans[index] : null;
    }

    /// <summary>Get all spans in insertion order.</summary>
    public IReadOnlyList<TraceSpan> GetAllSpans() => _spans;

    /// <summary>Get all spans matching a dotted spanType string.</summary>
    public IReadOnlyList<TraceSpan> GetSpansByType(string spanType)
    {
        if (!_spanIndexByType.TryGetValue(spanType, out var indices))
            return Array.Empty<TraceSpan>();
        return indices.Select(i => _spans[i]).ToList();
    }

    /// <summary>Get all child spans whose ParentSpanId matches the given id.</summary>
    public IReadOnlyList<TraceSpan> GetChildSpans(string parentSpanId)
    {
        return _spans.Where(s => s.ParentSpanId == parentSpanId).ToList();
    }

    // ── Private span helpers ──────────────────────────────

    private void AddSpanInternal(TraceSpan span)
    {
        var index = _spans.Count;
        _spans.Add(span);
        _spanIndex[span.SpanId] = index;

        if (!_spanIndexByType.TryGetValue(span.SpanType, out var list))
        {
            list = new List<int>();
            _spanIndexByType[span.SpanType] = list;
        }
        list.Add(index);
    }

    /// <summary>Generate the next spanId in <c>"{traceId}-{counter:D6}"</c> format.</summary>
    internal string NextSpanId(string? traceId)
    {
        _spanCounter++;
        return $"{(traceId ?? "trace")}-{_spanCounter:D6}";
    }

    /// <summary>Public read access for span counter (tests).</summary>
    internal int SpanCount => _spans.Count;

    // ── InMemoryTraceStorage-specific index methods (NOT on ITraceStorage — ISP D-2b) ──

    /// <summary>Get execution records grouped by Context.NodeId</summary>
    public IReadOnlyList<ExecutionRecord> GetByNodeId(string nodeId)
    {
        return _byNodeId.TryGetValue(nodeId, out var list) ? list : Array.Empty<ExecutionRecord>();
    }

    /// <summary>Get execution records grouped by SpanType</summary>
    public IReadOnlyList<ExecutionRecord> GetBySpanType(SpanType spanType)
    {
        return _bySpanType.TryGetValue(spanType, out var list) ? list : Array.Empty<ExecutionRecord>();
    }
}
