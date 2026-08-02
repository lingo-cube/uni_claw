using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;

namespace UniClaw.Host.Runner;

/// <summary>
/// Reuses one visual analysis while the physical screen is unchanged. Every
/// successful device action invalidates the cache, so the next engine read is
/// a fresh model call rather than stale state.
/// </summary>
public sealed class InvalidatingPageAnalysisCache : IPageAnalyzer
{
    private readonly IPageAnalyzer _inner;
    private readonly object _gate = new();
    private PageAnalysis? _cached;
    private long _generation;

    public InvalidatingPageAnalysisCache(IPageAnalyzer inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(
        CancellationToken cancellationToken = default)
    {
        long generation;
        lock (_gate)
        {
            if (_cached is not null)
                return _cached;
            generation = _generation;
        }

        var analysis = await _inner.AnalyzeCurrentPageAsync(cancellationToken);
        if (analysis is null)
            return null;
        lock (_gate)
        {
            if (_generation == generation)
                _cached = analysis;
        }
        return analysis;
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(
        string targetApp,
        CancellationToken cancellationToken = default) =>
        _inner.FindAppEntryAsync(targetApp, cancellationToken);

    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken cancellationToken = default) =>
        _inner.VerifyPageTypeAsync(
            pageAnalysis,
            expectedType,
            expectedPageName,
            cancellationToken);

    public void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _cached = null;
        }
    }
}

/// <summary>
/// Invalidates visual state only after a device-changing action succeeds.
/// Also invalidates the shared <see cref="StepCaptureStore"/> so the reused
/// pre-action capture is never consumed after an action has run.
/// </summary>
public sealed class PageInvalidatingActionExecutor : IActionExecutor
{
    private readonly IActionExecutor _inner;
    private readonly Action _invalidate;
    private readonly StepCaptureStore? _captureStore;
    private readonly Action? _onBackSuccess;

    public PageInvalidatingActionExecutor(
        IActionExecutor inner,
        Action invalidate,
        StepCaptureStore? captureStore = null,
        Action? onBackSuccess = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
        _captureStore = captureStore;
        _onBackSuccess = onBackSuccess;
    }

    public Task<bool> TapAsync(
        double x,
        double y,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(token => _inner.TapAsync(x, y, token), cancellationToken);

    public Task<bool> SwipeAsync(
        double startX,
        double startY,
        double endX,
        double endY,
        int durationMs,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            token => _inner.SwipeAsync(
                startX,
                startY,
                endX,
                endY,
                durationMs,
                token),
            cancellationToken);

    public Task<bool> PressBackAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            _inner.PressBackAsync,
            cancellationToken,
            _onBackSuccess);

    public Task<bool> InputTextAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(token => _inner.InputTextAsync(text, token), cancellationToken);

    public Task<bool> LongPressAsync(
        double x,
        double y,
        int durationMs,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            token => _inner.LongPressAsync(x, y, durationMs, token),
            cancellationToken);

    public Task WaitAsync(
        int milliseconds,
        CancellationToken cancellationToken = default) =>
        _inner.WaitAsync(milliseconds, cancellationToken);

    public List<ActionRecord> GetHistory() => _inner.GetHistory();

    private async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> execute,
        CancellationToken cancellationToken,
        Action? onSuccess = null)
    {
        var success = await execute(cancellationToken);
        if (success)
        {
            _invalidate();
            _captureStore?.Invalidate();
            onSuccess?.Invoke();
        }

        return success;
    }
}

/// <summary>Run-scoped UniBrain facade whose page analyzer has screen-state caching.</summary>
public sealed record class CachedPageAnalysisUniBrain(
    IPageAnalyzer PageAnalyzer,
    ITraversalAdvisor Advisor,
    ITextUnderstanding Text) : IUniBrain;

/// <summary>
/// Augments model perception with deterministic UIAutomator rows from the same
/// physical screen. UIAutomator supplies complete labels and trusted
/// coordinates; the visual model remains authoritative for page/popup meaning.
/// The hierarchy comes from the step's before-step capture when still valid,
/// avoiding a duplicate ADB refresh on the traversal hot path.
/// </summary>
/// <remarks>
/// Deprecated (core-observation-pipeline 3.3): the UIA→AI cascade and the
/// UIAutomator XML parsing moved into Core's <see cref="ObservationPipeline"/>
/// (parser: <see cref="UiAutomatorPageAnalysis"/>). This class is retained as a
/// legacy shim so existing callers and tests keep compiling; new code must use
/// the pipeline. Note the shim keeps its original merge semantics — the
/// pipeline deliberately does NOT merge UIA rows into AI results (D1: stale
/// UIA data is worse than no data).
/// </remarks>
[Obsolete(
    "Use ObservationPipeline (Core) instead — the UIA→AI cascade moved into "
    + "UniClaw.Core.Observation.ObservationPipeline (core-observation-pipeline).")]
