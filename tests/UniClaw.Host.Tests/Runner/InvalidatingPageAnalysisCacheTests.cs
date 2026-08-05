using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

public sealed class InvalidatingPageAnalysisCacheTests
{
    [Fact]
    public async Task SamePhysicalScreen_IsAnalyzedOnce()
    {
        var inner = new CountingPageAnalyzer();
        var cache = new InvalidatingPageAnalysisCache(inner);

        var first = await cache.AnalyzeCurrentPageAsync();
        var second = await cache.AnalyzeCurrentPageAsync();

        Assert.Same(first, second);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task SuccessfulDeviceAction_InvalidatesNextVisualRead()
    {
        var inner = new CountingPageAnalyzer();
        var cache = new InvalidatingPageAnalysisCache(inner);
        var actions = new PageInvalidatingActionExecutor(
            new ConfigurableActionExecutor(success: true),
            cache.Invalidate);
        await cache.AnalyzeCurrentPageAsync();

        Assert.True(await actions.TapAsync(0.5, 0.5));
        await cache.AnalyzeCurrentPageAsync();

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task FailedDeviceAction_KeepsCurrentVisualRead()
    {
        var inner = new CountingPageAnalyzer();
        var cache = new InvalidatingPageAnalysisCache(inner);
        var actions = new PageInvalidatingActionExecutor(
            new ConfigurableActionExecutor(success: false),
            cache.Invalidate);
        await cache.AnalyzeCurrentPageAsync();

        Assert.False(await actions.TapAsync(0.5, 0.5));
        await cache.AnalyzeCurrentPageAsync();

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task BackSuccess_InvokesOnBackSuccessHook()
    {
        var inner = new CountingPageAnalyzer();
        var cache = new InvalidatingPageAnalysisCache(inner);
        var backed = false;
        var actions = new PageInvalidatingActionExecutor(
            new ConfigurableActionExecutor(success: true),
            cache.Invalidate,
            onBackSuccess: () => backed = true);

        Assert.True(await actions.PressBackAsync());

        Assert.True(backed);
    }

    private sealed class CountingPageAnalyzer : IPageAnalyzer
    {
        public int Calls { get; private set; }

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<PageAnalysis?>(new PageAnalysis(
                Direction.Left,
                Direction.Left,
                CurrentPath: ["Settings"]));
        }

        public Task<AppEntryPoint?> FindAppEntryAsync(
            string targetApp,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AppEntryPoint?>(null);

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PageTypeVerification(
                true,
                1,
                expectedType));
    }

    private sealed class ConfigurableActionExecutor(bool success) : IActionExecutor
    {
        public Task<bool> TapAsync(
            double x,
            double y,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(success);

        public Task<bool> SwipeAsync(
            double startX,
            double startY,
            double endX,
            double endY,
            int durationMs,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(success);

        public Task<bool> PressBackAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(success);

        public Task<bool> InputTextAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(success);

        public Task<bool> LongPressAsync(
            double x,
            double y,
            int durationMs,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(success);

        public Task WaitAsync(
            int milliseconds,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public List<ActionRecord> GetHistory() => [];
    }
}
