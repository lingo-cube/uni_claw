using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-S0-CAPSTONE-001 Task 2.1 integration run evidence: one complete end-to-end run composing the
/// frozen production Agent behavior (read-only reuse) over the Task 1.1 deterministic S0 world.
///
/// The run sequence is: traversal intent + allowed scope + depth bound 4 + safety constraints →
/// branch discovery from fresh initial evidence (CAND-008 inventory acceptance + CAND-006 candidate
/// transient step, CAND-009 carrier wired but never fired) → branch progress (CAND-004 surfaces) →
/// dangerous candidate zero-dispatch (CAND-006) → exactly one Popup handled with VERIFIED Container
/// continuity (SC-P3-002) → exactly one external Launcher drift with re-enter/restore/Observe/
/// Verify/reconcile/resume (SC-P2-001 + CAND-005/009) → one bounded viewport movement (SC-P3-003 +
/// CAND-007) → completion only on the satisfied GoalEvidence conjunction (I-10).
///
/// Each fact asserts the Scenario's required assertions 1-9 and completion evidence 1-7: the route
/// is not pre-encoded (assertion 1); every approved reachable safe branch within depth &lt;= 4 is
/// traversed and none unresolved (assertions 2/4); zero dangerous dispatch proven by final state
/// (assertion 3); Popup followed by fresh verified Container continuity (assertion 5, evidence 4);
/// drift triggers the frozen recovery path and re-entry is not new progress (assertion 6); retained
/// progress is neither fabricated nor silently discarded after Recovery (assertion 7); already
/// verified work is not double-counted (assertion 8); Plan exhaustion / action dispatch / Recovery
/// dispatch / viewport snapshot change / local Container completion alone never complete the Run
/// (assertion 9, evidence 1-7); equal-input replay yields equal progress / ActionHistory /
/// Observations / journal / Trace / GoalEvidence / final RunState (evidence 7).
///
/// The bounded viewport movement (CAND-007) composes in the resumed segment: the frozen
/// Traversal/Container step protocol executes the scroll, the frozen Agent reconciles fresh identity
/// evidence within the same semantic Container, and the wired evaluator is recomputed test-side from
/// the accepted same-Container evidence — honest exhaustion framing (initial evidence justifies one
/// bounded movement; fresh viewport evidence positively proves the bounded content fully visible).
/// </summary>
public sealed class CapstoneSettingsIntegrationRunTests
{
    private const string SettingsRoot = CapstoneSettingsWorldFixture.SettingsRootScreen;
    private const string WifiPrefs = CapstoneSettingsWorldFixture.WifiPrefsScreen;
    private const string ResetOptions = CapstoneSettingsWorldFixture.ResetOptionsScreen;
    private const string TargetApplication = CapstoneSettingsWorldFixture.TargetApplication;

    [Fact]
    public async Task Run_CompletesOnGoalEvidenceConjunctionAtFinalObservation()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var evidence = await harness.RunAsync();

        // Completion evidence 1/2/3/5/6 + assertion 9: exactly one satisfied GoalEvidence, only at
        // the final post-action observation; the conjunction covers all pages, the Popup, the drift,
        // the viewport, zero dangerous dispatch, and retained progress.
        Assert.Equal(RunState.Completed, evidence.State);
        Assert.NotNull(evidence.Reason);
        var satisfied = evidence.GoalEvidence.Where(item => item.Satisfied).ToArray();
        Assert.Single(satisfied);
        Assert.Equal(36L, satisfied[0].SourceObservationSequence);
        Assert.Equal(evidence.Observations[^1].SequenceNumber, satisfied[0].SourceObservationSequence);
        Assert.Equal(evidence.GoalEvidence[^1], satisfied[0]);
        Assert.Equal(evidence.GoalEvidence[^1].Reason, evidence.Reason);
        Assert.DoesNotContain(evidence.Trace[..^1], entry => entry.RunState == RunState.Completed);
        Assert.Equal(RunState.Completed, evidence.Trace[^1].RunState);

        // Deterministic shape: 36 Observations, 35 dispatches, 33 journal entries, and 32 Goal
        // evidence evaluations. Recovery resumes the root-bound Display step without restore taps.
        Assert.Equal(36, evidence.Observations.Length);
        Assert.Equal(35, evidence.ActionHistory.Length);
        Assert.Equal(33, evidence.Journal.Length);
        Assert.Equal(32, evidence.GoalEvidence.Length); // CP-06：seq2 初始评估在前

