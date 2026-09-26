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

    /// <summary>
    /// S1 (AGT-002 review): realization-private detach. Retires the DSH-side
    /// attachment (pending turn aborted, ProductSession↔DshSession mapping
    /// released). Purely physical/realization cleanup — it must never
    /// terminate a Product Run, create a decision, or write an Outcome.
    /// </summary>
    Task DetachAsync(CancellationToken cancellationToken);
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
    private Task<DecisionChannelAttachment>? _attachTask;
    private int _attachWaiterCount;
    private long _attachAttempt;
    private long _attachmentEpoch;
    private string? _productSessionId;
    private string? _productRunId;
    private string? _attachProductSessionId;
    private string? _attachProductRunId;
    private string? _inFlightRequestId;
    private CancellationTokenSource? _inFlightCancellation;
    private CancellationTokenSource? _attachCancellation;
    private readonly HashSet<string> _completedRequestIds = new(StringComparer.Ordinal);
    private bool _revoked;
    private bool _disposed;

    public DshOpenedDecisionChannel(
        IDshOpenedChannelPeer peer,
        TimeSpan? attachTimeout = null)
    {
        _peer = peer ?? throw new ArgumentNullException(nameof(peer));
        AttachTimeout = attachTimeout ?? TimeSpan.FromSeconds(30);
        if (AttachTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(attachTimeout));
    }

    public TimeSpan AttachTimeout { get; }

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
        Task<DecisionChannelAttachment> attachTask;
        long? attachAttempt = null;
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_attachment is not null)
            {
                if (!string.Equals(_productSessionId, request.ProductSessionId,
                        StringComparison.Ordinal)
                    || !string.Equals(_productRunId, request.ProductRunId,
                        StringComparison.Ordinal))
                    return new DecisionChannelAttachment(false, null, null,
                        "product-mapping-mismatch");
                return _attachment;
            }

            // A revoked channel may be attached again, but every caller shares
            // the same physical handshake.  A caller's cancellation only
            // cancels its wait; it never tears down the shared attempt.
            if (_attachTask is null)
            {
                var attempt = ++_attachAttempt;
                attachAttempt = attempt;
                _attachWaiterCount++;
                _attachProductSessionId = request.ProductSessionId;
                _attachProductRunId = request.ProductRunId;
                _revoked = false;
                var attachCancellation = new CancellationTokenSource(AttachTimeout);
                _attachCancellation = attachCancellation;
                var task = AttachCoreAsync(request, attempt, attachCancellation);
                _attachTask = task;
                attachTask = task;
                _ = ClearAttachTaskWhenCompleteAsync(task, attempt);
            }
            else
            {
                if (!string.Equals(_attachProductSessionId, request.ProductSessionId,
                        StringComparison.Ordinal)
                    || !string.Equals(_attachProductRunId, request.ProductRunId,
                        StringComparison.Ordinal))
                {
                    return new DecisionChannelAttachment(false, null, null,
                        "product-mapping-mismatch");
                }
                attachTask = _attachTask;
                attachAttempt = _attachAttempt;
                _attachWaiterCount++;
            }
        }

        try
        {
            var attachment = await attachTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (attachAttempt is { } attempt)
            {
                lock (_gate)
                {
                    if (_attachAttempt != attempt || _revoked
                        || (attachment.Accepted && _attachment is null))
                    {
                        return new DecisionChannelAttachment(false, null, null,
                            "attachment-revoked");
                    }
                }
                if (!attachment.Accepted)
                    ClearAttachTaskIfCurrent(attachTask, attempt);
            }
            return attachment;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled waiter does not cancel or retire the shared physical
            // handshake; another caller must still be able to join it.
            throw;
        }
        catch
        {
            if (attachAttempt is { } attempt)
                ClearAttachTaskIfCurrent(attachTask, attempt);
            throw;
        }
        finally
        {
            ReleaseAttachWaiter(attachTask, attachAttempt);
        }
    }

    private async Task<DecisionChannelAttachment> AttachCoreAsync(
        HandshakeRequest request,
        long attempt,
        CancellationTokenSource attachCancellation)
    {
        HandshakeResponse response;
        Task<HandshakeResponse>? handshakeTask = null;
        try
        {
            var attachToken = attachCancellation.Token;
            handshakeTask = _peer.HandshakeAsync(request, attachToken);
            response = await handshakeTask.WaitAsync(attachToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (attachCancellation.IsCancellationRequested)
        {
            if (handshakeTask is { IsCompleted: false })
                _ = ObserveHandshakeAsync(handshakeTask, attachCancellation);
            else
                attachCancellation.Dispose();
            return new DecisionChannelAttachment(false, null, null, "handshake-timeout");
        }
        catch (Exception ex)
        {
            attachCancellation.Dispose();
            return new DecisionChannelAttachment(false, null, null, "handshake-failed:" + ex.Message);
        }

        attachCancellation.Dispose();

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
            // Revoke invalidates an in-flight attach as well as in-flight
            // responses.  The attempt id prevents late handshake completion
            // from creating a half-initialized attachment.
            if (_attachAttempt != attempt || _revoked)
                return new DecisionChannelAttachment(false, null, null, "attachment-revoked");
            _attachment = attachment;
            _productSessionId = request.ProductSessionId;
            _productRunId = request.ProductRunId;
            _attachProductSessionId = null;
            _attachProductRunId = null;
            _attachmentEpoch++;
            return attachment;
        }
    }

    private static async Task ObserveHandshakeAsync(
        Task<HandshakeResponse> handshakeTask,
        CancellationTokenSource attachCancellation)
    {
        try { await handshakeTask.ConfigureAwait(false); }
        catch { }
        finally { attachCancellation.Dispose(); }
    }

    private async Task ClearAttachTaskWhenCompleteAsync(
        Task<DecisionChannelAttachment> task,
        long attempt)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The waiter receives the original failure; this observer only
            // guarantees that a completed failed attempt is not retained.
        }
        finally
        {
            ClearAttachTaskIfCurrent(task, attempt);
        }
    }

    private void ClearAttachTaskIfCurrent(Task<DecisionChannelAttachment> task, long attempt)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_attachTask, task)
                && _attachAttempt == attempt
                && _attachWaiterCount == 0
                && _attachment is null)
            {
                _attachTask = null;
                _attachProductSessionId = null;
                _attachProductRunId = null;
            }
        }
    }

    private void ReleaseAttachWaiter(Task<DecisionChannelAttachment> task, long? attempt)
    {
        lock (_gate)
        {
            if (attempt is not { } value
                || !ReferenceEquals(_attachTask, task)
                || _attachAttempt != value)
                return;
            if (_attachWaiterCount > 0)
                _attachWaiterCount--;
            if (task.IsCompleted)
                ClearAttachTaskIfCurrent(task, value);
        }
    }

    public async Task<DecisionChannelResponse> PushAsync(
        DecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        long attachmentEpoch;
        CancellationTokenSource turnCancellation;
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
            attachmentEpoch = _attachmentEpoch;
            _inFlightRequestId = request.RequestId;
            turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _inFlightCancellation = turnCancellation;
        }

        Task<DecisionChannelResponse>? peerTask = null;
        try
        {
            DecisionChannelResponse response;
            try
            {
                peerTask = _peer.ReceiveDecisionRequestAsync(request, turnCancellation.Token);
                response = await peerTask.WaitAsync(turnCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (_gate)
                {
                    if (_revoked || attachmentEpoch != _attachmentEpoch)
                        return new DecisionChannelResponse(request.RequestId, request.Generation,
                            null, "attachment-revoked");
                }
                return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                    "turn-aborted");
            }
            catch (Exception ex)
            {
                return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                    "channel-error:" + ex.Message);
            }

            lock (_gate)
            {
                // Revoke is an attachment-generation boundary.  A response from
                // the old generation is never allowed back through this seam.
                if (_revoked || attachmentEpoch != _attachmentEpoch)
                    return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                        "attachment-revoked");
                if (turnCancellation.IsCancellationRequested)
                    return new DecisionChannelResponse(request.RequestId, request.Generation, null,
                        "turn-aborted");
                return response;
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_inFlightCancellation, turnCancellation))
                {
                    _inFlightCancellation = null;
                    _inFlightRequestId = null;
                }
                _completedRequestIds.Add(request.RequestId);
            }

            if (peerTask is null || peerTask.IsCompleted)
                turnCancellation.Dispose();
            else
                _ = ObserveAndDisposeAsync(peerTask, turnCancellation);
        }
    }

    private static async Task ObserveAndDisposeAsync(
        Task<DecisionChannelResponse> peerTask,
        CancellationTokenSource cancellation)
    {
        try
        {
            await peerTask.ConfigureAwait(false);
        }
        catch
        {
            // The caller has already received the cancellation result. Observe
            // the abandoned peer task so a late failure cannot become unobserved.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static void CancelSafely(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The peer completed and the channel already retired its token.
        }
    }

    public async Task AbortCurrentTurnAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? turnCancellation;
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            if (_attachment is not { Accepted: true } || _revoked)
                return;
            turnCancellation = _inFlightCancellation;
        }

        CancelSafely(turnCancellation);
        var operation = _peer.AbortCurrentTurnAsync(cancellationToken);
        try
        {
            await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!operation.IsCompleted)
                _ = ObserveDetachedOperationAsync(operation);
            throw;
        }
    }

    private static async Task ObserveDetachedOperationAsync(Task operation)
    {
        try { await operation.ConfigureAwait(false); }
        catch { }
    }

    public Task RevokeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? turnCancellation;
        CancellationTokenSource? attachCancellation;
        lock (_gate)
        {
            ThrowIfDisposedLocked();
            _revoked = true;
            _attachment = null;
            _attachTask = null;
            _attachProductSessionId = null;
            _attachProductRunId = null;
            _attachWaiterCount = 0;
            _attachAttempt++;
            _attachmentEpoch++;
            turnCancellation = _inFlightCancellation;
            attachCancellation = _attachCancellation;
            _attachCancellation = null;
        }

        CancelSafely(attachCancellation);
        CancelSafely(turnCancellation);
        // The semantic fence is installed; revoke is semantically complete.
        // S1: fire the bounded realization-private detach so the DSH side also
        // retires the attachment (pending turn aborted, mapping released).
        // Peer failure cannot undo the fence and must not surface as a Product
        // lifecycle fact; the bounded physical cleanup is awaited by the
        // adapter's revoke timeout envelope.
        return AwaitPeerDetachAsync(cancellationToken);
    }

    private async Task AwaitPeerDetachAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _peer.DetachAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Bounded physical cleanup failure is realization-diagnostic
            // noise; the semantic fence already owns the transition.
        }
    }

    public async ValueTask DisposeAsync()
    {
        bool disposePeer;
        CancellationTokenSource? turnCancellation;
        CancellationTokenSource? attachCancellation;
        lock (_gate)
        {
            if (_disposed) return;
            _revoked = true;
            _attachment = null;
            _attachTask = null;
            _attachProductSessionId = null;
            _attachProductRunId = null;
            _attachWaiterCount = 0;
            _attachAttempt++;
            _attachmentEpoch++;
            _disposed = true;
            disposePeer = true;
            turnCancellation = _inFlightCancellation;
            attachCancellation = _attachCancellation;
            _attachCancellation = null;
        }
        CancelSafely(attachCancellation);
        CancelSafely(turnCancellation);
        if (disposePeer)
            await _peer.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposedLocked()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DshOpenedDecisionChannel));
    }
}
