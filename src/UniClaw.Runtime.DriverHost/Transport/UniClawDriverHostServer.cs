using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Planning;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// ONE concrete local transport (protocol baseline §9 — TRANSPORT_DEFERRED now
/// resolved): loopback TCP, newline-delimited JSON-RPC. The DriverHost OWNS the
/// listening process; the DSH plugin CONNECTS.
///
/// Contract guarantees:
/// - Read-only dispatch: every method maps to <see cref="IUniClawControlSurface"/>,
///   which has no Kernel-mutating operation — the server can never mutate Kernel state.
/// - Typed deterministic errors: bad_request / unknown_method / internal_error.
/// - Fail-open: a dispatch exception is answered with internal_error and the
///   connection stays usable; observability failure never equals Kernel failure.
/// - Fresh-state reconnect: drain cursors are PER CONNECTION and reset when the
///   connection closes; a new connection starts from a fresh page (no fabricated
///   state, no cross-connection cache).
/// </summary>
public sealed class UniClawDriverHostServer : IDisposable
{
    private readonly IUniClawControlSurface _surface;
    private readonly IUniClawRunExecution? _execution;
    private readonly IAssistanceWireSurface? _assistance;
    private readonly IUniClawStrategyExecution? _strategyExecution;
    private readonly DriverHostServerOptions _options;
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<(int ConnectionId, string RunId), long> _drainCursors = new();
    private TcpListener? _listener;
    private CancellationTokenSource _cts = new();
    private int _nextConnectionId;
    private int _disposed;

    /// <summary>Create the server over one read-only control surface, an optional
    /// authorized execution seam (additive run.start), and an optional assistance
    /// wire surface (additive assistance.pending / assistance.resolve;
    /// dsh-assistance-provider-adapter).</summary>
    public UniClawDriverHostServer(
        IUniClawControlSurface surface,
        DriverHostServerOptions? options = null,
        IUniClawRunExecution? execution = null,
        IAssistanceWireSurface? assistance = null,
        IUniClawStrategyExecution? strategyExecution = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _execution = execution;
        _assistance = assistance;
        _strategyExecution = strategyExecution;
        _options = options ?? new DriverHostServerOptions();
    }

    /// <summary>Bound port after Start(); useful with Port = 0 (ephemeral).</summary>
    public int BoundPort { get; private set; }

    /// <summary>Whether the listener is currently accepting connections.</summary>
    public bool IsListening => _listener is not null && !_cts.IsCancellationRequested;

    /// <summary>Number of currently connected clients (diagnostic).</summary>
    public int ActiveConnections => _clients.Count;

    /// <summary>Start accepting connections. Idempotent.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        if (_listener is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Parse(_options.Host), _options.Port);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _options.Log?.Invoke($"DriverHost transport listening on {_options.Host}:{BoundPort}");

