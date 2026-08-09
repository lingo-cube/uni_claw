using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>Scenario-specific wiring for SC-P3-CAND-006 behavior proof; not a shared Runtime harness.</summary>
internal sealed class BoundedCandidateSafetyRunFixture
{
    private BoundedCandidateSafetyRunFixture(
        ScriptedEnvironment environment,
        RuntimeAgent agent,
        RuntimeTraversal traversal,
        Goal goal,
        Plan plan,
        List<int> authorizationOrder,
        List<GoalEvidence> goalEvidence)
    {
        Environment = environment;
        Agent = agent;
        Traversal = traversal;
        Goal = goal;
        Plan = plan;
        AuthorizationOrder = authorizationOrder;
        GoalEvidence = goalEvidence;
    }

    internal ScriptedEnvironment Environment { get; }

    internal RuntimeAgent Agent { get; }

    internal RuntimeTraversal Traversal { get; }

    internal Goal Goal { get; }

    internal Plan Plan { get; }

    internal IReadOnlyList<int> AuthorizationOrder { get; }

    internal IReadOnlyList<GoalEvidence> GoalEvidence { get; }

    internal Task<RunState> RunAsync()
        => Agent.RunAsync(Goal, Plan, BoundedCandidateSafetyFixture.RunId, CancellationToken.None);

    internal static BoundedCandidateSafetyRunFixture Create(
        Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? candidateEvaluator = null,
        bool includeCandidateEvaluator = true,
        ActionResultOutcome safeDispatchOutcome = ActionResultOutcome.Dispatched,
        bool safeWorldChanges = true,
        Plan? plan = null)
    {
        var screens = new[]
        {
            new ScreenConfig("Launcher", "Launcher", []),
            new ScreenConfig(
                "SettingsCandidates",
                "Settings",
                [
                    new ElementConfig(
                        BoundedCandidateSafetyFixture.SafeText,
                        null,
                        safeWorldChanges
                            ? new TransitionConfig(
                                ScreenTransitionAction.Tap,
                                "AboutDetails",
                                DispatchOutcome: safeDispatchOutcome)
                            : null),
                    new ElementConfig(BoundedCandidateSafetyFixture.DestructiveText, null, null),
                    new ElementConfig(BoundedCandidateSafetyFixture.StateChangingText, false, null),
                    new ElementConfig(BoundedCandidateSafetyFixture.UnknownText, null, null),
                ]),
            new ScreenConfig(
                "AboutDetails",
                "Settings",
                [new ElementConfig("About phone details", null, null)]),
        };
        var environment = new ScriptedEnvironment(
            "Launcher",
            "SettingsCandidates",
            screens);
        var traversal = new RuntimeTraversal(environment);
        var authorizationOrder = new List<int>();
        var goalEvidence = new List<GoalEvidence>();
        var criterion = candidateEvaluator ?? BoundedCandidateSafetyFixture.EvaluateCandidate;
        Func<Observation, ObservedElement, CandidateAuthorizationEvidence>? recordingCriterion =
            includeCandidateEvaluator
                ? (observation, candidate) =>
                {
                    authorizationOrder.Add(candidate.Index);
                    return criterion(observation, candidate);
                }
                : null;
        var goal = new Goal(
            observation =>
            {
                var satisfied = observation.Elements.Any(element =>
                    string.Equals(element.Text, "About phone details", StringComparison.Ordinal));
                var evidence = new GoalEvidence(
                    satisfied,
                    satisfied
                        ? "Fresh Observation proves the approved destination."
                        : "Approved destination remains unproven.",
                    observation.SequenceNumber);
                goalEvidence.Add(evidence);
                return evidence;
            },
            recordingCriterion);
        var startup = new RuntimeStartup(
            environment,
            "Settings",
            ResolveSemanticPage);
        var recovery = new RuntimeRecovery(
            environment,
            _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null,
            (_, _) => true);
        RuntimeContainer ContainerFactory(string semanticPage) => new(
            semanticPage,
            observation => string.Equals(
                ResolveSemanticPage(observation),
                semanticPage,
                StringComparison.Ordinal),
            traversal.ExecuteStep);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            ResolveSemanticPage,
            ContainerFactory,
            recovery);
        return new BoundedCandidateSafetyRunFixture(
            environment,
            agent,
            traversal,
            goal,
            plan ?? new Plan([]),
            authorizationOrder,
            goalEvidence);
    }

    private static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal))
            return null;
        if (observation.Elements.Any(element =>
                string.Equals(element.Text, "About phone details", StringComparison.Ordinal)))
        {
            return "AboutDetails";
        }
        return observation.Elements.Any(element =>
            string.Equals(element.Text, BoundedCandidateSafetyFixture.SafeText, StringComparison.Ordinal))
            ? "SettingsCandidates"
            : null;
    }
}
