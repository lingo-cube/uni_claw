using System;
using System.Linq;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// HypothesisAdaptation construction discipline: an immutable, passive record exposing
/// ONLY adaptation fields (RunId, AdaptationType, DecisionReference,
/// PreviousHypothesisReference, AdaptedHypothesis, AdaptationReason). It rejects blank
/// identity/references/reason, an undefined type, and a null adapted hypothesis; exposes
/// exactly the three adaptation types (Keep/Replace/Escalate); and carries NO Action,
/// authorization, UI element selection, Goal modification, Traversal control, or
/// scenario strings.
/// </summary>
public sealed class HypothesisAdaptationTests
{
    private static ExecutionHypothesis Hypothesis(
        ExecutionHypothesisStatus status = ExecutionHypothesisStatus.Active,
        string? revisionReason = null)
        => new(
            runId: "run-1",
            directiveReference: "Application/Root",
            objective: "Explore declared scope within bounded depth",
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.8f,
            revisionReason: revisionReason,
            createdAtObservation: null,
            status: status);

    private static HypothesisAdaptation Sample(
        string runId = "run-1",
        HypothesisAdaptationType type = HypothesisAdaptationType.Keep,
        string decisionReference = "run-1",
        string previousHypothesisReference = "run-1",
        ExecutionHypothesis? adapted = null,
        string adaptationReason = "Hypothesis consistent with the observed world.")
        => new(
            runId,
            type,
            decisionReference,
            previousHypothesisReference,
            adapted ?? Hypothesis(),
            adaptationReason);

    [Fact]
    public void Constructor_ExposesExactlyTheAdaptationFields()
    {
        var adapted = Hypothesis(ExecutionHypothesisStatus.Confirmed);
        var adaptation = new HypothesisAdaptation(
            "run-1",
            HypothesisAdaptationType.Replace,
            "run-1",
            "run-1",
            adapted,
            "Boundary-aware replacement records a hypothesis update only.");

        Assert.Equal("run-1", adaptation.RunId);
        Assert.Equal(HypothesisAdaptationType.Replace, adaptation.AdaptationType);
        Assert.Equal("run-1", adaptation.DecisionReference);
        Assert.Equal("run-1", adaptation.PreviousHypothesisReference);
        Assert.Same(adapted, adaptation.AdaptedHypothesis);
        Assert.Equal("Boundary-aware replacement records a hypothesis update only.", adaptation.AdaptationReason);
    }

    [Fact]
    public void TypeEnum_ContainsExactlyKeepReplaceAndEscalate()
    {
        var types = Enum.GetValues<HypothesisAdaptationType>()
            .OrderBy(value => (int)value)
            .ToArray();

        Assert.Equal(
            new[]
            {
                HypothesisAdaptationType.Keep,
                HypothesisAdaptationType.Replace,
                HypothesisAdaptationType.Escalate,
            },
            types);
        Assert.Equal(1, (int)HypothesisAdaptationType.Keep);
        Assert.Equal(2, (int)HypothesisAdaptationType.Replace);
        Assert.Equal(3, (int)HypothesisAdaptationType.Escalate);
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
    public void Constructor_RejectsBlankDecisionReference(string? decisionReference)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(decisionReference: decisionReference!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankPreviousHypothesisReference(string? previousHypothesisReference)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(previousHypothesisReference: previousHypothesisReference!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankAdaptationReason(string? adaptationReason)
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(adaptationReason: adaptationReason!));
    }

    [Fact]
    public void Constructor_RejectsUndefinedType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample(type: (HypothesisAdaptationType)99));
    }

    [Fact]
    public void Constructor_RejectsNullAdaptedHypothesis()
    {
        Assert.ThrowsAny<ArgumentException>(() => new HypothesisAdaptation(
            "run-1",
            HypothesisAdaptationType.Keep,
            "run-1",
            "run-1",
            null!,
            "Hypothesis consistent with the observed world."));
    }

    [Fact]
    public void Adaptation_ExposesNoAuthorizingOrExecutingMethodOrProperty()
    {
        var publicMethods = typeof(HypothesisAdaptation)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();
        var publicProperties = typeof(HypothesisAdaptation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToArray();

        // No method that authorizes, decides, completes, executes, dispatches, or mutates.
        foreach (var forbidden in new[]
        {
            "Authorize", "Decide", "Complete", "Execute", "Dispatch",
            "Evaluate", "CreateContainer", "SubRun", "StartRun", "Apply", "Mutate",
        })
        {
            Assert.DoesNotContain(publicMethods,
                name => name.Contains(forbidden, StringComparison.Ordinal));
        }

        // Only the adaptation fields — no Plan, Action, UI element, Goal, or Traversal control.
        Assert.Equal(
            new[]
            {
                "AdaptationReason",
                "AdaptationType",
                "AdaptedHypothesis",
                "DecisionReference",
                "PreviousHypothesisReference",
                "RunId",
            },
            publicProperties.OrderBy(name => name).ToArray());
    }

    [Fact]
    public void Adaptation_CarriesNoAuthorizationCompletionOrExecutionEvidenceType()
    {
        var propertyTypes = typeof(HypothesisAdaptation)
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
    public void Adaptation_IsPassiveAndImmutable()
    {
        var first = Sample();
        var second = Sample();

        Assert.Equal(first, second);
        Assert.NotSame(first, second);
    }
}