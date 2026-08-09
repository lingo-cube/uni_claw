using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-P3-CAND-009 Task 1.1 deterministic fixture tests: one parent P, accepted inventory evidence for
/// required siblings A and B, A absent from the initial Plan and independently authorized (CAND-006),
/// A evidence-completed in historical progress (CAND-004) while B remains required and unresolved,
/// the singular Goal-held carrier identity exactly A, and fresh post-verification Observations
/// expressing positive / contradicted / unresolved / absent-carrier / identity-mismatch /
/// ambiguous-parent-scope / stale-evidence / equal-input replay. Fixture-side expression only; no
/// Agent behavior is exercised.
/// </summary>
public sealed class DiscoveredBranchEffectRevalidationFixtureTests
{
    [Fact]
    public async Task Positive_ExpressesPlanAbsentAAuthorizedCompletedProgressAndFreshTrue()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.Positive().RunAsync();

        Assert.Equal(DiscoveredBranchEffectRevalidationFixture.DefaultRunId, evidence.RunId);

        // A is absent from the initial immutable Plan targets; B remains planned.
        Assert.DoesNotContain(
            evidence.InitialPlan.Steps,
            step => step.TargetDescription == DiscoveredBranchEffectRevalidationFixture.BranchA);
        Assert.Contains(
            evidence.InitialPlan.Steps,
            step => step.TargetDescription == DiscoveredBranchEffectRevalidationFixture.BranchB);

        // Accepted SC-P3-CAND-008 inventory evidence proves required siblings A and B under P.
        Assert.Equal(
            new[]
            {
                DiscoveredBranchEffectRevalidationFixture.BranchA,
                DiscoveredBranchEffectRevalidationFixture.BranchB,
            },
            evidence.Inventory.RequiredBranchEvidence!.Keys.Order());
        Assert.Equal(
            evidence.Observations[0].SequenceNumber,
            evidence.Inventory.RequiredBranchEvidence[DiscoveredBranchEffectRevalidationFixture.BranchA]);
        Assert.Equal(
            evidence.Observations[0].SequenceNumber,
            evidence.Inventory.RequiredBranchEvidence[DiscoveredBranchEffectRevalidationFixture.BranchB]);

        // A is independently authorized under SC-P3-CAND-006.
        Assert.True(evidence.AAuthorization.Authorized);

        // SC-P3-CAND-004 historical progress: A evidence-completed under the same active parent; B unresolved.
        Assert.Equal(evidence.ActiveParentSemanticPage, evidence.HistoricalProgress.ParentSemanticPage);
        Assert.Equal(
            new[]
            {
                DiscoveredBranchEffectRevalidationFixture.BranchA,
                DiscoveredBranchEffectRevalidationFixture.BranchB,
            },
            evidence.HistoricalProgress.ApprovedSiblingEvidence.Keys.Order());
        Assert.Equal(
            new[] { DiscoveredBranchEffectRevalidationFixture.BranchA },
            evidence.HistoricalProgress.CompletedSiblingEvidence.Keys);
        Assert.False(evidence.HistoricalProgress.IsSubtreeComplete);

        // Singular Goal-held carrier identity is exactly A and matches inventory + completion provenance.
        Assert.Equal(DiscoveredBranchEffectRevalidationFixture.BranchA, evidence.Carrier!.BranchIdentity);
        Assert.Same(evidence.Carrier, evidence.MatchedCarrier);

        // Fresh recovered-world Observation after verified Recovery evaluates positive.
        Assert.Equal("Settings", evidence.FreshRecoveredObservation.ForegroundApplication);
        Assert.True(evidence.FreshCriterionOutcome);
        Assert.True(evidence.StaleCriterionOutcome);
        Assert.True(
            evidence.FreshRecoveredObservation.Elements.Single(element =>
                element.Text == "A external effect").SwitchState);

