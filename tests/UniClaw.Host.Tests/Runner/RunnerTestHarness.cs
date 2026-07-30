using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Runner;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;

namespace UniClaw.Host.Tests.Runner;

/// <summary>
/// Shared fakes + harness builders for scenario runner unit tests. Extracted
/// from <see cref="IncrementalScenarioRunnerTests"/> so the enumerate tests can
/// reuse the same <c>FakeActionExecutor</c> (incl. <c>PressBackAsync</c>),
/// <c>FakeEntryDriver</c>, <c>FakeAdbRunner</c>, <c>FakeScreenState</c>, and
/// <c>UnusedPageAnalyzer</c>, plus the <c>Harness</c> that wires
/// <c>HostRunServices</c> with the safety gate.
/// </summary>
internal static class RunnerTestHarness
{
    public static MenuItem Item(string name) =>
        new(
            name,
            new Coordinate(0.5, 0.7),
            MenuItemType.MenuItem,
            ExpectedAction: ExpectedAction.Navigate,
            ExpectsPageChange: true);

    public static MenuInfo Menu(string name, double x = 0.5, double y = 0.7) =>
        new(name, new Coordinate(x, y));

    /// <summary>
    /// Build a <see cref="ScenarioObservation"/> for a Settings home/child page.
    /// <paramref name="level1Menus"/> fills <see cref="PageAnalysis.Level1Menus"/>
    /// (the enumerate planner consumes this); <paramref name="item"/> fills
    /// <see cref="PageAnalysis.Items"/> (the locate planner consumes this).
    /// </summary>
    public static ScenarioObservation Observation(
        string fingerprint,
        string page,
        MenuItem? item = null,
        bool hasScroll = false,
        bool isEnd = false,
        byte[]? screenshot = null,
        ImmutableArray<MenuInfo>? level1Menus = null) =>
        new(
            screenshot ?? [1, 2, 3],
            $"<hierarchy fingerprint=\"{fingerprint}\" />",
            new PageAnalysis(
                Direction.Left,
                Direction.Left,
                Level1Menus: level1Menus ?? ImmutableArray<MenuInfo>.Empty,
                CurrentPath: [page],
                Items: item is null ? [] : [item],
                HasScroll: hasScroll,
                IsEndOfList: isEnd),
            page,
            "com.android.settings",
            fingerprint,
            isEnd ? "verified_end_of_list" : hasScroll ? "scrollable" : "no_scroll",
            DateTimeOffset.UtcNow);

    public static RunManifestInput Manifest(string runId) =>
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

    /// <summary>
    /// Wire a <see cref="HostRunServices"/> + <see cref="FakeActionExecutor"/> +
    /// <see cref="FakeObservationSource"/> for a run. The runner is constructed
    /// by <paramref name="createRunner"/> so both locate and enumerate runners
    /// share the same fake wiring.
    /// </summary>
    public static async Task<RunnerHarness> CreateAsync(
        string root,
        ScenarioSnapshot snapshot,
        IEnumerable<object> observations,
        IEnumerable<string> fingerprints,
        Func<ScenarioSnapshot, TraversalPlan, HostRunServices, IScenarioObservationSource, ScenarioRunnerBase> createRunner)
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
        var runner = createRunner(snapshot, plan, services, source);
        return new RunnerHarness(runner, actions, assets);
    }
}

internal sealed class RunnerHarness
{
    public RunnerHarness(
        ScenarioRunnerBase runner,
        FakeActionExecutor actions,
        RunAssetSession assets)
    {
        Runner = runner;
        Actions = actions;
        Assets = assets;
    }

    public ScenarioRunnerBase Runner { get; }

    public FakeActionExecutor Actions { get; }

    public RunAssetSession Assets { get; }
}

internal sealed class FakeObservationSource : IScenarioObservationSource
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

internal sealed class FakeActionExecutor : IActionExecutor
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

internal sealed class FakeEntryDriver : IEntryActionDriver
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

internal sealed class FakeAdbRunner : IAdbCommandRunner
{
    public string Serial => "fake-device";

    public Task<AdbCommandResult> RunAsync(
        AdbCommandRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("ADB must not be used by fake runner.");
}

internal sealed class FakeScreenState : IScreenStateProvider
{
    public bool HasScroll() => false;

    public double GetScrollProgress() => 0;

    public bool IsEndOfList() => false;

    public ScrollSwipeConfig? GetScrollSwipeConfig() => null;
}

internal sealed class UnusedPageAnalyzer : IPageAnalyzer
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