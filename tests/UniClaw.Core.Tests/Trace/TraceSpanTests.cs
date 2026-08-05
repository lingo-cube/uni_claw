using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Tests.Observability.File;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.Trace;

/// <summary>
/// P1 tests for the TraceSpan model + ITraceRecorder.StartSpan/EndSpan + ITraceQuery
/// (trace-span-observability). Covers spec trace-span §9.1-9.5 and the ITraceQuery
/// parent-child tree scenarios.
/// P2 adds engine/entry instrumentation tests (§9.6-9.9).
/// </summary>
public class TraceSpanTests
{
    // ── TraceSpan record / JSON round-trip (§9.1) ──────────

    [Fact]
    public void TraceSpan_DurationMs_ZeroWhenEndTimeNull()
    {
        var span = new TraceSpan("s1", null, "engine.run", "run", DateTimeOffset.UtcNow, null, "ok", null);
        Assert.Equal(0, span.DurationMs);
    }

    [Fact]
    public void TraceSpan_DurationMs_ComputedFromInterval()
    {
        var start = DateTimeOffset.UtcNow;
        var span = new TraceSpan("s1", null, "engine.run", "run", start, start.AddMilliseconds(250), "ok", null);
        Assert.Equal(250, span.DurationMs, 1);
    }

    [Fact]
    public void TraceSpan_Json_RoundTripsThroughDomainJsonOptions()
    {
        var start = DateTimeOffset.UtcNow;
        var span = new TraceSpan("s1", "parent-1", "entry.observed", "Network",
            start, start.AddMilliseconds(5), "ok", null,
            new Dictionary<string, object> { ["entry.name"] = "Network" });

        var json = JsonSerializer.Serialize(span, DomainJsonOptions.Default);
        var back = JsonSerializer.Deserialize<TraceSpan>(json, DomainJsonOptions.Default);

        Assert.NotNull(back);
        Assert.Equal("s1", back.SpanId);
        Assert.Equal("parent-1", back.ParentSpanId);
        Assert.Equal("entry.observed", back.SpanType);
        Assert.Equal("Network", back.SpanName);
        Assert.Equal("ok", back.Status);
        Assert.Equal(5, back.DurationMs, 1);
        Assert.Equal("Network", back.Attributes!["entry.name"]);
    }

    [Fact]
    public void TraceSpan_InvalidStatus_ThrowsDomainValidationException()
    {
        var span = new TraceSpan("s1", null, "engine.run", "run", DateTimeOffset.UtcNow, null, "bogus", null);
        Assert.Throws<DomainValidationException>(() => span.Validate());
    }

    [Fact]
    public void SpanTypeCatalog_ContainsAllEmittedSpanTypes()
    {
        // Every dotted spanType from the design must be a known catalog member.
        Assert.True(SpanTypes.IsKnown("engine.run"));
        Assert.True(SpanTypes.IsKnown("engine.step"));
        Assert.True(SpanTypes.IsKnown("entry.generate"));
        Assert.True(SpanTypes.IsKnown("entry.observed"));
        Assert.True(SpanTypes.IsKnown("entry.ignored"));
        Assert.True(SpanTypes.IsKnown("entry.visited"));
        Assert.True(SpanTypes.IsKnown("entry.skipped"));
        Assert.True(SpanTypes.IsKnown("entry.action"));
        Assert.True(SpanTypes.IsKnown("action.click"));
        Assert.True(SpanTypes.IsKnown("action.scroll"));
        Assert.True(SpanTypes.IsKnown("action.back"));
        Assert.True(SpanTypes.IsKnown("action.launch"));
        Assert.True(SpanTypes.IsKnown("action.wait"));
        Assert.True(SpanTypes.IsKnown("ai.call"));
        Assert.True(SpanTypes.IsKnown("ai.analyze"));
        Assert.True(SpanTypes.IsKnown("analyze.completion"));
        Assert.True(SpanTypes.IsKnown("analyze.error_loop"));
        Assert.True(SpanTypes.IsKnown("analyze.tree"));
        Assert.False(SpanTypes.IsKnown("not.a.span.type"));
    }

