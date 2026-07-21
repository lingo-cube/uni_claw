namespace UniClaw.Core.Observability;

/// <summary>
/// HandlerTraceWriter — IHandlerTraceWriter 的默认实现，委托 ITraceRecorder.RecordExecutionAsync。
/// 封装 ExecutionRecord 构建逻辑（Action、Status、SpanType、Metadata），不感知引擎上下文。
/// 编排层在调用 handler 后，提取 result 字段构造 metadata 并调用此接口。
/// </summary>
public sealed class HandlerTraceWriter : IHandlerTraceWriter
{
    private readonly ITraceRecorder? _recorder;

    /// <summary>
    /// 构造 HandlerTraceWriter。
    /// </summary>
    /// <param name="recorder">ITraceRecorder 实例（null = no-op）</param>
    public HandlerTraceWriter(ITraceRecorder? recorder = null)
    {
        _recorder = recorder;
    }

    /// <inheritdoc />
    public async Task RecordHandlerLifecycleAsync(
        string action,
        SpanType spanType,
        string status = "ok",
        Dictionary<string, object>? metadata = null,
        TraceContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (_recorder == null)
            return;

        await _recorder.RecordExecutionAsync(new ExecutionRecord(
            Action: action,
            Status: status,
            SpanType: spanType,
            Context: context,
            Timestamp: DateTimeOffset.UtcNow,
            Metadata: metadata), cancellationToken);
    }
}
