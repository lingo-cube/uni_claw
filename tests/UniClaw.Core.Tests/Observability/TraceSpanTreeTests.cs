using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// TraceSpanTreeTests — span tree reconstruction over ITraceQuery
/// (trace-span-observability tasks 7.1 + 9.19):
/// root/child queries, entry.visited parentage of entry.skipped and action.* spans,
/// and a full mock run reconstructing the entry tree end-to-end.
/// Spans are built through InMemoryTraceRecorder (ITraceRecorder write surface)
/// and queried through InMemoryTraceService (ITraceQuery read surface).
/// SpanType strings use the SpanTypes catalog constants (spec §100, not literals).
/// </summary>
public class TraceSpanTreeTests
{
    // ── Task 7.1: span tree reconstruction (Core unit tests) ──

    [Fact(DisplayName = "Span tree: GetAllSpans() has engine.run as root (ParentSpanId == null)")]
    public async Task GetRootSpan_ReturnsEngineRunSpan()
    {
        var (service, runSpanId, _, _, _) = await BuildMockRunAsync();

        var root = service.GetRootSpan();

        Assert.NotNull(root);
        Assert.Equal(SpanTypes.EngineRun, root.SpanType);
        Assert.Equal(runSpanId, root.SpanId);
        Assert.Null(root.ParentSpanId);
    }

    [Fact(DisplayName = "Span tree: GetChildSpans(runSpanId) returns the engine.step children")]
    public async Task GetChildSpans_ReturnsEngineStepChildren()
    {
        var (service, runSpanId, stepSpanId, _, _) = await BuildMockRunAsync();

        var children = service.GetChildSpans(runSpanId);

        Assert.Single(children);
        Assert.Equal(SpanTypes.EngineStep, children[0].SpanType);
        Assert.Equal(stepSpanId, children[0].SpanId);
        Assert.Equal(runSpanId, children[0].ParentSpanId);
    }

    [Fact(DisplayName = "Span tree: entry.visited is parent of entry.skipped")]
    public async Task EntryVisited_IsParentOfEntrySkipped()
    {
        var (service, _, _, visitedSpanId, skippedSpanId) = await BuildMockRunAsync();

        var visited = service.GetSpan(visitedSpanId);
        var skipped = service.GetSpan(skippedSpanId);

        Assert.NotNull(visited);
        Assert.NotNull(skipped);
        Assert.Equal(SpanTypes.EntryVisited, visited.SpanType);
        Assert.Equal(SpanTypes.EntrySkipped, skipped.SpanType);
        Assert.Equal(visitedSpanId, skipped.ParentSpanId);
    }

    [Fact(DisplayName = "Span tree: entry.visited is parent of its action.* spans")]
    public async Task EntryVisited_IsParentOfActionSpans()
    {
        var (service, _, _, visitedSpanId, _) = await BuildMockRunAsync();

        var children = service.GetChildSpans(visitedSpanId);

        Assert.Contains(children, s => s.SpanType == SpanTypes.ActionClick);
        Assert.Contains(children, s => s.SpanType == SpanTypes.ActionScroll);
        Assert.All(children, s => Assert.Equal(visitedSpanId, s.ParentSpanId));
    }

    // ── Task 9.19: integration — full mock run span tree reconstruction ──

    [Fact(DisplayName = "Integration: full mock run GetAllSpans() reconstructs the entry tree")]
    public async Task FullMockRun_ReconstructsEntryTree()
    {
        var (service, runSpanId, step1SpanId, step2SpanId) = await BuildFullMockRunAsync();

        // All spans recorded in insertion order, run span first.
        var allSpans = service.GetAllSpans();
        Assert.Equal(11, allSpans.Count);
        Assert.Equal(SpanTypes.EngineRun, allSpans[0].SpanType);

        // Root = engine.run (ParentSpanId == null).
        var root = service.GetRootSpan();
        Assert.NotNull(root);
        Assert.Equal(SpanTypes.EngineRun, root.SpanType);
        Assert.Null(root.ParentSpanId);

        // Children of engine.run = the engine.step spans.
        var runChildren = service.GetChildSpans(runSpanId);
        Assert.Equal(2, runChildren.Count);
        Assert.All(runChildren, s => Assert.Equal(SpanTypes.EngineStep, s.SpanType));
        Assert.All(runChildren, s => Assert.Equal(runSpanId, s.ParentSpanId));
        Assert.Contains(runChildren, s => s.SpanId == step1SpanId);
        Assert.Contains(runChildren, s => s.SpanId == step2SpanId);

        // Every entry.visited hangs off an engine.step ...
        var stepSpanIds = runChildren.Select(s => s.SpanId).ToHashSet();
        var visitedSpans = service.GetSpansByType(SpanTypes.EntryVisited);
        Assert.Equal(3, visitedSpans.Count);
        Assert.All(visitedSpans, v =>
        {
            Assert.NotNull(v.ParentSpanId);
            Assert.Contains(v.ParentSpanId!, stepSpanIds);
        });

        // ... and is parent of its own entry.skipped / action.* children.
        foreach (var visited in visitedSpans)
        {
            var visitChildren = service.GetChildSpans(visited.SpanId);
            Assert.NotEmpty(visitChildren);
            Assert.All(visitChildren, c => Assert.Equal(visited.SpanId, c.ParentSpanId));
            Assert.Contains(visitChildren,
                c => c.SpanType == SpanTypes.EntrySkipped
                    || c.SpanType.StartsWith("action.", StringComparison.Ordinal));
        }

        // The full run covers both child categories: entry.skipped and action.*.
        Assert.NotEmpty(service.GetSpansByType(SpanTypes.EntrySkipped));
        Assert.NotEmpty(service.GetSpansByType(SpanTypes.ActionClick));
        Assert.NotEmpty(service.GetSpansByType(SpanTypes.ActionWait));
    }

