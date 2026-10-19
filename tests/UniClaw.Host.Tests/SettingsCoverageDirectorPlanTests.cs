using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-009 §11 — Director 受约束分支的单元级确定性测试（直接调 Consult；
/// driver 侧 Plan 执行不在本分支 HEAD，故不做返回 Plan 的端到端 Drive 测试）。
/// 覆盖：obstacle 分支出现/消失、bounded-stop:popup-not-cleared、受约束 Plan
/// 接受（单 ActItem + observe/control 尾）、多 ActItem / 失配 / 超尺寸
/// fail closed、未知页 Defer 保留。
/// </summary>
public sealed class SettingsCoverageDirectorPlanTests
{
    private const string RootRoute = "android.settings|route:Settings";
    private const string ScrollContainer = "com.android.settings:id/main_content_scrollable_container";
    private const string Entry = "Network & internet";

    private static SettingsCoverageConfig Config(
        int maxObstacleRetries = 2,
        IReadOnlyList<string>? obstacleTargets = null) =>
        new(
            ConfigVersion: "1",
            Session: new CoverageSessionConfig("t", "ws", WorkspaceReuse: true, AutoCloseTurn: false),
            Bounds: new CoverageBounds(MaxSteps: 24, MaxConsultRounds: 24, MaxScrolls: 4,
                MaxConsecutiveFailures: 3, MaxDirectiveRetries: 1),
            Coverage: new CoverageRequirements(RootPage: true, FirstLevelMode: "all-visible",
                ScrollDiscoveredEntries: 1, SecondLevelPages: 2, BackNavigation: true, RepeatedEntries: 1),
            TargetPages: new[] { Entry },
            Termination: new CoverageTermination(true, true, true, true),
            RootRoute: RootRoute,
            ScrollContainerDescriptor: ScrollContainer,
            BackDescriptor: "Navigate up",
            Popup: new PopupClearanceConfig(ObstacleTargets: obstacleTargets ?? new[] { "CANCEL", "button2", "DISMISS", "button1" },
                MaxObstacleRetries: maxObstacleRetries));

    private static AgentDecisionContext Context(
        string decisionId = "d-1",
        ConsultationProgress? progress = null) =>
        new(
            DecisionId: decisionId,
            RunId: "run-1",
            ContractVersion: "v0",
            Objective: "traverse settings",
            AllowedEffects: new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" },
            CurrentWorldClaims: new Dictionary<string, ClaimSummary>(),
            PendingObligations: Array.Empty<AgentObligationView>(),
            Phase: AgentDecisionPhase.StepVerified,
            Progress: progress);

    private static AgentDecision Act(string descriptor, string effectClass = "tap") =>
        new AgentDecision.Act(new AgentActionProposal(
            "d-1",
            new[] { new AgentActionStep("ui.element", descriptor, effectClass, null) },
            "following directive"));

    private static AgentDecision Plan(params PlanItem[] items) =>
        new AgentDecision.Plan("d-1", new AgentPlanProposal(items, "plan"));

