using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>Test-only SC-P3-CAND-005 Runtime assembly and evidence capture.</summary>
internal sealed class RecoveryProgressScenarioHarness
{
    private readonly List<GoalEvidence> _goalEvidence = [];
    private readonly List<ImmutableDictionary<string, BranchProgressEvidence>> _progressSnapshots = [];

    private RecoveryProgressScenarioHarness(
        RecoveryProgressResumeFixture fixture,
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

    internal RecoveryProgressResumeFixture Fixture { get; }

    internal ScriptedEnvironment Environment { get; }

    internal RuntimeTraversal Traversal { get; }

    internal RuntimeAgent Agent { get; }

    internal Goal Goal { get; }

    internal Plan Plan => Fixture.Plan;

    internal string RunId => Fixture.RunId;

    internal IReadOnlyList<GoalEvidence> GoalEvidence => _goalEvidence;

    internal static RecoveryProgressScenarioHarness Create(RecoveryProgressResumeFixture fixture)
    {
        var environment = fixture.Environment;
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(
            environment,
            "Settings",
            ResolveSemanticPage,
            restoreRecipe: "Launch Settings");
        var recovery = new RuntimeRecovery(
            environment,
            recipe => string.IsNullOrWhiteSpace(recipe)
                ? []
                : [new DeviceAction.LaunchApp("Settings")],
            (_, _) => null,
            (observation, _) => string.Equals(
                observation.ForegroundApplication,
                "Settings",
                StringComparison.Ordinal));

        RecoveryProgressScenarioHarness? harness = null;
        RuntimeAgent? agent = null;
        var goal = new Goal(observation =>
        {
            var snapshot = agent!.BranchProgress.ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            harness!._progressSnapshots.Add(snapshot);
            var satisfied = snapshot.TryGetValue("ParentP", out var progress)
                && progress.IsSubtreeComplete;
            var item = new GoalEvidence(
                satisfied,
                satisfied
                    ? "Agent evaluated revalidated A and proven B."
                    : "Recovered branch proof remains incomplete.",
                observation.SequenceNumber);
            harness._goalEvidence.Add(item);
            return item;
        });
        agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveSemanticPage,
            semanticPage => new RuntimeContainer(
                semanticPage,
                observation => string.Equals(
                    ResolveSemanticPage(observation),
                    semanticPage,
                    StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        harness = new RecoveryProgressScenarioHarness(fixture, environment, traversal, agent, goal);
        return harness;
    }

    internal async Task<RecoveryProgressScenarioEvidence> RunAsync()
    {
        var state = await Agent.RunAsync(Goal, Plan, Fixture.RunId, CancellationToken.None);
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
        var criterionOutcome = recoveredObservation is null
            ? null
            : Plan.Steps[0].BranchEffectEvidenceEvaluator?.Invoke(recoveredObservation);
        return new RecoveryProgressScenarioEvidence(
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
}

internal sealed record RecoveryProgressScenarioEvidence(
    RunState State,
    string? Reason,
    long? DriftBoundary,
    bool? CriterionOutcome,
    ImmutableDictionary<string, BranchProgressEvidence> FinalProgress,
    ImmutableArray<ImmutableDictionary<string, BranchProgressEvidence>> ProgressSnapshots,
    ImmutableArray<DeviceAction> ActionHistory,
    ImmutableArray<Observation> Observations,
    ImmutableArray<UniClaw.Runtime.Traversal.TraversalJournalEntry> Journal,
    ImmutableArray<TraceEvent> Trace,
    ImmutableArray<GoalEvidence> GoalEvidence);
