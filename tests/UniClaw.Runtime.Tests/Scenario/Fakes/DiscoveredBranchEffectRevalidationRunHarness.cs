using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-P3-CAND-009 Task 2.1 Runtime wiring. The harness scripts the same bounded world as the
/// Task 1.1 fixture (one parent P, A's observable external effect, external Launcher drift,
/// recovered-world P, remaining B navigation) but calibrates the single external drift transition
/// for the Agent run's observation numbering (Startup + observeInitial consume two sequences):
/// the drift fires on the "Branch B" step with the ParentP container suspended, so A's historical
/// completion exists under P before recovery. Read-only reuse of the fixture's carrier, inventory
/// evaluator, authorization, and plan; Agent retains interpretation and all retain/invalidate/
/// resume/GoalEvidence/RunState authority.
/// </summary>
internal sealed class DiscoveredBranchEffectRevalidationRunHarness
{
    /// <summary>
    /// Immutable initial Plan whose targets never include the discovered branch A: A's child-walk
    /// steps are ordinary plan mechanics and B remains planned. A itself is executed only through
    /// the SC-P3-CAND-006 transient Tap step.
    /// </summary>
    private static readonly Plan _plan = new([
        new PlanStep("A external effect", "SetSwitch true"),
        new PlanStep("Return to Parent P", "Tap"),
        new PlanStep(DiscoveredBranchEffectRevalidationFixture.BranchB, "Tap"),
        new PlanStep("Complete B work", "Tap"),
        new PlanStep("Return to Parent P", "Tap"),
    ]);

    private readonly List<GoalEvidence> _goalEvidence = [];
    private readonly List<ImmutableDictionary<string, BranchProgressEvidence>> _progressSnapshots = [];

    private DiscoveredBranchEffectRevalidationRunHarness(
        DiscoveredBranchEffectRevalidationFixture fixture,
        ScriptedEnvironment environment,
        RuntimeTraversal traversal,
        RuntimeAgent agent,
        Goal goal)
    {
        Fixture = fixture;
        Environment = environment;
        Traversal = traversal;
        Agent = agent;
        Goal = goal;
    }

    internal DiscoveredBranchEffectRevalidationFixture Fixture { get; }

    internal ScriptedEnvironment Environment { get; }

    internal RuntimeTraversal Traversal { get; }

    internal RuntimeAgent Agent { get; }

    internal Goal Goal { get; }

    /// <summary>Immutable Plan actually executed by the run (transient Tap A step aside).</summary>
    internal Plan Plan => _plan;

    internal Plan InitialPlan => Fixture.InitialPlan;

    internal string RunId => Fixture.RunId;

    internal IReadOnlyList<GoalEvidence> GoalEvidence => _goalEvidence;

