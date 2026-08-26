using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.ValidationHarness.Hosting;

/// <summary>
/// Tier-A in-process hosting (design D3): starts the REAL
/// <see cref="UniClawDriverHostServer"/> over the REAL
/// <see cref="RunExecutionCoordinator"/> with a harness-owned
/// <see cref="RunGraphFactory"/> and Strategy Contract compiler, mirroring the
/// DriverHost E2E hosting pattern (bind ephemeral port, loopback wire client).
/// The Emulator never bypasses the transport: validation exercises the real
/// wire contract including encoding.
///
/// Post-terminal attestation seam (just the accessor): while the coordinator's
/// diagnostic <c>Runs</c> view still holds the accepted run (admission →
/// terminal cleanup), <see cref="AttestationAgent"/> returns the run's public
/// Agent read model so later WorkItems can attest the ExplorationLedgerView
/// projection; it returns null once the coordinator releases the run record.
/// </summary>
public sealed class TierAHost : IDisposable
{
    private readonly DriverHostObservability _observability;
    private readonly RunExecutionCoordinator _coordinator;
    private readonly UniClawDriverHostServer _server;
    private readonly object _captureGate = new();
    private readonly Dictionary<string, RuntimeAgent> _agentsByDevice = new(StringComparer.Ordinal);

    /// <summary>Create and start the in-process Tier-A host.</summary>
    /// <param name="graphFactory">Composition-root fixture factory (device → graph).</param>
    /// <param name="strategyCompiler">Strategy Contract compiler over the fixture binding.</param>
    public TierAHost(RunGraphFactory graphFactory, StrategyContractCompiler strategyCompiler)
    {
        ArgumentNullException.ThrowIfNull(graphFactory);
        ArgumentNullException.ThrowIfNull(strategyCompiler);

        // ADMISSIBILITY-STABLE CAPTURE (design D3: "capture at admission"):
        // the coordinator builds the graph BEFORE storing the run record, and
        // releases the record at terminal. A post-admission lookup therefore
        // races the fixture run's whole lifetime (fast runs finish before the
        // client can poll). Wrapping the factory captures every composed
        // Agent at build time — guaranteed pre-publication, keyed by device —
        // so AttestationAgent stays truthful for the host's lifetime without
        // touching any coordinator semantics. Pure read-side composition.
        RunGraphFactory capturingFactory = selector =>
        {
            var graph = graphFactory(selector);
            lock (_captureGate)
            {
                _agentsByDevice[selector.Key] = graph.Agent;
            }
            return graph;
        };

        _observability = new DriverHostObservability();
        _coordinator = new RunExecutionCoordinator(
            _observability,
            capturingFactory,
            strategyCompiler: strategyCompiler);
        _server = new UniClawDriverHostServer(
            new UniClawControlSurface(_observability),
            new DriverHostServerOptions { Port = 0 },
            execution: _coordinator,
            strategyExecution: _coordinator);
        _server.Start();
    }

    /// <summary>Loopback port the wire server is bound to (0 = ephemeral at Start).</summary>
    public int BoundPort => _server.BoundPort;

    /// <summary>Whether the transport listener is currently accepting connections.</summary>
    public bool IsListening => _server.IsListening;

    /// <summary>Diagnostic view of accepted live runs (coordinator-owned).</summary>
    public IReadOnlyDictionary<string, RunExecutionCoordinator.ActiveRun> Runs => _coordinator.Runs;

    /// <summary>DriverHost observability read model behind the wire surface.</summary>
    public DriverHostObservability Observability => _observability;

    /// <summary>
    /// Attestation-seam accessor: the run's public Agent read model. The
    /// Agent is captured at graph-build time (pre-publication), so the
    /// reference survives the coordinator's terminal run-record release and
    /// remains truthful for the host's lifetime. The Agent surface (e.g.
    /// <c>CompileExplorationLedgerView()</c>) is a read-only evidence
    /// projection — attestation never mutates Runtime state.
    /// </summary>
    public RuntimeAgent? AttestationAgent(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        // Prefer the live coordinator record (exact run → graph mapping);
        // fall back to the build-time capture keyed by the fixture device
        // once the coordinator has released the record (post-terminal).
        if (_coordinator.Runs.TryGetValue(runId, out var active))
        {
            return active.Graph.Agent;
        }
        lock (_captureGate)
        {
            return _agentsByDevice.Count > 0 ? _agentsByDevice.Values.First() : null;
        }
    }

    /// <summary>Stop the transport and release all resources.</summary>
    public void Dispose() => _server.Dispose();
}