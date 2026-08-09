using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>Test-only deterministic SC-P3-CAND-006 evidence set and bounded criterion.</summary>
internal sealed class BoundedCandidateSafetyFixture
{
    internal const string RunId = "sc-p3-cand-006-run";
    internal const string SafeText = "About phone";
    internal const string DestructiveText = "Reset options";
    internal const string StateChangingText = "Wi-Fi";
    internal const string UnknownText = "Custom operation";

    private BoundedCandidateSafetyFixture(Observation observation, Goal goal)
    {
        Observation = observation;
        Goal = goal;
    }

    internal Observation Observation { get; }

    internal Goal Goal { get; }

    internal ObservedElement Safe => Observation.Elements[0];

    internal ObservedElement Destructive => Observation.Elements[1];

    internal ObservedElement StateChanging => Observation.Elements[2];

    internal ObservedElement Unknown => Observation.Elements[3];

    internal static BoundedCandidateSafetyFixture Create()
    {
        var observation = new Observation(
            ImmutableArray.Create(
                new ObservedElement(SafeText, null, 0),
                new ObservedElement(DestructiveText, null, 1),
                new ObservedElement(StateChangingText, false, 2),
                new ObservedElement(UnknownText, null, 3)),
            "Settings",
            1);
        var goal = new Goal(
            candidateObservation => new GoalEvidence(
                candidateObservation.Elements.Any(element => element.Text == "About phone details"),
                candidateObservation.Elements.Any(element => element.Text == "About phone details")
                    ? "Fresh Observation proves safe navigation reached its bounded destination."
                    : "Bounded safe navigation has not been proven.",
                candidateObservation.SequenceNumber),
            EvaluateCandidate);
        return new BoundedCandidateSafetyFixture(observation, goal);
    }

    internal static CandidateAuthorizationEvidence EvaluateCandidate(
        Observation observation,
        ObservedElement candidate)
    {
        if (!observation.Elements.Contains(candidate))
            throw new ArgumentException("Candidate must be contained in the supplied Observation.", nameof(candidate));

        if (candidate.SwitchState is not null)
        {
            return new CandidateAuthorizationEvidence(
                false,
                "State-changing evidence is outside the bounded read-only Settings intent.");
        }

        if (candidate.Text.Contains("reset", StringComparison.OrdinalIgnoreCase))
        {
            return new CandidateAuthorizationEvidence(
                false,
                "Destructive text evidence overrides navigation-like appearance.");
        }

        if (string.Equals(candidate.Text, SafeText, StringComparison.Ordinal))
        {
            return new CandidateAuthorizationEvidence(
                true,
                "Fresh evidence identifies one bounded safe navigation row.");
        }

        return new CandidateAuthorizationEvidence(
            null,
            "Available evidence cannot prove bounded read-only authorization.");
    }
}
