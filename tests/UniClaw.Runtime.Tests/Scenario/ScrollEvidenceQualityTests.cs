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
/// SETTINGS_SCROLL_EVIDENCE_QUALITY — SQ-1..SQ-12.
///
/// Bounded post-scroll evidence-quality settle: after ONE scroll dispatch
/// (ScrollForward / ScrollBackward) the immediate post-action Observation is
/// accepted only when every interaction-relevant structured element carries
/// valid non-empty bounds. A malformed mid-fling capture (interactive element
/// with invalid/empty bounds) is PROVISIONAL — bounded re-observe (composition
/// policy MaxPostScrollEvidenceObservations=3; one scroll -> N observations;
/// zero scroll redispatch); the provisional never updates CurrentObservation,
/// never enters normalization/exhaustion/epoch. Valid-bounds textless rows are
/// NOT treated as transient — they remain genuine UNKNOWN (fail closed). The
/// semantic-transition settle (branch/parent-return) is untouched.
/// </summary>
public sealed class ScrollEvidenceQualityTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private const int ChildCount = 8;

    private sealed class ScrollQualityWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private readonly string[][] _scrollFrameScripts; // frames emitted after each scroll
        private readonly int _expectedVisits;
        private string _screen = "Launcher";
        private int _viewport;
        private int _scriptIndex;
        private readonly Queue<string> _frames = new();
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public ScrollQualityWorld(string[][] rootViewports, string[][]? scrollFrameScripts = null)
        {
            _rootViewports = rootViewports;
            _scrollFrameScripts = scrollFrameScripts ?? [];
            _expectedVisits = rootViewports
                .SelectMany(v => v)
                .Where(t => t.StartsWith("Child ", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
        public IReadOnlyList<Observation> ObservationHistory => _history;
        public IReadOnlySet<string> Visited => _visited;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = _frames.Count > 0
                ? BuildFrame(_frames.Dequeue(), ++_seq)
                : Build(++_seq);
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
                    EnqueueScript();
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport advanced"));
                case DeviceAction.ScrollBackward:
                    if (_screen == "Root" && _viewport > 0)
                        _viewport--;
                    EnqueueScript();
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport receded"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                    {
                        var rows = _rootViewports[_viewport];
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

        private void EnqueueScript()
        {
            if (_scriptIndex < _scrollFrameScripts.Length)
            {
                foreach (var frame in _scrollFrameScripts[_scriptIndex++])
                    _frames.Enqueue(frame);
            }
        }

        private Observation BuildFrame(string frame, long seq) => frame switch
        {
            "clean" => Build(seq, _screen),
            "malformed" => BuildMalformed(seq),
            "textless" => BuildTextless(seq),
            _ => Build(seq, _screen),
        };

        private Observation Build(long seq) => Build(seq, _screen);

        private Observation Build(long seq, string screen)
        {
            if (screen == "Launcher")
                return new Observation([new ObservedElement("Launcher", null, 0, null, null)], App, seq);
            if (screen == "Root")
            {
                var rows = _rootViewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < rows.Length; i++)
                    elements.Add(new ObservedElement(rows[i], null, i, RowBounds(i), "text"));
                var state = _visited.Count == _expectedVisits
                    ? $"Visited {_visited.Count}/{_expectedVisits} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{_expectedVisits}";
                elements.Add(new ObservedElement(state, null, rows.Length, null, "text"));
                return new Observation(elements.ToImmutable(), App, seq)
                {
                    StructuredElements = rows.Select((r, i) => Row(r, i)).ToImmutableArray(),
                };
            }
            var title = screen["Child:".Length..];
            return new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "text"),
                    new ObservedElement(title + " page marker", null, 1, null, "text")),
                App, seq)
            {
                StructuredElements = ImmutableArray.Create(Row(RootPage, 0)),
            };
        }

        /// <summary>Malformed mid-fling capture: a clickable row with INVALID/EMPTY bounds.</summary>
        private Observation BuildMalformed(long seq)
        {
            var rows = _rootViewports[_viewport];
            var elements = rows.Select((r, i) => new ObservedElement(r, null, i, null, "text")).ToImmutableArray();
            // Explicitly NULL bounds (do NOT fall back to the default row bounds).
            var structured = rows.Select((r, i) => new StructuredElementEvidence(
                "android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
                true, false, false, true, true, null, r, null, false, null, null)).ToImmutableArray();
            var state = new ObservedElement($"Visited {_visited.Count}/{_expectedVisits}", null, rows.Length, null, "text");
            return new Observation(elements.Add(state), App, seq) { StructuredElements = structured };
        }

        /// <summary>Genuine textless row: VALID bounds, no title/rid/cd -> real UNKNOWN (never hidden).</summary>
        private Observation BuildTextless(long seq)
        {
            var rows = _rootViewports[_viewport];
            var elements = ImmutableArray.CreateBuilder<ObservedElement>();
            for (int i = 0; i < rows.Length; i++)
                elements.Add(new ObservedElement(rows[i], null, i, RowBounds(i), "text"));
            elements.Add(new ObservedElement($"Visited {_visited.Count}/{_expectedVisits}", null, rows.Length, null, "text"));
            var structured = rows.Select((r, i) => Row(r, i)).ToImmutableArray();
            structured = structured.Add(TextlessClickableRow(structured.Length)); // valid bounds, no label
            return new Observation(elements.ToImmutable(), App, seq) { StructuredElements = structured };
        }
    }

    private static StructuredElementEvidence Row(string title, int ordinal, ElementBounds? bounds = null)
        => new("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title",
            true, false, false, true, true, bounds ?? RowBounds(ordinal), title, null, false, null, null);

    private static StructuredElementEvidence TextlessClickableRow(int ordinal)
        => new("android.widget.LinearLayout", null,
            true, false, false, true, true, RowBounds(ordinal), null, null, false, null, null);

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

    // ── injected criteria (capstone-style completing; the quality settle is the unit under test) ──

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

    private sealed record RunOutcome(RunState State, string? Reason, ScrollQualityWorld Environment, RuntimeAgent Agent);

    private static async Task<RunOutcome> RunAsync(ScrollQualityWorld world, string runId)
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
        var goal = new Goal(
            observation => new GoalEvidence(
                observation.Elements.Any(e => e.Text is { } t && t.Contains("CAPSTONE COMPLETE", StringComparison.Ordinal)),
                "capstone goal evidence",
                observation.SequenceNumber),
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
        return new RunOutcome(state, agent.Reason, world, agent);
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

    // ── SQ-1: O1 malformed (provisional) -> O2 clean accepted ───────────────

    [Fact]
    public async Task SQ1_MalformedProvisional_CleanAccepted()
    {
        var world = new ScrollQualityWorld(CapstoneChain(), [["malformed", "clean"]]);

        var run = await RunAsync(world, "sq-1");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("Unknown interaction affordances remain", run.Reason ?? "");
        // SQ-7/8/9: the provisional malformed frame never entered the accepted
        // evidence — the discovery epoch froze with exactly the 8 chain sources.
        Assert.Single(run.Agent.Trace, entry =>
            entry.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true
            && entry.Reason.Contains("sources=8", StringComparison.Ordinal)
            && entry.Reason.Contains("unresolved=0", StringComparison.Ordinal));
    }

    // ── SQ-2: O1/O2 malformed -> O3 clean accepted (within budget) ──────────

    [Fact]
    public async Task SQ2_TwoMalformedThenClean_AcceptedWithinBudget()
    {
        var world = new ScrollQualityWorld(CapstoneChain(), [["malformed", "malformed", "clean"]]);

        var run = await RunAsync(world, "sq-2");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("Unknown interaction affordances remain", run.Reason ?? "");
    }

    // ── SQ-3: all observations malformed -> budget exhausted, zero redispatch ──

    [Fact]
    public async Task SQ3_BudgetExhausted_FailClosed_ZeroRedispatch()
    {
        var world = new ScrollQualityWorld(CapstoneChain(), [["malformed", "malformed", "malformed"]]);

        var run = await RunAsync(world, "sq-3");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not prove positive exhaustion", run.Reason);
        // ONE scroll dispatch only — no scroll redispatch loop.
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.ScrollForward>());
    }

    // ── SQ-4: valid-bounds textless row -> genuine UNKNOWN, NOT hidden ───────

    [Fact]
    public async Task SQ4_ValidBoundsTextlessRow_GenuineUnknown_NotHidden()
    {
        var world = new ScrollQualityWorld(CapstoneChain(), [["textless", "textless", "textless"]]);

        var run = await RunAsync(world, "sq-4");

        // The quality settle must NOT hide a valid-bounds textless interactive
        // row: it stays a genuine UNKNOWN and blocks completeness.
        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason);
    }

    // ── SQ-10: ScrollBackward uses the same bounded evidence-quality handling ──

    [Fact]
    public async Task SQ10_ScrollBackward_MalformedProvisional_CleanAccepted()
    {
        // The exploration is clean; the FIRST backward revisit observation is
        // malformed (provisional) then clean -> the revisit continues.
        var world = new ScrollQualityWorld(
            CapstoneChain(),
            [[], [], [], ["malformed", "clean"], ["malformed", "clean"]]);

        var run = await RunAsync(world, "sq-10");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("Unknown interaction affordances remain", run.Reason ?? "");
        Assert.Contains(run.Environment.ActionHistory, action => action is DeviceAction.ScrollBackward);
    }

    // ── SQ-5 / SQ-6 / SQ-11 / SQ-12: covered by the full suite ──────────────
    // (Search LOCAL_CONTROL, volatile SummaryText stability, semantic-transition
    // settle unchanged, and COMPOSE-05/SIG/SEARCH/PROV/NM/RVT/AFF/SET green.)
}
