using System.Collections.Frozen;
using UniClaw.Kernel.Assurance;
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

/// <summary>Controlled supplier evidence over the real Kernel/World/Driver Owner chain.</summary>
public sealed class WorldRecoveryReviewTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExhaustedIdentityRecovery_ConsultsOnceWithExecutionRecord_AndDoesNotRenewOnDefer()
    {
        var capture = Capture("recovery-c1", 1);
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string>
            { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new ProductAssociationStrategy());
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new ForbiddenDriver());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new Fresh()), effects);
        var directives = new List<ObservationDirective>();
        var executions = new List<SupplementationExecution>();
        var contexts = new List<AgentDecisionContext>();
        var supplierRequests = 0;
        var captureEvidenceId = "";
        var inputs = new RunDriverInputs
        {
            MaxObservationRecoveryRequests = 2,
            NextInput = directive =>
            {
                directives.Add(directive);
                if (directive.Need is null)
                {
                    return new RunDriverInput.Observation([CaptureProposal(capture)]);
                }

                supplierRequests++;
                captureEvidenceId = directive.Need.CaptureEvidenceId;
                var execution = new SupplementationExecution(
                    "empty-execution-" + supplierRequests,
                    "controlled-supplier:no-evidence",
                    directive.Need.CaptureEvidenceId,
                    [WorldModel.CurrentContainerSubject],
                    T0.AddSeconds(10 + supplierRequests), T0.AddSeconds(10 + supplierRequests).AddMilliseconds(1),
                    "NoEvidence", Array.Empty<string>());
                executions.Add(execution);
                return new RunDriverInput.Observation(Array.Empty<ObservationProposal>(), execution);
            },
            ConsultAgent = context =>
            {
                contexts.Add(context);
                return new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, MaxRounds: 1));
            },
        };
        Assert.True(kernel.AdmitContract(new("v1", "inspect current page",
            new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new("pending", RunObligationKind.MaterialEffect, "test.pending", "done", true)],
            TaskInstanceId: "recovery-task", ProductSessionId: "recovery-session", ContractRef: "recovery-contract")).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.GroundingFailed, result.Status);
        Assert.Equal(1, contexts.Count); // same unresolved issue is not raised as a second review
        Assert.Equal(2, supplierRequests); // Defer did not replenish the original execution slot
        Assert.Equal(2, executions.Select(e => e.ExecutionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(executions, execution =>
        {
            Assert.Equal(captureEvidenceId, execution.CaptureEvidenceId);
            Assert.Equal("NoEvidence", execution.Outcome);
            Assert.Empty(execution.ResultReferences);
        });

        var context = contexts[0];
        Assert.Equal(3, context.WorldKnowledge!.Gaps.Count);
        Assert.All(context.WorldKnowledge.Gaps, gap => Assert.Contains(captureEvidenceId, gap.EvidenceIds));
        Assert.True(context.WorldRecovery!.ReviewRequired);
        Assert.NotNull(context.WorldRecovery.ReviewKey);
        var identity = Assert.Single(context.WorldRecovery.Gaps.Where(g => g.GapId ==
            Assert.Single(context.WorldKnowledge.Gaps.Where(g => g.Kind == WorldKnowledgeGapKind.Identity)).GapId));
        var attribution = Assert.Single(context.WorldRecovery.Gaps.Where(g => g.GapId ==
            Assert.Single(context.WorldKnowledge.Gaps.Where(g => g.Kind == WorldKnowledgeGapKind.Attribution)).GapId));
        var coverage = Assert.Single(context.WorldRecovery.Gaps.Where(g => g.GapId ==
            Assert.Single(context.WorldKnowledge.Gaps.Where(g => g.Kind == WorldKnowledgeGapKind.ObservationCoverage)).GapId));
        Assert.Equal(2, identity.KnownExecutionCount);
        Assert.Equal(2, attribution.KnownExecutionCount);
        Assert.Equal("recorded-executions-only", identity.RecordCoverage);
        Assert.Null(coverage.KnownExecutionCount);
        Assert.Equal("not_collected", coverage.RecordCoverage);
        Assert.All(directives.Where(d => d.Need is not null), directive =>
            Assert.Equal(new[] { WorldModel.CurrentContainerSubject }, directive.Subjects!.ToArray()));
        Assert.NotNull(world.Current!.ObservationNeed);
        Assert.Equal(3, kernel.ProjectWorldKnowledge().Gaps.Count);
        Assert.Empty(effects.ReceiptLog);

        // A later accepted identity only closes C1 identity/attribution gaps;
        // coverage keeps its original ID. A new capture starts with its own gaps.
        var identityProposal = new ObservationProposal(new(PageObservationCodec.IdentitySubject,
            PageObservationCodec.Encode(new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
                capture.SessionCorrelation, capture.CaptureTime, "settings", "Settings"))),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["fixture:identity"]))
        {
            DerivedFromEvidenceIds = [captureEvidenceId],
            TransitiveEvidenceBasis = new HashSet<string> { captureEvidenceId }.ToFrozenSet(StringComparer.Ordinal),
            FusionRule = "controlled-page-identity",
            FusionVersion = "1",
        };
        kernel.Process(identityProposal);
        var resolved = kernel.ProjectWorldKnowledge();
        var coverageAfterIdentity = Assert.Single(resolved.Gaps.Where(g => g.Kind == WorldKnowledgeGapKind.ObservationCoverage));
        Assert.Equal(Assert.Single(context.WorldKnowledge.Gaps.Where(g => g.Kind == WorldKnowledgeGapKind.ObservationCoverage)).GapId,
            coverageAfterIdentity.GapId);
        Assert.DoesNotContain(resolved.Gaps, gap => gap.Kind is WorldKnowledgeGapKind.Identity or WorldKnowledgeGapKind.Attribution);

        var nextCapture = Capture("recovery-c2", 3);
        kernel.Process(CaptureProposal(nextCapture));
        var independent = kernel.ProjectWorldKnowledge();
        Assert.Equal(3, independent.Gaps.Count);
        Assert.DoesNotContain(independent.Gaps, gap => context.WorldKnowledge.Gaps.Any(old => old.GapId == gap.GapId));
    }

    [Fact]
    public void RecoveryControl_CountsOnlyDistinctRelevantSupplierExecutions_AndReviewKeysIgnoreRevision()
    {
        var control = new WorldRecoveryControl();
        var first = Knowledge("capture-a", "rev-1");
        var execution = Execution("execution-a", "capture-a", 1);
        Assert.True(control.Record(execution, first));
        Assert.True(control.Record(execution, first)); // transport replay is idempotent
        var projected = control.Project(first);
        Assert.Equal(1, Assert.Single(projected.Gaps.Where(g => g.GapId == "gap-identity")).KnownExecutionCount);
        Assert.Null(Assert.Single(projected.Gaps.Where(g => g.GapId == "gap-coverage")).KnownExecutionCount);

        Assert.True(control.RequestReview(first, "recovery-exhausted"));
        Assert.False(control.RequestReview(first, "recovery-exhausted"));
        control.Reviewed();
        Assert.False(control.RequestReview(first with { SourceRevisionId = "rev-2" }, "recovery-exhausted"));

        var unrelated = Knowledge("capture-b", "rev-b");
        Assert.True(control.Record(execution, unrelated)); // accepted record for A cannot satisfy B
        Assert.Null(Assert.Single(control.Project(unrelated).Gaps.Where(g => g.GapId == "gap-identity")).KnownExecutionCount);
        Assert.True(control.Record(Execution("execution-b", "capture-b", 2), unrelated));
        Assert.Equal(1, Assert.Single(control.Project(unrelated).Gaps.Where(g => g.GapId == "gap-identity")).KnownExecutionCount);
        Assert.True(control.RequestReview(unrelated, "new-capture-execution"));
    }

    [Fact]
    public void RecoveryControl_HigherResultVersionKeepsOneExecutionAndBothResultVersions()
    {
        var knowledge = Knowledge("capture-versioned", "rev-1");
        var control = new WorldRecoveryControl();
        var v1 = Execution("execution-versioned", "capture-versioned", 1);
        Assert.True(control.Record(v1, knowledge));
        Assert.Equal(1, Assert.Single(control.Project(knowledge).Gaps
            .Where(g => g.GapId == "gap-identity")).KnownExecutionCount);

        Assert.True(control.RequestReview(knowledge, "recovery-exhausted"));
        control.Reviewed();
        Assert.True(control.Record(v1, knowledge)); // replay cannot increment attempts or create another review
        Assert.False(control.RequestReview(knowledge, "same-result-replayed"));

        var v2 = v1 with
        {
            CompletedAt = v1.CompletedAt.AddSeconds(1),
            Outcome = "Partial",
            ResultReferences = ["controlled-result:v2"],
            ResultVersion = 2
        };
        Assert.True(control.Record(v2, knowledge));
        var projected = control.Project(knowledge);
        var identity = Assert.Single(projected.Gaps.Where(g => g.GapId == "gap-identity"));
        Assert.Equal(1, identity.KnownExecutionCount);
        Assert.Equal("execution-versioned", Assert.Single(identity.Executions).ExecutionId);
        Assert.Equal(new[] { 1, 2 }, identity.ResultHistory.Select(e => e.ResultVersion));
        Assert.True(identity.Executions.Single().ResultReferences.Contains("controlled-result:v2"));
        Assert.True(control.RequestReview(knowledge, "new-result-version"));
        control.Reviewed();
        Assert.True(control.Record(v2, knowledge));
        Assert.False(control.RequestReview(knowledge, "same-v2-replayed"));
        Assert.Equal(1, Assert.Single(control.Project(knowledge).Gaps
            .Where(g => g.GapId == "gap-identity")).KnownExecutionCount);

        // Supplier result updates have no authority to renew the separate observation slot's quota/deadline.
        long now = 0;
        var observationRecovery = new ObservationRecoveryControl(TimeSpan.FromSeconds(5), 2, () => now, 1000);
        Assert.True(observationRecovery.TrySchedule("same-observation-slot", out _));
        now = 4_000;
        var v3 = v2 with
        {
            CompletedAt = v2.CompletedAt.AddSeconds(1),
            ResultReferences = ["controlled-result:v3"],
            ResultVersion = 3
        };
        Assert.True(control.Record(v3, knowledge));
        Assert.True(control.RequestReview(knowledge, "new-result-version"));
        control.Reviewed();
        Assert.True(control.Record(v3, knowledge));
        Assert.False(control.RequestReview(knowledge, "same-v3-replayed"));
        Assert.True(observationRecovery.CanAccept("same-observation-slot"));
        now = 5_001;
        Assert.True(observationRecovery.IsExpired("same-observation-slot"));
        Assert.False(observationRecovery.TrySchedule("same-observation-slot", out _));
        Assert.Equal(1, observationRecovery.RequestsUsed);
    }

    [Fact]
    public void Driver_ReconsultsForNewResultVersion_WithoutRenewingSupplierRequestQuota()
    {
        var capture = Capture("recovery-versioned", 1);
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string>
            { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject }, new ProductAssociationStrategy());
        var plan = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(new ForbiddenDriver()));
        var supplierRequests = 0;
        var pollAfterFirstReview = false;
        var pollCallsAfterReview = 0;
        var captureEvidenceId = "";
        SupplementationExecution? v1 = null;
        SupplementationExecution? v2 = null;
        SupplementationExecution? v3 = null;
        var contexts = new List<AgentDecisionContext>();
        var inputs = new RunDriverInputs
        {
            MaxObservationRecoveryRequests = 2,
            ObservationRecoveryTimeout = TimeSpan.FromSeconds(30),
            NextInput = directive =>
            {
                if (directive.Need is null)
                    return new RunDriverInput.Observation([CaptureProposal(capture)]);
                supplierRequests++;
                captureEvidenceId = directive.Need.CaptureEvidenceId;
                var start = T0.AddSeconds(20);
                if (supplierRequests == 1)
                {
                    v1 = Execution("versioned-supplier-run", captureEvidenceId, 20);
                    return new RunDriverInput.Observation(Array.Empty<ObservationProposal>(), v1!);
                }
                Assert.Equal(2, supplierRequests);
                v2 = v1! with
                {
                    CompletedAt = start.AddSeconds(2), Outcome = "Partial",
                    ResultReferences = ["controlled-result:v2"], ResultVersion = 2
                };
                return new RunDriverInput.Observation(Array.Empty<ObservationProposal>(), v2!);
            },
            PollSupplementationExecutions = () =>
            {
                if (!pollAfterFirstReview) return Array.Empty<SupplementationExecution>();
                pollCallsAfterReview++;
                if (v3 is null)
                    v3 = v2! with
                    {
                        CompletedAt = v2!.CompletedAt.AddSeconds(1), Outcome = "Partial",
                        ResultReferences = ["controlled-result:v3"], ResultVersion = 3
                    };
                return [v3!]; // exact same result is replayed on later safe-boundary polls
            },
            ConsultAgent = context =>
            {
                contexts.Add(context);
                return contexts.Count == 1
                    ? new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, MaxRounds: 1))
                    : new AgentDecision.Blocked(context.DecisionId, "review-new-result-version");
            }
        };
        Assert.True(kernel.AdmitContract(new("versioned-v1", "review identity uncertainty",
            new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new("pending", RunObligationKind.MaterialEffect, "test.pending", "done", true)],
            TaskInstanceId: "versioned-task", ProductSessionId: "recovery-session", ContractRef: "versioned-contract")).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        var first = driver.Drive();
        Assert.Equal(RunDriveStatus.GroundingFailed, first.Status);
        Assert.Single(contexts);
        var firstRecovery = Assert.Single(contexts[0].WorldRecovery!.Gaps
            .Where(g => g.GapId == Assert.Single(contexts[0].WorldKnowledge!.Gaps
                .Where(g => g.Kind == WorldKnowledgeGapKind.Identity)).GapId));
        Assert.Equal(1, firstRecovery.KnownExecutionCount);
        Assert.Equal(new[] { 1, 2 }, firstRecovery.ResultHistory.Select(e => e.ResultVersion));
        Assert.Equal(2, supplierRequests);

        pollAfterFirstReview = true;
        var second = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, second.Status);
        Assert.Equal("agent-blocked:review-new-result-version", second.Reason);
        Assert.Equal(2, contexts.Count);
        Assert.Equal(2, supplierRequests); // polling results did not issue/replenish a supplier request
        Assert.True(pollCallsAfterReview >= 1);
        var secondContext = contexts[1];
        Assert.True(secondContext.WorldRecovery!.ReviewRequired);
        Assert.NotEqual(contexts[0].WorldRecovery!.ReviewKey, secondContext.WorldRecovery.ReviewKey);
        var updated = Assert.Single(secondContext.WorldRecovery.Gaps
            .Where(g => g.GapId == Assert.Single(secondContext.WorldKnowledge!.Gaps
                .Where(g => g.Kind == WorldKnowledgeGapKind.Identity)).GapId));
        Assert.Equal(1, updated.KnownExecutionCount);
        Assert.Equal(new[] { 1, 2, 3 }, updated.ResultHistory.Select(e => e.ResultVersion));
        Assert.Equal("controlled-result:v3", Assert.Single(updated.Executions).ResultReferences.Single());
        Assert.Empty(kernel.EffectReceipts);
    }

    private static PageCaptureObservation Capture(string id, int second) => new(id, "cycle:" + id,
        "recovery-session", T0.AddSeconds(second), CoordinateSpace.DeviceViewport(100, 100, captureId: id),
        [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
         new("row", "ui.element", "Wi-Fi", new UiBounds(0, 25, 90, 60))]);

    private static ObservationProposal CaptureProposal(PageCaptureObservation capture) =>
        new(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-layout", capture.CaptureTime, "capture:" + capture.CaptureId, ["fixture:layout"]));

    private static SupplementationExecution Execution(string id, string captureId, int second) => new(id,
        "controlled-supplier:no-evidence", captureId, [WorldModel.CurrentContainerSubject],
        T0.AddSeconds(second), T0.AddSeconds(second).AddMilliseconds(1), "NoEvidence", Array.Empty<string>());

    private static WorldKnowledgeView Knowledge(string captureId, string revision) => new(revision, "collected",
    [
        new("gap-identity", WorldKnowledgeGapKind.Identity, captureId, "capture:" + captureId + ":viewport",
            "identity-unresolved", revision, [captureId], false, "capture:" + captureId),
        new("gap-attribution", WorldKnowledgeGapKind.Attribution, captureId, "capture:" + captureId + ":viewport",
            "owner-unresolved", revision, [captureId], false, "capture:" + captureId),
        new("gap-coverage", WorldKnowledgeGapKind.ObservationCoverage, captureId, "capture:" + captureId + ":viewport",
            "coverage-unknown", revision, [captureId], false, "capture:" + captureId),
    ]);

    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "controlled");
    }

    private sealed class ForbiddenDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("zero Effect expected");
    }
}
