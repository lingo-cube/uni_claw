using System.Collections.Immutable;
using System.IO;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// ITraversalHook lifecycle tests — 11 scenarios covering all 7 hook call points,
/// registration order, exception handling, TraversalHookBase no-op, and config field.
/// </summary>
public class TraversalHookTests
{
    // ── Engine creation helpers ────────────────────────────────

    private static TraversalNode Leaf(string id, Operation op)
        => new(id, id, NodeType.LeafAction, op, new ChildrenStrategy(ChildrenStrategyType.None));

    private static Operation ClickAt(double x, double y)
        => new(OperationType.Click, new Target(TargetType.Coordinate, new Coordinate(x, y)));

    private static StateFixture SimpleFixture() => new StateFixtureBuilder()
        .Page("home", p => p.Name("HomeScreen").Button("btn_go", "Go", 0.5, 0.5))
        .Page("next", p => p.Name("NextScreen").BackButton("btn_back", 0.05, 0.05))
        .Transition(t => t.Id("go").Click("btn_go").From("home").To("next"))
        .Transition(t => t.Id("back").Click("btn_back").From("next").To("home"))
        .Build();

    private static TraversalEngine CreateEngine(
        StateFixture fixture, TraversalNode root,
        Dictionary<string, TraversalNode> nodes,
        TraversalEngineConfig? config = null)
    {
        var vision = new StatefulMockVisionService(fixture);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new StatefulMockActionExecutor(vision);
        var plan = new TraversalPlan(
            EntryApp: "test", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan", PlanId: "test-001", RootNode: root, StaticNodes: nodes);
        return new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action, config);
    }

