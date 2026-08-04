using System.Text.Json;
using UniClaw.Core.Observability;
using UniClaw.Host.Analysis;
using Xunit;

namespace UniClaw.Host.Tests.Analysis;

/// <summary>
/// P4 acceptance tests (tasks.md §9.12 / §9.13): BaselineBuilder appends exactly one
/// nine-field JSON line per run to artifacts/baselines/&lt;scenarioId&gt;.jsonl (append-only,
/// per-scenario file); BaselineProfile returns a not-ready profile below 10 records and
/// p50/p95 percentiles at 11, skipping corrupt lines.
/// </summary>
public sealed class BaselineTests
{
    private static InMemoryTraceService BuildTrace()
    {
        var storage = new InMemoryTraceStorage();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan("engine.run", "run", "run", null, start, null, null);
        storage.OpenSpan("engine.step", "step 1", "s1", "run", start.AddSeconds(1), null, null);
        storage.OpenSpan("engine.step", "step 2", "s2", "run", start.AddSeconds(5), null, null);
        storage.OpenSpan("entry.generate", "gen 1", "g1", "s1", start.AddSeconds(1.1), null, null);
        storage.OpenSpan("entry.observed", "Network", "o1", "g1", start.AddSeconds(1.2), null, null);
        storage.OpenSpan("entry.observed", "Bluetooth", "o2", "g1", start.AddSeconds(1.3), null, null);
        storage.OpenSpan("entry.generate", "gen 2", "g2", "s2", start.AddSeconds(5.1), null, null);
        // g2 has NO entry.observed children → end-of-list detected structurally
        storage.OpenSpan("entry.visited", "Network", "v1", "s1", start.AddSeconds(2), null, null);
        storage.OpenSpan("entry.visited", "Bluetooth", "v2", "s2", start.AddSeconds(6), null, null);
        storage.OpenSpan("entry.skipped", "Wi-Fi (denied)", "sk1", "v2", start.AddSeconds(6.5), null, null);
        storage.OpenSpan("action.scroll", "scroll", "sc1", "s1", start.AddSeconds(3), null, null);
        var ai1 = storage.OpenSpan("ai.call", "call 1", "a1", null, start.AddSeconds(0.5), null, null);
        storage.CloseSpan(ai1, start.AddSeconds(0.6), "ok", null); // 100ms
        var ai2 = storage.OpenSpan("ai.call", "call 2", "a2", null, start.AddSeconds(0.7), null, null);
        storage.CloseSpan(ai2, start.AddSeconds(1.0), "ok", null); // 300ms
        return new InMemoryTraceService(storage);
    }

    [Fact]
    public async Task AppendRun_AppendsOneNineFieldJsonLine()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var builder = new BaselineBuilder(BuildTrace(), root);
            await builder.AppendRunAsync("scenario-a");

            var file = Path.Combine(root, "baselines", "scenario-a.jsonl");
            Assert.True(File.Exists(file));
            var lines = File.ReadAllLines(file);
            Assert.Single(lines);

