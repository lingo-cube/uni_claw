using System.Collections.Immutable;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Logical read-only observability surface (design.md §5 / §7):
/// <list type="bullet">
/// <item><see cref="GetRunSnapshot"/> — one classified read-only RunSnapshot.</item>
/// <item><see cref="GetRuntimeEvents"/> — cursor-based event page reads.</item>
/// <item><see cref="GetTraceSummary"/> — finalized trace metadata reads.</item>
/// <item><see cref="GetTraceSpans"/> — cursor-based finalized span reads.</item>
/// <item><see cref="SubscribeRunEvents"/> — live drain subscription.</item>
/// <item><see cref="GetEvidence"/> — logical evidence resolution.</item>
/// </list>
/// Transport-neutral: no HTTP/WebSocket/gRPC/UDS/ACP/stdio — the surface is
/// in-process logical operations only; wire format is explicitly deferred.
/// </summary>
public interface IReadOnlyObservability
{
    /// <summary>Returns the latest truthful projection for a run.</summary>
    RunSnapshot GetRunSnapshot(string runId);

    /// <summary>Reads a cursor-bounded page of projected runtime events.</summary>
    RuntimeEventPage GetRuntimeEvents(string runId, EventCursor? cursor = null);

    /// <summary>Reads a summary for one explicitly registered finalized trace.</summary>
    TraceRunSummaryResult GetTraceSummary(string runId);

    /// <summary>Reads one bounded page of spans for one explicitly registered trace.</summary>
    TraceSpanPage GetTraceSpans(string runId, int pageSize = 100, TraceSpanCursor? cursor = null, TraceSpanFilter? filter = null);

    /// <summary>Reads the derived functional-trajectory timeline for one run
    /// (timed segments + ordered decision markers + stage duration summary).</summary>
    RunTimelineResult GetRunTimeline(string runId);

    /// <summary>Creates a live subscription over projected events for a run.</summary>
    IObservabilitySubscription SubscribeRunEvents(string runId);

    /// <summary>Resolves a logical evidence reference to catalog metadata.</summary>
    EvidenceResolution GetEvidence(EvidenceRef evidenceRef);
}

/// <summary>One live drain subscription over a run's projected event stream.</summary>
public interface IObservabilitySubscription : IDisposable
{
    /// <summary>Gets the run associated with this subscription.</summary>
    string RunId { get; }

    /// <summary>Return only events newer than the subscription's cursor.</summary>
    RuntimeEventPage Drain();
}

/// <summary>
/// In-process, transport-neutral adapter of <see cref="IReadOnlyObservability"/>
/// (design.md §9 — explicitly NON-FINAL, testing/in-process composition only;
/// it is NOT a wire protocol and never becomes one in this slice).
///
/// Fail-open by construction: projection and snapshot derivation never throw
/// on malformed input (diagnostics instead); a telemetry-side failure can
/// never change a Kernel run result.
/// </summary>
public sealed class DriverHostObservability : IReadOnlyObservability
{
    private readonly RuntimeEventStore _store = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, RegisteredRun> _runs = new(StringComparer.Ordinal);

    private sealed record RegisteredRun(
        TraceRun Trace,
        AgentStateSnapshot Agent,
        EvidenceCatalog? Catalog,
        bool TraceFinalized);

    /// <summary>
    /// Register one projected run (fail-open). Returns the projection result
    /// including truthful diagnostics; never throws for malformed input.
    /// Idempotent per runId.
    /// </summary>
    public RuntimeEventProjection RegisterRun(
        string runId,
        TraceRun trace,
        AgentStateSnapshot agent,
        TraceCaptureBundle? captureBundle = null)
        => RegisterOrReplace(runId, trace, agent, captureBundle, replace: false);

    /// <summary>
    /// Replace a registered live run's projection with its final truthful snapshot
    /// + trace (dsh-runtime-agent-subagent-run-entry). Uses
    /// <see cref="RuntimeEventStore.ReplaceRunEvents"/> for the accept→terminal
    /// transition (accept-time projection is empty); the stored snapshot is
    /// refreshed. Fail-open like <see cref="RegisterRun"/>. No second store.
    /// </summary>
    public RuntimeEventProjection ReplaceRunProjection(
        string runId,
        TraceRun trace,
        AgentStateSnapshot agent,
        TraceCaptureBundle? captureBundle = null)
        => RegisterOrReplace(runId, trace, agent, captureBundle, replace: true);

