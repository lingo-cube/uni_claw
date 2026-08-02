using UniClaw.Core.Observability;

namespace UniClaw.Host.Observability;

/// <summary>
/// Keeps the queryable in-memory trace as the authoritative read model while
/// mirroring every lifecycle/write operation to a durable run-asset backend.
/// </summary>
public sealed class MirroringTraceStorage : ITraceStorage
{
    private readonly ITraceStorage _primary;
    private readonly ITraceStorage _mirror;

    public MirroringTraceStorage(ITraceStorage primary, ITraceStorage mirror)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
    }

    public TraceSession? CurrentSession => _primary.CurrentSession;

    public void SetSession(TraceSession session)
    {
        _primary.SetSession(session);
        _mirror.SetSession(session);
        ConsoleTrace.SessionStart(session.TraceId);
    }

    public void EndSession()
    {
        _primary.EndSession();
        _mirror.EndSession();
        ConsoleTrace.SessionEnd();
    }

    public void AddExecution(ExecutionRecord record)
    {
        _primary.AddExecution(record);
        _mirror.AddExecution(record);
        ConsoleTrace.Log(record);
    }

    public void AddTransition(StateTransition transition)
    {
        _primary.AddTransition(transition);
        _mirror.AddTransition(transition);
        ConsoleTrace.Log(transition);
    }

    public void AddError(ErrorRecord record)
    {
        _primary.AddError(record);
        _mirror.AddError(record);
        ConsoleTrace.Log(record);
    }

    public void AddPageTransition(PageTransition transition)
    {
        _primary.AddPageTransition(transition);
        _mirror.AddPageTransition(transition);
        ConsoleTrace.Log(transition);
    }

    public void AddAICall(AICallRecord record)
    {
        _primary.AddAICall(record);
        _mirror.AddAICall(record);
        ConsoleTrace.Log(record);
    }

    public IReadOnlyList<ExecutionRecord> GetExecutions() =>
        _primary.GetExecutions();

    public IReadOnlyList<StateTransition> GetTransitions() =>
        _primary.GetTransitions();

    public IReadOnlyList<ErrorRecord> GetErrors() =>
        _primary.GetErrors();

    public IReadOnlyList<PageTransition> GetPageTransitions() =>
        _primary.GetPageTransitions();

    public IReadOnlyList<AICallRecord> GetAICalls() =>
        _primary.GetAICalls();

    public string Export() => _primary.Export();

    // ── TraceSpan write/read (D-134, trace-span-observability P1) ──
    // Read from primary (authoritative); write to both.

    public string OpenSpan(string spanType, string spanName, string spanId,
        string? parentSpanId, DateTimeOffset startTime, TraceContext? context,
        Dictionary<string, object>? attributes)
    {
        _primary.OpenSpan(spanType, spanName, spanId, parentSpanId, startTime, context, attributes);
        _mirror.OpenSpan(spanType, spanName, spanId, parentSpanId, startTime, context, attributes);
        return spanId;
    }

    public void CloseSpan(string spanId, DateTimeOffset endTime, string status,
        Dictionary<string, object>? attributes)
    {
        _primary.CloseSpan(spanId, endTime, status, attributes);
        _mirror.CloseSpan(spanId, endTime, status, attributes);
    }

    public TraceSpan? FindSpan(string spanId) => _primary.FindSpan(spanId);

    public IReadOnlyList<TraceSpan> GetAllSpans() => _primary.GetAllSpans();

    public IReadOnlyList<TraceSpan> GetSpansByType(string spanType) => _primary.GetSpansByType(spanType);

    public IReadOnlyList<TraceSpan> GetChildSpans(string parentSpanId) => _primary.GetChildSpans(parentSpanId);
}
