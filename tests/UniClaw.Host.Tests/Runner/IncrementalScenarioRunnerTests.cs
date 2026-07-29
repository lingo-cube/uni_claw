using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Runner;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

public sealed class IncrementalScenarioRunnerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-runner-{Guid.NewGuid():N}");

    private static readonly ScenarioSnapshot DefaultSnapshot =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"));

    [Fact]
    public async Task Compiler_IsDeterministicAndPlanIsPersistedBeforeExecution()
    {
        var compiler = new ScenarioPlanCompiler();
        var first = compiler.Compile(DefaultSnapshot);
        var second = compiler.Compile(DefaultSnapshot);
        var assets = await new RunAssetStore().CreateAsync(
            _root,
            DefaultSnapshot,
            first,
            Manifest("plan-run"));

        Assert.Equal(first.ToJson(), second.ToJson());
        Assert.Equal("target_only", first.IntentSlots?.Scope);
        Assert.Equal("menu_only", first.IntentSlots?.ElementHandling);
        Assert.Equal(DefaultSnapshot.ScenarioHash, first.Meta?["scenarioHash"]);
        Assert.True(File.Exists(Path.Combine(assets.RunDirectory, "plan.json")));
    }

    [Fact]
    public async Task TargetVisible_ExecutesOneAllowedClickAndVerifiesPage()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("about", "About phone"),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("target_page_identity_verified", outcome.CompletionReason);
        Assert.Equal(["click"], harness.Actions.Calls);
        Assert.Equal(1, outcome.SafetyAllowed);
        Assert.Equal(0, outcome.SafetyDenied);
    }

    [Fact]
    public async Task TargetAfterScroll_UsesBoundedScrollThenOneClick()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home-0", "Settings", hasScroll: true),
                Observation("home-0", "Settings", hasScroll: true),
                Observation("home-1", "Settings", Item("About phone"), hasScroll: true),
                Observation("home-1", "Settings", Item("About phone"), hasScroll: true),
                Observation("about", "About phone"),
            ],
            ["home-0", "home-1"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal(["scroll", "click"], harness.Actions.Calls);
        Assert.Equal(1, outcome.Scrolls);
        Assert.Equal(2, outcome.Steps);
    }

    [Fact]
    public async Task TargetAbsent_ReportsIncompleteWithoutOverstatement()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", isEnd: true),
                Observation("home", "Settings", isEnd: true),
            ],
            []);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("incomplete", outcome.Status);
        Assert.Equal("target_absent_at_verified_end", outcome.CompletionReason);
        Assert.Empty(harness.Actions.Calls);
        Assert.False(await ResultSucceededAsync(harness.Assets));
    }

    [Fact]
    public async Task StalePlan_IsRejectedBeforeSafetyOrDevice()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
            ],
            ["changed"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.Equal("stale_plan", outcome.CompletionReason);
        Assert.Empty(harness.Actions.Calls);
        Assert.Single(outcome.IssueFingerprints);
    }

    [Fact]
    public async Task DangerousTarget_IsDeniedWithZeroDeviceCalls()
    {
        var dangerous = DefaultSnapshot with
        {
            Scenario = DefaultSnapshot.Scenario with
            {
                Target = new ScenarioTarget("Reset options", []),
            },
        };
        var harness = await Harness.CreateAsync(
            _root,
            dangerous,
            [
                Observation("home", "Settings", Item("Reset options"), hasScroll: true),
                Observation("home", "Settings", Item("Reset options"), hasScroll: true),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("blocked", outcome.Status);
        Assert.Equal("deny.dangerous.text", outcome.CompletionReason);
        Assert.Empty(harness.Actions.Calls);
        Assert.Equal(1, outcome.SafetyDenied);
    }

    [Fact]
    public async Task VerificationMismatch_IsOperationalFailure()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("settings-other", "Settings"),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.StartsWith("verification_mismatch", outcome.CompletionReason);
        Assert.Equal(["click"], harness.Actions.Calls);
    }

    [Fact]
    public async Task VerificationAcceptsLargeVisualTransitionWhenHierarchyIsStale()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true,
                    screenshot: new byte[100]),
                Observation("home", "Settings", Item("About phone"), hasScroll: true,
                    screenshot: new byte[100]),
                Observation("stale-home", "Settings", screenshot: new byte[10]),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("target_page_visual_transition_verified", outcome.CompletionReason);
        Assert.Equal(["click"], harness.Actions.Calls);
    }

    [Theory]
    [InlineData("device_offline")]
    [InlineData("provider_timeout")]
    public async Task ObservationFailures_AreClassifiedAndRetainResult(string kind)
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings"),
                new ScenarioObservationException(kind, "injected failure"),
            ],
            []);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.Equal(kind, outcome.CompletionReason);
        Assert.Single(outcome.IssueFingerprints);
        Assert.True(File.Exists(Path.Combine(harness.Assets.RunDirectory, "result.json")));
    }

    [Fact]
    public async Task Cancellation_FinalizesCancelledResult()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings"),
                new OperationCanceledException(),
            ],
            []);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("cancelled", outcome.Status);
        Assert.Equal("cancelled", outcome.CompletionReason);
        Assert.False(await ResultSucceededAsync(harness.Assets));
    }

    private static async Task<bool> ResultSucceededAsync(RunAssetSession assets)
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(assets.RunDirectory, "result.json"));
        return json.Contains(
            "\"successCriteriaSatisfied\": true",
            StringComparison.Ordinal);
    }

    private static MenuItem Item(string name) =>
        new(
            name,
            new Coordinate(0.5, 0.7),
            MenuItemType.MenuItem,
            ExpectedAction: ExpectedAction.Navigate,
            ExpectsPageChange: true);

    private static ScenarioObservation Observation(
        string fingerprint,
        string page,
        MenuItem? item = null,
        bool hasScroll = false,
        bool isEnd = false,
        byte[]? screenshot = null) =>
        new(
            screenshot ?? [1, 2, 3],
            $"<hierarchy fingerprint=\"{fingerprint}\" />",
            new PageAnalysis(
                Direction.Left,
                Direction.Left,
                CurrentPath: [page],
                Items: item is null ? [] : [item],
                HasScroll: hasScroll,
                IsEndOfList: isEnd),
            page,
            "com.android.settings",
            fingerprint,
            isEnd ? "verified_end_of_list" : hasScroll ? "scrollable" : "no_scroll",
            DateTimeOffset.UtcNow);

    private static RunManifestInput Manifest(string runId) =>
        new(
            runId,
            null,
            null,
            "revision",
            "fake-device",
            "AOSP API 35",
            "mock",
            "deterministic-settings-v1",
            "mode-a");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class Harness
    {
        private Harness(
            IncrementalScenarioRunner runner,
            FakeActionExecutor actions,
            RunAssetSession assets)
        {
            Runner = runner;
            Actions = actions;
            Assets = assets;
        }

        public IncrementalScenarioRunner Runner { get; }

        public FakeActionExecutor Actions { get; }

        public RunAssetSession Assets { get; }

        public static async Task<Harness> CreateAsync(
            string root,
            ScenarioSnapshot snapshot,
            IEnumerable<object> observations,
            IEnumerable<string> fingerprints)
        {
            var plan = new ScenarioPlanCompiler().Compile(snapshot);
            var runId = $"run-{Guid.NewGuid():N}";
            var assets = await new RunAssetStore().CreateAsync(
                root,
                snapshot,
                plan,
                Manifest(runId));
            var traceStorage = new InMemoryTraceStorage();
            var trace = new InMemoryTraceRecorder(traceStorage);
            var evaluator = new SettingsSafetyEvaluator(snapshot);
            var context = new SafetyExecutionContext();
            var journal = new SafetyDecisionJournal();
            var sink = new CompositeSafetyDecisionSink(
                new RunAssetSafetyDecisionSink(assets),
                new TraceSafetyDecisionSink(trace),
                journal);
            var actions = new FakeActionExecutor();
            var safeActions = new SafeActionExecutor(
                actions,
                evaluator,
                sink,
                context);
            var safeEntry = new SafeEntryActionDriver(
                new FakeEntryDriver(),
                evaluator,
                sink,
                context);
            var services = new HostRunServices(
                new FakeAdbRunner(),
                new UnusedPageAnalyzer(),
                safeActions,
                new FakeScreenState(),
                safeEntry,
                context,
                evaluator,
                sink,
                journal,
                trace,
                assets);
            var source = new FakeObservationSource(
                observations,
                fingerprints);
            return new Harness(
                new IncrementalScenarioRunner(
                    snapshot,
                    plan,
                    services,
                    source),
                actions,
                assets);
        }
    }

    private sealed class FakeObservationSource : IScenarioObservationSource
    {
        private readonly Queue<object> _observations;
        private readonly Queue<string> _fingerprints;

        public FakeObservationSource(
            IEnumerable<object> observations,
            IEnumerable<string> fingerprints)
        {
            _observations = new Queue<object>(observations);
            _fingerprints = new Queue<string>(fingerprints);
        }

        public Task<ScenarioObservation> ObserveAsync(
            string? previousHierarchyXml = null,
            bool afterScroll = false,
            CancellationToken cancellationToken = default)
        {
            var next = _observations.Dequeue();
            return next switch
            {
                ScenarioObservation observation => Task.FromResult(observation),
                Exception exception => Task.FromException<ScenarioObservation>(exception),
                _ => throw new InvalidOperationException(),
            };
        }

        public Task<string> GetCurrentFingerprintAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_fingerprints.Dequeue());
    }

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
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public List<ActionRecord> GetHistory() => [];

        private Task<bool> Called(string action)
        {
            Calls.Add(action);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeEntryDriver : IEntryActionDriver
    {
        public Task<bool> OpenDeepLinkAsync(
            string target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> ColdLaunchAsync(
            string targetApp,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task WaitAsync(
            int milliseconds,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> CheckConditionAsync(
            IReadOnlyDictionary<string, object>? waitCondition,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeAdbRunner : IAdbCommandRunner
    {
        public string Serial => "fake-device";

        public Task<AdbCommandResult> RunAsync(
            AdbCommandRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("ADB must not be used by fake runner.");
    }

    private sealed class FakeScreenState : IScreenStateProvider
    {
        public bool HasScroll() => false;

        public double GetScrollProgress() => 0;

        public bool IsEndOfList() => false;

        public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
    }

    private sealed class UnusedPageAnalyzer : IPageAnalyzer
    {
        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(
            CancellationToken ct = default) =>
            throw new InvalidOperationException();

        public Task<AppEntryPoint?> FindAppEntryAsync(
            string targetApp,
            CancellationToken ct = default) =>
            throw new InvalidOperationException();

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default) =>
            throw new InvalidOperationException();
    }
}
