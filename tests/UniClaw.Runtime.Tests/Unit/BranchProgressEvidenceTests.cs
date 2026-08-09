using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class BranchProgressEvidenceTests
{
    [Fact]
    public void Constructor_RequiresNonblankParentAndBranchIdentities()
    {
        var approved = Evidence(("Branch A", 2L), ("Branch B", 2L));

        Assert.Throws<ArgumentException>(() => new BranchProgressEvidence(" ", approved, EmptyEvidence()));
        Assert.Throws<ArgumentException>(() => new BranchProgressEvidence(
            "ParentP",
            Evidence(("", 2L)),
            EmptyEvidence()));
        Assert.Throws<ArgumentException>(() => new BranchProgressEvidence(
            "ParentP",
            approved,
            Evidence((" ", 3L))));
    }

    [Fact]
    public void Constructor_RequiresNonnegativeSequencesAndCompletedSubset()
    {
        var approved = Evidence(("Branch A", 2L), ("Branch B", 2L));

        Assert.Throws<ArgumentOutOfRangeException>(() => new BranchProgressEvidence(
            "ParentP",
            Evidence(("Branch A", -1L)),
            EmptyEvidence()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BranchProgressEvidence(
            "ParentP",
            approved,
            Evidence(("Branch A", -1L))));
        Assert.Throws<ArgumentException>(() => new BranchProgressEvidence(
            "ParentP",
            approved,
            Evidence(("Branch C", 3L))));
    }

    [Fact]
    public void WithCompletedSibling_ReturnsImmutableIdempotentSnapshotsAndDerivesCompletion()
    {
        var initial = new BranchProgressEvidence(
            "ParentP",
            Evidence(("Branch A", 2L), ("Branch B", 2L)),
            EmptyEvidence());

        var afterA = initial.WithCompletedSibling("Branch A", 4);
        var revisitA = afterA.WithCompletedSibling("Branch A", 8);
        var afterB = revisitA.WithCompletedSibling("Branch B", 10);

        Assert.Empty(initial.CompletedSiblingEvidence);
        Assert.False(initial.IsSubtreeComplete);
        Assert.Single(afterA.CompletedSiblingEvidence);
        Assert.Single(revisitA.CompletedSiblingEvidence);
        Assert.Equal(8, revisitA.CompletedSiblingEvidence["Branch A"]);
        Assert.False(revisitA.IsSubtreeComplete);
        Assert.True(afterB.IsSubtreeComplete);
        Assert.Throws<ArgumentException>(() => initial.WithCompletedSibling("Branch C", 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => initial.WithCompletedSibling("Branch A", -1));
    }

    private static ImmutableDictionary<string, long> Evidence(params (string Identity, long Sequence)[] values)
        => values.ToImmutableDictionary(value => value.Identity, value => value.Sequence, StringComparer.Ordinal);

    private static ImmutableDictionary<string, long> EmptyEvidence()
        => ImmutableDictionary<string, long>.Empty.WithComparers(StringComparer.Ordinal);
}