        // Traversal intent + allowed scope + depth bound 4 + safety constraints are consumed as
        // fixture inputs and the run executes over exactly that world (completion evidence inputs).
        var fixture = harness.Fixture;
        Assert.Equal(CapstoneSettingsWorldFixture.DefaultTraversalIntent, fixture.TraversalIntent);
        Assert.Equal(CapstoneSettingsWorldFixture.DefaultAllowedScope, fixture.AllowedScope);
        Assert.Equal(CapstoneSettingsWorldFixture.DefaultDepthBound, fixture.DepthBound);
        Assert.Contains(fixture.SafetyConstraints, constraint =>
            string.Equals(
                constraint,
                CapstoneSettingsWorldFixture.DefaultSafetyConstraint,
                StringComparison.Ordinal));
        Assert.Equal(CapstoneSettingsRunHarness.Schedule, fixture.Schedule);
    }

    [Fact]
    public async Task Route_IsNotPreEncoded_BranchDiscoveredFromFreshInitialEvidence()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var evidence = await harness.RunAsync();

        // Assertion 1: the route is not pre-encoded. The world pre-enumerates no route at all, and
        // the first plan step ("Wi-Fi") cannot ground on the fresh initial Observation (SettingsRoot
        // shows no Wi-Fi element) — the Network subtree is first entered through the frozen CAND-006
        // transient Tap step grounded on that fresh evidence.
        Assert.Empty(harness.InitialPlan.Steps);
        Assert.Equal(
            CapstoneSettingsWorldFixture.WifiText,
            harness.Plan.Steps[0].TargetDescription);

        // Network & Internet is never pre-encoded: the sole dispatch is the initial transient step
        // grounded from fresh root evidence.
        var steps = harness.Plan.Steps.ToArray();
        var firstNetworkInternetIndex = Array.FindIndex(
            steps,
            step => string.Equals(step.TargetDescription, CapstoneSettingsWorldFixture.NetworkInternetText, StringComparison.Ordinal));
        Assert.Equal(-1, firstNetworkInternetIndex);
        Assert.Single(
            evidence.ActionHistory.Select((action, index) => (action, index)).Where(item =>
                item.action == new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f))
                && CapstoneSettingsWorldFixture.ResolveSemanticPage(evidence.Observations[item.index]) == SettingsRoot));

        // The first dispatched action is the transient Tap(0) grounded on the fresh initial evidence
        // (seq 2, SettingsRoot): the route is discovered from evidence, not from the Plan.
        var initial = evidence.Observations[1];
        Assert.Equal(2L, initial.SequenceNumber);
        Assert.Equal(SettingsRoot, CapstoneSettingsWorldFixture.ResolveSemanticPage(initial));
        Assert.DoesNotContain(initial.Elements, element =>
            string.Equals(element.Text, harness.Plan.Steps[0].TargetDescription, StringComparison.Ordinal));
        Assert.Equal(new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f)), evidence.ActionHistory[1]);

        // The frozen CAND-008 acceptance gate records the complete depth-0 inventory from the fresh
        // initial evidence, with source Observation seq 2 (assertion 1 trace evidence).
        Assert.Contains(evidence.Trace, entry =>
            entry.Reason?.Contains("branch inventory complete: depth=0", StringComparison.Ordinal) == true);
        Assert.Contains(evidence.Trace, entry =>
            entry.Reason?.Contains("source-seq=2", StringComparison.Ordinal) == true);
        Assert.Equal(3, evidence.FinalProgress[SettingsRoot].ApprovedSiblingEvidence.Count);
        Assert.All(
            evidence.FinalProgress[SettingsRoot].ApprovedSiblingEvidence,
            pair => Assert.Equal(2L, pair.Value));
    }

    [Fact]
    public async Task Traversal_ReachesEveryApprovedBranchWithinDepthBound_NoneUnresolved()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var fixture = harness.Fixture;
        var evidence = await harness.RunAsync();

        // Assertions 2/4: the visited page set equals the approved tree exactly — every approved
        // reachable safe branch is traversed and none is unresolved at completion.
        var visitedPages = evidence.Observations
            .Select(CapstoneSettingsWorldFixture.ResolveSemanticPage)
            .Where(page => page is not null)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            fixture.ApprovedTree.Select(page => page.Name).Order(StringComparer.Ordinal),
            visitedPages.Order(StringComparer.Ordinal));
        Assert.Equal(14, visitedPages.Count);

        // Every approved page lies within the depth bound input (depth 4 is the deepest page).
        Assert.All(fixture.ApprovedTree, page => Assert.InRange(page.Depth, 0, fixture.DepthBound));

        // The deepest approved page (Wi-Fi calling, depth 4) is actually reached with fresh evidence.
        Assert.Contains(evidence.Observations, observation =>
            CapstoneSettingsWorldFixture.ResolveSemanticPage(observation)
                == CapstoneSettingsWorldFixture.WifiCallingScreen);
    }

    [Fact]
    public async Task DangerousCandidate_ZeroDispatch_ProvenByFinalState()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var fixture = harness.Fixture;
        var evidence = await harness.RunAsync();

        // Assertion 3: the dangerous mutation candidate is visible exactly once (Reset options, seq
        // 30), and the only action dispatched while it is visible is the safe return tap (index 1).
        var dangerousObservations = evidence.Observations
            .Where(observation =>
                CapstoneSettingsWorldFixture.ResolveSemanticPage(observation) == ResetOptions)
            .ToArray();
        Assert.Single(dangerousObservations);
        Assert.Equal(30L, dangerousObservations[0].SequenceNumber);
        Assert.Contains(dangerousObservations[0].Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.DangerousCandidateText, StringComparison.Ordinal));

        for (var index = 1; index < evidence.ActionHistory.Length; index++)
        {
            if (CapstoneSettingsWorldFixture.ResolveSemanticPage(evidence.Observations[index]) != ResetOptions)
            {
                continue;
            }

            var tap = Assert.IsType<DeviceAction.Tap>(evidence.ActionHistory[index]);
            Assert.Equal(1, tap.TargetElementIndex);
        }

        // The CAND-006 authorization evidence positively rejects the candidate (visible candidate !=
        // approved executable action is world data, not a production conclusion).
        var dangerousElement = dangerousObservations[0].Elements.Single(element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.DangerousCandidateText, StringComparison.Ordinal));
        var authorization = CapstoneSettingsWorldFixture.EvaluateAuthorization(dangerousObservations[0], dangerousElement);
        Assert.False(authorization.Authorized);
        Assert.False(string.IsNullOrWhiteSpace(authorization.Reason));

        // The dangerous candidate is never an approved branch of the world's tree.
        Assert.DoesNotContain(
            CapstoneSettingsWorldFixture.DangerousCandidateText,
            fixture.ApprovedTree.SelectMany(page => page.RequiredBranches));
    }

    [Fact]
    public async Task Popup_HandledWithFreshVerifiedContainerContinuity()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var fixture = harness.Fixture;
        var evidence = await harness.RunAsync();

        // Completion evidence 4 + assertion 5: exactly one Popup at the deterministic schedule point,
        // with the underlying page identical before (seq 7) and after (seq 9) the obstruction.
        var popup = evidence.Observations.Single(observation =>
            observation.SequenceNumber == CapstoneSettingsRunHarness.PopupObservationSequence);
        Assert.Contains(popup.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.PopupOverlayText, StringComparison.Ordinal));
        Assert.Contains(popup.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.DismissText, StringComparison.Ordinal));
        var beforePopup = evidence.Observations.Single(observation =>
            observation.SequenceNumber == CapstoneSettingsRunHarness.PopupObservationSequence - 1);
        var afterDismiss = evidence.Observations.Single(observation =>
            observation.SequenceNumber == CapstoneSettingsRunHarness.PopupObservationSequence + 1);
        Assert.Equal(WifiPrefs, CapstoneSettingsWorldFixture.ResolveSemanticPage(beforePopup));
        Assert.Equal(WifiPrefs, CapstoneSettingsWorldFixture.ResolveSemanticPage(afterDismiss));
        Assert.Equal(TargetApplication, afterDismiss.ForegroundApplication);
        Assert.True(afterDismiss.SequenceNumber > popup.SequenceNumber);

        // The frozen Dismiss step succeeded on the fresh popup evidence (journal: post-obs seq 9,
        // element 1, Tap(1)).
        var dismiss = evidence.Journal.Single(entry =>
            entry.PostActionObservation?.SequenceNumber == afterDismiss.SequenceNumber);
        Assert.Equal(1, dismiss.SelectedElementIndex);
        Assert.Equal(new DeviceAction.Tap(1, new ElementBounds(0f, 0.1f, 1f, 0.2f)), dismiss.DispatchedAction);
        Assert.IsType<TraversalStepResult.Succeeded>(dismiss.Result);

        // Fresh verified Container continuity: the SAME Container accepted the fresh obstruction
        // evidence (seq 8), executed the popup step and the Dismiss, and advanced its accepted
        // current Observation to the fresh post-dismiss evidence (seq 9) — no Bind, so no local
        // progress reset and no Container-scope Trap anywhere in the run. The trailing "Return to
        // Wi-Fi" step is the frozen recording of the next step this same Container dispatched before
        // the next page transition rebound it (frozen Container semantics: ExecutedSteps appends
        // every dispatched step, verified or not).
        var verifiedContainer = Assert.Single(harness.Containers, container =>
            container.CurrentObservation?.SequenceNumber == afterDismiss.SequenceNumber);
        Assert.Equal(WifiPrefs, verifiedContainer.SemanticPageName);
        Assert.Equal(
            new[]
            {
                CapstoneSettingsWorldFixture.WifiCallingText,
                CapstoneSettingsWorldFixture.DismissText,
                "Return to Wi-Fi",
            },
            verifiedContainer.ExecutedSteps.Select(step => step.TargetDescription));
        Assert.DoesNotContain(evidence.Trace, entry => entry.TrapScope == TrapScope.Container);
    }

    [Fact]
    public async Task Drift_TriggersFrozenRecovery_WithVerifiedReconciliationAndNoNewProgressForReentry()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var fixture = harness.Fixture;
        var evidence = await harness.RunAsync();

        // Assertion 6 (first half): exactly one external Launcher drift at the deterministic point
        // (seq 20), with the structured Agent-scope Trap (Expected = trusted root binding seq 19,
        // Observed = drift seq 20).
        var drift = evidence.Observations.Single(observation =>
            string.Equals(observation.ForegroundApplication, "Launcher", StringComparison.Ordinal));
        Assert.Equal(CapstoneSettingsRunHarness.DriftObservationSequence, drift.SequenceNumber);
        Assert.Empty(drift.Elements);
        var trap = evidence.LastTrap ?? throw new InvalidOperationException("Expected one Agent drift Trap.");
        Assert.Equal(TrapKind.UnexpectedPage, trap.Kind);
        Assert.Equal(TrapScope.Agent, trap.Scope);
        Assert.Equal(19L, trap.Expected);
        Assert.Equal(20L, trap.Observed);

        // The frozen recovery path ran: re-enter (LaunchApp), observe, verify VERIFIED, reconcile,
        // resume from the suspended root-bound index.
        Assert.Contains(evidence.Trace, entry =>
            entry.Reason?.Contains("recovery verify: VERIFIED", StringComparison.Ordinal) == true);

        // Fresh recovered-world re-entry: the observation after LaunchApp is the re-entered trusted
        // root with fresh recovered-world evidence.
        var reentered = evidence.Observations.Single(observation =>
            observation.SequenceNumber == CapstoneSettingsRunHarness.DriftObservationSequence + 1);
        Assert.Equal(SettingsRoot, CapstoneSettingsWorldFixture.ResolveSemanticPage(reentered));
        Assert.Contains(reentered.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.RecoveredEvidenceText, StringComparison.Ordinal));
        Assert.True(reentered.SequenceNumber > drift.SequenceNumber);

        // Frozen SC-P2 recovery needs no restore taps: the recovered trusted root already rebinds
        // the suspended root container, then plan index 16 retries the Display dispatch.
        var launches = evidence.ActionHistory
            .Select((action, index) => (Action: action, Index: index))
            .Where(item => item.Action is DeviceAction.LaunchApp)
            .ToArray();
        Assert.Equal(2, launches.Length);
        Assert.Equal(
            new DeviceAction.LaunchApp(TargetApplication),
            Assert.IsType<DeviceAction.LaunchApp>(launches[1].Action));
        var resumedDispatch = launches[1].Index + 1;
        Assert.Equal(new DeviceAction.Tap(1, new ElementBounds(0f, 0.1f, 1f, 0.2f)), evidence.ActionHistory[resumedDispatch]);
        Assert.Equal(21L, evidence.Observations[launches[1].Index + 1].SequenceNumber);

        // Assertion 6 (second half): the recovered-root carrier revalidates Network exactly once.
        Assert.True(evidence.CarrierCriterionOutcome);
    }

    [Fact]
    public async Task Progress_NeitherFabricatedNorDiscarded_ReentryIsNotNewProgress()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var evidence = await harness.RunAsync();

        // Assertion 7 (progress retained across Recovery): every evidence evaluation snapshot holds
        // the accepted SettingsRoot inventory and monotone, evidence-bound completions. Nothing is
        // invented, and recovered-root revalidation updates Network from its historical seq 18 to 21.
        Assert.Equal(32, evidence.ProgressSnapshots.Length); // CP-06：seq2 初始评估快照在前
        Assert.All(evidence.ProgressSnapshots, snapshot =>
        {
            var entry = Assert.Single(snapshot);
            Assert.Equal(SettingsRoot, entry.Key);
            Assert.Equal(3, entry.Value.ApprovedSiblingEvidence.Count);
        });
        Assert.Equal(21L, evidence.ProgressSnapshots[18][SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.NetworkInternetText]); // CP-06：index +2（seq2 初始快照 + drift 后恢复点移位）
        Assert.Equal(21L, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.NetworkInternetText]);
        Assert.Equal(27L, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.DisplayText]);
        Assert.Equal(34L, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.SystemResetText]);
    }

    [Fact]
    public async Task VerifiedWork_IsNotDoubleCounted()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var evidence = await harness.RunAsync();

        // Assertion 8: each approved sibling has exactly one final evidence-bound completion.
        var completed = evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence;
        Assert.Equal(3, completed.Count);
        Assert.Equal(21L, completed[CapstoneSettingsWorldFixture.NetworkInternetText]);
        Assert.Equal(27L, completed[CapstoneSettingsWorldFixture.DisplayText]);
        Assert.Equal(34L, completed[CapstoneSettingsWorldFixture.SystemResetText]);

        // The journal holds each executed step exactly once — the recovered suspended step appears
        // once (Step-10) even though it was dispatched before the drift (its post-action observation
        // was never accepted as progress evidence).
        Assert.Equal(
            evidence.Journal.Length,
            evidence.Journal.Select(entry => entry.StepId).Distinct().Count());
        Assert.Equal(33, evidence.Journal.Select(entry => entry.StepId).Distinct().Count());
    }

    [Fact]
    public async Task ExhaustionDispatchRecoveryViewportLocalCompletion_AloneDoNotCompleteTheRun()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var evidence = await harness.RunAsync();

        // Positive run: the run continues past every non-completing event — the Popup handling
        // (seq 8), verified continuity (seq 9), recovered root (seq 21), and the evaluation
        // immediately before the root viewport movement (seq 35) — none alone completes the Run.
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 8).Satisfied);
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 9).Satisfied);
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 22).Satisfied);
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 35).Satisfied);
        Assert.Single(evidence.GoalEvidence, item => item.Satisfied);
        Assert.Equal(36L, evidence.GoalEvidence.Single(item => item.Satisfied).SourceObservationSequence);

        // Negative control (assertion 9): with an always-unsatisfied Goal evidence evaluator, Plan
        // exhaustion, every dispatch, the Recovery dispatch, the viewport snapshot change, and every
        // local Container completion occur — and the Run still FAILS. No RunState.Completed is ever
        // recorded, and no satisfied GoalEvidence is produced.
        var negative = await CapstoneSettingsRunHarness.CreateAlwaysUnsatisfied().RunAsync();
        Assert.Equal(RunState.Failed, negative.State);
        Assert.Contains("Plan 步数耗尽", negative.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(negative.GoalEvidence, item => item.Satisfied);
        Assert.DoesNotContain(negative.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(RunState.Failed, negative.Trace[^1].RunState);
    }

    [Fact]
    public async Task BoundedViewportMovement_OneScrollWithinSameSemanticContainer_FreshEvidence()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var fixture = harness.Fixture;
        var evidence = await harness.RunAsync();

        // Exactly one bounded forward viewport movement, dispatched while the Settings root
        // container is active, with fresh same-Container identity evidence before and after.
        var scrolls = evidence.ActionHistory
            .Select((action, index) => (Action: action, Index: index))
            .Where(item => item.Action is DeviceAction.ScrollForward)
            .ToArray();
        Assert.Single(scrolls);
        var scrollIndex = scrolls[0].Index;
        var before = evidence.Observations[scrollIndex];
        var after = evidence.Observations[scrollIndex + 1];
        Assert.Equal(35L, before.SequenceNumber);
        Assert.Equal(36L, after.SequenceNumber);
        Assert.Equal(SettingsRoot, CapstoneSettingsWorldFixture.ResolveSemanticPage(before));
        Assert.Equal(SettingsRoot, CapstoneSettingsWorldFixture.ResolveSemanticPage(after));
        Assert.Contains(before.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.NetworkInternetText, StringComparison.Ordinal));
        Assert.DoesNotContain(before.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.SettingsTraversalSummaryText, StringComparison.Ordinal));
        Assert.Contains(after.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.SettingsTraversalSummaryText, StringComparison.Ordinal));
        Assert.True(after.SequenceNumber > before.SequenceNumber);

        // The CAND-007 evaluator is wired on the Goal (read-only production surface) and its
        // recomputed outcome is honest: the initial accepted evidence justifies exactly one bounded
        // movement; the fresh viewport evidence positively proves the bounded content fully visible.
        Assert.NotNull(harness.Goal.ViewportExplorationEvaluator);
        var initialDecision = CapstoneSettingsWorldFixture.EvaluateViewportExploration(
            ImmutableArray.Create(before));
        var freshDecision = CapstoneSettingsWorldFixture.EvaluateViewportExploration(
            ImmutableArray.Create(before, after));
        Assert.True(initialDecision.ContinueExploration);
        Assert.False(freshDecision.ContinueExploration);
        Assert.Contains("fully visible", freshDecision.Reason, StringComparison.Ordinal);

        // The composed run emits exactly one Trap (the Agent drift); the viewport movement is
        // expressed through the frozen same-Container protocol with no viewport or Container Trap.
        var traps = evidence.Trace.Where(entry => entry.TrapKind is not null).ToArray();
        var agentTrap = Assert.Single(traps);
        Assert.Equal(TrapScope.Agent, agentTrap.TrapScope);
    }

    [Fact]
    public async Task EqualInputs_ReplayEqualEverything()
    {
        // Completion evidence 7: equal inputs (same world creation inputs, same Plan, same frozen
        // wiring) replay to equal progress / ActionHistory / Observations / journal / Trace /
        // GoalEvidence / final RunState, and to an equal Container binding sequence.
        var firstHarness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var secondHarness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var first = await firstHarness.RunAsync();
        var second = await secondHarness.RunAsync();

        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.LastTrap, second.LastTrap);
        Assert.Equal(first.CarrierCriterionOutcome, second.CarrierCriterionOutcome);
        AssertProgressEqual(first.FinalProgress, second.FinalProgress);
        Assert.Equal(first.ProgressSnapshots.Length, second.ProgressSnapshots.Length);
        for (var index = 0; index < first.ProgressSnapshots.Length; index++)
        {
            AssertProgressEqual(first.ProgressSnapshots[index], second.ProgressSnapshots[index]);
        }

        Assert.Equal(first.ActionHistory, second.ActionHistory);
        AssertObservationsEqual(first.Observations, second.Observations);
        AssertJournalEqual(first.Journal, second.Journal);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.GoalEvidence, second.GoalEvidence);

        Assert.Equal(firstHarness.Containers.Length, secondHarness.Containers.Length);
        for (var index = 0; index < firstHarness.Containers.Length; index++)
        {
            Assert.Equal(
                firstHarness.Containers[index].SemanticPageName,
                secondHarness.Containers[index].SemanticPageName);
            Assert.Equal(
                firstHarness.Containers[index].CurrentObservation?.SequenceNumber,
                secondHarness.Containers[index].CurrentObservation?.SequenceNumber);
            Assert.Equal(
                firstHarness.Containers[index].ExecutedSteps.Select(step => step.TargetDescription),
                secondHarness.Containers[index].ExecutedSteps.Select(step => step.TargetDescription));
        }
    }

    private static void AssertProgressEqual(
        IReadOnlyDictionary<string, BranchProgressEvidence> expected,
        IReadOnlyDictionary<string, BranchProgressEvidence> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
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
            {
                Assert.Equal(expected[index].Elements[element], actual[index].Elements[element]);
            }
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
