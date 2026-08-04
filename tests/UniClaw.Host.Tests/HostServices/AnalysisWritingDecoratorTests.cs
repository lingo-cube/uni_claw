using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Artifacts;
using UniClaw.Host.HostServices;
using UniClaw.Host.Runner;
using Xunit;

namespace UniClaw.Host.Tests.HostServices;

public sealed class AnalysisWritingDecoratorTests
{
    [Fact]
    public async Task AnalyzeCurrentPageAsync_UpdatesAccessor()
    {
        var accessor = new CurrentPageAnalysisAccessor();
        var analysis = new PageAnalysis(Direction.Left, Direction.Left);
        var inner = new FakePageAnalyzer(analysis);
        var decorator = new AnalysisWritingDecorator(inner, accessor);

        Assert.Null(accessor.Current);

        var result = await decorator.AnalyzeCurrentPageAsync();

        Assert.Same(analysis, result);
        Assert.Same(analysis, accessor.Current);
    }

    [Fact]
    public async Task AnalyzeCurrentPageAsync_NullResult_DoesNotUpdateAccessor()
    {
        var accessor = new CurrentPageAnalysisAccessor();
        var inner = new FakePageAnalyzer(null);
        var decorator = new AnalysisWritingDecorator(inner, accessor);

        var result = await decorator.AnalyzeCurrentPageAsync();

        Assert.Null(result);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task AnalyzeCurrentPageAsync_WithSink_WritesAnalysisJsonl()
    {
        // D-197: run 场景下每次分析落盘 {runDirectory}/analysis.jsonl, 供
        // matcher/OCR 排查（检测到的条目名 vs 场景目标名）。
        var accessor = new CurrentPageAnalysisAccessor();
        var items = ImmutableArray.Create(
            new MenuItem(
                "About phone",
                new Coordinate(0.5, 0.5),
                MenuItemType.MenuItem,
                ExpectedAction: ExpectedAction.Navigate));
        var analysis = new PageAnalysis(
            Direction.Top, Direction.Bottom,
            Items: items,
            HasScroll: true,
            IsEndOfList: false);
        var inner = new FakePageAnalyzer(analysis);
        var tempDir = Path.Combine(Path.GetTempPath(), "uniclaw-ana-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        await using var pipeline = TestPipeline(tempDir);
        try
        {
            var decorator = new AnalysisWritingDecorator(inner, accessor, pipeline, tempDir);

            await decorator.AnalyzeCurrentPageAsync();
            await pipeline.DrainAsync();

            var lines = File.ReadAllLines(Path.Combine(tempDir, "analysis.jsonl"));
            var record = Assert.Single(lines);
            Assert.Contains("\"itemCount\":1", record);
            Assert.Contains("\"name\":\"About phone\"", record);
            // DomainJsonOptions: enum 输出 camelCase 成员名（menuItem/navigate）。
            Assert.Contains("\"type\":\"menuItem\"", record);
            Assert.Contains("\"hasScroll\":true", record);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeCurrentPageAsync_NullResult_WithSink_WritesNothing()
    {
        var accessor = new CurrentPageAnalysisAccessor();
        var inner = new FakePageAnalyzer(null);
        var tempDir = Path.Combine(Path.GetTempPath(), "uniclaw-ana-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        await using var pipeline = TestPipeline(tempDir);
        try
        {
            var decorator = new AnalysisWritingDecorator(inner, accessor, pipeline, tempDir);

            await decorator.AnalyzeCurrentPageAsync();
            await pipeline.DrainAsync();

            Assert.False(File.Exists(Path.Combine(tempDir, "analysis.jsonl")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task FindAppEntryAsync_DelegatesToInner()
    {
        var accessor = new CurrentPageAnalysisAccessor();
        var entry = new AppEntryPoint("com.test", 0.5, 0.5);
        var inner = new FakePageAnalyzer(null, entry);
        var decorator = new AnalysisWritingDecorator(inner, accessor);

        var result = await decorator.FindAppEntryAsync("com.test");

        Assert.Same(entry, result);
    }

    [Fact]
    public async Task VerifyPageTypeAsync_DelegatesToInner()
    {
        var accessor = new CurrentPageAnalysisAccessor();
        var verification = new PageTypeVerification(true, 1.0, "settings_home", "match");
        var inner = new FakePageAnalyzer(null, verification: verification);
        var decorator = new AnalysisWritingDecorator(inner, accessor);

        var analysis = new PageAnalysis(Direction.Left, Direction.Left);
        var result = await decorator.VerifyPageTypeAsync(analysis, "settings_home");

        Assert.Equal(verification, result);
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        var accessor = new CurrentPageAnalysisAccessor();
        Assert.Throws<ArgumentNullException>(() => new AnalysisWritingDecorator(null!, accessor));
    }

    [Fact]
    public void Constructor_NullAccessor_Throws()
    {
        var inner = new FakePageAnalyzer(null);
        Assert.Throws<ArgumentNullException>(() => new AnalysisWritingDecorator(inner, null!));
    }

    /// <summary>
    /// Mirrors <c>HostCommands.CreateAssetPipeline</c> write-side wiring: a
    /// <see cref="FileAssetStore"/> rooted at <paramref name="root"/> fed by a
    /// <see cref="TracePipeline"/> (relative paths land under the root).
    /// </summary>
    private static TracePipeline TestPipeline(string root)
    {
        var store = new FileAssetStore(root);
        return new TracePipeline(store, "run-test");
    }

    /// <summary>Minimal IPageAnalyzer fake for decorator tests.</summary>
    private sealed class FakePageAnalyzer : IPageAnalyzer
    {
        private readonly PageAnalysis? _analysis;
        private readonly AppEntryPoint? _entry;
        private readonly PageTypeVerification _verification;

        public FakePageAnalyzer(
            PageAnalysis? analysis,
            AppEntryPoint? entry = null,
            PageTypeVerification? verification = null)
        {
            _analysis = analysis;
            _entry = entry;
            _verification = verification ?? new PageTypeVerification(false, 0.0);
        }

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
            => Task.FromResult(_analysis);

        public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
            => Task.FromResult(_entry);

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis, string expectedType,
            string? expectedPageName = null, CancellationToken ct = default)
            => Task.FromResult(_verification);
    }
}
