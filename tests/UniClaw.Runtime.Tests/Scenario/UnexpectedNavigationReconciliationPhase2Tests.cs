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
/// Phase 2 reconciliation boundary and safety tests.
/// These are production-path SemanticRun tests, not direct helper unit tests.
/// </summary>
public sealed class UnexpectedNavigationReconciliationPhase2Tests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly SemanticGoalInput Goal = new("WifiConnectivity", "Enabled", true);

    private static readonly ElementBounds RowBounds = new(0.05f, 0.20f, 0.50f, 0.30f);
    private static readonly ElementBounds AToggleBounds = new(0.70f, 0.20f, 0.90f, 0.30f);
    private static readonly ElementBounds BToggleBounds = new(0.70f, 0.20f, 0.90f, 0.30f);

    private static ScreenConfig ScreenA(string foreground = "settings", bool hasToggle = true, string nextScreen = "B")
    {
        var elements = new List<ElementConfig>
        {
            new("A", null, null, null, "text"),
            new("Wi‑Fi", null, null, RowBounds, "menuItem"),
        };
        if (hasToggle)
            elements.Add(new("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, nextScreen, true), AToggleBounds, "toggle"));
        return new ScreenConfig("A", foreground, [.. elements]);
    }

    private static ScreenConfig ScreenB(
        bool toggleState,
        bool foregroundValid = true,
        string? nextScreen = null,
        int toggleIndex = 2)
    {
        var elements = new List<ElementConfig>
        {
            new("B", null, null, null, "text"),
            new("Wi‑Fi", null, null, RowBounds, "menuItem"),
        };
        while (elements.Count < toggleIndex)
            elements.Add(new($"filler-{elements.Count}", null, null, null, "text"));
        elements.Add(new("", toggleState,
            nextScreen is null ? null : new TransitionConfig(ScreenTransitionAction.SetSwitch, nextScreen, true),
            BToggleBounds, "toggle"));
        return new ScreenConfig("B", foregroundValid ? "settings" : "other_app", [.. elements]);
    }

    private static ScreenConfig ScreenBOn()
    {
        var b = ScreenB(true, nextScreen: null);
        return new ScreenConfig("BOn", "settings", b.Elements);
    }

    private static ScreenConfig ScreenUnknown()
        => new("Unknown", "settings", [new("Something unknown", null, null, null, "text")]);

    private static string? Resolve(Observation o)
        => o.Elements.Any(e => e.Text == "A") ? "A"
        : o.Elements.Any(e => e.Text == "B") ? "B"
        : null;

    private static Harness BuildNonScroll(ScreenConfig a, ScreenConfig b)
    {
        var env = new ScriptedEnvironment("A", "A", [a, b]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", Resolve);
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer Factory(string page)
        {
            var container = new RuntimeContainer(page, o => Resolve(o) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("A", ["A"])
                .Add("B", ["B"]));
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var agent = new RuntimeAgent(startup, traversal, _ => semanticEnv.ObserveAsync(default), Resolve, Factory, recovery, pages, criteria);
        return new Harness(agent, env, traversal, containers);
    }

    private static Harness BuildDeferred(ScreenConfig a, ScreenConfig b)
    {
        var env = new ScriptedEnvironment("A", "A", [a, b]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", Resolve);
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer Factory(string page)
        {
            var container = new RuntimeContainer(page, o => Resolve(o) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("A", ["A"])
                .Add("B", ["B"]));
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var agent = new RuntimeAgent(startup, traversal, _ => semanticEnv.ObserveAsync(default), Resolve, Factory, recovery, pages, criteria);
        return new Harness(agent, env, traversal, containers);
    }

    private sealed record Harness(
        RuntimeAgent Agent,
        ScriptedEnvironment Environment,
        RuntimeTraversal Traversal,
        List<RuntimeContainer> Containers);

    // ── NAV-2 / NAV-3 / NAV-9 / NAV-11: non-Scroll known page B becomes current reality ──

    [Fact]
    public async Task NAV2_NonScrollKnownPageB_Reconciles_AndGoalEvidenceUsesB()
    {
        var h = BuildNonScroll(ScreenA(), ScreenB(toggleState: true));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "nav2");

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal("B", h.Agent.Belief!.SemanticPage);
        Assert.Equal(3L, satisfied.Evidence.SourceObservationSequence);
        Assert.Equal(3L, Assert.Single(h.Agent.NavigationEvidence).SequenceNumber);
        Assert.Equal(2, h.Containers.Count);
        Assert.Contains(h.Containers, c => c.SemanticPageName == "B");
    }

    // ── NAV-7: foreground drift must not be normalized into a known-page transition ──

    [Fact]
    public async Task NAV7_ForegroundDrift_DoesNotReconcile()
    {
        var h = BuildNonScroll(ScreenA(), ScreenB(toggleState: true, foregroundValid: false));
        var result = await h.Agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "nav7");

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Single(h.Containers);
        Assert.DoesNotContain(h.Containers, c => c.SemanticPageName == "B");
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── Stale grounding: after A→B, the next SetSwitch must resolve from B, not A ──

    [Fact]
    public async Task StaleGrounding_NonScroll_NextActionUsesBIndicesAndBounds()
    {
        var a = ScreenA(hasToggle: true, nextScreen: "B");
        var b = ScreenB(toggleState: false, nextScreen: "BOn", toggleIndex: 3);
        var bOn = ScreenBOn();
        var h = BuildNonScroll(a, b);
        // Reuse non-scroll harness but include BOn screen.
        var envWithBOn = new ScriptedEnvironment("A", "A", [a, b, bOn]);
        var semanticEnv = envWithBOn.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", Resolve);
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer Factory(string page)
        {
            var container = new RuntimeContainer(page, o => Resolve(o) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("A", ["A"])
                .Add("B", ["B"]));
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var agent = new RuntimeAgent(startup, traversal, _ => semanticEnv.ObserveAsync(default), Resolve, Factory, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(Goal, [Wifi], [SetEnabled], "stale-grounding", maxIterations: 4);

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        var switches = envWithBOn.ActionHistory.OfType<DeviceAction.SetSwitch>().ToList();
        Assert.Equal(2, switches.Count);
        Assert.Equal(2, switches[0].TargetElementIndex);
        Assert.Equal(AToggleBounds, switches[0].TargetBounds);
        Assert.Equal(3, switches[1].TargetElementIndex);
        Assert.Equal(BToggleBounds, switches[1].TargetBounds);
        Assert.Equal(4L, satisfied.Evidence.SourceObservationSequence);
        Assert.Equal("B", agent.Belief!.SemanticPage);
    }

    // ── DEFER-NAV-1 / DEFER-NAV-3: deferred checkpoint A→B produces all-B state and B-sourced evidence ──

    [Fact]
    public async Task DEFER_NAV1_CheckpointAToB_ContainerObservationBeliefB_GoalEvidenceUsesB()
    {
        var a = new ScreenConfig("A", "settings",
        [
            new("A", null, null, null, "text"),
        ], new ViewportTransitionConfig("B"));
        var b = new ScreenConfig("B", "settings",
        [
            new("B", null, null, null, "text"),
            new("Wi‑Fi", null, null, RowBounds, "menuItem"),
            new("", true, null, BToggleBounds, "toggle"),
        ]);
        var h = BuildDeferred(a, b);

        var result = await h.Agent.RunSemanticGoalAsync(
            Goal, [Wifi], [SetEnabled], "defer-nav1",
            viewportExplorationEvaluator: _ => new ViewportExplorationEvidence(true, "one bounded step"),
            enableDeferredReconciliation: true);

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal("B", h.Agent.Belief!.SemanticPage);
        Assert.Equal(3L, satisfied.Evidence.SourceObservationSequence);
        Assert.Equal(2, h.Containers.Count);
        Assert.Contains(h.Containers, c => c.SemanticPageName == "B");
        Assert.Single(h.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
        Assert.Empty(h.Environment.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }

    // ── DEFER-NAV-2 / DEFER-NAV-3: deferred checkpoint then SetSwitch resolves from B, not old A ──

    [Fact]
    public async Task DEFER_NAV2_OldAGroundingRejected_DeferredCheckpointThenActionUsesB()
    {
        var a = new ScreenConfig("A", "settings",
        [
            new("A", null, null, null, "text"),
        ], new ViewportTransitionConfig("B"));
        var b = new ScreenConfig("B", "settings",
        [
            new("B", null, null, null, "text"),
            new("Wi‑Fi", null, null, RowBounds, "menuItem"),
            new("", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "BOn", true), BToggleBounds, "toggle"),
        ]);
        var bOn = new ScreenConfig("BOn", "settings",
        [
            new("B", null, null, null, "text"),
            new("Wi‑Fi", null, null, RowBounds, "menuItem"),
            new("", true, null, BToggleBounds, "toggle"),
        ]);
        var env = new ScriptedEnvironment("A", "A", [a, b, bOn]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", Resolve);
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer Factory(string page)
        {
            var container = new RuntimeContainer(page, o => Resolve(o) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("A", ["A"])
                .Add("B", ["B"]));
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var agent = new RuntimeAgent(startup, traversal, _ => semanticEnv.ObserveAsync(default), Resolve, Factory, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(
            Goal, [Wifi], [SetEnabled], "defer-nav2", maxIterations: 4,
            viewportExplorationEvaluator: _ => new ViewportExplorationEvidence(true, "one bounded step"),
            enableDeferredReconciliation: true);

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        var switches = env.ActionHistory.OfType<DeviceAction.SetSwitch>().ToList();
        var setSwitch = Assert.Single(switches);
        Assert.Equal(2, setSwitch.TargetElementIndex);
        Assert.Equal(BToggleBounds, setSwitch.TargetBounds);
        Assert.Equal(4L, satisfied.Evidence.SourceObservationSequence);
        Assert.Equal("B", agent.Belief!.SemanticPage);
        Assert.Equal(2, containers.Count);
    }

    // ── DEFER-NAV-4: deferred unknown page fails closed ──

    [Fact]
    public async Task DEFER_NAV4_UnknownPage_FailsClosed()
    {
        var a = new ScreenConfig("A", "settings",
        [
            new("A", null, null, null, "text"),
        ], new ViewportTransitionConfig("Unknown"));
        var unknown = ScreenUnknown();
        var env = new ScriptedEnvironment("A", "A", [a, unknown]);
        var semanticEnv = env.WithToggleLocalControl();
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(semanticEnv, "settings", Resolve);
        var recovery = new RuntimeRecovery(semanticEnv, _ => [], (_, _) => null, (_, _) => true);
        var containers = new List<RuntimeContainer>();
        RuntimeContainer Factory(string page)
        {
            var container = new RuntimeContainer(page, o => Resolve(o) == page, traversal.ExecuteStep);
            containers.Add(container);
            return container;
        }
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty
                .Add("A", ["A"])
                .Add("B", ["B"]));
        var criteria = new ElementBindingCriteria(
            [Wifi],
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "Wi‑Fi"),
            ImmutableDictionary<string, string>.Empty.Add("WifiConnectivity", "toggle"));
        var agent = new RuntimeAgent(startup, traversal, _ => semanticEnv.ObserveAsync(default), Resolve, Factory, recovery, pages, criteria);

        var result = await agent.RunSemanticGoalAsync(
            Goal, [Wifi], [SetEnabled], "defer-nav4",
            viewportExplorationEvaluator: _ => new ViewportExplorationEvidence(true, "one bounded step"),
            enableDeferredReconciliation: true);

        Assert.IsType<SemanticRunResult.SemanticContradiction>(result);
        Assert.Single(containers);
        Assert.Empty(env.ActionHistory.OfType<DeviceAction.SetSwitch>());
    }
}
