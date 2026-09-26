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
/// RUN-004 · 终局收口 — 验收补齐测试（Gate 6 面 + 收口裁决）。
/// 覆盖：完成证明三层（层2 锚定自证 / 层3 双分支 + dossier + settlement）、
/// V3/V4/V5 机械校验、预算耗尽诚实失败（Ac 6）、多轮连环咨询（Ac 2）、
/// E2 弹窗重议全链（Ac 1）、断点续跑不重问（Ac 8）、Defer DecisionId
/// correlation、Disposition 三态。
/// 全部验证行为（调用链 + 可观察结果），不验证字段存在性。
/// </summary>
public sealed class KernelRunDriverFinalizationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

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

    // ---- 合同 / 观察 / 世界夹具 --------------------------------------------

    /// <summary>层2/层3 测试合同：mandatory 义务 x.state="done" 只能经 completion
    /// claim 折抵满足（scope 含该 subject——不相关证据进不了 world state）。</summary>
    private static ExecutionContract CompletionContract(int? consultations = null, int? totalSteps = null) => new(
        "cf-v1", "attest-completion",
        new HashSet<string> { "live.frame", "x.state" },
        new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "objective" },
        new[] { new RunObligation("obl-x", RunObligationKind.MaterialEffect, "x.state", "done", true) },
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
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            record.Claim.Value.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment =>
                {
                    var roleParts = segment.Split(':', 2);
                    var descParts = roleParts.Length == 2 ? roleParts[1].Split('@', 2) : roleParts[0].Split('@', 2);
                    return new ProposedOccurrence(
                        owner, roleParts[0],
                        descParts.Length == 2 ? descParts[0] : null,
                        descParts.Length == 2 ? descParts[1] : null);
                })
                .ToArray();
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

    /// <summary>
    /// 组装带 occurrence 世界（StepAct/StepVerify 可达）的 kernel + driver。
    /// seedValue 必须与测试首条 External 观察的 claim value 逐字节一致——
    /// container id 内容寻址，探针值不一致 → occurrence 悬空（grounding 失败）。
    /// </summary>
    /// <summary>PER-014 R3：typed semantic checked claim（R1 缝输入；subject
    /// 须在 relevance scope）。</summary>
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

    private static (UniKernel Kernel, KernelRunDriver Driver, AgentPlanPolicy Plan) ComposeOccurrenceWorld(
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
        return (kernel, driver, plan);
    }

    // ==== Ac 7 层2：锚定自证折抵 =============================================

    /// <summary>
    /// 层2（SR-073）：NoAction+Completion 的 obs 锚全部核验 → completion
    /// claim 入证（kernel.completion-verifier producer）→ 层1 路径照常判
    /// → Completed。义务 x.state 无观察来源，只有折抵能满足它——完成归因
    /// 可直接断言 producer。
    /// </summary>
    [Fact]
    public void Acceptance7_Layer2_AnchoredSelfAttestation_DischargesToCompletion()
    {
        AgentDecisionContext? first = null;
        string? obsAnchor = null;
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: k => ctx =>
            {
                first = ctx;
                obsAnchor ??= "obs:" + k.CurrentBelief!.EvidenceBasis.First();
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    ctx.DecisionId, "attested-by-observation",
                    new CompletionEvidence("observed-done", new[] { obsAnchor })));
            },
            contract: CompletionContract());

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.NotNull(result.Outcome);
        Assert.True(kernel.IsRunTerminal);
        // 折抵写回：producer 留痕可审计（G2 落点）
        var claim = kernel.CurrentBelief!.WorldState["x.state"];
        Assert.Equal("done", claim.Value);
        Assert.Equal("kernel.completion-verifier", claim.EstablishingProducer);
    }

    // ==== Ac 7/11 层3：人为终极裁定双分支 ====================================

    /// <summary>
    /// 层3 rejected（SR-150 + 收口裁决②）：锚不住 → AwaitingCompletionAdjudication
    /// （dossier 呈递含核验结果）→ ResumeWithAdjudication(false) →
    /// TerminalNotProven "completion-rejected" → settlement 持久化：
    /// 后续 Drive() 幂等重报同一终局，不重新咨询、不重入裁决、dossier 关闭。
    /// </summary>
    [Fact]
    public void Acceptance11_Layer3_Rejected_SettlementIsPersistent_NoReAdjudication()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => ctx =>
            {
                consults++;
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    ctx.DecisionId, "agent-insists",
                    new CompletionEvidence("insisted", new[] { "dispatch:no-such-receipt" })));
            },
            contract: CompletionContract());

        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.AwaitingCompletionAdjudication, waiting.Status);
        Assert.Equal("completion-anchors-unverified", waiting.Reason);
        Assert.Equal(1, consults);

        // dossier 呈递：证据 + 逐锚核验结果（未锚住 = false）
        var dossier = driver.PendingCompletionDossier;
        Assert.NotNull(dossier);
        Assert.Equal("insisted", dossier!.Value.Evidence.Basis);
        var single = Assert.Single(dossier.Value.Results);
        Assert.Equal("dispatch:no-such-receipt", single.Anchor);
        Assert.False(single.Verified);

        // 人工终裁 rejected → 终局
        var rejected = driver.ResumeWithAdjudication(approved: false, note: "not-convinced");
        Assert.Equal(RunDriveStatus.TerminalNotProven, rejected.Status);
        Assert.Equal("completion-rejected:not-convinced", rejected.Reason);
        Assert.False(kernel.IsRunTerminal); // 诚实失败，非 Completed

        // settlement：后续 Drive() 不得重新发起同一 completion adjudication
        Assert.Null(driver.PendingCompletionDossier); // 等待态已闭合
        var again = driver.Drive();
        Assert.Equal(RunDriveStatus.TerminalNotProven, again.Status);
        Assert.Equal("completion-rejected", again.Reason);
        Assert.Equal(1, consults); // 无新咨询（防 NoAction→驳回→再裁决环）
    }

    /// <summary>
    /// 层3 approved（SR-073）：adjudicator 权威折抵（义务满足来源 = adjudicator
    /// 决定，锚点只是进入裁决的真实性门槛）→ TerminalEvaluation → Completed。
    /// </summary>
    [Fact]
    public void Acceptance11_Layer3_Approved_AdjudicatorDischargesToCompletion()
    {
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => ctx => new AgentDecision.NoAction(new AgentNoActionProposal(
                ctx.DecisionId, "agent-insists",
                new CompletionEvidence("insisted", new[] { "dispatch:no-such-receipt" }))),
            contract: CompletionContract());

        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.AwaitingCompletionAdjudication, waiting.Status);

        var approved = driver.ResumeWithAdjudication(approved: true, note: null);

        Assert.Equal(RunDriveStatus.Completed, approved.Status);
        Assert.True(kernel.IsRunTerminal);
        var claim = kernel.CurrentBelief!.WorldState["x.state"];
        Assert.Equal("done", claim.Value);
        Assert.Equal("kernel.completion-adjudicator", claim.EstablishingProducer);
    }

    // ==== Ac 7 层2：step / dispatch 锚核验 ===================================

    /// <summary>
    /// 层2 全锚面：真实派发步的 step:{n}.{i} 与 dispatch:{receipt} 锚
    /// （修复回归：step 锚按 (DecisionN, StepIndex) 命中——ReceiptId 不参与）。
    /// 义务经折抵满足（世界只观察 ui 容器，x.state 无观察来源）。
    /// </summary>
    [Fact]
    public void Acceptance7_Layer2_StepAndDispatchAnchors_VerifyAgainstArchive()
    {
        var consults = 0;
        var steps = new List<string>();
        var (kernel, driver, _) = ComposeOccurrenceWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : new RunDriverInput.Observation(new[]
                {
                    TypedCheckedClaim("checked", UIWorldDoubles.T1),
                    PostActionObservation("switch:primary@on", UIWorldDoubles.T1),
                }),
            consultFactory: k => ctx =>
            {
                consults++;
                if (consults == 1)
                    return new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("switch", "primary", "toggle", "on") },
                        "flip"));
                // E1 再咨询（StepVerified）：锚住真实步 → 层2 折抵
                steps.Add("step:1.0");
                steps.Add("dispatch:" + k.EffectReceipts[0].ReceiptId);
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    ctx.DecisionId, "done-and-anchored",
                    new CompletionEvidence("step-archive", steps)));
            },
            new ExecutionContract(
                "mf-v1", "switch-then-attest",
                new HashSet<string> { UIWorldDoubles.Observed, "x.state" },
                new HashSet<string> { "toggle" }, new HashSet<string>(),
                new[] { "objective" },
                new[] { new RunObligation("obl-x", RunObligationKind.MaterialEffect, "x.state", "done", true) }));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(2, consults);
        var claim = kernel.CurrentBelief!.WorldState["x.state"];
        Assert.Equal("kernel.completion-verifier", claim.EstablishingProducer);
    }

    // ==== Ac 10：V3 / V4 / V5 机械校验 =======================================

    /// <summary>V3（SR-049）：提案步数 &gt; StepsRemaining → fail closed。</summary>
    [Fact]
    public void Acceptance10_V3_StepBudgetExceeded_FailsClosed()
    {
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => ctx => new AgentDecision.Act(new AgentActionProposal(
                ctx.DecisionId,
                new[]
                {
                    new AgentActionStep("switch", null, "tap", "on"),
                    new AgentActionStep("switch", null, "tap", "on"),
                },
                "two-steps")),
            contract: CompletionContract(totalSteps: 1));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("budget-exceeded:steps", result.Reason);
        Assert.False(kernel.IsRunTerminal);
    }

    /// <summary>V4（SR-074）：首询 NoAction、mandatory 义务未满足、无自证 → 空洞收工。</summary>
    [Fact]
    public void Acceptance10_V4_HollowCompletion_FailsClosed()
    {
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => ctx => new AgentDecision.NoAction(
                new AgentNoActionProposal(ctx.DecisionId, "nothing-to-do")),
            contract: CompletionContract());

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("hollow-completion", result.Reason);
        Assert.False(kernel.IsRunTerminal);
    }

    /// <summary>V5 静态界（SR-068）：Defer MaxRounds &gt; 4 → fail closed。</summary>
    [Fact]
    public void Acceptance10_V5_DeferMaxRoundsOverBound_FailsClosed()
    {
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => ctx => new AgentDecision.Defer(
                ctx.DecisionId, new ObserveSpec(Subject: null, MaxRounds: 5)),
            contract: CompletionContract());

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("defer-unbounded:max-rounds", result.Reason);
        Assert.False(kernel.IsRunTerminal);
    }

    // ==== Ac 6：预算耗尽诚实失败 =============================================

    /// <summary>
    /// Ac 6（SR-102）：轮次预算耗尽 → consult 前 fail closed
    /// "consult-budget-exhausted"，非终态、零 effect、不续借。
    /// </summary>
    [Fact]
    public void Acceptance6_ConsultBudgetExhausted_HonestFailure()
    {
        var consults = 0;
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => ctx =>
            {
                consults++;
                return new AgentDecision.Defer(
                    ctx.DecisionId, new ObserveSpec(Subject: null, MaxRounds: 1));
            },
            contract: CompletionContract(consultations: 1));

        var result = driver.Drive();

        // 首个 Defer 消费唯一一轮 → 再咨询前预算已尽 → 诚实失败
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("consult-budget-exhausted", result.Reason);
        Assert.Equal(1, consults);
        Assert.Empty(kernel.EffectReceipts);
        Assert.False(kernel.IsRunTerminal);
    }

    // ==== Ac 2：StepVerified 连环咨询（≥3 次，预算 64）=======================

    /// <summary>
    /// Ac 2（SR-099）：两支提案 + 终问 = 3 次连环咨询；预算 64 声明在合同里
    /// 全程不被截断；相位序列 InitialPlanning → StepVerified → StepVerified。
    /// </summary>
    [Fact]
    public void Acceptance2_ThreeConsultations_UnderDeclaredBudget64()
    {
        var phases = new List<AgentDecisionPhase>();
        var postPulls = 0;
        var (kernel, driver, _) = ComposeOccurrenceWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off+menuItem:list@visible", UIWorldDoubles.T0),
                })
                // 每次 post 拉取内容递增：同内容 claim 幂等（不产生新 revision），
                // 第二步验证需要新 revision 才能过 post-action-reconciled 门
                : new RunDriverInput.Observation(new[]
                {
                    TypedCheckedClaim("checked", UIWorldDoubles.T1.AddSeconds(postPulls)),
                    PostActionObservation(
                        $"switch:primary@on+menuItem:list@{(++postPulls == 1 ? "visible" : "visited")}",
                        UIWorldDoubles.T1.AddSeconds(postPulls)),
                }),
            consultFactory: _ => ctx =>
            {
                phases.Add(ctx.Phase);
                return phases.Count switch
                {
                    1 => new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("switch", "primary", "toggle", "on") },
                        "first-screen")),
                    2 => new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("menuItem", "list", "tap", null) },
                        "second-screen")),
                    _ => new AgentDecision.NoAction(new AgentNoActionProposal(
                        ctx.DecisionId, "traversal-done")),
                };
            },
            new ExecutionContract(
                "ms-v1", "traverse",
                new HashSet<string> { UIWorldDoubles.Observed },
                new HashSet<string> { "toggle", "tap" }, new HashSet<string>(),
                new[] { "objective" },
                new[]
                {
                    new RunObligation(
                        "obl-switch", RunObligationKind.Objective, "ui.role.switch.checked", "checked", true,
                        EntityScope: new TargetDescriptor("switch", "primary")),
                },
                MaxConsultations: 64),
            seedValue: "switch:primary@off+menuItem:list@visible");

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(3, phases.Count);
        Assert.Equal(AgentDecisionPhase.InitialPlanning, phases[0]);
        Assert.Equal(AgentDecisionPhase.StepVerified, phases[1]);
        Assert.Equal(AgentDecisionPhase.StepVerified, phases[2]);
        Assert.Equal(2, kernel.EffectReceipts.Count);
    }

    // ==== Ac 1：弹窗重议全链（E2 → 重咨询 → 重规划 → 达标）===================

    /// <summary>
    /// Ac 1 全链（SR-106）：首支提案在 E2（no-single-root-container）被拒 →
    /// Agent 收到 StepRejected 相位 + 原因 → 重规划（NoAction+Completion
    /// 层2 折抵）→ Completed。RED5-A 只断言相位；这里闭合「重议后达标」。
    /// </summary>
    [Fact]
    public void Acceptance1_GroundingReject_FullChain_ReplanToCompletion()
    {
        var phases = new List<AgentDecisionPhase>();
        string? failureReason = null;
        string? obsAnchor = null;
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: k => ctx =>
            {
                phases.Add(ctx.Phase);
                failureReason ??= ctx.FailureReason;
                if (phases.Count == 1)
                    return new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("switch", null, "tap", "on") },
                        "original-plan"));
                obsAnchor ??= "obs:" + k.CurrentBelief!.EvidenceBasis.First();
                return new AgentDecision.NoAction(new AgentNoActionProposal(
                    ctx.DecisionId, "replanned-attest",
                    new CompletionEvidence("popup-handled", new[] { obsAnchor })));
            },
            contract: CompletionContract());

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal(2, phases.Count);
        Assert.Equal(AgentDecisionPhase.InitialPlanning, phases[0]);
        Assert.Equal(AgentDecisionPhase.StepRejected, phases[1]);
        Assert.Equal("no-single-root-container", failureReason);
        Assert.True(kernel.IsRunTerminal);
    }

    // ==== Ac 8：断点续跑不重问 ===============================================

    /// <summary>
    /// Ac 8（SR-107/108）：WaitingForInput 中断后重复 Drive() 不重咨询
    /// （幂等续跑）；证据到达后从断点恢复完成。决策序列确定性由
    /// Simulation SemanticDigestTests 承载（同输入两跑 digest 恒等）。
    /// </summary>
    [Fact]
    public void Acceptance8_WaitingResume_DoesNotReconsult()
    {
        var consults = 0;
        var postActionArrived = false;
        var (kernel, driver, _) = ComposeOccurrenceWorld(
            nextInput: expected => expected.Context == ObservationContext.External
                ? new RunDriverInput.Observation(new[]
                {
                    UIWorldDoubles.Observation("switch:primary@off", UIWorldDoubles.T0),
                })
                : postActionArrived
                    ? new RunDriverInput.Observation(new[]
                    {
                        TypedCheckedClaim("checked", UIWorldDoubles.T1),
                        PostActionObservation("switch:primary@on", UIWorldDoubles.T1),
                    })
                    : null,
            consultFactory: _ => ctx =>
            {
                consults++;
                return consults == 1
                    ? new AgentDecision.Act(new AgentActionProposal(
                        ctx.DecisionId,
                        new[] { new AgentActionStep("switch", "primary", "toggle", "on") },
                        "flip-the-switch"))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(
                        ctx.DecisionId, "script-exhausted"));
            },
            new ExecutionContract(
                "rs-v1", "turn-switch-on",
                new HashSet<string> { UIWorldDoubles.Observed },
                new HashSet<string> { "toggle" }, new HashSet<string>(),
                new[] { "objective" },
                new[]
                {
                    new RunObligation(
                        "obl-switch", RunObligationKind.Objective, "ui.role.switch.checked", "checked", true,
                        EntityScope: new TargetDescriptor("switch", "primary")),
                }));

        var waiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Equal(1, consults);

        // 断点续跑：证据未到时再 Drive 不产生新咨询
        var stillWaiting = driver.Drive();
        Assert.Equal(RunDriveStatus.WaitingForInput, stillWaiting.Status);
        Assert.Equal(1, consults);

        postActionArrived = true;
        var completed = driver.Drive();
        Assert.Equal(RunDriveStatus.Completed, completed.Status);
        Assert.Equal(2, consults); // E1 再咨询恰一次
    }

    // ==== Defer DecisionId correlation（收口裁决③）==========================

    /// <summary>
    /// Defer 回带 DecisionId ≠ 请求号 → correlation-mismatch fail closed
    /// （D2 防串话适用于每一次 consultation response，含 Defer）。
    /// 正例（回带同号被接受）由 Acceptance9_FirstDefer 及 Defer 链测试承载。
    /// </summary>
    [Fact]
    public void DeferResponse_WithMismatchedDecisionId_FailsClosed()
    {
        var (kernel, driver) = ComposePlain(
            nextInput: _ => SeedObservation(),
            consultFactory: _ => _ => new AgentDecision.Defer(
                "decision-forged", new ObserveSpec(Subject: null, MaxRounds: 1)),
            contract: CompletionContract());

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("correlation-mismatch", result.Reason);
        Assert.Empty(kernel.EffectReceipts);
        Assert.False(kernel.IsRunTerminal);
    }

    // ==== Disposition 三态（收口裁决④）=======================================

    /// <summary>
    /// Claims Disposition 三态同帧可产生：established（单次观察）/ revised
    /// （同 producer 异 scope 再观察——痕迹链非空）/ conflicted（同 subject
    /// 异值悬案）。Act 后 state off→on 的 revision 对 Agent 有真实信息价值。
    /// </summary>
    [Fact]
    public void ClaimsDisposition_ThreeStatesObservable()
    {
        AgentDecisionContext? captured = null;
        var (kernel, driver) = ComposePlain(
            nextInput: _ => new RunDriverInput.Observation(new[]
            {
                // established：单次观察
                new ObservationProposal(
                    new ObservationClaim("a.state", "1"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("test", T0, "scope:a", new[] { "test:a" })),
                // revised：同 producer 异 scope 值替换（CLE-001 Revise 痕迹链）
                new ObservationProposal(
                    new ObservationClaim("b.state", "off"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("test", T0, "scope:b1", new[] { "test:b1" })),
                new ObservationProposal(
                    new ObservationClaim("b.state", "on"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("test", T0, "scope:b2", new[] { "test:b2" })),
                // conflicted：同 producer 同 scope 异值（显式 Conflict）
                new ObservationProposal(
                    new ObservationClaim("c.state", "x"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("test", T0, "scope:c", new[] { "test:c1" })),
                new ObservationProposal(
                    new ObservationClaim("c.state", "y"),
                    IngressKind.Observation, ObservationContext.External,
                    new Provenance("test", T0, "scope:c", new[] { "test:c2" })),
            }),
            consultFactory: _ => ctx =>
            {
                captured = ctx;
                return null; // 捕获上下文即止（no-response fail closed 为预期终态）
            },
            contract: new ExecutionContract(
                "dp-v1", "inspect-disposition",
                new HashSet<string> { "a.state", "b.state", "c.state" },
                new HashSet<string> { "tap" }, new HashSet<string>(),
                new[] { "objective" },
                new[] { new RunObligation("obl-a", RunObligationKind.MaterialEffect, "a.state", "1", false) }));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status); // no-response（测试意图）
        var claims = captured!.CurrentWorldClaims;
        Assert.Equal("established", claims["a.state"].Disposition);
        Assert.False(claims["a.state"].InConflict);
        Assert.Equal("revised", claims["b.state"].Disposition);
        Assert.Equal("on", claims["b.state"].Value);
        Assert.False(claims["b.state"].InConflict);
        Assert.Equal("conflicted", claims["c.state"].Disposition);
        Assert.True(claims["c.state"].InConflict);
    }

    // ---- 组装（plain 世界：无 container strategy → E2 no-single-root 可达）----

    private static (UniKernel Kernel, KernelRunDriver Driver) ComposePlain(
        Func<ObservationDirective, RunDriverInput?>? nextInput,
        Func<UniKernel, Func<AgentDecisionContext, AgentDecision?>>? consultFactory,
        ExecutionContract contract)
    {
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(new HashSet<string>(contract.Scope)),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        var inputs = new RunDriverInputs
        {
            NextInput = nextInput ?? (_ => null),
            ConsultAgent = consultFactory is null ? (_ => null) : consultFactory(kernel),
        };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(kernel.AdmitContract(contract).Accepted);
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver);
    }
}