    private static SettingsCoverageLedger NormalRootLedger()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", Entry), ("ui.element", "CANCEL") });
        return ledger;
    }

    private static SettingsCoverageLedger PopupLedger()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", Entry), ("ui.element", "CANCEL"), ("ui.element", "button1") });
        ledger.RecordPopupState("present");
        return ledger;
    }

    [Fact]
    public void PopupPresent_IssuesObstacleDirective_FromConfigVocabulary()
    {
        var director = new SettingsCoverageDirector(
            _ => Act("CANCEL"), PopupLedger(), Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Act>(decision);
        var record = Assert.Single(director.ConsultLog);
        Assert.Equal("obstacle", record.DirectiveKind);
        Assert.Contains("CANCEL", record.Directive);
        var adopted = Assert.Single(director.AdoptedSteps);
        Assert.Equal("CANCEL", adopted.Step.TargetDescriptor);
    }

    [Fact]
    public void PopupAbsent_ResumesNormalTraversal()
    {
        var ledger = PopupLedger();
        // 状态化桩：第一轮回 CANCEL（清障），清障后回正常 enter 目标。
        var calls = 0;
        var director = new SettingsCoverageDirector(
            _ => ++calls == 1 ? Act("CANCEL") : Act(Entry), ledger, Config());
        Assert.Equal("obstacle", director.Consult(Context("d-1")) is AgentDecision.Act ? "obstacle" : "");

        // 弹窗清障成功：声明回到 absent → 下一次咨询回到普通 enter 指令。
        ledger.RecordPopupState("absent");
        var second = director.Consult(Context("d-2"));
        Assert.IsType<AgentDecision.Act>(second);
        Assert.Equal("enter", director.ConsultLog[^1].DirectiveKind);
        Assert.Contains(Entry, director.ConsultLog[^1].Directive);
    }

    [Fact]
    public void PopupStillPresent_AfterRetries_StopsBounded_FailClosed()
    {
        var ledger = PopupLedger();
        var director = new SettingsCoverageDirector(_ => Act("CANCEL"), ledger, Config(maxObstacleRetries: 2));
        Assert.NotNull(director.Consult(Context("d-1")));
        Assert.NotNull(director.Consult(Context("d-2")));

        // 第三次咨询：重试预算尽且 popup 仍 present → NoAction fail closed。
        var third = director.Consult(Context("d-3"));
        Assert.IsType<AgentDecision.NoAction>(third);
        Assert.Equal("bounded-stop:popup-not-cleared", director.TerminalJustification);
        Assert.Contains(director.ConsultLog, r => r.Justification == "bounded-stop:popup-not-cleared");
    }

    [Fact]
    public void ConstrainedPlan_SingleMatchingAct_WithObserveControlTail_IsAccepted()
    {
        var director = new SettingsCoverageDirector(
            _ => Plan(
                new PlanItem.ActItem("ui.element", Entry, "tap", null),
                new PlanItem.ObserveItem(null),
                new PlanItem.ControlItem(PlanControlKind.Reobserve, "confirm entry")),
            NormalRootLedger(), Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Plan>(decision);
        var adopted = Assert.Single(director.AdoptedSteps);
        Assert.Equal(Entry, adopted.Step.TargetDescriptor);
        Assert.Equal("plan", Assert.Single(director.ConsultLog).DecisionKind);
    }

    [Fact]
    public void AdvisorySkip_IsLoggedAsSkip_WithoutAdoptingAnEffectStep()
    {
        var director = new SettingsCoverageDirector(
            _ => new AgentDecision.Skip("d-1", Entry, "unsupported dialog", ["evidence:dialog"]),
            NormalRootLedger(), Config());

        var decision = director.RecordAdvisoryDecision(Context(),
            new AgentDecision.Skip("d-1", Entry, "unsupported dialog", ["evidence:dialog"]));

        Assert.IsType<AgentDecision.Skip>(decision);
        Assert.Equal("skip", Assert.Single(director.ConsultLog).DecisionKind);
        Assert.Empty(director.AdoptedSteps);
    }

    [Fact]
    public void Plan_WithMultipleActItems_FailsClosed()
    {
        var director = new SettingsCoverageDirector(
            _ => Plan(
                new PlanItem.ActItem("ui.element", Entry, "tap", null),
                new PlanItem.ActItem("ui.element", "CANCEL", "tap", null)),
            NormalRootLedger(), Config());

        Assert.Null(director.Consult(Context()));
        Assert.Equal("plan-multiple-acts", Assert.Single(director.ConsultLog).DeviationReason);
        Assert.Empty(director.AdoptedSteps);
    }

    [Fact]
    public void Plan_WithMismatchedAct_FailsClosed()
    {
        var director = new SettingsCoverageDirector(
            _ => Plan(
                new PlanItem.ActItem("ui.element", "Wrong target", "tap", null),
                new PlanItem.ObserveItem(null)),
            NormalRootLedger(), Config());

        Assert.Null(director.Consult(Context()));
        Assert.StartsWith("plan-directive-target-mismatch", Assert.Single(director.ConsultLog).DeviationReason);
    }

    [Fact]
    public void Plan_Oversized_FailsClosed()
    {
        var director = new SettingsCoverageDirector(
            _ => Plan(
                new PlanItem.ActItem("ui.element", Entry, "tap", null),
                new PlanItem.ObserveItem(null),
                new PlanItem.ObserveItem(null),
                new PlanItem.ObserveItem(null),
                new PlanItem.ObserveItem(null)),
            NormalRootLedger(), Config());

        Assert.Null(director.Consult(Context()));
        Assert.Equal("plan-oversized:5", Assert.Single(director.ConsultLog).DeviationReason);
    }

    [Fact]
    public void Plan_ActOnlyWithoutTail_IsAccepted()
    {
        var director = new SettingsCoverageDirector(
            _ => Plan(new PlanItem.ActItem("ui.element", Entry, "tap", null)),
            NormalRootLedger(), Config());

        Assert.IsType<AgentDecision.Plan>(director.Consult(Context()));
    }

    [Fact]
    public void UnknownPage_DeferPath_StillWorks()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var consultations = 0;
        var director = new SettingsCoverageDirector(
            context =>
            {
                consultations++;
                return new AgentDecision.Defer(context.DecisionId,
                    new ObserveSpec(Subject: null, MaxRounds: 2));
            }, ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Equal(1, consultations);
        Assert.Equal("defer-unknown-page", Assert.Single(director.ConsultLog).DirectiveKind);
    }

    [Fact]
    public void UnknownPage_RejectsAgentActWithoutEffect()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var director = new SettingsCoverageDirector(
            _ => Act(Entry), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Equal("act", director.ConsultLog.Single().DecisionKind);
        Assert.Equal("unknown-route-agent-response-rejected",
            director.ConsultLog.Single().DeviationReason);
    }

    [Fact]
    public void PopupObstacle_PrecedesUnknownRouteDefer()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            new[] { ("ui.element", (string?)"CANCEL") });
        ledger.RecordPopupState("present");
        var director = new SettingsCoverageDirector(
            _ => Act("CANCEL"), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("obstacle", Assert.Single(director.ConsultLog).DirectiveKind);
    }

    // AGT-019 M4（Q5 裁决）：弹窗处置任务型白名单——遍历型任务没有
    // confirm 权限。确认侧目标（button1）永不进入清障指令：安全词可用
    // 用安全词；无安全词用返回键 dismiss；返回键也缺配置 → 暂停+重授权
    // （bounded stop），绝不把 confirm 侧按钮交给 tap。

    [Fact]
    public void PopupWithOnlyConfirmTarget_DismissesByBack_NeverTapsConfirm()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", "button1") });
        ledger.RecordPopupState("present");
        var director = new SettingsCoverageDirector(
            _ => Act("Navigate up"), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Act>(decision);
        var record = Assert.Single(director.ConsultLog);
        Assert.Equal("obstacle", record.DirectiveKind);
        Assert.DoesNotContain("button1", record.Directive);
        Assert.Equal("Navigate up", Assert.Single(director.AdoptedSteps).Step.TargetDescriptor);
    }

    [Fact]
    public void PopupWithOnlyConfirmTarget_AndNoBack_StopsBounded_ForReauthorization()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", "button1") });
        ledger.RecordPopupState("present");
        var config = Config() with { BackDescriptor = null };
        var director = new SettingsCoverageDirector(
            _ => Act("button1"), ledger, config);

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.NoAction>(decision);
        Assert.Equal("bounded-stop:popup-confirm-requires-reauthorization",
            director.TerminalJustification);
        Assert.Empty(director.AdoptedSteps);
    }

    [Fact]
    public void PopupObstacle_SkipsDeniedConfirmTarget_EvenIfListed()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", "button1"), ("ui.element", "DISMISS") });
        ledger.RecordPopupState("present");
        var director = new SettingsCoverageDirector(
            _ => Act("DISMISS"), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Act>(decision);
        var record = Assert.Single(director.ConsultLog);
        Assert.Equal("obstacle", record.DirectiveKind);
        Assert.Contains("DISMISS", record.Directive);
        Assert.DoesNotContain("button1", record.Directive);
    }

    // AGT-019 M1（Q7 裁决）：处理不了的默认升级 UniAgent——路由不明是
    // 强制升级谓词之一。升级咨询给选项集 {再观察, back（配置了才给）,
    // 滚动（容器可见才给）}；选项外的 Act/Plan/Policy 照拒、零 Effect
    // （004-A2 等价执法，返回安全 Defer）。

    [Fact]
    public void UnknownPage_AcceptsBackRecovery_WhenConfigured()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var director = new SettingsCoverageDirector(
            _ => Act("Navigate up"), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Act>(decision);
        var adopted = Assert.Single(director.AdoptedSteps);
        Assert.Equal("Navigate up", adopted.Step.TargetDescriptor);
        Assert.Equal("tap", adopted.Step.EffectClass);
        Assert.Null(Assert.Single(director.ConsultLog).DeviationReason);
    }

    [Theory]
    [InlineData(AgentDecisionPhase.VerificationFailed)]
    [InlineData(AgentDecisionPhase.StepRejected)]
    public void FailedStep_AcceptsObservedBackRecovery_InsteadOfRepeatingCoverageTarget(AgentDecisionPhase phase)
    {
        var ledger = NormalRootLedger();
        ledger.RecordObservation(RootRoute, new (string, string?)[]
        {
            ("ui.element", Entry), ("ui.element", "Navigate up"),
        });
        AgentDecisionContext? received = null;
        var renewal = new BudgetRenewalRequest(20, 24, "verified progress; remaining work");
        var director = new SettingsCoverageDirector(context =>
        {
            received = context;
            return new AgentDecision.Act(new AgentActionProposal(context.DecisionId,
                new[] { new AgentActionStep("ui.element", "Navigate up", "tap", null) },
                "back out after failed verification", renewal));
        }, ledger, Config());

        var decision = director.Consult(Context() with
        {
            Phase = phase, FailureReason = "post-action-target-unique", FailedStepIndex = 0,
        });

        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("Navigate up", Assert.Single(act.Proposal.Steps).TargetDescriptor);
        Assert.Equal(renewal, act.Proposal.Renewal);
        Assert.Contains("recovery", received!.Objective, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("directive-target-mismatch", director.ConsultLog[^1].DeviationReason ?? "");
        Assert.Equal("Navigate up", Assert.Single(director.AdoptedSteps).Step.TargetDescriptor);
    }

    [Fact]
    public void FailedStep_ReobservationKeepsRecovery_ThenReturnsToNormalTraversal()
    {
        var ledger = NormalRootLedger();
        ledger.RecordObservation(RootRoute, new (string, string?)[] { ("ui.element", Entry), ("ui.element", "Navigate up") });
        var answers = new Queue<Func<AgentDecisionContext, AgentDecision>>();
        answers.Enqueue(c => new AgentDecision.Defer(c.DecisionId, new ObserveSpec(null, 2)));
        answers.Enqueue(c => new AgentDecision.Act(new AgentActionProposal(c.DecisionId,
            new[] { new AgentActionStep("ui.element", "Navigate up", "tap", null) }, "recover")));
        answers.Enqueue(c => new AgentDecision.Act(new AgentActionProposal(c.DecisionId,
            new[] { new AgentActionStep("ui.element", Entry, "tap", null) }, "resume")));
        var director = new SettingsCoverageDirector(c => answers.Dequeue()(c), ledger, Config());
        Assert.IsType<AgentDecision.Defer>(director.Consult(Context() with
        {
            Phase = AgentDecisionPhase.VerificationFailed, FailureReason = "post-action-target-unique",
        }));
        // Kernel consumes FailureReason after a consultation and reuses StepVerified
        // for a bounded Defer re-observation; it does not expose a separate phase.
        var recovery = Assert.IsType<AgentDecision.Act>(director.Consult(Context("d-2")));
        Assert.Equal("Navigate up", Assert.Single(recovery.Proposal.Steps).TargetDescriptor);
        var resumed = Assert.IsType<AgentDecision.Act>(director.Consult(Context("d-3")));
        Assert.Equal(Entry, Assert.Single(resumed.Proposal.Steps).TargetDescriptor);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "on")]
    public void FailedStep_RejectsUnobservedOrStateChangingBackRecovery(bool visible, string? desiredState)
    {
        var ledger = NormalRootLedger();
        if (visible)
            ledger.RecordObservation(RootRoute, new (string, string?)[] { ("ui.element", Entry), ("ui.element", "Navigate up") });
        var director = new SettingsCoverageDirector(c => new AgentDecision.Act(new AgentActionProposal(
            c.DecisionId, new[] { new AgentActionStep("ui.element", "Navigate up", "tap", desiredState) }, "recover")),
            ledger, Config());
        var decision = director.Consult(Context() with
        {
            Phase = AgentDecisionPhase.VerificationFailed, FailureReason = "post-action-target-unique",
        });
        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Empty(director.AdoptedSteps);
    }

    [Fact]
    public void UnknownPage_AcceptsScrollRecovery_WhenContainerVisible()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            new (string, string?)[] { ("ui.element", ScrollContainer) });
        var director = new SettingsCoverageDirector(
            _ => Act(ScrollContainer, "swipe-up"), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Act>(decision);
        var adopted = Assert.Single(director.AdoptedSteps);
        Assert.Equal(ScrollContainer, adopted.Step.TargetDescriptor);
        Assert.Equal("swipe-up", adopted.Step.EffectClass);
        Assert.Null(Assert.Single(director.ConsultLog).DeviationReason);
    }

    [Fact]
    public void UnknownPage_BackNotConfigured_RejectsBackRecovery_WithZeroEffect()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var config = Config() with { BackDescriptor = null };
        var director = new SettingsCoverageDirector(
            _ => Act("Navigate up"), ledger, config);

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Empty(director.AdoptedSteps);
        Assert.Equal("unknown-route-agent-response-rejected",
            Assert.Single(director.ConsultLog).DeviationReason);
    }

    [Fact]
    public void UnknownPage_ScrollContainerNotVisible_RejectsScrollRecovery_WithZeroEffect()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var director = new SettingsCoverageDirector(
            _ => Act(ScrollContainer, "swipe-up"), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Empty(director.AdoptedSteps);
        Assert.Equal("unknown-route-agent-response-rejected",
            Assert.Single(director.ConsultLog).DeviationReason);
    }

    // AGT-019 M1/B1（Q1 裁决；RUN-004 D3/D7 修订通道）：blocked 诚实
    // 申报——同号采纳透传（Kernel 零 Effect 终局 agent-blocked:{reason}），
    // 异号照旧零 Effect 安全 Defer。

    [Fact]
    public void UnknownPage_AcceptsBlockedDeclaration_PassesThroughWithReason()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var director = new SettingsCoverageDirector(
            context => new AgentDecision.Blocked(
                context.DecisionId, "hierarchy unavailable on identity-only page"),
            ledger, Config());

        var decision = director.Consult(Context());

        var blocked = Assert.IsType<AgentDecision.Blocked>(decision);
        Assert.Equal("hierarchy unavailable on identity-only page", blocked.Reason);
        var record = Assert.Single(director.ConsultLog);
        Assert.Equal("blocked", record.DecisionKind);
        Assert.Equal("hierarchy unavailable on identity-only page", record.Justification);
        Assert.Empty(director.AdoptedSteps);
    }

    [Fact]
    public void UnknownPage_BlockedWithWrongDecisionId_RejectedWithZeroEffect()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity,
            Array.Empty<(string, string?)>());
        var director = new SettingsCoverageDirector(
            _ => new AgentDecision.Blocked("other-decision", "stale reason"),
            ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Empty(director.AdoptedSteps);
        Assert.Equal("unknown-route-agent-response-rejected",
            Assert.Single(director.ConsultLog).DeviationReason);
    }

    // RUNTIME-OVERVIEW-001：no-progress 双零窗口只提供复核事实，不能由
    // Host 计数替 Agent 作方向判断。

    [Fact]
    public void StagnantTraversal_InjectsNoProgressFacts_AfterWindow()
    {
        // B4：停滞事实升格为协议字段。Director 的 M5 用原始 StepsVerified
        //（含清障验证 → 弹窗场景恒涨不触发，F8 已证）。主防线已移至
        // InterleaveBookkeeper 覆盖指纹 + 目标尝试上限。本测试验证
        // Progress 字段协议在场且可访问。
        var progresses = new List<ConsultationProgress?>();
        var director = new SettingsCoverageDirector(
            context =>
            {
                progresses.Add(context.Progress);
                return Act(Entry);
            },
            NormalRootLedger(), Config());
        var first = director.Consult(Context("d-1",
            progress: new ConsultationProgress(1, 0, 0)));
        Assert.NotNull(first);
        Assert.NotNull(progresses[0]);
        Assert.IsType<int>(progresses[0]!.StagnantConsultations);
        Assert.IsType<int>(progresses[0]!.SameRouteConsultations);
    }

    [Fact]
    public void StagnantTraversal_ContinuesPastLegacyStopThreshold()
    {
        // 停滞硬停（bounded-stop:no-progress）在目标尝试上限（auto-skip）
        // 先行触发时退为后备——用非 enter 指令测试（obstacle 清障不计入
        // 目标尝试计数，停滞仍可触发）。
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", "CANCEL"), ("ui.element", "button1") });
        ledger.RecordPopupState("present");
        var consultations = 0;
        var director = new SettingsCoverageDirector(
            _ => { consultations++; return Act("CANCEL"); }, ledger, Config(maxObstacleRetries: 10));
        for (var i = 1; i <= 5; i++)
        {
            var result = director.Consult(Context($"d-{i}"));
            // 前 5 次都返回有效决策（obstacle 指令，不触发目标尝试上限）
            Assert.NotNull(result);
        }

        var sixth = director.Consult(Context("d-6"));

        Assert.IsType<AgentDecision.Act>(sixth);
        Assert.Equal(6, consultations);
        Assert.Null(director.TerminalJustification);
    }

    [Fact]
    public void MandatoryBudgetReview_UsesOneAgentCallAndPreservesReviewResponse()
    {
        var calls = 0;
        var review = new BudgetReviewResponse("review-1", BudgetReviewOutcome.ContinueWithoutRenewal);
        var config = Config() with
        {
            Bounds = Config().Bounds with { GoalDirectedContinuationEnabled = true, MaxDirectiveRetries = 3 },
        };
        var director = new SettingsCoverageDirector(
            _ =>
            {
                calls++;
                return new AgentDecision.Act(Assert.IsType<AgentDecision.Act>(Act(Entry)).Proposal, review);
            }, NormalRootLedger(), config);
        var context = Context("d-1") with
        {
            BudgetReview = new BudgetReview(
                "review-1", Required: true, Attempt: 1, Strengthened: false,
                SuggestedAdditionalSteps: 20, SuggestedAdditionalRounds: 23,
                InitialSteps: 96, InitialRounds: 112, GrantedSteps: 96, GrantedRounds: 112,
                StepsConsumed: 80, RoundsConsumed: 90, StepsRemaining: 16, RoundsRemaining: 22),
        };

        var decision = director.Consult(context);

        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal(1, calls);
        Assert.Same(review, act.ReviewResponse);
        Assert.Equal("budget-review", Assert.Single(director.ConsultLog).DirectiveKind);
    }

    [Fact]
    public void GoalDirectedConsult_AllowsAgentToDeepenCurrentBranchWithOtherRootTargetsPending()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", Entry), ("ui.element", "Battery") });
        ledger.RecordStep(new CoverageStepRecord(
            Index: 1, DecisionId: "enter-network", DshSessionId: null,
            TargetRole: "ui.element", TargetDescriptor: Entry, EffectClass: "tap",
            DesiredState: null, PreActionTargetUnique: true, ReceiptId: "receipt-network",
            ReceiptOutcome: "ok", ReceiptCommand: "tap", RouteBefore: RootRoute,
            RouteAfter: "android.settings|route:Network & internet", PostCaptureId: "capture-network",
            Verified: true, FailureReason: null, Directive: "enter"));
        ledger.RecordObservation("android.settings|route:Network & internet",
            new (string, string?)[] { ("ui.element", "Wi-Fi") });

        var calls = 0;
        var config = Config() with
        {
            Bounds = Config().Bounds with { GoalDirectedContinuationEnabled = true },
        };
        var director = new SettingsCoverageDirector(
            _ => { calls++; return Act("Wi-Fi"); }, ledger, config);

        var decision = director.Consult(Context("d-1"));

        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("Wi-Fi", act.Proposal.Steps.Single().TargetDescriptor);
        Assert.Equal(1, calls);
        Assert.Equal("goal-directed", Assert.Single(director.ConsultLog).DirectiveKind);
        Assert.Null(director.ConsultLog[0].Directive);
        Assert.Null(director.TerminalJustification);
        Assert.Contains(SettingsCoverageOverviewProjector.Project(ledger, config).KnownItems,
            item => item.Contains("[pending][dynamic-observation] Battery", StringComparison.Ordinal)
                && item.Contains("discovered=true", StringComparison.Ordinal));
    }

    [Fact]
    public void TargetAttemptLimit_AutoSkipsAfterMaxAttempts()
    {
        // TRAJECTORY-001：目标尝试上限——同目标派发超过 MaxTargetAttempts(3)
        // 次后自动跳过（零 Effect），下一轮选不同目标。
        var config = Config() with
        {
            Coverage = new CoverageRequirements(true, "all-visible", 0, 2, true, 1),
        };
        var director = new SettingsCoverageDirector(
            _ => Act(Entry), NormalRootLedger(), config);
        // 前 3 次正常派发
        for (var i = 1; i <= 3; i++)
            Assert.NotNull(director.Consult(Context($"d-{i}")));
        // 没有其他入口时诚实停止；跳过不能冒充重复进入或等待。
        Assert.IsType<AgentDecision.NoAction>(director.Consult(Context("d-4")));
        Assert.Null(director.ConsultLog[^1].DeviationReason);
        // 目标已被标记 skipped（justification 含目标名和 auto-skip 字样）
        Assert.Contains(director.ConsultLog, r =>
            (r.DirectiveKind == "skip" || r.DecisionKind == "skip")
            && (r.Justification ?? "").Contains("auto-skip")
            && (r.Justification ?? "").Contains(Entry));
    }

    [Fact]
    public void TargetAttemptLimit_AtRoot_SelectsNextTargetInSameConsultation()
    {
        var ledger = NormalRootLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", Entry), ("ui.element", "Apps") });
        var config = Config() with { TargetPages = new[] { Entry, "Apps" } };
        var director = new SettingsCoverageDirector(
            context => Act(context.Objective.Contains("<Apps>", StringComparison.Ordinal) ? "Apps" : Entry),
            ledger, config);
        for (var i = 1; i <= 3; i++)
            Assert.IsType<AgentDecision.Act>(director.Consult(Context($"d-{i}")));

        var next = Assert.IsType<AgentDecision.Act>(director.Consult(Context("d-4")));

        Assert.Equal("Apps", next.Proposal.Steps[0].TargetDescriptor);
        Assert.Contains("<Apps>", director.ConsultLog[^1].Directive);
        Assert.Contains(Entry, ledger.AgentSkippedTargets);
    }

    [Fact]
    public void RejectedSkipMark_IsNotLoggedAsSuccessfulSkip()
    {
        var ledger = NormalRootLedger();
        var director = new SettingsCoverageDirector(_ => Act(Entry), ledger, Config());
        for (var i = 1; i <= 3; i++)
            Assert.IsType<AgentDecision.Act>(director.Consult(Context($"d-{i}")));
        Assert.True(ledger.TryMarkTargetReached(Entry, TargetCoverageMarkKind.ReachedOverlay, "d-arrival"));

        Assert.IsType<AgentDecision.NoAction>(director.Consult(Context("d-4")));

        Assert.Equal("bounded-stop:coverage-mark-conflict", director.TerminalJustification);
        Assert.DoesNotContain(director.ConsultLog, record => record.DecisionKind == "skip");
        Assert.Equal(TargetCoverageMarkKind.ReachedOverlay, ledger.TargetMarks[Entry].Kind);
        Assert.Empty(ledger.AgentSkippedTargets);
    }

    [Fact]
    public void DeclaredSwitchTargets_AreExcludedBeforeConsultingNextNavigation()
    {
        var config = Config() with
        {
            TargetPages = new[] { "Wi-Fi", "Bluetooth", "Apps" },
            ActionPolicy = new SettingsActionPolicy(
                SettingsActionPolicy.SupportedSchemaVersion, "test-policy", 1, true,
                new HashSet<string> { "navigate", "back", "scroll" },
                new HashSet<string> { "toggle:Wi-Fi", "toggle:Bluetooth" },
                new HashSet<string> { "unknown-action" }, Array.Empty<string>(),
                SettingsActionPolicy.RejectUnknown, "test-digest", "test"),
        };
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute, config.TargetPages.Select(target => ("ui.element", (string?)target)).ToArray());
        string? receivedObjective = null;
        var director = new SettingsCoverageDirector(context =>
        {
            receivedObjective = context.Objective;
            return Act("Apps");
        }, ledger, config);

        var next = Assert.IsType<AgentDecision.Act>(director.Consult(Context()));

        Assert.Equal("Apps", next.Proposal.Steps[0].TargetDescriptor);
        Assert.Contains("<Apps>", receivedObjective);
        Assert.Equal(new[] { "Bluetooth", "Wi-Fi" }, ledger.AgentSkippedTargets);
        Assert.Single(director.AdoptedSteps);
    }

    [Theory]
    [InlineData("ui.element", true)]
    [InlineData("switch", false)]
    public void ObservedSwitchTarget_IsExcludedEvenWhenNotDeclaredToggle(string role, bool checkable)
    {
        var config = Config() with { TargetPages = new[] { "Roaming", "Apps" } };
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute, config.TargetPages.Select(target => ("ui.element", (string?)target)).ToArray());
        var director = new SettingsCoverageDirector(_ => Act("Apps"), ledger, config);
        var context = Context() with
        {
            Elements = new[]
            {
                new ElementSummary(role, "Roaming", null, true, checkable, true, ElementEpistemic.Observed),
                new ElementSummary("ui.element", "Apps", null, true, false, true, ElementEpistemic.Observed),
            },
        };

        var next = Assert.IsType<AgentDecision.Act>(director.Consult(context));

        Assert.Equal("Apps", next.Proposal.Steps[0].TargetDescriptor);
        Assert.Equal(new[] { "Roaming" }, ledger.AgentSkippedTargets);
        Assert.DoesNotContain(director.AdoptedSteps, item => item.Step.TargetDescriptor == "Roaming");
    }

    // AGT-019 M2-②（B3）：局部视图默认形态——容器结构（含可滚动）与
    // 近周期趋势有真源即载；诊断/视口源未接（诚实空）。

    [Fact]
    public void LocalView_DefaultForm_CarriesContainersAndTrends_HonestNullsForUnsourced()
    {
        var localViews = new List<LocalObservationView?>();
        var director = new SettingsCoverageDirector(
            context => { localViews.Add(context.LocalView); return Act(Entry); },
            NormalRootLedger(), Config());
        Assert.NotNull(director.Consult(Context("d-1")));
        Assert.NotNull(director.Consult(Context("d-2")));

        var view = localViews[1]!;
        Assert.NotNull(view);
        var container = Assert.Single(view.Containers);
        Assert.False(container.IsScrollable); // NormalRootLedger 无滚动容器可见
        Assert.Equal(2, view.RecentCycles!.Count);
        Assert.Contains("frontier=", view.RecentCycles[0]);
        Assert.Null(view.Diagnostics);
        Assert.Null(view.Viewport);
    }

    // AGT-019 M2-②/B3 seam：诊断/视口从感知管线最新周期（Runner 注入的
    // supplier）流入 LocalView——四族补缺闭环；supplier 缺席才诚实空。

    [Fact]
    public void LocalView_Seam_PopulatesDiagnosticsAndViewport_FromSuppliers()
    {
        var localViews = new List<LocalObservationView?>();
        var director = new SettingsCoverageDirector(
            context => { localViews.Add(context.LocalView); return Act(Entry); },
            NormalRootLedger(), Config(),
            localViewSources: () => (
                new ObservationDiagnostics(
                    HierarchyAvailable: true, FastAvailable: true, ProposalCount: 7,
                    FastLatencyMs: 120, HierarchyLatencyMs: 340),
                new ViewportSummary(1080, 2340)));
        Assert.NotNull(director.Consult(Context("d-1")));

        var view = localViews[0]!;
        Assert.NotNull(view);
        Assert.NotNull(view.Diagnostics);
        Assert.True(view.Diagnostics!.HierarchyAvailable);
        Assert.Equal(7, view.Diagnostics.ProposalCount);
        Assert.Equal(120, view.Diagnostics.FastLatencyMs);
        Assert.Equal(2340, view.Viewport!.Height);
    }
}
