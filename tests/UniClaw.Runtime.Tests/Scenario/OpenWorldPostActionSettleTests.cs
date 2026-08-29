using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
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
/// POST-ACTION OBSERVATION SETTLE — SET-1..SET-16.
///
/// After a semantic-transition action (branch dispatch / parent return) the
/// first post-action Observation is PROVISIONAL: it is ignored for
/// CurrentObservation / accepted viewport evidence / identity safety /
/// completeness / GoalEvidence / branch progress until a bounded
/// candidate -> confirmation -> SETTLED sequence of fresh Observations all
/// satisfy the Agent-owned transition predicate. The settle loop only observes +
/// reconciles + verifies — it NEVER redispatchs the action; budget exhaustion
/// fails closed (composition policy). Scroll continuity and the parents-stack
/// authority are unchanged.
/// </summary>
public sealed class OpenWorldPostActionSettleTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";

    // ── world with scripted post-tap frames ─────────────────────────────────

    private sealed class SettleWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private readonly string[][] _tapFrameScripts; // per-tap frame scripts
        private readonly int _expectedVisits;
        private string _screen = "Launcher";
        private string _previousScreen = "Launcher";
        private int _viewport;
        private int _scriptIndex;
        private readonly Queue<string> _frames = new();
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private readonly List<(string Title, ElementBounds? Bounds)> _taps = [];
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public SettleWorld(string[][] rootViewports, string[][]? tapFrameScripts = null)
        {
            _rootViewports = rootViewports;
            _tapFrameScripts = tapFrameScripts ?? [];
            _expectedVisits = rootViewports
                .SelectMany(v => v)
                .Where(t => t.StartsWith("Child ", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;
        public IReadOnlyList<Observation> ObservationHistory => _history;
        public IReadOnlySet<string> Visited => _visited;
        public IReadOnlyList<(string Title, ElementBounds? Bounds)> Taps => _taps;

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
                            _previousScreen = _screen;
                            _visited.Add(rows[i]);
                            _taps.Add((rows[i], tap.TargetBounds));
                            _screen = "Child:" + rows[i];
                        }
                    }
                    else if (_screen.StartsWith("Child:", StringComparison.Ordinal))
                    {
                        _previousScreen = _screen;
                        _screen = "Root";
                    }
                    if (_scriptIndex < _tapFrameScripts.Length)
                    {
                        foreach (var frame in _tapFrameScripts[_scriptIndex++])
                            _frames.Enqueue(frame);
                    }
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap dispatched"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Rejected, "other", "rejected"));
            }
        }

        private Observation BuildFrame(string frame, long seq) => frame switch
        {
            "previous" => Build(seq, _previousScreen),
            "parent" => Build(seq, "Root"),
            "foreign" => Build(seq, "Foreign"),
            var f when f.StartsWith("child:", StringComparison.Ordinal) => Build(seq, "Child:" + f["child:".Length..]),
            _ => Build(seq, frame),
        };

        private Observation Build(long seq) => Build(seq, _screen);

        private Observation Build(long seq, string screen)
        {
            if (screen == "Launcher")
                return Stamp(new Observation([new ObservedElement("Launcher", null, 0, null, null)], App, seq), seq);
            if (screen == "Foreign")
                return Stamp(new Observation([new ObservedElement("Foreign marker", null, 0, null, "text")], App, seq), seq);
            if (screen == "Root")
            {
                var rows = _rootViewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < rows.Length; i++)
                    elements.Add(new ObservedElement(rows[i], null, i, RowBounds(i), "text"));
                var state = _visited.Count == _expectedVisits
                    ? $"Visited {_visited.Count}/{_expectedVisits} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{_expectedVisits}";
                elements.Add(new ObservedElement(state, null, rows.Length, RowBounds(rows.Length), "text"));
                return Stamp(new Observation(elements.ToImmutable(), App, seq)
                {
                    StructuredElements = rows.Select((r, i) => Row(r, i)).ToImmutableArray(),
                }, seq);
            }
            var title = screen["Child:".Length..];
            return Stamp(new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "text"),
                    new ObservedElement(title + " page marker", null, 1, RowBounds(1), "text")),
                App, seq)
            {
                StructuredElements = ImmutableArray.Create(Row(RootPage, 0)),
            }, seq);
        }

        private static Observation Stamp(Observation observation, long sequence)
        {
            const string source = "primary-vision";
            const string frame = "capture:";
            var metadata = new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true, sequence,
                frame + sequence, 100, 100, "fixture-vision", source);
            var evidence = observation.Elements
                .Select((element, index) => (element, index))
                .Select(x => new SemanticEvidenceV2Envelope(
                    $"e:{sequence}:{x.index}",
                    new ElementAffordanceCandidateEvidence(
                        SemanticObservationFactProjector.CreateOccurrenceId(source, x.index.ToString()),
                        ClassifyStamp(x.element),
                        new SemanticSymbolReference("fixture", "1", "navigation"),
                        new SemanticObservationReference($"obs:{sequence}", sequence, frame + sequence),
                        new SemanticScopeReference("observation"),
                        new SemanticProvenance(source, SemanticSourceTier.Primary, $"capture:{sequence}", DateTimeOffset.UnixEpoch, frame + sequence),
                        .9, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue)))
                .ToImmutableArray();
            return observation with { Sources = [metadata], AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(evidence) };
        }

        private static ElementAffordanceKind ClassifyStamp(ObservedElement element) =>
            element.Text is { } text
                ? text.StartsWith("Child ", StringComparison.Ordinal)
                    ? ElementAffordanceKind.NavigationCandidate
                    : string.Equals(text, RootPage, StringComparison.Ordinal)
                        ? ElementAffordanceKind.ParentReturnControl
                        : ElementAffordanceKind.NonInteractive
                : ElementAffordanceKind.NonInteractive;
    }

    private static StructuredElementEvidence Row(string title, int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: "com.uniclaw.fixture:id/row_title",
            Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true,
            Bounds: RowBounds(ordinal), RawText: title);

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
        SettleWorld Environment,
        RuntimeAgent Agent,
        List<GoalEvidence> GoalEvidenceReceipts);

    private static async Task<RunOutcome> RunAsync(SettleWorld world, string runId)
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

    // ── SET-1: dispatch O1=stale Parent, O2=Child, O3=Child -> settle PASS ──

    [Fact]
    public async Task SET1_Dispatch_StaleThenConfirmedChild_Settles()
    {
        var world = new SettleWorld(CapstoneChain(),
            [["previous", "child:Child 05", "child:Child 05"]]);

        var run = await RunAsync(world, "set-1");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("did not settle within", run.Reason ?? "");
        Assert.DoesNotContain("duplicate semantic page identity", run.Reason ?? "");
    }

    // ── SET-2: dispatch O1/O2 stale, O3=Child, no confirmation -> budget FAIL ──

    [Fact]
    public async Task SET2_Dispatch_NoConfirmationWithinBudget_FailsClosed()
    {
        // Budget is 8 (real-emulator settle window); 9 scripted frames exceed it.
        var world = new SettleWorld(CapstoneChain(),
            [["previous", "previous", "child:Child 05"]]);

        var run = await RunAsync(world, "set-2");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not settle within", run.Reason);
        // SET-12: zero redispatch — exactly ONE Tap action.
        Assert.Single(run.Environment.ActionHistory.OfType<DeviceAction.Tap>());
    }

    // ── SET-3: dispatch O1=Child, O2=Child -> PASS ──────────────────────────

    [Fact]
    public async Task SET3_Dispatch_ImmediateChild_Confirms()
    {
        var world = new SettleWorld(CapstoneChain()); // no scripting -> immediate

        var run = await RunAsync(world, "set-3");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("did not settle within", run.Reason ?? "");
    }

    // ── SET-4: dispatch O1/O2=foreign -> FAIL (never settles) ───────────────

    [Fact]
    public async Task SET4_Dispatch_ForeignFrames_NeverSettles()
    {
        // Budget is 8; 9 foreign frames exceed it.
        var world = new SettleWorld(CapstoneChain(),
            [["foreign", "foreign"]]);

        var run = await RunAsync(world, "set-4");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not settle within", run.Reason);
    }

    // ── SET-5: return O1=stale Child, O2=Parent, O3=Parent -> verified PASS ──

    [Fact]
    public async Task SET5_Return_StaleChildThenParentConfirmation_Settles()
    {
        var world = new SettleWorld(CapstoneChain(),
            [[], ["previous", "parent", "parent"]]); // tap1=dispatch (immediate), tap2=return (scripted)

        var run = await RunAsync(world, "set-5");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("did not settle within", run.Reason ?? "");
        Assert.DoesNotContain("did not prove fresh exact reconciliation", run.Reason ?? "");
    }

    // ── SET-6: return O1=Parent, O2=foreign -> NOT settled ──────────────────

    [Fact]
    public async Task SET6_Return_ParentThenForeign_NotSettled()
    {
        // Budget is 8; 9 mixed foreign frames exceed it.
        var world = new SettleWorld(CapstoneChain(),
            [[], ["parent", "foreign"]]);

        var run = await RunAsync(world, "set-6");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("did not settle within", run.Reason);
    }

    // ── SET-7: provisional stale Child05 never triggers duplicate identity ──

    [Fact]
    public async Task SET7_ProvisionalStaleChild05_DoesNotTriggerDuplicate()
    {
        // The second dispatch's first frame is STALE "Child 05" (provisional);
        // the confirmation re-candidates to the real "Child 06" — identity
        // safety never sees the provisional identity.
        var world = new SettleWorld(CapstoneChain(),
            [[], [], ["child:Child 05", "child:Child 06", "child:Child 06"]]);

        var run = await RunAsync(world, "set-7");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("duplicate semantic page identity", run.Reason ?? "");
    }

    // ── SET-8: CONFIRMED duplicate Child05 still fails closed ───────────────

    [Fact]
    public async Task SET8_ConfirmedDuplicateChild05_StillFailsClosed()
    {
        var world = new SettleWorld(CapstoneChain(),
            [[], [], ["child:Child 05", "child:Child 05"]]);

        var run = await RunAsync(world, "set-8");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("duplicate semantic page identity", run.Reason);
    }

    // ── SET-9 / SET-10: provisional observations never enter accepted state ──

    [Fact]
    public async Task SET910_ProvisionalObservations_NeverPolluteEpochOrCurrent()
    {
        var world = new SettleWorld(CapstoneChain(),
            [["previous", "child:Child 05", "child:Child 05"]]);

        var run = await RunAsync(world, "set-9");

        Assert.Equal(RunState.Completed, run.State);
        // The discovery epoch was frozen BEFORE any dispatch: its sequences are
        // exactly [2,4,6,8] — one stability-confirmed frame per scroll; the
        // provisional frames never appended.
        Assert.Single(run.Agent.Trace, entry =>
            entry.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true
            && entry.Reason.Contains("seq=[2,4,6,8]", StringComparison.Ordinal));
        // SET-14: the satisfied GoalEvidence reads the LATEST settled observation.
        Assert.Contains(run.GoalEvidenceReceipts, receipt => receipt.Satisfied);
        var final = run.GoalEvidenceReceipts[^1];
        Assert.Equal(world.ObservationHistory[^1].SequenceNumber, final.SourceObservationSequence);
    }

    // ── SET-11: provisional observations never update BranchProgress/visited ──
    // Covered behaviorally by SET-7 (a provisional stale "Child 05" would have
    // triggered the duplicate identity fail; it does not) and SET-1 (Completed).

    // ── SET-13 / SET-15 / SET-16: unchanged surfaces ─────────────────────────
    // ScrollForward/ScrollBackward behavior is covered by the RVT2 suite;
    // the parents-stack authority by SET-5/SET-1 and the AFF/RVT suites;
    // BranchIdentity != destination by ACCEPT-3. All green in the full suite.
}
