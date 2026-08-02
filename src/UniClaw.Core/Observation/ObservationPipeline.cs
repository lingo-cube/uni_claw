using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Observation;

/// <summary>
/// Unified observation pipeline (core-observation-pipeline D1): UIA → AI → fail.
///
/// <code>
/// Screenshot + UIA XML
///   ├─ UIA dump failed? → [2] AI directly (skip UIA)
///   ├─ [1] UIA.Parse → ≥N items + no popup → return UIA-only
///   │     └─ &lt;N items or popup detected → [2]
///   ├─ [2] AI vision → success → return AI
///   │     └─ empty response → DomainValidationException propagates (no retry)
///   └─ No fallback to UIA on AI failure — stale UIA data is worse than no data
/// </code>
///
/// UIA hits &gt;90% on standard Settings pages in ~1s vs ~60s for AI vision, so the
/// UIA leg runs first. When the device's UIAutomator becomes unavailable
/// (<see cref="IUiAutomatorAvailability.IsUiAutomatorAvailable"/> false — first
/// dump failure of the session, D6) or the config disables UIA, every
/// observation routes straight to AI and records an "UIA_disabled" decision.
/// An AI empty response is a structural model failure: it is not retried and
/// never falls back to stale UIA data.
/// </summary>
public sealed class ObservationPipeline : IPageAnalyzer
{
    private readonly IPageAnalyzer _visual;
    private readonly IObservableScreenStateProvider _screenState;
    private readonly ObservationConfig _config;
    private readonly IScreenStateCache? _captureStore;
    private readonly ITraceRecorder? _traceRecorder;
    private readonly object _gate = new();
    private readonly List<(string Fingerprint, PageAnalysis Analysis)> _history = new();
    private bool _backPending;

    /// <summary>
    /// Construct the pipeline. <paramref name="visual"/> is the AI vision leg
    /// (<see cref="PageAnalyzer"/>), <paramref name="screenState"/> provides the
    /// UIA hierarchy. Null checks fail fast via <see cref="ArgumentNullException"/>.
    /// </summary>
    /// <param name="visual">AI vision analyzer — the L2 leg.</param>
    /// <param name="screenState">Device screen state provider — the L1 dump source.</param>
    /// <param name="config">Observation behavior configuration; defaults to <see cref="ObservationConfig.Default"/>.</param>
    /// <param name="captureStore">Optional shared before-step capture; when valid the pipeline
    /// consumes it instead of issuing a duplicate ADB refresh.</param>
    /// <param name="traceRecorder">Optional trace recorder for pipeline decisions ("UIA",
    /// "AI", "UIA_disabled", "UIA_back_reuse").</param>
    public ObservationPipeline(
        IPageAnalyzer visual,
        IObservableScreenStateProvider screenState,
        ObservationConfig? config = null,
        IScreenStateCache? captureStore = null,
        ITraceRecorder? traceRecorder = null)
    {
        _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        _screenState = screenState ?? throw new ArgumentNullException(nameof(screenState));
        _config = config ?? ObservationConfig.Default;
        _captureStore = captureStore;
        _traceRecorder = traceRecorder;
    }

    /// <inheritdoc />
    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(
        CancellationToken cancellationToken = default)
    {
        // Back navigation reuse (D2): the page returned to was already analyzed,
        // so reuse that analysis — no ADB UIA dump, no AI call (AC6).
        if (_config.SkipUIAOnBackNavigation && ConsumeBackPending())
        {
            var reuse = GetBackReuseAnalysis();
            if (reuse is not null)
            {
                await RecordDecisionAsync("UIA_back_reuse", cancellationToken);
                return reuse;
            }
        }

        // L1 gate (D6 / config): UIA disabled → AI directly, no dump attempt.
        if (!_config.UIA_Enabled || !IsUiAutomatorAvailable())
        {
            await RecordDecisionAsync("UIA_disabled", cancellationToken);
            return await AnalyzeWithAiAsync(cancellationToken);
        }

        // ── L1: UIAutomator-first fast path ─────────────────────────────
        // Dump failed (Succeeded=false / empty HierarchyXml) → skip UIA and go
        // straight to AI (D1).
        var state = await GetFreshScreenStateAsync(cancellationToken);
        if (state.Succeeded && !string.IsNullOrWhiteSpace(state.HierarchyXml))
        {
            var uia = UiAutomatorPageAnalysis.Parse(state.HierarchyXml, state);
            if (uia.Items.Length >= _config.UIA_MinItems
                && (!_config.EnablePopupDetection || !HasPopupItems(uia)))
            {
                await RecordDecisionAsync("UIA", cancellationToken);
                Remember(state.HierarchyFingerprint, uia);
                return uia;
            }
            // Too few items (< UIA_MinItems) or popup-like items → fall through
            // to AI vision (D1); the UIA analysis is NOT returned.
        }

        // ── L2: AI vision ────────────────────────────────────────────────
        // AI empty response → DomainValidationException (IsTransient=false)
        // propagates: no retry, no UIA fallback — stale UIA is worse than no data.
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
    /// page returned to (no dump, no AI) instead of re-observing — the
    /// Host action-executor seam calls this on successful back actions when
    /// <see cref="ObservationConfig.SkipUIAOnBackNavigation"/> is enabled.
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
            Remember(null, analysis);
        return analysis;
    }

    /// <summary>
    /// Returns the step's before-step capture when still valid (pre-action,
    /// same step) — zero ADB cost — otherwise performs the ADB refresh.
    /// </summary>
    private async Task<ScreenStateResult> GetFreshScreenStateAsync(
        CancellationToken cancellationToken)
    {
        if (_captureStore is not null
            && _captureStore.TryGetBefore(out var cached)
            && cached is { Succeeded: true }
            && !string.IsNullOrWhiteSpace(cached.HierarchyXml))
        {
            return cached;
        }

        return await _screenState.RefreshAsync(
            cancellationToken: cancellationToken);
    }

    private bool IsUiAutomatorAvailable() =>
        _screenState is not IUiAutomatorAvailability capability
        || capability.IsUiAutomatorAvailable;

    private static readonly HashSet<string> PopupItemLabels = new(
        StringComparer.Ordinal)
    {
        // Only the most unambiguous dialog/popup button labels.
        // Broader terms like "delete", "stop", "exit" appear in normal
        // Settings content and would cause false-positive AI fallbacks.
        "close app", "dismiss", "allow", "deny", "got it", "not now",
    };

    /// <summary>
    /// Heuristic: when UIAutomator items contain popup/dialog button labels,
    /// fall back to AI. UIAutomator has no semantic understanding of popups
    /// and will treat "Close app" as a regular menu item.
    /// </summary>
    private static bool HasPopupItems(PageAnalysis uia) =>
        uia.Items.Any(item => PopupItemLabels.Contains(
            Normalize(item.Name)));

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
    /// the same page (same hierarchy fingerprint) so the history holds one
    /// entry per distinct page. Bounded to keep memory flat for long runs.
    /// </summary>
    private void Remember(string? fingerprint, PageAnalysis analysis)
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(fingerprint)
                && _history.Count > 0
                && string.Equals(
                    _history[^1].Fingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                _history[^1] = (fingerprint, analysis);
                return;
            }

            _history.Add((fingerprint ?? string.Empty, analysis));
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
