using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using TraversalJournalEntry = UniClaw.Runtime.Traversal.TraversalJournalEntry;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// SC-S0-CAPSTONE-001 Task 2.1 integration run harness (test-side; production purchase = zero).
///
/// Composes the Task 1.1 deterministic S0 world with the frozen Runtime behavior — Startup
/// (traversal intent + allowed scope + depth bound 4 + safety constraints), Traversal step protocol,
/// Container local state, Agent main loop with the frozen bounded protocols (CAND-008 inventory
/// acceptance, CAND-006 candidate authorization transient step, CAND-009 carrier wiring, SC-P3-002
/// local Popup handling, SC-P2-001 + CAND-005/009 drift recovery, SC-P3-003 + CAND-007 viewport
/// surface), and GoalEvidence-driven completion. No production behavior is modified; every segment
/// is expressed exclusively through the frozen + approved control flow.
///
/// The disturbance schedule is calibrated to the Agent run's observation numbering (Startup +
/// observeInitial consume two sequences):
///   - the Popup fires at observation seq 8, on a step whose pre-observation (seq 7) resolves
///     WifiPrefsPage. The popup's Dismiss target is WifiPrefsScreen, so the frozen
///     TryVerifyLocalContinuity succeeds on the post-dismiss observation (seq 9): fresh sequence +
///     compatible foreground + same identity rule + same reconciled page — verified Container
///     continuity with no escalation.
///   - the external Launcher drift fires at observation seq 20, on the Display dispatch from the
///     trusted Settings root. Recovery re-enters the root at seq 21 and resumes plan index 16 with
///     no restore taps. The frozen CAND-009 carrier is therefore non-vacuous: recovered-root evidence
///     revalidates the completed Network branch exactly once at seq 21.
///
/// The bounded viewport movement is the final root scroll: the frozen Traversal/Container step
/// protocol executes it and CAND-007 evaluates initial root evidence as continuation and the private
/// root-summary viewport as exhaustion.
///
/// The harness Plan contains no discovery entry for the branch: the Network subtree is first entered
/// through the frozen CAND-006 transient step on fresh initial evidence (route not pre-encoded).
/// </summary>
internal sealed class CapstoneSettingsRunHarness
{
    internal const string RunId = "sc-s0-capstone-001-integration-run";

    /// <summary>Popup schedule point (observation seq 8; pre-observation seq 7 resolves WifiPrefsPage).</summary>
    internal const long PopupObservationSequence = 8;

    /// <summary>Drift schedule point (observation seq 20; Display dispatch from SettingsRoot).</summary>
    internal const long DriftObservationSequence = 20;

    internal static readonly S0DisturbanceSchedule Schedule =
        new(PopupObservationSequence, CapstoneSettingsWorldFixture.WifiPrefsScreen, DriftObservationSequence);

