using System;
using System.Linq;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// ExecutionHypothesis lifecycle: exactly Created / Active / Confirmed / Revised /
/// Replaced. A revised hypothesis records a non-blank revision reason; a replaced
/// hypothesis is superseded by a new Created hypothesis.
/// </summary>
public sealed class ExecutionHypothesisLifecycleTests
{
    private static ExecutionHypothesis Create(ExecutionHypothesisStatus status)
        => new(
            "run-1", "Settings/SettingsRoot", "objective", "Discover -> Authorize -> Expand",
            "Exhaustive coverage within declared scope", 0.8f, null, null, status);

    [Fact]
    public void StatusEnum_ContainsExactlyTheFiveLifecycleStates()
    {
        var names = Enum.GetNames<ExecutionHypothesisStatus>();
        Assert.Equal(
            new[]
            {
                nameof(ExecutionHypothesisStatus.Created),
                nameof(ExecutionHypothesisStatus.Active),
                nameof(ExecutionHypothesisStatus.Confirmed),
                nameof(ExecutionHypothesisStatus.Revised),
                nameof(ExecutionHypothesisStatus.Replaced),
            },
            names);

        // The numeric values are stable and exactly 1..5.
        Assert.Equal(1, (int)ExecutionHypothesisStatus.Created);
        Assert.Equal(2, (int)ExecutionHypothesisStatus.Active);
        Assert.Equal(3, (int)ExecutionHypothesisStatus.Confirmed);
        Assert.Equal(4, (int)ExecutionHypothesisStatus.Revised);
        Assert.Equal(5, (int)ExecutionHypothesisStatus.Replaced);
    }

    [Fact]
    public void RevisedHypothesis_RecordsANonBlankRevisionReason()
    {
        var revised = Create(ExecutionHypothesisStatus.Revised) with
        {
            RevisionReason = "EXTERNAL_BOUNDARY_OBSERVED: boundary",
        };

        Assert.Equal(ExecutionHypothesisStatus.Revised, revised.Status);
        Assert.False(string.IsNullOrWhiteSpace(revised.RevisionReason));
        Assert.Contains("EXTERNAL_BOUNDARY_OBSERVED", revised.RevisionReason, StringComparison.Ordinal);
    }

    [Fact]
    public void WithBasedRevision_PreservesIdentityButChangesLifecycleFields()
    {
        var original = Create(ExecutionHypothesisStatus.Active);

        var revised = original with
        {
            Status = ExecutionHypothesisStatus.Revised,
            Confidence = original.Confidence * 0.5f,
        };

        // Identity fields are preserved; only the lifecycle and confidence change.
        Assert.Equal(original.RunId, revised.RunId);
        Assert.Equal(original.DirectiveReference, revised.DirectiveReference);
        Assert.Equal(original.Objective, revised.Objective);
        Assert.Equal(ExecutionHypothesisStatus.Revised, revised.Status);
        Assert.Equal(0.4f, revised.Confidence);
    }

    [Fact]
    public void ReplacedHypothesis_IsSupersededByANewCreatedHypothesis()
    {
        var replaced = Create(ExecutionHypothesisStatus.Replaced);
        var successor = Create(ExecutionHypothesisStatus.Created) with
        {
            Objective = "Continue remaining siblings within declared scope",
        };

        Assert.Equal(ExecutionHypothesisStatus.Replaced, replaced.Status);
        Assert.Equal(ExecutionHypothesisStatus.Created, successor.Status);
        Assert.False(string.Equals(replaced.Objective, successor.Objective, StringComparison.Ordinal));
    }
}
