namespace UniClaw.Core.Observability;

/// <summary>
/// TraceQueries — aggregated read-only query surface injected into analyzers
/// (ISP, design D-6). Combines the trace event-stream facet
/// (<see cref="ITraceEventQuery"/>) and the asset facet (<see cref="IAssetQuery"/>).
/// Analyzers receive this aggregate and never hold write capability — the full
/// write-capable <see cref="IAssetStore"/> / <see cref="ITraceStorage"/> are exposed
/// only to the write-side pipeline and implementations. Backend or composition swap
/// does not change analyzer code: analyzers depend on this aggregate, not on
/// implementations. Immutable: both facets are fixed at construction.
/// </summary>
public sealed class TraceQueries
{
    /// <summary>Read-only trace event-stream queries (spans, executions, errors, ...).</summary>
    public ITraceEventQuery Events { get; }

    /// <summary>Read-only asset queries (per-run runId injected at assembly; relative paths only).</summary>
    public IAssetQuery Assets { get; }

    /// <param name="events">Trace event-stream query facet. Null throws ArgumentNullException.</param>
    /// <param name="assets">Asset query facet. Null throws ArgumentNullException.</param>
    public TraceQueries(ITraceEventQuery events, IAssetQuery assets)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(assets);
        Events = events;
        Assets = assets;
    }
}