    internal static DiscoveredBranchEffectRevalidationRunHarness Create(
        DiscoveredBranchEffectRevalidationFixture fixture,
        bool? recoveredEffectState,
        bool staleRecoveryObservation = false)
    {
        var environment = new ScriptedEnvironment(
            DiscoveredBranchEffectRevalidationFixture.ActiveParentSemanticPage,
            launchNextScreenName: "RecoveredParentP",
            Screens(recoveredEffectState),
            observeScreenTransitions: new Dictionary<long, string> { [6] = "Launcher" },
            observeSequenceOverrides: staleRecoveryObservation
                ? new Dictionary<long, long> { [7] = 6 }
                : null);
        var semanticEnv = new SemanticCapabilityTestEnvironment(
            environment,
            element => element.Text switch
            {
                var text when text is DiscoveredBranchEffectRevalidationFixture.BranchA
                    or DiscoveredBranchEffectRevalidationFixture.BranchB => FixtureSemanticRole.NavigationCandidate,
                var text when string.IsNullOrWhiteSpace(text) => null,
                _ => FixtureSemanticRole.NonInteractive,
            });
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(
            semanticEnv,
            "Settings",
            ResolveSemanticPage,
            restoreRecipe: "Launch Settings");
        var recovery = new RuntimeRecovery(
            semanticEnv,
            recipe => string.IsNullOrWhiteSpace(recipe)
                ? []
                : [new DeviceAction.LaunchApp("Settings")],
            (_, _) => null,
            (observation, _) => string.Equals(
                observation.ForegroundApplication,
                "Settings",
                StringComparison.Ordinal));

        DiscoveredBranchEffectRevalidationRunHarness? harness = null;
        RuntimeAgent? agent = null;
        var goal = new Goal(
            observation =>
            {
                var snapshot = agent!.BranchProgress.ToImmutableDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
                harness!._progressSnapshots.Add(snapshot);
                var satisfied = snapshot.TryGetValue(
                        DiscoveredBranchEffectRevalidationFixture.ActiveParentSemanticPage,
                        out var progress)
                    && progress.IsSubtreeComplete;
                var item = new GoalEvidence(
                    satisfied,
                    satisfied
                        ? "Agent evaluated revalidated discovered A and proven B."
                        : "Discovered-branch reconciliation remains incomplete.",
                    observation.SequenceNumber);
                harness._goalEvidence.Add(item);
                return item;
            },
            DiscoveredBranchEffectRevalidationFixture.AuthorizeA,
            BranchInventoryEvaluator: DiscoveredBranchEffectRevalidationFixture.EvaluateInventory,
            DiscoveredBranchEffectCriterion: fixture.Carrier);
        agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => semanticEnv.ObserveAsync(cancellationToken),
            ResolveSemanticPage,
            semanticPage => new RuntimeContainer(
                semanticPage,
                observation => string.Equals(
                    ResolveSemanticPage(observation),
                    semanticPage,
                    StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        harness = new DiscoveredBranchEffectRevalidationRunHarness(
            fixture,
            environment,
            traversal,
            agent,
            goal);
        return harness;
    }

    internal async Task<DiscoveredBranchEffectRevalidationRunEvidence> RunAsync()
    {
        var state = await Agent.RunAsync(Goal, Plan, RunId, CancellationToken.None);
        var finalProgress = Agent.BranchProgress.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        var boundary = Agent.LastTrap?.Observed;
        var recoveredObservation = boundary is null
            ? null
            : Environment.ObservationHistory.FirstOrDefault(observation =>
                observation.SequenceNumber > boundary.Value
                && string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal)
                && string.Equals(ResolveSemanticPage(observation), "ParentP", StringComparison.Ordinal));

        // Test-side recomputation mirroring the approved exact-match boundary: the carrier is
        // evaluated only when its identity is exactly present in both the approved inventory and the
        // run's retained completion under the same active parent, and only on the recovered
        // Observation the run consumed. This is read-only evidence expression; the run itself
        // retains interpretation authority.
        bool? criterionOutcome = null;
        if (recoveredObservation is not null
            && _progressSnapshots.Count > 0
            && _progressSnapshots[^1].TryGetValue("ParentP", out var retainedProgress)
            && Fixture.Carrier is { } carrier
            && retainedProgress.ApprovedSiblingEvidence.ContainsKey(carrier.BranchIdentity)
            && retainedProgress.CompletedSiblingEvidence.ContainsKey(carrier.BranchIdentity))
        {
            criterionOutcome = carrier.Evaluator(recoveredObservation);
        }

        return new DiscoveredBranchEffectRevalidationRunEvidence(
            state,
            Agent.Reason,
            boundary,
            criterionOutcome,
            finalProgress,
            _progressSnapshots.ToImmutableArray(),
            Environment.ActionHistory.ToImmutableArray(),
            Environment.ObservationHistory.ToImmutableArray(),
            Traversal.Journal.ToImmutableArray(),
            Agent.Trace.ToImmutableArray(),
            _goalEvidence.ToImmutableArray());
    }

    private static string? ResolveSemanticPage(Observation observation)
    {
        var texts = observation.Elements.Select(element => element.Text).ToHashSet(StringComparer.Ordinal);
        if (texts.Contains("Branch A") && texts.Contains("Branch B"))
            return "ParentP";
        if (texts.Contains("A external effect") && texts.Contains("Return to Parent P"))
            return "ChildA";
        if (texts.Contains("Complete B work") || texts.Contains("B local effect"))
            return "ChildB";
        return null;
    }

    private static IEnumerable<ScreenConfig> Screens(bool? recoveredEffectState)
    {
        yield return new ScreenConfig(
            "ParentP",
            "Settings",
            [
                new ElementConfig("Branch A", null, TapTo("ChildA")),
                new ElementConfig("Branch B", null, TapTo("ChildB")),
            ]);
        yield return new ScreenConfig(
            "ChildA",
            "Settings",
            [
                new ElementConfig(
                    "A external effect",
                    false,
                    new TransitionConfig(ScreenTransitionAction.SetSwitch, "ChildAComplete", true)),
                new ElementConfig("Return to Parent P", null, TapTo("ParentAfterA")),
            ]);
        yield return new ScreenConfig(
            "ChildAComplete",
            "Settings",
            [
                new ElementConfig("A external effect", true, null),
                new ElementConfig("Return to Parent P", null, TapTo("ParentAfterA")),
            ]);
        yield return new ScreenConfig(
            "ParentAfterA",
            "Settings",
            [
                new ElementConfig("Branch A", null, TapTo("ChildA")),
                new ElementConfig("Branch B", null, TapTo("ChildB")),
                new ElementConfig("A external effect", true, null),
            ]);
        yield return new ScreenConfig(
            "RecoveredParentP",
            "Settings",
            RecoveredParentElements(recoveredEffectState));
        yield return new ScreenConfig("Launcher", "Launcher", []);
        yield return new ScreenConfig(
            "ChildB",
            "Settings",
            [
                new ElementConfig("Complete B work", null, TapTo("ChildBComplete")),
                new ElementConfig("Return to Parent P", null, TapTo("RecoveredParentP")),
            ]);
        yield return new ScreenConfig(
            "ChildBComplete",
            "Settings",
            [
                new ElementConfig("B local effect", null, null),
                new ElementConfig("Return to Parent P", null, TapTo("RecoveredParentP")),
            ]);
    }

    private static ImmutableArray<ElementConfig> RecoveredParentElements(bool? recoveredEffectState)
    {
        var elements = ImmutableArray.CreateBuilder<ElementConfig>();
        elements.Add(new ElementConfig("Branch A", null, TapTo("ChildA")));
        elements.Add(new ElementConfig("Branch B", null, TapTo("ChildB")));
        if (recoveredEffectState is not null)
        {
            elements.Add(new ElementConfig("A external effect", recoveredEffectState, null));
        }
        return elements.ToImmutable();
    }

    private static TransitionConfig TapTo(string screen)
        => new(ScreenTransitionAction.Tap, screen);
}

/// <summary>Immutable test-only SC-P3-CAND-009 run evidence snapshot.</summary>
internal sealed record DiscoveredBranchEffectRevalidationRunEvidence(
    RunState State,
    string? Reason,
    long? DriftBoundary,
    bool? CriterionOutcome,
    ImmutableDictionary<string, BranchProgressEvidence> FinalProgress,
    ImmutableArray<ImmutableDictionary<string, BranchProgressEvidence>> ProgressSnapshots,
    ImmutableArray<DeviceAction> ActionHistory,
    ImmutableArray<Observation> Observations,
    ImmutableArray<UniClaw.Runtime.Traversal.TraversalJournalEntry> Journal,
    ImmutableArray<DecisionRecord> Trace,
    ImmutableArray<GoalEvidence> GoalEvidence);
