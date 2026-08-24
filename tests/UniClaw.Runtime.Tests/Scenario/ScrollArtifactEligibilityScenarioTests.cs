using System.Collections.Immutable;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.Adapters.Device;
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
/// SETTINGS_SCROLL_ARTIFACT_ELIGIBILITY — ART-11..ART-13 (full pipeline).
///
/// Through the REAL admission boundary (AdbUiHierarchySource.Parse), the
/// persistent recycled-container artifacts (clickable, negative-height bounds)
/// are excluded as NON_ACTIONABLE_STRUCTURAL_ARTIFACTS. Therefore the
/// post-scroll evidence-quality settle converges even when every capture
/// carries the artifacts (ART-11), a valid-bounds textless clickable row is
/// still admitted and blocks completeness as a genuine UNKNOWN (ART-12), and
/// the ScrollBackward bounded-revisit path uses identical eligibility
/// (ART-13). The settle mechanism and its budget are untouched.
/// ART-1..ART-10/13 (admission / analyzer / normalizer level) are covered in
/// Unit.ViewportInteractionEligibilityTests; ART-14 is the full suite.
/// </summary>
public sealed class ScrollArtifactEligibilityScenarioTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";
    private const int ChildCount = 8;
    private const int Width = 1080;
    private const int Height = 1920;

    // ── raw XML builders (fixture-style rows + Settings-style artifacts) ─────

    private static string TextView(string text, string resourceId, string bounds)
        => $"<node index=\"0\" text=\"{text}\" resource-id=\"{resourceId}\" class=\"android.widget.TextView\" "
           + $"package=\"{App}\" content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"false\" "
           + $"enabled=\"true\" focusable=\"false\" focused=\"false\" scrollable=\"false\" long-clickable=\"false\" "
           + $"password=\"false\" selected=\"false\" bounds=\"{bounds}\"/>";

    private static string RowXml(string title, int row)
    {
        int y1 = 300 + row * 231;
        int y2 = y1 + 231;
        var bounds = $"[0,{y1}][{Width},{y2}]";
        return $"<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
               + $"package=\"{App}\" content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"true\" "
               + $"enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" long-clickable=\"false\" "
               + $"password=\"false\" selected=\"false\" bounds=\"{bounds}\">"
               + TextView(title, "android:id/title", bounds)
               + "</node>";
    }

    /// <summary>Persistent recycled-container artifact: clickable, negative-height bounds, stale title descendant.</summary>
    private static string ArtifactXml(string staleTitle)
        => $"<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
           + $"package=\"{App}\" content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"true\" "
           + $"enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" long-clickable=\"false\" "
           + $"password=\"false\" selected=\"false\" bounds=\"[0,284][{Width},203]\">"
           + TextView(staleTitle, "android:id/title", "[0,284][1080,203]")
           + "</node>";

    /// <summary>Valid-bounds textless clickable row: genuine UNKNOWN (never hidden).</summary>
    private static string TextlessRowXml(int row)
    {
        int y1 = 300 + row * 231;
        int y2 = y1 + 231;
        return $"<node index=\"0\" text=\"\" resource-id=\"\" class=\"android.widget.LinearLayout\" "
               + $"package=\"{App}\" content-desc=\"\" checkable=\"false\" checked=\"false\" clickable=\"true\" "
               + $"enabled=\"true\" focusable=\"true\" focused=\"false\" scrollable=\"false\" long-clickable=\"false\" "
               + $"password=\"false\" selected=\"false\" bounds=\"[0,{y1}][{Width},{y2}]\"/>";
    }

    // ── Parse-based world ────────────────────────────────────────────────────

    private sealed class ParseArtifactWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private readonly string[][] _scrollFrameScripts;
        private readonly int _expectedVisits;
        private string _screen = "Launcher";
        private int _viewport;
        private int _scriptIndex;
        private readonly Queue<string> _frames = new();
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];
        public List<string> RawXmls { get; } = new();

        public ParseArtifactWorld(string[][] rootViewports, string[][]? scrollFrameScripts = null)
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
        public int ObservationCount => _history.Count;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = _frames.Count > 0 ? _frames.Dequeue() : "clean";
            var xml = BuildXml(frame);
            RawXmls.Add(xml);
            var structured = AdbUiHierarchySource.Parse(xml, Width, Height);
            var observation = BuildObservation(structured);
            _history.Add(observation);
            return Task.FromResult(observation);
        }

        private string BuildXml(string frame)
        {
            if (_screen == "Launcher")
                return "<hierarchy rotation=\"0\"></hierarchy>";
            if (_screen != "Root")
                return "<hierarchy rotation=\"0\">" + RowXml(RootPage, 0) + "</hierarchy>";
            var rows = _rootViewports[_viewport];
            var body = string.Join("", rows.Select((title, i) => RowXml(title, i)));
            switch (frame)
            {
                case "mixed":
                    // The REAL pattern: valid rows + persistent artifacts coexist.
                    return "<hierarchy rotation=\"0\">" + ArtifactXml("Child 01") + ArtifactXml("Child 08") + body + "</hierarchy>";
                case "artifact-only":
                    return "<hierarchy rotation=\"0\">" + ArtifactXml("Child 01") + ArtifactXml("Child 08") + "</hierarchy>";
                case "textless":
                    return "<hierarchy rotation=\"0\">" + body + TextlessRowXml(rows.Length) + "</hierarchy>";
                default:
                    return "<hierarchy rotation=\"0\">" + body + "</hierarchy>";
            }
        }

        private Observation BuildObservation(ImmutableArray<StructuredElementEvidence> structured)
        {
            if (_screen == "Launcher")
                return new Observation([new ObservedElement("Launcher", null, 0, null, null)], App, ++_seq);
            if (_screen == "Root")
            {
                var rows = _rootViewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < rows.Length; i++)
                    elements.Add(new ObservedElement(rows[i], null, i, RowBounds(i), "text"));
                var state = _visited.Count == _expectedVisits
                    ? $"Visited {_visited.Count}/{_expectedVisits} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{_expectedVisits}";
                elements.Add(new ObservedElement(state, null, rows.Length, RowBounds(rows.Length), "text"));
                // Mirror any textless interactive surface as a PRIMARY Vision
                // occurrence (eligible UNKNOWN that blocks completeness).
                for (int i = 0; i < structured.Length; i++)
                {
                    if (structured[i].RawText is null && structured[i].Clickable == true
                        && !elements.Any(e => e.Index == rows.Length + 1 + i))
                    {
                        elements.Add(new ObservedElement("", null, rows.Length + 1 + i, structured[i].Bounds ?? RowBounds(rows.Length + 1 + i), "text"));
                    }
                }
                return new Observation(elements.ToImmutable(), App, ++_seq) { StructuredElements = structured };
            }
            var title = _screen["Child:".Length..];
            return new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "text"),
                    new ObservedElement(title + " page marker", null, 1, RowBounds(1), "text")),
                App, ++_seq)
            {
                StructuredElements = structured,
            };
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

        private static ElementBounds RowBounds(int ordinal)
            => new(0, (300f + ordinal * 231f) / Height, 1, (300f + (ordinal + 1) * 231f) / Height);

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

    // ── injected criteria (capstone-style completing; eligibility is the unit under test) ──

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

    private sealed record RunOutcome(RunState State, string? Reason, ParseArtifactWorld Environment);

    private static async Task<RunOutcome> RunAsync(ParseArtifactWorld world, string runId)
    {
        var environment = new SemanticCapabilityTestEnvironment(world, element => element.Text switch
        {
            var text when string.Equals(text, RootPage, StringComparison.Ordinal) => FixtureSemanticRole.ParentReturnControl,
            var text when text is not null && text.StartsWith("Child ", StringComparison.Ordinal) => FixtureSemanticRole.NavigationCandidate,
            var text when string.IsNullOrWhiteSpace(text) => null, // textless surface -> eligible UNKNOWN (fail closed)
            _ => FixtureSemanticRole.NonInteractive,
        });
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, App, Resolve, launchIntentAction: "com.uniclaw.fixture.action.CAPSTONE");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
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
        return new RunOutcome(state, agent.Reason, world);
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

    // ── ART-11: settle converges when only persistent invalid-bound artifacts remain ──

    [Fact]
    public async Task ART11_PersistentArtifacts_SettleConverges_AndCompletes()
    {
        // EVERY post-scroll capture carries the persistent artifacts alongside
        // the valid rows (the real Settings pattern). The eligibility admission
        // excludes the artifacts, so the settle converges on the first capture
        // (no provisional re-observes) and the run completes.
        var mixed = new ParseArtifactWorld(CapstoneChain(), [["mixed"], ["mixed"], ["mixed"], ["mixed"]]);
        var clean = new ParseArtifactWorld(CapstoneChain(), [["clean"], ["clean"], ["clean"], ["clean"]]);

        var mixedRun = await RunAsync(mixed, "art-11-mixed");
        var cleanRun = await RunAsync(clean, "art-11-clean");

        Assert.Equal(RunState.Completed, mixedRun.State);
        Assert.Equal(RunState.Completed, cleanRun.State);
        // The settle produced NO extra re-observations: identical observation
        // counts with and without artifacts.
        Assert.Equal(cleanRun.Environment.ObservationCount, mixedRun.Environment.ObservationCount);
        // The raw captures DID contain artifacts (they were present but
        // excluded), and every admitted element has valid bounds.
        Assert.Contains(mixedRun.Environment.RawXmls, xml => xml.Contains("clickable=\"true\"") && xml.Contains("[0,284][1080,203]"));
        Assert.All(mixedRun.Environment.ObservationHistory.SelectMany(o => o.StructuredElements),
            e => Assert.True(e.Bounds is { IsValid: true }));
    }

    // ── ART-12: genuinely actionable UNKNOWN evidence still fails closed ─────

    [Fact]
    public async Task ART12_ValidBoundsTextlessClickable_FailsClosed()
    {
        // A VALID-bounds textless clickable row is admitted (eligibility never
        // consults text) and remains a genuine UNKNOWN that blocks completeness.
        // Each scroll enqueues TWO textless frames — one for the post-scroll
        // observation and one for the stability-confirmed frame — so the
        // genuine-UNKNOWN surface is present in an ACCEPTED frame.
        var world = new ParseArtifactWorld(CapstoneChain(),
            [["textless", "textless"], ["textless", "textless"], ["textless", "textless"], ["textless", "textless"]]);

        var run = await RunAsync(world, "art-12");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
    }

    // ── ART-13: ScrollBackward uses identical eligibility ────────────────────

    [Fact]
    public async Task ART13_ScrollBackward_ArtifactMixed_Converges()
    {
        // The exploration is clean; the FIRST backward revisit observation is
        // artifact-mixed (valid rows + persistent artifacts). Identical
        // eligibility applies to the backward path: artifacts excluded, settle
        // converges, the revisit continues and the run completes.
        var world = new ParseArtifactWorld(
            CapstoneChain(),
            [[], [], [], ["mixed", "clean"], ["mixed", "clean"]]);

        var run = await RunAsync(world, "art-13");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("Unknown interaction affordances remain", run.Reason ?? "");
        Assert.Contains(run.Environment.ActionHistory, action => action is DeviceAction.ScrollBackward);
        Assert.All(run.Environment.ObservationHistory.SelectMany(o => o.StructuredElements),
            e => Assert.True(e.Bounds is { IsValid: true }));
    }
}
