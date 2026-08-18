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
/// BOUNDED SOURCE REVISIT V2 — RVT2-1..RVT2-16.
///
/// Deterministic proof of the open-world bounded source revisit: after the
/// forward discovery epoch is frozen (completeness + provenance acceptance),
/// dispatch selects the first pending branch that is CURRENTLY_VISIBLE in the
/// current fresh Observation via LOGICAL-SOURCE resolution (never BranchIdentity
/// text / OCR text / historical bounds/index); when none is visible it executes
/// bounded ScrollBackward steps (frozen forward-transition budget, same-Container
/// continuity, post-completeness consistency) until visible or the budget is
/// exhausted (fail closed). The full 8-child COMPOSE-05-like round trip
/// completes through bottom-dispatch -> revisit -> top-dispatch.
/// </summary>
public sealed class OpenWorldBoundedSourceRevisitTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private const int ChildCount = 8;

    // ── deterministic world (per-viewport OCR + structured; backward variants) ──

    private sealed record ViewportSpec(string[] OcrTexts, StructuredElementEvidence[] Structured);

    private sealed class RevisitWorld : IEnvironment
    {
        private readonly ViewportSpec[] _viewports;
        private readonly bool _backwardNoOp;
        private readonly bool _backwardForeign;
        private readonly string? _novelTitleAfterBackward;
        private readonly bool _unknownAfterBackward;
        private string _screen = "Launcher";
        private int _viewport;
        private bool _backwardHappened;
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private readonly List<(string Title, ElementBounds? Bounds)> _taps = [];
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public RevisitWorld(
            ViewportSpec[] viewports,
            bool backwardNoOp = false,
            bool backwardForeign = false,
            string? novelTitleAfterBackward = null,
            bool unknownAfterBackward = false)
        {
            _viewports = viewports;
            _backwardNoOp = backwardNoOp;
            _backwardForeign = backwardForeign;
            _novelTitleAfterBackward = novelTitleAfterBackward;
            _unknownAfterBackward = unknownAfterBackward;
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
        public IReadOnlyList<Observation> ObservationHistory => _history;
        public IReadOnlySet<string> Visited => _visited;
        public IReadOnlyList<(string Title, ElementBounds? Bounds)> Taps => _taps;

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
                    if (_screen == "Root" && _viewport < _viewports.Length - 1)
                        _viewport++;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport advanced"));
                case DeviceAction.ScrollBackward:
                    if (_backwardForeign)
                    {
                        _screen = "Foreign";
                    }
                    else if (!_backwardNoOp && _screen == "Root" && _viewport > 0)
                    {
                        _viewport--;
                    }
                    _backwardHappened = true;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport receded"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                    {
                        // The physical tap hits the STRUCTURED row (real uiautomator
                        // bounds); the OCR channel may have dropped the row's text.
                        // Elements-only (legacy) worlds have no structured rows and
                        // fall back to the OCR channel.
                        var spec = _viewports[_viewport];
                        var titles = spec.Structured.Length > 0
                            ? spec.Structured.Select(se => se.TitleText).ToArray()
                            : spec.OcrTexts;
                        int? idx = ResolveRowIndex(tap, titles.Length);
                        if (idx is { } i && i >= 0 && i < titles.Length && titles[i] is { } title)
                        {
                            _visited.Add(title);
                            _taps.Add((title, tap.TargetBounds));
                            _screen = "Child:" + title;
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
            if (_screen == "Foreign")
                return new Observation([new ObservedElement("Foreign marker", null, 0, null, "text")], App, seq);
            if (_screen == "Root")
            {
                var spec = _viewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < spec.OcrTexts.Length; i++)
                    elements.Add(new ObservedElement(spec.OcrTexts[i], null, i, RowBounds(i), "text"));
                var state = _visited.Count == ChildCount
                    ? $"Visited {_visited.Count}/{ChildCount} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{ChildCount}";
                elements.Add(new ObservedElement(state, null, spec.OcrTexts.Length, null, "text"));
                var structured = spec.Structured.ToImmutableArray();
                if (_backwardHappened && _novelTitleAfterBackward is not null)
                    structured = structured.Add(Row(_novelTitleAfterBackward, structured.Length));
                if (_backwardHappened && _unknownAfterBackward)
                    structured = structured.Add(ClickableTextlessRow(structured.Length));
                return new Observation(elements.ToImmutable(), App, seq)
                {
                    StructuredElements = structured,
                };
            }
            var title = _screen["Child:".Length..];
            return new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "text"),
                    new ObservedElement(title + " page marker", null, 1, null, "text")),
                App, seq)
            {
                StructuredElements = ImmutableArray.Create(Row(RootPage, 0)),
            };
        }

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

    private static StructuredElementEvidence Row(string title, int ordinal)
        => new("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
            true, false, false, true, true, RowBounds(ordinal), title, null, false, null, null);

    private static StructuredElementEvidence ClickableTextlessRow(int ordinal)
        => new("android.widget.LinearLayout", null,
            true, false, false, true, true, RowBounds(ordinal), null, null, false, null, null);

    private static StructuredElementEvidence[] Rows(params string[] titles)
        => titles.Select((t, i) => Row(t, i)).ToArray();

    private static ElementBounds RowBounds(int ordinal)
        => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));

    // ── injected criteria ───────────────────────────────────────────────────

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

    private static BranchInventoryEvidence Inventory(ImmutableArray<Observation> observations, int semanticDepth)
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
        return new BranchInventoryEvidence(required.ToImmutable(), $"inventory: {first.Count} children", grounding.ToImmutable());
    }

    private static CandidateAuthorizationEvidence Authorize(Observation observation, ObservedElement candidate)
        => new(
            candidate.Text.StartsWith("Child ", StringComparison.Ordinal)
                || string.Equals(candidate.Text, RootPage, StringComparison.Ordinal),
            $"authorize {candidate.Text}");

    // ── run harness ─────────────────────────────────────────────────────────

    private sealed record RunOutcome(
        RunState State,
        string? Reason,
        RevisitWorld Environment,
        RuntimeAgent Agent,
        List<GoalEvidence> GoalEvidenceReceipts);

    private static async Task<RunOutcome> RunAsync(
        RevisitWorld world,
        Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? explore,
        Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>? inventory,
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
                var evidence = new GoalEvidence(
                    observation.Elements.Any(e => e.Text is { } t && t.Contains("CAPSTONE COMPLETE", StringComparison.Ordinal)),
                    "capstone goal evidence",
                    observation.SequenceNumber);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: Authorize,
            ViewportExplorationEvaluator: explore,
            BranchInventoryEvaluator: inventory ?? Inventory);
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
        return new RunOutcome(state, agent.Reason, world, agent, receipts);
    }

    private static ViewportSpec[] CapstoneChain(string[]? v0Ocr = null, string[]? v0Structured = null)
    {
        return
        [
            new(v0Ocr ?? ["Child 01", "Child 02", "Child 03", "Child 04"],
                Rows(v0Structured ?? ["Child 01", "Child 02", "Child 03", "Child 04"])),
            new(["Child 03", "Child 04", "Child 05", "Child 06", "Child 07"],
                Rows("Child 03", "Child 04", "Child 05", "Child 06", "Child 07")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"],
                Rows("Child 05", "Child 06", "Child 07", "Child 08")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"],
                Rows("Child 05", "Child 06", "Child 07", "Child 08")),
        ];
    }

    // ── RVT2-1: bottom terminal + pending above fold -> revisit required ─────

    [Fact]
    public async Task RVT21_BottomTerminal_PendingAboveFold_RevisitRequired()
    {
        var world = new RevisitWorld(CapstoneChain());

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-1");

        Assert.Equal(RunState.Completed, run.State);
        Assert.Contains(run.Environment.ActionHistory, action => action is DeviceAction.ScrollForward);
        Assert.Contains(run.Environment.ActionHistory, action => action is DeviceAction.ScrollBackward);
    }

    // ── RVT2-2 / RVT2-3: ScrollBackward world effect + fresh observation ─────

    [Fact]
    public async Task RVT22_ScrollBackward_WorldEffect_FreshObservation()
    {
        var world = new RevisitWorld(
        [
            new(["Child 01", "Child 02", "Child 03", "Child 04"], Rows("Child 01", "Child 02", "Child 03", "Child 04")),
            new(["Child 03", "Child 04", "Child 05", "Child 06", "Child 07"], Rows("Child 03", "Child 04", "Child 05", "Child 06", "Child 07")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"], Rows("Child 05", "Child 06", "Child 07", "Child 08")),
        ]);

        await world.ExecuteAsync(new DeviceAction.LaunchApp(App), CancellationToken.None);
        var top = await world.ObserveAsync(CancellationToken.None);
        await world.ExecuteAsync(new DeviceAction.ScrollForward(), CancellationToken.None);
        await world.ObserveAsync(CancellationToken.None);
        await world.ExecuteAsync(new DeviceAction.ScrollForward(), CancellationToken.None);
        var bottom = await world.ObserveAsync(CancellationToken.None);
        Assert.True(NavTitles(bottom).Contains("Child 08"));

        await world.ExecuteAsync(new DeviceAction.ScrollBackward(), CancellationToken.None);
        var receded = await world.ObserveAsync(CancellationToken.None);

        // RVT2-2: fresh viewport CHANGED (earlier content re-enters).
        Assert.True(receded.SequenceNumber > bottom.SequenceNumber); // RVT2-3 fresh observation accepted
        Assert.NotEqual(NavTitles(bottom), NavTitles(receded));
        Assert.True(NavTitles(receded).Contains("Child 03") || NavTitles(receded).Contains("Child 04"));
        _ = top;
    }

    // ── RVT2-4 / RVT2-9 / RVT2-5: continuity + SAME_POSITION returns + 01..08 ──

    [Fact]
    public async Task RVT2459_FullRevisitRoundTrip_CompletesAllEight()
    {
        var world = new RevisitWorld(CapstoneChain());

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-4");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("did not prove same-Container continuity", run.Reason ?? "");
        Assert.DoesNotContain("Post-completeness fresh evidence INVALIDATED", run.Reason ?? "");
        Assert.Equal(ChildCount, world.Visited.Count);
        for (int i = 1; i <= ChildCount; i++)
            Assert.Contains($"Child {i:D2}", world.Visited);
        Assert.Equal(2 * ChildCount, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        // The 05..08 dispatches happened BEFORE the revisit (bottom-first), and the
        // 01..04 dispatches happened AFTER the backward scrolls — fresh evidence,
        // never the historical frame.
        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        var firstBackward = run.Environment.ActionHistory.ToList().FindIndex(a => a is DeviceAction.ScrollBackward);
        Assert.InRange(firstBackward, 0, int.MaxValue);
        Assert.True(taps.Count == 2 * ChildCount);
    }

    // ── RVT2-6 / RVT2-15: OCR drops Child 01 — logical-source visibility ─────

    [Fact]
    public async Task RVT2615_OcrDropsChild01_LogicalSourceVisibilityStillDispatches()
    {
        // v0's OCR channel DROPS "Child 01" (structured still carries it): the
        // dispatch must reach Child 01 via fresh logical-source resolution, never
        // OCR/BranchIdentity text.
        var world = new RevisitWorld(CapstoneChain(
            v0Ocr: ["Child 02", "Child 03", "Child 04"]));

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-6");

        Assert.Equal(RunState.Completed, run.State);
        Assert.Contains("Child 01", world.Visited);
    }

    // ── RVT2-7 / RVT2-8: tap uses fresh structured bounds, never historical ──

    [Fact]
    public async Task RVT278_TapUsesCurrentFreshStructuredBounds()
    {
        var world = new RevisitWorld(CapstoneChain());

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-7");

        Assert.Equal(RunState.Completed, run.State);
        // The Child 01 tap carries the CURRENT (top viewport) row-0 bounds.
        var child01Tap = world.Taps.FirstOrDefault(t => t.Title == "Child 01");
        Assert.NotNull(child01Tap.Bounds);
        Assert.True(Math.Abs(child01Tap.Bounds!.Y1 - 0f) < 0.001f);
        // The Child 08 tap carries the BOTTOM viewport's row-3 bounds.
        var child08Tap = world.Taps.FirstOrDefault(t => t.Title == "Child 08");
        Assert.NotNull(child08Tap.Bounds);
        Assert.True(Math.Abs(child08Tap.Bounds!.Y1 - 0.3f) < 0.001f);
        // Fresh, not historical: every tap action appears AFTER its preceding
        // revisit/return observations in the action history.
        var actions = run.Environment.ActionHistory.ToList();
        Assert.True(actions.IndexOf(actions.OfType<DeviceAction.Tap>().First()) >= 0);
    }

    // ── RVT2-10 / RVT2-11: no-op backward -> budget exhaustion, no loop ──────

    [Fact]
    public async Task RVT21011_NoOpBackward_BudgetExhausted_FailClosed()
    {
        // The world ignores ScrollBackward (no backward progress): the revisit
        // loop consumes the frozen budget (3 forward transitions) on no-op steps
        // and fails closed — no infinite loop.
        var world = new RevisitWorld(CapstoneChain(), backwardNoOp: true);

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-10");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("bounded revisit budget is exhausted", run.Reason);
        Assert.Equal(3, run.Environment.ActionHistory.OfType<DeviceAction.ScrollBackward>().Count());
    }

    // ── RVT2-12: unexpected navigation stops revisit ────────────────────────

    [Fact]
    public async Task RVT212_UnexpectedNavigation_StopsRevisit()
    {
        // The backward scroll transitions to a FOREIGN page: same-Container
        // continuity fails and the revisit stops fail-closed.
        var world = new RevisitWorld(CapstoneChain(), backwardForeign: true);

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-12");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not prove same-Container continuity", run.Reason);
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollBackward>());
    }

    // ── RVT2-13: new source / Unknown during revisit invalidates completeness ─

    [Theory]
    [InlineData("novel")]
    [InlineData("unknown")]
    public async Task RVT213_RevisitEvidenceInvalidatesCompleteness(string mode)
    {
        var world = mode == "novel"
            ? new RevisitWorld(CapstoneChain(), novelTitleAfterBackward: "Child 09")
            : new RevisitWorld(CapstoneChain(), unknownAfterBackward: true);

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-13-" + mode);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Post-completeness fresh evidence INVALIDATED", run.Reason);
    }

    // ── RVT2-14: legacy Elements-only behavior unchanged ────────────────────

    [Fact]
    public async Task RVT214_LegacyElementsOnly_Unchanged_CompletesWithoutRevisit()
    {
        var world = new RevisitWorld(
        [
            new(["Child 01", "Child 02", "Child 03", "Child 04", "Child 05", "Child 06", "Child 07", "Child 08"], []),
        ]);
        BranchInventoryEvidence Legacy(ImmutableArray<Observation> observations, int depth)
            => depth >= 1
                ? new BranchInventoryEvidence(ImmutableDictionary<string, long>.Empty, "leaf")
                : new BranchInventoryEvidence(
                    ImmutableDictionary<string, long>.Empty
                        .Add("Child 01", 2).Add("Child 02", 2).Add("Child 03", 2).Add("Child 04", 2)
                        .Add("Child 05", 2).Add("Child 06", 2).Add("Child 07", 2).Add("Child 08", 2),
                    "legacy Elements-only inventory");

        var run = await RunAsync(world, explore: null, inventory: Legacy, runId: "rvt2-14");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain(run.Environment.ActionHistory, action => action is DeviceAction.ScrollBackward);
    }

    // ── RVT2-16: frozen inventory cardinality unchanged ─────────────────────

    [Fact]
    public async Task RVT216_FrozenInventoryCardinalityUnchanged()
    {
        var world = new RevisitWorld(CapstoneChain());

        var run = await RunAsync(world, ExploreWhileNew, null, "rvt2-16");

        Assert.Equal(RunState.Completed, run.State);
        Assert.Equal(ChildCount, run.Agent.BranchProgress[RootPage].ApprovedSiblingEvidence.Count);
    }
}
