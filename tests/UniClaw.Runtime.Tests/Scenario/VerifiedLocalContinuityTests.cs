using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// VERIFIED LOCAL CONTINUITY — T1..T15 (APPLY gate: verified-local-continuity).
///
/// Buyer: SCROLLED_CONTAINER_IDENTITY_DRIFT — after a same-Container action
/// (ScrollForward / SetSwitch) the ABSOLUTE page resolver returns null because the
/// page title scrolled out of view. The repair preserves the previous semantic page
/// ONLY when fresh continuity evidence independently verifies same-Container
/// continuity (Source = VERIFIED_LOCAL_CONTINUITY). Never resolver==null → previousPage.
///
/// The fake resolver models the ASU DeveloperOptions page: it returns "DeveloperOptions"
/// when the "Developer options"/"Developeroptions" title is visible, and null when the
/// title is scrolled off (bottom of the scrollable list).
/// </summary>
public sealed class VerifiedLocalContinuityTests
{
    private static readonly SemanticObject Asu = SemanticObject.Define("AutomaticSystemUpdates", "SystemUpdateSetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "SystemUpdateSetting", "Enabled");
    private static readonly ElementBounds TextBounds = new(0.05f, 0.20f, 0.50f, 0.30f);
    private static readonly ElementBounds ToggleBounds = new(0.75f, 0.20f, 0.90f, 0.30f);
    private static readonly SemanticGoalInput Goal = new("AutomaticSystemUpdates", "Enabled", true);

    private sealed class Harness
    {
        public required RuntimeAgent Agent { get; init; }
        public required ScriptedEnvironment Environment { get; init; }
        public required RuntimeTraversal Traversal { get; init; }
        public required Func<Observation, string?> Resolver { get; init; }
    }

    /// <summary>
    /// Absolute resolver: "Developer options"/"Developeroptions" visible → DeveloperOptions;
    /// otherwise null (title scrolled off — the ASU bottom-of-list case).
    /// </summary>
    private static Func<Observation, string?> TitleVisibleResolver()
        => obs => obs.Elements.Any(e =>
                string.Equals(e.Text, "Developer options", StringComparison.Ordinal)
                || string.Equals(e.Text, "Developeroptions", StringComparison.Ordinal))
            ? "DeveloperOptions"
            : null;

    private static Harness Build(
        bool initialAsuState = false,
        bool settledAsuState = true,
        string? postScrollScreen = null,
        string? postSetSwitchScreen = null,
        Func<Observation, string?>? resolver = null,
        bool foregroundMismatchOnPostScroll = false)
    {
        // 顶部视口：标题可见 + 目标 below-fold（需滚动）
        var top = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Use developer options", true, null, ToggleBounds, "toggle"),
            new ElementConfig("Memory", null, null, TextBounds, "menu_item"),
        ]);
        // 滚动后视口：标题滚出（绝对解析器 null），目标行进入视口
        var bottom = new ScreenConfig("Bottom", "settings",
        [
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", initialAsuState,
                new TransitionConfig(ScreenTransitionAction.SetSwitch, "BottomOn", true),
                ToggleBounds, "toggle"),
            new ElementConfig("DSU Loader", null, null, TextBounds, "menu_item"),
        ]);
        // 滚动后底部（更深处）：标题仍不可见，目标行 + 演示段
        var deeper = new ScreenConfig("Deeper", "settings",
        [
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", initialAsuState,
                new TransitionConfig(ScreenTransitionAction.SetSwitch, "DeeperOn", true),
                ToggleBounds, "toggle"),
            new ElementConfig("Enable demo mode", null, null, TextBounds, "menu_item"),
            new ElementConfig("Show demo mode", null, null, TextBounds, "menu_item"),
        ]);
        var bottomOn = new ScreenConfig("BottomOn", "settings",
        [
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", settledAsuState, null, ToggleBounds, "toggle"),
            new ElementConfig("DSU Loader", null, null, TextBounds, "menu_item"),
        ]);
        var deeperOn = new ScreenConfig("DeeperOn", "settings",
        [
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", settledAsuState, null, ToggleBounds, "toggle"),
            new ElementConfig("Enable demo mode", null, null, TextBounds, "menu_item"),
            new ElementConfig("Show demo mode", null, null, TextBounds, "menu_item"),
        ]);
        var wrongPage = new ScreenConfig("OtherPage", "settings",
        [
            new ElementConfig("Network & internet", null, null, TextBounds, "menu_item"),
            new ElementConfig("Connected devices", null, null, TextBounds, "menu_item"),
        ]);
        var popup = new ScreenConfig("Popup", "systemui", // 前台变化 → 连续性拒绝
        [
            new ElementConfig("Enable demo mode", null, null, TextBounds, "menu_item"),
            new ElementConfig("Show demo mode", null, null, TextBounds, "menu_item"),
        ]);
        var emptyScreen = new ScreenConfig("Empty", "settings", []); // 无结构性证据 → 连续性拒绝

        // ScrollForward 转场：Top → Bottom → Deeper（默认）；可注入 postScrollScreen 覆盖
        var topScreen = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Use developer options", true, null, ToggleBounds, "toggle"),
            new ElementConfig("Memory", null, null, TextBounds, "menu_item"),
        ],
        new ViewportTransitionConfig(postScrollScreen ?? "Bottom"));
        var bottomScreen = new ScreenConfig("Bottom", "settings",
        [
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", initialAsuState,
                new TransitionConfig(ScreenTransitionAction.SetSwitch, "BottomOn", true),
                ToggleBounds, "toggle"),
            new ElementConfig("DSU Loader", null, null, TextBounds, "menu_item"),
        ],
        new ViewportTransitionConfig(postScrollScreen == "Deeper" ? "Deeper" : "Bottom"));

        var env = new ScriptedEnvironment("Top", "Top",
            [topScreen, bottomScreen, deeper, bottomOn, deeperOn, wrongPage, popup, emptyScreen]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", resolver ?? TitleVisibleResolver());
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("DeveloperOptions", o => (resolver ?? TitleVisibleResolver())(o) == "DeveloperOptions", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Asu],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("DeveloperOptions", ["Developer options", "Developeroptions"]));
        var agent = new RuntimeAgent(startup, traversal, t => semanticEnv.ObserveAsync(t), resolver ?? TitleVisibleResolver(), _ => container, recovery, pages, criteria);
        return new Harness { Agent = agent, Environment = env, Traversal = traversal, Resolver = resolver ?? TitleVisibleResolver() };
    }

    private static Func<ImmutableArray<Observation>, ViewportExplorationEvidence> ContinueIfViewportChanged()
        => observations =>
        {
            if (observations.Length <= 1)
                return new ViewportExplorationEvidence(true, "initial viewport lacks target; one bounded step is justified");
            var previous = observations[^2].Elements.Select(e => e.Text).ToImmutableHashSet(StringComparer.Ordinal);
            var current = observations[^1].Elements.Select(e => e.Text).ToImmutableHashSet(StringComparer.Ordinal);
            var changed = !previous.SetEquals(current);
            return new ViewportExplorationEvidence(
                changed,
                changed ? "viewport content advanced; exploration not exhausted" : "viewport unchanged; exploration exhausted");
        };

    // ── T1: title visible → absolute recognition succeeds → no fallback needed ──

    [Fact]
    public async Task T1_TitleVisible_AbsoluteRecognition_NoFallback()
    {
        // 目标在 Top 视口直接可见（不需滚动）：绝对解析器直接成功，无 continuity fallback。
        var top = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true), ToggleBounds, "toggle"),
        ]);
        var on = new ScreenConfig("On", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"),
        ]);
        var env = new ScriptedEnvironment("Top", "Top", [top, on]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", TitleVisibleResolver());
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("DeveloperOptions", o => TitleVisibleResolver()(o) == "DeveloperOptions", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Asu],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("DeveloperOptions", ["Developer options", "Developeroptions"]));
        var agent = new RuntimeAgent(startup, traversal, t => semanticEnv.ObserveAsync(t), TitleVisibleResolver(), _ => container, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t1");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        // 绝对识别成功 → 无 VERIFIED_LOCAL_CONTINUITY trace
        Assert.DoesNotContain(agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
    }

    // ── T2: scroll within DeveloperOptions → title disappears → fresh evidence → same page preserved ──

    [Fact]
    public async Task T2_ScrollTitleDisappears_FreshEvidence_PagePreserved()
    {
        var h = Build(initialAsuState: false, postScrollScreen: "Bottom");
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t2", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        // 滚动后标题不可见 → absolute resolver null → VerifiedContinuity 保留页面 → 目标可见 →
        // SetSwitch → GoalEvidence → Satisfied（无 false SemanticContradiction）。
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Contains(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("SemanticContradiction", StringComparison.Ordinal));
        Assert.Equal(RunState.Completed, h.Agent.State);
    }

    // ── T3: multiple consecutive scrolls → same page remains verified ──

    [Fact]
    public async Task T3_MultipleScrolls_PageRemainsVerified()
    {
        var h = Build(initialAsuState: false, postScrollScreen: "Deeper");
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t3", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        // 每次滚动后标题都不可见 → 每次经 VerifiedContinuity 保留（trace 至少 2 条）
        var verified = h.Agent.Trace.Count(t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
        Assert.True(verified >= 2, $"expected ≥2 verified-continuity events, got {verified}");
        Assert.Equal(RunState.Completed, h.Agent.State);
    }

    // ── T4: below-fold SetSwitch → post-action title absent → continuity preserved ──

    [Fact]
    public async Task T4_BelowFoldSetSwitch_PostActionTitleAbsent_NoFalseContradiction()
    {
        var h = Build(initialAsuState: false, postScrollScreen: "Bottom");
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t4", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, h.Agent.State);
        // 目标下方没有 false SemanticContradiction（post-action 标题不可见时保留页面）
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("semantic page unresolved", StringComparison.Ordinal));
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── T5: fresh observation positively identifies another page → continuity rejected ──

    [Fact]
    public async Task T5_PositiveOtherPageMatch_ContinuityRejected()
    {
        // 滚动后落到 OtherPage（其 anchors 正面匹配）→ absolute resolver 返回 OtherPage
        // （非 null）→ 不走 VerifiedContinuity → 既有导航转场路径。
        var topScreen = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Use developer options", true, null, ToggleBounds, "toggle"),
            new ElementConfig("Memory", null, null, TextBounds, "menu_item"),
        ],
        new ViewportTransitionConfig("OtherPage"));
        var otherPage = new ScreenConfig("OtherPage", "settings",
        [
            new ElementConfig("Network & internet", null, null, TextBounds, "menu_item"),
            new ElementConfig("Connected devices", null, null, TextBounds, "menu_item"),
        ]);
        var env = new ScriptedEnvironment("Top", "Top", [topScreen, otherPage]);
        var traversal = new RuntimeTraversal(env);
        Func<Observation, string?> resolver = obs =>
        {
            if (obs.Elements.Any(e => string.Equals(e.Text, "Developer options", StringComparison.Ordinal)
                || string.Equals(e.Text, "Developeroptions", StringComparison.Ordinal)))
                return "DeveloperOptions";
            if (obs.Elements.Any(e => string.Equals(e.Text, "Network & internet", StringComparison.Ordinal)))
                return "OtherPage";
            return null;
        };
        var startup = new RuntimeStartup(env, "settings", resolver);
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var containerFactory = new Func<string, RuntimeContainer>(page =>
        {
            var c = new RuntimeContainer(page, o => string.Equals(resolver(o), page, StringComparison.Ordinal), traversal.ExecuteStep);
            containers.Add(c);
            return c;
        });
        var criteria = new ElementBindingCriteria([Asu],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("DeveloperOptions", ["Developer options", "Developeroptions"])
                .Add("OtherPage", ["Network & internet", "Connected devices"]));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), resolver, containerFactory, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t5", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());

        // 关键断言：continuity 被拒绝（未保留 DeveloperOptions），真实转场被检测到
        //（external world transition → OtherPage）。OtherPage 无 ASU toggle → 非 Completed。
        Assert.DoesNotContain(agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("external world transition from 'DeveloperOptions' to 'OtherPage'", StringComparison.Ordinal));
        Assert.NotEqual(RunState.Completed, agent.State);
    }

    // ── T6: foreground application changes → continuity rejected ──

    [Fact]
    public async Task T6_ForegroundChange_ContinuityRejected()
    {
        var h = Build(initialAsuState: false, postScrollScreen: "Popup"); // Popup 前台 = systemui
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t6", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        // 前台变化 → VerifiedContinuity 拒绝 → 既有 fail-closed（initial page resolve 失败或 unknown）
        Assert.NotEqual(RunState.Completed, h.Agent.State);
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
    }

    // ── T7: fresh evidence insufficient → page remains unknown → fail-closed preserved ──

    [Fact]
    public async Task T7_InsufficientEvidence_Unknown_FailClosed()
    {
        // 滚动后空元素（无结构性证据）→ VerifiedContinuity 拒绝 → 既有 unknown/fail-closed。
        var emptyScreen = new ScreenConfig("Empty", "settings", []);
        var h = Build(initialAsuState: false, postScrollScreen: "Empty");
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t7", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        Assert.NotEqual(RunState.Completed, h.Agent.State);
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
    }

    // ── T8: previous page identity alone → insufficient for continuity ──

    [Fact]
    public async Task T8_PreviousPageAlone_Insufficient()
    {
        // 空观测（无 fresh 证据）+ 前一页身份 → 谓词拒绝（需 fresh 结构性证据 + 前台 + 动作）
        var emptyScreen = new ScreenConfig("Empty", "settings", []);
        var h = Build(initialAsuState: false, postScrollScreen: "Empty");
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t8", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        // 空观测不足以证明连续性 → 不保留页面
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
        Assert.NotEqual(RunState.Completed, h.Agent.State);
    }

    // ── T9: element indices reorder across observations → continuity unaffected ──

    [Fact]
    public async Task T9_IndexReorder_ContinuityUnaffected()
    {
        // 滚动后元素顺序变化（目标从 index 1 → 2），但标题仍不可见 → 连续性仍保留（按
        // 结构性证据而非 index identity）。绑定在 fresh observation 上重新解析。
        var topScreen = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Use developer options", true, null, ToggleBounds, "toggle"),
            new ElementConfig("Memory", null, null, TextBounds, "menu_item"),
        ],
        new ViewportTransitionConfig("BottomReordered"));
        var bottomReordered = new ScreenConfig("BottomReordered", "settings",
        [
            new ElementConfig("DSU Loader", null, null, TextBounds, "menu_item"),          // index 0
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"), // index 1
            new ElementConfig("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "BottomOn", true), ToggleBounds, "toggle"), // index 2
        ]);
        var bottomOn = new ScreenConfig("BottomOn", "settings",
        [
            new ElementConfig("DSU Loader", null, null, TextBounds, "menu_item"),
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"),
        ]);
        var env = new ScriptedEnvironment("Top", "Top", [topScreen, bottomReordered, bottomOn]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", TitleVisibleResolver());
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("DeveloperOptions", o => TitleVisibleResolver()(o) == "DeveloperOptions", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Asu],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("DeveloperOptions", ["Developer options", "Developeroptions"]));
        var agent = new RuntimeAgent(startup, traversal, t => semanticEnv.ObserveAsync(t), TitleVisibleResolver(), _ => container, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t9", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.Contains(agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
    }

    // ── T10: normal page recognition unchanged ──

    [Fact]
    public async Task T10_NormalRecognition_Unchanged()
    {
        // 目标 + 标题同屏可见 → 绝对识别成功 → 已满足快路径，零 dispatch，零 fallback。
        var top = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Automatic system updates", null, null, TextBounds, "menu_item"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"), // 已满足
        ]);
        var env = new ScriptedEnvironment("Top", "Top", [top]);
        var traversal = new RuntimeTraversal(env);
        var startup = new RuntimeStartup(env, "settings", TitleVisibleResolver());
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("DeveloperOptions", o => TitleVisibleResolver()(o) == "DeveloperOptions", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Asu],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("DeveloperOptions", ["Developer options", "Developeroptions"]));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), TitleVisibleResolver(), _ => container, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t10");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Empty(agent.Trace.Where(t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal)));
        Assert.Empty(agent.Trace.Where(t => t.Reason is not null && t.Reason.Contains("SemanticContradiction", StringComparison.Ordinal)));
    }

    // ── T11: real page navigation still produces page transition ──

    [Fact]
    public async Task T11_RealNavigation_StillTransition()
    {
        // Top → OtherPage（正面匹配另一页）→ 绝对解析器返回 OtherPage → 导航转场（非 continuity）
        var topScreen = new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
            new ElementConfig("Use developer options", true, null, ToggleBounds, "toggle"),
            new ElementConfig("Memory", null, null, TextBounds, "menu_item"),
        ],
        new ViewportTransitionConfig("OtherPage"));
        var otherPage = new ScreenConfig("OtherPage", "settings",
        [
            new ElementConfig("Network & internet", null, null, TextBounds, "menu_item"),
            new ElementConfig("Connected devices", null, null, TextBounds, "menu_item"),
        ]);
        var env = new ScriptedEnvironment("Top", "Top", [topScreen, otherPage]);
        var traversal = new RuntimeTraversal(env);
        Func<Observation, string?> resolver = obs =>
        {
            if (obs.Elements.Any(e => string.Equals(e.Text, "Developer options", StringComparison.Ordinal)
                || string.Equals(e.Text, "Developeroptions", StringComparison.Ordinal)))
                return "DeveloperOptions";
            if (obs.Elements.Any(e => string.Equals(e.Text, "Network & internet", StringComparison.Ordinal)))
                return "OtherPage";
            return null;
        };
        var startup = new RuntimeStartup(env, "settings", resolver);
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        var containerFactory = new Func<string, RuntimeContainer>(page =>
        {
            var c = new RuntimeContainer(page, o => string.Equals(resolver(o), page, StringComparison.Ordinal), traversal.ExecuteStep);
            containers.Add(c);
            return c;
        });
        var criteria = new ElementBindingCriteria([Asu],
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "Automatic system updates"),
            ImmutableDictionary<string, string>.Empty.Add("AutomaticSystemUpdates", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("DeveloperOptions", ["Developer options", "Developeroptions"])
                .Add("OtherPage", ["Network & internet", "Connected devices"]));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), resolver, containerFactory, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t11", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        Assert.NotEqual(RunState.Completed, agent.State);
        // 转场被检测（新 Container），未保留 DeveloperOptions
        Assert.DoesNotContain(agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
        Assert.Contains(agent.Trace, t => t.Reason is not null
            && (t.Reason.Contains("transition", StringComparison.OrdinalIgnoreCase)
                || t.Reason.Contains("navigation", StringComparison.OrdinalIgnoreCase)));
    }

    // ── T12: popup/overlay breaks Container ownership → continuity rejected ──

    [Fact]
    public async Task T12_PopupOverlay_ContinuityRejected()
    {
        // 前台变为 systemui（popup）→ 谓词拒绝 → 不保留 DeveloperOptions
        var h = Build(initialAsuState: false, postScrollScreen: "Popup");
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Asu], [SetEnabled], "t12", maxIterations: 10, viewportExplorationEvaluator: ContinueIfViewportChanged());
        Assert.NotEqual(RunState.Completed, h.Agent.State);
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("verified local continuity", StringComparison.Ordinal));
    }

    // ── 谓词单元级：resolver==null + 前一页身份 单独不足（永不 resolver==null→previousPage）──

    [Fact]
    public void T8b_ContainerMechanicalAccept_RejectsStaleSequenceOrForeground()
    {
        // TryAcceptVerifiedContinuity 是机械接受（sequence 推进 + 前台兼容）——语义谓词在 Agent。
        // 这里验证机械层拒绝：stale sequence 或前台变化 → 拒绝（无 fresh 证据不得接受）。
        var env = new ScriptedEnvironment("Top", "Top", [new ScreenConfig("Top", "settings",
        [
            new ElementConfig("Developer options", null, null, TextBounds, "text_block"),
        ])]);
        var traversal = new RuntimeTraversal(env);
        var container = new RuntimeContainer("DeveloperOptions", o => TitleVisibleResolver()(o) == "DeveloperOptions", traversal.ExecuteStep);
        container.Bind(new Observation([new ObservedElement("Developer options", null, 0, TextBounds, "text_block")], "settings", 1));

        // stale sequence（seq 1 <= current 1）→ 拒绝
        var stale = new Observation([new ObservedElement("Developer options", null, 0, TextBounds, "text_block")], "settings", 1);
        Assert.False(container.TryAcceptVerifiedContinuity(stale, "settings", recordViewportObservation: true));

        // 前台变化（systemui）→ 拒绝
        var fgMismatch = new Observation([new ObservedElement("Enable demo mode", null, 0, TextBounds, "menu_item")], "systemui", 2);
        Assert.False(container.TryAcceptVerifiedContinuity(fgMismatch, "settings", recordViewportObservation: true));

        // fresh + 前台兼容 → 接受（语义谓词在 Agent 层负责 fresh 结构性证据）
        var fresh = new Observation([new ObservedElement("Enable demo mode", null, 0, TextBounds, "menu_item")], "settings", 2);
        Assert.True(container.TryAcceptVerifiedContinuity(fresh, "settings", recordViewportObservation: true));
    }

    // ── T13/T14/T15: 真实设备证明见 PhysicalHost corpus（实机 ASU OFF→ON / ON→OFF / already）──
}
