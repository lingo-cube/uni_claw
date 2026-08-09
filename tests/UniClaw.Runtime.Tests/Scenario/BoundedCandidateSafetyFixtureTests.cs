using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class BoundedCandidateSafetyFixtureTests
{
    [Fact]
    public void ExistingGoalConstruction_RemainsCompatibleAndHasNoCandidateCriterion()
    {
        var goal = new Goal(_ => new GoalEvidence(false, "not complete", null));

        Assert.Null(goal.CandidateAuthorizationEvaluator);
    }

    [Fact]
    public void FreshCandidateSet_HasStableBoundedEvidence()
    {
        var fixture = BoundedCandidateSafetyFixture.Create();

        Assert.Equal(1, fixture.Observation.SequenceNumber);
        Assert.Equal("Settings", fixture.Observation.ForegroundApplication);
        Assert.Equal(
            new[]
            {
                (BoundedCandidateSafetyFixture.SafeText, (bool?)null, 0),
                (BoundedCandidateSafetyFixture.DestructiveText, (bool?)null, 1),
                (BoundedCandidateSafetyFixture.StateChangingText, (bool?)false, 2),
                (BoundedCandidateSafetyFixture.UnknownText, (bool?)null, 3),
            },
            fixture.Observation.Elements.Select(element => (element.Text, element.SwitchState, element.Index)));
    }

    [Fact]
    public void Criterion_DistinguishesSafeDestructiveStateChangingAndUnknown()
    {
        var fixture = BoundedCandidateSafetyFixture.Create();
        var evaluator = fixture.Goal.CandidateAuthorizationEvaluator
            ?? throw new InvalidOperationException("Expected bounded candidate evaluator.");

        AssertOutcome(evaluator(fixture.Observation, fixture.Safe), true, "safe navigation");
        AssertOutcome(evaluator(fixture.Observation, fixture.Destructive), false, "Destructive text");
        AssertOutcome(evaluator(fixture.Observation, fixture.StateChanging), false, "State-changing evidence");
        AssertOutcome(evaluator(fixture.Observation, fixture.Unknown), null, "cannot prove");
    }

    [Fact]
    public void Criterion_RejectsCandidateOutsideSuppliedObservation()
    {
        var fixture = BoundedCandidateSafetyFixture.Create();
        var evaluator = fixture.Goal.CandidateAuthorizationEvaluator!;
        var outside = new ObservedElement("About phone", null, 99);

        Assert.Throws<ArgumentException>(() => evaluator(fixture.Observation, outside));
    }

    [Fact]
    public void EqualInputs_ReplayEqualAuthorizationEvidence()
    {
        var first = BoundedCandidateSafetyFixture.Create();
        var second = BoundedCandidateSafetyFixture.Create();
        var firstEvaluator = first.Goal.CandidateAuthorizationEvaluator!;
        var secondEvaluator = second.Goal.CandidateAuthorizationEvaluator!;

        Assert.Equal(first.Observation.ForegroundApplication, second.Observation.ForegroundApplication);
        Assert.Equal(first.Observation.SequenceNumber, second.Observation.SequenceNumber);
        Assert.Equal(first.Observation.Elements.Length, second.Observation.Elements.Length);
        for (var index = 0; index < first.Observation.Elements.Length; index++)
        {
            Assert.Equal(first.Observation.Elements[index], second.Observation.Elements[index]);
            Assert.Equal(
                firstEvaluator(first.Observation, first.Observation.Elements[index]),
                secondEvaluator(second.Observation, second.Observation.Elements[index]));
        }
    }

    private static void AssertOutcome(
        CandidateAuthorizationEvidence actual,
        bool? authorized,
        string reasonFragment)
    {
        Assert.Equal(authorized, actual.Authorized);
        Assert.Contains(reasonFragment, actual.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
