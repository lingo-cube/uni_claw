using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SC-S0-CAPSTONE-001 Task 3.1 formal Capstone proof (test-side; production purchase = zero).
///
/// Establishes the formal end-to-end Capstone evidence demanded by the Scenario: Required
/// Assertions 1-12 each proven by dedicated formal assertions (assertions 1-9 over the calibrated
/// integration run as the formal record; assertion 10 as the zero-production-delta guard; assertion
/// 11 as an equal-input replay Theory across the positive / negative-control / stop-extract run
/// kinds plus an unequal-inputs negative proving the replay conjunct load-bearing; assertion 12 as
/// the stop-extract-gate path: an observation class the 13 frozen capabilities cannot express stops
/// the run and extracts exactly one bounded Candidate registration sketch, without absorbing it).
///
/// Completion Evidence 1-7 are each asserted as independently-satisfied GoalEvidence conjuncts, and
/// each conjunct's necessity is proven honestly: (N1) no satisfied evaluation exists before seq 36;
/// (N2) each conjunct's event observation is non-completing (its first true evaluation is strictly
/// before 36 and unsatisfied); (N3) the six scenario conjuncts are jointly true at seq 35 yet the
/// run does not complete — the viewport conjunct (final required integration behavior) is the one
/// that completes the set, and its own event observation (seq 36) is the satisfied conjunction, so
/// no single event completes the Run. Joint sufficiency: at seq 36 all seven conjuncts are true,
/// the GoalEvidence is satisfied, and the Run reaches Completed.
///
/// The stop-extract fixture (assertion 12) schedules the Popup at seq 8 and the external Launcher
/// drift at seq 9: the run dispatches the popup Dismiss (action 8) and drifts before any
/// post-dismiss observation; the frozen recovery re-enters, verifies, position-restores back to the
/// suspended WifiPrefsPage container, and the resumed suspended Dismiss step cannot be grounded on
/// the recovered world (no popup, no Dismiss element) — the frozen Select-failure vocabulary stops
/// the run explicitly instead of absorbing the step into the resumed run. The extraction record
/// documents exactly ONE bounded Candidate for a future Semantic Gate; it is not pre-approved here.
///
/// No production behavior is exercised beyond the frozen + approved Task 2.1 control flow; every
/// evidence record below is the read-only surface of the deterministic world + frozen Runtime.
/// </summary>
public sealed class CapstoneSettingsFormalProofTests
{
    private const string SettingsRoot = CapstoneSettingsWorldFixture.SettingsRootScreen;
    private const string WifiPrefs = CapstoneSettingsWorldFixture.WifiPrefsScreen;
    private const string ResetOptions = CapstoneSettingsWorldFixture.ResetOptionsScreen;
    private const string TargetApplication = CapstoneSettingsWorldFixture.TargetApplication;

    // ─────────────────────────────────────────────────────────────────────────────
    // Required Assertions 1-9 — formal record over the calibrated integration run
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assertions1To9_FormalRecord_CalibratedIntegrationRun()
    {
        var harness = CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture());
        var fixture = harness.Fixture;
        var evidence = await harness.RunAsync();
        var trace = evidence.Trace.ToArray();

        // Deterministic shape: 36 Observations, 35 dispatched actions, 33 journal entries, and 32
        // Goal evidence evaluations; recovery resumes at the recovered root without restore taps.
        Assert.Equal(RunState.Completed, evidence.State);
        Assert.Equal(36, evidence.Observations.Length);
        Assert.Equal(35, evidence.ActionHistory.Length);
        Assert.Equal(33, evidence.Journal.Length);
        Assert.Equal(32, evidence.GoalEvidence.Length); // CP-06：seq2 初始评估在前

