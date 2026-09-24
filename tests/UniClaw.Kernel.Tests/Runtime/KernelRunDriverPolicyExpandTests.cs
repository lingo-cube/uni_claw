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
/// RUN-005 Slice B — PolicyExpand 展开运行时（FROZEN v0.3.1 §5/§7/§8）：
/// adoption（exact lease 绑定 + PolicyId 采纳占用）· 良基循环（fresh observe
/// → lease exact-equality → termination → guard → bounds → match → 模板物化
/// 单步）· 复用 StepAct→StepVerify 全链（零新执行器）· E4 映射 ·
/// _pendingPolicyOutcome / PolicyInvalidated typed cause。全 deterministic
/// driver 级测试（仿真接线归 Slice C）。
/// </summary>
public sealed class KernelRunDriverPolicyExpandTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 24, 10, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 9, 24, 10, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T3 = new(2026, 9, 24, 10, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T4 = new(2026, 9, 24, 10, 20, 0, TimeSpan.Zero);

    private const string ContainerSubject = "ui.container.observed";
    private const string TempSubject = "temp";
    private static readonly string[] TempDomain = { "24", "23", "22", "21" };

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

    /// <summary>同 KernelRunDriverTests 约定：claim value "role:descriptor@state"。</summary>
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

    /// <summary>确定性脚本输入：External/PostAction 双队列（耗尽 → null = 等待）。</summary>
    private sealed class ScriptedInputs
    {
        private readonly Queue<RunDriverInput.Observation> _external = new();
        private readonly Queue<RunDriverInput.Observation> _postAction = new();
        public Func<AgentDecisionContext, AgentDecision?>? Consult { get; set; }

        public RunDriverInputs Build() => new()
        {
            NextInput = directive => directive.Context == ObservationContext.External
                ? Dequeue(_external)
                : Dequeue(_postAction),
            ConsultAgent = ctx => Consult!(ctx),
        };

        private static RunDriverInput? Dequeue(Queue<RunDriverInput.Observation> queue) =>
            queue.Count > 0 ? queue.Dequeue() : null;

        public void EnqueueExternal(params ObservationProposal[] proposals) =>
            _external.Enqueue(new RunDriverInput.Observation(proposals));

        public void EnqueuePost(params ObservationProposal[] proposals) =>
            _postAction.Enqueue(new RunDriverInput.Observation(proposals));
    }

    /// <summary>temp 观察：scope 按 capture time 派生——值变化经同 producer
    /// 异 scope 走 Revise（值替换），不落入同 scope 异值的显式 Conflict。</summary>
    private static ObservationProposal Temp(string value, DateTimeOffset t) => new(
        new ObservationClaim(TempSubject, value), IngressKind.Observation, ObservationContext.External,
        new Provenance("provider.scripts", t, $"scope:{TempSubject}:{t.Ticks}", new[] { "raw://capture", "encode:v1" }));

    private static ObservationProposal Container(string value, DateTimeOffset t) => new(
        new ObservationClaim(ContainerSubject, value), IngressKind.Observation, ObservationContext.External,
        new Provenance("provider.scripts", t, $"scope:ui.container", new[] { "raw://capture", "encode:v1" }));

    private static ObservationProposal PostContainer(string value, DateTimeOffset t) => new(
        new ObservationClaim(ContainerSubject, value), IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance("provider.scripts", t, "scope:ui.container", new[] { "raw://capture", "encode:v1" }));

    /// <summary>SeedContainer 探针：root container id 内容派生自**批内第一条**
    /// processed evidence record（association 仅在 Previous==null 时 New）；
    /// 而 revision occurrences 由**最后一条**记录派生——探针必须对准首条。</summary>
    private static string ProbeContainerIdOf(ObservationProposal firstRecord)
    {
        var (admission, _) = new EvidenceLedger().Admit(firstRecord);
        return "ctr-" + admission.EvidenceId![3..15];
    }

    private sealed class Composition
    {
        public required UniKernel Kernel { get; init; }
        public required AgentPlanPolicy Plan { get; init; }
        public required ScriptedInputs Script { get; init; }
        public required EffectBoundary Effects { get; init; }
        public required List<AgentDecisionContext> Consultations { get; init; }
    }

    /// <summary>标准 policy 世界：seed container + 状态化 occurrence（minus:primary@…）。
    /// consult 工厂收 (context, 第几次调用 n≥1)——避免闭包捕获未完成构造的组态。</summary>
    private static Composition Compose(
        Func<AgentDecisionContext, int, AgentDecision?> consult,
        ObservationProposal initialFirstRecord,
        IAssociationStrategy? association = null)
    {
        var cid = ProbeContainerIdOf(initialFirstRecord);
        var script = new ScriptedInputs();
        var consultations = new List<AgentDecisionContext>();
        var calls = 0;
        script.Consult = ctx =>
        {
            calls++;
            consultations.Add(ctx);
            return consult(ctx, calls);
        };
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new OkDriver());
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(
                new HashSet<string> { ContainerSubject, TempSubject },
                association ?? new SeedContainerAssociationStrategy(),
                new StatefulObservationStrategy(cid),
                new RoleContinuityStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        return new Composition
        {
            Kernel = kernel, Plan = plan, Script = script, Effects = effects,
            Consultations = consultations,
        };
    }

    private static void Admit(Composition c) =>
        Assert.True(c.Kernel.AdmitContract(new ExecutionContract(
            Version: "v1",
            Objective: "cool-down",
            Scope: new HashSet<string> { ContainerSubject, TempSubject },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "temp-20" },
            Obligations: new[]
            {
                new RunObligation("obl-temp", RunObligationKind.Objective, TempSubject, "20", true),
            })).Accepted);

    /// <summary>合法 policy（A2 语义：temp 方向域 match + 终点终止 + 可选守卫）。</summary>
    private static AgentDecision.Policy Policy(
        string decisionId,
        string policyId = "pol-1",
        IReadOnlyList<PolicyPredicate>? match = null,
        PolicyActionTemplate? template = null,
        IReadOnlyList<PolicyPredicate>? termination = null,
        IReadOnlyList<PolicyGuard>? guards = null,
        int maxApplications = 4) => new(
        decisionId,
        new PolicyProposal(
            policyId,
            match ?? new[] { new PolicyPredicate.ClaimInSet(TempSubject, TempDomain) },
            template ?? new PolicyActionTemplate("minus", "primary", "tap", DesiredState: null),
            termination ?? new[] { new PolicyPredicate.ClaimEquals(TempSubject, "20") },
            guards ?? Array.Empty<PolicyGuard>(),
            maxApplications,
        "step-down-until-20"));

    // ---- P1：adoption → 展开循环 → policy-succeeded（含 WaitingForInput 续跑）----

    [Fact]
    public void Adoption_BindsLease_RunsExpandLoop_ToPolicySucceeded()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId)
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "goal-met")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // 初始观察 → 咨询1 → adoption → PolicyExpand r1（fresh obs）→ 物化 →
        // dispatch → StepVerify（post-action 证据）→ verified → r2 等待新观察
        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@off", T1));
        c.Script.EnqueuePost(PostContainer("minus:primary@on", T2));

        var waiting = driver.Drive();
        // r2 fresh observation 未到 → 合法等待（不 dispatch、不退出判定）
        Assert.Equal(RunDriveStatus.WaitingForInput, waiting.Status);
        Assert.Equal("policy-observation", waiting.Reason);
        Assert.Equal(1, c.Effects.ReceiptLog.Count); // 恰一次 application

        // r2：temp=20 → Termination Satisfied → policy-succeeded → 咨询2
        //（StepVerified 相位）→ NoAction → 终局达成
        c.Script.EnqueueExternal(Temp("20", T3), Container("minus:primary@on", T3));
        var completed = driver.Drive();
        Assert.Equal(RunDriveStatus.Completed, completed.Status);
        Assert.Equal(1, completed.DeliveredEffects);
        Assert.True(c.Kernel.IsRunTerminal);

        // 咨询序列：InitialPlanning（采纳）→ StepVerified（policy 成功出口）
        Assert.Equal(2, c.Consultations.Count);
        Assert.Equal(AgentDecisionPhase.InitialPlanning, c.Consultations[0].Phase);
        Assert.Null(c.Consultations[0].Progress.PolicyState);
        Assert.Equal(AgentDecisionPhase.StepVerified, c.Consultations[1].Phase);
        Assert.Null(c.Consultations[1].FailureReason);
        var summary = c.Consultations[1].Progress.PolicyState;
        Assert.NotNull(summary);
        Assert.Equal("pol-1", summary!.PolicyId);
        Assert.Equal(1, summary.ApplicationsUsed); // 恰一次已验证 application
        Assert.Equal(PolicyTruth.Satisfied, summary.TerminationStatus);
    }

    [Fact]
    public void TerminationSatisfiedAtAdoptionRound_ZeroApplications_Succeeds()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId)
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "already-20")),
            Temp("20", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        c.Script.EnqueueExternal(Temp("20", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("20", T1), Container("minus:primary@off", T1));

        var completed = driver.Drive();
        // 0-application 即时满足：第一轮 fresh observation 后 Termination 已
        // Satisfied → 成功出口（§7 行 1），零 dispatch
        Assert.Equal(RunDriveStatus.Completed, completed.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        var summary = c.Consultations[1].Progress.PolicyState;
        Assert.NotNull(summary);
        Assert.Equal(0, summary!.ApplicationsUsed);
        Assert.Equal(PolicyTruth.Satisfied, summary.TerminationStatus);
    }

    // ---- P4/P5/P6：termination / guard 三态出口 -----------------------------------

    [Fact]
    public void TerminationUnknown_Invalidates_TerminationUnprovable()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId, termination: new[] { new PolicyPredicate.ClaimEquals(TempSubject, "20") })
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "cannot-prove")),
            Container("minus:primary@off", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // 初始无 temp claim；r1 fresh obs 亦无 → subject 缺席 → Unknown
        c.Script.EnqueueExternal(Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Container("minus:primary@off", T1));

        var result = driver.Drive();

        // 咨询2 后 NoAction → TerminalNotProven（目标未达的诚实报告）
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.False(c.Kernel.IsRunTerminal);
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, c.Consultations[1].Phase);
        Assert.Equal("policy:termination-unprovable", c.Consultations[1].FailureReason);
        var summary = c.Consultations[1].Progress.PolicyState;
        Assert.NotNull(summary);
        Assert.Equal(PolicyTruth.Unknown, summary!.TerminationStatus);
        Assert.Equal(0, summary.ApplicationsUsed);
    }

    [Fact]
    public void MatchViolated_OutOfDirectionDomain_InvalidatesNoMatch()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId)
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "overshot")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // temp=19：termination 19≠20 Violated（继续）→ match 出集 → no-match
        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("19", T1), Container("minus:primary@off", T1));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count); // 未物化任何步——出集即停
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, c.Consultations[1].Phase);
        Assert.Equal("policy:no-match", c.Consultations[1].FailureReason);
        Assert.Equal(PolicyTruth.Violated, c.Consultations[1].Progress.PolicyState!.TerminationStatus);
    }

    [Fact]
    public void MatchUnknown_SubjectAbsent_InvalidatesMatchUnknown()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId,
                // termination：temp=24 在场且 ≠20 → Violated（继续，先通过）
                // match：subject "mode" 缺席 → Unknown → match-unknown
                match: new[] { new PolicyPredicate.ClaimInSet("mode", new[] { "eco", "sport" }) })
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "no-mode-data")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@off", T1));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.Equal("policy:match-unknown", c.Consultations[1].FailureReason);
    }

    // ---- P2/P3：bounds 与 guard ---------------------------------------------------

    [Fact]
    public void BoundsExhausted_AfterVerifiedApplications_Invalidates()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId, maxApplications: 1)
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "stuck-at-24")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@off", T1));
        c.Script.EnqueuePost(PostContainer("minus:primary@off", T2)); // click 型：唯一重现即验证通过
        c.Script.EnqueueExternal(Temp("24", T3), Container("minus:primary@off", T3));

        var result = driver.Drive();

        // r1 dispatch+verify（ApplicationsUsed=1）→ r2：termination Violated →
        // bounds 1≥1 → bounds-exhausted
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(1, c.Effects.ReceiptLog.Count);
        Assert.Equal("policy:bounds-exhausted", c.Consultations[1].FailureReason);
        var summary = c.Consultations[1].Progress.PolicyState;
        Assert.Equal(1, summary!.ApplicationsUsed);
        Assert.Equal(PolicyTruth.Violated, summary.TerminationStatus);
    }

    [Fact]
    public void GuardViolated_AfterConsecutiveUnchangedRounds_Invalidates()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId, guards: new[] { new PolicyGuard.ObservationUnchanged(TempSubject, 1) })
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "temp-stuck")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // r1：warm-up（首样本只初始化，count=0）→ dispatch#1；r2：count 0 < 1
        // → Satisfied → dispatch#2 → 同值 → count=1；r3：count 1 ≥ 1 → Violated
        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@off", T1));
        c.Script.EnqueuePost(PostContainer("minus:primary@off", T2));
        c.Script.EnqueueExternal(Temp("24", T2), Container("minus:primary@off", T2));
        c.Script.EnqueuePost(PostContainer("minus:primary@off", T3));
        c.Script.EnqueueExternal(Temp("24", T3), Container("minus:primary@off", T3));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(2, c.Effects.ReceiptLog.Count);
        Assert.Equal("policy:guard-violated", c.Consultations[1].FailureReason);
        Assert.Equal(2, c.Consultations[1].Progress.PolicyState!.ApplicationsUsed);
    }

    [Fact]
    public void GuardUnknown_SubjectValueUnknown_Invalidates()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId, guards: new[] { new PolicyGuard.ObservationUnchanged("mode", 2) })
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "no-mode-data")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // mode 缺席：guard 本轮值 Unknown（≠ warm-up——当前证据不足不可求值）
        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@off", T1));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.Equal("policy:guard-unknown", c.Consultations[1].FailureReason);
    }

    // ---- P7：semantic lease（v0.3.1 exact-equality 约束）---------------------------

    [Fact]
    public void LeaseInvalidated_OnContainerIdentityDrift_ZeroNewEffect()
    {
        // SignatureAssociationStrategy：未见过的 signature → New（第二 container）
        var c = Compose(
            (ctx, n) => n == 1
                ? Policy(ctx.DecisionId)
                : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "screen-changed")),
            Container("sig-a", T0),
            association: new SignatureAssociationStrategy());
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // 初始 sig-a（adoption 绑定 ctr-A）→ r1 fresh obs sig-b（unseen → New
        // 第二 container → 多根 ≠ adopted lease）→ r1 零 dispatch 即出口
        c.Script.EnqueueExternal(Container("sig-a", T0));
        c.Script.EnqueueExternal(Container("sig-b", T1));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count); // 零新 Effect（§4 出口语义）
        Assert.Equal("policy:lease-invalidated", c.Consultations[1].FailureReason);
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, c.Consultations[1].Phase);
    }

    // ---- P8：E4 映射（control-non-act）----------------------------------------------

    [Fact]
    public void ControlNonAct_WhenTargetAlreadySatisfiedAndTerminationNot_E4Mapped()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId,
                template: new PolicyActionTemplate("minus", "primary", "tap", DesiredState: "on"))
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "target-already-on")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        // r1：occurrence 已 @on → desired 满足 → SelectIntent plain-Observe（E4）
        // → 重评 Termination（24≠20 → Violated）→ control-non-act（F7(b)：消灭
        // 静默楔死，不伪装成功）
        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@on", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@on", T1));

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.Equal("policy:control-non-act", c.Consultations[1].FailureReason);
        Assert.Equal(PolicyTruth.Violated, c.Consultations[1].Progress.PolicyState!.TerminationStatus);
    }

    // ---- 步链既有转移：展开步验证失败（D5 同律 + policy 摘要）-----------------------

    [Fact]
    public void StepVerificationFailure_VoidsPolicy_UsesExistingTransition()
    {
        var c = Compose((ctx, n) => n == 1
            ? Policy(ctx.DecisionId,
                template: new PolicyActionTemplate("minus", "primary", "tap", DesiredState: "on"))
            : new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "verify-failed")),
            Temp("24", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        c.Script.EnqueueExternal(Temp("24", T0), Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Temp("24", T1), Container("minus:primary@off", T1));
        c.Script.EnqueuePost(PostContainer("minus:primary@off", T2)); // 期望 on，实际 off → 验证失败

        var result = driver.Drive();

        // 既有 VerificationFailed 转移（相位 + 原文 reason），policy 作废 + 摘要
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(1, c.Effects.ReceiptLog.Count);
        Assert.Equal(AgentDecisionPhase.VerificationFailed, c.Consultations[1].Phase);
        Assert.Equal("post-action-desired-state-not-satisfied", c.Consultations[1].FailureReason);
        var summary = c.Consultations[1].Progress.PolicyState;
        Assert.NotNull(summary);
        Assert.Equal("pol-1", summary!.PolicyId);
        Assert.Equal(0, summary.ApplicationsUsed); // 未验证成功——不计 application
    }

    // ---- v0.3.1 约束 ②：PolicyId = 实际采纳才占用 ----------------------------------

    [Fact]
    public void PolicyId_OccupiedOnlyOnAdoption_ReAdoptionOfSameIdRejected()
    {
        var c = Compose((ctx, n) => n switch
        {
            1 => Policy(ctx.DecisionId, policyId: "pol-1"),
            // 复用同 PolicyId（内容不同）→ V6f duplicate（实际采纳过的 id 被占用）
            2 => Policy(ctx.DecisionId, policyId: "pol-1",
                termination: new[] { new PolicyPredicate.ClaimEquals(TempSubject, "21") }),
            _ => new AgentDecision.NoAction(new AgentNoActionProposal(ctx.DecisionId, "done")),
        }, Container("minus:primary@off", T0));
        Admit(c);
        var driver = new KernelRunDriver(c.Kernel, c.Plan, c.Script.Build());
        Assert.True(driver.Activate().Accepted);

        c.Script.EnqueueExternal(Container("minus:primary@off", T0));
        c.Script.EnqueueExternal(Container("minus:primary@off", T1));
        // r1 无 temp → termination-unprovable → 咨询2 复用 pol-1 → duplicate reject

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("policy:duplicate-policy-id", result.Reason);
        Assert.Equal(0, c.Effects.ReceiptLog.Count);
        Assert.False(c.Kernel.IsRunTerminal);
        // 第一次采纳后的 invalidation 相位正常送达（消费即清后占用的语义可见）
        Assert.Equal(AgentDecisionPhase.PolicyInvalidated, c.Consultations[1].Phase);
    }
}
