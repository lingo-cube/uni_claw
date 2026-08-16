using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// PHYSICAL_SCROLL_CONTAINER_SEMANTIC_TRAVERSAL (BOUNDED_IMPLEMENTATION) falsifier proofs.
///
/// World shape: Developer options page (single semantic page identity) whose initial viewport
/// does NOT show "Automatic system updates"; one bounded ScrollForward reveals the
/// "Automatic system updates" toggle. The Agent must NOT guess SetSwitch before the target is
/// bound, must treat scroll as a same-Container viewport movement (NOT a Container transition),
/// must refresh its grounding from the fresh viewport only, and must let the external world win
/// when continuity fails.
///
/// The exploration evaluator is injected at the RunSemanticGoalAsync call boundary (NOT on
/// SemanticGoalInput). It is target-agnostic evidence interpretation: it reads the accumulated
/// viewport observations and returns continue/exhausted/unresolved. The Agent keeps sole decision
/// authority. Evaluator absent → navigation-only behavior (zero regression).
///
/// F1 target invisible → no guessed SetSwitch, scroll only when evaluator=true;
/// F2 scroll dispatched but viewport unchanged → exhausted → bounded stop (no fabricated progress);
/// F3 fresh viewport target still absent → another scroll only from fresh evaluator=true (no count);
/// F4 target appears → action grounded from fresh viewport only;
/// F5 scroll changes semantic page → same-Container rejected, external world authoritative;
/// F6 UNKNOWN after scroll → fail closed;
/// F7 target visible initially → ZERO ScrollForward;
/// F8 target visible but ambiguous → no guessed action.
/// </summary>
public sealed class PhysicalScrollContainerSemanticTraversalFalsifierTests
{
    private static readonly SemanticObject AutomaticSystemUpdates = SemanticObject.Define("AutomaticSystemUpdates", "SystemUpdateSetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "SystemUpdateSetting", "Enabled");
    private static readonly SemanticGoalInput Goal = new("AutomaticSystemUpdates", "Enabled", true); // turn ON

    private const string DeveloperOptions = "DeveloperOptions";
    private const string OtherPage = "OtherPage";

    // 现场校准几何：Automatic system updates 行 + 空文本 toggle（同一行带 → SPATIAL_RELATION 合并绑定）
    private static readonly ElementBounds AutomaticSystemUpdatesRowBounds = new(0.06f, 0.65f, 0.64f, 0.70f);
    private static readonly ElementBounds AutomaticSystemUpdatesToggleBounds = new(0.832f, 0.648f, 0.96f, 0.692f);

    private sealed class BuildSpec
    {
        public bool InitialHasTarget = false;      // F7: 初始视口即含 Automatic system updates
        public bool InitialSwitchState = false;    // toggle 初值（false = OFF）
        public bool ScrollSelfLoop = false;        // F2: 滚动分发成功但视口不变
        public bool TwoHops = false;               // F3: 需要两次滚动才出现目标
        public bool ScrollToOtherPage = false;     // F5: 滚动落到不同语义页面
        public bool ScrollToUnknown = false;       // F6: 滚动后观测 UNKNOWN
        public bool ScrolledSwitchUnknown = false; // F8: 目标可见但 SwitchState UNKNOWN
    }

    private sealed class Harness
    {
        public required RuntimeAgent Agent;
        public required ScriptedEnvironment Environment;
        public required RuntimeTraversal Traversal;
        public required List<RuntimeContainer> Containers;
        public required Func<Observation, string?> Resolver;
    }

    private static ElementConfig Menu(string text, ElementBounds? bounds = null)
        => new(text, null, null, bounds, "menuItem");

    private static ElementConfig Text(string text, ElementBounds? bounds = null)
        => new(text, null, null, bounds, "text");

    private static ElementConfig Toggle(bool? state, TransitionConfig? transition, ElementBounds bounds)
        => new("", state, transition, bounds, "toggle");

    private static TransitionConfig OnTransition()
        => new(ScreenTransitionAction.SetSwitch, "DeveloperScrolledOn", true);

    private static BuildSpec NormalSpec() => new();

    private static Harness Build(BuildSpec spec)
    {
        // ── DeveloperTop：初始视口（无 Automatic system updates；Developer options 是跨视口持久锚） ──
        var topElements = new List<ElementConfig>
        {
            Menu("Developer options", new ElementBounds(0.05f, 0.34f, 0.3f, 0.36f)),
            Menu("Memory", new ElementBounds(0.05f, 0.3f, 0.35f, 0.32f)),
            Menu("Bug report", new ElementBounds(0.05f, 0.26f, 0.4f, 0.28f)),
            Menu("Desktop backup password", new ElementBounds(0.05f, 0.38f, 0.3f, 0.40f)),
        };
        if (spec.InitialHasTarget)
        {
            topElements.Add(Text("Automatic system updates", AutomaticSystemUpdatesRowBounds));
            topElements.Add(Toggle(
                spec.InitialSwitchState,
                spec.InitialSwitchState ? null : OnTransition(),
                AutomaticSystemUpdatesToggleBounds));
        }

        string scrollTarget = spec.ScrollSelfLoop
            ? "DeveloperTop"
            : spec.ScrollToOtherPage
                ? OtherPage
                : spec.ScrollToUnknown
                    ? "Unknown"
                    : spec.TwoHops
                        ? "DeveloperMid"
                        : "DeveloperScrolled";

        var developerTop = new ScreenConfig("DeveloperTop", "settings", [.. topElements], new ViewportTransitionConfig(scrollTarget));

        // ── DeveloperMid：F3 中间视口（仍无目标，但内容推进 → evaluator 继续） ──
        var developerMid = new ScreenConfig("DeveloperMid", "settings",
        [
            Menu("Developer options", new ElementBounds(0.05f, 0.34f, 0.3f, 0.36f)),
            Menu("Stay awake", new ElementBounds(0.05f, 0.44f, 0.3f, 0.46f)),
            Menu("Select mock location app", new ElementBounds(0.05f, 0.48f, 0.4f, 0.50f)),
        ], new ViewportTransitionConfig("DeveloperScrolled"));

        // ── DeveloperScrolled：一次滚动后视口（目标出现；Developer options 仍持久 → 同容器） ──
        var developerScrolled = new ScreenConfig("DeveloperScrolled", "settings",
        [
            Menu("Developer options", new ElementBounds(0.05f, 0.34f, 0.3f, 0.36f)),
            Text("Automatic system updates", AutomaticSystemUpdatesRowBounds),
            Toggle(
                spec.ScrolledSwitchUnknown ? null : false,
                spec.ScrolledSwitchUnknown ? null : OnTransition(),
                AutomaticSystemUpdatesToggleBounds),
            Menu("Wireless debugging", new ElementBounds(0.05f, 0.52f, 0.3f, 0.54f)),
        ]);

        // ── DeveloperScrolledOn：SetSwitch(true) 后（目标 ON） ──
        var developerScrolledOn = new ScreenConfig("DeveloperScrolledOn", "settings",
        [
            Menu("Developer options", new ElementBounds(0.05f, 0.34f, 0.3f, 0.36f)),
            Text("Automatic system updates", AutomaticSystemUpdatesRowBounds),
            Toggle(true, null, AutomaticSystemUpdatesToggleBounds),
            Menu("Wireless debugging", new ElementBounds(0.05f, 0.52f, 0.3f, 0.54f)),
        ]);

        // ── 异页 / 未知页（F5 / F6） ──
        var other = new ScreenConfig(OtherPage, "settings",
        [
            Menu("Other", new ElementBounds(0.05f, 0.2f, 0.3f, 0.22f)),
        ]);
        var unknown = new ScreenConfig("Unknown", "settings",
        [
            Text("Something unknown"),
        ]);

        var env = new ScriptedEnvironment("DeveloperTop", "DeveloperTop",
            [developerTop, developerMid, developerScrolled, developerScrolledOn, other, unknown]);
        var traversal = new RuntimeTraversal(env);

        // 页面身份识别（resolver）：Developer options 是跨视口持久锚 → 两视口都唯一解析到 DeveloperOptions
        var identityCriteria = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(DeveloperOptions, ["Developer options"])
                .Add(OtherPage, ["Other"]));
        var resolver = CreateTestResolver(identityCriteria);

        var elementCriteria = new ElementBindingCriteria([AutomaticSystemUpdates],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));