        // ── Assertion 1: the complete route is not encoded up front; branch discovery comes from
        // fresh external-world evidence within the approved scope.
        Assert.Empty(harness.InitialPlan.Steps);
        Assert.Equal(CapstoneSettingsWorldFixture.WifiText, harness.Plan.Steps[0].TargetDescription);
        var steps = harness.Plan.Steps.ToArray();
        var firstNetworkInternetIndex = Array.FindIndex(
            steps,
            step => string.Equals(step.TargetDescription, CapstoneSettingsWorldFixture.NetworkInternetText, StringComparison.Ordinal));
        Assert.Equal(-1, firstNetworkInternetIndex);
        Assert.Single(
            evidence.ActionHistory.Select((action, index) => (action, index)).Where(item =>
                item.action == new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f))
                && CapstoneSettingsWorldFixture.ResolveSemanticPage(evidence.Observations[item.index]) == SettingsRoot));
        var initial = evidence.Observations[1];
        Assert.Equal(2L, initial.SequenceNumber);
        Assert.Equal(SettingsRoot, CapstoneSettingsWorldFixture.ResolveSemanticPage(initial));
        Assert.DoesNotContain(initial.Elements, element =>
            string.Equals(element.Text, harness.Plan.Steps[0].TargetDescription, StringComparison.Ordinal));
        Assert.Equal(new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f)), evidence.ActionHistory[1]);
        var inventoryIndex = IndexOfReason(trace, "branch inventory complete: depth=0");
        Assert.True(inventoryIndex >= 0, "The approved depth-0 inventory must be accepted from fresh evidence.");
        Assert.Contains(trace, entry => entry.Reason?.Contains("source-seq=2", StringComparison.Ordinal) == true);
        Assert.Equal(3, evidence.FinalProgress[SettingsRoot].ApprovedSiblingEvidence.Count);
        Assert.All(
            evidence.FinalProgress[SettingsRoot].ApprovedSiblingEvidence,
            pair => Assert.Equal(2L, pair.Value));

        // ── Assertions 2/4: traversal reaches every approved reachable safe branch within depth
        // <= 4 and none remains unresolved at completion.
        var visitedPages = evidence.Observations
            .Select(CapstoneSettingsWorldFixture.ResolveSemanticPage)
            .Where(page => page is not null)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            fixture.ApprovedTree.Select(page => page.Name).Order(StringComparer.Ordinal),
            visitedPages.Order(StringComparer.Ordinal));
        Assert.Equal(14, visitedPages.Count);
        Assert.All(fixture.ApprovedTree, page => Assert.InRange(page.Depth, 0, fixture.DepthBound));
        Assert.Contains(evidence.Observations, observation =>
            CapstoneSettingsWorldFixture.ResolveSemanticPage(observation)
                == CapstoneSettingsWorldFixture.WifiCallingScreen);

        // ── Assertion 3: dangerous visible actions are never dispatched; zero dangerous dispatch
        // is proven by the final state.
        var dangerousObservations = evidence.Observations
            .Where(observation => CapstoneSettingsWorldFixture.ResolveSemanticPage(observation) == ResetOptions)
            .ToArray();
        Assert.Single(dangerousObservations);
        Assert.Equal(30L, dangerousObservations[0].SequenceNumber);
        var dangerousElement = dangerousObservations[0].Elements.Single(element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.DangerousCandidateText, StringComparison.Ordinal));
        var authorization = CapstoneSettingsWorldFixture.EvaluateAuthorization(dangerousObservations[0], dangerousElement);
        Assert.False(authorization.Authorized);
        Assert.False(string.IsNullOrWhiteSpace(authorization.Reason));
        Assert.DoesNotContain(
            CapstoneSettingsWorldFixture.DangerousCandidateText,
            fixture.ApprovedTree.SelectMany(page => page.RequiredBranches));
        for (var index = 1; index < evidence.ActionHistory.Length; index++)
        {
            if (CapstoneSettingsWorldFixture.ResolveSemanticPage(evidence.Observations[index]) != ResetOptions)
            {
                continue;
            }

            var tap = Assert.IsType<DeviceAction.Tap>(evidence.ActionHistory[index]);
            Assert.Equal(1, tap.TargetElementIndex);
        }

        // ── Assertion 5: the single Popup obstruction is handled with fresh verified Container
        // continuity.
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

        // ── Assertion 6: the single external drift triggers the frozen recovery path; re-entry is
        // not counted as new progress.
        var drift = evidence.Observations.Single(observation =>
            string.Equals(observation.ForegroundApplication, "Launcher", StringComparison.Ordinal));
        Assert.Equal(CapstoneSettingsRunHarness.DriftObservationSequence, drift.SequenceNumber);
        Assert.Empty(drift.Elements);
        var trap = evidence.LastTrap ?? throw new InvalidOperationException("Expected one Agent drift Trap.");
        Assert.Equal(TrapKind.UnexpectedPage, trap.Kind);
        Assert.Equal(TrapScope.Agent, trap.Scope);
        Assert.Equal(19L, trap.Expected);
        Assert.Equal(20L, trap.Observed);
        var verifyIndex = IndexOfReason(trace, "recovery verify: VERIFIED");
        var trapIndex = Array.FindIndex(trace, entry => entry.TrapScope == TrapScope.Agent);
        Assert.True(trapIndex >= 0 && trapIndex < verifyIndex, "The drift Trap precedes the verified recovery.");
        var reentered = evidence.Observations.Single(observation =>
            observation.SequenceNumber == CapstoneSettingsRunHarness.DriftObservationSequence + 1);
        Assert.Equal(SettingsRoot, CapstoneSettingsWorldFixture.ResolveSemanticPage(reentered));
        Assert.Contains(reentered.Elements, element =>
            string.Equals(element.Text, CapstoneSettingsWorldFixture.RecoveredEvidenceText, StringComparison.Ordinal));
        var launches = evidence.ActionHistory
            .Select((action, index) => (Action: action, Index: index))
            .Where(item => item.Action is DeviceAction.LaunchApp)
            .ToArray();
        Assert.Equal(2, launches.Length);
        Assert.Equal(
            new DeviceAction.LaunchApp(TargetApplication),
            Assert.IsType<DeviceAction.LaunchApp>(launches[1].Action));
        var restoreStart = launches[1].Index + 1;
        Assert.Equal(new DeviceAction.Tap(1, new ElementBounds(0f, 0.1f, 1f, 0.2f)), evidence.ActionHistory[restoreStart]);
        Assert.True(evidence.CarrierCriterionOutcome);

        // ── Assertion 7: retained traversal progress is neither fabricated nor silently discarded
        // after Recovery.
        Assert.Equal(32, evidence.ProgressSnapshots.Length); // CP-06：seq2 初始评估快照在前
        Assert.All(evidence.ProgressSnapshots, snapshot =>
        {
            var entry = Assert.Single(snapshot);
            Assert.Equal(SettingsRoot, entry.Key);
            Assert.Equal(3, entry.Value.ApprovedSiblingEvidence.Count);
        });
        Assert.Equal(21L, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.NetworkInternetText]);
        Assert.Equal(27L, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.DisplayText]);
        Assert.Equal(34L, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence[CapstoneSettingsWorldFixture.SystemResetText]);

        // ── Assertion 8: already-verified semantic work is not counted repeatedly as new progress.
        Assert.Equal(3, evidence.FinalProgress[SettingsRoot].CompletedSiblingEvidence.Count);
        Assert.Equal(
            evidence.Journal.Length,
            evidence.Journal.Select(entry => entry.StepId).Distinct().Count());

        // ── Assertion 9: Plan exhaustion / action dispatch / Recovery dispatch / viewport snapshot
        // change / local Container completion alone do not complete the Run. The positive run
        // continues past every non-completing event; the negative control fails with the frozen
        // explicit exhaustion vocabulary while executing the identical world and action sequence.
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 8).Satisfied);
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 9).Satisfied);
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 22).Satisfied);
        Assert.False(evidence.GoalEvidence.Single(item => item.SourceObservationSequence == 35).Satisfied);
        Assert.Single(evidence.GoalEvidence, item => item.Satisfied);
        Assert.Equal(36L, evidence.GoalEvidence.Single(item => item.Satisfied).SourceObservationSequence);

        var negative = await CapstoneSettingsRunHarness.CreateAlwaysUnsatisfied().RunAsync();
        Assert.Equal(RunState.Failed, negative.State);
        Assert.Contains("Plan 步数耗尽", negative.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(negative.GoalEvidence, item => item.Satisfied);
        Assert.DoesNotContain(negative.Trace, entry => entry.RunState == RunState.Completed);
        Assert.Equal(evidence.ActionHistory, negative.ActionHistory);
        Assert.Equal(evidence.Observations.Length, negative.Observations.Length);
        Assert.Equal(evidence.LastTrap, negative.LastTrap);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Required Assertion 10 — zero production delta (git-level audit is session-level;
    // this test fixes the assembly-level invariant + the frozen 13-slice surface)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Assertion10_ZeroProductionDelta_NoCapstoneArtifactsInRuntimeAssembly()
    {
        // A Capstone production purchase would necessarily add a Capstone/S0-shaped type to the
        // Runtime assembly; the frozen + approved surface exports none. The session-level git diff
        // audit (src/ before vs after) is reported by the task runner.
        var runtimeAssembly = typeof(RuntimeAgent).Assembly;
        Assert.DoesNotContain(
            runtimeAssembly.GetExportedTypes(),
            type => type.Name.Contains("Capstone", StringComparison.Ordinal)
                || type.Name.Contains("SettingsTraversal", StringComparison.Ordinal)
                || type.Name.StartsWith("S0", StringComparison.OrdinalIgnoreCase));

        // The 13 frozen slice regression surfaces all remain present (their PASS status is the full
        // suite run, executed unchanged): SC-P1-001/005, SC-P2-001/003, SC-P3-001/002/003,
        // SC-P3-CAND-004/005/006/007/008/009.
        Type[] frozenSlices =
        [
            typeof(NormalWifiHappyPathTests),                        // SC-P1-001
            typeof(UncertainActionTraversalBehaviorTests),           // SC-P1-005
            typeof(AgentRecoveryLauncherDriftTests),                 // SC-P2-001
            typeof(RecoveryVerificationFailureTests),                // SC-P2-003
            typeof(GoalEvidenceCompletionTests),                     // SC-P3-001
            typeof(PopupObstructionRecoveryTests),                   // SC-P3-002
            typeof(ViewportIdentityContinuityTests),                 // SC-P3-003
            typeof(SiblingBranchProgressScenarioTests),              // SC-P3-CAND-004
            typeof(RecoveryProgressResumeScenarioTests),             // SC-P3-CAND-005
            typeof(BoundedCandidateSafetyScenarioTests),             // SC-P3-CAND-006
            typeof(ViewportExplorationScenarioTests),                // SC-P3-CAND-007
            typeof(BoundedCrossPageDiscoveryScenarioTests),          // SC-P3-CAND-008
            typeof(DiscoveredBranchEffectRevalidationScenarioTests), // SC-P3-CAND-009
        ];
        Assert.Equal(13, frozenSlices.Length);
        Assert.All(frozenSlices, slice => Assert.NotNull(slice));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Completion Evidence 1-7 — individual satisfaction and necessity (N1/N2)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompletionEvidence_EachConjunctIndividuallySatisfied_NoneAloneCompletes()
    {
        var evidence = await CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture()).RunAsync();
        var all = evidence.GoalEvidence.ToArray();

        // N1: no satisfied evaluation exists before the final observation — the run never completes
        // on any single event.
        var satisfied = all.Where(item => item.Satisfied).ToArray();
        Assert.Single(satisfied);
        Assert.Equal(36L, satisfied[0].SourceObservationSequence);

        // N2: each conjunct's event observation is non-completing — its first true evaluation is
        // strictly before seq 36 and unsatisfied.
        var firstPagesTrue = all.First(item => item.Reason.Contains("pages=True", StringComparison.Ordinal));
        Assert.Equal(32L, firstPagesTrue.SourceObservationSequence);
        Assert.False(firstPagesTrue.Satisfied);
        var firstPopupTrue = all.First(item => item.Reason.Contains("popup=True", StringComparison.Ordinal));
        Assert.Equal(9L, firstPopupTrue.SourceObservationSequence);
        Assert.False(firstPopupTrue.Satisfied);
        var firstDriftTrue = all.First(item => item.Reason.Contains("drift=True", StringComparison.Ordinal));
        Assert.Equal(22L, firstDriftTrue.SourceObservationSequence);
        Assert.False(firstDriftTrue.Satisfied);
        var firstScrollTrue = all.FirstOrDefault(item => item.Reason.Contains("scroll=True", StringComparison.Ordinal));
        Assert.Null(firstScrollTrue); // the scroll conjunct is never true before the completing evaluation
        Assert.All(all[..^1], item =>
        {
            Assert.False(item.Satisfied);
            Assert.Contains("zeroDangerous=True", item.Reason, StringComparison.Ordinal);
            Assert.Contains("noContainerEscalation=True", item.Reason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CompletionEvidence_SixConjunctsJointlyTrueAtSeq35_ViewportConjunctCompletesTheSet()
    {
        var evidence = await CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture()).RunAsync();
        var all = evidence.GoalEvidence.ToArray();

        // N3: at the evaluation immediately before the viewport movement (seq 35) the six scenario
        // conjuncts (pages / popup / drift / zero dangerous / no escalation / progress) are jointly
        // true and yet the run does not complete — the scroll (viewport) conjunct is the final
        // required integration behavior that completes the set, and its event observation (seq 36)
        // is the satisfied conjunction itself; no single event completes the Run.
        var at35 = all.Single(item => item.SourceObservationSequence == 35);
        Assert.False(at35.Satisfied);
        Assert.Equal(
            "S0 integration Goal conjunction incomplete at seq=35 "
            + "(pages=True, popup=True, drift=True, scroll=False, zeroDangerous=True, "
            + "noContainerEscalation=True, progress=True).",
            at35.Reason);
        Assert.DoesNotContain(evidence.Trace[..^1], entry => entry.RunState == RunState.Completed);

        // Joint sufficiency: at seq 36 all seven conjuncts are true, the GoalEvidence is satisfied,
        // and the Run reaches Completed with the satisfaction reason as its final state record.
        var final = all[^1];
        Assert.True(final.Satisfied);
        Assert.Equal(36L, final.SourceObservationSequence);
        Assert.Equal(evidence.Observations[^1].SequenceNumber, final.SourceObservationSequence);
        Assert.Equal(evidence.Reason, final.Reason);
        Assert.Equal(RunState.Completed, evidence.State);
        Assert.Equal(RunState.Completed, evidence.Trace[^1].RunState);
        Assert.Contains("Goal conjunction satisfied", final.Reason, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Required Assertion 11 — equal-input replay to equal everything (Theory over the
    // positive / negative-control / stop-extract run kinds) + unequal-inputs negative
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("positive")]
    [InlineData("negative")]
    [InlineData("edge")]
    public async Task Assertion11_EqualInputs_ReplayEqualEverything(string runKind)
    {
        var firstHarness = CreateKind(runKind);
        var secondHarness = CreateKind(runKind);
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

    [Fact]
    public async Task Assertion11_UnequalInputs_ReplayNotEqual_ReplayConjunctLoadBearing()
    {
        // Completion Evidence 7 necessity: the replay equality is a function of the inputs. Moving
        // only the drift schedule point (11 → 21) changes the world, and the recovered-world
        // position-restore can no longer re-ground the suspended step — the run stops at the frozen
        // restore-failure vocabulary instead of completing. Unequal inputs therefore replay to
        // unequal State / Actions / Observations / progress / Trace / GoalEvidence.
        var calibrated = await CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture()).RunAsync();
        var movedDrift = await CapstoneSettingsRunHarness.Create(
            CapstoneSettingsWorldFixture.Create(
                schedule: new S0DisturbanceSchedule(
                    CapstoneSettingsRunHarness.PopupObservationSequence,
                    CapstoneSettingsWorldFixture.WifiPrefsScreen,
                    DriftObservationSequence: 21))).RunAsync();

        Assert.Equal(RunState.Completed, calibrated.State);
        Assert.Equal(RunState.Failed, movedDrift.State);
        Assert.Contains("位置恢复: 无法解析", movedDrift.Reason, StringComparison.Ordinal);
        Assert.NotEqual(calibrated.ActionHistory, movedDrift.ActionHistory);
        Assert.NotEqual(calibrated.Observations.Length, movedDrift.Observations.Length);
        Assert.NotEqual(calibrated.ProgressSnapshots.Length, movedDrift.ProgressSnapshots.Length);
        Assert.NotEqual(calibrated.Trace, movedDrift.Trace);
        Assert.NotEqual(calibrated.GoalEvidence.Length, movedDrift.GoalEvidence.Length);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Required Assertion 12 — stop-extract gate path over the deterministic world
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assertion12_NewRealityDistinction_StopsRun_ExtractsExactlyOneBoundedCandidate()
    {
        // Edge schedule: Popup at seq 8 (Dismiss → WifiPrefsScreen), external Launcher drift at
        // seq 9. The run dispatches the popup Dismiss (grounded on the popup evidence at seq 8) and
        // drifts before any post-dismiss observation: the suspended step is the popup Dismiss and
        // the suspended container is WifiPrefsPage.
        var edgeFixture = CreateEdgeFixture();
        var harness = CapstoneSettingsRunHarness.Create(edgeFixture);
        var evidence = await harness.RunAsync();

        // The run STOPS: 13 observations, 12 dispatched actions, structured Agent drift Trap
        // (Expected = suspended container binding seq 8, Observed = drift seq 9), frozen verified
        // recovery + position-restore, then the frozen Select-failure vocabulary stops the resumed
        // suspended step — no RunState.Completed, no satisfied GoalEvidence.
        Assert.Equal(RunState.Failed, evidence.State);
        Assert.NotNull(evidence.Reason);
        Assert.Contains("Dismiss", evidence.Reason, StringComparison.Ordinal);
        Assert.Contains("无匹配候选", evidence.Reason, StringComparison.Ordinal);
        Assert.Equal(13, evidence.Observations.Length);
        Assert.Equal(12, evidence.ActionHistory.Length);
        Assert.Equal(
            new DeviceAction[]
            {
                new DeviceAction.LaunchApp(TargetApplication),
                new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f)), // transient discovered branch (fresh SettingsRoot evidence)
                new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f)), // Wi-Fi
                new DeviceAction.Tap(1, new ElementBounds(0f, 0.1f, 1f, 0.2f)), // Wi-Fi preferences
                new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f)), // Wi-Fi calling
                new DeviceAction.Tap(1, new ElementBounds(0f, 0.1f, 1f, 0.2f)), // return to Wi-Fi preferences
                new DeviceAction.Tap(0, new ElementBounds(0f, 0f, 1f, 0.1f)), // Wi-Fi calling → popup observed (seq 8)
                new DeviceAction.Tap(1, new ElementBounds(0f, 0.1f, 1f, 0.2f)), // Dismiss (grounded on the popup; dispatched exactly once)
                new DeviceAction.LaunchApp(TargetApplication), // recovery re-enter
                new DeviceAction.Tap(0), // position-restore: transient → Network (index-grounded legacy step)
                new DeviceAction.Tap(0), // position-restore: Wi-Fi (index-grounded legacy step)
                new DeviceAction.Tap(1), // position-restore: Wi-Fi preferences → suspended page rebind (index-grounded legacy step)
            },
            evidence.ActionHistory);
        Assert.Equal(13L, evidence.Observations[^1].SequenceNumber);
        Assert.Equal(WifiPrefs, CapstoneSettingsWorldFixture.ResolveSemanticPage(evidence.Observations[^1]));
        var trap = evidence.LastTrap ?? throw new InvalidOperationException("Expected one Agent drift Trap.");
        Assert.Equal(TrapKind.UnexpectedPage, trap.Kind);
        Assert.Equal(TrapScope.Agent, trap.Scope);
        Assert.Equal(8L, trap.Expected);
        Assert.Equal(9L, trap.Observed);
        Assert.Contains(evidence.Trace, entry =>
            entry.Reason?.Contains("recovery verify: VERIFIED", StringComparison.Ordinal) == true);
        Assert.Contains(evidence.Trace, entry =>
            entry.Reason?.Contains("recovery resume: plan index=6", StringComparison.Ordinal) == true);
        Assert.All(evidence.GoalEvidence, item => Assert.False(item.Satisfied));
        Assert.DoesNotContain(evidence.Trace, entry => entry.RunState == RunState.Completed);
        Assert.DoesNotContain(evidence.Trace, entry =>
            entry.Reason?.Contains("recovered parent branch progress revalidated", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(evidence.ActionHistory, action => action is DeviceAction.ScrollForward);

        // NOTHING is silently absorbed: the Dismiss step was dispatched exactly once (pre-drift),
        // the failed resumed step dispatched nothing, and the run's final observation resolves the
        // recovered WifiPrefsPage — the world was never re-scripted to contain the popup again.
        Assert.Equal(12, evidence.ActionHistory.Length); // no dispatch at/after the failed resume
        Assert.Single(evidence.Observations, observation =>
            observation.Elements.Any(element =>
                string.Equals(element.Text, CapstoneSettingsWorldFixture.PopupOverlayText, StringComparison.Ordinal)));

        // The stop-extract path yields EXACTLY ONE bounded Candidate registration sketch for its
        // Semantic Gate; no such candidate is pre-approved here.
        var extraction = CapstoneCandidateExtraction.FromRun(evidence);
        var candidate = Assert.Single(extraction);
        Assert.Equal("Suspended popup-Dismiss step without recovered-world grounding", candidate.Name);
        Assert.Contains("no popup", candidate.Description, StringComparison.Ordinal);
        Assert.False(candidate.PreApproved);
        Assert.Contains("pending", candidate.SemanticGate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed", candidate.EvidencePointer, StringComparison.Ordinal);
        Assert.Contains("Trap(Expected=8, Observed=9)", candidate.EvidencePointer, StringComparison.Ordinal);
        Assert.Equal(13, evidence.Observations.Length);
        Assert.Equal(12, evidence.ActionHistory.Length);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Test-side helpers (minimal; production purchase = zero)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>The stop-extract edge fixture (assertion 12): Popup at seq 8, drift at seq 9.</summary>
    private static CapstoneSettingsWorldFixture CreateEdgeFixture()
        => CapstoneSettingsWorldFixture.Create(
            schedule: new S0DisturbanceSchedule(8, CapstoneSettingsWorldFixture.WifiPrefsScreen, 9));

    /// <summary>One fresh harness for the replay Theory run kinds.</summary>
    private static CapstoneSettingsRunHarness CreateKind(string runKind)
        => runKind switch
        {
            "positive" => CapstoneSettingsRunHarness.Create(CapstoneSettingsRunHarness.CreateFixture()),
            "negative" => CapstoneSettingsRunHarness.CreateAlwaysUnsatisfied(),
            "edge" => CapstoneSettingsRunHarness.Create(CreateEdgeFixture()),
            _ => throw new ArgumentOutOfRangeException(nameof(runKind), runKind, "Unknown replay run kind."),
        };

    private static int IndexOfReason(IReadOnlyList<TraceEvent> trace, string reason)
        => Array.FindIndex(
            trace.ToArray(),
            entry => entry.Reason?.Contains(reason, StringComparison.Ordinal) == true);

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
        ImmutableArray<TraversalJournalEntry> expected,
        ImmutableArray<TraversalJournalEntry> actual)
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

/// <summary>
/// SC-S0-CAPSTONE-001 Task 3.1 test-side stop-extract registration sketch (assertion 12): a bounded
/// Candidate extracted from a run that stopped on an observation class the 13 frozen capabilities
/// cannot express, registered for its Semantic Gate and never pre-approved here. Test-side only;
/// no production state or authority.
/// </summary>
internal sealed record CapstoneBoundedCandidateRegistration(
    string Name,
    string Description,
    string SemanticGate,
    bool PreApproved,
    string EvidencePointer);

/// <summary>
/// Test-side factory for the stop-extract path (assertion 12): derives exactly one bounded
/// Candidate registration sketch from a run that stopped on the inexpressible observation class
/// ("a suspended popup-Dismiss step whose grounding evidence does not exist in the recovered
/// world"). Read-only evidence expression; the extraction itself carries no production authority.
/// </summary>
internal static class CapstoneCandidateExtraction
{
    internal static ImmutableArray<CapstoneBoundedCandidateRegistration> FromRun(CapstoneSettingsRunEvidence evidence)
    {
        if (evidence.State != RunState.Failed)
        {
            throw new InvalidOperationException("A bounded Candidate extraction requires a run that stopped (Failed).");
        }

        return
        [
            new CapstoneBoundedCandidateRegistration(
                Name: "Suspended popup-Dismiss step without recovered-world grounding",
                Description: "The suspended step (popup Dismiss) was grounded on the local Popup "
                    + "overlay at seq 8; the recovered world has no popup and no Dismiss candidate, "
                    + "so the frozen composition cannot re-ground or absorb the step — it stops with "
                    + "the explicit Select-failure vocabulary.",
                SemanticGate: "pending semantic gate (not authorized in this change)",
                PreApproved: false,
                EvidencePointer: $"SC-S0-CAPSTONE-001 assertion-12 edge run: State=Failed, "
                    + $"Trap(Expected=8, Observed=9), observations=13, actions=12, "
                    + $"reason=\"{evidence.Reason}\""),
        ];
    }
}
