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
        Obligations: null, consultations, totalSteps,
        TaskInstanceId: "task-budget", ProductSessionId: "session-budget",
        ContractRef: "contract-budget");

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
        AgentPlanPolicy? plan = null,
        Func<ExecutionOverviewProjection?>? projectExecutionOverview = null)
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
            ProjectExecutionOverview = projectExecutionOverview,
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
        Assert.Equal(BudgetValueOrigin.Configured, configured.View.MaxConsultationsOrigin);
        Assert.Equal(BudgetValueOrigin.Configured, configured.View.MaxTotalStepsOrigin);

        var defaulted = new RunModel();
        var admittedDefault = defaulted.AdmitContract(RenewalContract());
        Assert.True(admittedDefault.Accepted);
        Assert.Equal(BudgetValueOrigin.Default, defaulted.View!.BudgetValueOrigin);
        Assert.Equal(BudgetValueOrigin.Default, defaulted.View.MaxConsultationsOrigin);
        Assert.Equal(BudgetValueOrigin.Default, defaulted.View.MaxTotalStepsOrigin);

        // 同内容重复 admit：同一 View 实例（幂等）+ origin 稳定（View 字节不变）
        var readmitted = configured.AdmitContract(RenewalContract(consultations: 4, totalSteps: 32));
        Assert.True(readmitted.Accepted);
        Assert.Same(configured.View, configured.View);
        Assert.Equal(BudgetValueOrigin.Configured, configured.View.BudgetValueOrigin);
    }

    [Fact]
    public void Admission_RecordsBudgetValueOriginPerDimension_ForPartialDefault()
    {
        var consultationsConfigured = new RunModel();
        Assert.True(consultationsConfigured.AdmitContract(
            RenewalContract(consultations: 4)).Accepted);
        Assert.Equal(BudgetValueOrigin.Configured,
            consultationsConfigured.View!.MaxConsultationsOrigin);
        Assert.Equal(BudgetValueOrigin.Default,
            consultationsConfigured.View.MaxTotalStepsOrigin);
        Assert.Equal(BudgetValueOrigin.Mixed, consultationsConfigured.View.BudgetValueOrigin);

        var stepsConfigured = new RunModel();
        Assert.True(stepsConfigured.AdmitContract(
            RenewalContract(totalSteps: 32)).Accepted);
        Assert.Equal(BudgetValueOrigin.Default,
            stepsConfigured.View!.MaxConsultationsOrigin);
        Assert.Equal(BudgetValueOrigin.Configured,
            stepsConfigured.View.MaxTotalStepsOrigin);
        Assert.Equal(BudgetValueOrigin.Mixed, stepsConfigured.View.BudgetValueOrigin);
    }

    [Fact]
    public void Admission_SameResolvedBudgets_RemainsIdempotent_AndOriginsStayImmutable()
    {
        var model = new RunModel();
        Assert.True(model.AdmitContract(RenewalContract(consultations: 16)).Accepted);
        var firstView = model.View;

        // Explicit default equals null at the budget signature level; the
        // original View and its per-dimension provenance remain unchanged.
        Assert.True(model.AdmitContract(RenewalContract(consultations: 16, totalSteps: 256)).Accepted);
        Assert.Same(firstView, model.View);
        Assert.Equal(BudgetValueOrigin.Configured, model.View!.MaxConsultationsOrigin);
        Assert.Equal(BudgetValueOrigin.Default, model.View.MaxTotalStepsOrigin);
        Assert.Equal(BudgetValueOrigin.Mixed, model.View.BudgetValueOrigin);
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

    [Fact]
    public void GoalDirectedReview_IsProactive_UsesInitialCeilingAndExplicitCorrelation()
    {
        var contexts = new List<AgentDecisionContext>();
        var reviewed = false;
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            if (ctx.BudgetReview is { } review)
            {
                reviewed = true;
                var assessment = new AgentProgressAssessment(
                    ctx.ExecutionOverview!.OverviewId, "progressing", "continue", "inspect remaining branch",
                    new[] { "settings-page-visible" }, new[] { "event:1" });
                return new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, 1),
                    ReviewResponse: new BudgetReviewResponse(review.ReviewId,
                        BudgetReviewOutcome.RequestRenewal, assessment,
                        new BudgetRenewalRequest(review.SuggestedAdditionalSteps,
                            review.SuggestedAdditionalRounds, "next branch")));
            }
            if (reviewed) return new AgentDecision.Blocked(ctx.DecisionId, "test-stop-after-proof");
            return new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, 1));
        }, RenewalContract(consultations: 112, totalSteps: 96), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true,
            RenewalThresholdRounds = 112,
            RenewalThresholdSteps = 96,
        });

        driver.Drive();

        var reviewContext = Assert.Single(contexts.Where(c => c.BudgetReview is not null));
        Assert.True(reviewContext.BudgetReview!.Required);
        Assert.Equal(20, reviewContext.BudgetReview.SuggestedAdditionalSteps);
        Assert.Equal(23, reviewContext.BudgetReview.SuggestedAdditionalRounds);
        var grant = Assert.Single(driver.RenewalsGranted.Where(r => r.Granted));
        Assert.Equal(reviewContext.BudgetReview.ReviewId, grant.ReviewId);
        Assert.Equal(20, grant.AdditionalSteps);
        Assert.Equal(23, grant.AdditionalRounds);
        Assert.Equal(112, reviewContext.ExecutionOverview!.InitialRounds);
        Assert.Equal(reviewContext.BudgetReview.RoundsRemaining, reviewContext.ExecutionOverview.RoundsRemaining);
        Assert.Equal(kernel.RunId, reviewContext.ExecutionOverview.RunId);
        var nextOverview = contexts.Last().ExecutionOverview!;
        Assert.Equal(reviewContext.Objective, nextOverview.PreviousSegmentExpectation);
        Assert.Equal("inspect remaining branch", nextOverview.CurrentSegmentExpectation);
        Assert.Null(contexts.Last().RenewalWindow);
    }

    [Fact]
    public void GoalDirectedReview_GivesOneCorrectionThenAuditsIncompleteWithoutGrant()
    {
        var contexts = new List<AgentDecisionContext>();
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            // Deliberately omit the mandatory explicit review response.
            return new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, 1));
        }, RenewalContract(consultations: 5, totalSteps: 20), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true,
        });

        for (var i = 0; i < 8 && !driver.RenewalsGranted.Any(r => !r.ReviewComplete); i++)
            driver.Drive();

        Assert.Equal(2, contexts.Count(c => c.BudgetReview is not null));
        var incomplete = Assert.Single(driver.RenewalsGranted.Where(r => !r.ReviewComplete));
        Assert.Equal("renewal:review-incomplete", incomplete.RejectionReason);
        Assert.False(incomplete.Granted);
        Assert.Equal(kernel.RunId, contexts.First(c => c.BudgetReview is not null).RunId);
    }

    [Fact]
    public void GoalDirectedReview_TwoTotalNoResponsesAreIncomplete_ThenRuntimeContinuesNormally()
    {
        var contexts = new List<AgentDecisionContext>();
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            return ctx.BudgetReview is null
                ? new AgentDecision.Blocked(ctx.DecisionId, "test-stop-after-incomplete")
                : null;
        }, RenewalContract(consultations: 5, totalSteps: 20), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true,
            RenewalThresholdRounds = 5,
        });

        driver.Drive();

        Assert.Equal(2, contexts.Count(c => c.BudgetReview is not null));
        var incomplete = Assert.Single(driver.RenewalsGranted.Where(r => !r.ReviewComplete));
        Assert.False(incomplete.Granted);
        Assert.Equal("renewal:review-incomplete", incomplete.RejectionReason);
        Assert.Equal(kernel.RunId, contexts[0].RunId);
    }

    [Fact]
    public void GoalDirectedContinuation_CanPassLegacy256WithoutAuthorizedCap()
    {
        var contexts = new List<AgentDecisionContext>();
        var sourceVersion = 0;
        var completedReviews = 0;
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            if (ctx.BudgetReview is { } review)
            {
                completedReviews++;
                if (completedReviews > 260)
                    return new AgentDecision.Blocked(ctx.DecisionId, "test-complete");
                var fact = $"owner-fact-{sourceVersion}";
                var assessment = new AgentProgressAssessment(
                    ctx.ExecutionOverview!.OverviewId, "new owner fact", "next branch", "inspect newly observed item",
                    new[] { "new item" }, new[] { fact });
                return new AgentDecision.Plan(ctx.DecisionId,
                    new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "new fact") }, "replan from new fact"),
                    new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal, assessment,
                        new BudgetRenewalRequest(0, 1, "new source fact")));
            }
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "continue") }, "continue"));
        }, RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true,
            RenewalThresholdRounds = 1,
        }, projectExecutionOverview: () =>
        {
            sourceVersion++;
            return new ExecutionOverviewProjection("branch-a", new[] { "item-a" }, Array.Empty<string>(),
                Array.Empty<string>(), "inspect item", null, new[] { $"owner-fact-{sourceVersion}" }, $"revision-{sourceVersion}",
                ProgressFactKeys: new[] { $"stable-progress-{sourceVersion}" });
        });
        driver.Drive();

        Assert.True(completedReviews > 256, $"reviews={completedReviews}, calls={contexts.Count}, last={driver.Drive()}");
        Assert.True(driver.RenewalsGranted.Count(r => r.Granted) > 256);
        Assert.Equal(kernel.RunId, contexts[0].RunId);
        Assert.True(contexts.Last().Progress!.RoundsUsed > 256);
    }

    [Fact]
    public void GoalDirectedReview_RepeatRouteWithOnlyNewObservationRefsIsRejected_AndReplanKeepsSegmentUsage()
    {
        var contexts = new List<AgentDecisionContext>();
        var sourceVersion = 0;
        var reviews = 0;
        var (kernel, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            if (ctx.BudgetReview is { } review)
            {
                reviews++;
                var assessment = new AgentProgressAssessment(
                    ctx.ExecutionOverview!.OverviewId, "no verified progress", "settings.general", "continue same route",
                    new[] { "settings.general" }, new[] { $"observation-{sourceVersion}" });
                return new AgentDecision.Plan(ctx.DecisionId,
                    new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "same route") }, "replan"),
                    new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal, assessment,
                        new BudgetRenewalRequest(0, 1, "same route again")));
            }
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "continue") }, "continue"));
        }, RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true,
            RenewalThresholdRounds = 1,
        }, projectExecutionOverview: () =>
        {
            sourceVersion++;
            return new ExecutionOverviewProjection("settings.general", new[] { "settings.general" }, Array.Empty<string>(),
                Array.Empty<string>(), "inspect same route", "no new verified items", new[] { $"observation-{sourceVersion}" },
                $"revision-{sourceVersion}", ProgressFactKeys: new[] { "stable:settings.general:verified-none" });
        });

        driver.Drive();

        Assert.True(reviews >= 2);
        Assert.Single(driver.RenewalsGranted.Where(r => r.Granted));
        Assert.Contains(driver.RenewalsGranted,
            r => r.RejectionReason == "renewal:repeat-route-without-new-evidence");
        var secondReview = contexts.Last(c => c.BudgetReview is not null);
        Assert.Equal(1, secondReview.ExecutionOverview!.SegmentRoundsConsumed);
        Assert.Equal(2, secondReview.ExecutionOverview.RoundsConsumed);
        Assert.Equal(kernel.RunId, secondReview.ExecutionOverview.RunId);
    }

    [Fact]
    public void GoalDirectedReview_ReappearingOldFactsDoNotFundAnotherSegment()
    {
        var reviews = 0;
        var sourceVersion = 0;
        var (_, driver, _) = Compose(ctx =>
        {
            if (ctx.BudgetReview is not { } review || ++reviews > 4)
                return new AgentDecision.Blocked(ctx.DecisionId, "end probe");
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "retry") }, "retry"),
                new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                    new AgentProgressAssessment(ctx.ExecutionOverview!.OverviewId, "known facts", "same route",
                        "inspect existing page", new[] { "existing page" }, new[] { "owner:known-page" }),
                    new BudgetRenewalRequest(0, 1, "same route")));
        }, RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true, RenewalThresholdRounds = 2,
        }, projectExecutionOverview: () => new ExecutionOverviewProjection(
            "same branch", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, null,
            new[] { $"observation:{++sourceVersion}" },
            ProgressFactKeys: new[] { sourceVersion % 2 == 1 ? "known:a" : "known:b" }));

        driver.Drive();

        Assert.Equal(2, driver.RenewalsGranted.Count(record => record.Granted));
        Assert.Contains(driver.RenewalsGranted,
            record => record.RejectionReason == "renewal:repeat-route-without-new-evidence");
    }

    [Fact]
    public void GoalDirectedReview_RewordingDoesNotRenewButGroundedNewItemCan()
    {
        var reviews = 0;
        var (_, driver, _) = Compose(ctx =>
        {
            if (ctx.BudgetReview is not { } review || ++reviews > 4)
                return new AgentDecision.Blocked(ctx.DecisionId, "end probe");
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "retry") }, "retry"),
                new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                    new AgentProgressAssessment(ctx.ExecutionOverview!.OverviewId, "no new facts", $"different words {reviews}",
                        "inspect pending item", new[] { "verified pending item" }, new[] { "owner:pending" },
                        NextItemKey: reviews == 2 ? "pending:b" : "pending:a"),
                    new BudgetRenewalRequest(0, 1, "bounded inspection")));
        }, RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true, RenewalThresholdRounds = 2,
        }, projectExecutionOverview: () => new ExecutionOverviewProjection(
            "same branch", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, null,
            new[] { "owner:pending" }, ProgressFactKeys: new[] { "pending:known" },
            ActionableItems: new Dictionary<string, ActionableItem>
            {
                ["pending:a"] = new(new[] { "owner:pending" }, new[] { "pending:known" }), ["pending:b"] = new(new[] { "owner:pending" }, new[] { "pending:known" }),
            }));

        driver.Drive();

        Assert.Equal(2, driver.RenewalsGranted.Count(record => record.Granted));
        Assert.Contains(driver.RenewalsGranted,
            record => record.RejectionReason == "renewal:repeat-route-without-new-evidence");
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 4)]
    public void GoalDirectedReview_OnlyRelatedProgressCanRepeatedlyFundSameItem(bool relatedProgress, int expectedGrants)
    {
        var reviews = 0;
        var projectionVersion = 0;
        var (_, driver, _) = Compose(ctx =>
        {
            if (ctx.BudgetReview is not { } review || ++reviews > 4)
                return new AgentDecision.Blocked(ctx.DecisionId, "end probe");
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "retry") }, "retry"),
                new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                    new AgentProgressAssessment(ctx.ExecutionOverview!.OverviewId, "unrelated facts changed", "same item",
                        "inspect a", new[] { "item a" }, new[] { "owner:a" }, NextItemKey: "pending:a"),
                    new BudgetRenewalRequest(0, 1, "retry a")));
        }, RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true, RenewalThresholdRounds = 2,
        }, projectExecutionOverview: () => new ExecutionOverviewProjection(
            "branch", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, null,
            new[] { "owner:a" }, ProgressFactKeys: new[] { "known:a", $"unrelated:b:{++projectionVersion}" },
            ActionableItems: new Dictionary<string, ActionableItem>
            { ["pending:a"] = new(new[] { "owner:a" }, new[] { relatedProgress ? $"known:a:{projectionVersion}" : "known:a" }) }));

        driver.Drive();

        Assert.Equal(expectedGrants, driver.RenewalsGranted.Count(record => record.Granted));
        if (!relatedProgress)
            Assert.Contains(driver.RenewalsGranted,
                record => record.RejectionReason == "renewal:repeat-route-without-new-evidence");
    }

    [Fact]
    public void GoalDirectedReview_RejectsItemMissingFromCurrentOwnerProjection()
    {
        var (_, driver, _) = Compose(ctx => new AgentDecision.Defer(ctx.DecisionId, new ObserveSpec(null, 1),
            ReviewResponse: new BudgetReviewResponse(ctx.BudgetReview!.ReviewId, BudgetReviewOutcome.RequestRenewal,
                new AgentProgressAssessment(ctx.ExecutionOverview!.OverviewId, "continue", "new branch", "inspect",
                    new[] { "page" }, new[] { "owner:pending" }, NextItemKey: "invented-or-closed"),
                new BudgetRenewalRequest(0, 1, "continue"))),
            RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
            {
                GoalDirectedContinuationEnabled = true, RenewalThresholdRounds = 2,
            }, projectExecutionOverview: () => new ExecutionOverviewProjection(
                "branch", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, null,
                new[] { "owner:pending" }, ActionableItems: new Dictionary<string, ActionableItem>
                { ["pending:a"] = new(new[] { "owner:pending" }, new[] { "pending:known" }) }));

        driver.Drive();

        Assert.DoesNotContain(driver.RenewalsGranted, record => record.Granted);
        Assert.Contains(driver.RenewalsGranted,
            record => record.RejectionReason == "renewal:next-item-not-actionable");
    }

    [Fact]
    public void GoalDirectedReview_StepOnlyGrantDoesNotOpenAnotherReviewWithOneRoundLeft()
    {
        var contexts = new List<AgentDecisionContext>();
        var (_, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "continue") }, "continue"),
                ctx.BudgetReview is { } review ? new BudgetReviewResponse(review.ReviewId,
                    BudgetReviewOutcome.RequestRenewal, new AgentProgressAssessment(ctx.ExecutionOverview!.OverviewId,
                        "continue", "inspect", "new evidence", new[] { "new observation" }, new[] { "owner:1" }),
                    new BudgetRenewalRequest(1, 0, "steps only")) : null);
        }, RenewalContract(consultations: 2, totalSteps: 1), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true, RenewalThresholdRounds = 2,
        });

        driver.Drive();

        Assert.Equal(2, contexts.Count);
        Assert.Single(contexts.Where(context => context.BudgetReview is not null));
        Assert.Single(driver.RenewalsGranted.Where(record => record.Granted));
    }

    [Fact]
    public void GoalDirectedReview_ExplicitLowThresholdStillReservesPaidCorrection()
    {
        var contexts = new List<AgentDecisionContext>();
        var (_, driver, _) = Compose(ctx =>
        {
            contexts.Add(ctx);
            return new AgentDecision.Plan(ctx.DecisionId,
                new AgentPlanProposal(new PlanItem[] { new PlanItem.ControlItem(PlanControlKind.Replan, "continue") }, "continue"));
        }, RenewalContract(consultations: 5, totalSteps: 20), new AgentPlanPolicy
        {
            GoalDirectedContinuationEnabled = true, RenewalThresholdRounds = 1,
        });

        driver.Drive();

        var reviewContexts = contexts.Where(context => context.BudgetReview is not null).ToArray();
        Assert.Equal(new[] { 1, 2 }, reviewContexts.Select(context => context.BudgetReview!.Attempt));
        // Context accounting already includes the current paid consultation.
        Assert.Equal(1, reviewContexts[0].BudgetReview!.RoundsRemaining);
        Assert.Equal(0, reviewContexts[1].BudgetReview!.RoundsRemaining);
        Assert.Equal(5, contexts.Count);
        Assert.Single(driver.RenewalsGranted.Where(record => !record.ReviewComplete));
    }

    [Fact]
    public void GoalDirectedReview_RespectsExplicitAuthorizedFinalTotal()
    {
        var (kernel, driver, _) = Compose(context => context.BudgetReview is { } review
            ? new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, 1),
                ReviewResponse: new BudgetReviewResponse(review.ReviewId, BudgetReviewOutcome.RequestRenewal,
                    new AgentProgressAssessment(context.ExecutionOverview!.OverviewId, "continue", "next branch",
                        "observe allowed branch", new[] { "fresh branch observation" }, new[] { "event:1" }),
                    new BudgetRenewalRequest(20, 0, "next branch")))
            : new AgentDecision.Blocked(context.DecisionId, "end probe"),
            RenewalContract(consultations: 112, totalSteps: 96), new AgentPlanPolicy
            {
                GoalDirectedContinuationEnabled = true, RenewalThresholdSteps = 96,
                AuthorizedTotalSteps = 100,
            });
        driver.Drive();
        var record = Assert.Single(driver.RenewalsGranted);
        Assert.False(record.Granted);
        Assert.Equal("renewal:hardcap-exceeded:steps", record.RejectionReason);
        Assert.Equal(96, kernel.RunView!.MaxTotalSteps);
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