    // ── InMemoryTraceStorage span CRUD (§9.3) ──────────────

    [Fact]
    public void InMemoryStorage_SpanWrites_LeaveExistingListsUntouched()
    {
        var storage = new InMemoryTraceStorage();
        var exec = new ExecutionRecord("click", "ok", SpanType.DfsForward, Timestamp: DateTimeOffset.UtcNow);
        storage.AddExecution(exec);

        storage.OpenSpan("engine.run", "run", "s1", null, DateTimeOffset.UtcNow, null, null);
        storage.CloseSpan("s1", DateTimeOffset.UtcNow.AddSeconds(1), "ok", null);

        // Existing consumers unaffected.
        Assert.Single(storage.GetExecutions());
        Assert.Empty(storage.GetTransitions());
        Assert.Empty(storage.GetErrors());
        Assert.Empty(storage.GetPageTransitions());
        Assert.Empty(storage.GetAICalls());
        Assert.Single(storage.GetAllSpans());
    }

    [Fact]
    public void InMemoryStorage_CloseSpan_SetsEndTimeStatusAndMergesAttributes()
    {
        var storage = new InMemoryTraceStorage();
        storage.OpenSpan("engine.step", "step 1", "s1", "run", DateTimeOffset.UtcNow, null,
            new Dictionary<string, object> { ["step.number"] = 1 });
        storage.CloseSpan("s1", DateTimeOffset.UtcNow.AddMilliseconds(100), "ok",
            new Dictionary<string, object> { ["step.wall_ms"] = 100 });

        var span = storage.FindSpan("s1");
        Assert.NotNull(span);
        Assert.True(span.EndTime.HasValue);
        Assert.Equal("ok", span.Status);
        Assert.Equal(1, span.Attributes!["step.number"]);
        Assert.Equal(100, span.Attributes["step.wall_ms"]);
        Assert.Equal(100, span.DurationMs, 1);
    }

    [Fact]
    public void InMemoryStorage_CloseSpan_UnknownOrAlreadyClosed_NoOp()
    {
        var storage = new InMemoryTraceStorage();
        storage.OpenSpan("engine.run", "run", "s1", null, DateTimeOffset.UtcNow, null, null);
        storage.CloseSpan("s1", DateTimeOffset.UtcNow.AddSeconds(1), "ok", null);
        var endTimeAfterFirst = storage.FindSpan("s1")!.EndTime;

        // Closing again (and closing an unknown span) must not throw and must not alter the span.
        storage.CloseSpan("s1", DateTimeOffset.UtcNow.AddSeconds(5), "error", null);
        storage.CloseSpan("unknown-id", DateTimeOffset.UtcNow.AddSeconds(5), "ok", null);

        Assert.Equal(endTimeAfterFirst, storage.FindSpan("s1")!.EndTime);
        Assert.Equal("ok", storage.FindSpan("s1")!.Status);
    }

