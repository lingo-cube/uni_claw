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
        Func<PlanLanguageHandler, PlanHandlerInput, Finding?>? inspect = null)
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
            ConsultAgent = context => decide(context),
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "Current page" },
            ProjectTraversal = _ => projection(kernel.CurrentBelief!),
            CanBindPlanLanguage = canBind,
            InspectPlanLanguage = inspect,
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

    private static AgentDecision Plan(AgentDecisionContext context, TraversalOperation operation) =>
        new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(new PlanItem[]
        {
            new PlanItem.TraverseItem("current", new[] { new TraversalRule("r1", "always", operation) }, 0,
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
        var operation = Assert.Single(driver.TraversalExecution.Operations!);
        Assert.True(operation.Completed);
        Assert.Equal(container, operation.ContainerId);
        Assert.Equal("ready", operation.ObservedState);
        Assert.Equal(kernel.CurrentBelief!.RevisionId, operation.SourceRevisionId);
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
    public void NoResponsePause_QueryAndLaterContinuePreserveReadOnlyWork()
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
                return driverDecisionContinue(context);
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

        var resumed = driver.Drive();
        Assert.True(resumed.Status == RunDriveStatus.TerminalNotProven,
            $"{resumed.Status}:{resumed.Reason}; position={System.Text.Json.JsonSerializer.Serialize(driver.PlanExecution)}");
        Assert.Equal(3, consults);
        Assert.Equal(1, inspectCalls);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal("completed", driver.TraversalExecution!.EndReason);
        Assert.Single(driver.TraversalExecution.Operations!);
        Assert.True(driver.TraversalExecution.Operations![0].Completed);

        AgentDecision driverDecisionContinue(AgentDecisionContext context) =>
            new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution!.Position);
    }
}
