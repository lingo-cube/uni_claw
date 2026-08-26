using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.ValidationHarness.Fixtures;

/// <summary>
/// Tier-A fixture composition: the deterministic settings-like world, the
/// harness-local RunGraphFactory (DeviceSelector → RunExecutionGraph), and the
/// Strategy Contract compiler over the fixture capability binding. This module
/// owns ALL fixture compositions for the Validation Harness; no fake ever enters
/// UniClaw.Runtime.PhysicalHost (F1).
/// </summary>
public static class FixtureComposition
{
    /// <summary>Fixture device selector text used over the wire (alias selector).</summary>
    public const string FixtureDeviceText = "fixture-settings";

    /// <summary>Deterministic reservation key of the fixture device.</summary>
    public const string FixtureDeviceKey = "alias:fixture-settings";

    /// <summary>
    /// Deterministic settings-like world: root container → two expandable child
    /// containers → each child carries 1–2 record-only leaf elements. Enough for
    /// a depth-2 exploration (S1) and for the anomaly-injection hooks (S2).
    /// </summary>
    public static ValidationFixtureWorld CreateSettingsWorld() => new(
        initialScreenName: "Launcher",
        launchNextScreenName: FixtureStrategyBinding.Root,
        screens:
        [
            new FixtureScreenConfig("Launcher", "Launcher",
            [
                new FixtureElementConfig("power-button", null, new ElementBounds(0.45f, 0.85f, 0.55f, 0.95f)),
            ]),
            new FixtureScreenConfig(FixtureStrategyBinding.Root, FixtureStrategyBinding.Application,
            [
                new FixtureElementConfig(
                    FixtureStrategyBinding.ChildOne,
                    new FixtureTransition(FixtureTransitionAction.Tap, FixtureStrategyBinding.ChildOnePage),
                    new ElementBounds(0.00f, 0.10f, 0.30f, 0.20f)),
                new FixtureElementConfig(
                    FixtureStrategyBinding.ChildTwo,
                    new FixtureTransition(FixtureTransitionAction.Tap, FixtureStrategyBinding.ChildTwoPage),
                    new ElementBounds(0.40f, 0.10f, 0.70f, 0.20f)),
            ]),
            new FixtureScreenConfig(FixtureStrategyBinding.ChildOnePage, FixtureStrategyBinding.Application,
            [
                new FixtureElementConfig(
                    FixtureStrategyBinding.ParentReturnLabel,
                    new FixtureTransition(FixtureTransitionAction.Tap, FixtureStrategyBinding.Root),
                    new ElementBounds(0.00f, 0.00f, 0.20f, 0.10f)),
                new FixtureElementConfig(FixtureStrategyBinding.LeafOneA, null, new ElementBounds(0.10f, 0.30f, 0.30f, 0.40f)),
                new FixtureElementConfig(FixtureStrategyBinding.LeafOneB, null, new ElementBounds(0.10f, 0.50f, 0.30f, 0.60f)),
            ]),
            new FixtureScreenConfig(FixtureStrategyBinding.ChildTwoPage, FixtureStrategyBinding.Application,
            [
                new FixtureElementConfig(
                    FixtureStrategyBinding.ParentReturnLabel,
                    new FixtureTransition(FixtureTransitionAction.Tap, FixtureStrategyBinding.Root),
                    new ElementBounds(0.00f, 0.00f, 0.20f, 0.10f)),
                new FixtureElementConfig(FixtureStrategyBinding.LeafTwoA, null, new ElementBounds(0.10f, 0.30f, 0.30f, 0.40f)),
                new FixtureElementConfig(FixtureStrategyBinding.LeafTwoB, null, new ElementBounds(0.10f, 0.50f, 0.30f, 0.60f)),
            ]),
        ]);

    /// <summary>
    /// Builds a fully wired Runtime Agent graph over <paramref name="world"/>,
    /// mirroring the Scenario composition chain exactly (Startup → Traversal →
    /// Recovery → Container factory) with the harness-local semantic decorator.
    /// One graph (and its Agent) corresponds to exactly one Run.
    /// </summary>
    public static RunExecutionGraph BuildGraph(ValidationFixtureWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var decorated = new FixtureSemanticEnvironment(world, element => element.Text switch
        {
            FixtureStrategyBinding.ChildOne or FixtureStrategyBinding.ChildTwo
                => FixtureSemanticRole.NavigationCandidate,
            FixtureStrategyBinding.ParentReturnLabel => FixtureSemanticRole.ParentReturnControl,
            _ => null,
        });
        var traversal = new RuntimeTraversal(decorated);
        Func<Observation, string?> resolve = FixtureStrategyBinding.ResolvePage;
        var startup = new RuntimeStartup(decorated, FixtureStrategyBinding.Application, resolve);
        var recovery = new RuntimeRecovery(
            decorated,
            _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null,
            (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => decorated.ObserveAsync(cancellationToken),
            resolve,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(resolve(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        return new RunExecutionGraph(agent, decorated);
    }

    /// <summary>Composition-root factory from a fresh deterministic world per admitted run.</summary>
    public static RunGraphFactory CreateFactory()
        => selector => BuildGraphFor(selector, CreateSettingsWorld());

    /// <summary>Composition-root factory over an explicit world instance. The world
    /// supports exactly one admitted Run (single-run graph semantics); later S2
    /// exercises use this entry to reach the world's anomaly-injection hooks.</summary>
    public static RunGraphFactory CreateFactory(ValidationFixtureWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return selector => BuildGraphFor(selector, world);
    }

    /// <summary>Strategy Contract compiler over the fixture capability binding.</summary>
    public static StrategyContractCompiler CreateCompiler() => new([new FixtureStrategyBinding()]);

    private static RunExecutionGraph BuildGraphFor(DeviceSelector selector, ValidationFixtureWorld world)
    {
        if (!string.Equals(selector.Key, FixtureDeviceKey, StringComparison.Ordinal))
        {
            throw new DeviceSelectorUnsupportedException(
                selector.Key,
                $"The validation harness Tier-A fixture supports only '{FixtureDeviceKey}'.");
        }

        return BuildGraph(world);
    }
}