        // World walk: P → A's child → A effect → P (stale) → drift → recovered P (fresh) → B's child.
        AssertElements(evidence.Observations[0], "Branch A", "Branch B");
        AssertElements(evidence.Observations[5], "Branch A", "Branch B", "A external effect");
        Assert.Equal("Launcher", evidence.Observations[4].ForegroundApplication);
        Assert.Equal(
            Enumerable.Range(1, 7).Select(value => (long)value),
            evidence.Observations.Select(observation => observation.SequenceNumber));
        Assert.Single(
            evidence.ActionHistory.OfType<DeviceAction.Tap>().Where(action => action.TargetElementIndex == 0));
        Assert.Equal(5, evidence.ActionHistory.Length);
        foreach (var dispatch in evidence.Dispatches)
        {
            Assert.Equal(ActionResultOutcome.Dispatched, dispatch.Outcome);
        }
    }

    [Fact]
    public async Task Contradicted_FreshRecoveredEvidenceYieldsFalseWhileStaleYieldsTrue()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.Contradicted().RunAsync();

        Assert.Same(evidence.Carrier, evidence.MatchedCarrier);
        Assert.False(evidence.FreshCriterionOutcome);
        Assert.True(evidence.StaleCriterionOutcome);
        Assert.False(
            evidence.FreshRecoveredObservation.Elements.Single(element =>
                element.Text == "A external effect").SwitchState);
        Assert.NotEqual(
            evidence.StalePreRecoveryObservation.SequenceNumber,
            evidence.FreshRecoveredObservation.SequenceNumber);
    }

    [Fact]
    public async Task Unresolved_FreshRecoveredEvidenceYieldsNull()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.Unresolved().RunAsync();

        Assert.Same(evidence.Carrier, evidence.MatchedCarrier);
        Assert.Null(evidence.FreshCriterionOutcome);
        Assert.True(evidence.StaleCriterionOutcome);
        Assert.DoesNotContain(
            evidence.FreshRecoveredObservation.Elements,
            element => element.Text == "A external effect");
    }

    [Fact]
    public async Task AbsentCarrier_GoalDefaultsAbsentAndStaysUnresolved()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.AbsentCarrier().RunAsync();

        Assert.Null(evidence.Carrier);
        Assert.Null(evidence.Goal.DiscoveredBranchEffectCriterion);
        Assert.Null(evidence.MatchedCarrier);
        Assert.Null(evidence.FreshCriterionOutcome);
        Assert.Null(evidence.StaleCriterionOutcome);
    }

    [Fact]
    public async Task IdentityMismatch_CarrierCannotAttachAndStaysUnresolved()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.IdentityMismatch().RunAsync();

        Assert.Equal(DiscoveredBranchEffectRevalidationFixture.MismatchedIdentity, evidence.Carrier!.BranchIdentity);
        Assert.Null(evidence.MatchedCarrier);
        Assert.Null(evidence.FreshCriterionOutcome);
        Assert.Null(evidence.StaleCriterionOutcome);
    }

    [Fact]
    public async Task AmbiguousParentScope_CarrierCannotMatchAndStaysUnresolved()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.AmbiguousParent().RunAsync();

        Assert.Equal(
            DiscoveredBranchEffectRevalidationFixture.ActiveParentSemanticPage,
            evidence.ActiveParentSemanticPage);
        Assert.Equal(
            DiscoveredBranchEffectRevalidationFixture.ConflictingParentSemanticPage,
            evidence.HistoricalProgress.ParentSemanticPage);
        Assert.Equal(DiscoveredBranchEffectRevalidationFixture.BranchA, evidence.Carrier!.BranchIdentity);
        Assert.Null(evidence.MatchedCarrier);
        Assert.Null(evidence.FreshCriterionOutcome);
        Assert.Null(evidence.StaleCriterionOutcome);
    }

    [Fact]
    public async Task StaleEvidence_PreRecoveryObservationCannotSubstituteForFresh()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.StaleEvidence().RunAsync();

        // The stale pre-Recovery snapshot is a non-advancing Observation with positively evaluable content...
        Assert.Equal(evidence.Observations[2].SequenceNumber, evidence.StalePreRecoveryObservation.SequenceNumber);
        Assert.True(
            evidence.StalePreRecoveryObservation.Elements.Single(element =>
                element.Text == "A external effect").SwitchState);
        Assert.True(evidence.StaleCriterionOutcome);
        // ...while the fresh post-verification Observation has a distinct advancing sequence and no effect evidence.
        Assert.NotEqual(
            evidence.StalePreRecoveryObservation.SequenceNumber,
            evidence.FreshRecoveredObservation.SequenceNumber);
        Assert.DoesNotContain(
            evidence.FreshRecoveredObservation.Elements,
            element => element.Text == "A external effect");
        Assert.Null(evidence.FreshCriterionOutcome);
        Assert.Same(evidence.Carrier, evidence.MatchedCarrier);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("contradicted")]
    [InlineData("unresolved")]
    [InlineData("absent-carrier")]
    [InlineData("identity-mismatch")]
    [InlineData("ambiguous-parent")]
    [InlineData("stale-evidence")]
    public async Task EqualInputs_ReplayCarrierObservationsActionsAndWorldDeterministically(string path)
    {
        var first = await Create(path);
        var second = await Create(path);

        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.Carrier?.BranchIdentity, second.Carrier?.BranchIdentity);
        Assert.Equal(first.MatchedCarrier?.BranchIdentity, second.MatchedCarrier?.BranchIdentity);
        Assert.Equal(first.ActiveParentSemanticPage, second.ActiveParentSemanticPage);
        Assert.Equal(first.AAuthorization, second.AAuthorization);
        Assert.Equal(first.Inventory.Reason, second.Inventory.Reason);
        AssertEvidenceMapEqual(first.Inventory.RequiredBranchEvidence, second.Inventory.RequiredBranchEvidence);
        Assert.Equal(first.HistoricalProgress.ParentSemanticPage, second.HistoricalProgress.ParentSemanticPage);
        AssertEvidenceMapEqual(
            first.HistoricalProgress.ApprovedSiblingEvidence,
            second.HistoricalProgress.ApprovedSiblingEvidence);
        AssertEvidenceMapEqual(
            first.HistoricalProgress.CompletedSiblingEvidence,
            second.HistoricalProgress.CompletedSiblingEvidence);
        Assert.Equal(first.StaleCriterionOutcome, second.StaleCriterionOutcome);
        Assert.Equal(first.FreshCriterionOutcome, second.FreshCriterionOutcome);
        Assert.Equal(first.Dispatches.ToArray(), second.Dispatches.ToArray());
        Assert.Equal(first.ActionHistory.ToArray(), second.ActionHistory.ToArray());
        Assert.Equal(first.Observations.Length, second.Observations.Length);
        for (var index = 0; index < first.Observations.Length; index++)
        {
            Assert.Equal(first.Observations[index].ForegroundApplication, second.Observations[index].ForegroundApplication);
            Assert.Equal(first.Observations[index].SequenceNumber, second.Observations[index].SequenceNumber);
            Assert.Equal(first.Observations[index].Elements, second.Observations[index].Elements);
        }
    }

    private static Task<DiscoveredBranchEffectWorldEvidence> Create(string path) => path switch
    {
        "positive" => DiscoveredBranchEffectRevalidationFixture.Positive().RunAsync(),
        "contradicted" => DiscoveredBranchEffectRevalidationFixture.Contradicted().RunAsync(),
        "unresolved" => DiscoveredBranchEffectRevalidationFixture.Unresolved().RunAsync(),
        "absent-carrier" => DiscoveredBranchEffectRevalidationFixture.AbsentCarrier().RunAsync(),
        "identity-mismatch" => DiscoveredBranchEffectRevalidationFixture.IdentityMismatch().RunAsync(),
        "ambiguous-parent" => DiscoveredBranchEffectRevalidationFixture.AmbiguousParent().RunAsync(),
        "stale-evidence" => DiscoveredBranchEffectRevalidationFixture.StaleEvidence().RunAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(path)),
    };

    private static void AssertEvidenceMapEqual(
        IReadOnlyDictionary<string, long>? expected,
        IReadOnlyDictionary<string, long>? actual)
    {
        if (expected is null || actual is null)
        {
            Assert.Equal(expected is null, actual is null);
            return;
        }

        Assert.Equal(expected.OrderBy(entry => entry.Key), actual.OrderBy(entry => entry.Key));
    }

    private static void AssertElements(Observation observation, params string[] expected)
        => Assert.Equal(expected, observation.Elements.Select(element => element.Text));
}
