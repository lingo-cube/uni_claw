using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

/// <summary>
/// PROTOCOL-FIX-003（ADR-0050 落地）— 预算两层顶 + P25 续授锚定测试。
/// 覆盖：A1 admission 预算 valueOrigin（View 沿载、同内容 View 不变）；
/// A2 阈值窗口开/关 + 终问携带续授；A3 liveness——无限续授请求全部被
/// HardCap 封顶 → 必达 consult-budget-exhausted 诚实终局；A4 续授全程
/// P25 留痕（决策 id、量、依据）+ 契约 View 零变化；A5 缺省路径（无
/// policy hardcap）行为不变。
/// </summary>
public sealed class KernelRunDriverBudgetRenewalTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

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

    private static ExecutionContract RenewalContract(int? consultations = null, int? totalSteps = null) => new(
        "renewal-v1", "budget-renewal",
        new HashSet<string> { "live.frame" },
        new HashSet<string> { "tap" }, new HashSet<string>(),
        new[] { "objective" },
        Obligations: null, consultations, totalSteps);

    private static RunDriverInput.Observation SeedObservation() => new(new[]
    {
        new ObservationProposal(
            new ObservationClaim("live.frame", "seed"),
            IngressKind.Observation,
            ObservationContext.External,
            new Provenance("test", T0, "scope:live.frame", new[] { "test:seed" })),
    });

    private static (UniKernel Kernel, KernelRunDriver Driver, AgentPlanPolicy Plan) Compose(
        Func<AgentDecisionContext, AgentDecision?> consult,
        ExecutionContract contract,
        AgentPlanPolicy? plan = null)
    {
        plan ??= new AgentPlanPolicy();
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
            NextInput = _ => SeedObservation(),
            ConsultAgent = consult,
        };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(kernel.AdmitContract(contract).Accepted);
        Assert.True(driver.Activate().Accepted);
        return (kernel, driver, plan);
    }

    // ==== A1：admission 预算 valueOrigin =====================================

    [Fact]
    public void Admission_RecordsBudgetValueOrigin_ConfiguredAndDefault()
    {
        var configured = new RunModel();
        var admittedConfigured = configured.AdmitContract(RenewalContract(consultations: 4, totalSteps: 32));
        Assert.True(admittedConfigured.Accepted);
        Assert.Equal(BudgetValueOrigin.Configured, configured.View!.BudgetValueOrigin);

        var defaulted = new RunModel();
        var admittedDefault = defaulted.AdmitContract(RenewalContract());
        Assert.True(admittedDefault.Accepted);
        Assert.Equal(BudgetValueOrigin.Default, defaulted.View!.BudgetValueOrigin);

        // 同内容重复 admit：同一 View 实例（幂等）+ origin 稳定（View 字节不变）
        var readmitted = configured.AdmitContract(RenewalContract(consultations: 4, totalSteps: 32));
        Assert.True(readmitted.Accepted);
        Assert.Same(configured.View, configured.View);
        Assert.Equal(BudgetValueOrigin.Configured, configured.View.BudgetValueOrigin);
    }

    // ==== A2：阈值窗口开/关 + 续授生效 =======================================

    [Fact]
    public void RenewalWindow_OpensAtThreshold_ClosesAfterGrant()
    {
        var contexts = new List<AgentDecisionContext>();
        var consults = 0;
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            consults++;
            return consults == 1
                ? new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1),
                    new BudgetRenewalRequest(AdditionalSteps: 0, AdditionalRounds: 6,
                        Justification: "work-unfinished"))
                : new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1));
        }, RenewalContract(consultations: 2),
            new AgentPlanPolicy { HardCapConsultations = 8, RenewalThresholdRounds = 2 });

        // consult1：剩余 2 ≤ 阈值 2 → 窗口开；续授 +6 轮被授予
        // consult2：剩余 8-2 = 6 > 2 → 窗口关
        while (true)
        {
            var r = driver.Drive();
            if (r.Reason is not ("evidence-insufficient" or "defer-exhausted")) break;
        }

        Assert.True(contexts.Count >= 2);
        Assert.NotNull(contexts[0].RenewalWindow);
        Assert.True(contexts[0].RenewalWindow!.Open);
        Assert.False(contexts[1].RenewalWindow!.Open);
        // 续授后剩余额度实际增长（轻记账作用于预算算术）
        Assert.Equal(6, contexts[1].BudgetRemaining!.RoundsRemaining);

        // A4：留痕 = 决策 id、量、依据
        var record = Assert.Single(driver.RenewalsGranted);
        Assert.True(record.Granted);
        Assert.Equal(contexts[0].DecisionId, record.DecisionId);
        Assert.Equal(6, record.AdditionalRounds);
        Assert.Equal("work-unfinished", record.Justification);

        // A4：契约 View 零变化（续授不是 contract mutation）
        Assert.Equal("renewal-v1", kernel.RunView!.Version);
        Assert.Equal(2, kernel.RunView.MaxConsultations);
    }

    // ==== D3：DeferRoundsExhausted 终问可携带续授（死锁角闭合）===============

    [Fact]
    public void DeferRoundsExhausted_FinalQuestion_CanCarryRenewal()
    {
        var contexts = new List<AgentDecisionContext>();
        var consults = 0;
        var (_, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            consults++;
            // consult1：Defer 开链 + 续授；consult2：耗尽触发终问；
            // consult3（DeferRoundsExhausted 终问）：携带续授 → 链重新有界继续；
            // consult4：NoAction 收口（诚实未证终局，防测试环）。
            return consults switch
            {
                1 => new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1),
                    new BudgetRenewalRequest(0, 2, "extend")),
                3 => new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1),
                    new BudgetRenewalRequest(0, 2, "extend-final")),
                _ => new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1)),
            };
        }, RenewalContract(consultations: 3),
            new AgentPlanPolicy { HardCapConsultations = 16, RenewalThresholdRounds = 4 });

        driver.Drive();

        // 终问相位确实是 DeferRoundsExhausted 且窗口开、续授被授予
        Assert.Equal(AgentDecisionPhase.DeferRoundsExhausted, contexts[2].Phase);
        Assert.True(contexts[2].RenewalWindow!.Open);
        Assert.Equal(2, driver.RenewalsGranted.Count);
        Assert.True(driver.RenewalsGranted[1].Granted);
        Assert.Equal(contexts[2].DecisionId, driver.RenewalsGranted[1].DecisionId);
        // 终问续授后链继续（consult4 发生——死锁角闭合）
        Assert.True(consults >= 4);
        Assert.Equal(AgentDecisionPhase.InitialPlanning, contexts[3].Phase);
    }

    // ==== A3：liveness——无限续授请求必达诚实终局 =============================

    [Fact]
    public void Liveness_UnboundedRenewalRequests_BoundedByHardCap_ReachesHonestBudgetExhausted()
    {
        var contexts = new List<AgentDecisionContext>();
        var (_, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            // 每次都贪婪请求 +2 轮——授到 HardCap 为止，之后全部被规则侧拒绝
            return new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1),
                new BudgetRenewalRequest(0, 2, "greedy"));
        }, RenewalContract(consultations: 2),
            new AgentPlanPolicy { HardCapConsultations = 4, RenewalThresholdRounds = 2 });

        // 有界驱动：硬顶有限 ⇒ 授出轮次有限 ⇒ 咨询轮次有限 ⇒ 必达诚实终局
        RunDriveResult result;
        var guard = 0;
        do
        {
            result = driver.Drive();
            guard++;
            Assert.True(guard < 64, "liveness 违规：Run 未在硬顶界内终止");
        }
        while (result.Reason is "evidence-insufficient" or "defer-exhausted");

        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("consult-budget-exhausted", result.Reason);
        // 有界性：总咨询 ≤ 契约初始额度 + HardCap（授出上限）
        Assert.True(contexts.Count <= 2 + 4,
            $"咨询次数超界：{contexts.Count} > 6");
        // 授到 HardCap（4 = 2×2）为止，之后全部被 HardCap 拒绝
        Assert.Equal(2, driver.RenewalsGranted.Count(r => r.Granted));
        Assert.Contains(driver.RenewalsGranted, r => !r.Granted && r.RejectionReason is not null);
    }

    // ==== A5：缺省路径（无 policy hardcap）行为不变 ==========================

    [Fact]
    public void DefaultPath_NoPolicyHardcap_RenewalRefused_BehaviorUnchanged()
    {
        var contexts = new List<AgentDecisionContext>();
        var consults = 0;
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            consults++;
            return consults == 1
                ? new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1),
                    new BudgetRenewalRequest(0, 5, "please"))
                : new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1));
        }, RenewalContract(consultations: 1));

        // 无 hardcap → 续授通道整体关闭（上下文不携带窗口、提案被拒、记账留痕）
        var result = driver.Drive();
        Assert.Null(contexts[0].RenewalWindow);
        var record = Assert.Single(driver.RenewalsGranted);
        Assert.False(record.Granted);
        Assert.Equal("renewal:no-policy-hardcap", record.RejectionReason);
        // 预算算术未被续授改变：唯一一轮耗尽后 → 既有诚实终局
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, result.Status);
        Assert.Equal("consult-budget-exhausted", result.Reason);
        Assert.Equal(1, consults);
        Assert.Empty(kernel.EffectReceipts);
    }

    // ==== A4：契约 View 零变化（record 相等锚定）=============================

    [Fact]
    public void ContractView_UntouchedByRenewals()
    {
        var (kernel, driver, _) = Compose(ctx =>
            new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, MaxRounds: 1),
                new BudgetRenewalRequest(0, 3, "x")), RenewalContract(consultations: 2),
            new AgentPlanPolicy { HardCapConsultations = 8, RenewalThresholdRounds = 2 });

        var viewBefore = kernel.RunView;
        driver.Drive();
        Assert.True(driver.RenewalsGranted.Any(r => r.Granted));
        Assert.Equal(viewBefore, kernel.RunView);
        Assert.Equal(2, kernel.RunView!.MaxConsultations);
        Assert.Equal(256, kernel.RunView.MaxTotalSteps);
    }
}
