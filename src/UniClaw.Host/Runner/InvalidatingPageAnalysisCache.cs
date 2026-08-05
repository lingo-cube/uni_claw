using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<InvalidatingPageAnalysisCache> _logger;
    private PageAnalysis? _cached;
    private long _generation;

    public InvalidatingPageAnalysisCache(
        IPageAnalyzer inner,
        ILogger<InvalidatingPageAnalysisCache>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<InvalidatingPageAnalysisCache>.Instance;
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

        _logger.LogInformation(
            "page={Path} items={ItemCount} scroll={HasScroll} endOfList={EndOfList}",
            string.Join(" > ", analysis.CurrentPath),
            analysis.Items.Length,
            analysis.HasScroll,
            analysis.IsEndOfList);

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
/// </summary>
public sealed class PageInvalidatingActionExecutor : IActionExecutor
{
    private readonly IActionExecutor _inner;
    private readonly Action _invalidate;
    private readonly Action? _onBackSuccess;
    private readonly int _settleDelayMs;

    public PageInvalidatingActionExecutor(
        IActionExecutor inner,
        Action invalidate,
        Action? onBackSuccess = null,
        int settleDelayMs = 300)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
        _onBackSuccess = onBackSuccess;
        _settleDelayMs = settleDelayMs;
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
            onSuccess?.Invoke();
            if (_settleDelayMs > 0)
                await Task.Delay(_settleDelayMs, cancellationToken);
        }

        return success;
    }
}

/// <summary>Run-scoped UniBrain facade whose page analyzer has screen-state caching.</summary>
public sealed record class CachedPageAnalysisUniBrain(
    IPageAnalyzer PageAnalyzer,
    ITraversalAdvisor Advisor,
    ITextUnderstanding Text) : IUniBrain;
