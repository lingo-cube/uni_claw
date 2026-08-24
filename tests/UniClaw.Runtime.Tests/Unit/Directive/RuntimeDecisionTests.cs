using System;
using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// RuntimeDecision construction discipline: an immutable, passive record exposing ONLY
/// decision fields (RunId, State, HypothesisReference, EvidenceReference, DecisionReason).
/// It rejects blank identity/reason and undefined state, exposes exactly the three decision
/// states (Continue/Revise/Escalate), and carries NO Action, authorization, UI element
/// selection, Goal modification, Traversal control, or scenario strings.
/// </summary>
public sealed class RuntimeDecisionTests
{
    private static RuntimeDecision Sample(
        string runId = "run-1",
        RuntimeDecisionState state = RuntimeDecisionState.Continue,
        string hypothesisReference = "run-1",
        string evidenceReference = "in-scope progress",
        string decisionReason = "Hypothesis consistent with the observed world.")
        => new(runId, state, hypothesisReference, evidenceReference, decisionReason);

    [Fact]
    public void Constructor_ExposesExactlyTheDecisionFields()
    {
        var decision = Sample();

        Assert.Equal("run-1", decision.RunId);
        Assert.Equal(RuntimeDecisionState.Continue, decision.State);
        Assert.Equal("run-1", decision.HypothesisReference);
        Assert.Equal("in-scope progress", decision.EvidenceReference);
        Assert.Equal("Hypothesis consistent with the observed world.", decision.DecisionReason);
    }

    [Fact]
    public void StateEnum_ContainsExactlyContinueReviseAndEscalate()
    {
        var states = Enum.GetValues<RuntimeDecisionState>().OrderBy(value => (int)value).ToArray();

        Assert.Equal(
            new[]
            {
                RuntimeDecisionState.Continue,
                RuntimeDecisionState.Revise,
                RuntimeDecisionState.Escalate,
            },
            states);
        Assert.Equal(1, (int)RuntimeDecisionState.Continue);
        Assert.Equal(2, (int)RuntimeDecisionState.Revise);
        Assert.Equal(3, (int)RuntimeDecisionState.Escalate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankRunId(string? runId)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(runId: runId!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankDecisionReason(string? decisionReason)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(decisionReason: decisionReason!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankHypothesisReference(string? hypothesisReference)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(hypothesisReference: hypothesisReference!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankEvidenceReference(string? evidenceReference)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(evidenceReference: evidenceReference!));
    }

    [Fact]
    public void Constructor_RejectsUndefinedState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample(state: (RuntimeDecisionState)99));
    }

    [Fact]
    public void Decision_ExposesNoAuthorizingOrExecutingMethodOrProperty()
    {
        var publicMethods = typeof(RuntimeDecision)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();
        var publicProperties = typeof(RuntimeDecision)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToArray();

        // No method that authorizes, decides, completes, executes, or dispatches.
        foreach (var forbidden in new[]
        {
            "Authorize", "Decide", "Complete", "Execute", "Dispatch",
            "Evaluate", "CreateContainer", "SubRun", "StartRun", "Apply",
        })
        {
            Assert.DoesNotContain(publicMethods,
                name => name.Contains(forbidden, StringComparison.Ordinal));
        }

        // Only the decision fields — no Action, UI element, Goal, or Traversal control.
        Assert.Equal(
            new[]
            {
                "DecisionReason",
                "EvidenceReference",
                "HypothesisReference",
                "RunId",
                "State",
            },
            publicProperties.OrderBy(name => name).ToArray());
    }

    [Fact]
    public void Decision_CarriesNoAuthorizationOrCompletionEvidenceType()
    {
        var propertyTypes = typeof(RuntimeDecision)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.DoesNotContain(propertyTypes,
            type => typeof(CandidateAuthorizationEvidence).IsAssignableFrom(type)
                || typeof(GoalEvidence).IsAssignableFrom(type)
                || typeof(DeviceAction).IsAssignableFrom(type)
                || typeof(ObservedElement).IsAssignableFrom(type));
    }

    [Fact]
    public void Decision_IsPassiveAndImmutable()
    {
        var first = Sample();
        var second = Sample();

        Assert.Equal(first, second);
        Assert.NotSame(first, second);
    }
}
