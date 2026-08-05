using System.Collections.Immutable;
using System.Globalization;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Analysis;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using UniClaw.Host.Tests.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Trace;

/// <summary>
/// AC1 differential snapshot gate (trace-span-helpers design.md) — freezes the span
/// tree emitted by the CURRENT pre-migration production code for five scenarios
/// (S1–S5). M1–M4 migrations must leave this suite byte-for-byte green; any diff
/// means behavior drift and the tier is rolled back.
///
/// Snapshot format (canonical dump, one line per span, insertion/sibling order):
///   S{n} spanType | spanName | status | parent=S{m}|- | end=open|closed | attrs=k=v,...(sorted)
/// Dynamic values normalized away: span ids become tree-traversal numbers S1..Sn,
/// timestamps and DurationMs are stripped; only the open/closed flag (derived from
/// EndTime, not its value) survives. Attributes compare sorted.
/// </summary>
public sealed class SpanTreeEquivalenceTests
{
    // ── S1: 成功枚举 mock run（全链路: engine → safety gate → 分析器）──

    [Fact]
    public async Task S1_SuccessEnumerateMockRun_FrozenSnapshot()
    {
        var (storage, recorder, service) = CreateTrace();

        // Full composition: TraversalEngine + SafeActionExecutor (unscoped context →
        // every action denied → entry.skipped under the latest entry.visited) +
        // EnumerateCompletionAnalyzer (analyze.completion).
        var snapshot = LoadScenarioSnapshot();
        var vision = new StatefulMockVisionService(new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen")
                .Element("m_network", e => e.Type("menu_item").Text("Network").At(0.5, 0.2))
                .Element("m_bluetooth", e => e.Type("menu_item").Text("Bluetooth").At(0.5, 0.4))
                .Element("m_wifi", e => e.Type("menu_item").Text("Wi-Fi").At(0.5, 0.6)))
            .Build());
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var safeAction = new SafeActionExecutor(
            new StatefulMockActionExecutor(vision),
            new SettingsSafetyEvaluator(snapshot),
            new InMemorySafetyDecisionSink(),
            new SafetyExecutionContext(),
            service,
            recorder);
        var engine = new TraversalEngine(
            Plan(DynamicMatchRoot()),
            brain,
            new DefaultScreenStateProvider(),
            safeAction,
            config: null,
            recorder);

        var result = await engine.RunAsync();
        Assert.True(result.Success, $"S1 engine run failed: {result.CompletionReason}");

        // Post-run analyzer poll writes analyze.completion into the same tree.
        await new EnumerateCompletionAnalyzer(recorder).EvaluateAsync(service);

