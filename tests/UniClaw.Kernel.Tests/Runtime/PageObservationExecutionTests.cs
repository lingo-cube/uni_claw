using System.Collections.Frozen;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class PageObservationExecutionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "fixture");
    }
    private sealed class Delivery : IEffectDriver
    {
        public int Count;
        public DispatchResult Deliver(DispatchRequest request)
        { Count++; return new(DispatchOutcome.DeliveryCompleted, "fixture", T0.AddSeconds(1)); }
    }
    private static PageCaptureObservation Capture(string id, string title, string target, int second) =>
        new(id, "cycle:" + id, "session", T0.AddSeconds(second), CoordinateSpace.DeviceViewport(1000, 1000, captureId: id),
            new[] { new SemanticLayoutItem("title", "text", title, new(100, 50, 900, 100)),
                new SemanticLayoutItem("row", "ui.element", target, new(100, 200, 900, 300)) });
    private static ObservationProposal CaptureProposal(PageCaptureObservation c, ObservationContext context) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(c)), IngressKind.Observation, context,
            new("fixture", c.CaptureTime, "capture:" + c.CaptureId, ["fixture"]));
    private static ObservationProposal Identity(ObservationNeed need, ObservationContext context, string? overrideCapture = null) =>
        new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(new PageIdentityObservation(
            overrideCapture ?? need.Capture.CaptureId, need.Capture.ObservationCycleId, need.Capture.SessionCorrelation,
            need.Capture.CaptureTime, "observed:" + need.Capture.Items[0].Text, need.Capture.Items[0].Text))),
            IngressKind.Observation, context, new("fixture.identity", need.Capture.CaptureTime,
                "identity:" + need.Capture.CaptureId, ["fixture"]))
        {
            DerivedFromEvidenceIds = new[] { need.CaptureEvidenceId },
            TransitiveEvidenceBasis = new[] { need.CaptureEvidenceId }.ToFrozenSet(),
            FusionRule = "fixture", FusionVersion = "1",
        };
    private static ObservationProposal Route(string route, int second, ObservationContext context) =>
        new(new(ProductAssociationStrategy.ScreenRouteSubject, route), IngressKind.Observation, context,
            new("fixture", T0.AddSeconds(second), "route:" + second, ["fixture"]));

    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "duplicate")]
    [InlineData(false, "insufficient")]
    [InlineData(false, "timeout")]
    [InlineData(false, "cancel")]
    [InlineData(false, "late-scene")]
    public void NavigationWaitsForIdentityAndAssurance_ThenTraversalContinuesWithoutRedispatch(bool wrongFirst, string failure)
    {
        var scope = new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            ProductAssociationStrategy.ScreenRouteSubject };
        var plan = new AgentPlanPolicy();
        var delivery = new Delivery();
        var kernel = new UniKernel(new EvidenceLedger(), new WorldModel(scope, new ProductAssociationStrategy()),
            DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan), new RuntimeAssurance(new Fresh()),
            new EffectBoundary(delivery));
        var initial = false;
        var post = false;
        var released = false;
        var invalidSent = false;
        var sceneChanged = false;
        ObservationNeed? retiredNeed = null;
        var directives = new List<ObservationDirective>();
        var consulted = 0;
        var recoveries = new List<TraversalRecoveryFacts>();
        var inputs = new RunDriverInputs
        {
            NextInput = directive =>
            {
                directives.Add(directive);
                if (directive.Need is { } need)
                {
                    if (directive.Context == ObservationContext.PostActionEffectFlow && !released) return null;
                    if (need.Capture.CaptureId != "root" && failure == "insufficient") return null;
                    if (need.Capture.CaptureId == "child" && failure == "late-scene" && !sceneChanged)
                    {
                        sceneChanged = true; retiredNeed = need;
                        return new RunDriverInput.Observation([CaptureProposal(Capture("child-new", "New page", "Details", 3), directive.Context)]);
                    }
                    if (retiredNeed is not null)
                    {
                        var late = Identity(retiredNeed, directive.Context); retiredNeed = null;
                        Assert.Equal(0, kernel.PostActionVerifications.Count);
                        return new RunDriverInput.Observation([late]);
                    }
                    var invalid = wrongFirst && need.Capture.CaptureId == "child" && !invalidSent;
                    if (invalid) invalidSent = true;
                    var identity = Identity(need, directive.Context, invalid ? "wrong-capture" : null);
                    return new RunDriverInput.Observation(failure == "duplicate" ? [identity, identity] : [identity]);
                }
                if (!initial)
                {
                    initial = true;
                    return new RunDriverInput.Observation([CaptureProposal(Capture("root", "Settings", "Open", 0), directive.Context), Route("root", 0, directive.Context)]);
                }
                if (directive.Context == ObservationContext.PostActionEffectFlow && !post)
                {
                    post = true;
                    return new RunDriverInput.Observation([CaptureProposal(Capture("child", "All apps", "Details", 2), directive.Context), Route("child", 2, directive.Context)]);
                }
                return null;
            },
            ConsultAgent = context => ++consulted == 1
                ? new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(new PlanItem[]
                {
                    new PlanItem.TraverseItem("current", [new("navigate", "go", new("open", TraversalOperationKind.Navigate,
                        new("ui.element", "Open", "tap", null), [], []))], 1, new("ui.element", "Back", "tap", null)),
                }, "fixture traversal", "identity-plan", MaxLocalOperations: 8))
                : null,
            AvailablePlanSources = new Dictionary<string, string> { ["current"] = "fixture" },
            ProjectTraversal = _ =>
            {
                var b = kernel.CurrentBelief!;
                if (b.ObservationNeed is not null) return null;
                if (b.CurrentPageObservation!.Capture.CaptureId == "child") Assert.Equal(1, consulted);
                var current = b.WorldState[WorldModel.CurrentContainerSubject].Value;
                var candidates = b.CurrentPageObservation!.Capture.CaptureId == "root"
                    ? b.Occurrences!.Where(o => o.SemanticDescriptor == "Open").Select(o => new TraversalCandidate(
                        new("open", o.Role, o.SemanticDescriptor!, PlanCandidateDisposition.Ready, o.EvidenceBasis),
                        new Dictionary<string, PolicyTruth> { ["go"] = PolicyTruth.Satisfied })).ToArray() : [];
                return new(new(b.RevisionId, kernel.NavigationScope, current, b.Containers!, b.Relations!, []),
                    candidates, false, b.EvidenceBasis.ToArray());
            },
            ObserveTraversalRecovery = facts => recoveries.Add(facts),
            MaxObservationRecoveryRequests = 8,
            ObservationRecoveryTimeout = failure == "timeout" ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(5),
            PollCancellation = () => released && failure == "cancel" ? new("fixture-cancel", T0.AddSeconds(3)) : null,
        };
        Assert.True(kernel.AdmitContract(new("v1", "fixture", scope, new HashSet<string> { "tap" }, new HashSet<string>(),
            ["objective"], TaskInstanceId: "task", ProductSessionId: "session", ContractRef: "contract")).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);
        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Equal(1, delivery.Count);
        Assert.NotNull(kernel.CurrentBelief!.ObservationNeed);
        Assert.All(kernel.CurrentBelief.Occurrences!, o => Assert.Null(o.OwningContainerId));
        Assert.Empty(kernel.PostActionVerifications);
        Assert.Equal(0, driver.TraversalExecution!.Depth);
        var before = kernel.CurrentBelief;
        released = true;
        if (failure == "timeout") Thread.Sleep(70);
        var resumed = driver.Drive();
        if (failure is "insufficient" or "timeout" or "cancel")
        {
            if (failure == "insufficient")
                for (var i = 0; i < 9 && resumed.Status == RunDriveStatus.WaitingForInput; i++) resumed = driver.Drive();
            Assert.Equal(failure == "cancel" ? RunDriveStatus.CancelledWithoutSafeStopPath : RunDriveStatus.VerificationFailed, resumed.Status);
            Assert.Equal(1, delivery.Count);
            Assert.Empty(kernel.PostActionVerifications);
            Assert.NotNull(kernel.CurrentBelief!.ObservationNeed);
            if (failure is "insufficient" or "timeout")
            {
                Assert.Single(recoveries);
                Assert.False(kernel.IsRunTerminal);
            }
            if (failure == "timeout")
            {
                kernel.Process(Identity(kernel.CurrentBelief.ObservationNeed!, ObservationContext.PostActionEffectFlow));
                Assert.Equal(RunDriveStatus.VerificationFailed, driver.Drive().Status);
                Assert.Empty(kernel.PostActionVerifications); // late evidence cannot authorize expired attempt
                Assert.Equal(1, delivery.Count);
            }
            return;
        }
        Assert.Equal(1, delivery.Count);
        Assert.Null(kernel.CurrentBelief!.ObservationNeed);
        Assert.True(Assert.Single(kernel.PostActionVerifications).IsVerified);
        Assert.Equal(1, driver.TraversalExecution!.Depth);
        Assert.All(kernel.CurrentBelief.Occurrences!, o => Assert.Equal(kernel.CurrentBelief.CurrentPageObservation!.ContainerId, o.OwningContainerId));
        Assert.NotNull(before.ObservationNeed);
        Assert.All(before.Occurrences!, o => Assert.Null(o.OwningContainerId));
        Assert.All(directives.Where(d => d.Need is not null), d => Assert.True(d.RemainingBudget > TimeSpan.Zero));
        Assert.Equal(2, consulted); // continuation consulted only after verified navigation
    }
}
