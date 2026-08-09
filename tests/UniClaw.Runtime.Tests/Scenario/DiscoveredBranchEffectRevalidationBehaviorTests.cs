using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-P3-CAND-009 Task 2.1: Agent bounded post-Recovery effect revalidation behavior. A's historical
/// completion under P, one external drift, one verified Recovery, and one fresh recovered-world
/// Observation produce the three-way revalidation outcome; true contributes without redispatch and B
/// continues, false/null/absent/mismatch contribute zero with explicit non-completion records, and
/// the frozen carrier-absent CAND-008 route stays unchanged. The Agent remains the sole
/// retain/invalidate/unresolved, resume, progress, GoalEvidence, and RunState authority.
/// </summary>
public sealed class DiscoveredBranchEffectRevalidationBehaviorTests
{
    private const string ParentPage = DiscoveredBranchEffectRevalidationFixture.ActiveParentSemanticPage;
    private const string BranchA = DiscoveredBranchEffectRevalidationFixture.BranchA;
    private const string BranchB = DiscoveredBranchEffectRevalidationFixture.BranchB;

    /// <summary>Deterministic expected action history of the positive run:
    /// startup LaunchApp; transient A Tap; A effect SetSwitch; A return Tap; B Tap (drift step);
    /// Recovery LaunchApp; resume B Tap; B work Tap; B return Tap. A is dispatched exactly once.</summary>
    private static readonly ImmutableArray<DeviceAction> PositiveActionHistory =
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

