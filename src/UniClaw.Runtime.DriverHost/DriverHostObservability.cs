using System.Collections.Immutable;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Logical read-only observability surface (design.md §5 / §7):
/// <list type="bullet">
/// <item><see cref="GetRunSnapshot"/> — one classified read-only RunSnapshot.</item>
/// <item><see cref="GetRuntimeEvents"/> — cursor-based event page reads.</item>
/// <item><see cref="SubscribeRunEvents"/> — live drain subscription.</item>
/// <item><see cref="GetEvidence"/> — logical evidence resolution.</item>
/// </list>
/// Transport-neutral: no HTTP/WebSocket/gRPC/UDS/ACP/stdio — the surface is
/// in-process logical operations only; wire format is explicitly deferred.
/// </summary>
public interface IReadOnlyObservability
{
    RunSnapshot GetRunSnapshot(string runId);

    RuntimeEventPage GetRuntimeEvents(string runId, EventCursor? cursor = null);

    IObservabilitySubscription SubscribeRunEvents(string runId);

    EvidenceResolution GetEvidence(EvidenceRef evidenceRef);
}

/// <summary>One live drain subscription over a run's projected event stream.</summary>
public interface IObservabilitySubscription : IDisposable
{
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
        EvidenceCatalog? Catalog);

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

        _store.Append(runId, projection.Events);
        lock (_gate)
        {
            _runs[runId] = new RegisteredRun(trace, agent, catalog);
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

            return RunSnapshotProjector.Project(runId, registered.Trace, registered.Agent);
        }
    }

    /// <summary>Cursor-based page read over the projected event stream.</summary>
    public RuntimeEventPage GetRuntimeEvents(string runId, EventCursor? cursor = null)
        => _store.GetAfter(runId, cursor);

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
