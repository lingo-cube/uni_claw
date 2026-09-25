using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// Product semantic seam for a DSH realization. It deliberately contains no
/// process, stdio, JSON-RPC, or DSH session lifecycle details.
/// </summary>
public interface IDecisionChannel : IAsyncDisposable
{
    Task<DecisionChannelAttachment> AttachAsync(
        HandshakeRequest request,
        CancellationToken cancellationToken);

    /// <summary>Kernel pushes one already-created DecisionRequest to the attached
    /// realization. The call returns only the realization response; it does not
    /// create a Product Run or a new semantic request.</summary>
    Task<DecisionChannelResponse> PushAsync(
        DecisionRequest request,
        CancellationToken cancellationToken);

    /// <summary>Mechanical turn interruption. It has no Product lifecycle meaning.</summary>
    Task AbortCurrentTurnAsync(CancellationToken cancellationToken);

    /// <summary>Revokes only this realization's semantic attachment. It does not
    /// cancel or terminate the Product Run.</summary>
    Task RevokeAsync(CancellationToken cancellationToken);
}

public sealed record DecisionChannelAttachment(
    bool Accepted,
    string? AttachmentId,
    string? DshSessionId,
    string? FailureReason = null);

/// <summary>One Kernel-created decision opportunity crossing the Product seam.</summary>
public sealed record DecisionRequest(
    string RequestId,
    long Generation,
    string ProductSessionId,
    string ProductRunId,
    AgentDecisionContext Context);

public sealed record DecisionChannelResponse(
    string RequestId,
    long Generation,
    AgentDecision? Decision,
    string? Error = null);

/// <summary>
/// The implementation-side peer for a DSH-opened physical channel. This is a
/// realization detail, not part of the Product seam exposed to Kernel callers.
/// </summary>
public interface IDshOpenedChannelPeer : IAsyncDisposable
{
    Task<HandshakeResponse> HandshakeAsync(
        HandshakeRequest request,
        CancellationToken cancellationToken);

    Task<DecisionChannelResponse> ReceiveDecisionRequestAsync(
        DecisionRequest request,
        CancellationToken cancellationToken);

    Task AbortCurrentTurnAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Formal Product realization. DSH opens the physical channel; this object owns
/// only the Product semantic attachment and bounded decision turn forwarding.
/// </summary>
public sealed class DshOpenedDecisionChannel : IDecisionChannel
{
    private readonly IDshOpenedChannelPeer _peer;
    private readonly object _gate = new();
    private DecisionChannelAttachment? _attachment;
    private string? _productSessionId;
    private string? _productRunId;
    private string? _inFlightRequestId;
    private readonly HashSet<string> _completedRequestIds = new(StringComparer.Ordinal);
    private bool _revoked;
    private bool _disposed;

    public DshOpenedDecisionChannel(IDshOpenedChannelPeer peer)
    {
        _peer = peer ?? throw new ArgumentNullException(nameof(peer));
    }

    public string? AttachmentId
    {
        get { lock (_gate) return _attachment?.AttachmentId; }
    }

    public bool IsAttached
    {
        get { lock (_gate) return _attachment is { Accepted: true } && !_revoked; }
    }

    public bool IsRevoked
    {
        get { lock (_gate) return _revoked; }
    }

    public async Task<DecisionChannelAttachment> AttachAsync(
        HandshakeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_attachment is not null)
            {
                if (_revoked)
                    return new DecisionChannelAttachment(false, null, null, "attachment-revoked");
                if (!string.Equals(_productSessionId, request.ProductSessionId,
                        StringComparison.Ordinal)
                    || !string.Equals(_productRunId, request.ProductRunId,
                        StringComparison.Ordinal))
                    return new DecisionChannelAttachment(false, null, null,
                        "product-mapping-mismatch");
                return _attachment;
            }
        }

        HandshakeResponse response;
        try
        {
            response = await _peer.HandshakeAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new DecisionChannelAttachment(false, null, null,
                "handshake-failed:" + ex.Message);
        }

        var validation = ProductHandshake.Validate(request, response);
        if (!validation.Accepted)
            return new DecisionChannelAttachment(false, null, response.DshSessionId,
                validation.FailureReason);

        var attachment = new DecisionChannelAttachment(
            true,
            $"attachment-{Guid.NewGuid():N}",
            response.DshSessionId);
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_revoked)
                return new DecisionChannelAttachment(false, null, null, "attachment-revoked");
            _attachment = attachment;
            _productSessionId = request.ProductSessionId;
            _productRunId = request.ProductRunId;
            return attachment;
        }
    }

    public async Task<DecisionChannelResponse> PushAsync(
        DecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_attachment is not { Accepted: true } || _revoked)
                return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                    "channel-not-attached");
            if (!string.Equals(_productSessionId, request.ProductSessionId,
                    StringComparison.Ordinal)
                || !string.Equals(_productRunId, request.ProductRunId,
                    StringComparison.Ordinal))
                return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                    "product-mapping-mismatch");
            if (_completedRequestIds.Contains(request.RequestId))
                return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                    "duplicate-request");
            if (_inFlightRequestId is not null)
                return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                    "one-in-flight");
            _inFlightRequestId = request.RequestId;
        }

        try
        {
            return await _peer.ReceiveDecisionRequestAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                "turn-aborted");
        }
        catch (Exception ex)
        {
            return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                "channel-error:" + ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                _inFlightRequestId = null;
                _completedRequestIds.Add(request.RequestId);
            }
        }
    }

    public async Task AbortCurrentTurnAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_attachment is not { Accepted: true } || _revoked)
                return;
        }
        await _peer.AbortCurrentTurnAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task RevokeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            _revoked = true;
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        bool disposePeer;
        lock (_gate)
        {
            if (_disposed) return;
            _revoked = true;
            _disposed = true;
            disposePeer = true;
        }
        if (disposePeer)
            await _peer.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposedLocked()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DshOpenedDecisionChannel));
    }
}