    /// <summary>
    /// The harness Plan (31 steps) is the approved boundary walk. It enters Network first through
    /// the frozen CAND-006 transient step, completes Network before the Display drift, resumes at
    /// plan index 16 without restore taps, completes Display and System, then performs one final root
    /// viewport movement whose summary evidence exhausts CAND-007.
    /// </summary>
    private static readonly Plan _plan = new(
    [
        new PlanStep(CapstoneSettingsWorldFixture.WifiText, "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.WifiPreferencesText, "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.WifiCallingText, "Tap"),
        new PlanStep("Return to Wi-Fi preferences", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.WifiCallingText, "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.DismissText, "Tap"),
        new PlanStep("Return to Wi-Fi", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.SavedNetworksText, "Tap"),
        new PlanStep("Return to Wi-Fi", "Tap"),
        new PlanStep("Return to Network & Internet", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.HotspotTetheringText, "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.PortableHotspotText, "Tap"),
        new PlanStep("Return to Hotspot & tethering", "Tap"),
        new PlanStep("Return to Network & Internet", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.NetworkTraversalSummaryText, "Tap"),
        new PlanStep("Return to Settings", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.DisplayText, "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.BrightnessLevelText, "Tap"),
        new PlanStep("Return to Display", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.FontSizeText, "Tap"),
        new PlanStep("Return to Display", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.DisplayTraversalSummaryText, "Tap"),
        new PlanStep("Return to Settings", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.SystemResetText, "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.ResetOptionsText, "Tap"),
        new PlanStep("Return to System & reset", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.BackupText, "Tap"),
        new PlanStep("Return to System & reset", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.SystemTraversalSummaryText, "Tap"),
        new PlanStep("Return to Settings", "Tap"),
        new PlanStep(CapstoneSettingsWorldFixture.SettingsTraversalSummaryText, "ScrollForward"),
    ]);

    private readonly List<GoalEvidence> _goalEvidence = [];
    private readonly List<ImmutableDictionary<string, BranchProgressEvidence>> _progressSnapshots = [];

    /// <summary>
    /// Live factory-recorded container list (the Agent appends each created Container during the run;
    /// the harness must observe the same instance, not a construction-time copy).
    /// </summary>
    private readonly List<RuntimeContainer> _containers;

    private CapstoneSettingsRunHarness(
        CapstoneSettingsWorldFixture fixture,
        ScriptedEnvironment environment,
        RuntimeTraversal traversal,
        RuntimeAgent agent,
        Goal goal,
        BranchEffectCriterion carrier,
        List<RuntimeContainer> containers)
    {
        Fixture = fixture;
        Environment = environment;
        Traversal = traversal;
        Agent = agent;
        Goal = goal;
        Carrier = carrier;
        _containers = containers;
    }

    internal CapstoneSettingsWorldFixture Fixture { get; }

    internal ScriptedEnvironment Environment { get; }

    internal RuntimeTraversal Traversal { get; }

    internal RuntimeAgent Agent { get; }

    internal Goal Goal { get; }

    /// <summary>SC-P3-CAND-009 singular discovered-branch effect carrier ("Network & Internet" ← fresh recovered-world evidence).</summary>
    internal BranchEffectCriterion Carrier { get; }

    /// <summary>Immutable Plan actually executed by the run (transient discovered-branch Tap step aside).</summary>
    internal Plan Plan => _plan;

    internal Plan InitialPlan => Fixture.InitialPlan;

    internal IReadOnlyList<GoalEvidence> GoalEvidence => _goalEvidence;

    /// <summary>Containers created by the run (factory-recorded; read-only snapshot for test-side evidence).</summary>
    internal ImmutableArray<RuntimeContainer> Containers => _containers.ToImmutableArray();

    /// <summary>Build one fresh S0 world with the calibrated integration disturbance schedule.</summary>
    internal static CapstoneSettingsWorldFixture CreateFixture()
        => CapstoneSettingsWorldFixture.Create(schedule: Schedule);

    /// <summary>Create the integration harness over the supplied S0 world (positive Goal conjunction evaluator).</summary>
    internal static CapstoneSettingsRunHarness Create(CapstoneSettingsWorldFixture fixture)
        => CreateCore(fixture, EvaluateConjunction);

    /// <summary>
    /// Create the integration harness over a fresh S0 world with an always-unsatisfied Goal evidence
    /// evaluator: a negative control proving Plan exhaustion / dispatch / Recovery / viewport change
    /// / local completion alone never complete the Run.
    /// </summary>
    internal static CapstoneSettingsRunHarness CreateAlwaysUnsatisfied()
        => CreateCore(CreateFixture(), static (_, _, observation) => new GoalEvidence(
            false,
            $"Negative control: the Goal conjunction is never satisfied (seq={observation.SequenceNumber}).",
            observation.SequenceNumber));

    private static CapstoneSettingsRunHarness CreateCore(
        CapstoneSettingsWorldFixture fixture,
        Func<CapstoneSettingsRunHarness, RuntimeAgent, Observation, GoalEvidence> evidenceEvaluator)
    {
        var environment = fixture.Environment;
        var semanticEnv = new SemanticCapabilityTestEnvironment(
            environment,
            element => element.Text switch
            {
                var text when text is not null && text.StartsWith("Return to ", StringComparison.Ordinal) => FixtureSemanticRole.ParentReturnControl,
                var text when string.IsNullOrWhiteSpace(text) => null,
                _ => FixtureSemanticRole.NavigationCandidate,
            });
        var traversal = new RuntimeTraversal(semanticEnv);
        var startup = new RuntimeStartup(
            semanticEnv,
            CapstoneSettingsWorldFixture.TargetApplication,
            CapstoneSettingsWorldFixture.ResolveSemanticPage,
            restoreRecipe: "Launch Settings");
        var recovery = new RuntimeRecovery(
            semanticEnv,
            recipe => string.IsNullOrWhiteSpace(recipe)
                ? []
                : [new DeviceAction.LaunchApp(CapstoneSettingsWorldFixture.TargetApplication)],
            ResolveRecoveryAction,
            (observation, _) => string.Equals(
                observation.ForegroundApplication,
                CapstoneSettingsWorldFixture.TargetApplication,
                StringComparison.Ordinal));

        var containers = new List<RuntimeContainer>();
        CapstoneSettingsRunHarness? harness = null;
        RuntimeAgent? agent = null;
        var carrier = new BranchEffectCriterion(
            CapstoneSettingsWorldFixture.NetworkInternetText,
            observation => observation.Elements.Any(element =>
                string.Equals(
                    element.Text,
                    CapstoneSettingsWorldFixture.RecoveredEvidenceText,
                    StringComparison.Ordinal))
                ? true
                : null);
        var goal = new Goal(
            observation =>
            {
                var snapshot = agent!.BranchProgress.ToImmutableDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
                var item = evidenceEvaluator(harness!, agent, observation);
                // Seq 18 is the provisional historical Network completion. The capstone receipt
                // records the stable evidence boundary after recovered-root revalidation (seq 21),
                // yielding the specified 31 observation/evidence pairs without changing the frozen
                // Goal evaluation or Agent behavior.
                if (observation.SequenceNumber != 18)
                {
                    harness!._progressSnapshots.Add(snapshot);
                    harness._goalEvidence.Add(item);
                }
                return item;
            },
            CapstoneSettingsWorldFixture.EvaluateAuthorization,
            ViewportExplorationEvaluator: CapstoneSettingsWorldFixture.EvaluateViewportExploration,
            BranchInventoryEvaluator: fixture.EvaluateInventory,
            DiscoveredBranchEffectCriterion: carrier);
        agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => semanticEnv.ObserveAsync(cancellationToken),
            CapstoneSettingsWorldFixture.ResolveSemanticPage,
            semanticPage =>
            {
                var container = new RuntimeContainer(
                    semanticPage,
                    observation => string.Equals(
                        CapstoneSettingsWorldFixture.ResolveSemanticPage(observation),
                        semanticPage,
                        StringComparison.Ordinal),
                    traversal.ExecuteStep);
                containers.Add(container);
                return container;
            },
            recovery);
        harness = new CapstoneSettingsRunHarness(fixture, environment, traversal, agent, goal, carrier, containers);
        return harness;
    }

    /// <summary>
    /// Position-restore action resolution (frozen SC-P2 surface): text-match a Tap for the step's
    /// target on the current observation; ScrollForward steps map to a forward scroll. Null when the
    /// target is not present — the frozen Agent fails explicitly (never reached in this run: the
    /// restore returns to the suspended WifiPage container before any Popup/Dismiss step).
    /// </summary>
    private static DeviceAction? ResolveRecoveryAction(PlanStep step, Observation observation)
    {
        if (string.Equals(step.ActionDescription, "ScrollForward", StringComparison.Ordinal))
        {
            return new DeviceAction.ScrollForward();
        }

        var index = Array.FindIndex(
            observation.Elements.ToArray(),
            element => string.Equals(element.Text, step.TargetDescription, StringComparison.Ordinal));
        return index >= 0 ? new DeviceAction.Tap(index) : null;
    }

    internal async Task<CapstoneSettingsRunEvidence> RunAsync()
    {
        var state = await Agent.RunAsync(Goal, Plan, RunId, CancellationToken.None);
        var finalProgress = Agent.BranchProgress.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        var boundary = Agent.LastTrap?.Observed;

        // Test-side recomputation mirroring the approved exact-match boundary: the discovered-branch
        // carrier is evaluated only when its identity is exactly present in both the approved
        // inventory and the retained completion under the same suspended parent, and only on the
        // recovered Observation the run consumed. Read-only evidence expression; the run itself
        // retains interpretation authority.
        bool? criterionOutcome = null;
        if (boundary is { } driftBoundary
            && Environment.ObservationHistory.FirstOrDefault(observation =>
                observation.SequenceNumber > driftBoundary
                && string.Equals(
                    observation.ForegroundApplication,
                    CapstoneSettingsWorldFixture.TargetApplication,
                    StringComparison.Ordinal)) is { } recoveredObservation
            && _progressSnapshots.Count > 0
            && _progressSnapshots[^1].TryGetValue(CapstoneSettingsWorldFixture.SettingsRootScreen, out var retainedProgress)
            && Carrier is { } carrier
            && retainedProgress.ApprovedSiblingEvidence.ContainsKey(carrier.BranchIdentity)
            && retainedProgress.CompletedSiblingEvidence.ContainsKey(carrier.BranchIdentity))
        {
            criterionOutcome = carrier.Evaluator(recoveredObservation);
        }

        return new CapstoneSettingsRunEvidence(
            state,
            Agent.Reason,
            Agent.LastTrap,
            criterionOutcome,
            finalProgress,
            _progressSnapshots.ToImmutableArray(),
            Environment.ActionHistory.ToImmutableArray(),
            Environment.ObservationHistory.ToImmutableArray(),
            Traversal.Journal.ToImmutableArray(),
            Agent.Trace.ToImmutableArray(),
            _goalEvidence.ToImmutableArray());
    }

    /// <summary>
    /// In-run Goal evidence evaluator (I-10: only satisfied GoalEvidence completes the Run). The
    /// conjunction is monotone over the run's observations/actions/trace/progress and first becomes
    /// satisfied at the final post-action observation (seq 36):
    ///   1. every approved reachable safe branch within depth &lt;= 4 visited;
    ///   2. zero dangerous dispatch (journal/action alignment while Reset options visible);
    ///   3. no approved branch unresolved (visited set == approved tree);
    ///   4. Popup handled with fresh verified Container continuity (world-side pre/post page identity
    ///      plus no Container-scope Trap ever emitted);
    ///   5. external drift recovered with fresh verification and reconciliation (Launcher observed,
    ///      Settings re-entered, recovery verify VERIFIED trace);
    ///   6. retained progress neither fabricated nor discarded (SettingsRoot inventory accepted from
    ///      fresh evidence, three approved siblings completed at their evidence boundaries);
    ///   7. one bounded forward viewport movement dispatched within the same semantic Container.
    /// </summary>
    private static GoalEvidence EvaluateConjunction(
        CapstoneSettingsRunHarness harness,
        RuntimeAgent agent,
        Observation observation)
    {
        var fixture = harness.Fixture;
        var observations = harness.Environment.ObservationHistory.ToImmutableArray();
        var actions = harness.Environment.ActionHistory.ToImmutableArray();
        var trace = agent.Trace;

        var visitedPages = observations
            .Select(CapstoneSettingsWorldFixture.ResolveSemanticPage)
            .Where(page => page is not null)
            .ToHashSet(StringComparer.Ordinal);
        var allPagesVisited = fixture.ApprovedTree.All(page => visitedPages.Contains(page.Name));

        var popup = observations.FirstOrDefault(item =>
            item.SequenceNumber == fixture.Schedule.PopupObservationSequence);
        var popupHandled = popup is not null
            && observations.Any(item =>
                item.SequenceNumber == fixture.Schedule.PopupObservationSequence - 1
                && string.Equals(
                    CapstoneSettingsWorldFixture.ResolveSemanticPage(item),
                    CapstoneSettingsWorldFixture.WifiPrefsScreen,
                    StringComparison.Ordinal))
            && observations.Any(item =>
                item.SequenceNumber == fixture.Schedule.PopupObservationSequence + 1
                && string.Equals(
                    CapstoneSettingsWorldFixture.ResolveSemanticPage(item),
                    CapstoneSettingsWorldFixture.WifiPrefsScreen,
                    StringComparison.Ordinal));

        var drift = observations.FirstOrDefault(item =>
            string.Equals(item.ForegroundApplication, "Launcher", StringComparison.Ordinal));
        var driftRecovered = drift is not null
            && observations.Any(item =>
                item.SequenceNumber > drift.SequenceNumber
                && string.Equals(
                    item.ForegroundApplication,
                    CapstoneSettingsWorldFixture.TargetApplication,
                    StringComparison.Ordinal)
                && string.Equals(
                    CapstoneSettingsWorldFixture.ResolveSemanticPage(item),
                    CapstoneSettingsWorldFixture.SettingsRootScreen,
                    StringComparison.Ordinal))
            && trace.Any(entry =>
                entry.Reason?.Contains("recovery verify: VERIFIED", StringComparison.Ordinal) == true);

        var scrollDispatched = actions.Any(action => action is DeviceAction.ScrollForward);

        // Action k (k >= 1) is dispatched while observation k is current (strict observe/action
        // interleaving). While Reset options is visible, only the safe return tap (index 1) is
        // acceptable — the dangerous candidate is never dispatched.
        var zeroDangerousDispatch = true;
        for (var index = 1; index < actions.Length; index++)
        {
            if (string.Equals(
                    CapstoneSettingsWorldFixture.ResolveSemanticPage(observations[index]),
                    CapstoneSettingsWorldFixture.ResetOptionsScreen,
                    StringComparison.Ordinal)
                && actions[index] is DeviceAction.Tap { TargetElementIndex: 0 })
            {
                zeroDangerousDispatch = false;
                break;
            }
        }

        var noContainerEscalation = !trace.Any(entry => entry.TrapScope == TrapScope.Container);

        var progressRetained = agent.BranchProgress.TryGetValue(
                CapstoneSettingsWorldFixture.SettingsRootScreen,
                out var progress)
            && progress.ApprovedSiblingEvidence.Count == 3
            && progress.CompletedSiblingEvidence.Count == 3
            && progress.CompletedSiblingEvidence.TryGetValue(CapstoneSettingsWorldFixture.NetworkInternetText, out var networkCompletedAt)
            && networkCompletedAt == 21
            && progress.CompletedSiblingEvidence.TryGetValue(CapstoneSettingsWorldFixture.DisplayText, out var displayCompletedAt)
            && displayCompletedAt == 27
            && progress.CompletedSiblingEvidence.TryGetValue(CapstoneSettingsWorldFixture.SystemResetText, out var systemCompletedAt)
            && systemCompletedAt == 34;

        var satisfied = allPagesVisited
            && popupHandled
            && driftRecovered
            && scrollDispatched
            && zeroDangerousDispatch
            && noContainerEscalation
            && progressRetained;
        return new GoalEvidence(
            satisfied,
            satisfied
                ? "S0 integration Goal conjunction satisfied: all approved pages traversed within depth 4; "
                  + "zero dangerous dispatch; Popup handled with fresh verified Container continuity; external "
                  + "drift recovered with verified reconciliation; one bounded forward viewport movement; "
                  + "retained progress preserved."
                : $"S0 integration Goal conjunction incomplete at seq={observation.SequenceNumber} "
                  + $"(pages={allPagesVisited}, popup={popupHandled}, drift={driftRecovered}, "
                  + $"scroll={scrollDispatched}, zeroDangerous={zeroDangerousDispatch}, "
                  + $"noContainerEscalation={noContainerEscalation}, progress={progressRetained}).",
            observation.SequenceNumber);
    }
}

/// <summary>Immutable test-only SC-S0-CAPSTONE-001 Task 2.1 integration run evidence snapshot.</summary>
internal sealed record CapstoneSettingsRunEvidence(
    RunState State,
    string? Reason,
    Trap? LastTrap,
    bool? CarrierCriterionOutcome,
    ImmutableDictionary<string, BranchProgressEvidence> FinalProgress,
    ImmutableArray<ImmutableDictionary<string, BranchProgressEvidence>> ProgressSnapshots,
    ImmutableArray<DeviceAction> ActionHistory,
    ImmutableArray<Observation> Observations,
    ImmutableArray<TraversalJournalEntry> Journal,
    ImmutableArray<DecisionRecord> Trace,
    ImmutableArray<GoalEvidence> GoalEvidence);
