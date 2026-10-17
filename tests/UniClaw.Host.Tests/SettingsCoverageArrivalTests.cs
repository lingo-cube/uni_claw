using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// TRAJECTORY-001 D2 — 到达即完成（arrival detection）单元测试：我们自己的
/// enter-tap 之后弹窗立即出现（prior absent → present），且对话框标题与
/// 被进入目标模糊匹配（词重叠 ≥50%，& ≈ and）→ 该目标已到达目标页面
/// （ReachedOverlay 落账）+ 安全 dismiss（清障词表/返回键）继续遍历；
/// 不匹配或无 prior enter-tap → 走既有 obstacle 清障逻辑。
/// </summary>
public sealed class SettingsCoverageArrivalTests
{
    private const string RootRoute = "android.settings|route:Settings";
    private const string Entry = "SIM name & color";
    private const string ScrollContainer = "com.android.settings:id/main_content_scrollable_container";

    private static SettingsCoverageConfig Config() =>
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
            BackDescriptor: "Navigate up");

    private static AgentDecisionContext Context(string decisionId = "d-1") =>
        new(
            DecisionId: decisionId,
            RunId: "run-1",
            ContractVersion: "v0",
            Objective: "traverse settings",
            AllowedEffects: new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" },
            CurrentWorldClaims: new Dictionary<string, ClaimSummary>(),
            PendingObligations: Array.Empty<AgentObligationView>(),
            Phase: AgentDecisionPhase.StepVerified,
            Progress: null);

    private static AgentDecision Act(string descriptor, string effectClass = "tap") =>
        new AgentDecision.Act(new AgentActionProposal(
            "d-x",
            new[] { new AgentActionStep("ui.element", descriptor, effectClass, null) },
            "following directive"));

    /// <summary>第一轮：根页可见 + 无弹窗 → enter 指令被采纳（enter-tap 完成）。</summary>
    private static (SettingsCoverageLedger Ledger, SettingsCoverageDirector Director) AdoptEnterTap(
        Func<string?>? dialogTitleSource)
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", Entry), ("ui.element", "CANCEL") });
        // 与真实 feed 同律：无弹窗周期也显式声明 absent（arrival 判据要求
        // prior popupState = absent 的成对声明）。
        ledger.RecordPopupState("absent");
        var calls = 0;
        var director = new SettingsCoverageDirector(
            _ => ++calls == 1 ? Act(Entry) : Act("CANCEL"),
            ledger, Config(), dialogTitleSource: dialogTitleSource);
        var first = director.Consult(Context("d-enter"));
        Assert.IsType<AgentDecision.Act>(first);
        Assert.Equal("enter", director.ConsultLog[^1].DirectiveKind);
        return (ledger, director);
    }

    [Fact]
    public void PopupRightAfterEnterTap_TitleMatches_MarksReachedOverlay_AndDismisses()
    {
        // "SIM label and color" vs "SIM name & color"：交集 {sim,and,color}，
        // 并集 {sim,label,and,name,color} → 3/5 = 60% ≥ 50% → 匹配。
        var (ledger, director) = AdoptEnterTap(() => "SIM label and color");
        ledger.RecordPopupState("present");

        var decision = director.Consult(Context("d-arrival"));

        // 到达事实落账：ReachedOverlay（零 Effect mark）。
        Assert.True(ledger.TargetMarks.TryGetValue(Entry, out var mark));
        Assert.Equal(TargetCoverageMarkKind.ReachedOverlay, mark.Kind);
        // 审计留痕：arrival-detected 咨询记录。
        Assert.Contains(director.ConsultLog, record => record.DirectiveKind == "arrival-detected");
        // 处置：安全 dismiss（CANCEL 在清障词表内），不是 confirm 侧。
        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("CANCEL", act.Proposal.Steps[0].TargetDescriptor);
    }

    [Fact]
    public void PopupAfterEnterTap_TitleMismatch_FallsThroughToObstacleLogic()
    {
        var (ledger, director) = AdoptEnterTap(() => "Battery optimization");
        ledger.RecordPopupState("present");

        var decision = director.Consult(Context("d-arrival"));

        Assert.False(ledger.TargetMarks.ContainsKey(Entry));
        Assert.DoesNotContain(director.ConsultLog, record => record.DirectiveKind == "arrival-detected");
        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("CANCEL", act.Proposal.Steps[0].TargetDescriptor);
        Assert.Equal("obstacle", director.ConsultLog[^1].DirectiveKind);
    }

    [Fact]
    public void PopupAtFirstObservation_NoPriorEnterTap_ObstacleLogic()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(RootRoute,
            new (string, string?)[] { ("ui.element", Entry), ("ui.element", "CANCEL") });
        ledger.RecordPopupState("present");
        var director = new SettingsCoverageDirector(
            _ => Act("CANCEL"), ledger, Config(), dialogTitleSource: () => Entry);

        var decision = director.Consult(Context("d-first"));

        Assert.False(ledger.TargetMarks.ContainsKey(Entry));
        Assert.DoesNotContain(director.ConsultLog, record => record.DirectiveKind == "arrival-detected");
        var act = Assert.IsType<AgentDecision.Act>(decision);
        Assert.Equal("CANCEL", act.Proposal.Steps[0].TargetDescriptor);
        Assert.Equal("obstacle", director.ConsultLog[^1].DirectiveKind);
    }
}