    [Fact]
    public async Task Positive_FreshPostRecoveryObservation_RevalidatesA_ZeroDuplicateDispatch_BContinues()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Positive(), true);

        // Run completes only through independently satisfied GoalEvidence (I-10).
        Assert.Equal(RunState.Completed, evidence.State);
        Assert.Contains("revalidated", evidence.Reason);
        Assert.Equal(6L, evidence.DriftBoundary);
        Assert.True(evidence.CriterionOutcome);

        // A's historical completion is revalidated against the fresh recovered Observation and
        // contributes to the current subtree; B completes through the resumed plan.
        var completed = evidence.FinalProgress[ParentPage].CompletedSiblingEvidence;
        Assert.Equal(2, completed.Count);
        Assert.Equal(7L, completed[BranchA]);
        Assert.Equal(9L, completed[BranchB]);
        Assert.All(completed.Values, sequence => Assert.True(sequence > evidence.DriftBoundary!.Value));

        // Historical provenance stays observable in the pre-drift snapshot.
        Assert.Equal(4L, evidence.ProgressSnapshots[2][ParentPage].CompletedSiblingEvidence[BranchA]);

        // Zero duplicate dispatch: A's Tap (index 0 on a screen whose element 0 is Branch A)
        // appears exactly once in the whole run.
        Assert.Equal(1, CountDispatchesOfA(evidence));
        Assert.Equal(PositiveActionHistory, evidence.ActionHistory);

        Assert.Contains(evidence.Trace, trace => trace.Reason?.Contains("branch progress revalidated") == true);
        Assert.Contains(evidence.Trace, trace => trace.Reason?.Contains("branch inventory complete") == true);
        Assert.True(evidence.GoalEvidence[^1].Satisfied);
    }

    [Fact]
    public async Task Contradicted_FreshPostRecoveryObservation_ZeroContribution_ProvenanceObservable()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Contradicted(), false);

        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("contradicted", evidence.Reason);
        Assert.False(evidence.CriterionOutcome);
        Assert.Equal(6L, evidence.DriftBoundary);

        // A contributes zero to the current subtree/Goal evaluation.
        Assert.Empty(evidence.FinalProgress[ParentPage].CompletedSiblingEvidence);

        // Historical provenance stays observable in the pre-drift snapshot (zero fabricated success).
        Assert.Equal(4L, evidence.ProgressSnapshots[2][ParentPage].CompletedSiblingEvidence[BranchA]);

        // Zero fabricated repair/success/redispatch: no Completed, no revalidation, one A Tap.
        Assert.DoesNotContain(evidence.Trace, trace => trace.RunState == RunState.Completed);
        Assert.DoesNotContain(evidence.Trace, trace => trace.Reason?.Contains("branch progress revalidated") == true);
        Assert.Equal(1, CountDispatchesOfA(evidence));
        Assert.Contains(evidence.Trace, trace => trace.RunState == RunState.Failed);
    }

    [Fact]
    public async Task Unresolved_RetainedHistory_ExplicitFailure_ZeroContribution()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Unresolved(), null);

        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("unresolved", evidence.Reason);
        Assert.Null(evidence.CriterionOutcome);

        // Retained historical completion stays at/before the drift boundary — never revalidated,
        // never fabricated into the current evaluation.
        var retained = evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA];
        Assert.Equal(4L, retained);
        Assert.True(retained <= evidence.DriftBoundary!.Value);

        // Explicit non-completion record and zero blind redispatch.
        Assert.Contains(evidence.Trace, trace => trace.RunState == RunState.Failed && trace.Reason?.Contains("unresolved") == true);
        Assert.DoesNotContain(evidence.Trace, trace => trace.Reason?.Contains("branch progress revalidated") == true);
        Assert.Equal(1, CountDispatchesOfA(evidence));
    }

    [Fact]
    public async Task AbsentCarrier_FrozenCand008BehaviorUnchanged()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.AbsentCarrier(), true);

        // Carrier absent → the frozen CAND-008 route runs unchanged: depth-0 inventory accepted,
        // one authorized A dispatch, explicit unresolved failure at depth 1. No Recovery, no
        // revalidation, no carrier evaluation.
        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("unresolved at depth=1", evidence.Reason);
        Assert.Null(evidence.DriftBoundary);
        Assert.Null(evidence.CriterionOutcome);
        Assert.Equal(
            [new DeviceAction.LaunchApp("Settings"), new DeviceAction.Tap(0)],
            evidence.ActionHistory);
        Assert.DoesNotContain(evidence.Trace, trace => trace.Reason?.Contains("branch progress revalidated") == true);
    }

    [Fact]
    public async Task IdentityMismatch_Unresolved_ZeroEvaluation()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.IdentityMismatch(), true);

        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("unresolved", evidence.Reason);

        // The carrier identity never matches the retained completed branch under P, so the carrier
        // is never evaluated and A stays unresolved with its retained historical evidence only.
        Assert.Null(evidence.CriterionOutcome);
        Assert.Equal(4L, evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA]);
        Assert.DoesNotContain(evidence.Trace, trace => trace.Reason?.Contains("branch progress revalidated") == true);
    }

    [Fact]
    public async Task AmbiguousParentScope_EvidenceBoundary_StaysUnmatched()
    {
        var evidence = await DiscoveredBranchEffectRevalidationFixture.AmbiguousParent().RunAsync();

        // The exact-match boundary is parent-scoped: historical completion under the conflicting
        // parent scope never matches the carrier under the active parent — ambiguous stays
        // unresolved and the fresh recovered Observation is never evaluated.
        Assert.Null(evidence.MatchedCarrier);
        Assert.Null(evidence.FreshCriterionOutcome);
    }

    [Fact]
    public async Task StaleRecoveryEvidence_FreshnessGate_ZeroEvaluation()
    {
        var evidence = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Positive(), true, staleRecoveryObservation: true);

        // The recovered Observation (stamped at the drift boundary) is not fresh: the retained
        // branch progress cannot be verified against it, the carrier is never evaluated, and the
        // run fails with an explicit non-completion record.
        Assert.Equal(RunState.Failed, evidence.State);
        Assert.Contains("fresh recovered parent continuity 未获证明", evidence.Reason);
        Assert.Null(evidence.CriterionOutcome);
        Assert.Equal(4L, evidence.FinalProgress[ParentPage].CompletedSiblingEvidence[BranchA]);
        Assert.DoesNotContain(evidence.Trace, trace => trace.Reason?.Contains("branch progress revalidated") == true);
    }

    [Fact]
    public async Task Carrier_DoesNotAddAPlanStep_AndDoesNotProveMembership()
    {
        var fixture = DiscoveredBranchEffectRevalidationFixture.Positive();
        var harness = DiscoveredBranchEffectRevalidationRunHarness.Create(fixture, true);
        var evidence = await harness.RunAsync();

        // The carrier never makes A a PlanStep: A's target is absent from both the immutable
        // initial Plan and the executed Plan, and no plan step carries an effect criterion.
        Assert.DoesNotContain(harness.Plan.Steps, step => step.TargetDescription == BranchA);
        Assert.DoesNotContain(fixture.InitialPlan.Steps, step => step.TargetDescription == BranchA);
        Assert.All(harness.Plan.Steps, step => Assert.Null(step.BranchEffectEvidenceEvaluator));
        Assert.All(fixture.InitialPlan.Steps, step => Assert.Null(step.BranchEffectEvidenceEvaluator));

        // Approved membership comes from the frozen CAND-008 acceptance gate on bounded accepted
        // evidence, not from the carrier.
        Assert.Contains(evidence.Trace, trace => trace.Reason?.Contains("branch inventory complete") == true);
        Assert.Equal(RunState.Completed, evidence.State);
    }

    [Fact]
    public async Task EqualInputs_ReplayEqual_ForPositiveAndUnresolved()
    {
        var positive1 = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Positive(), true);
        var positive2 = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Positive(), true);
        AssertReplayEqual(positive1, positive2);

        var unresolved1 = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Unresolved(), null);
        var unresolved2 = await RunFixture(DiscoveredBranchEffectRevalidationFixture.Unresolved(), null);
        AssertReplayEqual(unresolved1, unresolved2);
    }

    /// <summary>
    /// Deterministic replay equality over value projections (ImmutableArray/ImmutableDictionary
    /// record members are reference-compared; SequenceEqual-based element comparison is used).
    /// </summary>
    private static void AssertReplayEqual(
        DiscoveredBranchEffectRevalidationRunEvidence first,
        DiscoveredBranchEffectRevalidationRunEvidence second)
    {
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.DriftBoundary, second.DriftBoundary);
        Assert.Equal(first.CriterionOutcome, second.CriterionOutcome);

        // Contribution equality: identical parent scopes and identical approved/completed evidence.
        Assert.Equal(
            first.FinalProgress.Keys.OrderBy(key => key, StringComparer.Ordinal),
            second.FinalProgress.Keys.OrderBy(key => key, StringComparer.Ordinal));
        foreach (var key in first.FinalProgress.Keys)
        {
            Assert.Equal(
                first.FinalProgress[key].ApprovedSiblingEvidence.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
                second.FinalProgress[key].ApprovedSiblingEvidence.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
            Assert.Equal(
                first.FinalProgress[key].CompletedSiblingEvidence.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
                second.FinalProgress[key].CompletedSiblingEvidence.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
        }

        // Actions equality.
        Assert.Equal(first.ActionHistory.ToArray(), second.ActionHistory.ToArray());

        // Journal equality: step identity, dispatched action, and post-action evidence sequence.
        Assert.Equal(
            first.Journal.Select(entry => (entry.StepId, entry.DispatchedAction, entry.PostActionObservation?.SequenceNumber)),
            second.Journal.Select(entry => (entry.StepId, entry.DispatchedAction, entry.PostActionObservation?.SequenceNumber)));

        // Trace equality: the full causal-chain fields.
        Assert.Equal(
            first.Trace.Select(trace => (trace.RunState, trace.Reason, trace.ContainerId, trace.StepId,
                trace.ActionId, trace.Action, trace.RecoveryId, trace.TrapKind, trace.TrapScope)),
            second.Trace.Select(trace => (trace.RunState, trace.Reason, trace.ContainerId, trace.StepId,
                trace.ActionId, trace.Action, trace.RecoveryId, trace.TrapKind, trace.TrapScope)));

        // GoalEvidence equality.
        Assert.Equal(
            first.GoalEvidence.Select(evidence => (evidence.Satisfied, evidence.Reason, evidence.SourceObservationSequence)),
            second.GoalEvidence.Select(evidence => (evidence.Satisfied, evidence.Reason, evidence.SourceObservationSequence)));
    }

    /// <summary>
    /// Count dispatches of the discovered branch A: a Tap whose index references a screen element
    /// named Branch A at dispatch time (action k executes against the observation produced at the
    /// same index in the observation history).
    /// </summary>
    private static int CountDispatchesOfA(DiscoveredBranchEffectRevalidationRunEvidence evidence)
        => evidence.ActionHistory
            .Zip(evidence.Observations, (action, observation) => (action, observation))
            .Count(pair => pair.action is DeviceAction.Tap { TargetElementIndex: { } index }
                && index < pair.observation.Elements.Length
                && string.Equals(
                    pair.observation.Elements[index].Text,
                    BranchA,
                    StringComparison.Ordinal));

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
}
