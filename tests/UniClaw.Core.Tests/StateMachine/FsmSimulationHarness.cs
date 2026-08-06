using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// Reusable harness for FSM flow simulation tests.  Provides factory methods
/// for <see cref="StepContext"/> with controllable fakes, plus helpers to
/// drive the FSM into specific states without boilerplate.
/// </summary>
internal static class FsmSimulationHarness
{
    /// <summary>Drives the FSM from Idle to a target state in one call.</summary>
    public static TraversalFSM DriveTo(
        TraversalRuntimeContext ctx,
        TraversalState target)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var node = new TestTraversalNode("root", "root", NodeType.Container);
        ctx.SetCurrentFrame(node);
        ctx.NodeStack.Push(node);
        var fsm = new TraversalFSM(ctx);

        return target switch
        {
            TraversalState.PreconditionCheck => DriveToPreconditionCheck(fsm),
            TraversalState.Execute => DriveToExecute(fsm),
            TraversalState.ResultVerify => DriveToResultVerify(fsm),
            TraversalState.ErrorHandling => DriveToErrorHandling(fsm),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Not implemented"),
        };
    }

    private static TraversalFSM DriveToPreconditionCheck(TraversalFSM fsm)
    {
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        return fsm;
    }

    private static TraversalFSM DriveToExecute(TraversalFSM fsm)
    {
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);
        return fsm;
    }

    private static TraversalFSM DriveToResultVerify(TraversalFSM fsm)
    {
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);
        fsm.TransitionTo(TraversalState.ResultVerify);
        return fsm;
    }

    private static TraversalFSM DriveToErrorHandling(TraversalFSM fsm)
    {
        fsm.TransitionTo(TraversalState.PreconditionCheck);
        fsm.TransitionTo(TraversalState.Execute);
        fsm.TransitionTo(TraversalState.ErrorHandling);
        return fsm;
    }

    /// <summary>
    /// Re-enters ErrorHandling from whatever state a previous step left the FSM
    /// in.  Routes through matrix-legal edges only (19-edge D-1 matrix):
    /// NodeSelect has no direct ErrorHandling edge → NodeSelect →
    /// PreconditionCheck → ErrorHandling; FrameComplete routes via NodeSelect
    /// first; Branch / Execute / ResultVerify / PopupHandling transition
    /// directly (Branch → ErrorHandling is a direct matrix edge).
    /// </summary>
    public static void ReenterErrorHandling(TraversalFSM fsm)
    {
        if (fsm.CurrentState == TraversalState.ErrorHandling)
            return;
        if (fsm.CurrentState == TraversalState.FrameComplete)
            fsm.TransitionTo(TraversalState.NodeSelect);
        if (fsm.CurrentState == TraversalState.NodeSelect)
            fsm.TransitionTo(TraversalState.PreconditionCheck);
        if (fsm.CurrentState != TraversalState.ErrorHandling)
            fsm.TransitionTo(TraversalState.ErrorHandling);
    }

    /// <summary>Creates a StepContext with controllable fakes and active TraceCoordinator.</summary>
    public static (StepContext stepCtx, InMemoryTraceStorage storage) CreateStepContext(
        TraversalRuntimeContext ctx,
        TraversalFSM fsm,
        IActionExecutor? action = null,
        IPageAnalyzer? pageAnalyzer = null,
        ErrorHandler? errorHandler = null,
        IPreconditionChecker? preconditionChecker = null)
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var trace = new TraceCoordinator(recorder, ctx.TraceId, ctx);
        var brain = pageAnalyzer is not null
            ? (IUniBrain)new FakeBrain(pageAnalyzer)
            : new UniBrainService(
                new MockVisionProvider(), new MockTraversalAdvisor(), new MockTextUnderstanding());

        var stepCtx = new StepContext(
            Context: ctx,
            StateMachine: fsm,
            Brain: brain,
            ScreenState: new DefaultScreenStateProvider(),
            Action: action!,
            ChildMgr: null!,
            NodeRegistry: null!,
            Trace: trace,
            SnapshotMgr: new PageSnapshotManager(),
            Stack: null!,
            ErrorHandler: errorHandler,
            PreconditionChecker: preconditionChecker);
        return (stepCtx, storage);
    }

    /// <summary>Returns an ErrorHandler that always produces the given strategy / outcome.</summary>
    public static ErrorHandler StrategyForcingHandler(
        ErrorStrategy strategy,
        RecoveryOutcome outcome = RecoveryOutcome.Success) =>
        new(
            classify: _ => ErrorType.Unknown,
            selectStrategy: (_, _) => strategy,
            execute: (_, _) => new ErrorRecoveryResult(strategy, outcome, 0));

    /// <summary>Returns a fake action executor whose methods return the given value.</summary>
    public static IActionExecutor FakeAction(bool returns) =>
        new FakeActionExecutor(returns);

    /// <summary>Returns a PageAnalysis with the given popup flag.</summary>
    public static PageAnalysis PopupPage(string name = "Allow access") =>
        new(Direction.Left, Direction.Left, Items: [new MenuItem(name, new Coordinate(0.5, 0.5))], IsPopup: true);

    /// <summary>Returns a PageAnalysis with the given items.</summary>
    public static PageAnalysis Page(params string[] itemNames) =>
        new(Direction.Left, Direction.Left,
            Items: [.. itemNames.Select(n => new MenuItem(n, new Coordinate(0.5, 0.5)))]);

    private sealed class FakeActionExecutor : IActionExecutor
    {
        private readonly bool _returns;
        public FakeActionExecutor(bool returns) => _returns = returns;
        public Task<bool> TapAsync(double x, double y, CancellationToken ct = default) => Task.FromResult(_returns);
        public Task<bool> SwipeAsync(double sx, double sy, double ex, double ey, int durationMs, CancellationToken ct = default) => Task.FromResult(_returns);
        public Task<bool> PressBackAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> InputTextAsync(string text, CancellationToken ct = default) => Task.FromResult(_returns);
        public Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken ct = default) => Task.FromResult(_returns);
        public Task WaitAsync(int milliseconds, CancellationToken ct = default) => Task.CompletedTask;
        public List<ActionRecord> GetHistory() => [];
    }

    private sealed class FakeBrain : IUniBrain
    {
        private readonly IPageAnalyzer _analyzer;
        public FakeBrain(IPageAnalyzer analyzer) => _analyzer = analyzer;
        public IPageAnalyzer PageAnalyzer => _analyzer;
        // Null-object advisor: Confidence 0.0 → below the FSM's 0.7 gate, so
        // error handling never acts on it.
        public ITraversalAdvisor Advisor { get; } = new NullAdvisor();
        public ITextUnderstanding Text => throw new NotSupportedException();
    }

    private sealed class NullAdvisor : ITraversalAdvisor
    {
        public Task<ContainerInference> InferContainerTypeAsync(
            PageAnalysis pageAnalysis, string? currentNodeId = null, CancellationToken ct = default) =>
            Task.FromResult(new ContainerInference("", 0.0));
        public Task<ContextDecisionResult> DecideNextActionAsync(
            string goal, PageAnalysis pageAnalysis, string? currentNodeId = null,
            int? depth = null, CancellationToken ct = default) =>
            Task.FromResult(new ContextDecisionResult(default, Confidence: 0.0));
        public Task<ContextDecisionResult> HandleExceptionAsync(
            Exception exception, PageAnalysis pageAnalysis,
            string? currentNodeId = null, CancellationToken ct = default) =>
            Task.FromResult(new ContextDecisionResult(default, Confidence: 0.0));
        public Task<SafetyScreeningResult> ScreenSafetyAsync(
            PageAnalysis pageAnalysis, string instruction,
            string? pageType = null, CancellationToken ct = default) =>
            Task.FromResult(new SafetyScreeningResult([]));
    }
}
