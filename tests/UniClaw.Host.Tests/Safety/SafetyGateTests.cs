using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Safety;

public sealed class SafetyGateTests
{
    private static readonly ScenarioSnapshot Snapshot =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"));

    [Fact]
    public void DangerousText_DenialOverridesNavigationAllowance()
    {
        var decision = Evaluator().Evaluate(
            Candidate(target: "Reset options", semantic: "navigation_row"));

        Assert.False(decision.Allowed);
        Assert.Equal("deny.dangerous.text", decision.RuleId);
    }

    [Fact]
    public void UnknownAction_IsDefaultDeny()
    {
        var decision = Evaluator().Evaluate(
            Candidate(action: "teleport", target: "About phone"));

        Assert.False(decision.Allowed);
        Assert.Equal("deny.allowlist.action", decision.RuleId);
    }

    [Fact]
    public void ToggleSemantic_IsDeniedWithoutDangerousLabel()
    {
        var decision = Evaluator().Evaluate(
            Candidate(target: "Wi-Fi", semantic: "toggle"));

        Assert.False(decision.Allowed);
        Assert.Equal("deny.dangerous.semantic", decision.RuleId);
    }

    [Fact]
    public void SafeTrustedNavigationRow_IsAllowedAndCorrelated()
    {
        var decision = Evaluator().Evaluate(Candidate());

        Assert.True(decision.Allowed);
        Assert.Equal("allow.navigation_row", decision.RuleId);
        Assert.Equal(Snapshot.PolicyHash, decision.PolicyHash);
        Assert.Equal("run-1", decision.RunId);
        Assert.Equal(3, decision.StepNumber);
        Assert.Equal("page-fingerprint", decision.PageFingerprint);
    }

    [Fact]
    public void CoreRootToLeafDepthTwo_IsAllowedForTrustedNavigation()
    {
        var decision = Evaluator().Evaluate(Candidate(depth: 2));

        Assert.True(decision.Allowed);
        Assert.Equal("allow.navigation_row", decision.RuleId);
    }

    [Fact]
    public async Task DeniedAction_HasZeroExecutorCallsButPersistsDecision()
    {
        var inner = new FakeActionExecutor();
        var sink = new InMemorySafetyDecisionSink();
        var context = new SafetyExecutionContext();
        var executor = new SafeActionExecutor(inner, Evaluator(), sink, context);
        using var scope = context.Push(
            Candidate(target: "Factory reset", semantic: "navigation_row"));

        var executed = await executor.TapAsync(0.5, 0.5);

        Assert.False(executed);
        Assert.Empty(inner.Calls);
        Assert.Single(sink.Decisions);
        Assert.Equal("deny.dangerous.text", sink.Decisions[0].RuleId);
    }

    [Theory]
    [InlineData("recovery")]
    [InlineData("popup")]
    [InlineData("traversal")]
    [InlineData("host")]
    public async Task EveryActionSource_UsesSameGate(string source)
    {
        var inner = new FakeActionExecutor();
        var sink = new InMemorySafetyDecisionSink();
        var context = new SafetyExecutionContext();
        var executor = new SafeActionExecutor(inner, Evaluator(), sink, context);
        using var scope = context.Push(
            Candidate(target: "Delete all data", source: source));

        Assert.False(await executor.TapAsync(0.2, 0.4));

        Assert.Empty(inner.Calls);
        Assert.Equal(source, sink.Decisions.Single().Source);
    }

    [Fact]
    public async Task EntryLaunch_UsesSameGateAndAllowsExplicitSettingsPreparation()
    {
        var inner = new FakeEntryDriver();
        var sink = new InMemorySafetyDecisionSink();
        var context = new SafetyExecutionContext();
        var driver = new SafeEntryActionDriver(
            inner,
            Evaluator(),
            sink,
            context);
        using var scope = context.Push(
            Candidate(
                action: "launch",
                target: "com.android.settings",
                semantic: "settings_home",
                isPreparation: true,
                source: "entry"));

        Assert.True(await driver.ColdLaunchAsync("com.android.settings"));

        Assert.Equal(["launch"], inner.Calls);
        Assert.Equal("allow.preparation", sink.Decisions.Single().RuleId);
    }

    [Fact]
    public async Task MissingScope_DefaultDeniesWithoutSideEffect()
    {
        var inner = new FakeActionExecutor();
        var executor = new SafeActionExecutor(
            inner,
            Evaluator(),
            new InMemorySafetyDecisionSink(),
            new SafetyExecutionContext());

        Assert.False(await executor.PressBackAsync());
        Assert.Empty(inner.Calls);
    }

    [Fact]
    public async Task TraceSink_RecordsAllowOrDenyWithStableCorrelation()
    {
        var storage = new InMemoryTraceStorage();
        var sink = new TraceSafetyDecisionSink(
            new InMemoryTraceRecorder(storage));
        var decision = Evaluator().Evaluate(Candidate());

        await sink.RecordAsync(decision);

        var record = Assert.Single(storage.GetExecutions());
        Assert.Equal("safety.click", record.Action);
        Assert.Equal("allow", record.Status);
        Assert.Equal(SpanType.StateDecision, record.SpanType);
        Assert.Equal("run-1", record.Context?.TraceId);
        Assert.Equal(3, record.Context?.StepNumber);
        Assert.Equal("allow.navigation_row", record.Metadata?["ruleId"]);
        Assert.Equal(Snapshot.PolicyHash, record.Metadata?["policyHash"]);
    }