        AssertTree(SpanTreeSnapshot.Dump(service.GetAllSpans()), S1Expected,
            "S1 success enumerate mock run");
    }

    // ── S2: safety deny 的 action（幽灵 action.* span 回归防护）──

    [Fact]
    public async Task S2_SafetyDeniedAction_NoActionSpan_AndSkippedParent()
    {
        var (storage, recorder, service) = CreateTrace();

        // Core-side OnBranch pushed the current entry → entry.visited (unclosed marker).
        var visitedSpanId = await recorder.StartSpanAsync(
            SpanTypes.EntryVisited,
            SpanTypes.EntryVisited,
            parentSpanId: null,
            new Dictionary<string, object> { ["entry.name"] = "About phone" });

        // Empty SafetyExecutionContext → unscoped candidate → default deny.
        var executor = new SafeActionExecutor(
            new FakeActionExecutor(),
            new SettingsSafetyEvaluator(LoadScenarioSnapshot()),
            new InMemorySafetyDecisionSink(),
            new SafetyExecutionContext(),
            service,
            recorder);

        Assert.False(await executor.TapAsync(0.5, 0.5));

        var spans = service.GetAllSpans();

        // deny-gate ghost-span regression guard: no action.* span on a denied run.
        Assert.DoesNotContain(spans, s => s.SpanType.StartsWith("action.", StringComparison.Ordinal));
        Assert.DoesNotContain(SpanTreeSnapshot.Dump(spans), line => line.Contains(" action."));

        var skipped = Assert.Single(spans.Where(s => s.SpanType == SpanTypes.EntrySkipped));
        Assert.Equal(visitedSpanId, skipped.ParentSpanId);
        Assert.Null(skipped.EndTime);

        AssertTree(SpanTreeSnapshot.Dump(spans), S2Expected,
            "S2 safety denied action");
    }

    // ── S3: 5 连 all-skipped error loop ──

    [Fact]
    public async Task S3_FiveConsecutiveAllSkippedSteps_ErrorLoopSpanFrozen()
    {
        var (storage, recorder, service) = CreateTrace();

        // 5 consecutive engine.step spans, each with ONLY entry.skipped children
        // (same span-tree fixture ErrorLoopAnalyzerTests builds).
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        for (var i = 0; i < 5; i++)
        {
            storage.OpenSpan(SpanTypes.EngineStep, $"step {i}", $"s{i}", "run",
                start.AddSeconds(i + 1), null, null);
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", $"s{i}",
                start.AddSeconds(i + 1.1), null, null);
        }

        var verdict = await new ErrorLoopAnalyzer(recorder).EvaluateAsync(service);
        Assert.NotNull(verdict);
        Assert.Contains("stuck_in_error_loop", verdict!.Reason);

        var loop = Assert.Single(service.GetSpansByType(SpanTypes.AnalyzeErrorLoop));
        Assert.Equal("error loop: stuck_in_error_loop", loop.SpanName);
        Assert.Equal("stuck_in_error_loop", loop.Attributes!["error.reason"]);
        Assert.Equal(5, loop.Attributes["error.consecutive_steps"]);

        AssertTree(SpanTreeSnapshot.Dump(service.GetAllSpans()), S3Expected,
            "S3 five-consecutive all-skipped error loop");
    }

    [Fact]
    public async Task S3_NormalRun_WritesNoErrorLoopSpan()
    {
        var (storage, recorder, service) = CreateTrace();

        // Healthy run: two steps with visited > skipped each — no rule fires.
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        storage.OpenSpan(SpanTypes.EngineRun, "run", "run", null, start, null, null);
        for (var i = 0; i < 2; i++)
        {
            storage.OpenSpan(SpanTypes.EngineStep, $"step {i}", $"s{i}", "run",
                start.AddSeconds(i + 1), null, null);
            storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i} a", $"v{i}a", $"s{i}",
                start.AddSeconds(i + 1.1), null, null);
            storage.OpenSpan(SpanTypes.EntryVisited, $"visited {i} b", $"v{i}b", $"s{i}",
                start.AddSeconds(i + 1.2), null, null);
            storage.OpenSpan(SpanTypes.EntrySkipped, $"skip {i}", $"sk{i}", $"s{i}",
                start.AddSeconds(i + 1.3), null, null);
        }

        var verdict = await new ErrorLoopAnalyzer(recorder).EvaluateAsync(service);
        Assert.NotNull(verdict);
        Assert.False(verdict!.ShouldTerminate);

        Assert.Empty(service.GetSpansByType(SpanTypes.AnalyzeErrorLoop));
    }

    // ── S4: AI 失败路径（ai.call error + ai.analyze 事件语义）──

    [Fact]
    public async Task S4_AiFailurePath_RetryThenSuccess_FrozenSnapshot()
    {
        var (storage, recorder, service) = CreateTrace();

        // M1 + 2.7 re-freeze (trace-parent-linkage): the fixture opens an engine.step span
        // directly on the recorder and publishes its id through EngineStepSpanContext — the
        // same AsyncLocal channel the engine's step scope uses (Push at scope open, Pop at
        // close) — and PageAnalyzer receives the singleton instance as its
        // ITraceContextProvider, so ai.call parent = engine.step span id.
        // Provider failure flow unchanged: ai.call #1 closes "error" with ai.success=false,
        // retry #2 succeeds; ai.analyze stays unclosed (EndTime == null — event semantics).
        var stepSpanId = await recorder.StartSpanAsync(SpanTypes.EngineStep, SpanTypes.EngineStep);
        EngineStepSpanContext.Instance.Push(stepSpanId);

        var analyzer = new PageAnalyzer(
            new FailOnceThenSuccessVisionProvider(),
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            recorder,
            EngineStepSpanContext.Instance);

        try
        {
            var page = await analyzer.AnalyzeCurrentPageAsync();
            Assert.NotNull(page);

            var calls = service.GetSpansByType(SpanTypes.AiCall);
            Assert.Equal(2, calls.Count);
            Assert.Equal("error", calls[0].Status);
            Assert.False((bool)calls[0].Attributes!["ai.success"]);
            Assert.Equal("ok", calls[1].Status);
            Assert.Equal(stepSpanId, calls[0].ParentSpanId);
            Assert.Equal(stepSpanId, calls[1].ParentSpanId);

            var analyze = Assert.Single(service.GetSpansByType(SpanTypes.AiAnalyze));
            Assert.Null(analyze.EndTime);
            Assert.Equal(calls[1].SpanId, analyze.ParentSpanId);
        }
        finally
        {
            EngineStepSpanContext.Instance.Pop();
            await recorder.EndSpanAsync(stepSpanId);
        }

        AssertTree(SpanTreeSnapshot.Dump(service.GetAllSpans()), S4Expected,
            "S4 AI failure path (retry then success)");
    }

    // ── S4b: 非引擎入口 — ai.call 保留孤儿根（AC7 双向覆盖）──

    [Fact]
    public async Task NonEngineEntry_AiCallRemainsRoot_OrphanPreserved()
    {
        var (storage, recorder, service) = CreateTrace();

        // No ITraceContextProvider injected → ai.call parentSpanId is null (root span).
        // Orphan spans are preserved, not suppressed; ai.analyze parent = ai.call.
        var analyzer = new PageAnalyzer(
            new FailOnceThenSuccessVisionProvider(),
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            recorder);

        var page = await analyzer.AnalyzeCurrentPageAsync();
        Assert.NotNull(page);

        var calls = service.GetSpansByType(SpanTypes.AiCall);
        Assert.Equal(2, calls.Count);
        Assert.All(calls, c => Assert.Null(c.ParentSpanId));

        var analyze = Assert.Single(service.GetSpansByType(SpanTypes.AiAnalyze));
        Assert.Equal(calls[1].SpanId, analyze.ParentSpanId);

        // Canonical dump confirms the root form: both ai.call lines are parent=-.
        var dump = SpanTreeSnapshot.Dump(service.GetAllSpans());
        var callLines = dump.Where(l => l.Contains(" ai.call |")).ToList();
        Assert.Equal(2, callLines.Count);
        Assert.All(callLines, l => Assert.Contains("| parent=- |", l));
    }

    // ── S5: 父链归属（含滚动 dedup → entry.ignored）──

    [Fact]
    public async Task S5_ParentChain_EngineRunWithScrollDedup_FrozenSnapshot()
    {
        var (storage, recorder, service) = CreateTrace();

        // Full engine run: DynamicMatch root → scrollable page → cache invalidation
        // on scroll re-generates the same fingerprint → dedup hits → entry.ignored.
        var vision = new FixedPageVisionProvider(3);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var engine = new TraversalEngine(
            Plan(DynamicMatchRoot()),
            brain,
            new ScrollableScreenStateProvider(),
            new FakeActionExecutor(),
            config: null,
            recorder);

        var result = await engine.RunAsync();
        Assert.True(result.Success, $"S5 engine run failed: {result.CompletionReason}");

        var spans = service.GetAllSpans();

        // Explicit parent-chain assertions (design.md S5).
        var gen = service.GetSpansByType(SpanTypes.EntryGenerate);
        Assert.NotEmpty(gen);
        var observed = service.GetSpansByType(SpanTypes.EntryObserved);
        var ignored = service.GetSpansByType(SpanTypes.EntryIgnored);
        Assert.NotEmpty(observed);
        Assert.NotEmpty(ignored);
        Assert.All(observed, o => Assert.Contains(gen, g => g.SpanId == o.ParentSpanId));
        Assert.All(ignored, i => Assert.Contains(gen, g => g.SpanId == i.ParentSpanId));

        var steps = service.GetSpansByType(SpanTypes.EngineStep);
        var visited = service.GetSpansByType(SpanTypes.EntryVisited);
        Assert.NotEmpty(steps);
        Assert.NotEmpty(visited);
        Assert.All(visited, v => Assert.Contains(steps, s => s.SpanId == v.ParentSpanId));

        var run = service.GetRootSpan();
        Assert.NotNull(run);
        Assert.All(steps, s => Assert.Equal(run!.SpanId, s.ParentSpanId));

        AssertTree(SpanTreeSnapshot.Dump(spans), S5Expected,
            "S5 parent chain (engine run with scroll dedup)");
    }

    // ── S6: 完整父链 engine.run → engine.step → ai.call → ai.analyze（含重试）──

    [Fact]
    public async Task S6_FullParentChain_WithRetry_FrozenSnapshot()
    {
        var (storage, recorder, service) = CreateTrace();

        // M1 + 2.7 (trace-parent-linkage): the complete parent chain in one tree. The fixture
        // drives the engine-span lifecycle directly on the recorder (engine.run → engine.step,
        // as the engine's RunAsync does) and publishes the open engine.step id through
        // EngineStepSpanContext — the same AsyncLocal channel the engine uses (Push at step-scope
        // open, Pop at close) — while PageAnalyzer receives the singleton instance as its
        // ITraceContextProvider. FailOnceThenSuccessVisionProvider fails attempt #1 and succeeds
        // on the retry → ai.call #1 (error) + ai.call #2 (ok), both parented to the engine.step.
        var runSpanId = await recorder.StartSpanAsync(SpanTypes.EngineRun, SpanTypes.EngineRun);
        var stepSpanId = await recorder.StartSpanAsync(
            SpanTypes.EngineStep, SpanTypes.EngineStep, parentSpanId: runSpanId);
        EngineStepSpanContext.Instance.Push(stepSpanId);
        Assert.Equal(stepSpanId, EngineStepSpanContext.Instance.CurrentSpanId);

        var analyzer = new PageAnalyzer(
            new FailOnceThenSuccessVisionProvider(),
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            recorder,
            EngineStepSpanContext.Instance);

        try
        {
            var page = await analyzer.AnalyzeCurrentPageAsync();
            Assert.NotNull(page);
        }
        finally
        {
            EngineStepSpanContext.Instance.Pop();
            await recorder.EndSpanAsync(stepSpanId);
            await recorder.EndSpanAsync(runSpanId);
        }

        var spans = service.GetAllSpans();

        // Explicit chain assertions: engine.run → engine.step → ai.call → ai.analyze.
        var run = service.GetRootSpan();
        Assert.NotNull(run);
        var step = Assert.Single(service.GetSpansByType(SpanTypes.EngineStep));
        Assert.Equal(run!.SpanId, step.ParentSpanId);
        var calls = service.GetSpansByType(SpanTypes.AiCall);
        Assert.Equal(2, calls.Count);
        Assert.Equal(step.SpanId, calls[0].ParentSpanId);
        Assert.Equal(step.SpanId, calls[1].ParentSpanId);
        var analyze = Assert.Single(service.GetSpansByType(SpanTypes.AiAnalyze));
        Assert.Equal(calls[1].SpanId, analyze.ParentSpanId);
        // Retry path: successful attempt is attempt index 1 (0-based) → ai.retry_count >= 1.
        Assert.True((int)analyze.Attributes!["ai.retry_count"] >= 1);

        AssertTree(SpanTreeSnapshot.Dump(spans), S6Expected,
            "S6 full parent chain (engine.run → engine.step → ai.call → ai.analyze, with retry)");
    }

    // ── Shared fixtures ──────────────────────────────────────

    private static (InMemoryTraceStorage Storage, InMemoryTraceRecorder Recorder, InMemoryTraceService Service)
        CreateTrace()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("run-1", DateTimeOffset.UtcNow));
        var recorder = new InMemoryTraceRecorder(storage);
        return (storage, recorder, new InMemoryTraceService(storage));
    }

    private static ScenarioSnapshot LoadScenarioSnapshot() =>
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(AppContext.BaseDirectory, "Scenarios", "locate-one-item.v1.json"));

    private static TraversalNode DynamicMatchRoot()
    {
        var rules = new Dictionary<string, DynamicRule>
        {
            ["menu_rule"] = new DynamicRule(
                RuleId: "menu_rule",
                MatchCondition: new MatchCondition(Type: "menu_item"),
                ChildTemplate: "menu_container",
                Action: MatchAction.GenerateChild),
        };
        return new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, DynamicRules: rules));
    }

    private static TraversalPlan Plan(TraversalNode root) =>
        new(
            EntryApp: "test",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan",
            PlanId: "test-001",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>());

    private static void AssertTree(IReadOnlyList<string> actual, string[] expected, string scenario)
    {
        Assert.True(
            actual.SequenceEqual(expected),
            $"{scenario}: snapshot mismatch\n" +
            $"--- expected ({expected.Length}) ---\n{string.Join('\n', expected)}\n" +
            $"--- actual ({actual.Count}) ---\n{string.Join('\n', actual)}");
    }

    // ── Frozen snapshots (captured from pre-migration behavior 2026-08-03) ──

    // S1 — full engine run (dynamic-match root, 3 menu items, safety-denied children)
    //       + post-run EnumerateCompletionAnalyzer poll. Frozen verbatim 2026-08-03.
    private static readonly string[] S1Expected =
    [
        "S1 engine.run | engine.run | all_visited | parent=- | end=closed | attrs=",
        "S2 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S3 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S4 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S5 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S6 entry.generate | entry.generate | ok | parent=S5 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=0, entry.match_count=3, entry.parent_node=root",
        "S7 entry.observed | entry.observed | ok | parent=S6 | end=open | attrs=entry.index=0, entry.match_rule=menu_rule, entry.name=Network, entry.node_id=dyn_menu_container_Network_root, entry.parent=root",
        "S8 entry.observed | entry.observed | ok | parent=S6 | end=open | attrs=entry.index=1, entry.match_rule=menu_rule, entry.name=Bluetooth, entry.node_id=dyn_menu_container_Bluetooth_root, entry.parent=root",
        "S9 entry.observed | entry.observed | ok | parent=S6 | end=open | attrs=entry.index=2, entry.match_rule=menu_rule, entry.name=Wi-Fi, entry.node_id=dyn_menu_container_Wi-Fi_root, entry.parent=root",
        "S10 entry.visited | entry.visited | ok | parent=S5 | end=open | attrs=entry.depth=2, entry.name=Network, entry.node_id=dyn_menu_container_Network_root, entry.step=4",
        "S11 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S12 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S13 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S14 entry.skipped | entry.skipped | ok | parent=S10 | end=open | attrs=entry.name=click, entry.reason=Step budget is exhausted., entry.rule_id=deny.boundary.step_budget",
        "S15 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S16 entry.generate | entry.generate | ok | parent=S15 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Network_root",
        "S17 entry.ignored | entry.ignored | ok | parent=S16 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S18 entry.ignored | entry.ignored | ok | parent=S16 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S19 entry.ignored | entry.ignored | ok | parent=S16 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S20 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S21 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S22 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S23 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S24 entry.skipped | entry.skipped | ok | parent=S10 | end=open | attrs=entry.name=click, entry.reason=Step budget is exhausted., entry.rule_id=deny.boundary.step_budget",
        "S25 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S26 entry.generate | entry.generate | ok | parent=S25 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Bluetooth_root",
        "S27 entry.ignored | entry.ignored | ok | parent=S26 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S28 entry.ignored | entry.ignored | ok | parent=S26 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S29 entry.ignored | entry.ignored | ok | parent=S26 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S30 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S31 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S32 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S33 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S34 entry.skipped | entry.skipped | ok | parent=S10 | end=open | attrs=entry.name=click, entry.reason=Step budget is exhausted., entry.rule_id=deny.boundary.step_budget",
        "S35 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S36 entry.generate | entry.generate | ok | parent=S35 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Wi-Fi_root",
        "S37 entry.ignored | entry.ignored | ok | parent=S36 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S38 entry.ignored | entry.ignored | ok | parent=S36 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S39 entry.ignored | entry.ignored | ok | parent=S36 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S40 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S41 analyze.completion | enumerate completion check | ok | parent=- | end=closed | attrs=analyze.cold_start=true, analyze.end_reached=true, analyze.observed=3, analyze.p50=14, analyze.p95=21, analyze.pending=0, analyze.rule=halt: pending=0 and end reached, analyze.skipped=3, analyze.visited=1",
    ];

    // S2 — safety-denied action: only the entry.visited marker + entry.skipped
    //       (parent = latest visited, unclosed). No action.* span anywhere.
    private static readonly string[] S2Expected =
    [
        "S1 entry.visited | entry.visited | ok | parent=- | end=open | attrs=entry.name=About phone",
        "S2 entry.skipped | entry.skipped | ok | parent=S1 | end=open | attrs=entry.name=click, entry.reason=Step budget is exhausted., entry.rule_id=deny.boundary.step_budget",
    ];

    // S3 — 5 consecutive all-skipped steps → analyze.error_loop (dynamic spanName +
    //       whole-dictionary attrs), parented at tree root after the 5 steps.
    private static readonly string[] S3Expected =
    [
        "S1 engine.run | run | ok | parent=- | end=open | attrs=",
        "S2 engine.step | step 0 | ok | parent=S1 | end=open | attrs=",
        "S3 entry.skipped | skip 0 | ok | parent=S2 | end=open | attrs=",
        "S4 engine.step | step 1 | ok | parent=S1 | end=open | attrs=",
        "S5 entry.skipped | skip 1 | ok | parent=S4 | end=open | attrs=",
        "S6 engine.step | step 2 | ok | parent=S1 | end=open | attrs=",
        "S7 entry.skipped | skip 2 | ok | parent=S6 | end=open | attrs=",
        "S8 engine.step | step 3 | ok | parent=S1 | end=open | attrs=",
        "S9 entry.skipped | skip 3 | ok | parent=S8 | end=open | attrs=",
        "S10 engine.step | step 4 | ok | parent=S1 | end=open | attrs=",
        "S11 entry.skipped | skip 4 | ok | parent=S10 | end=open | attrs=",
        "S12 analyze.error_loop | error loop: stuck_in_error_loop | ok | parent=- | end=closed | attrs=error.consecutive_steps=5, error.reason=stuck_in_error_loop",
    ];

    // S4 — AI failure path under an open engine.step (M1+2.7 re-freeze 2026-08-03): fixture
    //       opens engine.step on the recorder and publishes its id via EngineStepSpanContext
    //       (the production AsyncLocal channel), injected as the ITraceContextProvider;
    //       ai.call #1 error (ai.success=false) and retry #2 ok are both parented to the
    //       engine.step; ai.analyze unclosed event marker under the retry call.
    private static readonly string[] S4Expected =
    [
        "S1 engine.step | engine.step | ok | parent=- | end=closed | attrs=",
        "S2 ai.call | ai.call | error | parent=S1 | end=closed | attrs=ai.capability=analyze_visual, ai.mode=vision, ai.success=false",
        "S3 ai.call | ai.call | ok | parent=S1 | end=closed | attrs=ai.capability=analyze_visual, ai.latency_ms=15, ai.mode=vision, ai.model=, ai.provider_id=fake-vision, ai.success=true, ai.tokens=250",
        "S4 ai.analyze | ai.analyze | ok | parent=S3 | end=open | attrs=ai.item_count=4, ai.retry_count=1",
    ];

    // S5 — parent chain: observed/ignored → generate, visited → step, step → run;
    //       scrollable page drives 3 dedup regeneration rounds (entry.ignored).
    private static readonly string[] S5Expected =
    [
        "S1 engine.run | engine.run | all_visited | parent=- | end=closed | attrs=",
        "S2 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S3 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S4 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S5 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S6 entry.generate | entry.generate | ok | parent=S5 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=0, entry.match_count=3, entry.parent_node=root",
        "S7 entry.observed | entry.observed | ok | parent=S6 | end=open | attrs=entry.index=0, entry.match_rule=menu_rule, entry.name=Network, entry.node_id=dyn_menu_container_Network_root, entry.parent=root",
        "S8 entry.observed | entry.observed | ok | parent=S6 | end=open | attrs=entry.index=1, entry.match_rule=menu_rule, entry.name=Bluetooth, entry.node_id=dyn_menu_container_Bluetooth_root, entry.parent=root",
        "S9 entry.observed | entry.observed | ok | parent=S6 | end=open | attrs=entry.index=2, entry.match_rule=menu_rule, entry.name=Wi-Fi, entry.node_id=dyn_menu_container_Wi-Fi_root, entry.parent=root",
        "S10 entry.visited | entry.visited | ok | parent=S5 | end=open | attrs=entry.depth=2, entry.name=Network, entry.node_id=dyn_menu_container_Network_root, entry.step=4",
        "S11 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S12 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S13 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S14 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S15 entry.generate | entry.generate | ok | parent=S14 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Network_root",
        "S16 entry.ignored | entry.ignored | ok | parent=S15 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S17 entry.ignored | entry.ignored | ok | parent=S15 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S18 entry.ignored | entry.ignored | ok | parent=S15 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S19 entry.generate | entry.generate | ok | parent=S14 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Network_root",
        "S20 entry.ignored | entry.ignored | ok | parent=S19 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S21 entry.ignored | entry.ignored | ok | parent=S19 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S22 entry.ignored | entry.ignored | ok | parent=S19 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S23 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S24 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S25 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S26 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S27 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S28 entry.generate | entry.generate | ok | parent=S27 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Bluetooth_root",
        "S29 entry.ignored | entry.ignored | ok | parent=S28 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S30 entry.ignored | entry.ignored | ok | parent=S28 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S31 entry.ignored | entry.ignored | ok | parent=S28 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S32 entry.generate | entry.generate | ok | parent=S27 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Bluetooth_root",
        "S33 entry.ignored | entry.ignored | ok | parent=S32 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S34 entry.ignored | entry.ignored | ok | parent=S32 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S35 entry.ignored | entry.ignored | ok | parent=S32 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S36 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S37 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S38 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S39 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S40 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S41 entry.generate | entry.generate | ok | parent=S40 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Wi-Fi_root",
        "S42 entry.ignored | entry.ignored | ok | parent=S41 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S43 entry.ignored | entry.ignored | ok | parent=S41 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S44 entry.ignored | entry.ignored | ok | parent=S41 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S45 entry.generate | entry.generate | ok | parent=S40 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=dyn_menu_container_Wi-Fi_root",
        "S46 entry.ignored | entry.ignored | ok | parent=S45 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S47 entry.ignored | entry.ignored | ok | parent=S45 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S48 entry.ignored | entry.ignored | ok | parent=S45 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S49 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S50 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S51 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S52 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S53 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S54 entry.generate | entry.generate | ok | parent=S53 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=root",
        "S55 entry.ignored | entry.ignored | ok | parent=S54 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S56 entry.ignored | entry.ignored | ok | parent=S54 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S57 entry.ignored | entry.ignored | ok | parent=S54 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S58 entry.generate | entry.generate | ok | parent=S53 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=root",
        "S59 entry.ignored | entry.ignored | ok | parent=S58 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S60 entry.ignored | entry.ignored | ok | parent=S58 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S61 entry.ignored | entry.ignored | ok | parent=S58 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S62 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S63 entry.generate | entry.generate | ok | parent=S62 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=root",
        "S64 entry.ignored | entry.ignored | ok | parent=S63 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S65 entry.ignored | entry.ignored | ok | parent=S63 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S66 entry.ignored | entry.ignored | ok | parent=S63 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
        "S67 entry.generate | entry.generate | ok | parent=S62 | end=closed | attrs=entry.fingerprint=1258285051, entry.ignored_count=3, entry.match_count=0, entry.parent_node=root",
        "S68 entry.ignored | entry.ignored | ok | parent=S67 | end=open | attrs=entry.name=menu_container_Network, entry.reason=dedup",
        "S69 entry.ignored | entry.ignored | ok | parent=S67 | end=open | attrs=entry.name=menu_container_Bluetooth, entry.reason=dedup",
        "S70 entry.ignored | entry.ignored | ok | parent=S67 | end=open | attrs=entry.name=menu_container_Wi-Fi, entry.reason=dedup",
    ];

    // S6 — complete parent chain (M1+2.7, 2026-08-03): engine.run → engine.step → ai.call →
    //       ai.analyze, with the retry path (ai.call #1 error + #2 ok under the same
    //       engine.step). Parentage flows through EngineStepSpanContext, the production
    //       AsyncLocal channel. Same canonical format as S1–S5.
    private static readonly string[] S6Expected =
    [
        "S1 engine.run | engine.run | ok | parent=- | end=closed | attrs=",
        "S2 engine.step | engine.step | ok | parent=S1 | end=closed | attrs=",
        "S3 ai.call | ai.call | error | parent=S2 | end=closed | attrs=ai.capability=analyze_visual, ai.mode=vision, ai.success=false",
        "S4 ai.call | ai.call | ok | parent=S2 | end=closed | attrs=ai.capability=analyze_visual, ai.latency_ms=15, ai.mode=vision, ai.model=, ai.provider_id=fake-vision, ai.success=true, ai.tokens=250",
        "S5 ai.analyze | ai.analyze | ok | parent=S4 | end=open | attrs=ai.item_count=4, ai.retry_count=1",
    ];

    // ── Test-local fakes (production types only; no src/ modifications) ──

    /// <summary>Deterministic vision: fixed page with N menu_item entries; no navigation.</summary>
    private sealed class FixedPageVisionProvider : IPageAnalyzer
    {
        private readonly PageAnalysis _page;

        public FixedPageVisionProvider(int itemCount)
        {
            var names = new[] { "Network", "Bluetooth", "Wi-Fi" };
            var items = ImmutableArray.CreateRange(
                Enumerable.Range(0, itemCount).Select(i => new MenuItem(
                    names[i % names.Length],
                    new Coordinate(0.5, 0.2 + i * 0.2),
                    MenuItemType.MenuItem,
                    ExpectedAction: ExpectedAction.Navigate)));
            _page = new PageAnalysis(
                Direction.Left,
                Direction.Top,
                CurrentPath: ImmutableArray.Create("Settings"),
                Items: items,
                HasScroll: true,
                IsEndOfList: false);
        }

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
            => Task.FromResult<PageAnalysis?>(_page);

        public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
            => Task.FromResult<AppEntryPoint?>(null);

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default) =>
            Task.FromResult(new PageTypeVerification(false, 0.0, expectedType));
    }

    /// <summary>Scrollable screen state so the engine exercises TryHandleScrollAsync.</summary>
    private sealed class ScrollableScreenStateProvider : IScreenStateProvider
    {
        public bool HasScroll() => true;
        public double GetScrollProgress() => 0.0;
        public bool IsEndOfList() => false;
        public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
    }

    /// <summary>Model provider: first call fails transiently, retry succeeds.</summary>
    private sealed class FailOnceThenSuccessVisionProvider : IModelProvider
    {
        private int _calls;
        public string ProviderId => "fake-vision";

        public Task<ModelResponse> CompleteVisionAsync(
            ModelRequest request,
            byte[] imageData,
            CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _calls) == 1)
                throw new DomainValidationException(
                    nameof(ModelCapabilities.AnalyzeVisual),
                    null,
                    "analyze_visual model call failed: transient");
            return Task.FromResult(new ModelResponse(
                HappyPathJson(), ProviderId, "vision", 50, 200, 15.0));
        }

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
}

