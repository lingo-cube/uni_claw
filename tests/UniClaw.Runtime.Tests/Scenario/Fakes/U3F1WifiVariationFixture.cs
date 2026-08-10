using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

internal enum U3F1WifiVariationWorld
{
    AlreadyOnLayoutVariant,
    OffReordered,
    AmbiguousReordered,
}

/// <summary>
/// SC-U3-F1-001 deterministic world. Candidate order is external-world evidence
/// only; the fixture does not encode target identity, action authority, or Goal completion.
/// </summary>
internal static class U3F1WifiVariationFixture
{
    internal const string Intent = "确保 WiFi 已开启";

    internal static U3F1WifiVariationRun Create(U3F1WifiVariationWorld world)
    {
        var ambiguous = world == U3F1WifiVariationWorld.AmbiguousReordered;
        var candidateRows = new[]
        {
            new ElementConfig(
                "Wi-Fi Calling",
                ambiguous ? false : null,
                new TransitionConfig(ScreenTransitionAction.Tap, "WifiCallingSettings")),
            new ElementConfig("Mobile network", null, null),
            new ElementConfig(
                "Wi-Fi",
                false,
                new TransitionConfig(ScreenTransitionAction.Tap, "WifiOff")),
        };
        var screens = new[]
        {
            new ScreenConfig("Launcher", "Launcher", []),
            new ScreenConfig("CandidatesReordered", "Settings", [.. candidateRows]),
            new ScreenConfig("WifiOff", "Settings", [
                new ElementConfig("Auto-connect", true, null),
                new ElementConfig("Wi-Fi", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "WifiOn", true))]),
            new ScreenConfig("WifiOn", "Settings", [
                new ElementConfig("Auto-connect", true, null),
                new ElementConfig("Wi-Fi preferences", null, null),
                new ElementConfig("Wi-Fi", true, null)]),
            new ScreenConfig("WifiCallingSettings", "Settings", [
                new ElementConfig("Wi-Fi Calling Settings", null, null)]),
        };

        var alreadyOn = world == U3F1WifiVariationWorld.AlreadyOnLayoutVariant;
        var initialScreen = alreadyOn ? "WifiOn" : "Launcher";
        var launchScreen = alreadyOn ? "WifiOn" : "CandidatesReordered";
        var environment = new ScriptedEnvironment(initialScreen, launchScreen, screens);
        var traversal = new RuntimeTraversal(environment);
        var safetyOrder = new List<int>();
        var groundingOrder = new List<int>();
        var postActionEvidenceSequences = new List<long>();
        var goalEvidence = new List<GoalEvidence>();

        var goal = new Goal(
            observation =>
            {
                var satisfied = observation.Elements.Any(
                    element => string.Equals(element.Text, "Wi-Fi", StringComparison.Ordinal)
                        && element.SwitchState is true);
                var evidence = new GoalEvidence(
                    satisfied,
                    satisfied ? "Fresh reordered-layout Wi-Fi ON GoalEvidence." : "Wi-Fi ON remains unproven.",
                    observation.SequenceNumber);
                goalEvidence.Add(evidence);
                return evidence;
            },
            (_, candidate) =>
            {
                safetyOrder.Add(candidate.Index);
                var authorized = candidate.Text is "Wi-Fi" or "Wi-Fi Calling";
                return new CandidateAuthorizationEvidence(
                    authorized,
                    authorized
                        ? $"Independent safe navigation receipt for text={candidate.Text}, index={candidate.Index}."
                        : $"Candidate text={candidate.Text}, index={candidate.Index} is outside this action authority.");
            });

        var criterion = new TargetGroundingCriterion(
            (_, candidate) =>
            {
                groundingOrder.Add(candidate.Index);
                var wifiTextMatch = candidate.Text.Contains("Wi-Fi", StringComparison.Ordinal);
                var stateBearingSupport = candidate.SwitchState is false;
                return new TargetGroundingEvidence(
                    wifiTextMatch && stateBearingSupport,
                    wifiTextMatch && stateBearingSupport
                        ? $"Current candidate text={candidate.Text}, index={candidate.Index} has state-bearing OFF support."
                        : $"Current candidate text={candidate.Text}, index={candidate.Index} lacks combined Wi-Fi text and OFF-state support.");
            },
            observation =>
            {
                postActionEvidenceSequences.Add(observation.SequenceNumber);
                var expectedDestination = observation.Elements.Any(
                        element => string.Equals(element.Text, "Wi-Fi", StringComparison.Ordinal)
                            && element.SwitchState is false)
                    && !observation.Elements.Any(
                        element => string.Equals(element.Text, "Mobile network", StringComparison.Ordinal)
                            || string.Equals(element.Text, "Wi-Fi Calling", StringComparison.Ordinal));
                return expectedDestination
                    ? new TargetGroundingEvidence(true, "Fresh Wi-Fi settings evidence confirms the expected destination.")
                    : new TargetGroundingEvidence(null, "Fresh evidence does not uniquely confirm the expected Wi-Fi destination.");
            });

        var plan = new Plan([
            new PlanStep("Wi-Fi", "Tap", TargetGroundingCriterion: criterion),
            new PlanStep("Wi-Fi", "SetSwitch true"),
        ]);
        var envelope = IntentSemanticEnvelope.Project(
            Intent,
            goal,
            new IntentExecutionRepresentation.ClosedWorldConcrete(plan));

        static string? ResolvePage(Observation observation)
        {
            if (!string.Equals(observation.ForegroundApplication, "Settings", StringComparison.Ordinal))
                return null;
            if (observation.Elements.Any(element => element.Text == "Wi-Fi Calling Settings"))
                return "WifiCalling";
            if (observation.Elements.Any(element => element.Text == "Mobile network"))
                return "Candidates";
            if (observation.Elements.Any(element => element.Text == "Wi-Fi"))
                return "WifiSettings";
            return null;
        }

        RuntimeContainer Factory(string page) => new(
            page,
            observation => ResolvePage(observation) == page,
            traversal.ExecuteStep,
            forwardsAuthorizationReceipts: true);

        var startup = new RuntimeStartup(environment, "Settings", ResolvePage);
        var recovery = new RuntimeRecovery(
            environment,
            _ => ImmutableArray<DeviceAction>.Empty,
            (_, _) => null,
            (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            token => environment.ObserveAsync(token),
            ResolvePage,
            Factory,
            recovery);

        return new U3F1WifiVariationRun(
            agent,
            traversal,
            environment,
            envelope,
            plan,
            goalEvidence,
            safetyOrder,
            groundingOrder,
            postActionEvidenceSequences);
    }
}

internal sealed record U3F1WifiVariationRun(
    RuntimeAgent Agent,
    RuntimeTraversal Traversal,
    ScriptedEnvironment Environment,
    IntentSemanticEnvelope.Resolved Envelope,
    Plan Plan,
    IReadOnlyList<GoalEvidence> GoalEvidence,
    IReadOnlyList<int> SafetyOrder,
    IReadOnlyList<int> GroundingOrder,
    IReadOnlyList<long> PostActionEvidenceSequences)
{
    internal Task<RunState> RunAsync(string runId)
    {
        if (Envelope.Representation is not IntentExecutionRepresentation.ClosedWorldConcrete closedWorld)
            throw new InvalidOperationException("SC-U3-F1-001 requires the existing closed-world concrete representation.");
        if (!ReferenceEquals(closedWorld.Plan, Plan))
            throw new InvalidOperationException("The upstream projection did not preserve the caller-supplied Plan exactly.");
        return Agent.RunAsync(Envelope.Goal, closedWorld.Plan, runId, CancellationToken.None);
    }
}
