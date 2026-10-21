using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class TraversalExecutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 1, 0, 0, TimeSpan.Zero);
    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "test");
    }
    private sealed class Driver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => new(DispatchOutcome.DeliveryCompleted, "unused", Now);
    }
    private sealed class Strategy(string owner) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            var roleParts = record.Claim.Value.Split(':', 2);
            var valueParts = roleParts[1].Split('@', 2);
            return new[] { new ProposedOccurrence(owner, roleParts[0], valueParts[0], valueParts[1]) };
        }
    }
    private static ExecutionContract Contract() => new("plan-v1", "bounded traversal",
        new HashSet<string> { UIWorldDoubles.Observed }, new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "objective" }, new[] { new RunObligation("objective", RunObligationKind.Objective, "done", "yes", true) },
        MaxConsultations: 4, MaxTotalSteps: 4, TaskInstanceId: "traversal", ProductSessionId: "session",
        ContractRef: "contract");

    private static string ProbeContainerId()
    {
        var (admission, _) = new EvidenceLedger().Admit(UIWorldDoubles.Observation("switch:entry@ready", Now));
        return "ctr-" + admission.EvidenceId![3..15];
    }
    private static string CurrentContainer(WorldBeliefRevision revision) =>
        revision.WorldState[WorldModel.CurrentContainerSubject].Value;

    private static (UniKernel Kernel, KernelRunDriver Driver, string Container) Compose(
        Func<WorldBeliefRevision, TraversalObservation> projection,
        Func<AgentDecisionContext, AgentDecision> decide,
        Action<DispatchRequest>? delivered = null,
        Func<PlanLanguageHandler, bool>? canBind = null,
        Func<PlanLanguageHandler, PlanHandlerInput, Finding?>? inspect = null,
        Func<PlanLanguageHandler, PlanHandlerInput, CancellationToken, ValueTask<Finding?>>? inspectAsync = null,
        TimeSpan? handlerTimeout = null,
        Func<RunDriverInput.Cancel?>? pollCancellation = null,
        Func<AgentActionStep, AgentDecisionContext, PlanStepGuardResult>? guard = null)
    {
        var cid = ProbeContainerId();
        var plan = new AgentPlanPolicy();
        var kernel = new UniKernel(new EvidenceLedger(),
            new WorldModel(new HashSet<string>(Contract().Scope), new SeedContainerAssociationStrategy(),
                new Strategy(cid), new RoleContinuityStrategy()),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(delivered is null ? new Driver() : new TestDriver(delivered)));
        var inputs = new RunDriverInputs
        {
            NextInput = directive => directive.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:entry@ready", Now) })
                : null,
            PollCancellation = pollCancellation,
            ConsultAgent = context => decide(context),
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "Current page" },
            ProjectTraversal = _ =>
            {
                var projected = projection(kernel.CurrentBelief!);
                return projected with { Graph = projected.Graph with { Scope = kernel.NavigationScope } };
            },
            CanBindPlanLanguage = canBind,
            InspectPlanLanguage = inspect,
            InspectPlanLanguageAsync = inspectAsync,
            PlanHandlerTimeout = handlerTimeout ?? TimeSpan.FromSeconds(2),
            GuardStep = guard,
        };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver, cid);
    }

    private sealed class TestDriver(Action<DispatchRequest> delivered) : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request)
        {
            delivered(request);
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "test", Now);
        }
    }

    private static TraversalObservation CurrentObservation(WorldBeliefRevision revision, string container, bool complete = true)
    {
        var occurrence = Assert.Single(revision.Occurrences!);
        var graph = new WorldGraphProjection(revision.RevisionId, "current", container,
            revision.Containers!, revision.Relations!, Array.Empty<string>());
        var candidate = new PlanCandidate("entry", occurrence.Role, occurrence.SemanticDescriptor!,
            PlanCandidateDisposition.Ready, occurrence.EvidenceBasis);
        return new TraversalObservation(graph,
            new[] { new TraversalCandidate(candidate, new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied }) },
            complete, revision.EvidenceBasis.ToArray());
    }

    private static AgentDecision Plan(AgentDecisionContext context, TraversalOperation operation, int maxDepth = 0) =>
        new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(new PlanItem[]
        {
            new PlanItem.TraverseItem("current", new[] { new TraversalRule("r1", "always", operation) }, maxDepth,
                new AgentActionStep("switch", "entry", "tap", null)),
        }, "traverse current page", "traversal-plan", MaxLocalOperations: 16));

    [Fact]
    public void ReadOnlyTraversal_RecordsCurrentWorldAndCompletesWithoutEffect()
    {
        var consults = 0;
        var (kernel, driver, container) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision)), context =>
            ++consults == 1
                ? Plan(context, new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
                    Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>()))
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("completed", driver.TraversalExecution!.EndReason);
        Assert.Equal(0, driver.TraversalExecution.KnownRemainingWork);
        var operation = Assert.Single(driver.TraversalExecution.Operations!);
        Assert.True(operation.Completed);
        Assert.Equal(container, operation.ContainerId);
        Assert.Equal("ready", operation.ObservedState);
        Assert.Equal(kernel.CurrentBelief!.RevisionId, operation.SourceRevisionId);
    }

    [Fact]
    public async Task DelayedAsyncHandler_IsRecordedRunningThenResumesInPlace()
    {
        var firstCompletion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var inputs = new Dictionary<string, PlanHandlerInput>();
        var firstHandler = new PlanLanguageHandler("handler-1", "runtime.language-inspector", "1.0", "English");
        var secondHandler = new PlanLanguageHandler("handler-2", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { firstHandler, secondHandler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (binding, input, _) =>
            {
                calls.Add(binding.HandlerId);
                inputs[binding.HandlerId] = input;
                if (binding.HandlerId == secondHandler.HandlerId)
                {
                    secondStarted.TrySetResult();
                    return new ValueTask<Finding?>(secondCompletion.Task);
                }
                return new ValueTask<Finding?>(firstCompletion.Task);
            });

        var pending = driver.Drive();

        Assert.True(pending.Status == RunDriveStatus.WaitingForHandler,
            $"{pending.Status}:{pending.Reason}; {System.Text.Json.JsonSerializer.Serialize(driver.TraversalExecution)}");
        var running = Assert.Single(driver.TraversalExecution!.Handlers);
        Assert.Equal(firstHandler.HandlerId, running.HandlerId);
        Assert.Equal(PlanHandlerCallStatus.Running, running.Status);
        Assert.NotNull(running.CallId);
        Assert.Equal(new[] { firstHandler.HandlerId }, calls);
        Assert.Empty(kernel.EffectReceipts);

        var continuation = driver.DriveAsync();
        Assert.False(continuation.IsCompleted);
        firstCompletion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: inputs[firstHandler.HandlerId].SourceRevisionId,
            operationId: inputs[firstHandler.HandlerId].OperationId),
            firstHandler.ProtocolVersion, firstHandler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "matched"));
        await secondStarted.Task;
        Assert.Equal(new[] { firstHandler.HandlerId, secondHandler.HandlerId }, calls);
        Assert.Collection(driver.TraversalExecution!.Handlers,
            first => Assert.Equal(PlanHandlerCallStatus.Completed, first.Status),
            second => Assert.Equal(PlanHandlerCallStatus.Running, second.Status));
        secondCompletion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: inputs[secondHandler.HandlerId].SourceRevisionId,
            operationId: inputs[secondHandler.HandlerId].OperationId),
            secondHandler.ProtocolVersion, secondHandler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "matched"));

        var resumed = await continuation;

        Assert.Equal(RunDriveStatus.TerminalNotProven, resumed.Status);
        Assert.Equal(new[] { firstHandler.HandlerId, secondHandler.HandlerId }, calls);
        Assert.Collection(driver.TraversalExecution!.Handlers,
            first =>
            {
                Assert.Equal(running.CallId, first.CallId);
                Assert.Equal(PlanHandlerCallStatus.Completed, first.Status);
            },
            second => Assert.Equal(PlanHandlerCallStatus.Completed, second.Status));
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public async Task Drive_ConsumesCompletedPendingHandlerWithoutResubmitting()
    {
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (_, _, _) =>
            {
                calls++;
                return new ValueTask<Finding?>(completion.Task);
            });

        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var running = Assert.Single(driver.TraversalExecution!.Handlers);
        completion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: running.SourceRevisionId, operationId: running.OperationId),
            handler.ProtocolVersion, handler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "matched"));

        var resumed = driver.Drive();
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (resumed.Status == RunDriveStatus.WaitingForHandler && wait.Elapsed < TimeSpan.FromSeconds(1))
        {
            await Task.Delay(1);
            resumed = driver.Drive();
        }

        Assert.NotEqual(RunDriveStatus.WaitingForHandler, resumed.Status);
        Assert.Equal(RunDriveStatus.TerminalNotProven, resumed.Status);
        Assert.Equal(1, calls);
        Assert.Equal(PlanHandlerCallStatus.Completed, Assert.Single(driver.TraversalExecution!.Handlers).Status);
    }

    [Fact]
    public async Task Timeout_ClosesCallAndDoesNotResubmitOnDrive()
    {
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (_, _, _) =>
            {
                calls++;
                return new ValueTask<Finding?>(completion.Task);
            },
            handlerTimeout: TimeSpan.FromMilliseconds(25));

        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var timedOut = await driver.DriveAsync();

        Assert.Equal(RunDriveStatus.TerminalNotProven, timedOut.Status);
        Assert.Equal(1, calls);
        Assert.Equal(PlanHandlerCallStatus.TimedOut, Assert.Single(driver.TraversalExecution!.Handlers).Status);
        completion.SetException(new InvalidOperationException("late-fault"));
        Assert.Equal(PlanHandlerCallStatus.TimedOut, Assert.Single(driver.TraversalExecution.Handlers).Status);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public async Task CompletionAfterDeadline_IsTimedOutEvenWhenDriveConsumesAfterward()
    {
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (_, _, _) => new ValueTask<Finding?>(completion.Task),
            handlerTimeout: TimeSpan.FromMilliseconds(25));

        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var running = Assert.Single(driver.TraversalExecution!.Handlers);
        await Task.Delay(60);
        completion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: running.SourceRevisionId, operationId: running.OperationId),
            handler.ProtocolVersion, handler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "late"));

        driver.Drive();

        Assert.Equal(PlanHandlerCallStatus.TimedOut, Assert.Single(driver.TraversalExecution!.Handlers).Status);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public async Task CompletionBeforeDeadline_RemainsCompletedWhenDriveConsumesLater()
    {
        var completion = new TaskCompletionSource<Finding?>();
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (_, input, _) => new ValueTask<Finding?>(completion.Task),
            handlerTimeout: TimeSpan.FromMilliseconds(80));

        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var running = Assert.Single(driver.TraversalExecution!.Handlers);
        completion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: running.SourceRevisionId, operationId: running.OperationId),
            handler.ProtocolVersion, handler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "on-time"));
        await completion.Task;
        await Task.Delay(120);

        driver.Drive();

        Assert.Equal(PlanHandlerCallStatus.Completed, Assert.Single(driver.TraversalExecution!.Handlers).Status);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public async Task DriveAsyncCancellation_UsesRunCancelPathAndIgnoresLateCompletion()
    {
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (_, _, _) => new ValueTask<Finding?>(completion.Task));

        Assert.Equal(RunDriveStatus.WaitingForHandler, driver.Drive().Status);
        var running = Assert.Single(driver.TraversalExecution!.Handlers);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await driver.DriveAsync(cancellation.Token);

        Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, result.Status);
        Assert.Equal(PlanHandlerCallStatus.Cancelled, Assert.Single(driver.TraversalExecution!.Handlers).Status);
        completion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: running.SourceRevisionId, operationId: running.OperationId),
            handler.ProtocolVersion, handler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "late"));
        Assert.Equal(PlanHandlerCallStatus.Cancelled, Assert.Single(driver.TraversalExecution.Handlers).Status);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void RequirePassFailure_PausesAndRechecksWithANewCallId()
    {
        var consults = 0;
        var calls = 0;
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English",
            Required: true, RequirePass: true);
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context =>
            {
                consults++;
                if (consults == 1) return Plan(context, operation);
                if (consults == 2)
                    return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution!.Position);
                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done"));
            },
            canBind: _ => true,
            inspectAsync: (_, input, _) =>
            {
                calls++;
                var passed = calls > 1;
                return ValueTask.FromResult<Finding?>(new Finding("english",
                    new CapabilityCorrelation(input.RunId, domainCorrelation: input.SourceRevisionId,
                        operationId: input.OperationId), handler.ProtocolVersion, handler.CapabilityId,
                    passed ? FindingDisposition.Pass : FindingDisposition.Unknown,
                    passed ? CapabilityStatus.Completed : CapabilityStatus.Unknown,
                    passed ? "matched" : "not-proven"));
            });

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(3, consults);
        Assert.Equal(2, calls);
        var records = driver.TraversalExecution!.Handlers;
        Assert.Equal(2, records.Count);
        Assert.NotEqual(records[0].CallId, records[1].CallId);
        Assert.All(records, record => Assert.Equal(PlanHandlerCallStatus.Completed, record.Status));
        Assert.Equal(FindingDisposition.Pass, records[^1].Result!.Disposition);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public async Task ConcurrentDriveEntries_AreRejectedWhileAsyncHandlerIsPending()
    {
        var completion = new TaskCompletionSource<Finding?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerStarted = new TaskCompletionSource<PlanHandlerInput>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, operation)
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")),
            canBind: _ => true,
            inspectAsync: (_, input, _) =>
            {
                Interlocked.Increment(ref calls);
                handlerStarted.TrySetResult(input);
                return new ValueTask<Finding?>(completion.Task);
            });

        var firstDrive = driver.DriveAsync();
        var input = await handlerStarted.Task;
        var before = Assert.IsType<TraversalExecutionView>(driver.TraversalExecution);
        Assert.Equal(PlanHandlerCallStatus.Running, Assert.Single(before.Handlers).Status);

        var competingDrive = driver.Drive();
        var competingDriveAsync = driver.DriveAsync();
        var whilePending = Assert.IsType<TraversalExecutionView>(driver.TraversalExecution);
        Assert.Equal(before.WorkUsed, whilePending.WorkUsed);
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.Equal(PlanHandlerCallStatus.Running, Assert.Single(whilePending.Handlers).Status);
        Assert.Empty(kernel.EffectReceipts);

        completion.SetResult(new Finding("english", new CapabilityCorrelation(kernel.RunId!,
            domainCorrelation: input.SourceRevisionId, operationId: input.OperationId),
            handler.ProtocolVersion, handler.CapabilityId, FindingDisposition.Pass,
            CapabilityStatus.Completed, "matched"));
        var firstResult = await firstDrive;
        var competingAsyncResult = await competingDriveAsync;

        Assert.Equal(RunDriveStatus.UnexpectedInput, competingDrive.Status);
        Assert.Equal("drive-in-progress", competingDrive.Reason);
        Assert.Equal(RunDriveStatus.UnexpectedInput, competingAsyncResult.Status);
        Assert.Equal("drive-in-progress", competingAsyncResult.Reason);
        Assert.Equal(RunDriveStatus.TerminalNotProven, firstResult.Status);
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.Equal(PlanHandlerCallStatus.Completed, Assert.Single(driver.TraversalExecution!.Handlers).Status);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void OptionalRequirePassUnavailable_NavigateRemainsPausedWithoutEffect(bool canBind, bool hasAdapter)
    {
        var handler = new PlanLanguageHandler("optional", "runtime.language-inspector", "1.0", "English",
            Required: false, RequirePass: true);
        var consults = 0;
        var operation = new TraversalOperation("navigate", TraversalOperationKind.Navigate,
            new AgentActionStep("switch", "entry", "tap", null),
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => ++consults == 1 ? Plan(context, operation, maxDepth: 1) : null!,
            canBind: _ => canBind,
            inspect: hasAdapter ? (_, _) => throw new InvalidOperationException("must-not-invoke") : null);

        driver.Drive();

        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("traversal:handler-require-pass", driver.TraversalExecution!.PauseReason);
        Assert.Equal(PlanHandlerCallStatus.Unavailable,
            Assert.Single(driver.TraversalExecution.Handlers).Status);
    }

    [Fact]
    public void OptionalNonBlockingRuntimeBindingFailure_DoesNotPauseTraversal()
    {
        var handler = new PlanLanguageHandler("optional", "runtime.language-inspector", "1.0", "English",
            Required: false, RequirePass: false);
        var consults = 0;
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => ++consults == 1 ? Plan(context, operation) : null!,
            canBind: _ => throw new InvalidOperationException("binding-check-failed"),
            inspect: (_, _) => throw new InvalidOperationException("must-not-invoke"));

        driver.Drive();

        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("completed", driver.TraversalExecution!.EndReason);
        Assert.Null(driver.TraversalExecution.PauseReason);
        Assert.Equal(PlanHandlerCallStatus.Failed, Assert.Single(driver.TraversalExecution.Handlers).Status);
    }

    [Fact]
    public void IncompleteDiscovery_ProcessesVisibleReadOnlyThenReportsPartial()
    {
        var (kernel, driver, _) = Compose(revision => CurrentObservation(revision, CurrentContainer(revision), complete: false), context =>
            context.DecisionId.EndsWith("-1", StringComparison.Ordinal)
                ? Plan(context, new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
                    Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>()))
                : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done")));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("partial", driver.TraversalExecution!.EndReason);
        Assert.Contains(driver.TraversalExecution.Gaps, gap => gap.Reason == "discovery-incomplete");
        Assert.Single(driver.TraversalExecution.Operations!);
    }

    [Fact]
    public async Task NoResponsePause_QueryAndLaterContinuePreserveReadOnlyWork()
    {
        var bindCalls = 0;
        var consults = 0;
        var inspectCalls = 0;
        var handler = new PlanLanguageHandler("handler", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
            new[] { handler }, Array.Empty<PlanLanguageHandler>());
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context =>
            {
                consults++;
                if (consults == 1) return Plan(context, operation);
                if (consults == 2) return null!;
                if (consults == 3) return driverDecisionContinue(context);
                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "done"));
            },
            canBind: _ => ++bindCalls != 2,
            inspect: (binding, input) =>
            {
                inspectCalls++;
                return new Finding("unknown", new CapabilityCorrelation(input.RunId,
                    domainCorrelation: input.SourceRevisionId, operationId: input.OperationId),
                    binding.ProtocolVersion, binding.CapabilityId, FindingDisposition.Unknown,
                    CapabilityStatus.Unknown, "no matching language input");
            });

        var paused = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, paused.Status);
        Assert.Equal("traversal:awaiting-decision", paused.Reason);
        var before = driver.TraversalExecution!;
        Assert.Equal(PlanExecutionStatus.Paused, before.Status);
        Assert.Null(before.EndReason);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(0, inspectCalls);

        var queried = driver.TraversalExecution;
        Assert.Equal(before, queried);
        Assert.Equal(2, consults);

        var resumed = await driver.DriveAsync();
        Assert.True(resumed.Status == RunDriveStatus.TerminalNotProven,
            $"{resumed.Status}:{resumed.Reason}; position={System.Text.Json.JsonSerializer.Serialize(driver.PlanExecution)}");
        Assert.Equal(4, consults);
        Assert.Equal(1, inspectCalls);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("completed", driver.TraversalExecution!.EndReason);
        Assert.Single(driver.TraversalExecution.Operations!);
        Assert.True(driver.TraversalExecution.Operations![0].Completed);

        AgentDecision driverDecisionContinue(AgentDecisionContext context) =>
            new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution!.Position);
    }

    [Fact]
    public void CancellationPoll_AbandonsActiveTraversalAndLatchesWithoutSafeStop()
    {
        var polls = 0;
        var consults = 0;
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context =>
            {
                consults++;
                return Plan(context, new TraversalOperation("inspect", TraversalOperationKind.ReadOnly, null,
                    Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>()));
            },
            pollCancellation: () => ++polls == 4
                ? new RunDriverInput.Cancel("test-cancel", Now)
                : null);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, result.Status);
        Assert.Equal("test-cancel", result.Reason);
        Assert.Equal(4, polls);
        Assert.Equal(1, consults);
        Assert.False(kernel.IsRunTerminal);
        Assert.Equal(PlanExecutionStatus.Abandoned, driver.TraversalExecution!.Status);
        Assert.Equal("traversal:cancelled:test-cancel", driver.TraversalExecution.EndReason);
        Assert.All(driver.TraversalExecution.Operations!, operation => Assert.False(operation.Completed));
        Assert.Empty(kernel.EffectReceipts);

        var repeated = driver.Drive();
        Assert.Equal(result, repeated);
        Assert.Equal(4, polls);
        Assert.Equal(1, consults);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void VerifiedAncestorNavigation_RecordsGapAndPausesAtActualPage()
    {
        var control = new TraversalControl();
        var returnOperation = new TraversalOperation("return", TraversalOperationKind.Navigate,
            new AgentActionStep("back", "root", "tap", null),
            Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>());
        var operation = new TraversalOperation("open", TraversalOperationKind.Navigate,
            new AgentActionStep("open", "page", "tap", null),
            Array.Empty<PlanLanguageHandler>(), Array.Empty<PlanLanguageHandler>());
        control.Start("cycle-test", 1, "root");
        control.ConfigureRules(new[] { new TraversalRule("always", "always", operation) });

        var childWork = Assert.IsType<TraversalControl.Work>(control.Select(Observation("root", "child"), 4));
        control.Navigated(childWork, "child", returnOperation);
        var ancestorWork = Assert.IsType<TraversalControl.Work>(control.Select(Observation("child", "root"), 4));
        control.Navigated(ancestorWork, "root", returnOperation);

        Assert.True(control.Paused);
        Assert.Equal("root", control.View().CurrentContainerId);
        Assert.Equal(0, control.Depth);
        Assert.Null(control.Select(Observation("root", "child"), 4));
        Assert.Contains(control.DrainProgressEvents(), progress =>
            progress is TraversalProgressEvent.Gap { Value.Reason: "navigation-cycle" });

        static TraversalObservation Observation(string current, string target)
        {
            var candidate = new PlanCandidate($"to-{target}", "entry", target,
                PlanCandidateDisposition.Ready, new[] { "evidence" });
            return new TraversalObservation(
                new WorldGraphProjection("revision", "scope", current,
                    Array.Empty<ContainerBelief>(), Array.Empty<ContainerRelation>(), Array.Empty<string>()),
                new[] { new TraversalCandidate(candidate,
                    new Dictionary<string, PolicyTruth> { ["always"] = PolicyTruth.Satisfied }) },
                true, new[] { "evidence" });
        }
    }

    [Fact]
    public void AlreadySatisfiedNavigate_DoesNotRunAfterHandlerOrClaimExpansion()
    {
        var inspectCalls = 0;
        var consults = 0;
        var handler = new PlanLanguageHandler("after", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("navigate", TraversalOperationKind.Navigate,
            new AgentActionStep("entry", "destination", "tap", null),
            Array.Empty<PlanLanguageHandler>(), new[] { handler });
        var (kernel, driver, container) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => ++consults == 1
                ? Plan(context, operation, maxDepth: 2)
                : null!,
            canBind: _ => true,
            inspect: (binding, input) =>
            {
                inspectCalls++;
                return new Finding("unknown", new CapabilityCorrelation(input.RunId,
                    domainCorrelation: input.SourceRevisionId, operationId: input.OperationId),
                    binding.ProtocolVersion, binding.CapabilityId, FindingDisposition.Unknown,
                    CapabilityStatus.Unknown, "no transition observed");
            },
            guard: (_, _) => new PlanStepGuardResult(PlanStepGuardVerdict.AlreadySatisfied,
                "guard-satisfied", new[] { "evidence" }));

        var result = driver.Drive();

        Assert.True(result.Status == RunDriveStatus.WaitingForInput,
            $"{result.Status}:{result.Reason}; consults={consults}; inspect={inspectCalls}; plan={System.Text.Json.JsonSerializer.Serialize(driver.PlanExecution)}; traversal={System.Text.Json.JsonSerializer.Serialize(driver.TraversalExecution)}");
        Assert.Equal("traversal:awaiting-decision", result.Reason);
        Assert.Equal(0, inspectCalls);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(PlanExecutionStatus.Paused, driver.TraversalExecution!.Status);
        Assert.Equal("traversal:navigation-already-satisfied-requires-revision",
            driver.TraversalExecution.PauseReason);
        Assert.True(driver.TraversalExecution.KnownRemainingWork >= 1);
        Assert.Contains(driver.TraversalExecution.Gaps, gap =>
            gap.Reason == "navigation-already-satisfied-without-page-transition");
        var operationRecord = Assert.Single(driver.TraversalExecution.Operations!);
        Assert.Equal(container, operationRecord.ContainerId);
        Assert.False(operationRecord.Completed);
        Assert.Empty(driver.TraversalExecution.Handlers);
    }

    [Fact]
    public void CancellationDuringMissingPostActionEvidence_PreservesReceiptAndAbandonsUnverifiedWork()
    {
        UniKernel? kernelRef = null;
        var inspectCalls = 0;
        var after = new PlanLanguageHandler("after", "runtime.language-inspector", "1.0", "English");
        var operation = new TraversalOperation("navigate", TraversalOperationKind.Navigate,
            new AgentActionStep("switch", "entry", "tap", null),
            Array.Empty<PlanLanguageHandler>(), new[] { after });
        var (kernel, driver, _) = Compose(
            revision => CurrentObservation(revision, CurrentContainer(revision)),
            context => Plan(context, operation, maxDepth: 2),
            canBind: _ => true,
            inspect: (_, _) =>
            {
                inspectCalls++;
                return null;
            },
            pollCancellation: () => kernelRef?.EffectReceipts.Count == 1
                ? new RunDriverInput.Cancel("cancel-in-flight", Now)
                : null);
        kernelRef = kernel;

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.CancelledWithoutSafeStopPath, result.Status);
        Assert.Equal(1, kernel.EffectReceipts.Count);
        Assert.False(kernel.IsRunTerminal);
        Assert.Equal(0, inspectCalls);
        Assert.Equal(PlanExecutionStatus.Abandoned, driver.TraversalExecution!.Status);
        Assert.Equal("traversal:cancelled:cancel-in-flight", driver.TraversalExecution.EndReason);
        Assert.Contains(driver.TraversalExecution.Gaps, gap =>
            gap.Reason == "verification-interrupted-by-cancel");
        Assert.Single(driver.TraversalExecution.Operations!);
        Assert.False(driver.TraversalExecution.Operations![0].Completed);
        Assert.DoesNotContain(driver.PlanExecution!.Attempts,
            attempt => attempt.Status == PlanAttemptStatus.Verified);

        var repeated = driver.Drive();
        Assert.Equal(result, repeated);
        Assert.Single(kernel.EffectReceipts);
    }
}
