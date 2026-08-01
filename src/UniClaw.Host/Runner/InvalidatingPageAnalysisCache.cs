using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
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

/// <summary>Invalidates visual state only after a device-changing action succeeds.</summary>
public sealed class PageInvalidatingActionExecutor : IActionExecutor
{
    private readonly IActionExecutor _inner;
    private readonly Action _invalidate;

    public PageInvalidatingActionExecutor(
        IActionExecutor inner,
        Action invalidate)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
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
        ExecuteAsync(_inner.PressBackAsync, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var success = await execute(cancellationToken);
        if (success)
            _invalidate();
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
/// </summary>
public sealed class UiAutomatorAugmentingPageAnalyzer : IPageAnalyzer
{
    private readonly IPageAnalyzer _visual;
    private readonly IObservableScreenStateProvider _screenState;

    public UiAutomatorAugmentingPageAnalyzer(
        IPageAnalyzer visual,
        IObservableScreenStateProvider screenState)
    {
        _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        _screenState = screenState
                       ?? throw new ArgumentNullException(nameof(screenState));
    }

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(
        CancellationToken cancellationToken = default)
    {
        var visual = await _visual.AnalyzeCurrentPageAsync(cancellationToken);
        var state = await _screenState.RefreshAsync(
            cancellationToken: cancellationToken);
        if (!state.Succeeded || string.IsNullOrWhiteSpace(state.HierarchyXml))
            return visual;

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
