using UniClaw.Runtime.Capabilities.Brain;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// The DriverHost-side <see cref="IAssistanceProvider"/> implementation over the
/// cross-process Assistance seam (dsh-assistance-provider-adapter).
///
/// Responsibility (frozen): register a bounded pending request, await the
/// correlated resolution, validate requestId/worldVersion/recommendation at the
/// registry, and return the advice — or fail closed (null) on timeout,
/// cancellation, capacity exhaustion, or rejection. Owns NO intelligence.
///
/// Availability semantics (A2):
///  - provider absent            → the Agent's existing null-provider immediate
///    fail-closed behavior (seam already handles null) — NOT equivalent to this
///    path.
///  - provider present, harness  → bounded truthful failure: ConsultAsync waits at
///    most the consult timeout, then returns null; the Agent fails closed. It
///    never hangs indefinitely and never fabricates advice.
///  - consult timeout is COMPOSITION_POLICY (default 30s; injectable for tests).
/// </summary>
public sealed class AssistanceWireProvider : IAssistanceProvider
{
    /// <summary>COMPOSITION_POLICY default: bounded consult wait.</summary>
    public static readonly TimeSpan DefaultConsultTimeout = TimeSpan.FromSeconds(30);

    private readonly AssistancePendingRegistry _registry;
    private readonly TimeSpan _consultTimeout;

    /// <param name="registry">Shared bounded pending registry (also wired to the
    /// transport's assistance.pending / assistance.resolve methods).</param>
    /// <param name="consultTimeout">COMPOSITION_POLICY timeout (default 30s).</param>
    public AssistanceWireProvider(AssistancePendingRegistry registry, TimeSpan? consultTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _consultTimeout = consultTimeout ?? DefaultConsultTimeout;
    }

    /// <inheritdoc />
    public async Task<AssistanceAdvice?> ConsultAsync(AssistanceContext context, CancellationToken cancellationToken)
    {
        var entry = _registry.TryRegister(context);
        if (entry is null)
        {
            // Capacity exhausted → fail closed (no fabricated advice).
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_consultTimeout);
        try
        {
            // Bounded await: a matched resolve completes this task with the advice;
            // timeout/cancellation throws and we fail closed.
            return await entry.Completion.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout or caller cancellation → fail closed.
            return null;
        }
        finally
        {
            // All terminal paths remove the entry (idempotent). A removed entry can
            // never be resolved: late/duplicate resolves find no entry.
            _registry.Remove(context.RequestId);
        }
    }
}
