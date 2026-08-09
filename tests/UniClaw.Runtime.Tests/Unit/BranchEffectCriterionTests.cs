using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// SC-P3-CAND-009 Task 1.1 targeted Model semantic: exactly two immutable fields, non-empty
/// BranchIdentity, non-null Evaluator, and the backward-compatible absent-by-default Goal carrier.
/// </summary>
public sealed class BranchEffectCriterionTests
{
    [Fact]
    public void Carrier_ExactlyTwoImmutableFields()
    {
        Assert.Equal(
            new[] { "BranchIdentity", "Evaluator" },
            typeof(BranchEffectCriterion)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        foreach (var property in typeof(BranchEffectCriterion).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.Null(property.SetMethod);
        }

        Assert.True(typeof(BranchEffectCriterion).IsSealed);
        Assert.True(typeof(BranchEffectCriterion).IsClass);
        Assert.True(
            typeof(BranchEffectCriterion).GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
            "BranchEffectCriterion 应为 record。");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void EmptyOrWhitespaceIdentity_IsRejected(string identity)
    {
        Assert.Throws<ArgumentException>(
            () => new BranchEffectCriterion(identity, _ => null));
    }

    [Fact]
    public void NullEvaluator_IsRejectedAndCanNeverBeInvoked()
    {
        // A null evaluator cannot be constructed, therefore it can never be invoked on any Observation.
        Assert.Throws<ArgumentNullException>(
            () => new BranchEffectCriterion("Branch A", null!));
    }

    [Fact]
    public void ValidCarrier_PreservesIdentityAndEvaluatesDeterministicallyOnSuppliedObservation()
    {
        var effectOn = new Observation(
            ImmutableArray.Create(new ObservedElement("A external effect", true, 0)),
            "Settings",
            6);
        var effectOff = new Observation(
            ImmutableArray.Create(new ObservedElement("A external effect", false, 0)),
            "Settings",
            7);

        var carrier = new BranchEffectCriterion("Branch A", EvaluateAEffect);

        Assert.Equal("Branch A", carrier.BranchIdentity);
        Assert.NotNull(carrier.Evaluator);
        Assert.True(carrier.Evaluator(effectOn));
        Assert.False(carrier.Evaluator(effectOff));
    }

    [Fact]
    public void Goal_ExistingConstruction_RemainsSourceCompatibleWithCarrierAbsent()
    {
        var goal = new Goal(observation => new GoalEvidence(false, "No completion in this slice.", observation.SequenceNumber));

        Assert.Null(goal.DiscoveredBranchEffectCriterion);
        Assert.NotNull(goal.EvidenceEvaluator);
        Assert.Null(goal.CandidateAuthorizationEvaluator);
        Assert.Null(goal.ViewportExplorationEvaluator);
        Assert.Null(goal.BranchInventoryEvaluator);
    }

    [Fact]
    public void Goal_ExplicitCarrier_IsPreservedAndAbsentRemainsBackwardCompatible()
    {
        var carrier = new BranchEffectCriterion("Branch A", EvaluateAEffect);
        var withCarrier = new Goal(
            observation => new GoalEvidence(false, "No completion in this slice.", observation.SequenceNumber),
            DiscoveredBranchEffectCriterion: carrier);
        var withoutCarrier = withCarrier with { DiscoveredBranchEffectCriterion = null };

        Assert.Same(carrier, withCarrier.DiscoveredBranchEffectCriterion);
        Assert.Equal("Branch A", withCarrier.DiscoveredBranchEffectCriterion!.BranchIdentity);
        Assert.Null(withoutCarrier.DiscoveredBranchEffectCriterion);
        Assert.Equal(withCarrier.EvidenceEvaluator, withoutCarrier.EvidenceEvaluator);
    }

    private static bool? EvaluateAEffect(Observation observation)
    {
        var matches = observation.Elements
            .Where(element => string.Equals(element.Text, "A external effect", StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0].SwitchState : null;
    }
}
