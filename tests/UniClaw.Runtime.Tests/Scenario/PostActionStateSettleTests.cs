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
/// POST-ACTION STATE SETTLE — T1..T15 (APPLY gate: post-action-state-settle).
///
/// The toggle animation window is modeled as: after SetSwitch dispatch the
/// post-action Observation carries the toggle with SwitchState=null (perception
/// cannot classify a moving knob), then a bounded fresh re-observation settles to
/// the real value. Traversal Verify phase settles within COMPOSITION_POLICY budget
/// (max 3 re-observations, bounded delay); budget exhaustion returns through the
/// SAME existing StateEvidenceRequired path; dispatch stays EXACTLY ONCE (T13).
/// </summary>
public sealed class PostActionStateSettleTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ElementBounds TextBounds = new(0.05f, 0.20f, 0.50f, 0.30f);
    private static readonly ElementBounds ToggleBounds = new(0.75f, 0.20f, 0.90f, 0.30f);
    private static readonly SemanticGoalInput Goal = new("WifiConnectivity", "Enabled", true);

    private sealed class Harness
    {
        public required RuntimeAgent Agent { get; init; }
        public required ScriptedEnvironment Environment { get; init; }
        public required RuntimeTraversal Traversal { get; init; }
    }

    /// <summary>
    /// Single-screen semantic world. After SetSwitch the world transitions to the
    /// "On" screen (toggle = settledState). Animation window is injected via
    /// per-sequence observe masks (toggle SwitchState=null for the post-action
    /// observation), mirroring the real ImageSwitchStateProvider null-window.
    /// </summary>
    private static Harness Build(
        bool initial = false,
        bool settled = true,
        bool? settledState = null,
        IReadOnlyDictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>? observeMasks = null,
        IReadOnlyDictionary<long, long>? sequenceOverrides = null,
        int maxPostActionSettles = 3,
        TimeSpan? settleDelay = null)
    {
        var settingsScreen = new ScreenConfig("Settings", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", initial, new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true), ToggleBounds, "toggle"),
        ]);
        var onScreen = new ScreenConfig("On", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", settledState ?? settled, null, ToggleBounds, "toggle"),
        ]);
        var env = new ScriptedEnvironment(
            "Settings", "Settings", [settingsScreen, onScreen],
            observeOverrides: observeMasks,
            observeSequenceOverrides: sequenceOverrides);
        var traversal = new RuntimeTraversal(env, maxPostActionSettles: maxPostActionSettles, postActionSettleDelay: settleDelay);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("Settings", o => o.ForegroundApplication == "settings", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), _ => "Settings", _ => container, recovery, pages, criteria);
        return new Harness { Agent = agent, Environment = env, Traversal = traversal };
    }

    private static ImmutableArray<ObservedElement> Mask(bool? toggleState)
        => [new ObservedElement("Wi‑Fi", null, 0, TextBounds, "menuItem"), new ObservedElement("", toggleState, 1, ToggleBounds, "toggle")];

    /// <summary>Post-action observation sequence: startup=1, initial=2, post-dispatch=3, settle retries=4+.</summary>
    private static IReadOnlyDictionary<long, (string, ImmutableArray<ObservedElement>)> AnimationWindowMasks(params long[] sequences)
        => sequences.ToDictionary(seq => seq, seq => ("settings", Mask(null)));

    // ── T1: initial null → second fresh desired → verifies/continues ──────────

    [Fact]
    public async Task T1_AnimationNull_ThenFreshDesired_VerifiesAndCompletes()
    {
        // Post-action observation (seq 3) shows the animation window (null);
        // the settle re-observation (seq 4) reads the real "On" screen (true).
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t1");
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, h.Agent.State);

        var entry = Assert.Single(h.Traversal.Journal);
        Assert.Equal(1, entry.PostActionSettleCount);
        var settled = entry.PostActionObservation!;
        Assert.Equal(4L, settled.SequenceNumber);
        Assert.True(Assert.Single(settled.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
        Assert.Equal(settled.SequenceNumber, satisfied.Evidence.SourceObservationSequence);
    }

    // ── T2: immediate valid → zero retry ──────────────────────────────────────

    [Fact]
    public async Task T2_ImmediateValidEvidence_ZeroSettleRetry()
    {
        var h = Build(initial: false); // no mask: post-action reads "On" (true) directly
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t2");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        var entry = Assert.Single(h.Traversal.Journal);
        Assert.Equal(0, entry.PostActionSettleCount);
        Assert.Equal(3L, entry.PostActionObservation!.SequenceNumber);
    }

    // ── T3: all retries unknown → StateEvidenceRequired ───────────────────────

    [Fact]
    public async Task T3_AllRetriesUnknown_BudgetExhausted_StateEvidenceRequired()
    {
        // seq 3 (post-action) + 3 settle retries (4,5,6) all show null → exhausted.
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3, 4, 5, 6), settleDelay: TimeSpan.Zero);
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t3");
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);
        var entry = Assert.Single(h.Traversal.Journal);
        Assert.Equal(3, entry.PostActionSettleCount); // exact and bounded
        Assert.Equal(6L, entry.PostActionObservation!.SequenceNumber);
        Assert.Null(Assert.Single(entry.PostActionObservation!.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
    }

    // ── T4: retry returns valid opposite state → settle stops → existing semantics decide ──

    [Fact]
    public async Task T4_OppositeState_SettleStops_ExistingSemanticsDecide()
    {
        // Post-action null (seq 3); settle retry (seq 4) reads real world stuck OFF (false).
        var h = Build(initial: false, settledState: false, observeMasks: AnimationWindowMasks(3));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t4", maxIterations: 2);
        // The world genuinely did not flip: settle stops at the first valid fresh
        // evidence (opposite), existing verification decides the outcome — never a
        // settle-manufactured contradiction, never assumed success.
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
        Assert.IsNotType<SemanticRunResult.SemanticContradiction>(result);
        var first = h.Traversal.Journal[0];
        Assert.Equal(1, first.PostActionSettleCount); // stopped at first valid (opposite) evidence
        Assert.False(Assert.Single(first.PostActionObservation!.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
    }

    // ── T5: SequenceNumber strictly advances on retry ─────────────────────────

    [Fact]
    public async Task T5_SettleRetries_StrictlyAdvanceSequence()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t5");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        var sequences = h.Environment.ObservationHistory.Select(o => o.SequenceNumber).ToArray();
        Assert.Equal(new long[] { 1, 2, 3, 4 }, sequences); // strictly increasing; settle consumed seq 4 only
        Assert.Equal(4L, Assert.Single(h.Traversal.Journal).PostActionObservation!.SequenceNumber);
    }

    // ── T6: cancellation stops settle promptly ────────────────────────────────

    [Fact]
    public async Task T6_Cancellation_InterruptsSettlePromptly()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3, 4, 5, 6), settleDelay: TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        var run = h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t6", cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        // Dispatch already happened exactly once; cancellation interrupted the settle delay.
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── T7: retry count exact/bounded ─────────────────────────────────────────

    [Fact]
    public async Task T7_RetryCount_ExactAndBounded()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3, 4, 5, 6), settleDelay: TimeSpan.Zero);
        await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t7");
        Assert.All(h.Traversal.Journal, e => Assert.InRange(e.PostActionSettleCount, 0, 3));
        Assert.Equal(3, Assert.Single(h.Traversal.Journal).PostActionSettleCount); // never exceeds budget
    }

    // ── T8: no stale SwitchState survives ─────────────────────────────────────

    [Fact]
    public async Task T8_NoStaleSwitchStateSurvives()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t8");
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        var entry = Assert.Single(h.Traversal.Journal);
        // Evidence points at the SETTLED fresh observation (seq 4), never the
        // pre-action frame (seq 2 had false) nor the null animation frame (seq 3).
        Assert.Equal(4L, satisfied.Evidence.SourceObservationSequence);
        Assert.Equal(4L, entry.PostActionObservation!.SequenceNumber);
        Assert.True(Assert.Single(entry.PostActionObservation!.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
    }

    // ── T9: NavigationTransitionSettle behavior unchanged ────────────────────

    private sealed class NavHarness
    {
        public required RuntimeAgent Agent { get; init; }
        public required ScriptedEnvironment Environment { get; init; }
        public required RuntimeTraversal Traversal { get; init; }
    }

    /// <summary>
    /// Two-page semantic world: Settings root (Network &amp; internet row) → Network page
    /// (Wi‑Fi toggle). Navigation uses the SAME Agent-side NavigationTransitionSettle
    /// machinery; the new Traversal post-action settle must not alter it (T9), and
    /// Tap (non-state-changing) must never enter the settle path (T14).
    /// </summary>
    private static NavHarness BuildNav()
    {
        var settingsRoot = new ScreenConfig("Root", "settings",
        [
            new ElementConfig("Network & internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "Network"),
                new ElementBounds(0.05f, 0.1f, 0.5f, 0.12f), "menuItem"),
            new ElementConfig("Connected devices", null, null, new ElementBounds(0.05f, 0.2f, 0.5f, 0.22f), "menuItem"),
        ]);
        var network = new ScreenConfig("Network", "settings",
        [
            new ElementConfig("Network & internet", null, null, new ElementBounds(0.05f, 0.08f, 0.5f, 0.1f), "text"),
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "NetworkOn", true), ToggleBounds, "toggle"),
        ]);
        var networkOn = new ScreenConfig("NetworkOn", "settings",
        [
            new ElementConfig("Network & internet", null, null, new ElementBounds(0.05f, 0.08f, 0.5f, 0.1f), "text"),
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"),
        ]);
        var env = new ScriptedEnvironment("Root", "Root", [settingsRoot, network, networkOn]);

        // 页面身份识别器 — 与宿主 CreateMultiPageResolver 同构（正锚 + negative 锚消歧）。
        Func<Observation, string?> resolver = observation =>
        {
            if (observation.Elements.Any(e => string.Equals(e.Text, "Connected devices", StringComparison.Ordinal)))
                return "Root";
            if (observation.Elements.Any(e => string.Equals(e.Text, "Wi‑Fi", StringComparison.Ordinal)
                || string.Equals(e.Text, "Network & internet", StringComparison.Ordinal)))
                return "Network";
            return null;
        };

        var traversal = new RuntimeTraversal(env);
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
        // 导航知识（Agent）：正锚 = 可导航行文本；不含 negative 锚（双词汇决策）。
        var navigationCriteria = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("Root", ["Network & internet", "Connected devices"])
                .Add("Network", ["Network & internet", "Wi‑Fi"]),
            PageNegativeAnchors: null,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Network", ["Wi‑Fi"]));
        var elementCriteria = new ElementBindingCriteria([Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), resolver, containerFactory, recovery, navigationCriteria, elementCriteria);
        return new NavHarness { Agent = agent, Environment = env, Traversal = traversal };
    }

    [Fact]
    public async Task T9_NavigationTransitionSettle_Unchanged_ThenToggleCompletes()
    {
        var h = BuildNav();
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t9", maxIterations: 8);
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, h.Agent.State);

        // 导航（Tap）与 SetSwitch 均完成：1 LaunchApp + 1 Tap + 1 SetSwitch。
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Equal(2, h.Traversal.Journal.Count);
        Assert.All(h.Traversal.Journal, e => Assert.IsType<TraversalStepResult.Succeeded>(e.Result));
        // 导航跳仍走既有 NavigationTransitionSettle 语义（Agent 侧）：post-action 观测
        // 立即证明页面转场（无动画窗口注入），Traversal settle 未介入 —— settle count 全为 0。
        Assert.All(h.Traversal.Journal, e => Assert.Equal(0, e.PostActionSettleCount));
        Assert.Equal(4L, h.Environment.ObservationHistory[^1].SequenceNumber);
        Assert.Equal(4L, satisfied.Evidence.SourceObservationSequence);
    }

    // ── T10: Assistance/L1 behavior unchanged (null provider) ─────────────────

    [Fact]
    public async Task T10_AssistanceUnchanged_NullProvider_NoConsult()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t10");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason is not null && t.Reason.Contains("assistance", StringComparison.OrdinalIgnoreCase));
    }

    // ── T11: real state producer used → no synthetic injection ────────────────

    [Fact]
    public async Task T11_NoSyntheticStateInjection_ProducerValueFlowsThrough()
    {
        // The settle only READS SwitchState from fresh Observations; the value that
        // flows into GoalEvidence is exactly the environment-produced one (true),
        // never a synthesized/desired-injected value. Settle with settleDelay=0 and
        // a valid settled frame; assert the consumed state is the producer's value.
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3), settleDelay: TimeSpan.Zero);
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t11");
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.True(satisfied.Evidence.Satisfied);
        var settled = Assert.Single(h.Traversal.Journal).PostActionObservation!;
        Assert.Equal(true, Assert.Single(settled.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
        Assert.Equal(4L, settled.SequenceNumber);
    }

    // ── T13: dispatch-once invariant (required) ───────────────────────────────

    [Fact]
    public async Task T13_PhysicalDispatchExactlyOnce_WhileReObservationGtZero()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t13");
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>()); // DispatchCount == 1
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.Tap>()); // 无导航/重发 Tap
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>()); // 无重发 scroll
        var entry = Assert.Single(h.Traversal.Journal);
        Assert.Equal(1, entry.PostActionSettleCount); // ReObservationCount > 0
        Assert.Equal(4L, entry.PostActionObservation!.SequenceNumber);
    }

    // ── T14: ordinary non-state-changing action does not enter settle ─────────

    [Fact]
    public async Task T14_NonStateChangingAction_NeverEntersSettle()
    {
        // Navigation Tap (non-state-changing) must never engage the post-action
        // settle, even though it shares ExecuteLoweredActionAsync. T9 world: the
        // Tap journal entry carries PostActionSettleCount == 0.
        var h = BuildNav();
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t14", maxIterations: 8);
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        var tapEntry = h.Traversal.Journal[0];
        Assert.IsType<DeviceAction.Tap>(tapEntry.DispatchedAction);
        Assert.Equal(0, tapEntry.PostActionSettleCount); // Tap 永不进入 settle
    }

    // ── T15: immediate valid evidence adds no artificial delay/re-observation ──

    [Fact]
    public async Task T15_ImmediateValid_NoArtificialDelayOrReObservation()
    {
        var h = Build(initial: false);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t15");
        sw.Stop();
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(0, Assert.Single(h.Traversal.Journal).PostActionSettleCount);
        Assert.Equal(new long[] { 1, 2, 3 }, h.Environment.ObservationHistory.Select(o => o.SequenceNumber).ToArray());
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"immediate-valid path must not sleep (took {sw.Elapsed})");
    }

    // ── T0: Budget-zero → existing fail-closed preserved ──────────────────────

    [Fact]
    public async Task T0_SettleDisabled_BudgetZero_ExistingPathPreserved()
    {
        var h = Build(initial: false, observeMasks: AnimationWindowMasks(3), maxPostActionSettles: 0, settleDelay: TimeSpan.Zero);
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t0");
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result); // same truthful terminal
        Assert.Equal(RunState.Failed, h.Agent.State);
        var entry = Assert.Single(h.Traversal.Journal);
        Assert.Equal(0, entry.PostActionSettleCount);
        Assert.Equal(3L, entry.PostActionObservation!.SequenceNumber); // consumed only the immediate frame
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── T16 (GRADUATION REPAIR): OBSERVATION-SCOPED TARGET IDENTITY ────────────
    // numeric Index is grounded-observation-scoped (裁决 3). The settle MUST
    // re-identify the target toggle in EVERY fresh observation via existing
    // SPATIAL_RELATION evidence (bounds overlap + toggle type) — NOT by carrying
    // the old TargetElementIndex into the new observation.

    [Fact]
    public async Task T16_IndexShiftsBetweenObservations_SettleStillReIdentifiesByBounds()
    {
        // Grounding observation has the toggle at index 1. After dispatch, the
        // fresh animation frame shifts element ordering: the toggle now appears at
        // index 3 (same bounds) with SwitchState null. A stale-index implementation
        // would misidentify the control and fail to settle. Observation-scoped
        // re-identification must still find it by bounds overlap.
        var settingsScreen = new ScreenConfig("Settings", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true), ToggleBounds, "toggle"),
        ]);
        var onScreen = new ScreenConfig("On", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"),
        ]);
        var shiftedAnimation = new Observation(
            [new ObservedElement("Wi‑Fi", null, 0, TextBounds, "menuItem"),
             new ObservedElement("other", null, 1, new ElementBounds(0.1f, 0.5f, 0.2f, 0.6f), "menuItem"),
             new ObservedElement("row2", null, 2, new ElementBounds(0.1f, 0.7f, 0.2f, 0.8f), "menuItem"),
             new ObservedElement("", null, 3, ToggleBounds, "toggle")],
            "settings", 0);
        var env = new ScriptedEnvironment(
            "Settings", "Settings", [settingsScreen, onScreen],
            observeOverrides: new Dictionary<long, (string, ImmutableArray<ObservedElement>)>
            {
                [3] = ("settings", shiftedAnimation.Elements), // post-action frame: toggle moved to index 3
            });
        var traversal = new RuntimeTraversal(env, postActionSettleDelay: TimeSpan.Zero);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("Settings", o => o.ForegroundApplication == "settings", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), _ => "Settings", _ => container, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t16");
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        var entry = Assert.Single(traversal.Journal);
        Assert.Equal(1, entry.PostActionSettleCount); // settle engaged via observation-scoped re-identification
        Assert.Equal(4L, entry.PostActionObservation!.SequenceNumber);
        Assert.True(Assert.Single(entry.PostActionObservation!.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
        Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>()); // dispatch still exactly once
    }

    [Fact]
    public async Task T17_ControlGoneAcrossObservations_NoSettle_FailClosed()
    {
        // After dispatch the fresh frames no longer contain a toggle overlapping
        // the action's TargetBounds (e.g. perception dropped the control / page
        // moved). Observation-scoped re-identification yields null → settle never
        // engages → existing fail-closed StateEvidenceRequired path.
        var settingsScreen = new ScreenConfig("Settings", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "On", true), ToggleBounds, "toggle"),
        ]);
        var onScreen = new ScreenConfig("On", "settings",
        [
            new ElementConfig("Wi‑Fi", null, null, TextBounds, "menuItem"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"),
        ]);
        var noToggleFrames = new Dictionary<long, (string, ImmutableArray<ObservedElement>)>
        {
            [3] = ("settings", [new ObservedElement("Wi‑Fi", null, 0, TextBounds, "menuItem")]), // toggle absent entirely
            [4] = ("settings", [new ObservedElement("Wi‑Fi", null, 0, TextBounds, "menuItem")]),
            [5] = ("settings", [new ObservedElement("Wi‑Fi", null, 0, TextBounds, "menuItem")]),
            [6] = ("settings", [new ObservedElement("Wi‑Fi", null, 0, TextBounds, "menuItem")]),
        };
        var env = new ScriptedEnvironment(
            "Settings", "Settings", [settingsScreen, onScreen],
            observeOverrides: noToggleFrames);
        var traversal = new RuntimeTraversal(env, postActionSettleDelay: TimeSpan.Zero);
        var startup = new RuntimeStartup(env, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(env, _ => [], (_, _) => null, (_, _) => true);
        var container = new RuntimeContainer("Settings", o => o.ForegroundApplication == "settings", traversal.ExecuteStep);
        var criteria = new ElementBindingCriteria([Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var pages = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", ["Wi‑Fi"]));
        var agent = new RuntimeAgent(startup, traversal, t => env.ObserveAsync(t), _ => "Settings", _ => container, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "t17", maxIterations: 3);
        // Control not identifiable → no settle → truthful fail-closed terminal.
        Assert.IsType<SemanticRunResult.StateEvidenceRequired>(result);
        Assert.Equal(RunState.Failed, agent.State);
        Assert.All(traversal.Journal, e => Assert.Equal(0, e.PostActionSettleCount));
        Assert.Single(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }
}