public sealed class UiAutomatorAugmentingPageAnalyzer : IPageAnalyzer
{
    private readonly IPageAnalyzer _visual;
    private readonly IObservableScreenStateProvider _screenState;
    private readonly StepCaptureStore? _captureStore;

    public UiAutomatorAugmentingPageAnalyzer(
        IPageAnalyzer visual,
        IObservableScreenStateProvider screenState,
        StepCaptureStore? captureStore = null)
    {
        _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
        _captureStore = captureStore;
    }

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(
        CancellationToken cancellationToken = default)
    {
        // ── UIAutomator-first fast path (D7): parse the hierarchy XML before
        // invoking the expensive AI vision model. When the XML has enough
        // interactive items (≥3) it's reliable → return immediately, zero AI cost.
        // Falls through to AI when UIAutomator is unavailable (car head units,
        // WebViews) or returns too few items (popups, error screens).
        var state = await GetFreshScreenStateAsync(cancellationToken);
        if (state.Succeeded && !string.IsNullOrWhiteSpace(state.HierarchyXml))
        {
            var uia = UiAutomatorPageAnalysis.Parse(
                state.HierarchyXml,
                state);
            if (uia.Items.Length >= 3 && !HasPopupItems(uia))
                return uia;
        }

        var visual = await _visual.AnalyzeCurrentPageAsync(cancellationToken);

        // If UIAutomator succeeded but had too few items (popup, WebView),
        // still use AI as the primary analysis.
        if (state.Succeeded && !string.IsNullOrWhiteSpace(state.HierarchyXml))
        {
            var deterministic = UiAutomatorPageAnalysis.Parse(
                state.HierarchyXml,
                state);
            if (visual is null)
                return deterministic;

            return visual with
            {
                Level1Menus = MergeMenus(
                    deterministic.Level1Menus,
                    visual.Level1Menus),
                Level2Menus = MergeMenus(
                    deterministic.Level2Menus,
                    visual.Level2Menus),
                Items = MergeItems(deterministic.Items, visual.Items),
                CurrentPath = PreferDeterministicIdentity(
                    deterministic.CurrentPath,
                    visual.CurrentPath),
                HasScroll = state.HasScroll || visual.HasScroll,
                IsEndOfList = state.IsEndOfList || visual.IsEndOfList,
            };
        }

        // No UIAutomator data at all — return pure AI result.
        return visual;
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(
        string targetApp,
        CancellationToken cancellationToken = default) =>
        _visual.FindAppEntryAsync(targetApp, cancellationToken);

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

    private static ImmutableArray<MenuInfo> MergeMenus(
        ImmutableArray<MenuInfo> deterministic,
        ImmutableArray<MenuInfo> visual) =>
        [.. deterministic
            .Concat(visual)
            .GroupBy(menu => Normalize(menu.Name), StringComparer.Ordinal)
            .Select(group => group.First())];

    private static ImmutableArray<MenuItem> MergeItems(
        ImmutableArray<MenuItem> deterministic,
        ImmutableArray<MenuItem> visual) =>
        [.. deterministic
            .Concat(visual)
            .GroupBy(item => Normalize(item.Name), StringComparer.Ordinal)
            .Select(group => group.First())];

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
    /// fall back to AI.  UIAutomator has no semantic understanding of popups
    /// and will treat "Close app" as a regular menu item.
    /// </summary>
    private static bool HasPopupItems(PageAnalysis uia) =>
        uia.Items.Any(item => PopupItemLabels.Contains(
            Normalize(item.Name)));

    private static ImmutableArray<string> PreferDeterministicIdentity(
        ImmutableArray<string> deterministic,
        ImmutableArray<string> visual)
    {
        var deterministicIdentity = deterministic.LastOrDefault();
        if (!string.IsNullOrWhiteSpace(deterministicIdentity)
            && !string.Equals(
                Normalize(deterministicIdentity),
                "settings",
                StringComparison.Ordinal))
        {
            return deterministic;
        }

        return visual.IsEmpty ? deterministic : visual;
    }
}
