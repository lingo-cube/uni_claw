using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class BranchInventoryEvidenceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingReason(string? reason)
        => Assert.ThrowsAny<ArgumentException>(() => new BranchInventoryEvidence(null, reason!));

    [Fact]
    public void Constructor_RejectsBlankIdentityAndNegativeSequence()
    {
        Assert.Throws<ArgumentException>(() => new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty.Add(" ", 1),
            "invalid identity"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty.Add("A", -1),
            "invalid sequence"));
    }

    [Fact]
    public void Constructor_PreservesCompleteLeafAndUnresolvedDistinction()
    {
        var complete = new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty.Add("A", 7),
            "complete bounded inventory");
        var leaf = new BranchInventoryEvidence(
            ImmutableDictionary<string, long>.Empty,
            "positive bounded leaf");
        var unresolved = new BranchInventoryEvidence(null, "inventory unresolved");

        Assert.Equal(7, Assert.Single(complete.RequiredBranchEvidence!).Value);
        Assert.Empty(leaf.RequiredBranchEvidence!);
        Assert.Null(unresolved.RequiredBranchEvidence);
        Assert.False(string.IsNullOrWhiteSpace(complete.Reason));
        Assert.False(string.IsNullOrWhiteSpace(leaf.Reason));
        Assert.False(string.IsNullOrWhiteSpace(unresolved.Reason));
    }
}