            using var doc = JsonDocument.Parse(lines[0]);
            var json = doc.RootElement;
            Assert.Equal(2, json.GetProperty("itemsObserved").GetInt32());
            Assert.Equal(2, json.GetProperty("itemsVisited").GetInt32());
            Assert.Equal(1, json.GetProperty("itemsSkipped").GetInt32());
            Assert.Equal(2, json.GetProperty("stepsUsed").GetInt32());
            Assert.Equal(1, json.GetProperty("scrollCount").GetInt32());
            Assert.True(json.GetProperty("endOfListDetected").GetBoolean());
            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.Equal(200.0, json.GetProperty("aiLatencyP50").GetDouble(), 3);
            Assert.Equal(200.0, json.GetProperty("aiLatencyP95").GetDouble(), 3);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AppendRun_Twice_AppendsTwoLinesWithFirstUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var trace = BuildTrace();
            var builder = new BaselineBuilder(trace, root);
            await builder.AppendRunAsync("scenario-b");
            var file = Path.Combine(root, "baselines", "scenario-b.jsonl");
            var first = File.ReadAllText(file);
            await builder.AppendRunAsync("scenario-b");
            var lines = File.ReadAllLines(file);
            Assert.Equal(2, lines.Length);
            Assert.Equal(first, lines[0] + Environment.NewLine);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AppendRun_NoEngineSteps_SkipsWithoutAppending()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var storage = new InMemoryTraceStorage();
            storage.OpenSpan("engine.run", "run", "run", null, DateTimeOffset.UtcNow, null, null);
            var builder = new BaselineBuilder(new InMemoryTraceService(storage), root);
            await builder.AppendRunAsync("scenario-c");
            Assert.False(File.Exists(Path.Combine(root, "baselines", "scenario-c.jsonl")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AppendRun_NoEntryActivity_SkipsWithoutAppending()
    {
        // D-193: engine.step 存在但零 entry 活动 (observed/visited/skipped 全 0) —
        // 该 run 对 visited 分布无信号, 不得追加 (否则失败 run 堆积稀释阈值)。
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var storage = new InMemoryTraceStorage();
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            storage.OpenSpan("engine.run", "run", "run", null, start, null, null);
            storage.OpenSpan("engine.step", "step 1", "s1", "run", start.AddSeconds(1), null, null);
            var builder = new BaselineBuilder(new InMemoryTraceService(storage), root);
            await builder.AppendRunAsync("scenario-c2");
            Assert.False(File.Exists(Path.Combine(root, "baselines", "scenario-c2.jsonl")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EndOfList_DetectedViaAttribute()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var storage = new InMemoryTraceStorage();
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            storage.OpenSpan("engine.run", "run", "run", null, start, null, null);
            storage.OpenSpan("engine.step", "step 1", "s1", "run", start.AddSeconds(1), null,
                new Dictionary<string, object> { ["end_of_list"] = true });
            // D-193: 零 entry 活动 run 不写基线 — 该测试需有 entry 才 append。
            storage.OpenSpan("entry.generate", "gen 1", "g1", "s1", start.AddSeconds(1.1), null, null);
            storage.OpenSpan("entry.observed", "Network", "o1", "g1", start.AddSeconds(1.2), null, null);
            var builder = new BaselineBuilder(new InMemoryTraceService(storage), root);
            await builder.AppendRunAsync("scenario-d");
            using var doc = JsonDocument.Parse(File.ReadAllLines(
                Path.Combine(root, "baselines", "scenario-d.jsonl"))[0]);
            Assert.True(doc.RootElement.GetProperty("endOfListDetected").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EndOfList_NotDetected_WhenGenerationProducesObservations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var storage = new InMemoryTraceStorage();
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            storage.OpenSpan("engine.run", "run", "run", null, start, null, null);
            storage.OpenSpan("engine.step", "step 1", "s1", "run", start.AddSeconds(1), null, null);
            storage.OpenSpan("entry.generate", "gen 1", "g1", "s1", start.AddSeconds(1.1), null, null);
            storage.OpenSpan("entry.observed", "Network", "o1", "g1", start.AddSeconds(1.2), null, null);
            var builder = new BaselineBuilder(new InMemoryTraceService(storage), root);
            await builder.AppendRunAsync("scenario-e");
            using var doc = JsonDocument.Parse(File.ReadAllLines(
                Path.Combine(root, "baselines", "scenario-e.jsonl"))[0]);
            Assert.False(doc.RootElement.GetProperty("endOfListDetected").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            Assert.Null(BaselineProfile.Load("scenario-z", root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_NineRecords_NotReady_ZeroPercentiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var dir = Path.Combine(root, "baselines");
            Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir, "scenario-f.jsonl"),
                Enumerable.Range(1, 9)
                    .Select(i => $"{{\"itemsObserved\":{i},\"itemsVisited\":{i},\"itemsSkipped\":0," +
                        $"\"stepsUsed\":{i},\"scrollCount\":0,\"endOfListDetected\":false,\"success\":true," +
                        $"\"aiLatencyP50\":{i * 100}.0,\"aiLatencyP95\":{i * 100}.0}}"));

            var profile = BaselineProfile.Load("scenario-f", root)!;
            Assert.Equal(9, profile.RecordCount);
            Assert.False(profile.IsReady);
            Assert.Equal(0, profile.ItemsVisitedP50);
            Assert.Equal(0, profile.ItemsVisitedP95);
            Assert.Equal(0, profile.StepsUsedP50);
            Assert.Equal(0, profile.AiLatencyP50);
            Assert.Equal(0, profile.AiLatencyP95);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_ElevenRecords_SkipsCorruptLine_ComputesP50P95()
    {
        var root = Path.Combine(Path.GetTempPath(), $"uniclaw-baseline-{Guid.NewGuid():N}");
        try
        {
            var dir = Path.Combine(root, "baselines");
            Directory.CreateDirectory(dir);
            var lines = Enumerable.Range(1, 11)
                .Select(i => $"{{\"itemsObserved\":{i},\"itemsVisited\":{i},\"itemsSkipped\":0," +
                    $"\"stepsUsed\":{i},\"scrollCount\":0,\"endOfListDetected\":false,\"success\":true," +
                    $"\"aiLatencyP50\":{i * 100}.0,\"aiLatencyP95\":{i * 100}.0}}")
                .ToList();
            lines.Insert(5, "this is not json {{{{");
            File.WriteAllLines(Path.Combine(dir, "scenario-g.jsonl"), lines);

            var profile = BaselineProfile.Load("scenario-g", root)!;
            Assert.Equal(11, profile.RecordCount);
            Assert.True(profile.IsReady);
            // itemsVisited 1..11 → p50 = index floor(11*0.5)=5 → 6; p95 = index floor(11*0.95)=10 → 11
            Assert.Equal(6, profile.ItemsVisitedP50);
            Assert.Equal(11, profile.ItemsVisitedP95);
            Assert.Equal(6, profile.StepsUsedP50);
            Assert.Equal(11, profile.StepsUsedP95);
            Assert.Equal(600, profile.AiLatencyP50, 3);
            Assert.Equal(1100, profile.AiLatencyP95, 3);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