    // ── Mock run builders ──────────────────────────────────

    /// <summary>
    /// Minimal mock run: engine.run → engine.step → entry.visited → entry.skipped,
    /// with action.click + action.scroll also under entry.visited.
    /// Covers the task 7.1 unit-test queries.
    /// </summary>
    private static async Task<(
        InMemoryTraceService Service,
        string RunSpanId,
        string StepSpanId,
        string VisitedSpanId,
        string SkippedSpanId)> BuildMockRunAsync()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var service = new InMemoryTraceService(storage);

        var runSpanId = await recorder.StartSpanAsync(SpanTypes.EngineRun, "run");
        var stepSpanId = await recorder.StartSpanAsync(SpanTypes.EngineStep, "step 1", runSpanId);
        var visitedSpanId = await recorder.StartSpanAsync(SpanTypes.EntryVisited, "visit Settings", stepSpanId);
        var skippedSpanId = await recorder.StartSpanAsync(SpanTypes.EntrySkipped, "skip dangerous", visitedSpanId);
        await recorder.EndSpanAsync(skippedSpanId, "skip");
        var clickSpanId = await recorder.StartSpanAsync(SpanTypes.ActionClick, "click toggle", visitedSpanId);
        await recorder.EndSpanAsync(clickSpanId, "ok");
        var scrollSpanId = await recorder.StartSpanAsync(SpanTypes.ActionScroll, "scroll list", visitedSpanId);
        await recorder.EndSpanAsync(scrollSpanId, "ok");
        await recorder.EndSpanAsync(visitedSpanId, "ok");
        await recorder.EndSpanAsync(stepSpanId, "ok");
        await recorder.EndSpanAsync(runSpanId, "ok");

        return (service, runSpanId, stepSpanId, visitedSpanId, skippedSpanId);
    }

    /// <summary>
    /// Full mock run spanning two engine.steps and three entry.visited subtrees:
    ///   11 spans = 1 engine.run + 2 engine.step + 3 entry.visited
    ///             + 2 entry.skipped + 3 action.* (click, scroll, wait).
    /// Covers the task 9.19 integration reconstruction.
    /// </summary>
    private static async Task<(
        InMemoryTraceService Service,
        string RunSpanId,
        string Step1SpanId,
        string Step2SpanId)> BuildFullMockRunAsync()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var service = new InMemoryTraceService(storage);

        var runSpanId = await recorder.StartSpanAsync(SpanTypes.EngineRun, "mock run");

        // Step 1: visit Settings → skip dangerous toggle + click + scroll; visit Network → wait.
        var step1SpanId = await recorder.StartSpanAsync(SpanTypes.EngineStep, "step 1", runSpanId);

        var settingsVisitedId = await recorder.StartSpanAsync(SpanTypes.EntryVisited, "visit Settings", step1SpanId);
        var dangerousSkippedId = await recorder.StartSpanAsync(SpanTypes.EntrySkipped, "skip dangerous toggle", settingsVisitedId);
        await recorder.EndSpanAsync(dangerousSkippedId, "skip");
        var toggleClickId = await recorder.StartSpanAsync(SpanTypes.ActionClick, "click toggle", settingsVisitedId);
        await recorder.EndSpanAsync(toggleClickId, "ok");
        var listScrollId = await recorder.StartSpanAsync(SpanTypes.ActionScroll, "scroll list", settingsVisitedId);
        await recorder.EndSpanAsync(listScrollId, "ok");
        await recorder.EndSpanAsync(settingsVisitedId, "ok");

        var networkVisitedId = await recorder.StartSpanAsync(SpanTypes.EntryVisited, "visit Network", step1SpanId);
        var loadWaitId = await recorder.StartSpanAsync(SpanTypes.ActionWait, "wait for load", networkVisitedId);
        await recorder.EndSpanAsync(loadWaitId, "ok");
        await recorder.EndSpanAsync(networkVisitedId, "ok");

        await recorder.EndSpanAsync(step1SpanId, "ok");

        // Step 2: visit Bluetooth → skip hotspot.
        var step2SpanId = await recorder.StartSpanAsync(SpanTypes.EngineStep, "step 2", runSpanId);
        var bluetoothVisitedId = await recorder.StartSpanAsync(SpanTypes.EntryVisited, "visit Bluetooth", step2SpanId);
        var hotspotSkippedId = await recorder.StartSpanAsync(SpanTypes.EntrySkipped, "skip hotspot", bluetoothVisitedId);
        await recorder.EndSpanAsync(hotspotSkippedId, "skip");
        await recorder.EndSpanAsync(bluetoothVisitedId, "ok");
        await recorder.EndSpanAsync(step2SpanId, "ok");

        await recorder.EndSpanAsync(runSpanId, "ok");

        return (service, runSpanId, step1SpanId, step2SpanId);
    }
}