        var startup = new RuntimeStartup(env, "settings", resolver);
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var containerFactory = new Func<string, RuntimeContainer>(page =>
        {
            var container = new RuntimeContainer(
                page,
                identityRule: observation => string.Equals(resolver(observation), page, StringComparison.Ordinal),
                stepExecutor: traversal.ExecuteStep);
            containers.Add(container);
            return container;
        });

        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), resolver, containerFactory, recovery, identityCriteria, elementCriteria);
        return new Harness { Agent = agent, Environment = env, Traversal = traversal, Containers = containers, Resolver = resolver };
    }

    /// <summary>测试侧页面身份识别器（与宿主 resolver 同构）：PageAnalysis 证据融合到唯一页面名或 null。</summary>
    private static Func<Observation, string?> CreateTestResolver(PageAnalysisCriteria identityCriteria)
        => observation =>
        {
            var evidence = PageAnalysis.Analyze(observation, identityCriteria);
            var candidates = new List<string>();
            foreach (var item in evidence)
            {
                if (!item.Claim.StartsWith("page is ", StringComparison.Ordinal))
                    continue;
                if ((item.Source == "TEXT_ANCHOR" || item.Source == "SWITCH_DISTRIBUTION")
                    && item.Stance == SemanticEvidenceStance.Supports)
                {
                    var pageName = item.Claim["page is ".Length..];
                    if (!candidates.Contains(pageName, StringComparer.Ordinal))
                        candidates.Add(pageName);
                }
            }
            return candidates.Count == 1 ? candidates[0] : null;
        };

    // ── 评估器（host 注入的确定性有界判据；只读累积视口证据，Agent 仍独占决策） ──

    /// <summary>F1/F4/F5/F6/E2E：目标在当前视口缺席 → 授权 ONE bounded step。</summary>
    private static ViewportExplorationEvidence ContinueOnce(ImmutableArray<Observation> _)
        => new(true, "target absent in current viewport; one bounded step is justified");

    /// <summary>F2/F3：视口内容推进且目标仍缺席 → 继续；视口不变 → exhausted。
    /// 同一判据在 F2 只产出一次滚动（视口不变即止）、在 F3 产出两次滚动（内容持续推进）——
    /// 滚动次数是证据涌现结果，不是硬编码计数。</summary>
    private static ViewportExplorationEvidence ContinueIfViewportChanged(ImmutableArray<Observation> observations)
    {
        if (observations.Length <= 1)
            return new ViewportExplorationEvidence(true, "first viewport lacks target; one bounded step is justified");
        var prev = observations[^2].Elements.Select(e => e.Text).ToImmutableHashSet(StringComparer.Ordinal);
        var curr = observations[^1].Elements.Select(e => e.Text).ToImmutableHashSet(StringComparer.Ordinal);
        var changed = !prev.SetEquals(curr);
        return new ViewportExplorationEvidence(
            changed,
            changed ? "viewport content advanced; exploration not exhausted" : "viewport unchanged; exploration exhausted");
    }

    // ── E2E：正向闭环 + 同容器连续性 + fresh 绑定 + 因果链 ──────────────────

    [Fact]
    public async Task S1E1_ScrollToTarget_FullLoop_FreshGoalEvidence_TraceChain()
    {
        var h = Build(NormalSpec());
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "s1e1", viewportExplorationEvaluator: ContinueOnce);
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.True(satisfied.Evidence.Satisfied);
        Assert.Equal(RunState.Completed, h.Agent.State);

        // 恰好一次 ScrollForward + 一次 SetSwitch
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        var setSwitch = Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.True(setSwitch.TargetState);
        Assert.Equal(AutomaticSystemUpdatesToggleBounds, setSwitch.TargetBounds);

        // 观察序列：startup(1) → initial(2) → 滚动后(3) → SetSwitch 后(4)
        Assert.Equal(new long[] { 1, 2, 3, 4 }, h.Environment.ObservationHistory.Select(o => o.SequenceNumber).ToArray());

        // 滚动 + SetSwitch 两个 Traversal step 均 Succeeded，滚动后 fresh 观测同容器
        var journal = h.Traversal.Journal;
        Assert.Equal(2, journal.Count);
        Assert.All(journal, e => Assert.IsType<TraversalStepResult.Succeeded>(e.Result));
        Assert.Equal(DeveloperOptions, h.Resolver(journal[0].PostActionObservation!));

        // 同容器连续性：滚动不创建新 Container（单容器贯穿全程）
        var container = Assert.Single(h.Containers);
        Assert.Equal(DeveloperOptions, container.SemanticPageName);

        // 因果链
        Assert.Contains(h.Agent.Trace, t => t.Reason == "viewport exploration decision: ScrollForward (current Container)");
        Assert.Contains(h.Agent.Trace, t => t.Reason == "semantic capability selected: SetEnabled");
        Assert.Equal(4L, satisfied.Evidence.SourceObservationSequence);
    }

    // ── F1：目标初始不可见 → 不猜 SetSwitch、仅授权 ScrollForward ───────────

    [Fact]
    public async Task F1_TargetInvisible_NoGuessedSetSwitch_ScrollOnlyWhenEvaluatorTrue()
    {
        var h = Build(NormalSpec());
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f1", viewportExplorationEvaluator: ContinueOnce);
        Assert.IsType<SemanticRunResult.Satisfied>(result);

        var setSwitch = Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var scroll = Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        // SetSwitch 只发生在滚动之后 —— 目标不可见时绝不猜测
        var actions = h.Environment.ActionHistory.ToList();
        Assert.True(actions.IndexOf(scroll) < actions.IndexOf(setSwitch),
            "Scroll must precede SetSwitch; no SetSwitch may be guessed before the target is revealed.");
    }

    // ── F2：滚动分发成功但视口未变 → exhausted → 有界停止、非 SATISFIED ─────

    [Fact]
    public async Task F2_ScrollDispatchedButViewportUnchanged_Exhausted_BoundedStop()
    {
        var h = Build(new BuildSpec { ScrollSelfLoop = true });
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f2", viewportExplorationEvaluator: ContinueIfViewportChanged);
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        // 分发成功（一次滚动）但视口未变 → evaluator=false → 无第二滚动、零 SetSwitch、有界停止
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F3：fresh 视口目标仍缺席 → 仅当 fresh evaluator=true 再滚动（无预定计数） ──

    [Fact]
    public async Task F3_FreshViewportTargetStillAbsent_SecondScrollOnlyFromFreshEvaluatorTrue()
    {
        var h = Build(new BuildSpec { TwoHops = true });
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f3", viewportExplorationEvaluator: ContinueIfViewportChanged);
        Assert.IsType<SemanticRunResult.Satisfied>(result);

        // 两次滚动是证据涌现（内容持续推进），非硬编码计数；SetSwitch 恰好一次
        Assert.Equal(2, h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>().Count());
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── F4：目标出现 → 动作几何仅来自 fresh 视口（旧视口无目标、无索引） ─────

    [Fact]
    public async Task F4_TargetAppears_ActionGroundedFromFreshViewportOnly()
    {
        var h = Build(NormalSpec());
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f4", viewportExplorationEvaluator: ContinueOnce);
        Assert.IsType<SemanticRunResult.Satisfied>(result);

        var setSwitch = Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        var preScroll = h.Environment.ObservationHistory.First(o => o.SequenceNumber == 2L);
        var postScroll = h.Environment.ObservationHistory.First(o => o.SequenceNumber == 3L);

        // 旧视口没有任何 toggle → 旧 grounding 无法授权任何 SetSwitch 几何
        Assert.Empty(preScroll.Elements.Where(e => e.PerceptionType == "toggle"));
        var freshToggle = Assert.Single(postScroll.Elements.Where(e => e.PerceptionType == "toggle"));
        Assert.Equal(freshToggle.Index, setSwitch.TargetElementIndex);
        Assert.Equal(freshToggle.Bounds, setSwitch.TargetBounds);
    }

    // ── F5：滚动致语义页面变更 → 同容器被拒、外部世界权威 ───────────────────

    [Fact]
    public async Task F5_ScrollChangesSemanticPage_ExternalWorldAuthoritative_NewContainer()
    {
        // F5: scroll causes unexpected page change -> external world wins.
        // After scroll, the fresh observation resolves to a DIFFERENT KNOWN semantic page (OtherPage).
        // The Agent must reconcile via multi-level traversal: create new Container for OtherPage,
        // continue same Goal. Since OtherPage has no scroll transition, the second scroll attempt
        // fails, resulting in ExecutionFailed.
        var h = Build(new BuildSpec { ScrollToOtherPage = true });
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f5", viewportExplorationEvaluator: ContinueOnce);

        // The reconcile should create a new container for OtherPage, then the second scroll
        // fails because OtherPage has no viewport transition
        Assert.IsType<SemanticRunResult.ExecutionFailed>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        // Two scrolls: first to transition to OtherPage, second fails on OtherPage
        Assert.Equal(2, h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>().Count());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        // External world wins: new Container created for OtherPage
        Assert.Equal(2, h.Containers.Count);
        Assert.Contains(h.Containers, c => c.SemanticPageName == "DeveloperOptions");
        Assert.Contains(h.Containers, c => c.SemanticPageName == "OtherPage");
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F6：滚动后观测 UNKNOWN → fail closed ─────────────────────────────────

    [Fact]
    public async Task F6_UnknownAfterScroll_FailClosed_NoBlindRedispatch()
    {
        var h = Build(new BuildSpec { ScrollToUnknown = true });
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f6", viewportExplorationEvaluator: ContinueOnce);
        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Single(h.Containers);
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F7：目标初始已可见 → 零 ScrollForward ────────────────────────────────

    [Fact]
    public async Task F7_TargetVisibleInitially_ZeroScrollForward()
    {
        var h = Build(new BuildSpec { InitialHasTarget = true, InitialSwitchState = false });
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f7", viewportExplorationEvaluator: ContinueOnce);
        Assert.IsType<SemanticRunResult.Satisfied>(result);

        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── F8：目标可见但歧义（SwitchState UNKNOWN）→ 不猜动作 ──────────────────

    [Fact]
    public async Task F8_TargetVisibleButAmbiguous_NoGuessedAction()
    {
        var h = Build(new BuildSpec { ScrolledSwitchUnknown = true });
        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [AutomaticSystemUpdates], [SetEnabled], "f8", viewportExplorationEvaluator: ContinueOnce);
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        // 滚动揭示了目标，但 SwitchState UNKNOWN → 零 SetSwitch、绝不猜测
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── 回归：evaluator 缺席 → 同一世界保持导航-only 行为（零滚动） ──────────

    [Fact]
    public async Task Regression_EvaluatorAbsent_NoScroll_NavigationOnlyBehavior()
    {
        var h = Build(NormalSpec());
        // 未注入 viewportExplorationEvaluator（可选形参默认 null）→ 探索相位跳过、导航-only
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [AutomaticSystemUpdates], [SetEnabled], "reg1");
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }
}
