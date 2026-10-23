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
using UniClaw.Kernel.Tests.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

/// <summary>Controlled suppliers/drivers through the real Ledger → World → Driver → Assurance chain.</summary>
public sealed class WorldCorrectionImpactTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "controlled-freshness");
    }

    private sealed class RecordingDriver : IEffectDriver
    {
        public List<DispatchRequest> Requests { get; } = [];
        public DispatchResult Deliver(DispatchRequest request)
        {
            Requests.Add(request);
            return new(DispatchOutcome.DeliveryCompleted, "controlled-external-driver", T0.AddMinutes(10));
        }
    }

    [Fact]
    public void CorrectedPlanBasis_ReassessesVerifiedReceipt_AndBlocksNextActAtStepBoundary()
    {
        var ledger = new EvidenceLedger();
        var scope = new HashSet<string>
        {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            PageInterpretationCodec.Subject, "ui.node.*"
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy());
        var external = new RecordingDriver();
        var policy = new AgentPlanPolicy();
        var control = new ControlLoop(policy);
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(),
            control, new RuntimeAssurance(new Fresh()), new EffectBoundary(external));

        var knownB = Capture("known-b", 1);
        Identify(kernel, knownB, "native:page:B");
        var c1 = Capture("c1-misattributed", 2);
        var visualFrames = new List<object>
        {
            WorldVisualizationExport.Frame("known-b", "先认识页面 B", kernel, world)
        };
        var c1CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c1));
        var c1IdentityId = EvidenceLedger.ComputeEvidenceId(IdentityProposal(c1, c1CaptureId, "native:page:A"));
        var c2 = Capture("c2-current", 11);
        var c2CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c2, ObservationContext.PostActionEffectFlow));
        var support = NativeContextSource(c1, "native:page:B");
        var supportId = EvidenceLedger.ComputeEvidenceId(support);
        var correction = new PageInterpretationCorrection(c1CaptureId, c1IdentityId,
            new(c1.CaptureId, c1.ObservationCycleId, c1.SessionCorrelation, c1.CaptureTime,
                "native:page:B", "Settings"), [supportId], "controlled-original-capture-parser:v1");
        var correctionProposal = CorrectionProposal(correction, c1CaptureId, c1IdentityId, supportId, c1.CaptureTime);

        var externalPull = 0;
        var postActionPull = 0;
        var consultations = 0;
        AgentDecisionContext? correctionConsult = null;
        string? c1IdentityRevisionId = null;
        string? c1RevisionBytes = null;
        string? receiptBytesBeforeCorrection = null;
        string? verificationBytesBeforeCorrection = null;
        var contract = new ExecutionContract("world-impact-v1", "correct misattributed page before continuing",
            new HashSet<string>(scope), new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new RunObligation("objective", RunObligationKind.Objective, "objective.complete", "yes", true)],
            MaxConsultations: 4, MaxTotalSteps: 8, TaskInstanceId: "impact-task", ProductSessionId: "impact-session",
            ContractRef: "impact-contract");

        var inputs = new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Context == ObservationContext.PostActionEffectFlow)
                {
                    visualFrames.Add(WorldVisualizationExport.Frame("delivered-awaiting-verify",
                        "动作已经发出，验证尚未返回", kernel, world,
                        effectImpacts: kernel.EffectCorrectionReviews));
                    postActionPull++;
                    Assert.Equal(1, postActionPull);
                    return new RunDriverInput.Observation([CaptureProposal(c2, directive.Context),
                        IdentityProposal(c2, c2CaptureId, "native:page:A", directive.Context)]);
                }

                externalPull++;
                if (externalPull == 1)
                    return new RunDriverInput.Observation([CaptureProposal(c1), IdentityProposal(c1, c1CaptureId, "native:page:A")]);
                if (externalPull == 2)
                {
                    visualFrames.Add(WorldVisualizationExport.Frame("verified-before-correction",
                        "按原依据验证通过；随后核查旧观察", kernel, world));
                    var originalRevision = world.RevisionHistory.Last(r =>
                        r.CurrentPageObservation?.EvidenceId == c1CaptureId
                        && r.CurrentPageObservation.IdentityEvidenceId == c1IdentityId);
                    c1IdentityRevisionId = originalRevision.RevisionId;
                    c1RevisionBytes = System.Text.Json.JsonSerializer.Serialize(originalRevision);
                    receiptBytesBeforeCorrection = System.Text.Json.JsonSerializer.Serialize(kernel.EffectReceipts.Single());
                    verificationBytesBeforeCorrection = System.Text.Json.JsonSerializer.Serialize(kernel.PostActionVerifications.Single());
                    return new RunDriverInput.Observation([support, correctionProposal]);
                }
                return null;
            },
            ConsultAgent = context =>
            {
                consultations++;
                if (consultations == 1)
                {
                    visualFrames.Add(WorldVisualizationExport.Frame("misattributed-before-plan",
                        "原观察误归页面 A，并被计划消费", kernel, world, actualConsultation: context));
                    return new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("button", "Wi-Fi", "tap", null),
                        new PlanItem.ObserveItem("reinterpret-original-capture"),
                        new PlanItem.ActItem("button", "Bluetooth", "tap", null),
                    }, "first action, then review historical identity before continuing", "corrected-plan"));
                }
                correctionConsult = context;
                visualFrames.Add(WorldVisualizationExport.Frame("corrected-and-blocked",
                    "旧观察更正到 B，原效果未决，后续动作停止", kernel, world,
                    world.Current!.Containers!.Select(c => c.Identity.ContainerId).ToArray(),
                    effectImpacts: kernel.EffectCorrectionReviews, terminal: kernel.IsRunTerminal,
                    actualConsultation: context));
                return new AgentDecision.Blocked(context.DecisionId, "review-corrected-world-basis");
            }
        };

        Assert.True(kernel.AdmitContract(contract).Accepted);
        var driver = new KernelRunDriver(kernel, policy, inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.True(result.Status == RunDriveStatus.TerminalNotProven, result.Status + ":" + result.Reason);
        Assert.Equal("agent-blocked:review-corrected-world-basis", result.Reason);
        Assert.Equal(1, external.Requests.Count);
        Assert.Single(kernel.EffectReceipts);
        var verification = Assert.Single(kernel.PostActionVerifications);
        Assert.True(verification.IsVerified);
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
        Assert.Equal("c2-current", world.Current!.CurrentPageObservation!.Capture.CaptureId);
        Assert.Equal(c1RevisionBytes, System.Text.Json.JsonSerializer.Serialize(world.RevisionHistory
            .Single(r => r.RevisionId == c1IdentityRevisionId)));
        Assert.Equal(receiptBytesBeforeCorrection, System.Text.Json.JsonSerializer.Serialize(kernel.EffectReceipts.Single()));
        Assert.Equal(verificationBytesBeforeCorrection, System.Text.Json.JsonSerializer.Serialize(verification));
        Assert.Equal(2, consultations);
        Assert.NotNull(correctionConsult);
        Assert.Contains(correctionConsult!.WorldKnowledge!.Corrections, item => item.Applied
            && item.TargetCaptureEvidenceId == c1CaptureId);
        var impact = Assert.Single(correctionConsult.WorldEffectImpacts!);
        Assert.True(impact.InitiallyVerified);
        Assert.Equal("Unresolved", impact.Resolution);
        Assert.Contains(c1IdentityId, impact.RevokedConsumedEvidenceIds);
        Assert.False(kernel.IsRunTerminal);
        Assert.Contains("planning-basis-retracted", Assert.Throws<InvalidOperationException>(() =>
            kernel.Act(control.IntentLog.Single(i => i.Kind == ControlIntentKind.Act), null,
                [kernel.WorldDecisionUses.First().DecisionId])).Message);
        Assert.Single(external.Requests);
        WorldVisualizationExport.Write("correction", visualFrames.ToArray());
        if (Environment.GetEnvironmentVariable("UNICLAW_WORLD_CORRECTION_EXPORT") is { Length: > 0 } output)
        {
            var example = new
            {
                schemaVersion = "uniclaw.world-correction-example.v1",
                evidenceClass = "real-kernel-driver-assurance;controlled-layout-native-source-and-delivery",
                liveDeviceOrModelProven = false,
                revisions = world.RevisionHistory,
                corrections = world.PageCorrectionLog,
                decisionUses = kernel.WorldDecisionUses,
                receipts = kernel.EffectReceipts,
                verifications = kernel.PostActionVerifications,
                effectImpacts = kernel.EffectCorrectionReviews,
                actualConsultation = correctionConsult,
                currentPages = world.Current!.Containers!.Select(c => world.DerivePageView(c.Identity.ContainerId)),
                immutableHistoricalRevisionId = c1IdentityRevisionId,
                deliveredEffects = external.Requests.Count,
            };
            System.IO.File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(example,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void IndependentCurrentCaptureActionBasis_DoesNotContainOlderCorrectedCaptureIdentity()
    {
        var ledger = new EvidenceLedger();
        var scope = new HashSet<string>
        {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            PageInterpretationCodec.Subject, "ui.node.*"
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy());
        var policy = new AgentPlanPolicy();
        var external = new RecordingDriver();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(policy),
            new RuntimeAssurance(new Fresh()), new EffectBoundary(external));
        var knownB = Capture("known-b2", 1);
        var c1 = Capture("old-c1", 2);
        var c1CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c1));
        var c1IdentityId = EvidenceLedger.ComputeEvidenceId(IdentityProposal(c1, c1CaptureId, "native:page:A"));
        var c2 = Capture("current-c2", 3);
        var c2CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c2));
        var knownBCaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(knownB));
        var knownBIdentity = IdentityProposal(knownB, knownBCaptureId, "native:page:B");
        var c1Identity = IdentityProposal(c1, c1CaptureId, "native:page:A");
        var c2Identity = IdentityProposal(c2, c2CaptureId, "native:page:A");
        var support = NativeContextSource(c1, "native:page:B");
        var supportId = EvidenceLedger.ComputeEvidenceId(support);
        var correction = new PageInterpretationCorrection(c1CaptureId, c1IdentityId,
            new(c1.CaptureId, c1.ObservationCycleId, c1.SessionCorrelation, c1.CaptureTime,
                "native:page:B", "Settings"), [supportId], "controlled-original-capture-parser:v1");

        Assert.True(kernel.AdmitContract(new ExecutionContract("world-impact-independent-v1", "act on current C2",
            new HashSet<string>(scope), new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new RunObligation("objective", RunObligationKind.Objective, "objective.complete", "yes", true)],
            MaxConsultations: 3, MaxTotalSteps: 4, TaskInstanceId: "independent-task",
            ProductSessionId: "impact-session", ContractRef: "independent-contract")).Accepted);
        var initialPulls = 0;
        var inputs = new RunDriverInputs
        {
            NextInput = _ => initialPulls++ == 0
                ? new RunDriverInput.Observation([
                    CaptureProposal(knownB), knownBIdentity,
                    CaptureProposal(c1), c1Identity,
                    CaptureProposal(c2), c2Identity,
                    support,
                    CorrectionProposal(correction, c1CaptureId, c1IdentityId, supportId, c1.CaptureTime)])
                : null,
            ConsultAgent = context => new AgentDecision.Plan(context.DecisionId,
                new AgentPlanProposal([new PlanItem.ActItem("button", "Wi-Fi", "tap", null)],
                    "act on independently observed C2", "independent-c2-plan"))
        };
        var driver = new KernelRunDriver(kernel, policy, inputs);
        Assert.True(driver.Activate().Accepted);
        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.WaitingForInput, result.Status);
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
        Assert.Equal("current-c2", world.Current!.CurrentPageObservation!.Capture.CaptureId);
        Assert.Single(external.Requests);
        var use = Assert.Single(kernel.EffectWorldUses);
        Assert.DoesNotContain(c1CaptureId, use.ActionBasis.EvidenceIds);
        Assert.DoesNotContain(c1IdentityId, use.ActionBasis.EvidenceIds);
        Assert.Contains(c2CaptureId, use.ActionBasis.EvidenceIds);
        Assert.Contains(world.Current.CurrentPageObservation!.IdentityEvidenceId!, use.ActionBasis.EvidenceIds);
        Assert.Empty(kernel.EffectCorrectionReviews);
    }

    [Fact]
    public void CorrectionWhileReceiptAwaitsStepVerify_BlocksVerificationWithoutRedispatchOrInverse()
    {
        var ledger = new EvidenceLedger();
        var scope = new HashSet<string>
        {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            PageInterpretationCodec.Subject, "ui.node.*"
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy());
        var external = new RecordingDriver();
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(),
            new ControlLoop(policy), new RuntimeAssurance(new Fresh()), new EffectBoundary(external));

        var knownB = Capture("verify-known-b", 1);
        Identify(kernel, knownB, "native:page:B");
        var c1 = Capture("verify-c1", 2);
        var c1CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c1));
        var c1Identity = IdentityProposal(c1, c1CaptureId, "native:page:A");
        var c1IdentityId = EvidenceLedger.ComputeEvidenceId(c1Identity);
        var c2 = Capture("verify-c2", 11);
        var c2CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c2, ObservationContext.PostActionEffectFlow));
        var support = NativeContextSource(c1, "native:page:B");
        var supportId = EvidenceLedger.ComputeEvidenceId(support);
        var correction = new PageInterpretationCorrection(c1CaptureId, c1IdentityId,
            new(c1.CaptureId, c1.ObservationCycleId, c1.SessionCorrelation, c1.CaptureTime,
                "native:page:B", "Settings"), [supportId], "controlled-original-capture-parser:v1");
        var correctionProposal = CorrectionProposal(correction, c1CaptureId, c1IdentityId,
            supportId, c1.CaptureTime);

        var externalCalls = 0;
        var postCalls = 0;
        var consultations = 0;
        var contract = new ExecutionContract("world-impact-pending-verify-v1", "review corrected page before continuing",
            new HashSet<string>(scope), new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new RunObligation("objective", RunObligationKind.Objective, "objective.complete", "yes", true)],
            MaxConsultations: 3, MaxTotalSteps: 5, TaskInstanceId: "pending-verify-task",
            ProductSessionId: "impact-session", ContractRef: "pending-verify-contract");
        var inputs = new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Context == ObservationContext.PostActionEffectFlow)
                {
                    if (postCalls++ == 0) return null;
                    return new RunDriverInput.Observation([CaptureProposal(c2, directive.Context),
                        IdentityProposal(c2, c2CaptureId, "native:page:A", directive.Context)]);
                }
                if (externalCalls++ == 0)
                    return new RunDriverInput.Observation([CaptureProposal(c1), c1Identity]);
                return null;
            },
            ConsultAgent = context =>
            {
                consultations++;
                return consultations == 1
                    ? new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(
                        [new PlanItem.ActItem("button", "Wi-Fi", "tap", null)],
                        "dispatch once, then verify", "pending-verification-plan"))
                    : new AgentDecision.Blocked(context.DecisionId, "review-corrected-verification-basis");
            }
        };

        Assert.True(kernel.AdmitContract(contract).Accepted);
        var driver = new KernelRunDriver(kernel, policy, inputs);
        Assert.True(driver.Activate().Accepted);
        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Equal("post-action-evidence", waiting.Reason);
        var receiptBytes = System.Text.Json.JsonSerializer.Serialize(Assert.Single(kernel.EffectReceipts));

        // The receipt exists, but StepVerify is still waiting. Correct the exact
        // source consumed by its binding before providing the post-action capture.
        Assert.True(kernel.Process(support).Admission.Decision == AdmissionDecision.Accepted);
        Assert.True(kernel.Process(correctionProposal).Admission.Decision == AdmissionDecision.Accepted);
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
        Assert.Equal("verify-c1", world.Current!.CurrentPageObservation!.Capture.CaptureId);

        var resumed = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, resumed.Status);
        var verification = Assert.Single(kernel.PostActionVerifications);
        Assert.False(verification.IsVerified);
        Assert.Equal("post-action-consumption-basis-retracted", verification.RejectionReason);
        Assert.Equal(receiptBytes, System.Text.Json.JsonSerializer.Serialize(Assert.Single(kernel.EffectReceipts)));
        Assert.Single(external.Requests); // no retry and no inverse dispatch
        var review = Assert.Single(kernel.EffectCorrectionReviews);
        Assert.Contains(c1IdentityId, review.RevokedConsumedEvidenceIds);
        Assert.False(review.TerminalAlreadyRecorded);
    }

    [Fact]
    public void CorrectionAfterRecordedTerminal_AppendsReviewWithoutChangingOutcomeOrDelivery()
    {
        var ledger = new EvidenceLedger();
        var scope = new HashSet<string>
        {
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject,
            PageInterpretationCodec.Subject, "ui.node.*", "objective.complete", "x.state"
        };
        var world = new WorldModel(scope, new ProductAssociationStrategy());
        var external = new RecordingDriver();
        var policy = new AgentPlanPolicy();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(),
            new ControlLoop(policy), new RuntimeAssurance(new Fresh()), new EffectBoundary(external));

        var knownB = Capture("terminal-known-b", 1);
        Identify(kernel, knownB, "native:page:B");
        var c1 = Capture("terminal-c1", 2);
        var c1CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c1));
        var c1Identity = IdentityProposal(c1, c1CaptureId, "native:page:A");
        var c1IdentityId = EvidenceLedger.ComputeEvidenceId(c1Identity);
        var c2 = Capture("terminal-c2", 11);
        var c2CaptureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(c2, ObservationContext.PostActionEffectFlow));
        var support = NativeContextSource(c1, "native:page:B");
        var supportId = EvidenceLedger.ComputeEvidenceId(support);
        var correction = new PageInterpretationCorrection(c1CaptureId, c1IdentityId,
            new(c1.CaptureId, c1.ObservationCycleId, c1.SessionCorrelation, c1.CaptureTime,
                "native:page:B", "Settings"), [supportId], "controlled-original-capture-parser:v1");
        var correctionProposal = CorrectionProposal(correction, c1CaptureId, c1IdentityId,
            supportId, c1.CaptureTime);

        var externalCalls = 0;
        var receiptId = (string?)null;
        var consultations = 0;
        var contract = new ExecutionContract("world-impact-terminal-v1", "complete after verified action",
            new HashSet<string>(scope), new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new RunObligation("objective", RunObligationKind.MaterialEffect, "x.state", "done", true)],
            MaxConsultations: 4, MaxTotalSteps: 5, TaskInstanceId: "terminal-impact-task",
            ProductSessionId: "impact-session", ContractRef: "terminal-impact-contract");
        var inputs = new RunDriverInputs
        {
            NextInput = directive =>
            {
                if (directive.Context == ObservationContext.PostActionEffectFlow)
                    return new RunDriverInput.Observation([CaptureProposal(c2, directive.Context),
                        IdentityProposal(c2, c2CaptureId, "native:page:A", directive.Context)]);
                if (externalCalls++ == 0) return new RunDriverInput.Observation([CaptureProposal(c1), c1Identity]);
                return null;
            },
            ConsultAgent = context =>
            {
                consultations++;
                if (consultations == 1)
                    return new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(
                        [new PlanItem.ActItem("button", "Wi-Fi", "tap", null)],
                        "verify one action before completion", "terminal-impact-plan"));
                receiptId = Assert.Single(kernel.EffectReceipts).ReceiptId;
                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId,
                    "complete from verified dispatch", new CompletionEvidence("verified-dispatch",
                        [$"dispatch:{receiptId}"])));
            }
        };

        Assert.True(kernel.AdmitContract(contract).Accepted);
        var driver = new KernelRunDriver(kernel, policy, inputs);
        Assert.True(driver.Activate().Accepted);
        var completed = driver.Drive();
        Assert.Equal(RunDriveStatus.Completed, completed.Status);
        Assert.True(kernel.IsRunTerminal);
        Assert.NotNull(completed.Outcome);
        Assert.Single(external.Requests);
        var receiptBefore = System.Text.Json.JsonSerializer.Serialize(Assert.Single(kernel.EffectReceipts));
        var outcomeBefore = System.Text.Json.JsonSerializer.Serialize(kernel.RunState!.Outcome);
        var terminalFrames = new List<object>
        {
            WorldVisualizationExport.Frame("terminal-recorded", "Terminal outcome and receipt recorded before correction",
                kernel, world, terminal: kernel.IsRunTerminal, outcome: kernel.RunState.Outcome,
                effectImpacts: kernel.EffectCorrectionReviews)
        };

        // A later historical correction may append a review, but cannot reopen
        // delivery or mutate the already-recorded terminal outcome.
        kernel.Process(support);
        kernel.Process(correctionProposal);
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
        var review = Assert.Single(kernel.EffectCorrectionReviews);
        Assert.True(review.TerminalAlreadyRecorded);
        Assert.Contains(c1IdentityId, review.RevokedConsumedEvidenceIds);
        Assert.Equal("Unresolved", review.Resolution);
        Assert.Equal(receiptBefore, System.Text.Json.JsonSerializer.Serialize(Assert.Single(kernel.EffectReceipts)));
        Assert.Equal(outcomeBefore, System.Text.Json.JsonSerializer.Serialize(kernel.RunState!.Outcome));
        Assert.Equal(RunDriveStatus.AlreadyTerminal, driver.Drive().Status);
        Assert.Single(external.Requests); // no redispatch/compensation after terminal
        terminalFrames.Add(WorldVisualizationExport.Frame("terminal-correction-review",
            "Correction appends an unresolved effect review without reopening terminal run",
            kernel, world, terminal: kernel.IsRunTerminal, outcome: kernel.RunState!.Outcome,
            effectImpacts: kernel.EffectCorrectionReviews));
        WorldVisualizationExport.Write("terminal", terminalFrames.ToArray());
    }

    private sealed class LegacyPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs input) => new(ControlIntentKind.Act, "tap", "legacy.target");
    }

    [Fact]
    public void ActualLegacyDeliveryWithoutPageLineage_IsUnknownDependencyScopeAfterCorrection()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "legacy.target", "ui.node.*",
            PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject, PageInterpretationCodec.Subject },
            new ProductAssociationStrategy());
        var external = new RecordingDriver();
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(),
            new ControlLoop(new LegacyPolicy()), new RuntimeAssurance(new Fresh()), new EffectBoundary(external));
        Assert.True(kernel.AdmitContract(new("legacy-lineage-v1", "inspect missing page dependency coverage",
            new HashSet<string> { "legacy.target" }, new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            TaskInstanceId: "legacy-lineage-task", ProductSessionId: "impact-session", ContractRef: "legacy-lineage-contract")).Accepted);
        foreach (var (subject, value) in new[] { ("ui.screen", "legacy-page"), ("legacy.target", "idle") })
            kernel.Process(new(new(subject, value), IngressKind.Observation, ObservationContext.External,
                new("controlled-legacy-source", T0, "legacy:" + subject, ["controlled-legacy-source"])));
        var root = world.Current!.Containers!.Single().Identity.ContainerId;
        var intent = kernel.SelectIntent(kernel.DeriveSlice(root));
        var delivered = kernel.Act(intent, new("legacy.target", "idle", world.Current.RevisionId));
        Assert.NotNull(delivered.Receipt);
        Assert.Single(external.Requests);
        Assert.Null(Assert.Single(kernel.EffectWorldUses).ActionBasis.CaptureEvidenceId);

        Identify(kernel, Capture("unknown-known-b", 1), "native:page:B");
        var c1 = Capture("unknown-c1", 2);
        var captureId = Identify(kernel, c1, "native:page:A");
        var identityId = world.Current!.CurrentPageObservation!.IdentityEvidenceId!;
        var support = NativeContextSource(c1, "native:page:B");
        var supportId = kernel.Process(support).Admission.EvidenceId!;
        var correction = new PageInterpretationCorrection(captureId, identityId,
            new(c1.CaptureId, c1.ObservationCycleId, c1.SessionCorrelation, c1.CaptureTime,
                "native:page:B", "Settings"), [supportId], "controlled-original-capture-parser:v1");
        kernel.Process(CorrectionProposal(correction, captureId, identityId, supportId, c1.CaptureTime));
        Assert.True(world.PageCorrectionLog[^1].Applied, world.PageCorrectionLog[^1].Reason);
        var review = Assert.Single(kernel.EffectCorrectionReviews);
        Assert.Equal("DependenciesUnknown", review.Resolution);
        Assert.Equal("not_collected", review.DependencyCoverage);
        Assert.Empty(review.RevokedConsumedEvidenceIds); // absence of recorded lineage is not zero impact
        Assert.Single(external.Requests);
    }

    private static string Identify(UniKernel kernel, PageCaptureObservation capture, string signature)
    {
        var captureId = EvidenceLedger.ComputeEvidenceId(CaptureProposal(capture));
        kernel.Process(CaptureProposal(capture));
        kernel.Process(IdentityProposal(capture, captureId, signature));
        return captureId;
    }

    private static PageCaptureObservation Capture(string id, int minute) => new(id, "cycle:" + id,
        "impact-session", T0.AddMinutes(minute), CoordinateSpace.DeviceViewport(120, 160, captureId: id),
        [new("title", "text", "Settings", new UiBounds(0, 0, 110, 20)),
         new("wifi", "button", "Wi-Fi", new UiBounds(0, 25, 110, 60)),
         new("bluetooth", "button", "Bluetooth", new UiBounds(0, 65, 110, 100))])
    { DeviceId = "fixture-device" };

    private static ObservationProposal CaptureProposal(PageCaptureObservation capture,
        ObservationContext context = ObservationContext.External) => new(
        new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)), IngressKind.Observation,
        context, new("controlled-layout", capture.CaptureTime, "capture:" + capture.CaptureId,
            ["controlled-fixture:" + capture.CaptureId]));

    private static ObservationProposal IdentityProposal(PageCaptureObservation capture, string captureEvidenceId,
        string signature, ObservationContext context = ObservationContext.External) => new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(
            new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId, capture.SessionCorrelation,
                capture.CaptureTime, signature, "Settings"))), IngressKind.Observation, context,
        new("controlled-identity", capture.CaptureTime, "identity:" + capture.CaptureId, ["capture:" + capture.CaptureId]))
    {
        DerivedFromEvidenceIds = [captureEvidenceId],
        TransitiveEvidenceBasis = new HashSet<string>(StringComparer.Ordinal) { captureEvidenceId },
        FusionRule = "controlled-page-identity",
        FusionVersion = "1"
    };

    private static ObservationProposal NativeContextSource(PageCaptureObservation capture, string signature) => new(
        new("ui.node." + capture.CaptureId + "#0.resource_id", signature), IngressKind.Observation,
        ObservationContext.External, new("platform.uiautomator", capture.CaptureTime, "native-context:" + capture.CaptureId,
            ["typed-hierarchy:v1", PageInterpretationCodec.NativeContextProfile],
            new HierarchyCaptureDescriptor(capture.CaptureId, 35, UiHierarchyAcquirerKind.AndroidXUiAutomator,
                "controlled-native-profile:v1", UiHierarchyFormat.UiAutomatorXml, "fixture-device",
                capture.SessionCorrelation, capture.ObservationCycleId, capture.CaptureTime, null,
                HierarchyCapability.SemanticText, CoverageCompleteness.Partial, "viewport", 0, null,
                "resource_id", capture.Space)));

    private static ObservationProposal CorrectionProposal(PageInterpretationCorrection correction,
        string captureEvidenceId, string identityEvidenceId, string supportEvidenceId, DateTimeOffset time) =>
        new(new(PageInterpretationCodec.Subject, PageInterpretationCodec.Encode(correction)), IngressKind.Observation,
            ObservationContext.External, new("controlled-original-capture-parser", time, "correction:" + captureEvidenceId,
                ["controlled-original-capture-reprocessing"]))
        {
            DerivedFromEvidenceIds = [captureEvidenceId, identityEvidenceId, supportEvidenceId],
            TransitiveEvidenceBasis = new HashSet<string>(StringComparer.Ordinal)
                { captureEvidenceId, supportEvidenceId },
            FusionRule = "page.interpretation",
            FusionVersion = "1"
        };
}
