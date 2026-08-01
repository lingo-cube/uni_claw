using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Hooks;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using UniClaw.Host.Verification;
using UniClaw.Host.Tests.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Commands;

/// <summary>
/// The Host-assembled <see cref="TraversalEngine"/> path. Mirrors the
/// <c>RunScenarioAsync</c> composition (entry → hooks → engine → analyzer) with
/// device fakes, proving the seam produces a <see cref="TraversalResult"/> plus
/// per-step run artifacts (E1.5) and that plan mode walks static nodes with
/// <see cref="VerifyHook"/> expected-change verification (E6.3/E6.5).
/// </summary>
public sealed class EnginePathTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-engine-{Guid.NewGuid():N}");

    private static readonly ScenarioSnapshot DefaultSnapshot =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"));

    [Fact]
    public async Task EnginePath_ProducesTraversalResultAndStepArtifacts()
    {
        var appPackage = DefaultSnapshot.Scenario.AppPackage;
        var run = await RunEngineAsync(
            StaticPlan(appPackage, expectedChange: null),
            appPackage);

        Assert.True(run.Result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, run.Result.CompletionReason);
        Assert.True(run.Result.TotalSteps >= 1);

        var stepDirectory = Path.Combine(run.Assets.RunDirectory, "steps", "0001");
        Assert.True(File.Exists(Path.Combine(stepDirectory, "before.png")));
        Assert.True(File.Exists(Path.Combine(stepDirectory, "before.xml")));
        Assert.True(File.Exists(Path.Combine(stepDirectory, "after.png")));
        Assert.True(File.Exists(Path.Combine(stepDirectory, "after.xml")));

        var outcome = new VerificationAnalyzer(
            run.Trace, run.Journal, run.RunId).Analyze(run.Result);
        Assert.Equal("success", outcome.Status);
        // The static-plan click executed exactly once (leaf Execute step).
        Assert.Equal(1, outcome.SafetyAllowed);
        Assert.Equal(0, outcome.SafetyDenied);
        Assert.Contains(
            run.Trace.GetTransitions(),
            transition => string.Equals(
                transition.FsmType,
                "TraversalFSM",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAssetHook_RefreshLastAfter_ReplacesImmediateCapture()
    {
        var runId = $"run-{Guid.NewGuid():N}";
        var assets = await new RunAssetStore().CreateAsync(
            _root,
            DefaultSnapshot,
            StaticPlan(DefaultSnapshot.Scenario.AppPackage, expectedChange: null),
            RunnerTestHarness.Manifest(runId));
        var capture = new SequencedScreenCapture();
        var hook = new RunAssetHook(assets, capture, new EngineScreenState());
        var context = new TraversalRuntimeContext(runId);
        context.IncrementStepCount();

        await hook.OnBeforeStepAsync(context);
        await hook.OnAfterStepAsync(context);
        await hook.RefreshLastAfterAsync();

        var afterPath = Path.Combine(
            assets.RunDirectory,
            "steps",
            "0001",
            "after.png");
        Assert.Equal(new byte[] { 3 }, await File.ReadAllBytesAsync(afterPath));
    }

    [Fact]
    public async Task PlanMode_ExpectedChangeMet_RecordsVerifyPassAndSucceeds()
    {
        var appPackage = DefaultSnapshot.Scenario.AppPackage;
        var run = await RunEngineAsync(
            StaticPlan(appPackage, expectedChange: "Settings"),
            appPackage);

        var outcome = new VerificationAnalyzer(
            run.Trace, run.Journal, run.RunId).Analyze(run.Result);

        Assert.True(run.Result.Success);
        Assert.Equal("success", outcome.Status);
        var pass = run.Trace.GetExecutions().Single(
            e => e.Action == "verify.pass");
        Assert.Equal("Settings", pass.PageId);
    }

    [Fact]
    public async Task PlanMode_ExpectedChangeNotMet_RecordsVerifyFailClassifiedByAnalyzer()
    {
        var appPackage = DefaultSnapshot.Scenario.AppPackage;
        var run = await RunEngineAsync(
            StaticPlan(appPackage, expectedChange: "change"),
            appPackage);

        var outcome = new VerificationAnalyzer(
            run.Trace, run.Journal, run.RunId).Analyze(run.Result);

        // The engine does not fail; the hook records, the analyzer classifies post-run.
        Assert.True(run.Result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, run.Result.CompletionReason);
        var fail = run.Trace.GetExecutions().Single(e => e.Action == "verify.fail");
        Assert.Equal("fail", fail.Status);

        Assert.Equal("failure", outcome.Status);
        Assert.Equal("verification_mismatch", outcome.FailureCause);
        Assert.NotNull(outcome.FailingStep);
        Assert.Contains("Settings", outcome.IssueFingerprints);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// Build a static single-step plan (root Container → click leaf). The leaf
    /// carries <c>Meta["expected_change"]</c> when <paramref name="expectedChange"/>
    /// is set; <see cref="VerifyHook"/> then verifies the leaf's step.
    /// </summary>
    private static TraversalPlan StaticPlan(string appPackage, string? expectedChange)
    {
        var leaf = new TraversalNode(
            "step-about",
            "About phone",
            NodeType.LeafAction,
            new Operation(
                OperationType.Click,
                new Target(TargetType.Coordinate, new Coordinate(0.5, 0.7))),
            new ChildrenStrategy(ChildrenStrategyType.None),
            Meta: expectedChange is null
                ? null
                : new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["expected_change"] = expectedChange,
                });
        var root = new TraversalNode(
            "root",
            "settings-root",
            NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(
                ChildrenStrategyType.Static,
                StaticChildren: new List<string> { leaf.NodeId }));
        return new TraversalPlan(
            EntryApp: appPackage,
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen, TimeoutSeconds: 10),
            PlanId: $"plan-mode-{Guid.NewGuid():N}",
            RootNode: root,
            StaticNodes: new Dictionary<string, TraversalNode>
            {
                [leaf.NodeId] = leaf,
            },
            Mode: TraversalMode.Concrete);
    }

    /// <summary>
    /// Same wiring as <c>HostCompositionFactory.CreateRunServices</c> +
    /// <c>RunScenarioAsync</c>'s hook composition, with device fakes.
    /// </summary>
    private async Task<EngineRun> RunEngineAsync(
        TraversalPlan plan,
        string appPackage)
    {
        var runId = $"run-{Guid.NewGuid():N}";
        var assets = await new RunAssetStore().CreateAsync(
            _root,
            DefaultSnapshot,
            plan,
            RunnerTestHarness.Manifest(runId));

        var traceStorage = new InMemoryTraceStorage();
        var traceRecorder = new InMemoryTraceRecorder(traceStorage);
        var traceService = new InMemoryTraceService(traceStorage);
        // Lenient evaluator isolates the seam (entry → hooks → engine → analyzer)
        // and plan-mode verification from safety-rule semantics, which are covered
        // by the dedicated safety suite and real integration scopes.
        var evaluator = new AllowAllSafetyEvaluator();
        var safetyContext = new SafetyExecutionContext();
        var journal = new SafetyDecisionJournal();
        var sink = new CompositeSafetyDecisionSink(
            new RunAssetSafetyDecisionSink(assets),
            new TraceSafetyDecisionSink(traceRecorder),
            journal);
        var actions = new FakeActionExecutor();
        var safeActions = new SafeActionExecutor(actions, evaluator, sink, safetyContext);
        var safeEntry = new SafeEntryActionDriver(
            new FakeEntryDriver(),
            evaluator,
            sink,
            safetyContext);
        var analyzer = new EnginePageAnalyzer();
        var services = new HostRunServices(
            new FakeAdbRunner(),
            analyzer,
            safeActions,
            new EngineScreenState(),
            safeEntry,
            new EntryPolicyExecutor(safeEntry),
            new EngineBrain(analyzer),
            safetyContext,
            evaluator,
            sink,
            journal,
            traceRecorder,
            assets,
            traceService);

        await traceRecorder.StartSessionAsync(
            runId,
            new Dictionary<string, object>(StringComparer.Ordinal),
            CancellationToken.None);

        var hooks = ImmutableArray.Create<ITraversalHook>(
            new SafetyContextHook(
                safetyContext,
                runId,
                appPackage,
                "Settings",
                DefaultSnapshot.Scenario.Boundaries.MaxSteps,
                DefaultSnapshot.Scenario.Boundaries.MaxScrolls),
            new RunAssetHook(assets, new FakeScreenCapture(), services.ScreenState),
            new BoundaryHook(
                () => Task.FromResult(appPackage),
                appPackage,
                DefaultSnapshot.Scenario.Boundaries.AllowedPages,
                traceRecorder,
                runId),
            new VerifyHook(traceRecorder, runId));

        var engine = services.CreateTraversalEngine(
            plan,
            services.Brain,
            new TraversalEngineConfig
            {
                Hooks = hooks,
                MaxSteps = DefaultSnapshot.Scenario.Boundaries.MaxSteps,
                // Static plan is root Container → leaf child (depth 2).
                MaxDepth = 2,
                DelayPerStepMs = 0,
            });

        var result = await engine.RunAsync();
        return new EngineRun(runId, result, traceService, journal, assets);
    }

    private sealed record EngineRun(
        string RunId,
        TraversalResult Result,
        InMemoryTraceService Trace,
        SafetyDecisionJournal Journal,
        RunAssetSession Assets);

    private sealed class AllowAllSafetyEvaluator : ISafetyEvaluator
    {
        public SafetyDecision Evaluate(SafetyCandidate candidate) =>
            new(
                SchemaVersion: "1",
                PolicyId: "test",
                PolicyVersion: "1",
                PolicyHash: "test",
                Disposition: "allow",
                RuleId: "allow.test",
                Reason: "Test evaluator allows everything.",
                Action: candidate.Action,
                NormalizedTarget: candidate.Target,
                Semantic: candidate.Semantic,
                PageIdentity: candidate.PageIdentity,
                PagePath: candidate.PagePath,
                Confidence: candidate.Confidence,
                RunId: candidate.RunId,
                StepNumber: candidate.StepNumber,
                PageFingerprint: candidate.PageFingerprint,
                Source: candidate.Source,
                Timestamp: DateTimeOffset.UtcNow);
    }

    private sealed class EnginePageAnalyzer : IPageAnalyzer
    {
        private readonly PageAnalysis _analysis = new(
            Direction.Left,
            Direction.Left,
            CurrentPath: ["Settings"]);

        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default) =>
            Task.FromResult<PageAnalysis?>(_analysis);

        public Task<AppEntryPoint?> FindAppEntryAsync(
            string targetApp,
            CancellationToken ct = default) =>
            Task.FromResult<AppEntryPoint?>(null);

        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default) =>
            Task.FromResult(new PageTypeVerification(IsMatch: true, Confidence: 1.0));
    }

    private sealed class EngineScreenState : IObservableScreenStateProvider
    {
        public bool HasScroll() => false;

        public double GetScrollProgress() => 0;

        public bool IsEndOfList() => false;

        public ScrollSwipeConfig? GetScrollSwipeConfig() => null;

        public Task<ScreenStateResult> RefreshAsync(
            string? previousHierarchyXml = null,
            bool afterScroll = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScreenStateResult(
                Succeeded: true,
                Status: "ok",
                HierarchyXml: "<hierarchy fingerprint=\"settings\" />",
                HierarchyFingerprint: "fp-settings",
                HasScroll: false,
                IsEndOfList: false,
                Failure: null));
    }

    private sealed class EngineBrain : IUniBrain
    {
        private readonly IPageAnalyzer _analyzer;

        public EngineBrain(IPageAnalyzer analyzer) => _analyzer = analyzer;

        public IPageAnalyzer PageAnalyzer => _analyzer;

        public ITraversalAdvisor Advisor =>
            throw new InvalidOperationException("Advisor must not be used by engine path test.");

        public ITextUnderstanding Text =>
            throw new InvalidOperationException("Text must not be used by engine path test.");
    }

    private sealed class FakeScreenCapture : IScreenCapture
    {
        public Task<byte[]> CaptureAsync(CancellationToken ct = default) =>
            Task.FromResult(new byte[] { 1, 2, 3 });
    }

    private sealed class SequencedScreenCapture : IScreenCapture
    {
        private byte _next = 1;

        public Task<byte[]> CaptureAsync(CancellationToken ct = default) =>
            Task.FromResult(new[] { _next++ });
    }
}