    private RuntimeEventProjection RegisterOrReplace(
        string runId,
        TraceRun trace,
        AgentStateSnapshot agent,
        TraceCaptureBundle? captureBundle,
        bool replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(agent);

        EvidenceCatalog? catalog = captureBundle is null ? null : EvidenceCatalog.FromBundle(captureBundle, runId);

        RuntimeEventProjection projection;
        try
        {
            projection = RuntimeEventProjector.Project(trace, agent, catalog);
        }
        catch (Exception ex)
        {
            // Fail-open: telemetry projection failure never propagates into the Kernel path.
            projection = new RuntimeEventProjection
            {
                RunId = runId,
                Events = [],
                Diagnostics = [$"Projection failure: {ex.GetType().Name}: {ex.Message}"],
                ClassificationCoverage = [.. RuntimeEventKindTable.All],
            };
        }

        if (replace)
        {
            _store.ReplaceRunEvents(runId, projection.Events);
        }
        else
        {
            _store.Append(runId, projection.Events);
        }

        lock (_gate)
        {
            // Initial live-run registration carries an empty placeholder trace;
            // replacement is the explicit finalization boundary. Non-empty
            // directly registered traces are already materialized read models.
            var traceFinalized = replace || !trace.Spans.IsDefaultOrEmpty;
            _runs[runId] = new RegisteredRun(trace, agent, catalog, traceFinalized);
        }

        return projection;
    }

    /// <summary>Classified read-only snapshot of the registered run.</summary>
    public RunSnapshot GetRunSnapshot(string runId)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var registered))
            {
                return RunSnapshot.Unknown(runId, $"No registered run '{runId}'.");
            }

            return RunSnapshotProjector.Project(runId, registered.Trace, registered.Agent, registered.Catalog);
        }
    }

    /// <summary>Cursor-based page read over the projected event stream.</summary>
    public RuntimeEventPage GetRuntimeEvents(string runId, EventCursor? cursor = null)
        => _store.GetAfter(runId, cursor);

    /// <summary>Reads a summary for one explicitly registered finalized trace.</summary>
    public TraceRunSummaryResult GetTraceSummary(string runId)
    {
        lock (_gate)
            return TraceSpanReadModelProjector.Summary(
                runId,
                _runs.TryGetValue(runId, out var registered) && registered.TraceFinalized
                    ? registered.Trace
                    : null);
    }

    /// <summary>Reads one bounded page of spans for one explicitly registered trace.</summary>
    public TraceSpanPage GetTraceSpans(string runId, int pageSize = 100, TraceSpanCursor? cursor = null, TraceSpanFilter? filter = null)
    {
        lock (_gate)
            return TraceSpanReadModelProjector.Page(
                runId,
                _runs.TryGetValue(runId, out var registered) && registered.TraceFinalized
                    ? registered.Trace
                    : null,
                pageSize,
                cursor,
                filter);
    }

    /// <summary>Reads the derived functional-trajectory timeline for one run.</summary>
    public RunTimelineResult GetRunTimeline(string runId)
    {
        lock (_gate)
        {
            if (!_runs.TryGetValue(runId, out var registered))
                return RunTimelineResult.Unavailable($"No registered run '{runId}'.");

            var events = _store.GetAfter(runId, cursor: null).Events;
            return RunTimelineProjector.Project(registered.Trace, events);
        }
    }

    /// <summary>Live drain subscription over the projected event stream.</summary>
    public IObservabilitySubscription SubscribeRunEvents(string runId)
        => new StoreSubscription(_store, runId);

    /// <summary>Logical evidence resolution against the registered run's catalog.</summary>
    public EvidenceResolution GetEvidence(EvidenceRef evidenceRef)
    {
        ArgumentNullException.ThrowIfNull(evidenceRef);

        lock (_gate)
        {
            if (!_runs.TryGetValue(evidenceRef.RunId, out var registered) || registered.Catalog is null)
            {
                return new EvidenceResolution
                {
                    Found = false,
                    Ref = evidenceRef,
                    Diagnostic = $"No evidence catalog registered for run '{evidenceRef.RunId}'.",
                };
            }

            return registered.Catalog.Resolve(evidenceRef);
        }
    }

    /// <summary>Registered run ids (read-only diagnostic view).</summary>
    public ImmutableArray<string> RegisteredRunIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _runs.Keys.OrderBy(k => k, StringComparer.Ordinal)];
            }
        }
    }
}
