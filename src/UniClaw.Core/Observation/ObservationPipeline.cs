using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Observation;

/// <summary>
/// Unified observation pipeline. The UIA leg was removed (delete-uia): the
/// pipeline is now pure AI passthrough with back-navigation analysis reuse.
///
/// <code>
/// Analysis requested
///   ├─ back navigation pending + reusable history → reuse cached analysis
///   │     (zero AI call — the page returned to was already analyzed)
///   └─ else → AI vision passthrough
///         └─ empty response → DomainValidationException propagates (no retry)
/// </code>
///
/// Every produced analysis is remembered (deduplicated by
/// <see cref="PageSnapshotManager.Fingerprint"/>) so the history holds one
/// entry per distinct page — the back-navigation reuse source.
/// </summary>
public sealed class ObservationPipeline : IPageAnalyzer
{
    private readonly IPageAnalyzer _visual;
    private readonly ObservationConfig _config;
    private readonly ITraceRecorder? _traceRecorder;
    private readonly PageSnapshotManager _snapshotManager = new();
    private readonly object _gate = new();
    private readonly List<(int Fingerprint, PageAnalysis Analysis)> _history = new();
    private bool _backPending;

    /// <summary>
    /// Construct the pipeline. <paramref name="visual"/> is the AI vision leg
    /// (<see cref="PageAnalyzer"/>). Null checks fail fast via
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    /// <param name="visual">AI vision analyzer — the observation leg.</param>
    /// <param name="config">Observation behavior configuration; defaults to <see cref="ObservationConfig.Default"/>.</param>
    /// <param name="traceRecorder">Optional trace recorder for pipeline decisions ("AI",
    /// "AI_back_reuse").</param>
    public ObservationPipeline(
        IPageAnalyzer visual,
        ObservationConfig? config = null,
        ITraceRecorder? traceRecorder = null)
    {
        _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        _config = config ?? ObservationConfig.Default;
        _traceRecorder = traceRecorder;
    }

    /// <inheritdoc />
    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(
        CancellationToken cancellationToken = default)
    {
        // Back navigation reuse (D2): the page returned to was already analyzed,
        // so reuse that analysis — no AI call (AC6).
        if (ConsumeBackPending())
        {
            var reuse = GetBackReuseAnalysis();
            if (reuse is not null)
            {
                await RecordDecisionAsync("AI_back_reuse", cancellationToken);
                return reuse;
            }
        }

        // AI passthrough. AI empty response → DomainValidationException
        // (IsTransient=false) propagates: no retry.
        return await AnalyzeWithAiAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<AppEntryPoint?> FindAppEntryAsync(
        string targetApp,
        CancellationToken cancellationToken = default) =>
        _visual.FindAppEntryAsync(targetApp, cancellationToken);

    /// <inheritdoc />
    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken cancellationToken = default) =>
        _visual.VerifyPageTypeAsync(
            pageAnalysis,
            expectedType,
            expectedPageName,
            cancellationToken);

    /// <summary>
    /// Signals that a back navigation just succeeded. The next
    /// <see cref="AnalyzeCurrentPageAsync"/> call reuses the analysis of the
    /// page returned to (no AI call) instead of re-observing — the Host
    /// action-executor seam calls this on successful back actions.
    /// </summary>
    public void MarkBackNavigation()
    {
        lock (_gate)
        {
            _backPending = true;
        }
    }

    private async Task<PageAnalysis?> AnalyzeWithAiAsync(
        CancellationToken cancellationToken)
    {
        await RecordDecisionAsync("AI", cancellationToken);
        var analysis = await _visual.AnalyzeCurrentPageAsync(cancellationToken);
        if (analysis is not null)
            Remember(analysis);
        return analysis;
    }

    private bool ConsumeBackPending()
    {
        lock (_gate)
        {
            if (!_backPending)
                return false;
            _backPending = false;
            return true;
        }
    }

    /// <summary>
    /// Returns the analysis of the page the back navigation returned to: the
    /// entry before the current (last) distinct page. The current page's entry
    /// is dropped — we have left it. Returns null when there is no earlier
    /// distinct page in the history, in which case the caller re-observes.
    /// </summary>
    private PageAnalysis? GetBackReuseAnalysis()
    {
        lock (_gate)
        {
            if (_history.Count < 2)
                return null;
            _history.RemoveAt(_history.Count - 1);
            return _history[^1].Analysis;
        }
    }

    /// <summary>
    /// Records the produced analysis, deduplicating consecutive analyses of
    /// the same page (same <see cref="PageSnapshotManager.Fingerprint"/>) so
    /// the history holds one entry per distinct page. Bounded to keep memory
    /// flat for long runs.
    /// </summary>
    private void Remember(PageAnalysis analysis)
    {
        var fingerprint = _snapshotManager.Fingerprint(analysis);
        lock (_gate)
        {
            if (fingerprint != 0
                && _history.Count > 0
                && _history[^1].Fingerprint == fingerprint)
            {
                _history[^1] = (fingerprint, analysis);
                return;
            }

            _history.Add((fingerprint, analysis));
            if (_history.Count > 16)
                _history.RemoveAt(0);
        }
    }

    private async Task RecordDecisionAsync(
        string decision,
        CancellationToken cancellationToken)
    {
        if (_traceRecorder is null)
            return;
        try
        {
            await _traceRecorder.RecordExecutionAsync(
                new ExecutionRecord(
                    Action: decision,
                    Status: "ok",
                    SpanType: SpanType.StateDecision,
                    Timestamp: DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch
        {
            // Trace is observability — a failed decision record must not fail
            // the observation itself (log-and-continue, TraceCoordinator style).
        }
    }
}
