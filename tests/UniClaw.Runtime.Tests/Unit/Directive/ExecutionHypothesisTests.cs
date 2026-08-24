using System;
using System.Linq;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// ExecutionHypothesis construction discipline: an immutable, passive record exposing
/// ONLY execution-assumption fields. It rejects blank identity/objective, rejects
/// confidence outside [0, 1], and carries NO Plan, coordinates, DeviceAction, element
/// index, scenario strings, or authorization rules.
/// </summary>
public sealed class ExecutionHypothesisTests
{
    private static ExecutionHypothesis Sample(string? objective = "Explore declared scope within bounded depth")
        => new(
            runId: "run-1",
            directiveReference: "Settings/SettingsRoot",
            objective: objective!,
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.8f,
            revisionReason: null,
            createdAtObservation: null,
            status: ExecutionHypothesisStatus.Created);

    [Fact]
    public void Construction_ExposesExactlyTheAssumptionFields()
    {
        var hypothesis = new ExecutionHypothesis(
            runId: "run-1",
            directiveReference: "Settings/SettingsRoot",
            objective: "Explore declared scope within bounded depth",
            expectedTransition: "Discover -> Authorize -> Expand",
            expectedOutcome: "Exhaustive coverage within declared scope",
            confidence: 0.9f,
            revisionReason: "EXTERNAL_BOUNDARY_OBSERVED: boundary",
            createdAtObservation: 4,
            status: ExecutionHypothesisStatus.Revised);

        Assert.Equal("run-1", hypothesis.RunId);
        Assert.Equal("Settings/SettingsRoot", hypothesis.DirectiveReference);
        Assert.Equal("Explore declared scope within bounded depth", hypothesis.Objective);
        Assert.Equal("Discover -> Authorize -> Expand", hypothesis.ExpectedTransition);
        Assert.Equal("Exhaustive coverage within declared scope", hypothesis.ExpectedOutcome);
        Assert.Equal(0.9f, hypothesis.Confidence);
        Assert.Equal("EXTERNAL_BOUNDARY_OBSERVED: boundary", hypothesis.RevisionReason);
        Assert.Equal(4, hypothesis.CreatedAtObservation);
        Assert.Equal(ExecutionHypothesisStatus.Revised, hypothesis.Status);
    }

    [Fact]
    public void Construction_RejectsBlankRunId()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ExecutionHypothesis(
            "", "ref", "objective", "transition", "outcome", 0.8f, null, null,
            ExecutionHypothesisStatus.Created));
    }

    [Fact]
    public void Construction_RejectsBlankObjective()
    {
        Assert.ThrowsAny<ArgumentException>(() => Sample(objective: "   "));
    }

    [Fact]
    public void Construction_RejectsBlankDirectiveReference_AndBlankTransitionOutcome()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ExecutionHypothesis(
            "run", "", "objective", "transition", "outcome", 0.8f, null, null,
            ExecutionHypothesisStatus.Created));
        Assert.ThrowsAny<ArgumentException>(() => new ExecutionHypothesis(
            "run", "ref", "objective", "", "outcome", 0.8f, null, null,
            ExecutionHypothesisStatus.Created));
        Assert.ThrowsAny<ArgumentException>(() => new ExecutionHypothesis(
            "run", "ref", "objective", "transition", "", 0.8f, null, null,
            ExecutionHypothesisStatus.Created));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Construction_RejectsConfidenceOutsideZeroToOne(float confidence)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ExecutionHypothesis(
            "run", "ref", "objective", "transition", "outcome", confidence, null, null,
            ExecutionHypothesisStatus.Created));
    }

    [Fact]
    public void Construction_RejectsUndefinedStatus()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ExecutionHypothesis(
            "run", "ref", "objective", "transition", "outcome", 0.8f, null, null,
            (ExecutionHypothesisStatus)999));
    }

    [Fact]
    public void Construction_BoundaryConfidenceValuesAreAccepted()
    {
        // Confidence is inclusive on both ends: 0 and 1 are valid (bounded).
        Assert.Equal(0f, (Sample() with { Confidence = 0f }).Confidence);
        Assert.Equal(1f, (Sample() with { Confidence = 1f }).Confidence);
    }

    [Fact]
    public void Record_CarriesNoForbiddenContent()
    {
        var hypothesis = Sample();

        var allowed = new[]
        {
            nameof(ExecutionHypothesis.RunId),
            nameof(ExecutionHypothesis.DirectiveReference),
            nameof(ExecutionHypothesis.Objective),
            nameof(ExecutionHypothesis.ExpectedTransition),
            nameof(ExecutionHypothesis.ExpectedOutcome),
            nameof(ExecutionHypothesis.Confidence),
            nameof(ExecutionHypothesis.RevisionReason),
            nameof(ExecutionHypothesis.CreatedAtObservation),
            nameof(ExecutionHypothesis.Status),
        };

        var propertyNames = typeof(ExecutionHypothesis).GetProperties()
            .Select(property => property.Name)
            .ToArray();

        // The record exposes exactly the assumption fields and nothing that carries
        // a Plan, coordinates, a DeviceAction, an element index, or a scenario string.
        Assert.Equal(allowed.OrderBy(name => name, StringComparer.Ordinal), propertyNames.OrderBy(name => name, StringComparer.Ordinal));

        Assert.DoesNotContain(propertyNames,
            name => name.Contains("Plan", StringComparison.Ordinal)
                || name.Contains("Coordinate", StringComparison.Ordinal)
                || name.Contains("Bounds", StringComparison.Ordinal)
                || name.Contains("Index", StringComparison.Ordinal)
                || name.Contains("Action", StringComparison.Ordinal)
                || name.Contains("Scenario", StringComparison.Ordinal));

        // No field declared as a Plan, DeviceAction, or coordinate-bearing type.
        Assert.DoesNotContain(typeof(ExecutionHypothesis).GetProperties(),
            property => typeof(DeviceAction).IsAssignableFrom(property.PropertyType));
    }
}
