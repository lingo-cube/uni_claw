using UniClaw.Core.Observability;
using UniClaw.Host.Analysis;
using Xunit;

namespace UniClaw.Host.Tests.Analysis;

/// <summary>
/// Acceptance tests for EnumerateCompletionAnalyzer (trace-span-observability 7.2 / 9.14):
/// termination verdicts derived purely from span counts (observed/visited/skipped) and
/// structural end-of-list detection (an entry.generate child under a step with zero
/// entry.observed grandchildren), with cold-start suppression of Terminate/Recommend.
/// </summary>
public sealed class EnumerateCompletionAnalyzerTests
{
    private static (InMemoryTraceService Service, InMemoryTraceStorage Storage) CreateTrace()
    {
        var storage = new InMemoryTraceStorage();
        return (new InMemoryTraceService(storage), storage);
    }

    [Fact]
    public async Task PendingZero_WithEndReached_ReturnsHalt()
    {
        // observed == visited + skipped → pending = 0; entry.generate with no
        // entry.observed grandchildren → end-of-list detected structurally.
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
        storage.OpenSpan(SpanTypes.EntryGenerate, "gen 1", "g1", "s1", start.AddSeconds(1.1), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Network", "o1", "s1", start.AddSeconds(1.2), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Bluetooth", "o2", "s1", start.AddSeconds(1.3), null, null);
        storage.OpenSpan(SpanTypes.EntryVisited, "Network", "v1", "s1", start.AddSeconds(2), null, null);
        storage.OpenSpan(SpanTypes.EntryVisited, "Bluetooth", "v2", "s1", start.AddSeconds(2.1), null, null);

        var verdict = Assert.IsType<CompletionVerdict>(
            await new EnumerateCompletionAnalyzer().EvaluateAsync(service));
        Assert.True(verdict.ShouldTerminate);
        Assert.Equal(1.0, verdict.Confidence);
        Assert.Contains("halt", verdict.Reason);
    }

    [Fact]
    public async Task VisitedGteP95_WithEndReached_ReturnsTerminate()
    {
        // Ready baseline (10 records, itemsVisited=8) → p95 = 8. With visited 8 >= p95
        // and end-of-list reached, Terminate fires at 0.9 (not suppressed — baseline ready).
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var dir = Path.Combine(root, "baselines");
            Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir, "scenario-h.jsonl"),
                Enumerable.Repeat("{\"itemsObserved\":8,\"itemsVisited\":8,\"itemsSkipped\":0," +
                    "\"stepsUsed\":1,\"scrollCount\":0,\"endOfListDetected\":false,\"success\":true," +
                    "\"aiLatencyP50\":100.0,\"aiLatencyP95\":100.0}", 10));
            var baseline = BaselineProfile.Load("scenario-h", root)!;
            Assert.True(baseline.IsReady);
            Assert.Equal(8, baseline.ItemsVisitedP95);

            // observed 10 > visited 8 → pending = 2, so Halt cannot fire; generate found
            // nothing → endReached; visited 8 == p95 8 → Terminate.
            var (service, storage) = CreateTrace();
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
            storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
            storage.OpenSpan(SpanTypes.EntryGenerate, "gen 1", "g1", "s1", start.AddSeconds(1.1), null, null);
            for (var i = 0; i < 10; i++)
                storage.OpenSpan(SpanTypes.EntryObserved, $"observed {i}", $"o{i}", "s1",
                    start.AddSeconds(1.2 + i * 0.01), null, null);
            for (var i = 0; i < 8; i++)
                storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i}", $"v{i}", "s1",
                    start.AddSeconds(2 + i * 0.01), null, null);

            var verdict = Assert.IsType<CompletionVerdict>(
                await new EnumerateCompletionAnalyzer(baselineProfile: baseline).EvaluateAsync(service));
            Assert.True(verdict.ShouldTerminate);
            Assert.Equal(0.9, verdict.Confidence);
            Assert.Contains("terminate", verdict.Reason);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ColdStart_SuppressesTerminate()
    {
        // No baseline → cold-start: defaults p50=14 / p95=21 and the would-be Terminate
        // (visited 21 >= p95 21 with end reached) is downgraded to Observe at 0.0.
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
        storage.OpenSpan(SpanTypes.EntryGenerate, "gen 1", "g1", "s1", start.AddSeconds(1.1), null, null);
        for (var i = 0; i < 23; i++)
            storage.OpenSpan(SpanTypes.EntryObserved, $"observed {i}", $"o{i}", "s1",
                start.AddSeconds(1.2 + i * 0.01), null, null);
        for (var i = 0; i < 21; i++)
            storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i}", $"v{i}", "s1",
                start.AddSeconds(2 + i * 0.01), null, null);

        var verdict = Assert.IsType<CompletionVerdict>(
            await new EnumerateCompletionAnalyzer().EvaluateAsync(service));
        Assert.False(verdict.ShouldTerminate);
        Assert.Equal(0.0, verdict.Confidence);
        Assert.Contains("cold-start", verdict.Reason);
    }

    [Fact]
    public async Task VisitedSpike_ReturnsWarn()
    {
        // visited 32 >= default p95 21 × 1.5 → Warn at 0.95, regardless of end-of-list
        // (no entry.generate span exists here, so endReached is false).
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
        for (var i = 0; i < 32; i++)
            storage.OpenSpan(SpanTypes.EntryObserved, $"observed {i}", $"o{i}", "s1",
                start.AddSeconds(1.2 + i * 0.01), null, null);
        for (var i = 0; i < 32; i++)
            storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i}", $"v{i}", "s1",
                start.AddSeconds(2 + i * 0.01), null, null);

        var verdict = Assert.IsType<CompletionVerdict>(
            await new EnumerateCompletionAnalyzer().EvaluateAsync(service));
        Assert.False(verdict.ShouldTerminate);
        Assert.Equal(0.95, verdict.Confidence);
        Assert.Contains("warn", verdict.Reason);
    }

    [Fact]
    public async Task NormalRun_ReturnsObserve()
    {
        // Healthy run: some visited, pending > 0, no end-of-list → keep going.
        var (service, storage) = CreateTrace();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        storage.OpenSpan(SpanTypes.EngineStep, "step 1", "s1", "run", start.AddSeconds(1), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Network", "o1", "s1", start.AddSeconds(1.2), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Bluetooth", "o2", "s1", start.AddSeconds(1.3), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Wi-Fi", "o3", "s1", start.AddSeconds(1.4), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Cellular", "o4", "s1", start.AddSeconds(1.5), null, null);
        storage.OpenSpan(SpanTypes.EntryObserved, "Airplane mode", "o5", "s1", start.AddSeconds(1.6), null, null);
        storage.OpenSpan(SpanTypes.EntryVisited, "Network", "v1", "s1", start.AddSeconds(2), null, null);
        storage.OpenSpan(SpanTypes.EntryVisited, "Bluetooth", "v2", "s1", start.AddSeconds(2.1), null, null);
        storage.OpenSpan(SpanTypes.EntrySkipped, "Wi-Fi (denied)", "sk1", "s1", start.AddSeconds(3), null, null);

        var verdict = Assert.IsType<CompletionVerdict>(
            await new EnumerateCompletionAnalyzer().EvaluateAsync(service));
        Assert.False(verdict.ShouldTerminate);
        Assert.Equal(0.0, verdict.Confidence);
        Assert.Contains("observe", verdict.Reason);
    }
}
