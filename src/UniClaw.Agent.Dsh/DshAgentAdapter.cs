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
    private readonly object _gate = new();
    private readonly List<DshDiagnostic> _diagnostics = new();
    private readonly HashSet<string> _retiredRequests = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedRequests = new(StringComparer.Ordinal);
    private bool _handshakeValidated;
    private bool _disposed;
    private bool _active;
    private long _generation;
    private int _requestSequence;
    private string? _activeRequestId;
    private string? _activeDecisionId;
    private CancellationTokenSource? _activeCancellation;

    public DshAgentAdapter(
        IDecisionChannel channel,
        string productSessionId,
        string productRunId,
        TimeSpan? turnTimeout = null)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        ProductSessionId = Require(productSessionId, nameof(productSessionId));
        ProductRunId = Require(productRunId, nameof(productRunId));
        _turnTimeout = turnTimeout ?? TimeSpan.FromSeconds(60);
        if (_turnTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(turnTimeout));
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

        var attached = await EnsureAttachmentAsync(cancellationToken).ConfigureAwait(false);
        if (!attached)
            return null;

        DecisionRequest request;
        CancellationTokenSource turnCancellation;
        lock (_gate)
        {
            if (_active)
            {
                AddDiagnosticLocked(new DshDiagnostic("active-consultation-exists",
                    "a Product Run may have at most one active DSH consultation",
                    _activeRequestId, _generation, context.DecisionId));
                return null;
            }

            _active = true;
            _generation++;
            var generation = _generation;
            var requestId = $"dsh-req-{++_requestSequence:D8}";
            _activeRequestId = requestId;
            _activeDecisionId = context.DecisionId;
            turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCancellation = turnCancellation;
            SemanticConsultationsStarted++;
            request = new DecisionRequest(requestId, generation, ProductSessionId,
                ProductRunId, context);
        }

        Task<DecisionChannelResponse> operation;
        try
        {
            operation = _channel.PushAsync(request, turnCancellation.Token);
        }
        catch (Exception ex)
        {
            CloseActive(request, "provider-error", ex.Message);
            turnCancellation.Dispose();
            return null;
        }

        try
        {
            var timeout = Task.Delay(_turnTimeout);
            var completed = await Task.WhenAny(operation, timeout).ConfigureAwait(false);
            if (completed != operation)
            {
                turnCancellation.Cancel();
                RetireActive(request, "timeout", "turn deadline elapsed");
                // A deadline is the adapter's bounded-turn decision.  Ask the
                // realization to interrupt the current turn as a mechanical
                // best-effort action; the Product Run remains Kernel-owned.
                _ = ObserveAbortAsync();
                _ = ObserveLateResponseAsync(operation, request, turnCancellation);
                return null;
            }

            var response = await operation.ConfigureAwait(false);
            var accepted = AcceptTransportResponse(request, response);
            turnCancellation.Dispose();
            return accepted;
        }
        catch (OperationCanceledException)
        {
            RetireActive(request, "aborted", "turn was cancelled");
            _ = ObserveAbortAsync();
            turnCancellation.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            CloseActive(request, "provider-error", ex.Message);
            turnCancellation.Dispose();
            return null;
        }
    }

    /// <summary>Mechanical interruption only. This does not cancel a Product Run,
    /// invalidate a lease, write an outcome, or create a decision.</summary>
    public void AbortCurrentTurn()
    {
        string? requestId;
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (!_active)
                return;
            requestId = _activeRequestId;
            cancellation = _activeCancellation;
            _generation++;
            _active = false;
            if (requestId is not null) _retiredRequests.Add(requestId);
            AddDiagnosticLocked(new DshDiagnostic("turn-aborted",
                "AbortCurrentTurn terminated the realization turn only", requestId, _generation,
                _activeDecisionId));
            _activeRequestId = null;
            _activeDecisionId = null;
            _activeCancellation = null;
        }

        cancellation?.Cancel();
        _ = ObserveAbortAsync();
    }

    /// <summary>Revokes only this realization's semantic attachment. The Product
    /// Run remains owned by Kernel and is not cancelled or terminated.</summary>
    public async Task RevokeAttachmentAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AbortCurrentTurn();
        await _channel.RevokeAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _handshakeValidated = false;
            AddDiagnosticLocked(new DshDiagnostic(
                "attachment-revoked",
                "DSH realization attachment was revoked; Product Run remains Kernel-owned"));
        }
    }

    private async Task<bool> EnsureAttachmentAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_handshakeValidated) return true;
        }

        var request = ProductHandshake.CreateRequest(ProductSessionId, ProductRunId);
        DecisionChannelAttachment attachment;
        try
        {
            attachment = await _channel.AttachAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AddDiagnostic(new DshDiagnostic("attachment-failed", ex.Message));
            return false;
        }

        if (!attachment.Accepted)
        {
            AddDiagnostic(new DshDiagnostic("handshake-rejected",
                attachment.FailureReason ?? "channel attachment rejected"));
            return false;
        }
        lock (_gate)
        {
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
            _activeCancellation = null;
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

    private async Task ObserveAbortAsync()
    {
        try
        {
            await _channel.AbortCurrentTurnAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AddDiagnostic(new DshDiagnostic("abort-transport-error", ex.Message));
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
        lock (_gate) RetireActiveLocked(request, code, message);
    }

    private void RetireActiveLocked(DecisionRequest request, string code, string message)
    {
        _active = false;
        _retiredRequests.Add(request.RequestId);
        AddDiagnosticLocked(new DshDiagnostic(code, message, request.RequestId,
            request.Generation, request.Context.DecisionId));
        _activeRequestId = null;
        _activeDecisionId = null;
        _activeCancellation = null;
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