/// <summary>
/// Canonical span-tree dump — the snapshot format AC1 freezes:
/// span ids normalized to tree-traversal numbers (insertion order = sibling order),
/// timestamps/DurationMs stripped, only open/closed survives, attrs sorted.
/// </summary>
internal static class SpanTreeSnapshot
{
    public static List<string> Dump(IReadOnlyList<TraceSpan> spans)
    {
        var canonical = new Dictionary<string, string>(spans.Count);
        for (var i = 0; i < spans.Count; i++)
            canonical[spans[i].SpanId] = $"S{i + 1}";

        return spans
            .Select(span => Line(span, canonical))
            .ToList();
    }

    private static string Line(TraceSpan span, IReadOnlyDictionary<string, string> canonical)
    {
        var id = canonical[span.SpanId];
        var parent = span.ParentSpanId is not null
                     && canonical.TryGetValue(span.ParentSpanId, out var parentId)
            ? parentId
            : "-";
        var end = span.EndTime.HasValue ? "closed" : "open";
        return $"{id} {span.SpanType} | {span.SpanName} | {span.Status} | parent={parent} | end={end} | attrs={FormatAttrs(span.Attributes)}";
    }

    private static string FormatAttrs(Dictionary<string, object>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
            return "";
        return string.Join(", ",
            attributes.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={FormatValue(kv.Value)}"));
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "<null>",
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "<null>",
    };
}