    [Fact]
    public void InMemoryStorage_GetSpansByType_And_GetChildSpans()
    {
        var storage = new InMemoryTraceStorage();
        storage.OpenSpan("engine.run", "run", "run", null, DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("engine.step", "step 1", "step1", "run", DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("entry.observed", "Network", "obs1", "step1", DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("entry.observed", "Apps", "obs2", "step1", DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("entry.ignored", "Network", "ign1", "step1", DateTimeOffset.UtcNow, null, null);

        Assert.Single(storage.GetSpansByType("engine.run"));
        Assert.Equal(2, storage.GetSpansByType("entry.observed").Count);
        Assert.Equal(1, storage.GetSpansByType("entry.ignored").Count);
        Assert.Equal(3, storage.GetChildSpans("step1").Count); // obs1 + obs2 + ign1
        Assert.Empty(storage.GetChildSpans("missing"));
    }

    // ── InMemoryTraceService : ITraceQuery (§9.5) ──────────

    [Fact]
    public void ITraceQuery_GetRootSpan_ReturnsParentNullSpan()
    {
        var storage = new InMemoryTraceStorage();
        storage.OpenSpan("engine.run", "run", "run", null, DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("engine.step", "step 1", "step1", "run", DateTimeOffset.UtcNow, null, null);

        var service = new InMemoryTraceService(storage);
        Assert.Equal("run", service.GetRootSpan()!.SpanId);
        Assert.Single(service.GetChildSpans("run"));
        Assert.Equal("step1", service.GetSpan("step1")!.SpanId);
        Assert.Equal(2, service.GetAllSpans().Count);
        Assert.Single(service.GetSpansByType("engine.step"));
    }

    [Fact]
    public void ITraceQuery_ThreeLevelTree_Queries()
    {
        var storage = new InMemoryTraceStorage();
        storage.OpenSpan("engine.run", "run", "run", null, DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("engine.step", "step 4", "step4", "run", DateTimeOffset.UtcNow, null, null);
        storage.OpenSpan("entry.visited", "Network & internet", "vis", "step4", DateTimeOffset.UtcNow, null, null);

        var service = new InMemoryTraceService(storage);
        Assert.Equal(3, service.GetAllSpans().Count);
        Assert.Single(service.GetSpansByType("entry.visited"));
        Assert.Equal("step4", service.GetSpan(service.GetChildSpans("run")[0].SpanId)!.SpanId);
    }

    // ── ITraceRecorder.StartSpan/EndSpan (§9.2) ────────────

    [Fact]
    public async Task StartSpan_ReturnsNonEmptyId_AndSpanReadable()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);

        var spanId = await recorder.StartSpanAsync("engine.step", "step 1");

        Assert.False(string.IsNullOrEmpty(spanId));
        var span = storage.FindSpan(spanId);
        Assert.NotNull(span);
        Assert.Equal("engine.step", span.SpanType);
        Assert.True(span.StartTime != default);
        Assert.Null(span.EndTime);
    }

    [Fact]
    public async Task EndSpan_SetsEndTime_AndMergesAttributes()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);

        var spanId = await recorder.StartSpanAsync("engine.run", "run",
            attributes: new Dictionary<string, object> { ["k"] = 1 });
        await recorder.EndSpanAsync(spanId, "ok", new Dictionary<string, object> { ["extra"] = "v" });

        var span = storage.FindSpan(spanId);
        Assert.True(span!.EndTime.HasValue);
        Assert.Equal("ok", span.Status);
        Assert.Equal(1, span.Attributes!["k"]);
        Assert.Equal("v", span.Attributes["extra"]);
    }

    [Fact]
    public async Task EndSpan_UnknownId_DoesNotThrow()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);

        await recorder.EndSpanAsync("never-started", "ok"); // no-op, no throw
        var spanId = await recorder.StartSpanAsync("engine.run", "run");
        await recorder.EndSpanAsync(spanId, "ok");
        await recorder.EndSpanAsync(spanId, "error"); // second close, no throw
        Assert.Equal("ok", storage.FindSpan(spanId)!.Status);
    }

    // ── FileTraceStorage span record_type (§9.4) ───────────

    [Fact]
    public void FileTraceStorage_OpenCloseSpan_ReadsBackDeduplicated()
    {
        var provider = new MockFileProvider();
        var storage = new FileTraceStorage(provider, baseDir: "traces");
        storage.SetSession(new TraceSession("t1", DateTimeOffset.UtcNow));

        var start = DateTimeOffset.UtcNow;
        var spanId = storage.OpenSpan("engine.run", "run", "s1", null, start, null, null);
        storage.CloseSpan("s1", start.AddSeconds(2), "ok", null);

        // The file has both open + close lines; reads deduplicate to the closed span.
        var spans = storage.GetAllSpans();
        var span = Assert.Single(spans);
        Assert.Equal("s1", span.SpanId);
        Assert.Equal(spanId, span.SpanId);
        Assert.True(span.EndTime.HasValue);
        Assert.Equal("ok", span.Status);
        Assert.Equal(2000, span.DurationMs, 1);
    }

