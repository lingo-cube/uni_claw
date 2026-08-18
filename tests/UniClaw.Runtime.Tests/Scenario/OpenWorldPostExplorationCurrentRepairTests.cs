using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// OPEN_WORLD_POST_EXPLORATION_CURRENT_REPAIR — CURRENT-1..CURRENT-10.
///
/// Deterministic regression for the RunOpenWorldAsync stale-`current` defect:
/// `current = container.CurrentObservation` was captured BEFORE
/// ExploreCurrentContainerViewportsAsync, which refreshes
/// container.CurrentObservation via TryVerifyViewportContinuity /
/// AcceptFreshObservation. The loop MUST reload its local `current` from the
/// container after successful same-Container exploration, BEFORE completeness /
/// inventory acceptance / branch dispatch / GoalEvidence consume it. No Bind,
/// no extra AcceptFreshObservation, no invariant relaxation, no sequence
/// special-casing, no provenance/normalization change.
///
/// The fake world mirrors the COMPOSE-05 capstone's structured rows (clickable
/// LinearLayout rows carrying "Child XX" titles) deterministically, so the
/// production open-world path (Startup -> Agent -> Container -> Traversal ->
/// Environment) is exercised end to end.
/// </summary>
public sealed class OpenWorldPostExplorationCurrentRepairTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private const int ChildCount = 8;

    // ── deterministic structured world (capstone-like rows, no adb/vision) ──

    private sealed class StructuredWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private string _screen = "Launcher";
        private int _viewport;
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public StructuredWorld(string[][] rootViewports)
        {
            _rootViewports = rootViewports;
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
        public IReadOnlyList<Observation> ObservationHistory => _history;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = Build(++_seq);
            _history.Add(observation);
            return Task.FromResult(observation);
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions.Add(action);
            switch (action)
            {
                case DeviceAction.LaunchApp:
                    _screen = "Root";
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "launch", "launch dispatched"));
                case DeviceAction.ScrollForward:
                    if (_screen == "Root" && _viewport < _rootViewports.Length - 1)
                        _viewport++;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport advanced"));
                case DeviceAction.ScrollBackward:
                    if (_screen == "Root" && _viewport > 0)
                        _viewport--;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport receded"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                    {
                        var rows = _rootViewports[_viewport];
                        // Resolve the tapped row by the action's structured bounds
                        // (mirror of the real ADB tap-by-bounds); index is a
                        // legacy fallback.
                        int? idx = ResolveRowIndex(tap, rows.Length);
                        if (idx is { } i && i >= 0 && i < rows.Length)
                        {
                            _visited.Add(rows[i]);
                            _screen = "Child:" + rows[i];
                        }
                    }
                    else if (_screen.StartsWith("Child:", StringComparison.Ordinal))
                    {
                        _screen = "Root";
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap dispatched"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Rejected, "other", "rejected"));
            }
        }

        private Observation Build(long seq)
        {
            if (_screen == "Launcher")
                return new Observation([new ObservedElement("Launcher", null, 0, null, null)], App, seq);
            if (_screen == "Root")
            {
                var rows = _rootViewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < rows.Length; i++)
                    elements.Add(new ObservedElement(rows[i], null, i, RowBounds(i), "menuItem"));
                var state = _visited.Count == ChildCount
                    ? $"Visited {_visited.Count}/{ChildCount} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{ChildCount}";
                elements.Add(new ObservedElement(state, null, rows.Length, null, "text"));
                var structured = rows.Select((row, i) => Row(row, i)).ToImmutableArray();
                return new Observation(elements.ToImmutable(), App, seq) { StructuredElements = structured };
            }
            var title = _screen["Child:".Length..];
            return new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "menuItem"),
                    new ObservedElement(title + " page marker", null, 1, null, "text")),
                App, seq)
            {
                StructuredElements = ImmutableArray.Create(Row(RootPage, 0)),
            };
        }

        private static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                true, false, false, true, true, RowBounds(ordinal), title, null, false, null, null);

        private static ElementBounds RowBounds(int ordinal)
            => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));

        private static int? ResolveRowIndex(DeviceAction.Tap tap, int rowCount)
        {
            if (tap.TargetBounds is { } bounds)
            {
                for (int i = 0; i < rowCount; i++)
                {
                    var b = RowBounds(i);
                    if (Math.Abs(b.Y1 - bounds.Y1) < 0.001f)
                        return i;
                }
            }
            return tap.TargetElementIndex;
        }
    }

    // ── injected criteria (test side; no Runtime internals) ─────────────────

    private static string? Resolve(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, App, StringComparison.Ordinal))
            return null;
        if (observation.Elements.Any(e => e.Text is { } t && t.StartsWith("Visited ", StringComparison.Ordinal)))
            return RootPage;
        var marker = observation.Elements.FirstOrDefault(e =>
            e.Text is { } t && t.EndsWith(" page marker", StringComparison.Ordinal));
        return marker?.Text is { } m ? m[..^" page marker".Length] : null;
    }

    private static string TitleOf(string signature)
    {
        int bar = signature.IndexOf('|');
        return bar < 0 ? signature : signature[..bar];
    }

    private static ImmutableArray<string> NavTitles(Observation observation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            builder.Add(TitleOf(occurrence.StructuredSignature));
        return builder.ToImmutable();
    }

    private static ViewportExplorationEvidence ExploreWhileNew(ImmutableArray<Observation> observations)
    {
        if (observations.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(true, "explore");
        var latest = observations[^1];
        var latestTitles = NavTitles(latest).ToHashSet(StringComparer.Ordinal);
        var prior = observations.Take(observations.Length - 1)
            .SelectMany(o => NavTitles(o)).ToHashSet(StringComparer.Ordinal);
        var hasNew = latestTitles.Any(title => !prior.Contains(title));
        return new ViewportExplorationEvidence(
            hasNew,
            hasNew ? "new source appeared; scroll more" : "no new source; exhausted");
    }

    private static BranchInventoryEvidence Inventory(
        ImmutableArray<Observation> observations,
        int semanticDepth)
    {
        if (semanticDepth >= 1)
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "no child is required inside depth <= 1",
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty);
        var first = new Dictionary<string, NavigationSourceOccurrence>(StringComparer.Ordinal);
        foreach (var observation in observations)
        {
            foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            {
                var title = TitleOf(occurrence.StructuredSignature);
                if (!first.ContainsKey(title))
                    first[title] = occurrence;
            }
        }
        if (first.Count == 0)
            return new BranchInventoryEvidence(
                ImmutableDictionary<string, long>.Empty,
                "no navigation occurrences (bounded leaf)",
                ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty);
        var required = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var (title, occurrence) in first)
        {
            required[title] = occurrence.ObservationSequence;
            grounding[title] = new NavigationSourceOccurrenceReference(
                occurrence.ObservationSequence, occurrence.OccurrenceIdentity);
        }
        return new BranchInventoryEvidence(
            required.ToImmutable(),
            $"inventory: {first.Count} children",
            grounding.ToImmutable());
    }

    private static CandidateAuthorizationEvidence Authorize(Observation observation, ObservedElement candidate)
        => new(
            candidate.Text.StartsWith("Child ", StringComparison.Ordinal)
                || string.Equals(candidate.Text, RootPage, StringComparison.Ordinal),
            $"authorize {candidate.Text}");

    private static GoalEvidence Goal(Observation observation)
        => new(
            observation.Elements.Any(e => e.Text is { } t && t.Contains("CAPSTONE COMPLETE", StringComparison.Ordinal)),
            "capstone goal evidence",
            observation.SequenceNumber);

    // ── run harness (production path: Startup -> Agent -> Container -> Traversal) ──

    private sealed record RunOutcome(
        RunState State,
        string? Reason,
        StructuredWorld Environment,
        RuntimeTraversal Traversal,
        RuntimeAgent Agent,
        List<GoalEvidence> GoalEvidenceReceipts);

    private static async Task<RunOutcome> RunAsync(
        StructuredWorld world,
        Func<ImmutableArray<Observation>, ViewportExplorationEvidence> explore,
        string runId)
    {
        var traversal = new RuntimeTraversal(world);
        var startup = new RuntimeStartup(world, App, Resolve, launchIntentAction: "com.uniclaw.fixture.action.CAPSTONE");
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => world.ObserveAsync(cancellationToken),
            Resolve,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(Resolve(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var receipts = new List<GoalEvidence>();
        var goal = new Goal(
            observation =>
            {
                var evidence = Goal(observation);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: Authorize,
            ViewportExplorationEvaluator: explore,
            BranchInventoryEvaluator: Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Traverse all Fixture Root children to CAPSTONE COMPLETE",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new RunOutcome(state, agent.Reason, world, traversal, agent, receipts);
    }

    // ── CURRENT-2..6: after viewport exploration O2->O5 the loop reloads the
    //    local current and TryAcceptBranchInventory consumes O5 (not O2), while
    //    branch groundings still reference historical O2/O3/O4 occurrences. ────

    [Fact]
    public async Task CURRENT_ReloadAfterViewportExploration_ConsumesLatestCurrent_NotStale()
    {
        // Capstone-like scroll chain: initial viewport shows Child 01-04; each
        // ScrollForward reveals the next slice; the final viewport is a duplicate
        // (positive exhaustion). Groundings reference HISTORICAL sequences
        // (01-04@2, 05-07@3, 08@4) — CURRENT-6.
        var world = new StructuredWorld(
        [
            ["Child 01", "Child 02", "Child 03", "Child 04"],
            ["Child 03", "Child 04", "Child 05", "Child 06", "Child 07"],
            ["Child 05", "Child 06", "Child 07", "Child 08"],
            ["Child 05", "Child 06", "Child 07", "Child 08"],
        ]);

        var run = await RunAsync(world, ExploreWhileNew, "ow-current-1");

        // CURRENT-4/5: the post-exploration invariant held — accepted latest ==
        // current == container.CurrentObservation (O5). The stale O2 was NOT used
        // (CURRENT-2/3). Without the repair this run fails with the stale-current
        // message before any inventory acceptance.
        Assert.False(run.Reason?.Contains("Inventory source is not the current accepted", StringComparison.Ordinal) ?? false);
        Assert.Contains(run.Agent.Trace, entry =>
            entry.Reason?.Contains("open-world branch inventory complete", StringComparison.Ordinal) is true
            && entry.Reason.Contains("source-seq=5", StringComparison.Ordinal));
        Assert.Contains(run.Agent.Trace, entry =>
            entry.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true
            && entry.Reason.Contains("sources=8", StringComparison.Ordinal)
            && entry.Reason.Contains("unresolved=0", StringComparison.Ordinal)
            && entry.Reason.Contains("seq=[2,3,4,5]", StringComparison.Ordinal));
        // CURRENT-6: TryAcceptBranchInventory accepted branches grounded to
        // historical O2/O3/O4 (it did not require every occurrence from O5).

        // BOUNDED SOURCE REVISIT (RVT2): the dispatch now consumes the reloaded O5,
        // dispatches the currently-visible 05..08 at the bottom viewport, then uses
        // the frozen revisit budget (3 forward transitions) to walk back to the top
        // viewport and dispatch Child 01..04 — the full 8-child round trip completes.
        Assert.Equal(RunState.Completed, run.State);
        Assert.Contains(run.Environment.ActionHistory, action => action is DeviceAction.ScrollBackward);
        Assert.Equal(2 * 8, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(run.GoalEvidenceReceipts, receipt => receipt.Satisfied);
    }

    // ── CURRENT-1 / CURRENT-9 / CURRENT-10: with no exploration scroll the local
    //    current stays the current observation and the run completes from the
    //    LATEST GoalEvidence; the legacy bounded-discovery path is untouched. ──

    [Fact]
    public async Task CURRENT_NoScroll_CurrentStaysAndRunCompletesFromLatestGoalEvidence()
    {
        // Whole list visible in ONE viewport; exploration declares exhaustion
        // immediately -> zero ScrollForward actions (CURRENT-1). The reload is a
        // no-op (current was already the container's CurrentObservation). The full
        // 8-child round trip completes and the satisfied GoalEvidence reads the
        // LATEST root Observation (CURRENT-9).
        var world = new StructuredWorld(
        [
            Enumerable.Range(1, ChildCount).Select(i => $"Child {i:D2}").ToArray(),
        ]);

        var run = await RunAsync(
            world,
            _ => new ViewportExplorationEvidence(false, "whole list visible; exhausted immediately"),
            "ow-current-2");

        Assert.Equal(RunState.Completed, run.State);
        Assert.False(run.Reason?.Contains("Inventory source is not the current accepted", StringComparison.Ordinal) ?? false);
        Assert.DoesNotContain(run.Environment.ActionHistory, action => action is DeviceAction.ScrollForward);
        // The 8-child round trip dispatches 8 enter taps + 8 "Fixture Root"
        // return taps.
        Assert.Equal(2 * ChildCount, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(run.Agent.Trace, entry =>
            entry.Reason?.Contains("open-world branch inventory complete", StringComparison.Ordinal) is true
            && entry.Reason.Contains("source-seq=2", StringComparison.Ordinal));
        // COMPLETENESS NON-MONOTONIC EXTENSION (NM-11/NM-12 at the runtime level):
        // each Container (root + 8 children) freezes its discovery epoch exactly
        // once; the post-return fresh root observations are consistency-validated
        // — never re-normalized.
        Assert.Single(run.Agent.Trace, entry =>
            entry.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true
            && entry.ContainerId == RootPage);
        Assert.Contains(run.Agent.Trace, entry =>
            entry.Reason?.Contains("post-completeness consistency PASS", StringComparison.Ordinal) is true);
        var root = run.Agent.BranchProgress[RootPage];
        Assert.True(root.IsSubtreeComplete);
        Assert.Equal(ChildCount, root.CompletedSiblingEvidence.Count);
        Assert.NotEmpty(run.GoalEvidenceReceipts);
        Assert.All(run.GoalEvidenceReceipts, receipt => Assert.True(receipt.Satisfied));
        var final = run.GoalEvidenceReceipts[^1];
        Assert.Equal(world.ObservationHistory[^1].SequenceNumber, final.SourceObservationSequence);
    }
}
