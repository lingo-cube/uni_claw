using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>Realization-private sidecar seam. It contains no Kernel lifecycle or
/// effect authority.</summary>
public interface IDshTransport : IAsyncDisposable
{
    Task<HandshakeResponse> HandshakeAsync(HandshakeRequest request, CancellationToken cancellationToken);
    Task<DshTransportResponse> ConsultAsync(ConsultationRequest request, CancellationToken cancellationToken);
    Task AbortCurrentTurnAsync(CancellationToken cancellationToken);
}

/// <summary>Deterministic sidecar double for protocol and lifecycle tests.</summary>
public sealed class FakeDshTransport : IDshTransport
{
    private readonly Func<ConsultationRequest, DshTransportResponse> _responder;
    private readonly TimeSpan _delay;
    private readonly bool _ignoreCancellation;
    private bool _disposed;

    public FakeDshTransport(
        Func<ConsultationRequest, DshTransportResponse> responder,
        TimeSpan? delay = null,
        bool ignoreCancellation = false,
        string dshSessionId = "dsh-fake-session")
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        _delay = delay ?? TimeSpan.Zero;
        _ignoreCancellation = ignoreCancellation;
        DshSessionId = dshSessionId;
    }

    public string DshSessionId { get; }
    public int HandshakeCount { get; private set; }
    public int ConsultationCount { get; private set; }
    public int AbortCount { get; private set; }
    public List<ConsultationRequest> Requests { get; } = new();

    public Task<HandshakeResponse> HandshakeAsync(HandshakeRequest request,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        HandshakeCount++;
        return Task.FromResult(new HandshakeResponse(
            true,
            request.Protocol,
            request.ExpectedCapabilities,
            DshSessionId));
    }

    public async Task<DshTransportResponse> ConsultAsync(ConsultationRequest request,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ConsultationCount++;
        Requests.Add(request);
        if (_delay > TimeSpan.Zero)
        {
            if (_ignoreCancellation)
                await Task.Delay(_delay).ConfigureAwait(false);
            else
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        }
        return _responder(request);
    }

    public Task AbortCurrentTurnAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        AbortCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FakeDshTransport));
    }
}

/// <summary>Replay transport. Replays serialized AgentDecision records keyed by the
/// Kernel-created DecisionId; it never replays a transcript or reality state.</summary>
public sealed class ReplayDshTransport : IDshTransport
{
    private readonly IReadOnlyDictionary<string, string> _decisions;
    private readonly System.Text.Json.JsonSerializerOptions _json = ProductProtocolJson.CreateOptions();
    private readonly string _sessionId;

    public ReplayDshTransport(IReadOnlyDictionary<string, string> decisions,
        string dshSessionId = "dsh-replay-session")
    {
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _sessionId = dshSessionId;
    }

    public Task<HandshakeResponse> HandshakeAsync(HandshakeRequest request,
        CancellationToken cancellationToken) => Task.FromResult(new HandshakeResponse(
            true, request.Protocol, request.ExpectedCapabilities, _sessionId));

    public Task<DshTransportResponse> ConsultAsync(ConsultationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_decisions.TryGetValue(request.Context.DecisionId, out var payload))
            return Task.FromResult(new DshTransportResponse(request.RequestId, request.Generation,
                null, "replay-decision-not-found"));
        try
        {
            var decision = System.Text.Json.JsonSerializer.Deserialize<AgentDecision>(payload, _json);
            return Task.FromResult(new DshTransportResponse(request.RequestId, request.Generation,
                decision, decision is null ? "replay-decision-null" : null));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return Task.FromResult(new DshTransportResponse(request.RequestId, request.Generation,
                null, "replay-malformed-decision:" + ex.Message));
        }
    }

    public Task AbortCurrentTurnAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
