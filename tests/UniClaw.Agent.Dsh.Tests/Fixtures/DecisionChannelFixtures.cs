using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests.Fixtures;

public sealed class DeterministicDshPeer : IDshOpenedChannelPeer
{
    private readonly Func<DecisionRequest, DecisionChannelResponse> _response;
    private readonly TimeSpan _delay;
    private readonly TimeSpan _handshakeDelay;
    private readonly TaskCompletionSource<bool>? _handshakeGate;
    private readonly bool _ignoreCancellation;
    private readonly Func<HandshakeRequest, HandshakeResponse>? _handshake;
    private readonly TaskCompletionSource<bool>? _decisionResponseGate;
    private readonly TaskCompletionSource<bool>? _abortGate;
    private readonly TaskCompletionSource<bool> _handshakeStarted = NewSignal();
    private readonly TaskCompletionSource<bool> _decisionRequestStarted = NewSignal();
    private readonly TaskCompletionSource<bool> _decisionResponseReturned = NewSignal();
    private readonly TaskCompletionSource<bool> _abortStarted = NewSignal();

    public DeterministicDshPeer(
        Func<DecisionRequest, DecisionChannelResponse> response,
        TimeSpan? delay = null,
        bool ignoreCancellation = false,
        string sessionId = "dsh-test",
        Func<HandshakeRequest, HandshakeResponse>? handshake = null,
        TimeSpan? handshakeDelay = null,
        TaskCompletionSource<bool>? decisionResponseGate = null,
        TaskCompletionSource<bool>? abortGate = null,
        TaskCompletionSource<bool>? handshakeGate = null)
    {
        _response = response;
        _delay = delay ?? TimeSpan.Zero;
        _ignoreCancellation = ignoreCancellation;
        DshSessionId = sessionId;
        _handshake = handshake;
        _handshakeDelay = handshakeDelay ?? TimeSpan.Zero;
        _handshakeGate = handshakeGate;
        _decisionResponseGate = decisionResponseGate;
        _abortGate = abortGate;
    }

    public string DshSessionId { get; }
    private int _handshakeCount;
    private int _requestCount;
    private int _abortCount;
    private int _detachCount;
    private readonly TaskCompletionSource<bool> _detached = NewSignal();
    public int DetachCount => _detachCount;
    public Task Detached => _detached.Task;
    public int HandshakeCount => Volatile.Read(ref _handshakeCount);
    public int RequestCount => Volatile.Read(ref _requestCount);
    public int AbortCount => Volatile.Read(ref _abortCount);
    public Task HandshakeStarted => _handshakeStarted.Task;
    public Task DecisionRequestStarted => _decisionRequestStarted.Task;
    public Task DecisionResponseReturned => _decisionResponseReturned.Task;
    public Task AbortStarted => _abortStarted.Task;

    public void ReleaseHandshake() => _handshakeGate?.TrySetResult(true);
    public void ReleaseDecisionResponse() => _decisionResponseGate?.TrySetResult(true);
    public void ReleaseAbort() => _abortGate?.TrySetResult(true);

    public async Task<HandshakeResponse> HandshakeAsync(
        HandshakeRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _handshakeCount);
        _handshakeStarted.TrySetResult(true);
        if (_handshakeGate is not null)
            await _handshakeGate.Task.ConfigureAwait(false);
        else if (_handshakeDelay > TimeSpan.Zero)
            await Task.Delay(_handshakeDelay, cancellationToken).ConfigureAwait(false);
        return _handshake?.Invoke(request)
            ?? new HandshakeResponse(true, request.Protocol, request.ExpectedCapabilities, DshSessionId);
    }

    public async Task<DecisionChannelResponse> ReceiveDecisionRequestAsync(
        DecisionRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        _decisionRequestStarted.TrySetResult(true);
        if (_decisionResponseGate is not null)
            await _decisionResponseGate.Task.ConfigureAwait(false);
        else if (_delay > TimeSpan.Zero)
            await (_ignoreCancellation
                ? Task.Delay(_delay)
                : Task.Delay(_delay, cancellationToken)).ConfigureAwait(false);

        var response = _response(request);
        _decisionResponseReturned.TrySetResult(true);
        return response;
    }

    public async Task AbortCurrentTurnAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _abortCount);
        _abortStarted.TrySetResult(true);
        if (_abortGate is not null)
            await _abortGate.Task.ConfigureAwait(false);
    }

    public Task DetachAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _detachCount);
        _detached.TrySetResult(true);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public static class DecisionChannelFixture
{
    public static DshOpenedDecisionChannel Open(Func<DecisionRequest, DecisionChannelResponse> response, TimeSpan? delay = null, bool ignoreCancellation = false)
        => new(new DeterministicDshPeer(response, delay, ignoreCancellation));

    public static DshOpenedDecisionChannel Replay(IReadOnlyDictionary<string, string> decisions)
        => Open(request => {
            if (!decisions.TryGetValue(request.Context.DecisionId, out var json)) return new(request.RequestId, request.Generation, null, "replay-decision-not-found");
            try { var d = JsonSerializer.Deserialize<AgentDecision>(json, ProductProtocolJson.CreateOptions()); return new(request.RequestId, request.Generation, d, d is null ? "replay-decision-null" : null); }
            catch (Exception ex) when (ex is JsonException or NotSupportedException) { return new(request.RequestId, request.Generation, null, "replay-malformed-decision:" + ex.Message); }
        });
}