    // ── D-134 P3: entry.skipped + action.* spans (§9.10/9.11) ──

    [Fact]
    public async Task Deny_EmitsEntrySkippedUnderLatestEntryVisited_AndStillJournals()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("run-1", DateTimeOffset.UtcNow));
        var recorder = new InMemoryTraceRecorder(storage);
        var service = new InMemoryTraceService(storage);
        var sink = new InMemorySafetyDecisionSink();
        var inner = new FakeActionExecutor();

        // Simulate Core-side OnBranch having pushed the current entry → entry.visited
        // (the design tree puts entry.skipped under the step's entry.visited).
        var visitedSpanId = await recorder.StartSpanAsync(
            SpanTypes.EntryVisited,
            SpanTypes.EntryVisited,
            null,
            new Dictionary<string, object> { ["entry.name"] = "About phone" });

        // Empty SafetyExecutionContext → unscoped candidate → default deny.
        var executor = new SafeActionExecutor(
            inner, Evaluator(), sink, new SafetyExecutionContext(), service, recorder);

        Assert.False(await executor.TapAsync(0.5, 0.5));
        Assert.Empty(inner.Calls);

        var skipped = Assert.Single(service.GetSpansByType(SpanTypes.EntrySkipped));
        Assert.Equal(visitedSpanId, skipped.ParentSpanId);
        Assert.Equal("click", skipped.Attributes!["entry.name"]);
        Assert.NotNull(skipped.Attributes["entry.rule_id"]);
        Assert.NotNull(skipped.Attributes["entry.reason"]);
        // The decision journal write is retained alongside the span.
        Assert.Single(sink.Decisions);
    }

    [Fact]
    public async Task AllowedAction_EmitsActionClickUnderLatestEntryVisited()
    {
        var storage = new InMemoryTraceStorage();
        storage.SetSession(new TraceSession("run-1", DateTimeOffset.UtcNow));
        var recorder = new InMemoryTraceRecorder(storage);
        var service = new InMemoryTraceService(storage);
        var sink = new InMemorySafetyDecisionSink();
        var inner = new FakeActionExecutor();
        var context = new SafetyExecutionContext();
        using var scope = context.Push(Candidate());

        var visitedSpanId = await recorder.StartSpanAsync(
            SpanTypes.EntryVisited,
            SpanTypes.EntryVisited,
            null,
            new Dictionary<string, object> { ["entry.name"] = "About phone" });

        var executor = new SafeActionExecutor(inner, Evaluator(), sink, context, service, recorder);

        Assert.True(await executor.TapAsync(0.5, 0.5));
        Assert.Equal(["click"], inner.Calls);

        var click = Assert.Single(service.GetSpansByType(SpanTypes.ActionClick));
        Assert.Equal(visitedSpanId, click.ParentSpanId);
        Assert.Equal("click", click.Attributes!["action.type"]);
        Assert.Equal("ok", click.Status);
        Assert.NotNull(click.EndTime);
        Assert.True(click.Attributes["action.result"] is bool r && r);
        Assert.True(click.Attributes.ContainsKey("action.adb_ms"));
    }

    private static SettingsSafetyEvaluator Evaluator() => new(Snapshot);

    private static SafetyCandidate Candidate(
        string action = "click",
        string? target = "About phone",
        string? semantic = "navigation_row",
        bool isPreparation = false,
        string source = "traversal",
        int depth = 1) =>
        new(
            action,
            target,
            semantic,
            "Settings",
            "Settings",
            "com.android.settings",
            0.99,
            true,
            isPreparation,
            depth,
            9,
            4,
            "run-1",
            3,
            "page-fingerprint",
            source);

    private sealed class FakeActionExecutor : IActionExecutor
    {
        public List<string> Calls { get; } = [];

        public Task<bool> TapAsync(
            double x,
            double y,
            CancellationToken cancellationToken = default) =>
            Called("click");

        public Task<bool> SwipeAsync(
            double startX,
            double startY,
            double endX,
            double endY,
            int durationMs,
            CancellationToken cancellationToken = default) =>
            Called("scroll");

        public Task<bool> PressBackAsync(
            CancellationToken cancellationToken = default) =>
            Called("back");

        public Task<bool> InputTextAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Called("input");

        public Task<bool> LongPressAsync(
            double x,
            double y,
            int durationMs,
            CancellationToken cancellationToken = default) =>
            Called("long_press");

        public Task WaitAsync(
            int milliseconds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("wait");
            return Task.CompletedTask;
        }

        public List<ActionRecord> GetHistory() => [];

        private Task<bool> Called(string action)
        {
            Calls.Add(action);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeEntryDriver : IEntryActionDriver
    {
        public List<string> Calls { get; } = [];

        public Task<bool> OpenDeepLinkAsync(
            string target,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("launch");
            return Task.FromResult(true);
        }

        public Task<bool> ColdLaunchAsync(
            string targetApp,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("launch");
            return Task.FromResult(true);
        }

        public Task WaitAsync(
            int milliseconds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("wait");
            return Task.CompletedTask;
        }

        public Task<bool> CheckConditionAsync(
            IReadOnlyDictionary<string, object>? waitCondition,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
