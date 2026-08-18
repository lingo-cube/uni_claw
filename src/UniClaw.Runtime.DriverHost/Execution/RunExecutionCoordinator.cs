using System.Collections.Immutable;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Narrow execution owner for accepted live runs (dsh-runtime-agent-subagent-run-entry).
///
/// Owns ONLY:
///  - accepted live run execution records
///  - authoritative runId creation (DriverHost-owned)
///  - device reservation (ONE_ACTIVE_RUN_PER_DEVICE)
///  - runtime graph construction (via injected composition-root
///    <see cref="RunGraphFactory"/>; the Agent still receives only IEnvironment)
///  - Agent task lifetime
///  - final cleanup / reservation release
///
/// Does NOT own: RuntimeEvent truth, GoalEvidence, Agent semantic decisions,
/// Container state, Binding, StateBelief, recovery policy, device IO semantics.
///
/// Reuses the existing <see cref="DriverHostObservability"/> / RuntimeEventStore /
/// RuntimeTraceRecorder / AgentStateSnapshot — no second observability store.
/// Device locking lives HERE (control layer), never inside the Agent.
/// </summary>
public sealed class RunExecutionCoordinator : IUniClawRunExecution
{
    /// <summary>Semantic loop iteration budget for accepted runs (same default as
    /// the existing production entry; no request field in this slice).</summary>
    private const int SemanticRunMaxIterations = 5;

    private readonly DriverHostObservability _observability;
    private readonly RunGraphFactory _graphFactory;
    private readonly Func<string> _runIdFactory;
    private readonly object _gate = new();
    private readonly Dictionary<string, ActiveRun> _runs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _deviceReservations = new(StringComparer.Ordinal);
    private int _nextRunNumber;

    /// <summary>One accepted live run record (coordinator-owned; never a second
    /// truth store — Kernel truth stays in the observability/Agent surfaces).</summary>
    public sealed record ActiveRun(
        string RunId,
        string DeviceKey,
        RunExecutionGraph Graph,
        RuntimeTraceRecorder Recorder,
        Task Execution);

    /// <param name="observability">Existing observability store (reused; no second store).</param>
    /// <param name="graphFactory">Composition-root device factory (explicit mapping; no discovery).</param>
    /// <param name="runIdFactory">Optional DriverHost-owned runId generator (deterministic default).</param>
    public RunExecutionCoordinator(
        DriverHostObservability observability,
        RunGraphFactory graphFactory,
        Func<string>? runIdFactory = null)
    {
        ArgumentNullException.ThrowIfNull(observability);
        ArgumentNullException.ThrowIfNull(graphFactory);
        _observability = observability;
        _graphFactory = graphFactory;
        _runIdFactory = runIdFactory ?? DefaultRunIdFactory;
    }

