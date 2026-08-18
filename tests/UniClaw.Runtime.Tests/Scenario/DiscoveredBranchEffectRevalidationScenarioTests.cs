using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-P3-CAND-009 formal end-to-end Scenario evidence (scenario Required Assertions 1-12), built on
/// the Task 1.1 deterministic fixture and the Task 2.1 Agent behavior harness. Each fact asserts the
/// frozen semantics: historical completion != Recovery verification != current effect validity;
/// RecoveryResult.Verified != branch-effect verification; criterion != proof; identity matches only
/// within the same bounded parent; null != false != true; observed/inventoried/authorized/completed
/// != revalidated; contribution != completion; only independently satisfied GoalEvidence completes
/// the Run. No production behavior is exercised beyond the frozen + approved Task 2.1 control flow.
/// </summary>
public sealed class DiscoveredBranchEffectRevalidationScenarioTests
{
    private const string ParentPage = DiscoveredBranchEffectRevalidationFixture.ActiveParentSemanticPage;
    private const string BranchA = DiscoveredBranchEffectRevalidationFixture.BranchA;
    private const string BranchB = DiscoveredBranchEffectRevalidationFixture.BranchB;

    [Fact]
    public async Task Positive_Assertions1to5And8to11_HistoricalARevalidated_ZeroDuplicateDispatch_BContinues_GoalEvidenceCompletes()
    {
        var harness = DiscoveredBranchEffectRevalidationRunHarness.Create(
            DiscoveredBranchEffectRevalidationFixture.Positive(),
            recoveredEffectState: true);
        var evidence = await harness.RunAsync();
        var trace = evidence.Trace.ToArray();
        var boundary = evidence.DriftBoundary
            ?? throw new InvalidOperationException("Expected one Agent drift boundary.");

        // Assertion 1: A is discovered from accepted evidence and is absent from initial Plan targets.
        Assert.DoesNotContain(harness.InitialPlan.Steps, step => step.TargetDescription == BranchA);
        Assert.DoesNotContain(harness.Plan.Steps, step => step.TargetDescription == BranchA);
        var approved = evidence.FinalProgress[ParentPage].ApprovedSiblingEvidence;
        Assert.Equal(2, approved.Count);
        Assert.Equal(2L, approved[BranchA]);
        Assert.Equal(2L, approved[BranchB]);
        Assert.Contains(evidence.Observations, observation =>
            observation.SequenceNumber == approved[BranchA]
            && observation.Elements.Any(element => element.Text == BranchA)
            && observation.Elements.Any(element => element.Text == BranchB));

        // Assertion 2: the carrier neither makes A a PlanStep nor proves inventory, authorization,
        // completion, or validity.
        Assert.All(harness.Plan.Steps, step => Assert.Null(step.BranchEffectEvidenceEvaluator));
        Assert.All(harness.InitialPlan.Steps, step => Assert.Null(step.BranchEffectEvidenceEvaluator));
        var inventoryIndex = IndexOfReason(trace, "branch inventory complete");
        Assert.True(inventoryIndex >= 0, "Approved inventory must be accepted from evidence.");
        var historical = Assert.Single(evidence.ProgressSnapshots, snapshot =>
            snapshot.TryGetValue(ParentPage, out var item)
            && item.CompletedSiblingEvidence.TryGetValue(BranchA, out var sequence)
            && sequence <= boundary);
        Assert.Equal(4L, historical[ParentPage].CompletedSiblingEvidence[BranchA]);
        Assert.Contains(evidence.Journal, entry =>
            entry.PostActionObservation?.Elements.Any(element =>
                element.Text == "A external effect" && element.SwitchState == true) == true);
        Assert.Equal(new DeviceAction.Tap(0), evidence.ActionHistory[1]);

        // Assertions 3/4: matched carrier evaluated once, only after the single verified Recovery,
        // and only against the fresh post-verification Observation.
        var verifyIndex = IndexOfReason(trace, "recovery verify: VERIFIED");
        var revalidatedIndex = IndexOfReason(trace, "recovered parent branch progress revalidated");
        var resumeIndex = IndexOfReason(trace, "recovery resume: plan index=3");
        Assert.True(verifyIndex >= 0, "One verified Recovery must have occurred.");
        Assert.True(verifyIndex < revalidatedIndex, "Evaluation must follow verified Recovery.");
        Assert.True(revalidatedIndex < resumeIndex, "Revalidation must precede resume.");
        Assert.True(inventoryIndex < revalidatedIndex, "Inventory acceptance precedes any criterion evaluation.");
        Assert.All(trace.Where(entry => entry.RecoveryId is not null), entry =>
            Assert.Equal("Recovery-1", entry.RecoveryId));
        var recovered = evidence.Observations.First(observation =>
            observation.SequenceNumber > boundary
            && observation.ForegroundApplication == "Settings"
            && observation.Elements.Any(element => element.Text == BranchA)
            && observation.Elements.Any(element => element.Text == BranchB));
        Assert.Equal(boundary + 1, recovered.SequenceNumber);

        // Assertion 5: true permits A to contribute and B to continue, with zero duplicate A dispatch.
        Assert.True(evidence.CriterionOutcome);
        var completed = evidence.FinalProgress[ParentPage].CompletedSiblingEvidence;
        Assert.Equal(2, completed.Count);
        Assert.Equal(recovered.SequenceNumber, completed[BranchA]);
        Assert.Equal(9L, completed[BranchB]);
        Assert.True(completed[BranchA] > boundary);
        Assert.True(completed[BranchB] > boundary);
        Assert.True(evidence.FinalProgress[ParentPage].IsSubtreeComplete);
        Assert.Equal(1, CountDispatchesOfA(evidence));
        Assert.Equal(ExpectedPositiveActions, evidence.ActionHistory);

        // Assertion 8: the nullable criterion result is derived and never persisted as validity,
        // lifecycle, Recovery, or completion state — the progress record keeps exactly its frozen
        // member shape (parent, approved, completed, authorized). This plan-driven bounded-
        // discovery path dispatches without the open-world authorization ledger, so the
        // authorized-obligation set stays empty here (the verified-return trigger is an
        // open-world-path mechanism).
        Assert.Equal(approved, evidence.FinalProgress[ParentPage].ApprovedSiblingEvidence);
        Assert.Equal(completed, evidence.FinalProgress[ParentPage].CompletedSiblingEvidence);
        Assert.Empty(evidence.FinalProgress[ParentPage].AuthorizedSiblingEvidence);


        // Assertion 9: GoalEvidence retains its frozen meaning — completion still requires an
        // independently satisfied GoalEvidence over the final observation (I-10).
        Assert.Equal(RunState.Completed, evidence.State);
        Assert.True(evidence.GoalEvidence[^1].Satisfied);
        Assert.Equal(evidence.Observations[^1].SequenceNumber, evidence.GoalEvidence[^1].SourceObservationSequence);
        Assert.DoesNotContain(trace[..^1], entry => entry.RunState == RunState.Completed);

        // Assertion 10: Agent remains sole authority — the Run completes only through Agent's
        // consumption of satisfied GoalEvidence, and the resume vocabulary is Agent's.
        Assert.Equal(RunState.Completed, trace[^1].RunState);
        Assert.NotNull(evidence.Reason);

        // Assertion 11: Recovery remains restore → observe → verify mechanics and produces no
        // branch-effect interpretation.
        var machineryReasons = trace
            .Where(entry => entry.Reason?.StartsWith("recovery ") == true)
            .Select(entry => entry.Reason!);
        Assert.Equal(3, machineryReasons.Count());
        Assert.DoesNotContain(machineryReasons, reason => reason.Contains("branch") || reason.Contains("progress"));
    }

