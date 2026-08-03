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

    /// <summary>End the span with status "ok" when not already ended.</summary>
    public ValueTask DisposeAsync() => new(End(ct: CancellationToken.None));
}
