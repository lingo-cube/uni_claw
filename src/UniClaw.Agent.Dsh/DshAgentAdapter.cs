using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// Product-side supervisor for a DSH realization. It owns only the realization
/// lifecycle and decision-channel guards. Kernel remains the owner of when a
/// DecisionRequest is created and what happens after a decision is returned.
/// </summary>
public sealed class DshAgentAdapter : IDisposable, IAsyncDisposable
{
    private readonly IDecisionChannel _channel;
    private readonly TimeSpan _turnTimeout;
    private readonly TimeSpan _attachTimeout;
    private readonly TimeSpan _abortTimeout;
    private readonly TimeSpan _revokeTimeout;
    private readonly object _gate = new();
    private readonly List<DshDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _retiredRequests = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedRequests = new(StringComparer.Ordinal);
    private bool _handshakeValidated;
    private long _attachmentEpoch;
    private bool _revocationInProgress;
    private bool _disposed;
    private bool _active;
    private long _generation;
    private int _requestSequence;
    private string? _activeRequestId;
    private string? _activeDecisionId;
    private TurnState? _activeTurn;
    private Task? _lastAbortTask;

    private sealed class TurnState
    {
        public TurnState(CancellationTokenSource cancellation) => Cancellation = cancellation;
        public CancellationTokenSource Cancellation { get; }
        public bool AbortRequested { get; set; }
        public Task? AbortTask { get; set; }
    }

