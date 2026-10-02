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
        consultations, totalSteps);

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
            if (record.Claim.Subject.StartsWith("ui.node.", StringComparison.Ordinal)
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
        string seedValue = "switch:primary@off")
    {
        var cid = ProbeContainerId(seedValue);
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(
                new HashSet<string>(contract.Scope.Append(TypedSubject)),
                new SeedContainerAssociationStrategy(),
                new StatefulObservationStrategy(cid),
                new RoleContinuityStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        var inputs = new RunDriverInputs { NextInput = nextInput, ConsultAgent = consultFactory(kernel) };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(kernel.AdmitContract(contract).Accepted);
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver);
    }

    private static AgentDecision PlanDecision(string decisionId, params PlanItem[] items) =>
        new AgentDecision.Plan(decisionId, new AgentPlanProposal(items, "test-plan"));

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

    // ==== V7 机械校验（malformed / unknown / 超预算 → 零 Effect）============

    [Fact]
    public void EmptyPlan_FailsClosed_ZeroEffects()
    {
        var (kernel, driver) = ComposePlanWorld(
            _ => SeedObservation(),
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
            _ => SeedObservation(),
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
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
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
            });
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
}