        _ = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (_listener is not null && !_cts.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                var connectionId = Interlocked.Increment(ref _nextConnectionId);
                _clients[connectionId] = client;
                _ = Task.Run(() => ClientLoopAsync(client, connectionId));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break; // listener stopped
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task ClientLoopAsync(TcpClient client, int connectionId)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

            string? line;
            while (!_cts.IsCancellationRequested && (line = await reader.ReadLineAsync()) is not null)
            {
                string response;
                if (line.Length > _options.MaxMessageBytes)
                {
                    response = UniClawWireCodec.SerializeError(null, UniClawWireContract.ErrorBadRequest, "message exceeds MaxMessageBytes");
                }
                else
                {
                    response = Dispatch(line, connectionId);
                }

                await writer.WriteLineAsync(response);
            }
        }
        catch (IOException)
        {
            // client disconnected — cleanup below
        }
        catch (SocketException)
        {
            // client disconnected — cleanup below
        }
        catch (ObjectDisposedException)
        {
            // server stopped — cleanup below
        }
        finally
        {
            RemoveClient(connectionId, client);
        }
    }

    private void RemoveClient(int connectionId, TcpClient client)
    {
        foreach (var key in _drainCursors.Keys.Where(k => k.ConnectionId == connectionId).ToList())
        {
            _drainCursors.TryRemove(key, out _);
        }

        _clients.TryRemove(connectionId, out _);
        try
        {
            client.Dispose();
        }
        catch
        {
            // best-effort cleanup only
        }
    }

    /// <summary>Dispatch one request line to the read-only control surface.</summary>
    internal string Dispatch(string line, int connectionId)
    {
        object? id = null;
        try
        {
            var request = UniClawWireCodec.ParseObject(line);
            id = request["id"]?.DeepClone();

            if (!UniClawWireCodec.TryGetString(request, "method", out var method))
            {
                return UniClawWireCodec.SerializeError(id, UniClawWireContract.ErrorBadRequest, "missing or empty 'method'");
            }

            var parameters = request["params"] as JsonObject;
            var result = Invoke(method, parameters, connectionId);
            return UniClawWireCodec.SerializeResponse(id, result);
        }
        catch (UnknownMethodException ex)
        {
            return UniClawWireCodec.SerializeError(id, UniClawWireContract.ErrorUnknownMethod, ex.Message);
        }
        catch (RequestRejectedException ex)
        {
            // Deterministic start rejection (REQUEST_REJECTED): typed, distinct
            // from bad_request/internal_error; no run was created.
            return UniClawWireCodec.SerializeError(id, UniClawRunStartWire.ErrorRequestRejected, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return UniClawWireCodec.SerializeError(id, UniClawWireContract.ErrorBadRequest, ex.Message);
        }
        catch (JsonException ex)
        {
            return UniClawWireCodec.SerializeError(id, UniClawWireContract.ErrorBadRequest, $"malformed JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Fail-open: never tear down the connection or the server on one bad dispatch.
            _options.Log?.Invoke($"Dispatch failure: {ex.GetType().Name}: {ex.Message}");
            return UniClawWireCodec.SerializeError(id, UniClawWireContract.ErrorInternalError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private object Invoke(string method, JsonObject? parameters, int connectionId)
    {
        switch (method)
        {
            case "ping":
                return UniClawWireCodec.ToDto(
                    UniClawWireContract.ServiceName,
                    UniClawWireContract.ProtocolVersion,
                    UniClawWireContract.BaselineChange);

            case "run.list":
                return UniClawWireCodec.ToDto(_surface.ListRuns());

            case "run.snapshot.get":
            {
                RequireRunId(parameters, out var runId);
                return UniClawWireCodec.ToDto(_surface.InspectRun(runId));
            }

            case "run.trap.get":
            {
                RequireRunId(parameters, out var runId);
                return UniClawWireCodec.ToDto(_surface.InspectTrap(runId));
            }

            case "run.events.after":
            {
                RequireRunId(parameters, out var runId);
                var cursor = UniClawWireCodec.ParseCursor(parameters?["cursor"] as JsonObject);
                return UniClawWireCodec.ToDto(_surface.GetRuntimeEvents(runId, cursor));
            }

            case "run.events.drain":
            {
                RequireRunId(parameters, out var runId);
                return Drain(runId, connectionId);
            }

            case "evidence.get":
            {
                if (parameters?["evidenceRef"] is not JsonObject evidenceRef)
                {
                    throw new ArgumentException("missing 'evidenceRef' object");
                }

                var reference = UniClawWireCodec.ParseEvidenceRef(evidenceRef);
                return UniClawWireCodec.ToDto(_surface.OpenEvidence(reference));
            }

            case "control.support":
            {
                if (!UniClawWireCodec.TryGetString(parameters ?? throw new ArgumentException("missing 'parameters' object"), "operation", out var operation))
                {
                    throw new ArgumentException("missing or empty 'operation'");
                }

                return UniClawWireCodec.ToDto(_surface.ControlSupport(operation));
            }

            case "run.start":
            {
                // ADDITIVE execution entry (dsh-runtime-agent-subagent-run-entry):
                // validates, reserves the device, creates the DriverHost-owned
                // runId, registers the accepted run, schedules Agent execution,
                // and returns RunAccepted immediately. Never blocks on execution.
                if (_execution is null)
                {
                    throw new RequestRejectedException("run.start: no run execution seam is configured on this DriverHost");
                }

                var startRequest = UniClawRunStartWire.ParseRunStartRequest(parameters);
                var accepted = _execution.StartRun(startRequest);
                return UniClawRunStartWire.ToDto(accepted);
            }

            case "run.strategy.start":
            {
                // ADDITIVE start-time Strategy Contract entry. It is distinct from
                // run.start and from deferred mid-Run Guidance. Admission rejects
                // before a Run exists when semantics are unsupported.
                if (_strategyExecution is null)
                {
                    return UniClawStrategyRunStartWire.ToDto(
                        StrategyRunAdmission.Reject(
                            StrategyRejectionCode.UnsupportedCapability,
                            "run.strategy.start: no strategy execution seam is configured on this DriverHost"));
                }

                var strategyRequest = UniClawStrategyRunStartWire.Parse(parameters);
                return UniClawStrategyRunStartWire.ToDto(
                    _strategyExecution.StartStrategyRun(strategyRequest));
            }

            case "assistance.pending":
            {
                // ADDITIVE assistance poll (dsh-assistance-provider-adapter):
                // read-only digest of bounded pending assistance requests.
                if (_assistance is null)
                {
                    throw new RequestRejectedException("assistance.pending: no assistance surface is configured on this DriverHost");
                }

                return UniClawAssistanceWire.ToPendingDto(_assistance.Pending());
            }

            case "assistance.resolve":
            {
                // ADDITIVE assistance resolve: completes the pending request with a
                // validated advice (or rejects: unknown/terminal/stale/invalid —
                // returned as a business result, never an RPC error).
                if (_assistance is null)
                {
                    throw new RequestRejectedException("assistance.resolve: no assistance surface is configured on this DriverHost");
                }

                var resolve = UniClawAssistanceWire.ParseResolve(parameters);
                return UniClawAssistanceWire.ToResolveDto(_assistance.Resolve(resolve));
            }

            default:
                throw new UnknownMethodException(method);
        }
    }

    private object Drain(string runId, int connectionId)
    {
        var key = (connectionId, runId);
        long? lastSequence = _drainCursors.TryGetValue(key, out var existing) ? existing : null;
        var cursor = lastSequence is null ? null : new EventCursor(runId, lastSequence.Value);
        var page = _surface.GetRuntimeEvents(runId, cursor);

        var next = page.NextCursor?.LastSequence
                   ?? (page.Events.IsDefaultOrEmpty ? lastSequence : page.Events[^1].Sequence);
        if (next is not null)
        {
            _drainCursors[key] = next.Value;
        }

        return UniClawWireCodec.ToDto(page);
    }

    private static void RequireRunId(JsonObject? parameters, out string runId)
    {
        if (!UniClawWireCodec.TryGetString(parameters ?? throw new ArgumentException("missing 'parameters' object"), "runId", out runId!))
        {
            throw new ArgumentException("missing or empty 'runId'");
        }
    }

    /// <summary>Stop accepting, close clients, and reset per-connection state.</summary>
    public void Stop()
    {
        _cts.Cancel();
        _listener?.Stop();
        _listener = null;
        foreach (var client in _clients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // best-effort
            }
        }

        _clients.Clear();
        _drainCursors.Clear();
        _options.Log?.Invoke("DriverHost transport stopped");
    }

    /// <summary>Stop the server and release the listener and all client resources.</summary>
    public void Dispose()
    {
        if (_disposed == 1)
        {
            return;
        }

        Stop();
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _cts.Dispose();
    }
}

/// <summary>Typed unknown-method dispatch failure.</summary>
internal sealed class UnknownMethodException(string method) : Exception($"unknown method '{method}'")
{
    public string Method { get; } = method;
}
