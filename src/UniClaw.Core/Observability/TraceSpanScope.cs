using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Observability;

/// <summary>
/// TraceSpanScope — async-disposable span region (trace-span-helpers D1).
/// Opened via <see cref="ITraceRecorderExtensions.BeginSpanAsync"/>; disposing
/// ends the span with status "ok" unless <see cref="End"/> was already called.
/// A no-op scope is returned when no ITraceRecorder is attached, so business
/// code can `await using` unconditionally without per-site null-guards.
/// trace-parent-linkage M2: 存储 profile + level，End 合并最终属性时同样按级过滤
/// （规则见 <see cref="SpanFieldProfile.Filter"/>）。
/// trace-correlated-logging D-1: 全部 span 的唯一生命周期封装 —— 构造（spanId 非 null）
/// Push 到 <see cref="EngineStepSpanContext"/>，DisposeAsync Pop；此处同步 span 上下文即全
/// span 覆盖（SourceGen Emitter 零改动）。no-op scope（spanId=null）不入栈、不改动当前上下文。
/// </summary>
public sealed class TraceSpanScope : IAsyncDisposable
{
    private static readonly TraceSpanScope NoOp = new(recorder: null, spanId: null);

    private readonly ITraceRecorder? _recorder;
    private readonly SpanFieldProfile? _profile;
    private readonly TraceLevel _level;
    private int _ended;

    internal TraceSpanScope(
        ITraceRecorder? recorder,
        string? spanId,
        SpanFieldProfile? profile = null,
        TraceLevel level = TraceLevel.Detailed)
    {
        _recorder = recorder;
        SpanId = spanId;
        _profile = profile;
        _level = level;
        // trace-correlated-logging D-1: span context sync point.
        // Push is NOT performed here — AsyncLocal writes inside async methods (BeginSpanAsync)
        // are invisible to the caller's ExecutionContext (copy-on-write at async boundaries).
        // Instead, callers that need span context visibility must explicitly
        // EngineStepSpanContext.Instance.Push(scope.SpanId) from their own flow
        // (see TraversalEngine for engine.step).  DisposeAsync Pop is still valid here
        // as it runs in the caller's flow.  Documented: log.md D-222 / D-223.
    }

    /// <summary>Span id of the open span; null for a no-op scope (no recorder).</summary>
    public string? SpanId { get; }

    /// <summary>Shared no-op scope — all members are side-effect-free on a null recorder.</summary>
    internal static TraceSpanScope CreateNoOp() => NoOp;

    /// <summary>
    /// End the span with the given status and merged final attributes
    /// (end attributes override start attributes on key conflict, per
    /// EndSpanAsync semantics). End 属性经 profile/level 过滤后再交给 recorder。
    /// Idempotent — a second end, including the dispose auto-end, is a no-op.
    /// </summary>
    public Task End(
        string status = "ok",
        Dictionary<string, object>? attributes = null,
        CancellationToken ct = default)
    {
        if (_recorder is null || SpanId is null)
            return Task.CompletedTask;
        if (Interlocked.Exchange(ref _ended, 1) != 0)
            return Task.CompletedTask;
        var filtered = SpanFieldProfile.Filter(attributes, _profile, _level);
        return _recorder.EndSpanAsync(SpanId, status, filtered, ct);
    }

    /// <summary>
    /// End the span with status "ok" when not already ended, then restore the parent
    /// span in the async span context (trace-correlated-logging D-1): Pop matches the
    /// Push performed in the constructor — guarded by SpanId so a no-op scope
    /// (spanId null, never pushed) can never pop a span it did not push (D-1: no-op
    /// scope 不入栈、不改动当前上下文).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await End(ct: CancellationToken.None);
        if (SpanId != null)
            EngineStepSpanContext.Instance.Pop();
    }
}