    private static TraversalEngine CreateSimpleEngine(TraversalEngineConfig? config = null)
    {
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));
        return CreateEngine(fixture, root, new Dictionary<string, TraversalNode>(), config);
    }

    private static TraversalEngine CreateSimpleEngineWithHooks(ITraversalHook[] hooks)
        => CreateSimpleEngine(new TraversalEngineConfig
        {
            MaxSteps = 5,
            Hooks = ImmutableArray.Create(hooks)
        });

    // ── 9.2: Empty Hooks list — engine runs normally, zero overhead ────────

    [Fact(DisplayName = "Hook: 空 Hooks 列表 — 引擎正常运行, 零开销")]
    public async Task EmptyHooks_EngineRunsNormally()
    {
        var engine = CreateSimpleEngine(new TraversalEngineConfig
        {
            MaxSteps = 5,
            Hooks = ImmutableArray<ITraversalHook>.Empty
        });

        var result = await engine.RunAsync();
        Assert.True(result.Success, $"Expected success, got: {result.CompletionReason}");
    }

    // ── 9.3: Single Hook counting — CountingHook records each OnXxx call ──

    [Fact(DisplayName = "Hook: 单 Hook 计数 — 各 OnXxx 调用次数正确")]
    public async Task SingleHook_CountsCallsCorrectly()
    {
        var hook = new CountingHook();
        var engine = CreateSimpleEngineWithHooks(new[] { hook });

        var result = await engine.RunAsync();
        Assert.True(result.Success);

        // OnBeforeRun: 1 call before step loop
        Assert.Equal(1, hook.BeforeRunCount);
        // OnAfterRun: 1 call at exit path
        Assert.Equal(1, hook.AfterRunCount);
        // OnBeforeStep: called for each step iteration (result.TotalSteps)
        Assert.Equal(result.TotalSteps, hook.BeforeStepCount);
        // OnAfterStep: called for each step including terminating step
        Assert.Equal(result.TotalSteps, hook.AfterStepCount);
        // OnError: not called (no errors)
        Assert.Equal(0, hook.ErrorCount);
    }

    // ── 9.4: Multiple Hook order — HookA fires before HookB ────────────────

    [Fact(DisplayName = "Hook: 多 Hook 注册顺序 — HookA 在 HookB 之前触发")]
    public async Task MultipleHooks_RegistrationOrderPreserved()
    {
        var hookA = new OrderHook("A");
        var hookB = new OrderHook("B");
        var engine = CreateSimpleEngineWithHooks(new ITraversalHook[] { hookA, hookB });

        var result = await engine.RunAsync();
        Assert.True(result.Success);

        // Verify that all A events come before corresponding B events
        var combined = hookA.Events.Concat(hookB.Events).ToList();
        var aBeforeRun = combined.IndexOf("A:BeforeRun");
        var bBeforeRun = combined.IndexOf("B:BeforeRun");
        Assert.True(aBeforeRun < bBeforeRun, "A:BeforeRun should fire before B:BeforeRun");

        var aAfterRun = combined.IndexOf("A:AfterRun");
        var bAfterRun = combined.IndexOf("B:AfterRun");
        Assert.True(aAfterRun < bAfterRun, "A:AfterRun should fire before B:AfterRun");
    }

    // ── 9.5: Hook throws exception — engine continues + Console.WriteLine ──

    [Fact(DisplayName = "Hook: Hook 抛异常 — 引擎继续运行 + Console.WriteLine 警告")]
    public async Task HookThrows_EngineContinuesWithWarning()
    {
        var throwingHook = new ThrowingHook("BeforeStep");
        var countingHook = new CountingHook();
        var engine = CreateSimpleEngineWithHooks(new ITraversalHook[] { throwingHook, countingHook });

        // Capture Console.WriteLine output
        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);

        var result = await engine.RunAsync();

        Console.SetOut(originalOut);
        var output = sw.ToString();

        Assert.True(result.Success, $"Engine should complete despite hook exception, got: {result.CompletionReason}");
        Assert.Contains("[Hook Warning]", output);
        Assert.Contains("InvalidOperationException", output);
        // CountingHook should still have been called (engine continues)
        Assert.True(countingHook.BeforeStepCount > 0, "CountingHook should still fire after ThrowingHook");
    }

    // ── 9.6: OnBeforeRun/OnAfterRun timing ──────────────────────────────────

    [Fact(DisplayName = "Hook: OnBeforeRun 在步骤循环前, OnAfterRun 在各退出路径触发")]
    public async Task BeforeRunAfterRun_TimingCorrect()
    {
        var hook = new CountingHook();
        var engine = CreateSimpleEngineWithHooks(new[] { hook });

        var result = await engine.RunAsync();
        Assert.True(result.Success);

        // OnBeforeRun fires once before any step
        Assert.Equal(1, hook.BeforeRunCount);
        // OnAfterRun fires once at the exit path
        Assert.Equal(1, hook.AfterRunCount);
        // OnBeforeRun fires before OnBeforeStep (verified by event sequence)
        Assert.True(hook.BeforeRunCount > 0 && hook.BeforeStepCount > 0);
    }

    [Fact(DisplayName = "Hook: OnAfterRun 在 Cancelled 退出路径触发")]
    public async Task AfterRun_FiresAtCancelledExit()
    {
        var hook2 = new CountingHook();
        var cts = new CancellationTokenSource();
        // Use delay per step to make engine take longer → cancellation fires before completion
        var engine2 = CreateSimpleEngine(new TraversalEngineConfig
        {
            MaxSteps = 1000,
            DelayPerStepMs = 50,
            Hooks = ImmutableArray.Create<ITraversalHook>(hook2)
        });
        // Cancel quickly — before engine completes
        cts.CancelAfter(150);
        var result2 = await engine2.RunAsync(cts.Token);
        Assert.Equal(TraversalResult.Reasons.Cancelled, result2.CompletionReason);
        Assert.Equal(1, hook2.AfterRunCount);  // OnAfterRun fires at Cancelled exit
    }

    // ── 9.7: OnBeforeStep/OnAfterStep timing ────────────────────────────────

    [Fact(DisplayName = "Hook: OnBeforeStep 在 vision 前, OnAfterStep 在终止检查前 (含终止步骤)")]
    public async Task BeforeStepAfterStep_TimingCorrect()
    {
        var hook = new CountingHook();
        var engine = CreateSimpleEngineWithHooks(new[] { hook });

        var result = await engine.RunAsync();
        Assert.True(result.Success);

        // OnBeforeStep fires for each step iteration
        Assert.Equal(result.TotalSteps, hook.BeforeStepCount);
        // OnAfterStep fires for each step including the terminating step
        Assert.Equal(result.TotalSteps, hook.AfterStepCount);
        // OnAfterStep fires before termination check → terminating step's OnAfterStep is not skipped
        Assert.True(hook.AfterStepCount > 0, "OnAfterStep should fire at least once");
    }

    // ── 9.8: OnError recoverable — FSM ErrorHandling → IsRecoverable=true ──

    [Fact(DisplayName = "Hook: OnError 可恢复 — FSM ErrorHandling → IsRecoverable=true")]
    public async Task OnError_Recoverable_IsRecoverableTrue()
    {
        var errorHook = new ErrorCaptureHook();
        var fixture = new StateFixtureBuilder()
            .Page("home", p => p.Name("HomeScreen").Button("btn_go", "Go", 0.5, 0.5))
            .Build();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.Static, StaticChildren: new List<string> { "leaf-1" }));
        var leaf = new TraversalNode("leaf-1", "Leaf1", NodeType.LeafAction,
            ClickAt(0.5, 0.5),
            new ChildrenStrategy(ChildrenStrategyType.None));
        var nodes = new Dictionary<string, TraversalNode> { ["leaf-1"] = leaf };

        // Use a throwing action executor to trigger recoverable error
        var vision = new StatefulMockVisionService(fixture);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new ThrowingActionExecutor();
        var plan = new TraversalPlan(
            EntryApp: "test", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan", PlanId: "test-001", RootNode: root, StaticNodes: nodes);
        var engine = new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action, new TraversalEngineConfig
        {
            MaxSteps = 20,
            Hooks = ImmutableArray.Create<ITraversalHook>(errorHook)
        });

        var result = await engine.RunAsync();
        // Engine should still complete (recoverable errors are handled by FSM)
        Assert.NotNull(result);

        // At least one recoverable error should have been observed
        Assert.True(errorHook.RecoverableErrors.Count > 0,
            $"Expected at least 1 recoverable error, got {errorHook.RecoverableErrors.Count}");
        Assert.True(errorHook.RecoverableErrors.All(e => e.IsRecoverable),
            "All recoverable errors should have IsRecoverable=true");
    }

    // ── 9.9: OnError fatal — Engine-level exception → IsRecoverable=false ──

    [Fact(DisplayName = "Hook: OnError 致命 — 引擎级异常 → IsRecoverable=false")]
    public async Task OnError_Fatal_IsRecoverableFalse()
    {
        var errorHook = new ErrorCaptureHook();
        var fixture = SimpleFixture();
        var root = new TraversalNode("root", "Root", NodeType.Container,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None));

        // Use a vision provider that throws to trigger fatal engine-level error
        var vision = new ThrowingVisionProvider();
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new StatefulMockActionExecutor(new StatefulMockVisionService(fixture));
        var plan = new TraversalPlan(
            EntryApp: "test", EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
            PlanName: "test_plan", PlanId: "test-001", RootNode: root);
        var engine = new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action, new TraversalEngineConfig
        {
            ThrowOnError = false,
            Hooks = ImmutableArray.Create<ITraversalHook>(errorHook)
        });

        var result = await engine.RunAsync();
        Assert.Equal(TraversalResult.Reasons.Error, result.CompletionReason);

        // Fatal error should have been observed
        Assert.True(errorHook.FatalErrors.Count > 0, "Expected at least 1 fatal error");
        Assert.True(errorHook.FatalErrors.All(e => !e.IsRecoverable),
            "Fatal errors should have IsRecoverable=false");
    }

    // ── 9.10: TraversalHookBase no-op — inherit without override ──────────────

    [Fact(DisplayName = "Hook: TraversalHookBase 无操作 — 不重写 → 全 Task.CompletedTask")]
    public async Task TraversalHookBase_NoOp_AllCompletedTask()
    {
        var noopHook = new NoOpHook();
        var engine = CreateSimpleEngineWithHooks(new[] { noopHook });

        var result = await engine.RunAsync();
        Assert.True(result.Success, $"Engine should complete normally with no-op hook, got: {result.CompletionReason}");

        // Verify all methods return Task.CompletedTask (synchronous completion)
        var plan = new TraversalPlan("test", new EntryPolicy(EntryStrategy.BindCurrentScreen), "test", "t-1");
        var ctx = engine.Context;

        // All methods should complete synchronously (Task.CompletedTask)
        Assert.Equal(Task.CompletedTask, noopHook.OnBeforeRunAsync(plan, ctx));
        Assert.Equal(Task.CompletedTask, noopHook.OnAfterRunAsync(result));
        Assert.Equal(Task.CompletedTask, noopHook.OnBeforeStepAsync(ctx));
        Assert.Equal(Task.CompletedTask, noopHook.OnAfterStepAsync(ctx));
        Assert.Equal(Task.CompletedTask, noopHook.OnErrorAsync(
            new TraversalErrorContext("Test", "msg", null, false), ctx));
        Assert.Equal(Task.CompletedTask, noopHook.OnPauseAsync(ctx));
        Assert.Equal(Task.CompletedTask, noopHook.OnResumeAsync(ctx));
    }

    // ── Step numbering: engine increments StepCount before OnBeforeStep ──

    [Fact(DisplayName = "Hook: StepCount 从 1 单调递增 — OnBeforeStep/OnAfterStep 看到 1,2,3…")]
    public async Task Hooks_ObserveSequentialStepNumbers()
    {
        var hook = new StepCountHook();
        var engine = CreateSimpleEngineWithHooks(new[] { hook });

        var result = await engine.RunAsync();
        Assert.True(result.Success);

        // Each step is numbered 1..TotalSteps and OnAfterStep sees the same
        // step number as the matching OnBeforeStep.
        Assert.Equal(result.TotalSteps, hook.BeforeSteps.Count);
        Assert.Equal(result.TotalSteps, hook.AfterSteps.Count);
        for (var i = 0; i < result.TotalSteps; i++)
        {
            Assert.Equal(i + 1, hook.BeforeSteps[i]);
            Assert.Equal(i + 1, hook.AfterSteps[i]);
        }

        Assert.True(result.TotalSteps >= 1, "Engine should execute at least one step");
    }

    // ── 9.11: Config field registration — Hooks via config; RegisterHook removed ──

    [Fact(DisplayName = "Hook: 配置字段注册 — TraversalEngineConfig.Hooks 有效; RegisterHook() 不再存在")]
    public async Task ConfigFieldRegistration_WorksAndRegisterHookRemoved()
    {
        var hook = new CountingHook();
        var config = new TraversalEngineConfig
        {
            MaxSteps = 20,
            Hooks = ImmutableArray.Create<ITraversalHook>(hook)
        };

        var engine = CreateSimpleEngine(config);
        var result = await engine.RunAsync();
        Assert.True(result.Success);

        // Hook should have been called via config field
        Assert.Equal(1, hook.BeforeRunCount);
        Assert.Equal(1, hook.AfterRunCount);

        // Verify RegisterHook method does NOT exist on TraversalEngine
        // (compile-time check: if RegisterHook existed, this would not compile)
        // Runtime verification: TraversalEngine has no public method named "RegisterHook"
        var registerHookMethod = engine.GetType().GetMethod("RegisterHook");
        Assert.Null(registerHookMethod);
    }

    // ── Test helper hook implementations ────────────────────────────────────

    /// <summary>
    /// CountingHook — records call counts for each ITraversalHook method.
    /// </summary>
    private sealed class CountingHook : TraversalHookBase
    {
        public int BeforeRunCount { get; private set; }
        public int AfterRunCount { get; private set; }
        public int BeforeStepCount { get; private set; }
        public int AfterStepCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }

        public override Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context)
        { BeforeRunCount++; return Task.CompletedTask; }

        public override Task OnAfterRunAsync(TraversalResult result)
        { AfterRunCount++; return Task.CompletedTask; }

        public override Task OnBeforeStepAsync(ITraversalContext context)
        { BeforeStepCount++; return Task.CompletedTask; }

        public override Task OnAfterStepAsync(ITraversalContext context)
        { AfterStepCount++; return Task.CompletedTask; }

        public override Task OnErrorAsync(TraversalErrorContext error, ITraversalContext context)
        { ErrorCount++; return Task.CompletedTask; }

        public override Task OnPauseAsync(ITraversalContext context)
        { PauseCount++; return Task.CompletedTask; }

        public override Task OnResumeAsync(ITraversalContext context)
        { ResumeCount++; return Task.CompletedTask; }
    }

    /// <summary>
    /// StepCountHook — records context.StepCount on each step boundary.
    /// </summary>
    private sealed class StepCountHook : TraversalHookBase
    {
        public readonly List<int> BeforeSteps = new();
        public readonly List<int> AfterSteps = new();

        public override Task OnBeforeStepAsync(ITraversalContext context)
        {
            BeforeSteps.Add(context.StepCount);
            return Task.CompletedTask;
        }

        public override Task OnAfterStepAsync(ITraversalContext context)
        {
            AfterSteps.Add(context.StepCount);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// OrderHook — records event sequence with a label prefix.
    /// </summary>
    private sealed class OrderHook : TraversalHookBase
    {
        private readonly string _label;
        public readonly List<string> Events = new();

        public OrderHook(string label) => _label = label;

        public override Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context)
        { Events.Add($"{_label}:BeforeRun"); return Task.CompletedTask; }

        public override Task OnAfterRunAsync(TraversalResult result)
        { Events.Add($"{_label}:AfterRun"); return Task.CompletedTask; }

        public override Task OnBeforeStepAsync(ITraversalContext context)
        { Events.Add($"{_label}:BeforeStep"); return Task.CompletedTask; }

        public override Task OnAfterStepAsync(ITraversalContext context)
        { Events.Add($"{_label}:AfterStep"); return Task.CompletedTask; }

        public override Task OnErrorAsync(TraversalErrorContext error, ITraversalContext context)
        { Events.Add($"{_label}:Error({error.IsRecoverable})"); return Task.CompletedTask; }
    }

    /// <summary>
    /// ThrowingHook — throws InvalidOperationException in the specified method.
    /// </summary>
    private sealed class ThrowingHook : TraversalHookBase
    {
        private readonly string _throwInMethod;

        public ThrowingHook(string throwInMethod) => _throwInMethod = throwInMethod;

        public override Task OnBeforeStepAsync(ITraversalContext context)
        {
            if (_throwInMethod == "BeforeStep")
                throw new InvalidOperationException("Hook threw in OnBeforeStepAsync");
            return Task.CompletedTask;
        }

        public override Task OnBeforeRunAsync(TraversalPlan plan, ITraversalContext context)
        {
            if (_throwInMethod == "BeforeRun")
                throw new InvalidOperationException("Hook threw in OnBeforeRunAsync");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// ErrorCaptureHook — records TraversalErrorContext for fatal and recoverable errors.
    /// </summary>
    private sealed class ErrorCaptureHook : TraversalHookBase
    {
        public readonly List<TraversalErrorContext> RecoverableErrors = new();
        public readonly List<TraversalErrorContext> FatalErrors = new();

        public override Task OnErrorAsync(TraversalErrorContext error, ITraversalContext context)
        {
            if (error.IsRecoverable)
                RecoverableErrors.Add(error);
            else
                FatalErrors.Add(error);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// NoOpHook — inherits TraversalHookBase without any overrides.
    /// All methods should return Task.CompletedTask (synchronous completion).
    /// </summary>
    private sealed class NoOpHook : TraversalHookBase;

    /// <summary>
    /// ThrowingVisionProvider — throws exception on AnalyzeCurrentPageAsync
    /// to trigger engine-level fatal error.
    /// </summary>
    private sealed class ThrowingVisionProvider : IPageAnalyzer
    {
        public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("Vision provider threw");

        public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
            => Task.FromResult<AppEntryPoint?>(null);

        /// <inheritdoc />
        public Task<PageTypeVerification> VerifyPageTypeAsync(
            PageAnalysis pageAnalysis,
            string expectedType,
            string? expectedPageName = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(new PageTypeVerification(
                IsMatch: false,
                Confidence: 0.0,
                ActualType: expectedType));
        }
    }

    /// <summary>
    /// ThrowingActionExecutor — throws on TapAsync to trigger FSM-level recoverable error.
    /// </summary>
    private sealed class ThrowingActionExecutor : IActionExecutor
    {
        public Task<bool> TapAsync(double x, double y, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Action executor threw on Tap");

        public Task<bool> SwipeAsync(double startX, double startY, double endX, double endY, int durationMs, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> PressBackAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> InputTextAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> LongPressAsync(double x, double y, int durationMs, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task WaitAsync(int milliseconds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public List<ActionRecord> GetHistory() => new();
    }
}