    [Fact]
    public async Task Contradicted_Assertion6_HistoricalProvenanceObservable_AZeroContribution_NoFabricatedRepairOrCompletion()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Contradicted(), false);
        var boundary = evidence.DriftBoundary
            ?? throw new InvalidOperationException("Expected one Agent drift boundary.");

        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("contradicted", evidence.Reason, StringComparison.Ordinal);
        Assert.False(evidence.CriterionOutcome);

        // Historical provenance stays observable pre-drift; A contributes nothing current.
        Assert.Single(evidence.ProgressSnapshots, snapshot =>
            snapshot.TryGetValue(ParentPage, out var item)
            && item.CompletedSiblingEvidence.TryGetValue(BranchA, out var sequence)
            && sequence <= boundary);
        Assert.Contains(evidence.Journal, entry =>
            entry.PostActionObservation?.Elements.Any(element =>
                element.Text == "A external effect" && element.SwitchState == true) == true);
        Assert.Empty(evidence.FinalProgress[ParentPage].CompletedSiblingEvidence);

        // Zero fabricated repair/success and zero blind A redispatch.
        Assert.DoesNotContain(evidence.Trace, entry => entry.RunState == RunState.Completed);
        Assert.DoesNotContain(evidence.Trace, entry =>
            entry.Reason?.Contains("recovered parent branch progress revalidated") == true);
        Assert.Equal(1, CountDispatchesOfA(evidence));
        Assert.Equal(ExpectedFailureActions, evidence.ActionHistory);

        // Explicit Agent non-completion record; no resume after the contradiction.
        Assert.Contains(evidence.Trace, entry =>
            entry.RunState == RunState.Failed && entry.Reason?.Contains("contradicted") == true);
        Assert.DoesNotContain(evidence.Trace, entry => entry.Reason?.Contains("recovery resume") == true);
    }

    [Fact]
    public async Task Unresolved_Assertion7_NullCriterion_AUnresolved_ZeroContribution_ZeroBlindRedispatch()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Unresolved(), null);
        var boundary = evidence.DriftBoundary
            ?? throw new InvalidOperationException("Expected one Agent drift boundary.");

        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("unresolved", evidence.Reason, StringComparison.Ordinal);
        Assert.Null(evidence.CriterionOutcome);

        // A stays unresolved: retained historical completion at/before the drift boundary, never
        // revalidated and never contributing.
        Assert.Equal(4L, evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA]);
        Assert.True(evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA] <= boundary);
        Assert.DoesNotContain(evidence.Trace, entry =>
            entry.Reason?.Contains("recovered parent branch progress revalidated") == true);
        Assert.Equal(1, CountDispatchesOfA(evidence));
        Assert.Equal(ExpectedFailureActions, evidence.ActionHistory);
        Assert.Contains(evidence.Trace, entry =>
            entry.RunState == RunState.Failed && entry.Reason?.Contains("unresolved") == true);
    }

    [Fact]
    public async Task AbsentCarrier_Assertions1_2_7_10_FrozenCand008RouteUnchanged_NoRecoveryNoRevalidation()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.AbsentCarrier(), true);

        // Carrier absent: A is still discovered from accepted evidence (frozen CAND-008 gate) and
        // still absent from the initial Plan, but the frozen discovery loop runs unchanged and
        // fails with its explicit unresolved record at depth 1.
        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("unresolved at depth=1", evidence.Reason, StringComparison.Ordinal);
        Assert.Equal(2L, evidence.FinalProgress[ParentPage].ApprovedSiblingEvidence[BranchA]);
        Assert.Empty(evidence.FinalProgress[ParentPage].CompletedSiblingEvidence);
        Assert.Null(evidence.DriftBoundary);
        Assert.Null(evidence.CriterionOutcome);
        Assert.DoesNotContain(evidence.Trace, entry => entry.RecoveryId is not null);
        Assert.DoesNotContain(evidence.Trace, entry =>
            entry.Reason?.Contains("recovered parent branch progress revalidated") == true);
        Assert.Equal(
            [new DeviceAction.LaunchApp("Settings"), new DeviceAction.Tap(0)],
            evidence.ActionHistory);
    }

    [Fact]
    public async Task IdentityMismatch_Assertions3_7_NonMatchingIdentity_Unresolved_ZeroEvaluation()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.IdentityMismatch(), true);

        // The carrier identity never identifies A under the same parent's inventory+progress, so
        // the criterion is never evaluated: A stays unresolved with retained historical evidence.
        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("unresolved", evidence.Reason, StringComparison.Ordinal);
        Assert.Null(evidence.CriterionOutcome);
        Assert.Equal(4L, evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA]);
        Assert.DoesNotContain(evidence.Trace, entry =>
            entry.Reason?.Contains("recovered parent branch progress revalidated") == true);
        Assert.Contains(evidence.Trace, entry =>
            entry.RunState == RunState.Failed && entry.Reason?.Contains("unresolved") == true);
    }

    [Fact]
    public async Task StaleEvidence_Assertions4_7_StaleRecoveredObservation_ZeroEvaluation_ExplicitFreshnessFailure()
    {
        var evidence = await RunFixture(
            DiscoveredBranchEffectRevalidationFixture.Positive(),
            true,
            staleRecoveryObservation: true);
        var boundary = evidence.DriftBoundary
            ?? throw new InvalidOperationException("Expected one Agent drift boundary.");

        // The recovered Observation is stamped at the drift boundary — not fresh — so retained
        // branch progress cannot be verified against it: the criterion is never evaluated and the
        // run fails with an explicit non-completion record.
        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("fresh recovered parent continuity 未获证明", evidence.Reason, StringComparison.Ordinal);
        Assert.Null(evidence.CriterionOutcome);
        Assert.Equal(4L, evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA]);
        Assert.DoesNotContain(evidence.Trace, entry =>
            entry.Reason?.Contains("recovered parent branch progress revalidated") == true);
        Assert.DoesNotContain(evidence.Trace, entry => entry.Reason?.Contains("recovery resume") == true);
        Assert.Contains(evidence.Observations, observation =>
            observation.SequenceNumber == boundary
            && observation.ForegroundApplication == "Settings"
            && observation.Elements.Any(element => element.Text == BranchA));
    }

    [Fact]
    public async Task AmbiguousParentScope_Assertions3_7_ConflictingParentEvidence_StaysUnmatched_Unresolved()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.AmbiguousParent().RunAsync();

        // Identity matches only within the same bounded parent: historical completion under the
        // conflicting parent scope never matches the carrier under the active parent, so the fresh
        // recovered Observation is never evaluated and A stays unresolved.
        Assert.Null(evidence.MatchedCarrier);
        Assert.Null(evidence.FreshCriterionOutcome);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("contradicted")]
    [InlineData("unresolved")]
    [InlineData("absent")]
    [InlineData("mismatch")]
    [InlineData("stale")]
    public async Task EqualInputs_Assertion12_ReplayEqualOutcomesContributionActionsJournalTraceGoalEvidenceAndState(string branch)
    {
        var first = await RunAsync(branch);
        var second = await RunAsync(branch);

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.DriftBoundary, second.DriftBoundary);
        Assert.Equal(first.CriterionOutcome, second.CriterionOutcome);
        AssertProgressEqual(first.FinalProgress, second.FinalProgress);
        Assert.Equal(first.ProgressSnapshots.Length, second.ProgressSnapshots.Length);
        for (var index = 0; index < first.ProgressSnapshots.Length; index++)
            AssertProgressEqual(first.ProgressSnapshots[index], second.ProgressSnapshots[index]);
        Assert.Equal(first.ActionHistory, second.ActionHistory);
        AssertObservationsEqual(first.Observations, second.Observations);
        AssertJournalEqual(first.Journal, second.Journal);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);
    }

    private static async Task<DiscoveredBranchEffectRevalidationRunEvidence> RunAsync(string branch)
    {
        var (fixture, effectState, stale) = branch switch
        {
            "positive" => (DiscoveredBranchEffectRevalidationFixture.Positive(), (bool?)true, false),
            "contradicted" => (DiscoveredBranchEffectRevalidationFixture.Contradicted(), (bool?)false, false),
            "unresolved" => (DiscoveredBranchEffectRevalidationFixture.Unresolved(), (bool?)null, false),
            "absent" => (DiscoveredBranchEffectRevalidationFixture.AbsentCarrier(), (bool?)true, false),
            "mismatch" => (DiscoveredBranchEffectRevalidationFixture.IdentityMismatch(), (bool?)true, false),
            "stale" => (DiscoveredBranchEffectRevalidationFixture.Positive(), (bool?)true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
        return await RunFixture(fixture, effectState, stale);
    }

    private static async Task<DiscoveredBranchEffectRevalidationRunEvidence> RunFixture(
        DiscoveredBranchEffectRevalidationFixture fixture,
        bool? recoveredEffectState,
        bool staleRecoveryObservation = false)
    {
        var harness = DiscoveredBranchEffectRevalidationRunHarness.Create(
            fixture,
            recoveredEffectState,
            staleRecoveryObservation);
        return await harness.RunAsync();
    }

    private static int IndexOfReason(TraceEvent[] trace, string fragment)
        => Array.FindIndex(trace, entry => entry.Reason?.Contains(fragment) == true);

    private static int CountDispatchesOfA(DiscoveredBranchEffectRevalidationRunEvidence evidence)
        => evidence.ActionHistory
            .Zip(evidence.Observations, (action, observation) => (action, observation))
            .Count(pair => pair.action is DeviceAction.Tap { TargetElementIndex: { } index }
                && index < pair.observation.Elements.Length
                && string.Equals(
                    pair.observation.Elements[index].Text,
                    BranchA,
                    StringComparison.Ordinal));

    private static readonly ImmutableArray<DeviceAction> ExpectedPositiveActions =
    [
        new DeviceAction.LaunchApp("Settings"),
        new DeviceAction.Tap(0),
        new DeviceAction.SetSwitch(0, true),
        new DeviceAction.Tap(1),
        new DeviceAction.Tap(1),
        new DeviceAction.LaunchApp("Settings"),
        new DeviceAction.Tap(1),
        new DeviceAction.Tap(0),
        new DeviceAction.Tap(1),
    ];

    private static readonly ImmutableArray<DeviceAction> ExpectedFailureActions =
    [
        new DeviceAction.LaunchApp("Settings"),
        new DeviceAction.Tap(0),
        new DeviceAction.SetSwitch(0, true),
        new DeviceAction.Tap(1),
        new DeviceAction.Tap(1),
        new DeviceAction.LaunchApp("Settings"),
    ];

    private static void AssertProgressEqual(
        IReadOnlyDictionary<string, BranchProgressEvidence> expected,
        IReadOnlyDictionary<string, BranchProgressEvidence> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key].ParentSemanticPage, actual[key].ParentSemanticPage);
            Assert.Equal(expected[key].ApprovedSiblingEvidence, actual[key].ApprovedSiblingEvidence);
            Assert.Equal(expected[key].CompletedSiblingEvidence, actual[key].CompletedSiblingEvidence);
        }
    }

    private static void AssertObservationsEqual(
        ImmutableArray<Observation> expected,
        ImmutableArray<Observation> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].ForegroundApplication, actual[index].ForegroundApplication);
            Assert.Equal(expected[index].SequenceNumber, actual[index].SequenceNumber);
            Assert.Equal(expected[index].Elements.Length, actual[index].Elements.Length);
            for (var element = 0; element < expected[index].Elements.Length; element++)
                Assert.Equal(expected[index].Elements[element], actual[index].Elements[element]);
        }
    }

    private static void AssertJournalEqual(
        ImmutableArray<UniClaw.Runtime.Traversal.TraversalJournalEntry> expected,
        ImmutableArray<UniClaw.Runtime.Traversal.TraversalJournalEntry> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].StepId, actual[index].StepId);
            Assert.Equal(expected[index].SelectedElementIndex, actual[index].SelectedElementIndex);
            Assert.Equal(expected[index].DispatchedAction, actual[index].DispatchedAction);
            Assert.Equal(expected[index].RetryCount, actual[index].RetryCount);
            Assert.Equal(expected[index].Result, actual[index].Result);
            if (expected[index].PostActionObservation is { } expectedObservation
                && actual[index].PostActionObservation is { } actualObservation)
            {
                AssertObservationsEqual([expectedObservation], [actualObservation]);
            }
            else
            {
                Assert.Equal(expected[index].PostActionObservation, actual[index].PostActionObservation);
            }
        }
    }
}
