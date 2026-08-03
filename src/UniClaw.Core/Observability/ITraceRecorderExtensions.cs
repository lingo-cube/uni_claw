using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Observability;

/// <summary>
/// Additive ITraceRecorder extensions (trace-span-helpers D1/D2 + trace-parent-linkage M2).
/// The 9-method interface contract itself is untouched —
/// ArchitectureGuardTests.ITraceRecorder_Has9Methods guards the interface surface;
/// these extensions live outside it.
/// </summary>
public static class ITraceRecorderExtensions
{
    /// <summary>
    /// Open a span and return an async-disposable region scope
    /// (<see cref="TraceSpanScope"/>). Disposing ends the span with status "ok"
    /// unless <see cref="TraceSpanScope.End"/> was already called. spanName
    /// defaults to the spanType. A null recorder yields a side-effect-free
    /// no-op scope (no span, no exception) — the recording mechanism for spans
    /// whose attributes are computed inside the region, whose spanType is
    /// runtime-selected from the catalog, or whose termination is conditional.
    /// profile + level (trace-parent-linkage M2): 同 span 按 TraceLevel 分级记录字段
    /// （过滤规则见 <see cref="SpanFieldProfile.Filter"/>）。缺省 profile=null / level=Detailed
    /// 与 change 前全量记录逐字节一致（向后兼容）。span 照常记录，level=None 仅清空属性。
    /// </summary>
    public static async Task<TraceSpanScope> BeginSpanAsync(
        this ITraceRecorder? recorder,
        string spanType,
        string? spanName = null,
        string? parentSpanId = null,
        Dictionary<string, object>? attributes = null,
        SpanFieldProfile? profile = null,
        TraceLevel level = TraceLevel.Detailed,
        CancellationToken ct = default)
    {
        if (recorder is null)
            return TraceSpanScope.CreateNoOp();

        var filtered = SpanFieldProfile.Filter(attributes, profile, level);
        var spanId = await recorder.StartSpanAsync(
            spanType,
            spanName ?? spanType,
            parentSpanId,
            filtered,
            ct);
        return new TraceSpanScope(recorder, spanId, profile, level);
    }

    /// <summary>
    /// Record a point-in-time event span (unpaired marker): opened with
    /// spanName == spanType and left unclosed (EndTime null, DurationMs == 0 —
    /// the model's expression for events, not durations). No-op when the
    /// recorder is null. parentSpanId accepts any runtime expression.
    /// profile + level: 同 BeginSpanAsync 的分级过滤（M2；缺省全量，向后兼容）。
    /// </summary>
    public static Task RecordEventAsync(
        this ITraceRecorder? recorder,
        string spanType,
        string? parentSpanId = null,
        Dictionary<string, object>? attributes = null,
        SpanFieldProfile? profile = null,
        TraceLevel level = TraceLevel.Detailed,
        CancellationToken ct = default)
    {
        if (recorder is null)
            return Task.CompletedTask;
        var filtered = SpanFieldProfile.Filter(attributes, profile, level);
        return recorder.StartSpanAsync(spanType, spanType, parentSpanId, filtered, ct);
    }
}
