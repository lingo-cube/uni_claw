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

    // ── 2 incrementally-built indexes ─────────────────────
    private readonly Dictionary<string, List<ExecutionRecord>> _byNodeId = new();
    private readonly Dictionary<SpanType, List<ExecutionRecord>> _bySpanType = new();

    // ── Session state ─────────────────────────────────────
    private TraceSession? _session;

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
        };
        return JsonSerializer.Serialize(data, DomainJsonOptions.Default);
    }

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
