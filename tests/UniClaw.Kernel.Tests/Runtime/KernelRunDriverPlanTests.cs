using UniClaw.Kernel.Assurance;
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

/// <summary>
/// AGT-009 — advisory AgentDecision.Plan 的采纳与逐项执行（§11）：
/// V7 机械校验（malformed / 未知 PlanItem / 超预算 → 零 Effect）、
/// 多 ActItem 串行执行（每次最多一个 Act；每个 Act 独立
/// grounding → Assurance → Effect Gate → dispatch → verification，
/// 不变量 43 屏障）、ObserveItem 零 Effect 且不单独宣告完成、
/// 失效条件（冲突 / 验证失败废弃剩余计划）、ControlItem 有界转移
/// （Reobserve / Replan / Stop；Replan 消耗全局咨询预算不重置）。
/// 全部验证行为（调用链 + 可观察结果），不验证字段存在性。
/// </summary>
public sealed class KernelRunDriverPlanTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 10, 2, 9, 5, 0, TimeSpan.Zero);

    private sealed class AlwaysFresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "test:sufficient");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test:ok", T0);
    }

    private sealed class UnknownDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.UnknownOutcome, "test:unknown", T0, "simulated-timeout");
    }

    private sealed class CallbackDriver(Action<DispatchRequest> delivered) : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request)
        {
            delivered(request);
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "test:record-verified", T0);
        }
    }

    private sealed class SequenceControlPolicy(params ControlDecision[] decisions) : IControlPolicy
    {
        private int _index;

        public ControlDecision Decide(ControlInputs inputs)
        {
            var index = Math.Min(_index++, decisions.Length - 1);
            return decisions[index];
        }
    }

    private sealed class SeparateEveryObservationAssociationStrategy : IAssociationStrategy
    {
        public AssociationProposal Propose(AssociationInput input) => new(
            AssociationDispositionKind.New,
            MatchedContainerId: null,
            new[]
            {
                new AssociationCandidate("(new)", new[] { input.Current.EvidenceId }, Array.Empty<string>()),
            },
            Relations: Array.Empty<ProposedRelation>(),
            Reason: "test-separate-observation-containers");
    }

    // ---- 夹具（同 KernelRunDriverFinalizationTests 约定）----------------------

    /// <summary>测试域 Rogue PlanItem（closed union 之外的派生类型）：plan:unknown-item 防御面。</summary>
    private sealed record RoguePlanItem : PlanItem;

    private static ExecutionContract PlanContract(int? consultations = null, int? totalSteps = null) => new(
        "plan-v1", "traverse-with-plan",
        new HashSet<string> { UIWorldDoubles.Observed, "ui.node.cap-x#0.resource_id" },
        new HashSet<string> { "toggle", "tap" }, new HashSet<string>(),
        new[] { "objective" },
        new[]
        {
            new RunObligation(
                "obl-switch", RunObligationKind.Objective, "ui.role.switch.checked", "checked", true,
                EntityScope: new TargetDescriptor("switch", "primary")),
        },
        consultations, totalSteps,
        TaskInstanceId: "task-plan", ProductSessionId: "session-plan",
        ContractRef: "contract-plan");

    private static RunDriverInput.Observation SeedObservation() => new(new[]
    {
        new ObservationProposal(
            new ObservationClaim("live.frame", "seed"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("test", T0, "scope:live.frame", new[] { "test:seed" })),
    });

    /// <summary>状态化 occurrence 世界（同 KernelRunDriverTests 约定）。</summary>
    private sealed class StatefulObservationStrategy(string? owner = null) : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if ((record.Claim.Subject.StartsWith("ui.node.", StringComparison.Ordinal)
                    || record.Claim.Subject.StartsWith("record:", StringComparison.Ordinal))
                && previous?.Occurrences is { Count: > 0 } prior)
            {
                return prior.Select(o => new ProposedOccurrence(
                    owner, o.Role, o.SemanticDescriptor, o.State,
                    o.Locator, o.Native, o.Space)).ToArray();
            }

            return record.Claim.Value.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment =>
                {
                    var roleParts = segment.Split(':', 2);
                    var descParts = roleParts.Length == 2 ? roleParts[1].Split('@', 2) : roleParts[0].Split('@', 2);
                    return new ProposedOccurrence(
                        owner, roleParts[0],
                        descParts.Length == 2 ? descParts[0] : null,
                        descParts.Length == 2 ? descParts[1] : null,
                        Native: roleParts[0] == "switch" && descParts.Length == 2 && descParts[0] == "primary"
                            ? new NativeLocator("android.resource-id", "switch-primary")
                            : null);
                })
                .ToArray();
        }
    }

    private static string ProbeContainerId(string seedValue)
    {
        var (admission, _) = new EvidenceLedger().Admit(
            UIWorldDoubles.Observation(seedValue, UIWorldDoubles.T0));
        return "ctr-" + admission.EvidenceId![3..15];
    }

    private static ObservationProposal PostActionObservation(string value, DateTimeOffset t) => new(
        new ObservationClaim(UIWorldDoubles.Observed, value),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance("provider.scripts", t, "scope:ui.container", new[] { "raw://capture", "encode:v1" }));

    private const string TypedSubject = "ui.node.cap-x#0.checked";

    private static ObservationProposal TypedCheckedClaim(string value, DateTimeOffset t) => new(
        new ObservationClaim(TypedSubject, value),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance(
            UniClaw.Kernel.Perception.UiHierarchy.TypedHierarchyProposalProjector.Producer, t,
            $"scope:{TypedSubject}",
            new[] { UniClaw.Kernel.Perception.UiHierarchy.TypedHierarchyProposalProjector.LineageMarker },
            Hierarchy: new UniClaw.Kernel.Perception.UiHierarchy.HierarchyCaptureDescriptor(
                CaptureId: "cap-x", AndroidApiLevel: 34,
                UniClaw.Kernel.Perception.UiHierarchy.UiHierarchyAcquirerKind.LegacyUiAutomatorXml, "1.0",
                UniClaw.Kernel.Perception.UiHierarchy.UiHierarchyFormat.UiAutomatorXml, "dev-1", "sess-1",
                ObservationCycleId: null, CaptureTimestamp: t,
                CaptureDuration: null,
                UniClaw.Kernel.Perception.UiHierarchy.HierarchyCapability.CheckedBooleanCollapsed,
                UniClaw.Kernel.Perception.UiHierarchy.CoverageCompleteness.CompleteWithinDeclaredSurface,
                CoverageLimitation: null, NodeLocalIndex: 0, ParentLocalIndex: null,
                Field: "checked")));

    private static ObservationProposal TypedResourceClaim(DateTimeOffset t) => new(
        new ObservationClaim("ui.node.cap-x#0.resource_id", "switch-primary"),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance(
            UniClaw.Kernel.Perception.UiHierarchy.TypedHierarchyProposalProjector.Producer, t,
            "scope:ui.node.cap-x#0.resource_id",
            new[] { UniClaw.Kernel.Perception.UiHierarchy.TypedHierarchyProposalProjector.LineageMarker },
            Hierarchy: new UniClaw.Kernel.Perception.UiHierarchy.HierarchyCaptureDescriptor(
                CaptureId: "cap-x", AndroidApiLevel: 34,
                UniClaw.Kernel.Perception.UiHierarchy.UiHierarchyAcquirerKind.LegacyUiAutomatorXml, "1.0",
                UniClaw.Kernel.Perception.UiHierarchy.UiHierarchyFormat.UiAutomatorXml, "dev-1", "sess-1",
                ObservationCycleId: null, CaptureTimestamp: t, CaptureDuration: null,
                UniClaw.Kernel.Perception.UiHierarchy.HierarchyCapability.CheckedBooleanCollapsed,
                UniClaw.Kernel.Perception.UiHierarchy.CoverageCompleteness.CompleteWithinDeclaredSurface,
                CoverageLimitation: null, NodeLocalIndex: 0, ParentLocalIndex: null, Field: "resource_id")));

    /// <summary>组装带 occurrence 世界（StepAct/StepVerify 可达）的 kernel + driver。</summary>
    private static (UniKernel Kernel, KernelRunDriver Driver) ComposePlanWorld(
        Func<ObservationDirective, RunDriverInput?> nextInput,
        Func<UniKernel, Func<AgentDecisionContext, AgentDecision?>> consultFactory,
        ExecutionContract contract,
        string seedValue = "switch:primary@off",
        Func<string, PlanCandidateSet?>? projectPlanCandidates = null,
        Func<AgentActionStep, AgentDecisionContext, PlanStepGuardResult>? guardStep = null,
        AgentPlanPolicy? planPolicy = null,
        IControlPolicy? controlPolicy = null,
        IEffectDriver? effectDriver = null,
        IAssociationStrategy? associationStrategy = null,
        Func<string, PlanCandidate?, PlanConditionResult?>? evaluatePlanCondition = null)
    {
        var cid = ProbeContainerId(seedValue);
        var plan = planPolicy ?? new AgentPlanPolicy();
        var effects = new EffectBoundary(effectDriver ?? new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(
                new HashSet<string>(contract.Scope.Append(TypedSubject).Append(CheckedGuardFixture.SubjectFor(TypedSubject))),
                associationStrategy ?? new SeedContainerAssociationStrategy(),
                new StatefulObservationStrategy(cid),
                new RoleContinuityStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(controlPolicy ?? plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        var inputs = new RunDriverInputs
        {
            NextInput = nextInput,
            ConsultAgent = consultFactory(kernel),
            ProjectPlanCandidates = projectPlanCandidates,
            EvaluatePlanCondition = evaluatePlanCondition,
            GuardStep = guardStep,
        };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(kernel.AdmitContract(contract).Accepted);
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver);
    }

    private static AgentDecision PlanDecision(string decisionId, params PlanItem[] items) =>
        new AgentDecision.Plan(decisionId, new AgentPlanProposal(items, "test-plan"));

    [Fact]
    public void ConditionalRepeat_UsesFreshOwnerFactsWithoutPerIterationConsultation()
    {
        var observed = new List<string>();
        var consults = 0;
        var evaluations = 0;
        var (kernel, driver) = ComposePlanWorld(
            expected =>
            {
                if (expected.Subjects?.SingleOrDefault() is { } subject) observed.Add(subject);
                return new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", T0.AddSeconds(observed.Count)),
                });
            },
            _ => ctx => ++consults == 1
                ? new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                {
                    new PlanItem.RepeatItem(new PlanItem[]
                    {
                        new PlanItem.IfItem("records.need-evidence",
                            new PlanItem[] { new PlanItem.ObserveItem("collect-evidence") },
                            new PlanItem[] { new PlanItem.ObserveItem("inspect-result") }),
                    }, 3),
                }, "choose from new owner facts", "conditional-records", MaxLocalOperations: 20))
                : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "not a completion proof")),
            PlanContract(),
            evaluatePlanCondition: (name, candidate) =>
            {
                Assert.Equal("records.need-evidence", name);
                Assert.Null(candidate);
                evaluations++;
                return new PlanConditionResult(observed.Count == 0 ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                    "records", $"observations:{observed.Count}", new[] { "owner:records:current-state" });
            });
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(new[] { "collect-evidence", "inspect-result", "inspect-result" }, observed);
        Assert.Equal(3, evaluations);
        Assert.Equal(2, consults);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void NestedCandidateBranch_ResumesParentAndProcessesNewCandidateWithoutDuplicateEffects()
    {
        var postPulls = 0;
        var postEnabled = false;
        var consults = 0;
        var evaluations = 0;
        var (kernel, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off+menuItem:list@visible", UIWorldDoubles.T0) })
                : !postEnabled ? null : new RunDriverInput.Observation(new[]
                {
                    PostActionObservation($"switch:primary@on+menuItem:list@{(++postPulls == 1 ? "visible" : "visited")}", T1.AddSeconds(postPulls)),
                    CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1.AddSeconds(postPulls))),
                    TypedCheckedClaim("checked", T1.AddSeconds(postPulls)),
                    TypedResourceClaim(T1.AddSeconds(postPulls)),
                }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.RepeatItem(new PlanItem[]
                        {
                            new PlanItem.ForEachItem("current", new PlanItem[]
                            {
                                new PlanItem.IfItem("candidate.is-switch",
                                    new PlanItem[]
                                    {
                                        new PlanItem.CheckpointItem("review nested branch"),
                                        new PlanItem.ActItem("$candidate", "$candidate", "toggle", "on"),
                                    },
                                    new PlanItem[] { new PlanItem.ActItem("$candidate", "$candidate", "tap", null) }),
                            }, 3),
                        }, 2),
                    }, "use fresh candidates and choose per-item behavior", "nested-candidates", MaxLocalOperations: 40));
                if (ctx.PlanExecution?.Status == PlanExecutionStatus.Paused)
                {
                    Assert.False(string.IsNullOrWhiteSpace(ctx.PlanExecution.Position.ControlPath));
                    Assert.Equal("primary", ctx.PlanExecution.Position.CandidateKey);
                    return new AgentDecision.ContinuePlan(ctx.DecisionId, ctx.PlanExecution.Position);
                }
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "done"));
            },
            PlanContract(), seedValue: "switch:primary@off+menuItem:list@visible",
            projectPlanCandidates: _ => new PlanCandidateSet("current", $"r{postPulls}", postPulls switch
            {
                0 => new[] { new PlanCandidate("primary", "switch", "primary", PlanCandidateDisposition.Ready, new[] { "owner:switch" }) },
                1 => new[] { new PlanCandidate("list", "menuItem", "list", PlanCandidateDisposition.Ready, new[] { "owner:menu" }) },
                _ => Array.Empty<PlanCandidate>(),
            }),
            evaluatePlanCondition: (_, candidate) =>
            {
                evaluations++;
                Assert.NotNull(candidate);
                return new PlanConditionResult(candidate!.TargetRole == "switch" ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                    "current", $"r{postPulls}", candidate.EvidenceRefs);
            });
        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Single(kernel.EffectReceipts);
        Assert.Equal(RunDriveStatus.WaitingForInput, driver.Drive().Status);
        Assert.Single(kernel.EffectReceipts);
        postEnabled = true;
        var result = driver.Drive();
        Assert.True(result.Status == RunDriveStatus.Completed, $"{result.Status}:{result.Reason}; {System.Text.Json.JsonSerializer.Serialize(driver.PlanExecution)}");
        Assert.Equal(3, consults); // initial + declared checkpoint + terminal assessment
        Assert.Equal(2, evaluations); // resuming a branch never re-evaluates or repeats its prefix
        Assert.Equal(2, kernel.EffectReceipts.Count);
        var attempts = driver.PlanExecution!.Attempts.Where(a => a.Status == PlanAttemptStatus.Verified).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.NotEqual(attempts[0].Position.ControlPath, attempts[1].Position.ControlPath);
        Assert.Equal(new[] { "primary", "list" }, attempts.Select(a => a.Step!.TargetDescriptor));
    }

    [Fact]
    public void UnknownPlanCondition_DoesNotSelectElseOrDispatch()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", T0) }),
            _ => ctx => ++consults == 1
                ? new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                {
                    new PlanItem.IfItem("owner.unknown", Array.Empty<PlanItem>(),
                        new PlanItem[] { new PlanItem.ActItem("switch", "primary", "toggle", "on") }),
                }, "unknown cannot imply false", "unknown-branch"))
                : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "not proven")),
            PlanContract(), evaluatePlanCondition: (_, _) => new PlanConditionResult(
                PolicyTruth.Unknown, "scope", "revision", Array.Empty<string>(), "missing evidence"));
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Contains("plan:condition-unknown", driver.PlanExecution!.InterruptionReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConditionalCheckpoint_RejectsTamperedControlPathAndMissingConditionEvidence(bool missingEvidence)
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx => ++consults == 1
                ? new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                {
                    new PlanItem.IfItem("owner.ready", new PlanItem[]
                    {
                        new PlanItem.CheckpointItem("review"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, Array.Empty<PlanItem>()),
                }, "guard exact branch position", "tamper-plan"))
                : ctx.PlanExecution?.Status == PlanExecutionStatus.Paused
                    ? new AgentDecision.ContinuePlan(ctx.DecisionId, ctx.PlanExecution.Position with { ControlPath = null })
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "unproven")),
            PlanContract(), evaluatePlanCondition: (_, _) => new PlanConditionResult(PolicyTruth.Satisfied,
                "scope", "r1", missingEvidence ? Array.Empty<string>() : new[] { "owner:evidence" }));
        var result = driver.Drive();
        Assert.Empty(kernel.EffectReceipts);
        if (missingEvidence)
        {
            Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
            Assert.Equal("plan:condition-unavailable", driver.PlanExecution!.InterruptionReason);
        }
        else
        {
            Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
            Assert.Equal("plan:resume-position-mismatch", result.Reason);
        }
    }

    [Fact]
    public void SelfReferencingControlBlock_IsRejectedBeforeEffects()
    {
        var body = new List<PlanItem>();
        body.Add(new PlanItem.RepeatItem(body, 1));
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx => new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(body, "invalid self-reference", "cyclic-plan")),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:invalid-control-structure", result.Reason);
        Assert.Empty(kernel.EffectReceipts);
    }

    // ==== 多 ActItem 串行执行 + 逐项验证 + 完成归档 ==========================

    /// <summary>
    /// 验收 2：Plan 含多个 ActItem 时逐个执行——每次最多一个 Act，每个
    /// Act 独立 dispatch（不变量 43：第一 Act 的 post-action 证据未到时
    /// 第二 Act 不 dispatch），全项 verified 后计划耗尽回决策边界，
    /// 步锚按计划项序归档（step:N.{item} 唯一）。
    /// </summary>
    [Fact]
    public void MultiActPlan_ExecutesOneActAtATime_EachActVerified_PlanExhaustionReconsults()
    {
        var postEnabled = false;
        var postPulls = 0;
        var consults = 0;
        var phases = new List<AgentDecisionPhase>();
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off+menuItem:list@visible", UIWorldDoubles.T0),
                })
                : postEnabled
                    ? new RunDriverInput.Observation(new[]
                    {
                        // 每次 post 拉取内容递增（幂等 claim 不产新 revision，
                        // 第二项验证需要新 revision 过 post-action-reconciled 门）；
                        // capture 时间必须严格晚于 dispatch（本测试 OkDriver = T0）
                        PostActionObservation(
                            $"switch:primary@on+menuItem:list@{(++postPulls == 1 ? "visible" : "visited")}",
                            T1.AddSeconds(postPulls)),
                        CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1.AddSeconds(postPulls))),
                        TypedCheckedClaim("checked", T1.AddSeconds(postPulls)),
                        TypedResourceClaim(T1.AddSeconds(postPulls)),
                    })
                    : null,
            consultFactory: _ => ctx =>
            {
                consults++;
                phases.Add(ctx.Phase);
                return consults == 1
                    ? PlanDecision(ctx.DecisionId,
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                        new PlanItem.ActItem("menuItem", "list", "tap", null))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "plan-done"));
            },
            PlanContract(),
            seedValue: "switch:primary@off+menuItem:list@visible");

        // 第一 Act：dispatch 恰一步，等待 post-action 证据（屏障前不得有第二 dispatch）
        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Equal("post-action-evidence", waiting.Reason);
        Assert.Equal(1, kernel.EffectReceipts.Count);
        Assert.Equal(1, consults);

        // 证据到达 → 第二 Act 才 dispatch 并验证 → 计划耗尽 → 再咨询 NoAction → Completed
        postEnabled = true;
        var completed = driver.Drive();
        Assert.Equal(RunDriveStatus.Completed, completed.Status);
        Assert.Equal(2, kernel.EffectReceipts.Count);
        Assert.Equal(2, consults);
        Assert.Equal(AgentDecisionPhase.InitialPlanning, phases[0]);
        Assert.Equal(AgentDecisionPhase.StepVerified, phases[1]);
        // 步锚按计划项序归档（决策 1 · 项 0/1）——完成锚可核验
        Assert.Equal(2, driver.CompletedSteps.Count);
        Assert.Equal(1, driver.CompletedSteps[0].DecisionN);
        Assert.Equal(0, driver.CompletedSteps[0].StepIndex);
        Assert.Equal(1, driver.CompletedSteps[1].DecisionN);
        Assert.Equal(1, driver.CompletedSteps[1].StepIndex);
    }

    [Fact]
    public void DynamicPlan_RecordsBlockedCandidate_AndExecutesNewCandidateThroughVerifiedActChain()
    {
        var projectCount = 0;
        var dispatchCount = 0;
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                    { UIWorldDoubles.Observation("switch:primary@off+menuItem:A@visible+menuItem:B@locked", UIWorldDoubles.T0) })
                : new RunDriverInput.Observation(new[]
                    {
                        PostActionObservation($"switch:primary@on+menuItem:C@visible+item:{dispatchCount}", T1.AddSeconds(dispatchCount)),
                        CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1.AddSeconds(dispatchCount))),
                        TypedCheckedClaim("checked", T1.AddSeconds(dispatchCount)),
                        TypedResourceClaim(T1.AddSeconds(dispatchCount)),
                    }),
            consultFactory: _ => ctx =>
            {
                consults++;
                if (ctx.BudgetReview is { Required: true } review)
                {
                    var assessment = new AgentProgressAssessment(
                        review.ReviewId, "progress", "continue records", "remaining records",
                        new[] { "candidate C current" }, new[] { "fact:C" }, Rationale: "A verified; C newly discovered");
                    return new AgentDecision.ContinuePlan(ctx.DecisionId, ctx.PlanExecution!.Position,
                        new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                            assessment, new BudgetRenewalRequest(2, 1, "continue C")));
                }
                if (consults > 1)
                {
                    // The dynamic scenario proves independent verified effects, but does not
                    // claim the external goal is complete without its own completion evidence.
                    return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "actions verified; goal remains unproven"));
                }
                return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(
                    new PlanItem[]
                    {
                        new PlanItem.ForEachItem("records", new PlanItem[]
                        {
                            new PlanItem.ActItem("$candidate", "$candidate", "tap", null),
                        }, MaxIterations: 3, ContinueOnBlocked: true),
                    }, "dynamic records", "records-plan", 1, "process records", 12));
            },
            PlanContract(consultations: 8, totalSteps: 2),
            seedValue: "switch:primary@off+menuItem:A@visible+menuItem:B@locked",
            projectPlanCandidates: source =>
            {
                Assert.Equal("records", source);
                projectCount++;
                var candidates = new List<PlanCandidate>
                {
                    new("a", "switch", "primary", PlanCandidateDisposition.Ready, new[] { "fact:A" }),
                    new("b", "menuItem", "B", PlanCandidateDisposition.Blocked, new[] { "fact:B" }, "locked by owner"),
                };
                if (projectCount >= 2)
                    candidates.Add(new("c", "menuItem", "C", PlanCandidateDisposition.Ready, new[] { "fact:C" }));
                return new PlanCandidateSet("records-scope", $"rev-{projectCount}", candidates);
            },
            guardStep: (step, _) =>
            {
                Assert.Contains(step.TargetDescriptor, new[] { "primary", "C" });
                dispatchCount++;
                return new PlanStepGuardResult(PlanStepGuardVerdict.Allow, "current owner facts", new[] { $"guard:{step.TargetDescriptor}" });
            },
            planPolicy: new AgentPlanPolicy
            {
                GoalDirectedContinuationEnabled = true,
                AuthorizedTotalSteps = 8,
                AuthorizedTotalConsultRounds = 12,
            });

        var first = driver.Drive();
        Assert.Equal(RunDriveStatus.Completed, first.Status);
        Assert.True(kernel.EffectReceipts.Count == 2,
            $"receipts={kernel.EffectReceipts.Count}; consults={consults}; dispatches={dispatchCount}; reason={first.Reason}; status={driver.PlanExecution?.Status}; attempts={string.Join(',', driver.PlanExecution?.Attempts.Select(a => a.Status) ?? Array.Empty<PlanAttemptStatus>())}");
        Assert.Equal(2, driver.CompletedSteps.Count);
        Assert.Equal(3, consults);
        Assert.Single(driver.RenewalsGranted.Where(record => record.Granted));
        Assert.Equal(2, dispatchCount);
        var execution = driver.PlanExecution;
        Assert.NotNull(execution);
        Assert.Equal(2, execution.Attempts.Count(attempt => attempt.Status == PlanAttemptStatus.Verified));
        Assert.True(execution.Attempts.Any(attempt => attempt.EvidenceRefs.Contains("fact:A")
            && attempt.EvidenceRefs.Contains("plan-source:records:revision:rev-1")),
            string.Join(";", execution.Attempts.Select(attempt => string.Join(",", attempt.EvidenceRefs))));
        Assert.True(execution.Attempts.Any(attempt => attempt.EvidenceRefs.Contains("fact:C")
            && attempt.EvidenceRefs.Contains("plan-source:records:revision:rev-3")),
            string.Join(";", execution.Attempts.Select(attempt => string.Join(",", attempt.EvidenceRefs))));
        Assert.Contains(execution.BlockedEvidence, item => item.CandidateKey == "b" && item.Reason == "locked by owner");
    }

    [Fact]
    public void Checkpoint_ContinuePlanResumesExactPosition_WithoutReplayingConsumedItem()
    {
        var postEnabled = false;
        var postPulls = 0;
        var consults = 0;
        var resumedPosition = (PlanExecutionPosition?)null;
        string? overviewStageExpectation = null;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) })
                : postEnabled
                    ? new RunDriverInput.Observation(new[]
                    {
                        PostActionObservation("switch:primary@on", T1.AddSeconds(++postPulls)),
                        CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1.AddSeconds(postPulls))),
                        TypedCheckedClaim("checked", T1.AddSeconds(postPulls)),
                        TypedResourceClaim(T1.AddSeconds(postPulls)),
                    })
                    : null,
            consultFactory: _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.CheckpointItem("review before action"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "checkpoint resume", "checkpoint-plan", Version: 1, StageExpectation: "turn switch on"));
                if (consults == 2)
                {
                    var position = ctx.PlanExecution!.Position;
                    resumedPosition = position;
                    overviewStageExpectation = ctx.ExecutionOverview?.PlanExecution?.StageExpectation;
                    return new AgentDecision.ContinuePlan(ctx.DecisionId, position);
                }
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "verified"));
            },
            PlanContract(),
            seedValue: "switch:primary@off");

        postEnabled = true;
        var result = driver.Drive();

        Assert.True(result.Status == RunDriveStatus.Completed, $"status={result.Status}; reason={result.Reason}");
        Assert.Equal(1, kernel.EffectReceipts.Count);
        Assert.Equal(1, driver.CompletedSteps.Count);
        Assert.Equal(3, consults);
        Assert.NotNull(resumedPosition);
        Assert.Equal(1, resumedPosition.ItemIndex);
        Assert.Equal(1, resumedPosition.WorkUsed);
        Assert.Equal("turn switch on", overviewStageExpectation);
    }

    [Fact]
    public void ContinuePlan_WithWrongPosition_FailsClosedBeforeEffect()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.CheckpointItem("pause"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "exact position", "position-plan", Version: 1, StageExpectation: "turn switch on"));
                var wrongPosition = ctx.PlanExecution!.Position with { ItemIndex = ctx.PlanExecution.Position.ItemIndex + 1 };
                return new AgentDecision.ContinuePlan(ctx.DecisionId, wrongPosition);
            },
            PlanContract(),
            seedValue: "switch:primary@off");

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:resume-position-mismatch", result.Reason);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(PlanExecutionStatus.Paused, driver.PlanExecution!.Status);
    }

    [Fact]
    public void NoActionAfterCheckpoint_AbandonsPlanWithoutResumingIt()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.CheckpointItem("pause"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "no implicit resume", "no-resume-plan", Version: 1, StageExpectation: "turn switch on"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stop here",
                    Renewal: new BudgetRenewalRequest(1, 0, "confirmed stopping point")));
            },
            PlanContract(totalSteps: 2),
            planPolicy: new AgentPlanPolicy
            {
                HardCapTotalSteps = 4,
                RenewalThresholdSteps = 2,
            },
            seedValue: "switch:primary@off");

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Single(driver.RenewalsGranted.Where(record => record.Granted));
        Assert.Equal(PlanExecutionStatus.Abandoned, driver.PlanExecution!.Status);
        Assert.Equal("plan:replaced-by-no-action", driver.PlanExecution.InterruptionReason);
    }

    [Fact]
    public void HigherPlanVersion_ReplacesPausedContentsWithoutDispatchingOldTail()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.CheckpointItem("revise"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "old content", "versioned-plan", Version: 1, StageExpectation: "old"));
                if (consults == 2)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ObserveItem(null),
                    }, "new content", "versioned-plan", Version: 2, StageExpectation: "new"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "no action"));
            },
            PlanContract(),
            seedValue: "switch:primary@off");

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(3, consults);
        Assert.Equal(2, driver.PlanExecution!.Position.Version);
        Assert.Equal(PlanExecutionStatus.Completed, driver.PlanExecution.Status);
    }

    [Fact]
    public void ReusingPlanVersion_FailsClosed()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.CheckpointItem("pause"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "version one", "strict-version-plan", Version: 1, StageExpectation: "turn switch on"));
                return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                {
                    new PlanItem.ObserveItem(null),
                }, "same version must not replace", "strict-version-plan", Version: 1, StageExpectation: "stale"));
            },
            PlanContract(),
            seedValue: "switch:primary@off");

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:version-must-increase", result.Reason);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(PlanExecutionStatus.Paused, driver.PlanExecution!.Status);
    }

    [Fact]
    public void LocalWorkExhaustion_CannotBeClearedByContinuePlan()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ObserveItem(null),
                        new PlanItem.ObserveItem(null),
                    }, "bounded observations", "work-limit-plan", Version: 1,
                        StageExpectation: "observe twice", MaxLocalOperations: 1));
                return new AgentDecision.ContinuePlan(ctx.DecisionId, ctx.PlanExecution!.Position);
            },
            PlanContract(),
            seedValue: "switch:primary@off");

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:local-work-exhausted-requires-revision", result.Reason);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(1, driver.PlanExecution!.Position.WorkUsed);
        Assert.Equal(PlanExecutionStatus.Paused, driver.PlanExecution.Status);
    }

    [Fact]
    public void UnknownCandidate_IsNotSkippedAsBlocked()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ForEachItem("records", new PlanItem[]
                        {
                            new PlanItem.ActItem("$candidate", "$candidate", "tap", null),
                        }, MaxIterations: 1, ContinueOnBlocked: true),
                    }, "unknown must stop", "unknown-candidate-plan", Version: 1,
                        StageExpectation: "process record", MaxLocalOperations: 4));
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stop after unknown"));
            },
            PlanContract(),
            seedValue: "switch:primary@off",
            projectPlanCandidates: _ => new PlanCandidateSet("records", "r1", new[]
            {
                new PlanCandidate("record-1", "switch", "primary", PlanCandidateDisposition.Unknown,
                    new[] { "record:evidence:1" }, "status unavailable"),
            }));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Empty(driver.PlanExecution!.BlockedEvidence);
        Assert.Contains("plan:candidate-unknown:record-1", driver.PlanExecution.InterruptionReason);
    }

    [Fact]
    public void GuardUnknown_StopsPlanBeforeNewEffect()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "guard unknown", "guard-unknown-plan", Version: 1, StageExpectation: "turn switch on"));
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stop on unknown"));
            },
            PlanContract(),
            seedValue: "switch:primary@off",
            guardStep: (_, _) => new PlanStepGuardResult(PlanStepGuardVerdict.Unknown, "current state unavailable"));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Contains(driver.PlanExecution!.Attempts, attempt => attempt.Status == PlanAttemptStatus.UnknownOutcome);
    }

    [Fact]
    public void UnconfirmedDelivery_ContinuesVerificationWithoutRedispatch()
    {
        var (kernel, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) })
                : null,
            _ => ctx => PlanDecision(ctx.DecisionId,
                new PlanItem.ActItem("switch", "primary", "toggle", "on")),
            PlanContract(),
            seedValue: "switch:primary@off",
            effectDriver: new UnknownDriver());

        var first = driver.Drive();
        Assert.Equal(RunDriveStatus.UnconfirmedDelivery, first.Status);
        Assert.Single(kernel.EffectReceipts);
        Assert.Contains(driver.PlanExecution!.Attempts, attempt => attempt.Status == PlanAttemptStatus.Unconfirmed);

        var second = driver.Drive();
        var third = driver.Drive();

        Assert.Equal(RunDriveStatus.WaitingForInput, second.Status);
        Assert.Equal(RunDriveStatus.WaitingForInput, third.Status);
        Assert.Single(kernel.EffectReceipts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecordBuyer_UsesRecordStateProjectionAndCurrentGuardThroughSamePlanDriver(bool conditional)
    {
        var recordStates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["R17"] = "EvidencePending",
            ["R22"] = "OwnerBlocked",
        };
        var projectCalls = 0;
        var guardCalls = 0;
        var consultCount = 0;
        var observeCalls = 0;
        var postActionCalls = 0;
        UniKernel? observedKernel = null;
        const string initialRecords = "record:R17@EvidencePending+record:R22@OwnerBlocked";
        var contract = new ExecutionContract(
            "records-v1",
            "verify records after collecting missing evidence",
            new HashSet<string>
            {
                UIWorldDoubles.Observed,
                "record:R17.supplement",
                "record:R17.status",
            },
            new HashSet<string> { "verify-record" },
            new HashSet<string>(),
            new[] { "record verification status" },
            new[]
            {
                new RunObligation(
                    "record-17-verified",
                    RunObligationKind.Objective,
                    "record:R17.status",
                    "Verified",
                    true),
            },
            MaxConsultations: 8,
            MaxTotalSteps: 4,
            TaskInstanceId: "records-task-17",
            ProductSessionId: "records-session-17",
            ContractRef: "records-contract-17");
        var (kernel, driver) = ComposePlanWorld(
            expected =>
            {
                if (expected.Context == ObservationContext.PostActionEffectFlow)
                {
                    postActionCalls++;
                    // The effect callback only records that delivery happened.
                    // The record owner independently reports the terminal state here.
                    recordStates["R17"] = "Verified";
                    var time = T1.AddSeconds(postActionCalls);
                    return new RunDriverInput.Observation(new[]
                    {
                        PostActionObservation("record:R17@Verified+record:R22@OwnerBlocked", time),
                        new ObservationProposal(
                            new ObservationClaim("record:R17.status", "Verified"),
                            IngressKind.Observation,
                            ObservationContext.PostActionEffectFlow,
                            new Provenance("record.owner", time, "scope:record:R17.status",
                                new[] { "record-store:R17:verification-result" })),
                    });
                }

                if (expected.Subjects?.Contains("record:R17.supplement", StringComparer.Ordinal) == true)
                {
                    observeCalls++;
                    recordStates["R17"] = "EvidenceCollected";
                    var time = T0.AddSeconds(10 + observeCalls);
                    return new RunDriverInput.Observation(new[]
                    {
                        new ObservationProposal(
                            new ObservationClaim("record:R17.supplement", "OwnerReceiptAttached"),
                            IngressKind.Observation,
                            ObservationContext.External,
                            new Provenance("record.owner", time, "scope:record:R17.supplement",
                                new[] { "record-store:R17:owner-receipt" })),
                    });
                }

                return new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation(initialRecords, UIWorldDoubles.T0),
                });
            },
            _ => ctx =>
            {
                consultCount++;
                if (consultCount == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ForEachItem("verification-records", new PlanItem[]
                        {
                            conditional
                                ? new PlanItem.IfItem("record.needs-receipt",
                                    new PlanItem[] { new PlanItem.ObserveItem("record:R17.supplement") }, Array.Empty<PlanItem>())
                                : new PlanItem.ObserveItem("record:R17.supplement"),
                            new PlanItem.ActItem("$candidate", "$candidate", "verify-record", null),
                        }, MaxIterations: 3, ContinueOnBlocked: true),
                    }, "collect and verify record evidence", "record-buyer-plan", Version: 1,
                        StageExpectation: "R17 verified; R22 owner lock retained", MaxLocalOperations: conditional ? 16 : 8));
                var verifiedAttempt = ctx.PlanExecution?.Attempts.SingleOrDefault(
                    attempt => attempt.Status == PlanAttemptStatus.Verified);
                var completion = verifiedAttempt is null
                    ? null
                    : new CompletionEvidence("record owner result observed",
                        new[] { $"plan-attempt:{verifiedAttempt.AttemptId}" });
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId,
                    "record verified", completion));
            },
            contract,
            seedValue: initialRecords,
            projectPlanCandidates: source =>
            {
                Assert.Equal("verification-records", source);
                projectCalls++;
                var candidates = new List<PlanCandidate>();
                if (recordStates["R17"] != "Verified")
                    candidates.Add(new PlanCandidate("record-17", "record", "R17",
                        PlanCandidateDisposition.Ready, new[] { "record-ledger:R17:evidence-pending" }));
                candidates.Add(new PlanCandidate("record-22", "record", "R22",
                    PlanCandidateDisposition.Blocked,
                    new[] { "owner-proof:R22:lease-locked" }, "record is locked by its owner"));
                return new PlanCandidateSet("record-ledger:task-17",
                    $"r17:{recordStates["R17"]}|r22:{recordStates["R22"]}", candidates);
            },
            evaluatePlanCondition: conditional ? (name, candidate) =>
            {
                Assert.Equal("record.needs-receipt", name);
                Assert.Equal("record-17", candidate!.Key);
                return new PlanConditionResult(recordStates["R17"] == "EvidencePending" ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                    "record-ledger:task-17", recordStates["R17"], new[] { "record-owner:R17:receipt-state" });
            } : null,
            guardStep: (step, _) =>
            {
                guardCalls++;
                Assert.Equal("record", step.TargetRole);
                Assert.Equal("R17", step.TargetDescriptor);
                Assert.Equal("EvidenceCollected", recordStates["R17"]);
                var currentEvidenceIds = observedKernel?.CurrentBelief?.EvidenceBasis?.ToArray() ?? Array.Empty<string>();
                return new PlanStepGuardResult(PlanStepGuardVerdict.Allow,
                    "record receipt is present and status remains actionable",
                    new[] { "record-ledger:R17:state:EvidenceCollected" }.Concat(currentEvidenceIds).ToArray());
            },
            effectDriver: new CallbackDriver(_ =>
            {
                recordStates["R17"] = "EffectDispatched";
            }));
        observedKernel = kernel;

        var result = driver.Drive();

        Assert.True(result.Status == RunDriveStatus.Completed, result.Reason);
        Assert.Single(kernel.EffectReceipts);
        Assert.Equal("Verified", recordStates["R17"]);
        Assert.Equal("Verified", kernel.CurrentBelief!.WorldState["record:R17.status"].Value);
        Assert.Equal("OwnerBlocked", recordStates["R22"]);
        Assert.Equal(1, guardCalls);
        Assert.Equal(1, observeCalls);
        Assert.Equal(1, postActionCalls);
        Assert.True(projectCalls >= 2);
        var attempt = Assert.Single(driver.PlanExecution!.Attempts);
        var receipt = Assert.Single(kernel.EffectReceipts);
        Assert.Equal(PlanAttemptStatus.Verified, attempt.Status);
        Assert.Equal(receipt.ReceiptId, attempt.ReceiptId);
        Assert.Contains("record-ledger:R17:evidence-pending", attempt.EvidenceRefs);
        Assert.Contains("record-ledger:R17:state:EvidenceCollected", attempt.EvidenceRefs);
        Assert.Contains(driver.PlanExecution.BlockedEvidence,
            blocked => blocked.CandidateKey == "record-22"
                && blocked.Reason == "record is locked by its owner"
                && blocked.EvidenceRefs.Contains("owner-proof:R22:lease-locked"));
    }

    [Theory]
    [InlineData(BudgetReviewOutcome.Replan)]
    [InlineData(BudgetReviewOutcome.SubmitCompletion)]
    [InlineData(BudgetReviewOutcome.CollectEvidence)]
    public void ContinuePlan_RejectsReviewChoicesThatConflictWithResuming(
        BudgetReviewOutcome outcome)
    {
        var consults = 0;
        var postPulls = 0;
        var (kernel, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[] { UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0) })
                : new RunDriverInput.Observation(new[]
                {
                    PostActionObservation("switch:primary@on", T1.AddSeconds(++postPulls)),
                    CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1.AddSeconds(postPulls))),
                    TypedCheckedClaim("checked", T1.AddSeconds(postPulls)),
                    TypedResourceClaim(T1.AddSeconds(postPulls)),
                }),
            _ => ctx =>
            {
                consults++;
                if (ctx.BudgetReview is not { Required: true } review)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "review must choose a different continuation", "review-choice-plan", Version: 1,
                        StageExpectation: "switch remains on", MaxLocalOperations: 5));

                var assessment = new AgentProgressAssessment(review.ReviewId, "progress", "review plan",
                    "remaining work", new[] { "switch verified" }, new[] { "fact:switch" },
                    Rationale: "review decision is not a resume authorization");
                return new AgentDecision.ContinuePlan(ctx.DecisionId, ctx.PlanExecution!.Position,
                    new BudgetReviewResponse(review.ReviewId, outcome, assessment));
            },
            PlanContract(consultations: 8, totalSteps: 2),
            seedValue: "switch:primary@off",
            planPolicy: new AgentPlanPolicy
            {
                GoalDirectedContinuationEnabled = true,
                AuthorizedTotalSteps = 8,
                AuthorizedTotalConsultRounds = 12,
            });

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:review-outcome-conflicts-with-resume", result.Reason);
        Assert.Single(kernel.EffectReceipts);
        Assert.Single(driver.CompletedSteps);
        Assert.Equal(PlanExecutionStatus.Paused, driver.PlanExecution!.Status);
    }

    // ==== V7 机械校验（malformed / unknown / 超预算 → 零 Effect）============

    [Fact]
    public void EmptyPlan_FailsClosed_ZeroEffects()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[]
            {
                UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
            }),
            _ => ctx => PlanDecision(ctx.DecisionId),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:empty", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void OversizedPlan_FailsClosed_ZeroEffects()
    {
        var items = Enumerable.Repeat(new PlanItem.ObserveItem(null), 17).ToArray();
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[]
            {
                UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
            }),
            _ => ctx => PlanDecision(ctx.DecisionId, items),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:too-many-items", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void UnknownPlanItemDerivative_FailsClosed_ZeroEffects()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => ctx => PlanDecision(ctx.DecisionId, new RoguePlanItem()),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:unknown-item", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void PlanActItem_EffectClassOutsideContract_FailsClosed()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => ctx => PlanDecision(ctx.DecisionId,
                new PlanItem.ActItem("switch", "primary", "swipe", null)),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:effect-class-not-allowed", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void PlanActItem_UnsupportedDesiredState_FailsClosed()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => ctx => PlanDecision(ctx.DecisionId,
                new PlanItem.ActItem("switch", "primary", "toggle", "Wi-Fi settings screen visible")),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:unsupported-desired-state", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void PlanActItem_MissingTargetRole_FailsClosed()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => ctx => PlanDecision(ctx.DecisionId,
                new PlanItem.ActItem(" ", "primary", "toggle", null)),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("plan:missing-target-role", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void PlanDecisionIdMismatch_FailsClosed_ZeroEffects()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => _ => PlanDecision("decision-forged",
                new PlanItem.ActItem("switch", "primary", "toggle", "on")),
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("correlation-mismatch", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    [Fact]
    public void OverBudgetPlan_MoreActItemsThanSteps_FailsClosed()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => ctx => PlanDecision(ctx.DecisionId,
                new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                new PlanItem.ActItem("menuItem", "list", "tap", null)),
            PlanContract(totalSteps: 1));
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("budget-exceeded:steps", result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    // ==== ObserveItem：零 Effect，不单独宣告完成 ============================

    /// <summary>
    /// 验收 3：Observe-only 计划——观察经 P2 入证（external 拉取发生），
    /// 零 dispatch；计划耗尽后任务并未被「完成」（mandatory 义务未满足
    /// → 再咨询 NoAction → TerminalNotProven，非 Completed）。
    /// </summary>
    [Fact]
    public void ObserveOnlyPlan_ZeroEffects_DoesNotCompleteAlone()
    {
        var externalPulls = 0;
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected =>
            {
                if (expected.Context == ObservationContext.External)
                {
                    externalPulls++;
                    return new RunDriverInput.Observation(new[]
                    {
                        UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                    });
                }
                return null;
            },
            consultFactory: _ => ctx =>
            {
                consults++;
                return consults == 1
                    ? PlanDecision(ctx.DecisionId, new PlanItem.ObserveItem(null))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "nothing-proven"));
            },
            PlanContract());
        var result = driver.Drive();
        Assert.True(result.Status == RunDriveStatus.TerminalNotProven, result.Reason);
        Assert.Equal(0, kernel.EffectReceipts.Count);
        Assert.Equal(2, externalPulls); // initial + plan-observe（P2 入证发生）
        Assert.Equal(2, consults);
        Assert.False(kernel.IsRunTerminal);
    }

    // ==== 失效条件：验证失败 / 冲突 → 废弃剩余计划 ==========================

    /// <summary>
    /// 冻结决策 11：验证失败废弃尚未执行的计划——第一 Act 验证失败
    /// （post 证据无新 revision）→ 剩余 ActItem 不再 dispatch；再咨询
    /// 携带 VerificationFailed 相位与原因。
    /// </summary>
    [Fact]
    public void VerificationFailure_VoidsRemainingPlan_NoSecondDispatch()
    {
        var consults = 0;
        AgentDecisionPhase? secondPhase = null;
        string? secondFailure = null;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                // post 证据与 seed 同值：无新 revision → 第一 Act 验证失败
                : new RunDriverInput.Observation(new[]
                {
                    PostActionObservation("switch:primary@off", UIWorldDoubles.T1),
                }),
            consultFactory: _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return PlanDecision(ctx.DecisionId,
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                        new PlanItem.ActItem("menuItem", "list", "tap", null));
                secondPhase = ctx.Phase;
                secondFailure = ctx.FailureReason;
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "plan-voided"));
            },
            PlanContract());
        var result = driver.Drive();
        // 第二 Act 从未 dispatch；再咨询看到失败上下文；终局如实未证
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(1, kernel.EffectReceipts.Count);
        Assert.Equal(2, consults);
        Assert.Equal(AgentDecisionPhase.VerificationFailed, secondPhase);
        Assert.False(string.IsNullOrEmpty(secondFailure));
        Assert.Empty(driver.CompletedSteps);
    }

    /// <summary>
    /// 冻结决策 11：世界存在权威域悬案（冲突）时 ActItem 不物化——
    /// 剩余计划废弃，回决策边界（StepRejected + plan:world-conflict），
    /// 零 Effect。
    /// </summary>
    [Fact]
    public void WorldConflict_VoidsPlanBeforeAct_ZeroEffects()
    {
        var conflictSubject = "switch:primary.state";
        var consults = 0;
        AgentDecisionPhase? secondPhase = null;
        string? secondFailure = null;
        RunDriverInput Observation() => new RunDriverInput.Observation(new ObservationProposal[]
        {
            UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
            // 冲突对：同 subject 异值 → 显式 Conflict（权威域悬案）
            UIWorldDoubles.SubjectObservation(conflictSubject, "on", UIWorldDoubles.T0),
            UIWorldDoubles.SubjectObservation(conflictSubject, "off", UIWorldDoubles.T0),
        });
        var contract = new ExecutionContract(
            "plan-v1", "traverse-with-plan",
            new HashSet<string> { UIWorldDoubles.Observed, "ui.node.cap-x#0.resource_id", conflictSubject },
            new HashSet<string> { "toggle", "tap" }, new HashSet<string>(),
            new[] { "objective" },
            new[]
            {
                new RunObligation(
                    "obl-switch", RunObligationKind.Objective, "ui.role.switch.checked", "checked", true,
                    EntityScope: new TargetDescriptor("switch", "primary")),
            },
            TaskInstanceId: "task-plan-inline", ProductSessionId: "session-plan-inline",
            ContractRef: "contract-plan-inline");
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External ? Observation() : null,
            consultFactory: _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return PlanDecision(ctx.DecisionId,
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"));
                secondPhase = ctx.Phase;
                secondFailure = ctx.FailureReason;
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "conflict-voided"));
            },
            contract);
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, kernel.EffectReceipts.Count); // 冲突在物化前废弃 → 零 dispatch
        Assert.Equal(2, consults);
        Assert.Equal(AgentDecisionPhase.StepRejected, secondPhase);
        Assert.Equal("plan:world-conflict", secondFailure);
    }

    // ==== ControlItem：Reobserve / Replan / Stop 有界转移 ====================

    /// <summary>
    /// 冻结决策 12：Replan 消耗当前 Run 的全局咨询预算——持续 Replan 计划
    /// 在预算耗尽后 AgentDecisionFailed（consult-budget-exhausted），
    /// 全程零 Effect、无新预算。
    /// </summary>
    [Fact]
    public void ReplanLoop_ConsumesGlobalBudget_BoundedFailClosed()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
            _ => ctx =>
            {
                consults++;
                return PlanDecision(ctx.DecisionId, new PlanItem.ControlItem(PlanControlKind.Replan, "loop"));
            },
            PlanContract(consultations: 3));
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("consult-budget-exhausted", result.Reason);
        Assert.Equal(3, consults);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    /// <summary>
    /// Stop：当前项验证通过后废弃剩余计划（Stop 及其后所有项），零新
    /// Effect、不再消耗咨询轮次，直接进终局评估——终局由既有层1 语义
    /// 如实判定（本场景 typed 证据已满足义务 → Completed；不伪造）。
    /// </summary>
    [Fact]
    public void PlanStop_AfterVerifiedAct_VoidsRemaining_ZeroNewEffects_GoesTerminalEvaluation()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : new RunDriverInput.Observation(new[]
                {
                    PostActionObservation("switch:primary@on", T1),
                    CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1)),
                    TypedCheckedClaim("checked", T1),
                    TypedResourceClaim(T1),
                }),
            consultFactory: _ => ctx =>
            {
                consults++;
                return PlanDecision(ctx.DecisionId,
                    new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    new PlanItem.ControlItem(PlanControlKind.Stop, "safe-stop"));
            },
            PlanContract());
        var result = driver.Drive();
        // 第一项正常 dispatch+verified（receipts=1）；Stop 到达时废弃剩余计划，
        // 零新 Effect、不再咨询，直接终局评估（层1 如实判定）
        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(1, kernel.EffectReceipts.Count);
        Assert.Equal(1, consults); // Stop 不回决策边界——无第二次咨询
        Assert.Equal(1, driver.CompletedSteps.Count); // 已验证项的锚保留
        Assert.True(kernel.IsRunTerminal);
    }

    /// <summary>
    /// Reobserve：废弃剩余计划 + 拉取一轮外部观察（external 拉取计数 +1）
    /// 后回决策边界再咨询；零 Effect；有界（预算内恰一次转移）。
    /// </summary>
    [Fact]
    public void PlanReobserve_PullsExternalObservation_ThenReconsults()
    {
        var externalPulls = 0;
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected =>
            {
                if (expected.Context == ObservationContext.External)
                {
                    externalPulls++;
                    return new RunDriverInput.Observation(new[]
                    {
                        UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                    });
                }
                return null;
            },
            consultFactory: _ => ctx =>
            {
                consults++;
                return consults == 1
                    ? PlanDecision(ctx.DecisionId, new PlanItem.ControlItem(PlanControlKind.Reobserve, "unclear"))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "after-reobserve"));
            },
            PlanContract());
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(2, externalPulls); // initial + plan-reobserve
        Assert.Equal(2, consults);
        Assert.Equal(0, kernel.EffectReceipts.Count);
    }

    // ==== ActItem 目标已满足：E4 语义（零 dispatch 消费该项）===============

    /// <summary>
    /// ActItem 目标已满足（seed 状态 = 期望终态）→ Control 发 plain
    /// Observe → 计划消费该项继续（零 dispatch），耗尽回决策边界。
    /// </summary>
    [Fact]
    public void ActItemTargetAlreadySatisfied_AdvancesWithoutDispatch()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@on", UIWorldDoubles.T0),
                })
                : null,
            consultFactory: _ => ctx =>
            {
                consults++;
                return consults == 1
                    ? PlanDecision(ctx.DecisionId, new PlanItem.ActItem("switch", "primary", "toggle", "on"))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "already-satisfied"));
            },
            PlanContract(),
            seedValue: "switch:primary@on");
        var result = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, kernel.EffectReceipts.Count);
        Assert.Equal(2, consults);
    }

    [Fact]
    public void ForEachActAlreadySatisfied_AdvancesBodyAndFinalizesAttempt()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@on", UIWorldDoubles.T0),
                })
                : null,
            _ => ctx =>
            {
                consults++;
                return consults == 1
                    ? new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ForEachItem("switches", new PlanItem[]
                        {
                            new PlanItem.ActItem("$candidate", "$candidate", "toggle", "on"),
                        }, MaxIterations: 1),
                    }, "consume already-satisfied switch", "foreach-e4", Version: 1,
                        StageExpectation: "switch already on", MaxLocalOperations: 4))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "finished"));
            },
            PlanContract(),
            seedValue: "switch:primary@on",
            projectPlanCandidates: _ => new PlanCandidateSet("switches", "r1", new[]
            {
                new PlanCandidate("primary", "switch", "primary", PlanCandidateDisposition.Ready,
                    new[] { "switch-source:primary" }),
            }));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        var attempt = Assert.Single(driver.PlanExecution!.Attempts);
        Assert.Equal(PlanAttemptStatus.AlreadySatisfied, attempt.Status);
        Assert.Null(attempt.ReceiptId);
        Assert.Contains("switch-source:primary", attempt.EvidenceRefs);
        Assert.Equal(2, consults);
    }

    [Fact]
    public void GateRejection_UpdatesTheStartedPlanAttemptInsteadOfAppendingAnother()
    {
        var contract = PlanContract() with { ForbiddenEffects = new HashSet<string> { "toggle" } };
        AgentDecisionContext? followupPlanContext = null;
        var consults = 0;
        var guardCalls = 0;
        var (kernel, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : null,
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "trigger gate rejection", "gate-rejection", Version: 1));
                if (consults == 2)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "inspect retained history", "after-gate-rejection", Version: 1));
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stop after rejection"));
            },
            contract,
            seedValue: "switch:primary@off",
            guardStep: (_, ctx) =>
            {
                guardCalls++;
                followupPlanContext = ctx;
                return guardCalls == 1
                    ? new PlanStepGuardResult(PlanStepGuardVerdict.Allow, "allow through to gate")
                    : new PlanStepGuardResult(PlanStepGuardVerdict.AlreadySatisfied, "inspect without dispatch");
            });

        var result = driver.Drive();

        Assert.True(result.Status == RunDriveStatus.TerminalNotProven, result.Reason);
        Assert.Empty(kernel.EffectReceipts);
        Assert.NotNull(followupPlanContext?.PlanExecution);
        var attempt = Assert.Single(followupPlanContext!.PlanExecution!.Attempts);
        Assert.Equal(PlanAttemptStatus.Rejected, attempt.Status);
        Assert.Null(attempt.ReceiptId);
        Assert.Equal("gate-rejection", attempt.Position.PlanId);
    }

    [Fact]
    public void OrdinaryActGuardRejection_DoesNotLeakAttemptIntoNextPlanHistory()
    {
        AgentDecisionContext? ordinaryGuardContext = null;
        AgentDecisionContext? planGuardContext = null;
        var consults = 0;
        var guardCalls = 0;
        var (kernel, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", T1),
                }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Act(new AgentActionProposal(ctx.DecisionId,
                        new[] { new AgentActionStep("switch", "primary", "toggle", "on") }, "ordinary act"));
                if (consults == 2)
                {
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "start a clean plan history", "clean-history", Version: 1));
                }
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "done"));
            },
            PlanContract(),
            seedValue: "switch:primary@off",
            guardStep: (_, ctx) =>
            {
                if (guardCalls++ == 0)
                {
                    ordinaryGuardContext = ctx;
                    return new PlanStepGuardResult(PlanStepGuardVerdict.Reject, "ordinary act blocked");
                }
                planGuardContext = ctx;
                return new PlanStepGuardResult(PlanStepGuardVerdict.AlreadySatisfied, "inspect clean plan history");
            });

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Null(ordinaryGuardContext?.PlanExecution);
        Assert.NotNull(planGuardContext?.PlanExecution);
        Assert.Empty(planGuardContext!.PlanExecution!.Attempts);
        Assert.Equal(3, consults);
    }

    [Fact]
    public void FocusedControlRetries_KeepOneAttemptAndFinalizeItOnExhaustion()
    {
        var focus = new ControlDecision(ControlIntentKind.Observe, null, "switch:primary");
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[]
            {
                UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
            }),
            _ => ctx => PlanDecision(ctx.DecisionId,
                new PlanItem.ActItem("switch", "primary", "toggle", "on")),
            PlanContract(),
            seedValue: "switch:primary@off",
            controlPolicy: new SequenceControlPolicy(focus, focus, focus, focus));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.GroundingFailed, result.Status);
        Assert.Equal("focused-reobserve-exhausted", result.Reason);
        Assert.Empty(kernel.EffectReceipts);
        var attempt = Assert.Single(driver.PlanExecution!.Attempts);
        Assert.Equal(PlanAttemptStatus.Rejected, attempt.Status);
        Assert.Null(attempt.ReceiptId);
    }

    [Fact]
    public void FocusedRetryGuardRejection_UpdatesOriginalStartedAttempt()
    {
        AgentDecisionContext? followupContext = null;
        var consults = 0;
        var guardCalls = 0;
        var focus = new ControlDecision(ControlIntentKind.Observe, null, "switch:primary");
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[]
            {
                UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
            }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "observe then reject", "focus-guard-reject", Version: 1));
                followupContext = ctx;
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stop"));
            },
            PlanContract(),
            seedValue: "switch:primary@off",
            controlPolicy: new SequenceControlPolicy(focus),
            guardStep: (_, _) => ++guardCalls == 1
                ? new PlanStepGuardResult(PlanStepGuardVerdict.Allow, "initially admissible")
                : new PlanStepGuardResult(PlanStepGuardVerdict.Reject, "fresh guard rejected after focus"));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.Equal(2, guardCalls);
        var attempt = Assert.Single(followupContext!.PlanExecution!.Attempts);
        Assert.Equal(PlanAttemptStatus.Rejected, attempt.Status);
        Assert.Equal("fresh guard rejected after focus", attempt.Reason);
        Assert.Null(attempt.ReceiptId);
    }

    [Fact]
    public void MissingRootAfterBegin_FinalizesTheAttemptAsRejected()
    {
        AgentDecisionContext? followupContext = null;
        var consults = 0;
        var baseContract = PlanContract();
        var contract = baseContract with
        {
            Scope = new HashSet<string>(baseContract.Scope.Append(WorldModel.CurrentContainerSubject), StringComparer.Ordinal),
        };
        var (kernel, driver) = ComposePlanWorld(
            _ => new RunDriverInput.Observation(new[]
            {
                UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0.AddSeconds(1)),
                new ObservationProposal(
                    new ObservationClaim(WorldModel.CurrentContainerSubject, "container:unknown-a"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("container.owner.a", UIWorldDoubles.T0, "scope:ui.container.current", new[] { "container-owner:a" })),
                new ObservationProposal(
                    new ObservationClaim(WorldModel.CurrentContainerSubject, "container:unknown-b"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("container.owner.b", UIWorldDoubles.T0.AddSeconds(2), "scope:ui.container.current", new[] { "container-owner:b" })),
            }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ActItem("switch", "primary", "toggle", "on"),
                    }, "missing root regression", "missing-root", Version: 1));
                followupContext = ctx;
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stop"));
            },
            contract,
            seedValue: "switch:primary@off",
            associationStrategy: new SeparateEveryObservationAssociationStrategy());

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Empty(kernel.EffectReceipts);
        Assert.True(kernel.CurrentBelief!.Containers!.Count > 1,
            $"association fixture created {kernel.CurrentBelief.Containers.Count} container(s)");
        var attempt = Assert.Single(followupContext!.PlanExecution!.Attempts);
        Assert.Equal(PlanAttemptStatus.Rejected, attempt.Status);
        Assert.Equal("no-single-root-container", attempt.Reason);
        Assert.Null(attempt.ReceiptId);
    }

    [Fact]
    public void AgentContextCompactsAttemptEvidenceRefs_WhileDriverAuditRemainsComplete()
    {
        var consults = 0;
        AgentDecisionContext? completionContext = null;
        var sourceRefs = Enumerable.Range(0, 24).Select(index => $"candidate-evidence:{index:D2}").ToArray();
        var guardRefs = Enumerable.Range(0, 24).Select(index => $"guard-evidence:{index:D2}").ToArray();
        var (_, driver) = ComposePlanWorld(
            expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : new RunDriverInput.Observation(new[]
                {
                    PostActionObservation("switch:primary@on", T1),
                    CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1)),
                    TypedCheckedClaim("checked", T1),
                    TypedResourceClaim(T1),
                }),
            _ => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                    {
                        new PlanItem.ForEachItem("switches", new PlanItem[]
                        {
                            new PlanItem.ActItem("$candidate", "$candidate", "toggle", "on"),
                        }, MaxIterations: 1),
                    }, "toggle the selected switch", "compact-context", Version: 1,
                        StageExpectation: "switch is on", MaxLocalOperations: 4));

                completionContext = ctx;
                var attempt = Assert.Single(ctx.PlanExecution!.Attempts,
                    item => item.Status == PlanAttemptStatus.Verified);
                return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId,
                    "attest the verified plan action",
                    new CompletionEvidence("switch is on", new[] { $"plan-attempt:{attempt.AttemptId}" })));
            },
            PlanContract(),
            seedValue: "switch:primary@off",
            projectPlanCandidates: _ => new PlanCandidateSet("switches", "rev-1", new[]
            {
                new PlanCandidate("primary", "switch", "primary", PlanCandidateDisposition.Ready, sourceRefs),
            }),
            guardStep: (_, _) => new PlanStepGuardResult(PlanStepGuardVerdict.Allow,
                "current switch state permits toggle", guardRefs));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        var auditAttempt = Assert.Single(driver.PlanExecution!.Attempts,
            item => item.Status == PlanAttemptStatus.Verified);
        Assert.All(sourceRefs.Concat(guardRefs), evidenceRef => Assert.Contains(evidenceRef, auditAttempt.EvidenceRefs));
        var contextAttempt = Assert.Single(completionContext!.PlanExecution!.Attempts,
            item => item.Status == PlanAttemptStatus.Verified);
        Assert.Equal(new[] { $"plan-attempt:{auditAttempt.AttemptId}" }, contextAttempt.EvidenceRefs);
        Assert.DoesNotContain(sourceRefs.Concat(guardRefs), evidenceRef => contextAttempt.EvidenceRefs.Contains(evidenceRef));
    }

    [Fact]
    public void ForEachCompletionRequiresVerifiedAttemptAnchor_NotLinearStepAnchor()
    {
        (RunDriveResult Result, KernelRunDriver Driver) RunWithAnchor(bool usePlanAttempt)
        {
            var consults = 0;
            var (_, driver) = ComposePlanWorld(
                expected => expected.Context == ObservationContext.External
                    ? new RunDriverInput.Observation(new[]
                    {
                        UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                    })
                    : new RunDriverInput.Observation(new[]
                    {
                        PostActionObservation("switch:primary@on", T1),
                        CheckedGuardFixture.Checkable(TypedCheckedClaim("checked", T1)),
                        TypedCheckedClaim("checked", T1),
                        TypedResourceClaim(T1),
                    }),
                _ => ctx =>
                {
                    consults++;
                    if (consults == 1)
                        return new AgentDecision.Plan(ctx.DecisionId, new AgentPlanProposal(new PlanItem[]
                        {
                            new PlanItem.ForEachItem("switches", new PlanItem[]
                            {
                                new PlanItem.ActItem("$candidate", "$candidate", "toggle", "on"),
                            }, MaxIterations: 1),
                        }, "verify switch candidate", "foreach-anchor", Version: 1,
                            StageExpectation: "switch on", MaxLocalOperations: 4));

                    var verifiedAttempt = Assert.Single(ctx.PlanExecution!.Attempts,
                        attempt => attempt.Status == PlanAttemptStatus.Verified);
                    var anchor = usePlanAttempt
                        ? $"plan-attempt:{verifiedAttempt.AttemptId}"
                        : "step:1.0";
                    return new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "attest exact action",
                        new CompletionEvidence("foreach-anchor", new[] { anchor })));
                },
                PlanContract(),
                seedValue: "switch:primary@off",
                projectPlanCandidates: _ => new PlanCandidateSet("switches", "r1", new[]
                {
                    new PlanCandidate("primary", "switch", "primary", PlanCandidateDisposition.Ready,
                        new[] { "switch-source:primary" }),
                }));
            return (driver.Drive(), driver);
        }

        var falseStepAnchor = RunWithAnchor(usePlanAttempt: false);
        Assert.Equal(RunDriveStatus.AwaitingCompletionAdjudication, falseStepAnchor.Result.Status);
        var failedAnchor = Assert.Single(falseStepAnchor.Driver.PendingCompletionDossier!.Value.Results);
        Assert.Equal("step:1.0", failedAnchor.Anchor);
        Assert.False(failedAnchor.Verified);

        var verifiedAttemptAnchor = RunWithAnchor(usePlanAttempt: true);
        Assert.True(verifiedAttemptAnchor.Result.Status == RunDriveStatus.Completed,
            verifiedAttemptAnchor.Result.Reason);
    }
}