public class StdioDecisionChannel : IDecisionChannel
{
    private readonly DshOpenedDecisionChannel _inner;
    public StdioDecisionChannel(DshProcessOptions options) => _inner = new(new StdioJsonRpcPeer(options));
    public StdioDecisionChannel(IDshOpenedChannelPeer peer) => _inner = new(peer);
    public Task<DecisionChannelAttachment> AttachAsync(HandshakeRequest request, CancellationToken cancellationToken) => _inner.AttachAsync(request, cancellationToken);
    public Task<DecisionChannelResponse> PushAsync(DecisionRequest request, CancellationToken cancellationToken) => _inner.PushAsync(request, cancellationToken);
    public Task AbortCurrentTurnAsync(CancellationToken cancellationToken) => _inner.AbortCurrentTurnAsync(cancellationToken);
    public Task RevokeAsync(CancellationToken cancellationToken) => _inner.RevokeAsync(cancellationToken);
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

public sealed class FakeDecisionChannel : IDecisionChannel
{
    private readonly DshOpenedDecisionChannel _inner;
    public FakeDecisionChannel(Func<DecisionRequest, DecisionChannelResponse> response, TimeSpan? delay = null, bool ignoreCancellation = false) => _inner = new(new DeterministicDshPeer(response, delay, ignoreCancellation));
    public FakeDecisionChannel(IDshOpenedChannelPeer peer) => _inner = new(peer);
    public Task<DecisionChannelAttachment> AttachAsync(HandshakeRequest request, CancellationToken cancellationToken) => _inner.AttachAsync(request, cancellationToken);
    public Task<DecisionChannelResponse> PushAsync(DecisionRequest request, CancellationToken cancellationToken) => _inner.PushAsync(request, cancellationToken);
    public Task AbortCurrentTurnAsync(CancellationToken cancellationToken) => _inner.AbortCurrentTurnAsync(cancellationToken);
    public Task RevokeAsync(CancellationToken cancellationToken) => _inner.RevokeAsync(cancellationToken);
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

public sealed class ReplayDecisionChannel : IDecisionChannel
{
    private readonly DshOpenedDecisionChannel _inner;
    public ReplayDecisionChannel(Func<DecisionRequest, DecisionChannelResponse> response) =>
        _inner = new(new DeterministicDshPeer(response));
    public ReplayDecisionChannel(IDshOpenedChannelPeer peer) => _inner = new(peer);
    public ReplayDecisionChannel(IReadOnlyDictionary<string, string> decisions) => _inner = DecisionChannelFixture.Open(r =>
    {
        if (!decisions.TryGetValue(r.Context.DecisionId, out var payload))
            return new(r.RequestId, r.Generation, null, "replay-decision-not-found");
        try
        {
            var decision = JsonSerializer.Deserialize<AgentDecision>(payload, ProductProtocolJson.CreateOptions());
            return new(r.RequestId, r.Generation, decision, decision is null ? "replay-decision-null" : null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return new(r.RequestId, r.Generation, null, "replay-malformed-decision:" + ex.Message);
        }
    });
    public Task<DecisionChannelAttachment> AttachAsync(HandshakeRequest request, CancellationToken cancellationToken) => _inner.AttachAsync(request, cancellationToken);
    public Task<DecisionChannelResponse> PushAsync(DecisionRequest request, CancellationToken cancellationToken) => _inner.PushAsync(request, cancellationToken);
    public Task AbortCurrentTurnAsync(CancellationToken cancellationToken) => _inner.AbortCurrentTurnAsync(cancellationToken);
    public Task RevokeAsync(CancellationToken cancellationToken) => _inner.RevokeAsync(cancellationToken);
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