    /// <summary>Diagnostic view of accepted live runs (coordinator-owned; NOT a
    /// wire protocol and NOT a second truth store).</summary>
    public IReadOnlyDictionary<string, ActiveRun> Runs
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, ActiveRun>(_runs, StringComparer.Ordinal);
            }
        }
    }

    /// <inheritdoc />
    public RunAccepted StartRun(RunStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. validate request (REQUEST_REJECTED; no run created).
        ValidateRequest(request);

        var deviceKey = request.Device.Key;

        // 2. resolve device through the composition-root factory (before any
        //    reservation; unknown selector → REQUEST_REJECTED).
        RunExecutionGraph graph;
        try
        {
            graph = _graphFactory(request.Device);
        }
        catch (DeviceSelectorUnsupportedException ex)
        {
            throw new RequestRejectedException(ex.Message);
        }

        // 3. acquire device reservation (ONE_ACTIVE_RUN_PER_DEVICE — control layer).
        string runId;
        lock (_gate)
        {
            if (_deviceReservations.ContainsKey(deviceKey))
            {
                throw new RequestRejectedException(
                    $"device '{deviceKey}' is busy: ONE_ACTIVE_RUN_PER_DEVICE (run '{_deviceReservations[deviceKey]}' is active)");
            }

            // 4. create the authoritative runId (DriverHost-owned; the outer control host never supplies it).
            runId = _runIdFactory();
            _deviceReservations[deviceKey] = runId;
        }

        try
        {
            // 5. recorder + truthful initial registration BEFORE scheduling Agent
            //    work — the returned runId is immediately legitimate for
            //    run.list / run.snapshot.get / run.events.after (no race).
            var recorder = new RuntimeTraceRecorder(runId);
            var initialSnapshot = AgentStateSnapshot.From(graph.Agent);
            var emptyTrace = new TraceRun { TraceRunId = runId, RunId = runId };
            _observability.RegisterRun(runId, emptyTrace, initialSnapshot);

            // 6. schedule Agent execution (fire-and-track; never awaited here).
            var task = Task.Run(async () => await ExecuteRunAsync(runId, request, graph, recorder));

            lock (_gate)
            {
                _runs[runId] = new ActiveRun(runId, deviceKey, graph, recorder, task);
            }

            // 7. return immediately (async start).
            return new RunAccepted(runId, graph.Agent.State);
        }
        catch
        {
            // Registration/scheduling failure after reservation: release the
            // reservation and drop the run — REQUEST_REJECTED semantics (no
            // zombie accepted run, no reservation leak).
            lock (_gate)
            {
                _deviceReservations.Remove(deviceKey);
                _runs.Remove(runId);
            }

            throw;
        }
    }

    /// <summary>Deterministic request validation mirroring the existing
    /// Runtime fail-closed semantics (unknown object / undeclared dimension /
    /// capability selection not exactly-one → REQUEST_REJECTED).</summary>
    private static void ValidateRequest(RunStartRequest request)
    {
        var goal = request.Goal;
        if (goal is null || string.IsNullOrWhiteSpace(goal.ObjectIdentity) || string.IsNullOrWhiteSpace(goal.StateDimension))
        {
            throw new RequestRejectedException("invalid goal: objectIdentity and stateDimension are required");
        }

        if (request.Objects.IsDefaultOrEmpty)
        {
            throw new RequestRejectedException("invalid request: at least one SemanticObject is required");
        }

        var obj = request.Objects.FirstOrDefault(o => o.Identity == goal.ObjectIdentity);
        if (obj is null)
        {
            throw new RequestRejectedException($"invalid goal: unknown object '{goal.ObjectIdentity}'");
        }

        if (!obj.StateDimensions.Contains(goal.StateDimension))
        {
            throw new RequestRejectedException(
                $"invalid goal: object '{goal.ObjectIdentity}' does not declare state dimension '{goal.StateDimension}'");
        }

        if (request.Capabilities.IsDefaultOrEmpty)
        {
            throw new RequestRejectedException("invalid request: at least one Capability is required");
        }

        var matches = request.Capabilities
            .Where(c => c.ApplicableToCategory == obj.Category && c.StateDimension == goal.StateDimension)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new RequestRejectedException(
                $"invalid request: capability selection for category '{obj.Category}' dimension '{goal.StateDimension}' is " +
                (matches.Length == 0 ? "unresolved" : "ambiguous"));
        }
    }

    /// <summary>
    /// Background execution: the existing Agent semantic entry runs to a terminal
    /// outcome; the recorder is finalized and the registered projection replaced
    /// with the final truthful snapshot + trace so terminal state and the full
    /// event stream are observable through existing surfaces. Reservation always
    /// released (all terminal paths, including unexpected exceptions).
    /// </summary>
    private async Task ExecuteRunAsync(
        string runId,
        RunStartRequest request,
        RunExecutionGraph graph,
        RuntimeTraceRecorder recorder)
    {
        Exception? unexpected = null;
        try
        {
            _ = await graph.Agent.RunSemanticGoalAsync(
                request.Goal,
                request.Objects,
                request.Capabilities,
                runId,
                CancellationToken.None,
                SemanticRunMaxIterations);
        }
        catch (OperationCanceledException)
        {
            // No cancellation surface exists in this slice; a cancellation would
            // surface as an abnormal execution outcome below (truthful, never
            // fabricated as a Kernel failure).
            unexpected = new InvalidOperationException(
                "run execution cancelled (no cancellation surface exists in this slice)");
        }
        catch (Exception ex)
        {
            // §22: an unexpected execution exception must never become an unobserved
            // process fault. The Agent owns its semantic state and exposes no public
            // failure injection — we record the abnormal outcome truthfully at the
            // coordinator level (no fabricated Trap, GoalEvidence, or Failed state),
            // finalize the recorder, and release the device reservation.
            unexpected = ex;
        }
        finally
        {
            try
            {
                recorder.Finalize();
            }
            catch
            {
                // recorder finalization is diagnostic-only; never blocks cleanup
            }

            try
            {
                var trace = recorder.FrozenTrace ?? new TraceRun { TraceRunId = runId, RunId = runId };
                if (unexpected is not null)
                {
                    trace = trace with
                    {
                        Diagnostics = trace.Diagnostics.Add(
                            $"RunExecutionCoordinator: unexpected execution exception: {unexpected.GetType().Name}: {unexpected.Message}"),
                    };
                }

                _observability.ReplaceRunProjection(runId, trace, AgentStateSnapshot.From(graph.Agent), null);
            }
            catch
            {
                // observability is fail-open; never corrupt the terminal path
            }

            ReleaseReservation(runId);
        }
    }

    private string DefaultRunIdFactory()
    {
        lock (_gate)
        {
            _nextRunNumber++;
            return $"run-{_nextRunNumber}";
        }
    }

    private void ReleaseReservation(string runId)
    {
        lock (_gate)
        {
            _runs.Remove(runId);
            var deviceKey = _deviceReservations.FirstOrDefault(kv => kv.Value == runId).Key;
            if (deviceKey is not null)
            {
                _deviceReservations.Remove(deviceKey);
            }
        }
    }
}
