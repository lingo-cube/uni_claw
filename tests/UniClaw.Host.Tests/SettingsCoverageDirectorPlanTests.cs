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

    private static AgentDecisionContext Context(string decisionId = "d-1") =>
        new(
            DecisionId: decisionId,
            RunId: "run-1",
            ContractVersion: "v0",
            Objective: "traverse settings",
            AllowedEffects: new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" },
            CurrentWorldClaims: new Dictionary<string, ClaimSummary>(),
            PendingObligations: Array.Empty<AgentObligationView>(),
            Phase: AgentDecisionPhase.StepVerified);

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
        var director = new SettingsCoverageDirector(
            _ => Act(Entry), ledger, Config());

        var decision = director.Consult(Context());

        Assert.IsType<AgentDecision.Defer>(decision);
        Assert.Equal("defer-unknown-page", Assert.Single(director.ConsultLog).DirectiveKind);
    }
}
