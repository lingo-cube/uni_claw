using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class CandidateAuthorizationEvidenceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingReason(string? reason)
        => Assert.ThrowsAny<ArgumentException>(() => new CandidateAuthorizationEvidence(null, reason!));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Constructor_PreservesThreeWayOutcome(bool? authorized)
    {
        var evidence = new CandidateAuthorizationEvidence(authorized, "bounded reason");

        Assert.Equal(authorized, evidence.Authorized);
        Assert.Equal("bounded reason", evidence.Reason);
    }
}
