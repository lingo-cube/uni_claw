using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class TargetGroundingEvidenceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingReason(string? reason)
        => Assert.ThrowsAny<ArgumentException>(() => new TargetGroundingEvidence(null, reason!));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Constructor_PreservesThreeWayOutcome(bool? supported)
        => Assert.Equal(supported, new TargetGroundingEvidence(supported, "observable reason").Supported);

    [Fact]
    public void Criterion_RejectsMissingPhaseEvaluator()
    {
        Func<Observation, ObservedElement, TargetGroundingEvidence> candidate =
            (_, _) => new TargetGroundingEvidence(true, "candidate");
        Func<Observation, TargetGroundingEvidence> post = _ => new TargetGroundingEvidence(true, "post");

        Assert.Throws<ArgumentNullException>(() => new TargetGroundingCriterion(null!, post));
        Assert.Throws<ArgumentNullException>(() => new TargetGroundingCriterion(candidate, null!));
    }
}
