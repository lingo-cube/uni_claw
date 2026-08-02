namespace UniClaw.Core.Observability;

/// <summary>
/// InMemoryTraceRecorder — minimal async wrapper over ITraceStorage.
/// Zero business logic — pure async-over-sync wrapper (D-6: async on consumer contract, sync on storage).
/// Injects ITraceStorage (interface, not concrete) per D-2b: Recorder injects interface.
/// </summary>
public sealed class InMemoryTraceRecorder : ITraceRecorder
{
    private readonly ITraceStorage _storage;

    /// <summary>Construct InMemoryTraceRecorder injecting ITraceStorage interface</summary>
    public InMemoryTraceRecorder(ITraceStorage storage)
    {
        _storage = storage;
    }

    public Task<TraceSession> StartSessionAsync(string traceId, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        var session = new TraceSession(traceId, DateTimeOffset.UtcNow, null, metadata);
        _storage.SetSession(session);
        return Task.FromResult(session);
    }

    public Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        _storage.EndSession();
        return Task.CompletedTask;
    }

    public Task RecordExecutionAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        _storage.AddExecution(record);
        return Task.CompletedTask;
    }

    public Task RecordTransitionAsync(StateTransition transition, CancellationToken cancellationToken = default)
    {
        _storage.AddTransition(transition);
        return Task.CompletedTask;
    }

    public Task RecordErrorAsync(ErrorRecord record, CancellationToken cancellationToken = default)
    {
        _storage.AddError(record);
        return Task.CompletedTask;
    }

    public Task RecordPageTransitionAsync(PageTransition transition, CancellationToken cancellationToken = default)
    {
        _storage.AddPageTransition(transition);
        return Task.CompletedTask;
    }

    public Task RecordAICallAsync(AICallRecord record, CancellationToken cancellationToken = default)
    {
        _storage.AddAICall(record);
        return Task.CompletedTask;
    }

    public Task<string> StartSpanAsync(string spanType, string spanName,
        string? parentSpanId = null, Dictionary<string, object>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = _storage.CurrentSession?.TraceId;
        var spanId = (_storage is InMemoryTraceStorage memStorage)
            ? memStorage.NextSpanId(traceId)
            : $"{traceId ?? "trace"}-{Guid.NewGuid():N}";
        var startTime = DateTimeOffset.UtcNow;
        _storage.OpenSpan(spanType, spanName, spanId, parentSpanId, startTime,
            context: null, attributes);
        return Task.FromResult(spanId);
    }

    public Task EndSpanAsync(string spanId, string status = "ok",
        Dictionary<string, object>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        _storage.CloseSpan(spanId, DateTimeOffset.UtcNow, status, attributes);
        return Task.CompletedTask;
    }
}
