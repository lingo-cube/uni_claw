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
/// CAPSTONE_CHILD_AFFORDANCE — AFF-1..AFF-14.
///
/// The InteractionAffordanceAnalyzer stays context-free (a Button remains
/// UNKNOWN in isolation). In a CHILD traversal context the Agent may
/// contextually resolve the UNIQUE interactive element whose action label
/// (TitleText) matches the known parent-return intent as PARENT_RETURN_CONTROL:
/// it is excluded from the child navigation inventory, does not block child
/// completeness, and the return is dispatched with FRESH structured bounds and
/// verified ONLY by fresh post-action evidence reconciling to the expected
/// parent. Missing / ambiguous / unknown-destination / unauthorized labels fail
/// closed; ordinary UNKNOWNs keep blocking completeness.
/// </summary>
public sealed class OpenWorldChildAffordanceTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private const int ChildCount = 8;

    // ── world (per-viewport rows + child return control kind) ────────────────

    public enum ChildReturnKind
    {
        Row,      // LinearLayout "Fixture Root" (NavigationCandidate — legacy)
        Button,   // Button "Fixture Root" (Unknown — contextual resolution)
        Submit,   // Button "Submit" (label mismatch -> fail closed)
        Two,      // two Buttons "Fixture Root" (ambiguous -> fail closed)
        Empty,    // Button with no title (fail closed)
        Other,    // Button "Other Page" (unknown destination -> fail closed)
        NoEffect, // return tap has no world effect (receipt alone insufficient)
        Foreign,  // return tap navigates to a foreign page
    }

    private sealed class AffordanceWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private readonly ChildReturnKind _childReturn;
        private readonly bool _rootHasReturnButton;
        private string _screen = "Launcher";
        private int _viewport;
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private readonly List<(string Title, ElementBounds? Bounds)> _taps = [];
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public AffordanceWorld(
            string[][] rootViewports,
            ChildReturnKind childReturn = ChildReturnKind.Row,
            bool rootHasReturnButton = false)
        {
            _rootViewports = rootViewports;
            _childReturn = childReturn;
            _rootHasReturnButton = rootHasReturnButton;
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
                        int? idx = ResolveRowIndex(tap, rows.Length);
                        if (idx is { } i && i >= 0 && i < rows.Length)
                        {
                            _visited.Add(rows[i]);
                            _taps.Add((rows[i], tap.TargetBounds));
                            _screen = "Child:" + rows[i];
                        }
                    }
                    else if (_screen.StartsWith("Child:", StringComparison.Ordinal))
                    {
                        _taps.Add((RootPage, tap.TargetBounds)); // the return tap (fresh bounds)
                        if (_childReturn is ChildReturnKind.NoEffect)
                        {
                            // tap dispatched but the world does NOT leave the child page
                        }
                        else if (_childReturn is ChildReturnKind.Foreign)
                        {
                            _screen = "Foreign";
                        }
                        else
                        {
                            _screen = "Root";
                        }
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
                var rows = _rootViewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < rows.Length; i++)
                    elements.Add(new ObservedElement(rows[i], null, i, RowBounds(i), "text"));
                var state = _visited.Count == ChildCount
                    ? $"Visited {_visited.Count}/{ChildCount} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{ChildCount}";
                elements.Add(new ObservedElement(state, null, rows.Length, null, "text"));
                var structured = rows.Select((r, i) => Row(r, i)).ToImmutableArray();
                if (_rootHasReturnButton)
                    structured = structured.Add(Button("Fixture Root", structured.Length));
                return new Observation(elements.ToImmutable(), App, seq)
                {
                    StructuredElements = structured,
                };
            }
            var title = _screen["Child:".Length..];
            var childStructured = _childReturn switch
            {
                ChildReturnKind.Row => ImmutableArray.Create(Row(RootPage, 0)),
                ChildReturnKind.Button => ImmutableArray.Create(Button(RootPage, 0)),
                ChildReturnKind.Submit => ImmutableArray.Create(Button("Submit", 0)),
                ChildReturnKind.Two => ImmutableArray.Create(Button(RootPage, 0), Button(RootPage, 1)),
                ChildReturnKind.Empty => ImmutableArray.Create(new StructuredElementEvidence(
                    "android.widget.Button", null, true, false, false, true, true, RowBounds(0),
                    null, null, false, null, null)),
                ChildReturnKind.Other => ImmutableArray.Create(Button("Other Page", 0)),
                _ => ImmutableArray.Create(Button(RootPage, 0)),
            };
            return new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "text"),
                    new ObservedElement(title + " page marker", null, 1, null, "text")),
                App, seq)
            {
                StructuredElements = childStructured,
            };
        }
    }

    private static StructuredElementEvidence Row(string title, int ordinal)
        => new("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
            true, false, false, true, true, RowBounds(ordinal), title, null, false, null, null);

    private static StructuredElementEvidence Button(string title, int ordinal)
        => new("android.widget.Button", "com.uniclaw.fixture:id/return_button",
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
        AffordanceWorld Environment,
        RuntimeAgent Agent,
        List<GoalEvidence> GoalEvidenceReceipts);

    private static async Task<RunOutcome> RunAsync(AffordanceWorld world, string runId)
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
            ViewportExplorationEvaluator: ExploreWhileNew,
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
        return new RunOutcome(state, agent.Reason, world, agent, receipts);
    }

    private static string[][] CapstoneChain()
    {
        return
        [
            ["Child 01", "Child 02", "Child 03", "Child 04"],
            ["Child 03", "Child 04", "Child 05", "Child 06", "Child 07"],
            ["Child 05", "Child 06", "Child 07", "Child 08"],
            ["Child 05", "Child 06", "Child 07", "Child 08"],
        ];
    }

    // ── AFF-1 / AFF-7 / AFF-9 / AFF-11: Button return control resolved ──────

    [Fact]
    public async Task AFF1711_ButtonReturnControl_Resolved_CompletesRoundTrip()
    {
        var world = new AffordanceWorld(CapstoneChain(), childReturn: ChildReturnKind.Button);

        var run = await RunAsync(world, "aff-1");

        Assert.Equal(RunState.Completed, run.State);
        Assert.Equal(ChildCount, world.Visited.Count);
        Assert.Equal(2 * ChildCount, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        // AFF-7: resolved return controls never became child-inventory sources.
        Assert.True(run.Agent.BranchProgress[RootPage].IsSubtreeComplete);
        Assert.Equal(ChildCount, run.Agent.BranchProgress[RootPage].CompletedSiblingEvidence.Count);
        // AFF-9: the return taps used the fresh structured Button bounds (row 0).
        Assert.All(world.Taps, tap => Assert.NotNull(tap.Bounds));
        Assert.Contains(world.Taps, tap => tap.Title == RootPage && Math.Abs(tap.Bounds!.Y1 - 0f) < 0.001f);
        // AFF-11: fresh post-action evidence proved the expected parent.
        Assert.DoesNotContain("did not prove fresh exact reconciliation", run.Reason ?? "");
        Assert.Contains(run.GoalEvidenceReceipts, receipt => receipt.Satisfied);
    }

    // ── AFF-2: no known parent -> UNKNOWN / fail closed ─────────────────────

    [Fact]
    public async Task AFF2_NoKnownParent_ButtonUnknown_BlocksCompleteness()
    {
        // A "Fixture Root" Button on the ROOT page: no known parent context -> the
        // interactive UNKNOWN stays unresolved -> root completeness is blocked.
        var world = new AffordanceWorld(CapstoneChain(), rootHasReturnButton: true);

        var run = await RunAsync(world, "aff-2");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason);
    }

    // ── AFF-3 / AFF-6: label mismatch / unknown destination -> fail closed ──

    [Theory]
    [InlineData(ChildReturnKind.Submit)]
    [InlineData(ChildReturnKind.Other)]
    public async Task AFF36_LabelMismatch_UnknownDestination_FailClosed(ChildReturnKind kind)
    {
        var world = new AffordanceWorld(CapstoneChain(), childReturn: kind);

        var run = await RunAsync(world, "aff-3-" + kind);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason);
    }

    // ── AFF-4: two matching Buttons -> ambiguous -> fail closed ─────────────

    [Fact]
    public async Task AFF4_AmbiguousParentReturnCandidates_FailClosed()
    {
        var world = new AffordanceWorld(CapstoneChain(), childReturn: ChildReturnKind.Two);

        var run = await RunAsync(world, "aff-4");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason);
    }

    // ── AFF-5: empty title -> fail closed ───────────────────────────────────

    [Fact]
    public async Task AFF5_EmptyLabel_FailClosed()
    {
        var world = new AffordanceWorld(CapstoneChain(), childReturn: ChildReturnKind.Empty);

        var run = await RunAsync(world, "aff-5");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason);
    }

    // ── AFF-8: unresolved interactive UNKNOWN still blocks completeness ─────
    // Covered by AFF-2/3/5 (each asserts "Unknown interaction affordances remain").

    // ── AFF-10: Tap receipt alone is not return truth ───────────────────────

    [Fact]
    public async Task AFF10_TapReceiptAlone_NotEnough_ReturnFailsClosed()
    {
        // The return tap dispatches but the world does NOT leave the child page:
        // fresh post-action evidence still shows the child -> return verification fails.
        var world = new AffordanceWorld(CapstoneChain(), childReturn: ChildReturnKind.NoEffect);

        var run = await RunAsync(world, "aff-10");

        // The return never settles (no fresh observation reconciles to the
        // expected parent within the bounded policy) -> fail closed, zero redispatch.
        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not settle within", run.Reason);
    }

    // ── AFF-12: fresh evidence proves a foreign page -> FAIL ────────────────

    [Fact]
    public async Task AFF12_FreshEvidenceProvesForeignPage_Fails()
    {
        var world = new AffordanceWorld(CapstoneChain(), childReturn: ChildReturnKind.Foreign);

        var run = await RunAsync(world, "aff-12");

        // The return never settles (no fresh observation reconciles to the
        // expected parent within the bounded policy) -> fail closed, zero redispatch.
        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not settle within", run.Reason);
    }

    // ── AFF-13: analyzer unchanged — Button is UNKNOWN context-free ─────────

    [Fact]
    public void AFF13_AnalyzerUnchanged_ButtonIsUnknownContextFree()
    {
        var observation = new Observation([], App, 1)
        {
            StructuredElements = ImmutableArray.Create(Button("Fixture Root", 0)),
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);

        Assert.Single(affordances);
        Assert.Equal(InteractionAffordanceKind.Unknown, affordances[0].Classification);
    }

    // ── AFF-14: root inventory / provenance / revisit unchanged ─────────────
    // Covered by the full deterministic suite (RVT2 + provenance + NM + CURRENT).

    [Fact]
    public async Task AFF14_RootProvenanceRevisit_Unchanged()
    {
        // The Row-style return control (NavigationCandidate) still completes the
        // round trip — legacy children are unaffected by the contextual resolution.
        var world = new AffordanceWorld(CapstoneChain(), childReturn: ChildReturnKind.Row);

        var run = await RunAsync(world, "aff-14");

        Assert.Equal(RunState.Completed, run.State);
        Assert.Equal(ChildCount, world.Visited.Count);
    }
}
