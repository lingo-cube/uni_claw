using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Observation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.Observation;

/// <summary>
/// ObservationPipeline 测试 (core-observation-pipeline D1/D2, UIA 移除后)。
/// 纯仿真：无 emulator、无 AI 服务。覆盖：
///   AI passthrough (decision "AI")；
///   AI 空响应 → DomainValidationException 直抛 (不重试)；
///   AI 返回 null → 透传 null；
///   back 导航后复用缓存分析 (零 AI 调用, decision "AI_back_reuse")；
///   Remember 按 PageSnapshotManager.Fingerprint 去重。
/// </summary>
public sealed class ObservationPipelineTests
{
    // ── 1. AI passthrough ────────────────────────────────────────────────

    [Fact(DisplayName = "Pipeline: AI passthrough — 返回视觉分析，trace decision 'AI'")]
    public async Task Analyze_ReturnsVisualAnalysis_RecordsAiDecision()
    {
        var visual = new FakeVisualAnalyzer(Analysis("AI Page", "item-a"));
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(visual, trace: trace);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal("AI Page", analysis!.CurrentPath.Single());
        Assert.Equal(1, visual.Calls);
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "AI");
    }

    [Fact(DisplayName = "AC4: AI 空响应 → DomainValidationException 直抛，不重试")]
    public async Task Ai_EmptyResponse_ThrowsDomainValidation()
    {
        var visual = new FakeVisualAnalyzer
        {
            Exception = new DomainValidationException(
                "content",
                null,
                "analyze_visual model returned empty response — structural failure, will not retry."),
        };

        var pipeline = NewPipeline(visual);

        var exception = await Assert.ThrowsAsync<DomainValidationException>(
            () => pipeline.AnalyzeCurrentPageAsync());

        Assert.Contains("empty response", exception.Message);
        Assert.Equal(1, visual.Calls); // exactly one attempt — no retry
    }

    [Fact(DisplayName = "Pipeline: AI 返回 null → 透传 null")]
    public async Task Ai_NullResult_PassesThrough()
    {
        var visual = new FakeVisualAnalyzer(null);

        var pipeline = NewPipeline(visual);

        var analysis = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Null(analysis);
        Assert.Equal(1, visual.Calls);
    }

    // ── 2. Back navigation reuse ────────────────────────────────────────

    [Fact(DisplayName = "AC6: back 导航后复用缓存分析 — 零 AI 调用")]
    public async Task BackNavigation_ReusesCachedAnalysis_NoAiCall()
    {
        var visual = new FakeVisualAnalyzer(
            Analysis("Settings", "home"),
            Analysis("Network", "wifi"));
        var trace = NewTrace(out var storage);

        var pipeline = NewPipeline(visual, trace: trace);

        var home = await pipeline.AnalyzeCurrentPageAsync();  // AI (home)
        var sub = await pipeline.AnalyzeCurrentPageAsync();   // AI (sub)
        Assert.Equal(2, visual.Calls);
        Assert.NotEqual(home!.CurrentPath, sub!.CurrentPath);

        pipeline.MarkBackNavigation();
        var after = await pipeline.AnalyzeCurrentPageAsync(); // reuse cached home

        Assert.Same(home, after);
        Assert.Equal(2, visual.Calls); // no additional AI call
        Assert.Contains(
            storage.GetExecutions(),
            record => record.Action == "AI_back_reuse");
    }

    [Fact(DisplayName = "Pipeline: back 复用无历史页面 → 回落正常分析")]
    public async Task BackNavigation_NoPriorPage_FallsBackToNormalAnalysis()
    {
        var visual = new FakeVisualAnalyzer(
            Analysis("Settings", "home"),
            Analysis("Settings", "home")); // fallback re-observe after back

        var pipeline = NewPipeline(visual);

        var first = await pipeline.AnalyzeCurrentPageAsync();
        pipeline.MarkBackNavigation(); // no earlier distinct page in history

        var second = await pipeline.AnalyzeCurrentPageAsync();

        Assert.NotNull(second);
        Assert.Equal(2, visual.Calls);
    }

    [Fact(DisplayName = "Pipeline: 连续同指纹页面在历史中去重")]
    public async Task Remember_DeduplicatesConsecutiveSamePage()
    {
        var visual = new FakeVisualAnalyzer(
            Analysis("Settings", "home"),
            Analysis("Settings", "home"), // same fingerprint, distinct instance
            Analysis("Settings", "home")); // fallback re-observe after back
        var pipeline = NewPipeline(visual);

        var first = await pipeline.AnalyzeCurrentPageAsync();
        var second = await pipeline.AnalyzeCurrentPageAsync();
        Assert.NotSame(first, second);

        // Same page twice → one history entry. Back-nav reuse needs a distinct
        // earlier page, so it falls back to a fresh analysis.
        pipeline.MarkBackNavigation();
        var after = await pipeline.AnalyzeCurrentPageAsync();

        Assert.Equal(3, visual.Calls); // no reuse — history held one page
        Assert.NotNull(after);
    }

    // ── Harness ─────────────────────────────────────────────────────────

    private static PageAnalysis Analysis(string page, string item) =>
        new(
            Direction.Left,
            Direction.Left,
            CurrentPath: [page],
            Items: ImmutableArray.Create(
                new MenuItem(
                    item,
                    new Coordinate(0.5, 0.5),
                    MenuItemType.MenuItem)));

    private static ITraceRecorder NewTrace(out InMemoryTraceStorage storage)
    {
        storage = new InMemoryTraceStorage();
        return new InMemoryTraceRecorder(storage);
    }

    private static ObservationPipeline NewPipeline(
        FakeVisualAnalyzer visual,
        ITraceRecorder? trace = null) =>
        new(visual, traceRecorder: trace);

    private sealed class FakeVisualAnalyzer : IPageAnalyzer
    {
        private readonly Queue<PageAnalysis?> _results = new();

        public FakeVisualAnalyzer(params PageAnalysis?[] results)
        {
            // An explicit `null` argument arrives as a null params array
            // (Ai_NullResult_PassesThrough) — treat it as an empty queue.
            foreach (var result in results ?? [])
                _results.Enqueue(result);
        }

        public int Calls { get; private set; }

        public Exception? Exception { get; set; }

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Exception is not null)
                return Task.FromException<PageAnalysis?>(Exception);
            return Task.FromResult(
                _results.Count > 0 ? _results.Dequeue() : null);
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
}