    // ── P2: TraceCoordinator StartSpan/EndSpan passthroughs (§9.6/9.8) ──

    [Fact]
    public async Task TraceCoordinator_StartSpan_ParentsEngineStepToRun()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);
        var ctx = new TraversalRuntimeContext("trace-1");
        var trace = new TraceCoordinator(recorder, "trace-1", ctx);

        var runSpanId = trace.StartSpan(SpanTypes.EngineRun, null);
        var stepSpanId = trace.StartSpan(SpanTypes.EngineStep, runSpanId);
        Assert.Equal(stepSpanId, trace.CurrentEngineStepSpanId);
        trace.EndSpan(stepSpanId);
        trace.EndSpan(runSpanId, "all_visited");

        Assert.Null(trace.CurrentEngineStepSpanId); // cleared by EndSpan(stepSpanId)
        Assert.Equal(runSpanId, storage.FindSpan(stepSpanId)!.ParentSpanId);
        Assert.Equal("all_visited", storage.FindSpan(runSpanId)!.Status);
    }

    [Fact]
    public async Task TraceCoordinator_CurrentEngineStepSpanId_Lifecycle()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, "trace-1", null);

        Assert.Null(trace.CurrentEngineStepSpanId);
        var runSpanId = trace.StartSpan(SpanTypes.EngineRun, null);
        Assert.Null(trace.CurrentEngineStepSpanId); // only engine.step updates it
        var stepSpanId = trace.StartSpan(SpanTypes.EngineStep, runSpanId);
        Assert.Equal(stepSpanId, trace.CurrentEngineStepSpanId);
        trace.EndSpan(stepSpanId);
        Assert.Null(trace.CurrentEngineStepSpanId);
    }

    [Fact]
    public async Task TraceCoordinator_StartSpan_NoRecorder_ReturnsNull()
    {
        var trace = new TraceCoordinator(null, "trace-1", null);
        Assert.Null(trace.StartSpan(SpanTypes.EngineRun, null));
        trace.EndSpan(null); // no-op, no throw
    }

    // ── P2: DynamicChildManager.Generate emits entry.generate/observed/ignored (§9.7) ──

    [Fact]
    public async Task Generate_Emission_WiredTrace_EmitsGenerateObservedIgnored()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);
        var ctx = new TraversalRuntimeContext("trace-1");
        var trace = new TraceCoordinator(recorder, "trace-1", ctx);

        // Open an engine.step so entry.generate/observed get a valid parent chain.
        var runSpanId = trace.StartSpan(SpanTypes.EngineRun, null);
        var stepSpanId = trace.StartSpan(SpanTypes.EngineStep, runSpanId);

        var registry = new DictionaryNodeRegistry();
        var mgr = new DynamicChildManager(registry, trace);

        var rules = new Dictionary<string, DynamicRule>
        {
            ["menu_rule"] = new DynamicRule(
                RuleId: "menu_rule",
                MatchCondition: new MatchCondition(Type: "menu_item"),
                ChildTemplate: "menu_container",
                Action: MatchAction.GenerateChild)
        };
        var parent = new TraversalNode("p", "parent", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, DynamicRules: rules));

        // First generate: 2 matching menu items → 2 entry.observed
        ctx.SetCurrentPageAnalysis(new PageAnalysis(
            Direction.Left, Direction.Top,
            Items: ImmutableArray.Create(
                new MenuItem("Network", new Coordinate(0.5, 0.5), MenuItemType.MenuItem),
                new MenuItem("Apps", new Coordinate(0.5, 0.5), MenuItemType.MenuItem))));

        mgr.Generate(parent, ctx);

        Assert.Equal(2, storage.GetSpansByType(SpanTypes.EntryObserved).Count);
        var genSpans = storage.GetSpansByType(SpanTypes.EntryGenerate);
        var genSpan = Assert.Single(genSpans);
        Assert.Equal(stepSpanId, genSpan.ParentSpanId);
        Assert.Equal("p", genSpan.Attributes!["entry.parent_node"]);
        Assert.Equal(2, genSpan.Attributes["entry.match_count"]);
        Assert.Equal(0, genSpan.Attributes["entry.ignored_count"]);
        foreach (var obs in storage.GetSpansByType(SpanTypes.EntryObserved))
            Assert.Equal(genSpan.SpanId, obs.ParentSpanId);

        // Second generate (same page, same fingerprint): dedup → 2 entry.ignored
        mgr.Generate(parent, ctx);

        Assert.Equal(2, storage.GetSpansByType(SpanTypes.EntryIgnored).Count);
        var genSpan2 = storage.GetSpansByType(SpanTypes.EntryGenerate).Last();
        Assert.Equal(0, genSpan2.Attributes!["entry.match_count"]);
        Assert.Equal(2, genSpan2.Attributes["entry.ignored_count"]);
        foreach (var ign in storage.GetSpansByType(SpanTypes.EntryIgnored))
            Assert.Equal("dedup", ign.Attributes!["entry.reason"]);

        trace.EndSpan(stepSpanId);
        trace.EndSpan(runSpanId, "all_visited");
    }

    // ── P2: full engine run reconstructs the span tree (§9.6/9.9) ──

    [Fact]
    public async Task FullRun_ReconstructsSpanTree_RootEngineRun()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);

        var vision = new StatefulMockVisionService(new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen").Button("btn_go", "Go", 0.5, 0.5))
            .Page("next", p => p.Name("NextScreen").BackButton("btn_back", 0.05, 0.05))
            .Transition(t => t.Id("go").Click("btn_go").From("home").To("next"))
            .Transition(t => t.Id("back").Click("btn_back").From("next").To("home"))
            .Build());
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new StatefulMockActionExecutor(vision);
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var plan = new TraversalPlan(
            EntryApp: "test", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan", PlanId: "test-001", RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());
        var engine = new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action, null, recorder);

        var result = await engine.RunAsync();
        Assert.True(result.Success);

        var query = new InMemoryTraceService(storage);
        var runSpan = query.GetRootSpan();
        Assert.NotNull(runSpan);
        Assert.Equal(SpanTypes.EngineRun, runSpan!.SpanType);
        Assert.NotNull(runSpan.EndTime);
        Assert.Equal("all_visited", runSpan.Status);

        var stepSpans = query.GetChildSpans(runSpan.SpanId);
        Assert.NotEmpty(stepSpans);
        Assert.All(stepSpans, s => Assert.Equal(SpanTypes.EngineStep, s.SpanType));
        Assert.All(stepSpans, s => Assert.NotNull(s.EndTime));
    }

    [Fact]
    public async Task FullRun_DynamicMatch_EntryGenerateParentsToEngineStep()
    {
        // Full engine run over a DynamicMatch root: entry.generate must parent to an engine.step
        // span via the TraceCoordinator link (CurrentEngineStepSpanId), not null.
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);

        var vision = new StatefulMockVisionService(new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Element("m1", e => e.Type("menu_item").Text("Network").At(0.5, 0.5))
                .Element("m2", e => e.Type("menu_item").Text("Apps").At(0.5, 0.6)))
            .Build());
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new StatefulMockActionExecutor(vision);

        var rules = new Dictionary<string, DynamicRule>
        {
            ["menu_rule"] = new DynamicRule(
                RuleId: "menu_rule",
                MatchCondition: new MatchCondition(Type: "menu_item"),
                ChildTemplate: "menu_container",
                Action: MatchAction.GenerateChild)
        };
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, DynamicRules: rules));
        var plan = new TraversalPlan(
            EntryApp: "test", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan", PlanId: "test-001", RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());
        var engine = new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action, null, recorder);

        await engine.RunAsync();

        var query = new InMemoryTraceService(storage);
        var runSpan = query.GetRootSpan();
        Assert.NotNull(runSpan);

        var stepIds = query.GetChildSpans(runSpan!.SpanId).Select(s => s.SpanId).ToHashSet();
        Assert.NotEmpty(stepIds);

        var genSpans = query.GetSpansByType(SpanTypes.EntryGenerate);
        Assert.NotEmpty(genSpans);
        Assert.All(genSpans, g => Assert.Contains(g.ParentSpanId, stepIds));

        var observed = query.GetSpansByType(SpanTypes.EntryObserved);
        Assert.NotEmpty(observed);
        Assert.All(observed, o => Assert.Contains(o.ParentSpanId, genSpans.Select(g => g.SpanId)));
    }

    // ── P3: PageAnalyzer emits ai.call + ai.analyze (§9.11) ──

    [Fact]
    public async Task PageAnalyzer_EmitsAiCall_WithAiAnalyzeChild()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("trace-1", DateTimeOffset.UtcNow));
        var recorder = new UniClaw.Core.Observability.InMemoryTraceRecorder(storage);

        var analyzer = new PageAnalyzer(
            new FakeVisionProvider(HappyPathJson()),
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            recorder);

        var page = await analyzer.AnalyzeCurrentPageAsync();
        Assert.NotNull(page);

        var query = new InMemoryTraceService(storage);
        var call = Assert.Single(query.GetSpansByType(SpanTypes.AiCall));
        Assert.Equal("fake-vision", call.Attributes!["ai.provider_id"]);
        Assert.Equal("ok", call.Status);
        Assert.NotNull(call.EndTime);
        Assert.True(call.Attributes["ai.success"] is bool s && s);
        Assert.Equal(250, call.Attributes["ai.tokens"]);

        var analyze = Assert.Single(query.GetSpansByType(SpanTypes.AiAnalyze));
        Assert.Equal(call.SpanId, analyze.ParentSpanId);
        Assert.Equal(4, analyze.Attributes!["ai.item_count"]);
        Assert.Equal(0, analyze.Attributes["ai.retry_count"]);
    }

    private static string HappyPathJson() =>
        "{\"level1_dir\":\"left\","
        + "\"level1_menus\":[{\"name\":\"Settings\",\"coordinate\":{\"x\":0.1,\"y\":0.5},\"active\":true}],"
        + "\"level2_dir\":\"top\",\"level2_menus\":[],"
        + "\"current_path\":[\"Settings\"],"
        + "\"items\":["
        + "{\"name\":\"WiFi\",\"type\":\"menu_item\",\"coordinate\":{\"x\":0.5,\"y\":0.2},\"parent\":null},"
        + "{\"name\":\"OK\",\"type\":\"button\",\"coordinate\":{\"x\":0.5,\"y\":0.3},\"parent\":null},"
        + "{\"name\":\"Airplane Mode\",\"type\":\"switch\",\"coordinate\":{\"x\":0.5,\"y\":0.4},\"parent\":null},"
        + "{\"name\":\"Description\",\"type\":\"text\",\"coordinate\":{\"x\":0.5,\"y\":0.5},\"parent\":null}"
        + "],\"is_popup\":false,\"popup_info\":null,\"close_button\":null,\"back_button\":null,"
        + "\"has_scroll\":false,\"is_end_of_list\":false}";

    private sealed class FakeVisionProvider : IModelProvider
    {
        private readonly string _content;
        public string ProviderId => "fake-vision";

        public FakeVisionProvider(string content) => _content = content;

        public Task<ModelResponse> CompleteVisionAsync(
            ModelRequest request,
            byte[] imageData,
            CancellationToken ct = default) =>
            Task.FromResult(new ModelResponse(
                _content, ProviderId, "vision", 50, 200, 15.0));

        public Task<ModelResponse> CompleteTextAsync(
            ModelRequest request,
            CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(
            ModelRequest request,
            byte[] imageData,
            CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeScreenCapture : IScreenCapture
    {
        private readonly byte[] _bytes;
        public FakeScreenCapture(byte[] bytes) => _bytes = bytes;
        public Task<byte[]> CaptureAsync(CancellationToken ct = default) =>
            Task.FromResult(_bytes);
        public Task<RawScreenBuffer> CaptureRawAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Raw capture not supported in test fake");
    }
}
