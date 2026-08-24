using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
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
/// Multi-level traversal falsifier proofs (PHYSICAL_SETTINGS_TO_WIFI_MULTI_LEVEL_TRAVERSAL):
/// the semantic goal must SURVIVE page/container transitions, and every hop must be
/// proven by a fresh Observation sequence advance + page-identity change — never by
/// dispatch receipt, never by coordinate guessing, never by reused old bindings.
///
/// World shape: SettingsRoot → (tap "Network &amp; internet") → NetworkAndInternet →
/// (tap "Internet") → WifiInternet (Wi‑Fi row + toggle) → SetSwitch → ON.
/// All recognition knowledge (navigation criteria, identity criteria, element
/// criteria, same-row-band tolerance) is caller-injected — no route, no coordinates.
///
/// F1 no candidate → zero dispatch; F1b multiple candidates → fail closed;
/// F2 dispatch ok but page unchanged → no semantic progress; F3 target child absent
/// → fresh container, no old binding reuse; F4 UNKNOWN observation → fail closed;
/// F5 old ElementIndex never reused; F6 already ON → Satisfied with zero SetSwitch.
/// </summary>
public sealed class MultiLevelNavigationFalsifierTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly SemanticGoalInput Goal = new("WifiConnectivity", "Enabled", true);

    // 现场校准几何（与 Slice 2 相同）：Wi‑Fi 行 + 空文本 toggle
    private static readonly ElementBounds WifiRowBounds = new(0.06f, 0.42f, 0.164f, 0.441f);
    private static readonly ElementBounds ToggleBounds = new(0.832f, 0.407f, 0.96f, 0.452f);

    private const string SettingsRoot = "SettingsRoot";
    private const string NetworkAndInternet = "NetworkAndInternet";
    private const string WifiInternet = "WifiInternet";
    private const string WifiInternetOn = "WifiInternetOn";

    /// <summary>
    /// 测试侧页面身份识别器 — 与宿主 CreateMultiPageResolver 同构（Fuse PageAnalysis
    /// 证据到唯一页面名或 null）。identity criteria 含 negative 锚（身份消歧），
    /// 与 Agent 导航 criteria（仅正锚）分离 — 裁决 11 双词汇决策。
    /// </summary>
    private static Func<Observation, string?> CreateTestResolver(PageAnalysisCriteria identityCriteria)
        => observation =>
        {
            var evidence = PageAnalysis.Analyze(observation, identityCriteria);
            var candidates = new List<string>();
            var contradicted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in evidence)
            {
                if (!item.Claim.StartsWith("page is ", StringComparison.Ordinal))
                    continue;
                var pageName = item.Claim["page is ".Length..];
                if (item.Source == "TEXT_ANCHOR_NEGATIVE" && item.Stance == SemanticEvidenceStance.Contradicts)
                {
                    contradicted.Add(pageName);
                    continue;
                }
                if ((item.Source == "TEXT_ANCHOR" || item.Source == "SWITCH_DISTRIBUTION")
                    && item.Stance == SemanticEvidenceStance.Supports
                    && !candidates.Contains(pageName, StringComparer.Ordinal))
                    candidates.Add(pageName);
            }
            var valid = candidates.Where(p => !contradicted.Contains(p)).ToArray();
            return valid.Length == 1 ? valid[0] : null;
        };

    private sealed class BuildSpec
    {
        public bool? InitialSwitchState = false;
        public bool ChangeToOn = true;
        public bool RootHasNetworkRow = true;
        public bool NetworkRowTransitions = true;
        public bool ExtraRootAnchorOnRoot = false; // F1b 歧义：根页额外出现 "Add network"（WifiInternet 正锚，非绑定锚）
        public bool RelaxedRootIdentityNegatives = false; // F1b：身份仍唯一解析为根页（导航层歧义）
        public string Hop1NextScreen = NetworkAndInternet; // F4: "Unknown"
        public bool WifiPageHasSwitch = true; // F3: 目标子元素缺失
        public ImmutableArray<ElementConfig> RootNetworkRowDuplicates = [];
    }

    private sealed class Harness
    {
        public required RuntimeAgent Agent;
        public required ScriptedEnvironment Environment;
        public required RuntimeTraversal Traversal;
        public required List<RuntimeContainer> Containers;
        public required Func<Observation, string?> Resolver;
    }

    private static Harness Build(BuildSpec spec)
    {
        var probeScreenName = WifiInternetOn;

        // ── SettingsRoot ──
        var rootElements = new List<ElementConfig>();
        if (spec.RootHasNetworkRow)
        {
            var row = new ElementConfig(
                "Network & internet", null,
                spec.NetworkRowTransitions
                    ? new TransitionConfig(ScreenTransitionAction.Tap, spec.Hop1NextScreen)
                    : null,
                new ElementBounds(0.05f, 0.403f, 0.5f, 0.426f), "menuItem");
            if (spec.RootNetworkRowDuplicates.IsDefaultOrEmpty)
                rootElements.Add(row);
            else
                rootElements.AddRange(spec.RootNetworkRowDuplicates);
        }
        if (spec.ExtraRootAnchorOnRoot)
            rootElements.Add(new ElementConfig("Add network", null, null, new ElementBounds(0.05f, 0.8f, 0.3f, 0.82f), "menuItem"));
        rootElements.Add(new ElementConfig("Connected devices", null, null, new ElementBounds(0.05f, 0.46f, 0.5f, 0.48f), "menuItem"));
        rootElements.Add(new ElementConfig("Apps", null, null, new ElementBounds(0.05f, 0.5f, 0.4f, 0.52f), "menuItem"));

        var settingsRoot = new ScreenConfig(SettingsRoot, "settings", [.. rootElements]);

        // ── NetworkAndInternet ──
        var networkAndInternet = new ScreenConfig(NetworkAndInternet, "settings",
        [
            new ElementConfig("Network & internet", null, null, new ElementBounds(0.05f, 0.1f, 0.5f, 0.12f), "text"),
            new ElementConfig("Internet", null, new TransitionConfig(ScreenTransitionAction.Tap, WifiInternet),
                new ElementBounds(0.05f, 0.3f, 0.35f, 0.32f), "menuItem"),
            new ElementConfig("SIMs", null, null, new ElementBounds(0.05f, 0.34f, 0.3f, 0.36f), "menuItem"),
        ]);

        // ── WifiInternet（F3：子元素缺失时仅剩唯一身份锚 "Add network"，导航候选归零 → fail closed）──
        var wifiElements = new List<ElementConfig>();
        if (spec.WifiPageHasSwitch)
        {
            wifiElements.Add(new ElementConfig("Internet", null, null, new ElementBounds(0.05f, 0.1f, 0.4f, 0.12f), "text"));
            wifiElements.Add(new ElementConfig("Wi‑Fi", null, null, WifiRowBounds, "menuItem"));
            wifiElements.Add(new ElementConfig("", spec.InitialSwitchState,
                spec.ChangeToOn
                    ? new TransitionConfig(ScreenTransitionAction.SetSwitch, probeScreenName, true)
                    : null,
                ToggleBounds, "toggle"));
        }
        wifiElements.Add(new ElementConfig("Add network", null, null, new ElementBounds(0.05f, 0.5f, 0.4f, 0.52f), "menuItem"));
        var wifiInternet = new ScreenConfig(WifiInternet, "settings", [.. wifiElements]);

        var wifiInternetOn = new ScreenConfig(WifiInternetOn, "settings",
        [
            new ElementConfig("Internet", null, null, new ElementBounds(0.05f, 0.1f, 0.4f, 0.12f), "text"),
            new ElementConfig("Wi‑Fi", null, null, WifiRowBounds, "menuItem"),
            new ElementConfig("", true, null, ToggleBounds, "toggle"),
            new ElementConfig("Add network", null, null, new ElementBounds(0.05f, 0.5f, 0.4f, 0.52f), "menuItem"),
        ]);

        var unknown = new ScreenConfig("Unknown", "settings",
        [
            new ElementConfig("Something unknown", null, null, null, "text"),
        ]);

        var env = new ScriptedEnvironment(SettingsRoot, SettingsRoot, [settingsRoot, networkAndInternet, wifiInternet, wifiInternetOn, unknown]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);

        // 导航知识（Agent）：仅正锚 — negative 锚属身份消歧，放导航 criteria 会误杀合法跳转（双词汇决策）。
        var navigationCriteria = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(SettingsRoot, ["Connected devices", "Apps"])
                .Add(NetworkAndInternet, ["Network & internet", "Internet"])
                .Add(WifiInternet, ["Internet", "Wi‑Fi", "Add network"]),
            PageNegativeAnchors: null,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(WifiInternet, ["Wi‑Fi"]));

        // 身份知识（resolver）：正锚 + negative 锚消歧共享标题文本（"Network & internet"/"Internet"）。
        // F1b 变体放宽根页 negative：根页身份仍唯一解析（"Add network" 不矛盾根页），歧义留在导航候选层
        // （根页同时出现 N&I 行 + "Add network" → N&I 与 WifiInternet 双候选 → fail closed）。
        var rootNegatives = spec.RelaxedRootIdentityNegatives
            ? new[] { "SIMs" }
            : new[] { "Internet", "Wi‑Fi", "Add network", "SIMs" };
        var identityCriteria = new PageAnalysisCriteria("settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(SettingsRoot, ["Connected devices", "Apps"])
                .Add(NetworkAndInternet, ["Network & internet", "Internet", "SIMs"])
                .Add(WifiInternet, ["Internet", "Wi‑Fi", "Add network"]),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add(SettingsRoot, rootNegatives.ToImmutableArray())
                .Add(NetworkAndInternet, ["Connected devices", "Apps", "Add network"])
                .Add(WifiInternet, ["Network & internet", "Connected devices", "Apps", "SIMs"]),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(WifiInternet, ["Wi‑Fi"]));
        var resolver = CreateTestResolver(identityCriteria);

        var elementCriteria = new ElementBindingCriteria([Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));

        var startup = new RuntimeStartup(semanticEnv, "settings", resolver);
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
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

        var agent = new RuntimeAgent(startup, traversal, t => semanticEnv.ObserveAsync(t), resolver, containerFactory, recovery, navigationCriteria, elementCriteria);
        return new Harness { Agent = agent, Environment = env, Traversal = traversal, Containers = containers, Resolver = resolver };
    }

    private static string[] ContainerSequence(RuntimeAgent agent)
    {
        // 同一 Container 可能出现在多条 trace 中（初始绑定 / RecordDispatchedStep / 导航后切换）；
        // 语义容器序列 = 折叠连续重复后的页名序列。
        var names = agent.Trace.Where(t => t.ContainerId is not null).Select(t => t.ContainerId!).ToList();
        var sequence = new List<string>(names.Count);
        foreach (var name in names)
        {
            if (sequence.Count == 0 || sequence[^1] != name)
                sequence.Add(name);
        }
        return [.. sequence];
    }

    // ── E2E：根页 → N&I → WiFi → SetSwitch → Satisfied（同一 Goal 跨容器延续）────

    [Fact]
    public async Task M1E1_MultiHop_SettingsRootToWifi_ExactlyOneSetSwitch_FreshGoalEvidence()
    {
        var h = Build(new BuildSpec { InitialSwitchState = false, ChangeToOn = true });
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1e1", maxIterations: 8);
        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);

        // 容器序列：Goal 存活于三次 Container 转场
        Assert.Equal(new[] { SettingsRoot, NetworkAndInternet, WifiInternet }, ContainerSequence(h.Agent));

        // 恰好一次物理分发：OFF → ON（每次导航也是动作，但 SetSwitch 唯一）
        var dispatch = Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.True(dispatch.TargetState);
        Assert.Equal(ToggleBounds, dispatch.TargetBounds);

        // 动作历史：LaunchApp + 2×Tap + 1×SetSwitch；导航决策均有 trace
        Assert.Equal(2, h.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(h.Agent.Trace, t => t.Reason == "navigation decision: NetworkAndInternet (anchor 'Network & internet')");
        Assert.Contains(h.Agent.Trace, t => t.Reason == "navigation decision: WifiInternet (anchor 'Internet')");
        Assert.Contains(h.Agent.Trace, t => t.Reason == "semantic capability selected: SetEnabled");

        // 每跳 fresh 验证：观察序列推进 + 页面身份变更（journal 同源佐证）
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, h.Environment.ObservationHistory.Select(o => o.SequenceNumber).ToArray());
        var journal = h.Traversal.Journal;
        Assert.Equal(3, journal.Count);
        Assert.All(journal, e => Assert.IsType<TraversalStepResult.Succeeded>(e.Result));
        var hop1 = journal[0].PostActionObservation!;
        var hop2 = journal[1].PostActionObservation!;
        Assert.Equal(3L, hop1.SequenceNumber);
        Assert.Equal(4L, hop2.SequenceNumber);
        Assert.Equal(NetworkAndInternet, h.Resolver(hop1));
        Assert.Equal(WifiInternet, h.Resolver(hop2));

        // 感知证据：fresh 观测提取 ON + GoalEvidence 指向 fresh 观测序列
        var toggle = Assert.Single(hop2.Elements.Where(e => e.PerceptionType == "toggle"));
        var freshObs = journal[2].PostActionObservation!;
        Assert.Equal(5L, freshObs.SequenceNumber);
        Assert.True(Assert.Single(freshObs.Elements.Where(e => e.PerceptionType == "toggle")).SwitchState);
        Assert.Equal(freshObs.SequenceNumber, satisfied.Evidence.SourceObservationSequence);
    }

    // ── F1：无导航候选 → 零分发 ─────────────────────────────────────────

    [Fact]
    public async Task M1F1_NoNavigationCandidate_ZeroDispatch_BindingUnresolved()
    {
        var h = Build(new BuildSpec { RootHasNetworkRow = false });
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f1", maxIterations: 4);
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Equal(new[] { SettingsRoot }, ContainerSequence(h.Agent));
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F1b：多候选（根页同时含 N&I 行与 "Add network" → N&I + WifiInternet 双候选）→ fail closed ─────

    [Fact]
    public async Task M1F1b_MultipleCandidates_FailClosed_ZeroDispatch()
    {
        var h = Build(new BuildSpec { ExtraRootAnchorOnRoot = true, RelaxedRootIdentityNegatives = true });
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f1b", maxIterations: 4);
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F2：导航分发成功但页面未变 → 无语义进展（拒盲重发）───────────────

    [Fact]
    public async Task M1F2_NavigationDispatchOk_PageUnchanged_NoSemanticProgress()
    {
        var h = Build(new BuildSpec { NetworkRowTransitions = false }); // Tap Dispatched 但世界不变
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f2", maxIterations: 4);
        var failed = Assert.IsType<SemanticRunResult.ExecutionFailed>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        // 分发收据 OK（Tap Succeeded + fresh 观测序列推进）…
        var journal = h.Traversal.Journal;
        var tap = Assert.Single(journal);
        Assert.IsType<TraversalStepResult.Succeeded>(tap.Result);
        Assert.Equal(3L, tap.PostActionObservation!.SequenceNumber);
        // …但页面身份未变 → 拒盲重发，零 SetSwitch，绝不 Satisfied
        Assert.Contains("did not prove a fresh Container transition", failed.Reason);
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F3：新页存在但目标子元素缺失 → fresh 容器、零旧绑定复用 ──────────

    [Fact]
    public async Task M1F3_TargetChildAbsent_FreshReconcile_NoOldBindingReuse()
    {
        var h = Build(new BuildSpec { WifiPageHasSwitch = false }); // WiFi 页无 "Wi‑Fi"/toggle
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f3", maxIterations: 8);
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);

        // 到达了 WiFi 页（2 跳成功）但目标子元素缺失 → fail closed，零 SetSwitch
        Assert.Equal(2, h.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal(new[] { SettingsRoot, NetworkAndInternet, WifiInternet }, ContainerSequence(h.Agent));
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F4：UNKNOWN 观测 → fail closed ─────────────────────────────────

    [Fact]
    public async Task M1F4_UnknownObservation_FailClosed_NoNewContainer()
    {
        var h = Build(new BuildSpec { Hop1NextScreen = "Unknown" }); // 跳 1 落到无法识别页面
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f4", maxIterations: 4);
        var failed = Assert.IsType<SemanticRunResult.ExecutionFailed>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);
        Assert.Contains("did not prove a fresh Container transition", failed.Reason);
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        // 未创建新 Container — 未知页面不产生容器转场
        Assert.Equal(new[] { SettingsRoot }, ContainerSequence(h.Agent));
        Assert.Single(h.Containers);
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }

    // ── F5：旧 ElementIndex 不被复用 — 绑定仅来自 fresh 观测 ────────────

    [Fact]
    public async Task M1F5_OldElementIndexNotReused_FreshBindingGeometry()
    {
        var h = Build(new BuildSpec { InitialSwitchState = false, ChangeToOn = true });
        await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f5", maxIterations: 8);

        // N&I 页 index2 是 "SIMs"（非开关）；若旧索引被复用，SetSwitch 会打错元素被 Rejected。
        // 断言 SetSwitch 落在 WiFi 页 fresh 观测的 toggle（index2 空文本 + toggle 几何）。
        var dispatch = Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.Equal(2, dispatch.TargetElementIndex);
        Assert.Equal(ToggleBounds, dispatch.TargetBounds);
        var fresh = h.Environment.ObservationHistory[^1];
        Assert.Equal(5L, fresh.SequenceNumber);
        var toggle = Assert.Single(fresh.Elements.Where(e => e.PerceptionType == "toggle"));
        Assert.Equal(dispatch.TargetElementIndex, toggle.Index);
        // 结构保证：转场后新 Container 的绑定索引全部落在 WiFi 页元素范围内（无跨页残留）
        var wifiContainer = h.Containers.Single(c => c.SemanticPageName == WifiInternet);
        Assert.All(wifiContainer.ObjectBindings.SelectMany(b => b.ElementIndices), idx => Assert.InRange(idx, 0, 3));
    }

    // ── F6：已是 ON → Satisfied，零 SetSwitch，不进入能力执行 ────────────

    [Fact]
    public async Task M1F6_AlreadyOn_Satisfied_ZeroSetSwitch()
    {
        var h = Build(new BuildSpec { InitialSwitchState = true, ChangeToOn = false });
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1f6", maxIterations: 8);
        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, h.Agent.State);
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        // 决策即终止：未进入 capability 执行阶段（跨容器幂等）
        Assert.DoesNotContain(h.Agent.Trace, t => t.Reason == "semantic capability selected: SetEnabled");
        Assert.Equal(2, h.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Equal(new[] { SettingsRoot, NetworkAndInternet, WifiInternet }, ContainerSequence(h.Agent));
    }

    // ── 校准场景：同文本重复元素 → 歧义 fail closed（不再做行带校准合并）─────

    [Fact]
    public async Task M1E2_SameRowBandDuplicates_MergedToSingleAnchor_Navigates()
    {
        var h = Build(new BuildSpec
        {
            RootNetworkRowDuplicates =
            [
                new ElementConfig("Network & internet", null,
                    new TransitionConfig(ScreenTransitionAction.Tap, NetworkAndInternet),
                    new ElementBounds(0.05f, 0.403f, 0.5f, 0.426f), "menuItem"),
                new ElementConfig("Network & internet", null,
                    new TransitionConfig(ScreenTransitionAction.Tap, NetworkAndInternet),
                    new ElementBounds(0.05f, 0.437f, 0.42f, 0.458f), "menuItem"),
            ],
        });
        // Two same-text navigation rows are genuinely ambiguous: Runtime performs
        // no row-band calibration merge (Settings-specific calibration was
        // removed with the embedded scenario knowledge) — it fails closed with
        // zero dispatch rather than guessing a target.
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1e2", maxIterations: 8);
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── 校准场景：不同行带共享文本 → fail closed（零坐标猜测）────────────

    [Fact]
    public async Task M1E3_DistinctRowBands_FailClosed_NoCoordinateGuessing()
    {
        var h = Build(new BuildSpec
        {
            RootNetworkRowDuplicates =
            [
                new ElementConfig("Network & internet", null, null, new ElementBounds(0.05f, 0.2f, 0.5f, 0.22f), "menuItem"),
                new ElementConfig("Network & internet", null, null, new ElementBounds(0.05f, 0.6f, 0.5f, 0.62f), "menuItem"),
            ],
        });
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "m1e3", maxIterations: 4);
        Assert.IsType<SemanticRunResult.BindingUnresolved>(result);
        Assert.Equal(RunState.Failed, h.Agent.State);
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.Tap>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
        Assert.IsNotType<SemanticRunResult.Satisfied>(result);
    }
}