    public DshAgentAdapter(
        IDecisionChannel channel,
        string productSessionId,
        string productRunId,
        TimeSpan? turnTimeout = null,
        TimeSpan? attachTimeout = null,
        TimeSpan? abortTimeout = null,
        TimeSpan? revokeTimeout = null)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        ProductSessionId = Require(productSessionId, nameof(productSessionId));
        ProductRunId = Require(productRunId, nameof(productRunId));
        _turnTimeout = turnTimeout ?? TimeSpan.FromSeconds(60);
        if (_turnTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(turnTimeout));
        _attachTimeout = attachTimeout ?? TimeSpan.FromSeconds(30);
        _abortTimeout = abortTimeout ?? TimeSpan.FromSeconds(30);
        _revokeTimeout = revokeTimeout ?? TimeSpan.FromSeconds(30);
        if (_attachTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(attachTimeout));
        if (_abortTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(abortTimeout));
        if (_revokeTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(revokeTimeout));
    }

    public string ProductSessionId { get; }
    public string ProductRunId { get; }
    public string? DshSessionId { get; private set; }
    public int SemanticConsultationsStarted { get; private set; }
    public IReadOnlyList<DshDiagnostic> Diagnostics
    {
        get { lock (_gate) return _diagnostics.ToArray(); }
    }

    /// <summary>Compatibility seam for the existing synchronous Product Host
    /// composition. The underlying transport still uses an async bounded turn.</summary>
    public AgentDecision? Consult(AgentDecisionContext context) =>
        ConsultAsync(context).GetAwaiter().GetResult();

    public async Task<AgentDecision?> ConsultAsync(
        AgentDecisionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ThrowIfDisposed();
        if (!string.Equals(context.RunId, ProductRunId, StringComparison.Ordinal))
        {
            AddDiagnostic(new DshDiagnostic("run-mismatch",
                $"context RunId '{context.RunId}' does not belong to Product Run '{ProductRunId}'",
                DecisionId: context.DecisionId));
            return null;
        }

        long consultationEpoch;
        lock (_gate)
        {
            if (_revocationInProgress)
            {
                AddDiagnosticLocked(new DshDiagnostic("attachment-revoked",
                    "a Product consultation cannot start while realization revoke is in progress",
                    DecisionId: context.DecisionId));
                return null;
            }
            consultationEpoch = _attachmentEpoch;
        }
        await WaitForPriorAbortAsync(cancellationToken).ConfigureAwait(false);
        var attached = await EnsureAttachmentAsync(cancellationToken, consultationEpoch).ConfigureAwait(false);
        if (!attached)
            return null;

        DecisionRequest request;
        TurnState turn;
        lock (_gate)
        {
            if (_active)
            {
                AddDiagnosticLocked(new DshDiagnostic("active-consultation-exists",
                    "a Product Run may have at most one active DSH consultation",
                    _activeRequestId, _generation, context.DecisionId));
                return null;
            }
            if (!_handshakeValidated || _attachmentEpoch != consultationEpoch || _disposed)
            {
                AddDiagnosticLocked(new DshDiagnostic("attachment-revoked",
                    "the realization attachment was revoked before the consultation started",
                    DecisionId: context.DecisionId));
                return null;
            }

            _active = true;
            _generation++;
            var generation = _generation;
            var requestId = $"dsh-req-{++_requestSequence:D8}";
            _activeRequestId = requestId;
            _activeDecisionId = context.DecisionId;
            turn = new TurnState(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            turn.Cancellation.CancelAfter(_turnTimeout);
            _activeTurn = turn;
            SemanticConsultationsStarted++;
            request = new DecisionRequest(requestId, generation, ProductSessionId,
                ProductRunId, context);
        }

        Task<DecisionChannelResponse> operation;
        try
        {
            operation = _channel.PushAsync(request, turn.Cancellation.Token);
        }
        catch (Exception ex)
        {
            CloseActive(request, "provider-error", ex.Message);
            turn.Cancellation.Dispose();
            return null;
        }

        try
        {
            // WaitAsync makes the adapter's own await cancellation-aware.  The
            // peer may ignore the token; the consultation still returns on the
            // linked turn boundary and the late task is quarantined below.
            var response = await operation.WaitAsync(turn.Cancellation.Token).ConfigureAwait(false);
            if (turn.Cancellation.IsCancellationRequested)
            {
                var code = CancellationCode(cancellationToken, turn);
                RetireActive(request, code, code == "timeout" ? "turn deadline elapsed" : "turn was cancelled");
                await AwaitAbortAsync(turn).ConfigureAwait(false);
                _ = ObserveLateResponseAsync(operation, request, turn.Cancellation);
                return null;
            }
            var accepted = AcceptTransportResponse(request, response);
            turn.Cancellation.Dispose();
            return accepted;
        }
        catch (OperationCanceledException)
        {
            var code = CancellationCode(cancellationToken, turn);
            RetireActive(request, code, code == "timeout" ? "turn deadline elapsed" : "turn was cancelled");
            await AwaitAbortAsync(turn).ConfigureAwait(false);
            _ = ObserveLateResponseAsync(operation, request, turn.Cancellation);
            return null;
        }
        catch (Exception ex)
        {
            CloseActive(request, "provider-error", ex.Message);
            turn.Cancellation.Dispose();
            return null;
        }
    }

    /// <summary>Mechanical interruption only. This does not cancel a Product Run,
    /// invalidate a lease, write an outcome, or create a decision.</summary>
    public void AbortCurrentTurn() => AbortCurrentTurnAsync().GetAwaiter().GetResult();

    public async Task AbortCurrentTurnAsync(CancellationToken cancellationToken = default)
    {
        TurnState? turn;
        Task abortTask;
        lock (_gate)
        {
            if (!_active || _activeTurn is null)
            {
                if (_lastAbortTask is null)
                    return;
                abortTask = _lastAbortTask;
                turn = null;
            }
            else
            {
                turn = _activeTurn;
                _generation++;
                _active = false;
                if (_activeRequestId is not null) _retiredRequests.Add(_activeRequestId);
                AddDiagnosticLocked(new DshDiagnostic("turn-aborted",
                    "AbortCurrentTurn terminated the realization turn only", _activeRequestId, _generation,
                    _activeDecisionId));
                _activeRequestId = null;
                _activeDecisionId = null;
                _activeTurn = null;
                turn.AbortRequested = true;
                turn.AbortTask ??= StartAbortTaskLocked();
                _lastAbortTask = turn.AbortTask;
                abortTask = turn.AbortTask;
            }
        }

        if (turn is not null)
            CancelSafely(turn.Cancellation);
        await abortTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Revokes only this realization's semantic attachment. The Product
    /// Run remains owned by Kernel and is not cancelled or terminated.</summary>
    public async Task RevokeAttachmentAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        // Install the semantic fence before touching any physical cleanup. A
        // concurrent attach therefore cannot make a new Product request valid.
        lock (_gate)
        {
            _attachmentEpoch++;
            _handshakeValidated = false;
            DshSessionId = null;
            _revocationInProgress = true;
        }

        try
        {
            await AbortCurrentTurnAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddDiagnostic(new DshDiagnostic("revoke-wait-cancelled",
                "revoke caller stopped waiting after the semantic fence was installed"));
        }

        try
        {
            await AwaitBoundedControlAsync(
                token => _channel.RevokeAsync(token), _revokeTimeout, "revoke", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddDiagnostic(new DshDiagnostic("revoke-wait-cancelled",
                "revoke caller stopped waiting after the semantic fence was installed"));
        }
        finally
        {
            lock (_gate)
            {
                _attachmentEpoch++;
                _handshakeValidated = false;
                DshSessionId = null;
                _revocationInProgress = false;
                AddDiagnosticLocked(new DshDiagnostic(
                    "attachment-revoked",
                    "DSH realization attachment was revoked; Product Run remains Kernel-owned"));
            }
        }
    }

    private async Task<bool> EnsureAttachmentAsync(
        CancellationToken cancellationToken,
        long expectedAttachmentEpoch)
    {
        using var attachWaitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attachWaitCancellation.CancelAfter(_attachTimeout);
        long attachmentEpoch;
            lock (_gate)
            {
                if (_attachmentEpoch != expectedAttachmentEpoch)
                    return false;
                if (_revocationInProgress)
                    return false;
                if (_handshakeValidated) return true;
                attachmentEpoch = _attachmentEpoch;
            }

            var request = ProductHandshake.CreateRequest(ProductSessionId, ProductRunId);
            DecisionChannelAttachment attachment;
            try
            {
                attachment = await _channel.AttachAsync(request, attachWaitCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (attachWaitCancellation.IsCancellationRequested)
            {
                AddDiagnostic(new DshDiagnostic(
                    cancellationToken.IsCancellationRequested ? "attachment-cancelled" : "attachment-timeout",
                    cancellationToken.IsCancellationRequested
                        ? "the attach waiter was cancelled"
                        : "the realization attach deadline elapsed"));
                return false;
            }
            catch (Exception ex)
            {
                AddDiagnostic(new DshDiagnostic("attachment-failed", ex.Message));
                return false;
            }

            if (!attachment.Accepted)
            {
                var code = string.Equals(attachment.FailureReason, "handshake-timeout",
                    StringComparison.Ordinal) ? "attachment-timeout" : "handshake-rejected";
                AddDiagnostic(new DshDiagnostic(code,
                    attachment.FailureReason ?? "channel attachment rejected"));
                return false;
            }
            lock (_gate)
            {
                if (_attachmentEpoch != attachmentEpoch
                    || _attachmentEpoch != expectedAttachmentEpoch
                    || _disposed)
                    return false;
                _handshakeValidated = true;
                DshSessionId = attachment.DshSessionId;
            }
        return true;
    }

    private AgentDecision? AcceptTransportResponse(DecisionRequest request,
        DecisionChannelResponse response)
    {
        lock (_gate)
        {
            if (_completedRequests.Contains(response.RequestId))
            {
                AddDiagnosticLocked(new DshDiagnostic("duplicate-response",
                    "a second response for a completed transport request was dropped",
                    response.RequestId, response.Generation, request.Context.DecisionId));
                return null;
            }
            if (!_active || !string.Equals(_activeRequestId, request.RequestId, StringComparison.Ordinal))
            {
                AddDiagnosticLocked(new DshDiagnostic(
                    _retiredRequests.Contains(response.RequestId) ? "late-response" : "stale-response",
                    "response arrived after the active turn was retired", response.RequestId,
                    response.Generation, request.Context.DecisionId));
                return null;
            }
            if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal)
                || response.Generation != request.Generation)
            {
                AddDiagnosticLocked(new DshDiagnostic("stale-response",
                    "transport request id or generation is stale", response.RequestId,
                    response.Generation, request.Context.DecisionId));
                RetireActiveLocked(request, "stale-response", "correlation mismatch");
                return null;
            }
            if (response.Decision is null)
            {
                AddDiagnosticLocked(new DshDiagnostic("no-decision",
                    response.Error ?? "sidecar returned no submit_decision result",
                    response.RequestId, response.Generation, request.Context.DecisionId));
                RetireActiveLocked(request, "no-decision", response.Error ?? "no decision");
                return null;
            }

            var returnedDecisionId = AgentDecisionCorrelation.TryGetDecisionId(response.Decision);
            if (!string.Equals(returnedDecisionId, request.Context.DecisionId,
                    StringComparison.Ordinal))
            {
                AddDiagnosticLocked(new DshDiagnostic("decision-correlation-mismatch",
                    "decision response did not echo the active Product DecisionId",
                    response.RequestId, response.Generation, returnedDecisionId));
                RetireActiveLocked(request, "correlation-mismatch", "DecisionId mismatch");
                return null;
            }

            _completedRequests.Add(request.RequestId);
            _active = false;
            _activeRequestId = null;
            _activeDecisionId = null;
            _activeTurn = null;
            return response.Decision;
        }
    }

    private async Task ObserveLateResponseAsync(Task<DecisionChannelResponse> operation,
        DecisionRequest request, CancellationTokenSource cancellation)
    {
        try
        {
            var response = await operation.ConfigureAwait(false);
            AcceptTransportResponse(request, response);
        }
        catch (Exception ex)
        {
            AddDiagnostic(new DshDiagnostic("late-transport-error", ex.Message,
                request.RequestId, request.Generation, request.Context.DecisionId));
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private Task StartAbortTaskLocked()
    {
        return ObserveAbortAsync();
    }

    private async Task ObserveAbortAsync()
    {
        using var abortCancellation = new CancellationTokenSource(_abortTimeout);
        Task? operation = null;
        try
        {
            operation = _channel.AbortCurrentTurnAsync(abortCancellation.Token);
            await operation.WaitAsync(abortCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (abortCancellation.IsCancellationRequested)
        {
            AddDiagnostic(new DshDiagnostic("abort-timeout",
                "the realization did not acknowledge AbortCurrentTurn before its deadline"));
            if (operation is { IsCompleted: false })
                _ = ObserveDetachedOperationAsync(operation);
        }
        catch (Exception ex)
        {
            AddDiagnostic(new DshDiagnostic("abort-transport-error", ex.Message));
        }
    }

    private async Task AwaitBoundedControlAsync(
        Func<CancellationToken, Task> operationFactory,
        TimeSpan timeout,
        string operationName,
        CancellationToken externalCancellation)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        timeoutCancellation.CancelAfter(timeout);
        Task? operation = null;
        try
        {
            operation = operationFactory(timeoutCancellation.Token);
            await operation.WaitAsync(timeoutCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
            var code = externalCancellation.IsCancellationRequested
                ? operationName + "-cancelled"
                : operationName + "-timeout";
            AddDiagnostic(new DshDiagnostic(code,
                externalCancellation.IsCancellationRequested
                    ? $"the caller stopped waiting for realization {operationName}"
                    : $"the realization {operationName} did not complete before its deadline"));
            if (operation is { IsCompleted: false })
                _ = ObserveDetachedOperationAsync(operation);
        }
    }

    private static async Task ObserveDetachedOperationAsync(Task operation)
    {
        try { await operation.ConfigureAwait(false); }
        catch { }
    }

    private async Task AwaitAbortAsync(TurnState turn)
    {
        Task abortTask;
        lock (_gate)
        {
            turn.AbortTask ??= StartAbortTaskLocked();
            _lastAbortTask = turn.AbortTask;
            abortTask = turn.AbortTask;
        }
        await abortTask.ConfigureAwait(false);
    }

    private async Task WaitForPriorAbortAsync(CancellationToken cancellationToken)
    {
        Task? abortTask;
        lock (_gate) abortTask = _lastAbortTask;
        if (abortTask is not null)
            await abortTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private string CancellationCode(CancellationToken externalCancellation, TurnState turn)
    {
        lock (_gate)
        {
            if (turn.AbortRequested || externalCancellation.IsCancellationRequested)
                return "aborted";
            return "timeout";
        }
    }

    private void CloseActive(DecisionRequest request, string code, string message)
    {
        lock (_gate)
        {
            if (_active && string.Equals(_activeRequestId, request.RequestId, StringComparison.Ordinal))
                RetireActiveLocked(request, code, message);
            else
                AddDiagnosticLocked(new DshDiagnostic(code, message, request.RequestId,
                    request.Generation, request.Context.DecisionId));
        }
    }

    private void RetireActive(DecisionRequest request, string code, string message)
    {
        lock (_gate)
        {
            if (_active && string.Equals(_activeRequestId, request.RequestId, StringComparison.Ordinal))
                RetireActiveLocked(request, code, message);
        }
    }

    private void RetireActiveLocked(DecisionRequest request, string code, string message)
    {
        _active = false;
        _retiredRequests.Add(request.RequestId);
        AddDiagnosticLocked(new DshDiagnostic(code, message, request.RequestId,
            request.Generation, request.Context.DecisionId));
        _activeRequestId = null;
        _activeDecisionId = null;
        _activeTurn = null;
    }

    private static void CancelSafely(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The turn completed between the active-state check and cancellation.
        }
    }

    private void AddDiagnostic(DshDiagnostic diagnostic)
    {
        lock (_gate) AddDiagnosticLocked(diagnostic);
    }

    private void AddDiagnosticLocked(DshDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public void Dispose()
    {
        if (_disposed) return;
        AbortCurrentTurn();
        _channel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        AbortCurrentTurn();
        await _channel.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DshAgentAdapter));
    }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("value is required", parameterName) : value;
}
