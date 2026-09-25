using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests.Fixtures;

public sealed class DeterministicDshPeer : IDshOpenedChannelPeer
{
    private readonly Func<DecisionRequest, DecisionChannelResponse> _response;
    private readonly TimeSpan _delay;
    private readonly bool _ignoreCancellation;
    private readonly Func<HandshakeRequest, HandshakeResponse>? _handshake;
    public DeterministicDshPeer(Func<DecisionRequest, DecisionChannelResponse> response, TimeSpan? delay = null, bool ignoreCancellation = false, string sessionId = "dsh-test", Func<HandshakeRequest, HandshakeResponse>? handshake = null)
    { _response = response; _delay = delay ?? TimeSpan.Zero; _ignoreCancellation = ignoreCancellation; DshSessionId = sessionId; _handshake = handshake; }
    public string DshSessionId { get; }
    public int HandshakeCount { get; private set; }
    public int RequestCount { get; private set; }
    public int AbortCount { get; private set; }
    public bool Revoked { get; set; }
    public async Task<HandshakeResponse> HandshakeAsync(HandshakeRequest request, CancellationToken cancellationToken)
    { HandshakeCount++; return _handshake?.Invoke(request) ?? new(true, request.Protocol, request.ExpectedCapabilities, DshSessionId); }
    public async Task<DecisionChannelResponse> ReceiveDecisionRequestAsync(DecisionRequest request, CancellationToken cancellationToken)
    { RequestCount++; if (_delay > TimeSpan.Zero) await (_ignoreCancellation ? Task.Delay(_delay) : Task.Delay(_delay, cancellationToken)); return _response(request); }
    public Task AbortCurrentTurnAsync(CancellationToken cancellationToken) { AbortCount++; return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
