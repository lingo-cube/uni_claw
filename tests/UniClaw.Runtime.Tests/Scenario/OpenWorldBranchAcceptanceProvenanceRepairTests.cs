using System.Collections.Immutable;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.World;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// OPEN_WORLD_BRANCH_ACCEPTANCE_PROVENANCE_REPAIR — ACCEPT-1..ACCEPT-10.
///
/// Deterministic regression: TryAcceptBranchInventory must be provenance-driven
/// when the caller supplies explicit RequiredBranchGrounding — branch acceptance
/// goes BranchSourceGroundingEvidence -> NavigationSourceOccurrenceReference ->
/// SourceGroundingValidator -> normalized logical source -> accept/reject. The
/// BranchIdentity is a caller branch LABEL, never a source identity: acceptance
/// MUST NOT require BranchIdentity == source.Elements.Text nor ==
/// StructuredElements.RawText (the OCR channel may drop rows that the
/// structured occurrence still carries). Elements-only environments keep the
/// legacy identity check, unchanged.
/// </summary>
public sealed class OpenWorldBranchAcceptanceProvenanceRepairTests
{
    private const string App = "com.uniclaw.fixture";
    private const string RootPage = "Fixture Root";

    // ── deterministic world: per-viewport OCR texts + structured rows ───────

    private sealed record ViewportSpec(string[] OcrTexts, StructuredElementEvidence[] Structured);

    private sealed class ScriptedWorld : IEnvironment
    {
        private readonly ViewportSpec[] _rootViewports;
        private readonly int _expectedVisits;
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private string _screen = "Launcher";
        private int _viewport;
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public ScriptedWorld(ViewportSpec[] rootViewports)
        {
            _rootViewports = rootViewports;
            _expectedVisits = rootViewports
                .SelectMany(v => v.OcrTexts)
                .Where(t => t.StartsWith("Child ", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Count();
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
                        var titles = _rootViewports[_viewport].OcrTexts;
                        // Resolve the tapped row by the action's structured bounds
                        // (mirror of the real ADB tap-by-bounds); index is a
                        // legacy fallback.
                        int? idx = ResolveRowIndex(tap, titles.Length);
                        if (idx is { } i && i >= 0 && i < titles.Length)
                        {
                            _visited.Add(titles[i]);
                            _screen = "Child:" + titles[i];
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
                var spec = _rootViewports[_viewport];
                var elements = ImmutableArray.CreateBuilder<ObservedElement>();
                for (int i = 0; i < spec.OcrTexts.Length; i++)
                    elements.Add(new ObservedElement(spec.OcrTexts[i], null, i, RowBounds(i), "text"));
                var state = _visited.Count == _expectedVisits
                    ? $"Visited {_visited.Count}/{_expectedVisits} [CAPSTONE COMPLETE]"
                    : $"Visited {_visited.Count}/{_expectedVisits}";
                elements.Add(new ObservedElement(state, null, spec.OcrTexts.Length, RowBounds(spec.OcrTexts.Length), "text"));
                return WithPrimaryEvidence(new Observation(elements.ToImmutable(), App, seq)
                {
                    StructuredElements = spec.Structured.ToImmutableArray(),
                });
            }
            var title = _screen["Child:".Length..];
            return WithPrimaryEvidence(new Observation(
                ImmutableArray.Create(
                    new ObservedElement(RootPage, null, 0, RowBounds(0), "text"),
                    new ObservedElement(title + " page marker", null, 1, RowBounds(1), "text")),
                App, seq)
            {
                StructuredElements = [],
            });
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

    private static Observation WithPrimaryEvidence(Observation observation)
    {
        var stamped = observation with
        {
            Sources = [new ObservationSourceMetadata(ObservationSourceTier.PrimaryVision, true,
                observation.SequenceNumber, $"frame-{observation.SequenceNumber}", 100, 100, "vision", "vision")]
        };
        var context = SemanticObservationFactProjector.Project(stamped);
        var manifest = new SemanticCapabilityManifest("fixture", "1", ["navigation", "parent-return"]);
        var evidence = context.Facts.Where(f => f.SourceTier == SemanticSourceTier.Primary && f.RawText is not null)
            .GroupBy(f => f.OccurrenceId, StringComparer.Ordinal)
            .Select(group =>
            {
                var fact = group.First();
                var kind = string.Equals(fact.RawText, RootPage, StringComparison.Ordinal)
                    ? ElementAffordanceKind.ParentReturnControl
                    : fact.RawText!.StartsWith("Child ", StringComparison.Ordinal)
                        ? ElementAffordanceKind.NavigationCandidate
                        : group.Any(f => f.Kind == SemanticObservationFactKind.Geometry)
                            ? ElementAffordanceKind.NavigationCandidate
                            : ElementAffordanceKind.NonInteractive;
                var candidate = new ElementAffordanceCandidateEvidence(
                    fact.OccurrenceId, kind,
                    new SemanticSymbolReference(manifest.ManifestId, manifest.Version, "navigation"),
                    context.Observation, new SemanticScopeReference(fact.OccurrenceId),
                    new SemanticProvenance(fact.SourceId, SemanticSourceTier.Primary, fact.ProvenanceId,
                        DateTimeOffset.UnixEpoch, fact.FrameId), 1d, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue);
                return new SemanticEvidenceV2Envelope($"evidence:{fact.OccurrenceId}", candidate);
            }).ToArray();
        return stamped with { AdmittedSemanticEvidence = new AdmittedSemanticEvidenceSnapshot(evidence) };
    }

    // ── structured element builders (mirror the real adapter's rows) ────────

    private static StructuredElementEvidence Row(string title, int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: "com.uniclaw.fixture:id/row_title",
            Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true,
            Bounds: RowBounds(ordinal), RawText: title);

    private static StructuredElementEvidence SwitchRow(string title, int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: "com.uniclaw.fixture:id/local_switch",
            Clickable: true, Checkable: true, Checked: false, Enabled: true, Focusable: true,
            Bounds: RowBounds(ordinal), RawText: title);

    private static StructuredElementEvidence ClickableTextlessRow(int ordinal)
        => new(Class: "android.widget.LinearLayout", ResourceId: null,
            Clickable: true, Checkable: false, Checked: false, Enabled: true, Focusable: true,
            Bounds: RowBounds(ordinal));

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

    /// <summary>Capstone-style inventory: first structured occurrence per title.</summary>
    private static BranchInventoryEvidence DefaultInventory(ImmutableArray<Observation> observations, int semanticDepth)
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

    private static BranchInventoryEvidence InventoryOf(params (string Branch, long Seq, string Occurrence)[] entries)
    {
        var required = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var (branch, seq, occurrence) in entries)
        {
            required[branch] = seq;
            grounding[branch] = new NavigationSourceOccurrenceReference(seq, occurrence);
        }
        return new BranchInventoryEvidence(required.ToImmutable(), "injected inventory", grounding.ToImmutable());
    }

    // ── run harness (production path) ───────────────────────────────────────

    private sealed record RunOutcome(
        RunState State,
        string? Reason,
        ScriptedWorld Environment,
        RuntimeAgent Agent);

    private static async Task<RunOutcome> RunAsync(
        ScriptedWorld world,
        Func<ImmutableArray<Observation>, int, BranchInventoryEvidence> inventory,
        Func<ImmutableArray<Observation>, ViewportExplorationEvidence>? explore,
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
        var goal = new Goal(
            observation => new GoalEvidence(
                observation.Elements.Any(e => e.Text is { } t && t.Contains("CAPSTONE COMPLETE", StringComparison.Ordinal)),
                "capstone goal evidence",
                observation.SequenceNumber),
            CandidateAuthorizationEvaluator: (observation, element) =>
                new CandidateAuthorizationEvidence(
                    element.Text.StartsWith("Child ", StringComparison.Ordinal)
                        || string.Equals(element.Text, RootPage, StringComparison.Ordinal),
                    $"authorize {element.Text}"),
            ViewportExplorationEvaluator: explore,
            BranchInventoryEvaluator: inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 1,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Traverse all Fixture Root children",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new RunOutcome(state, agent.Reason, world, agent);
    }

    // ── canonical capstone-like scroll chain (aligned OCR + structured) ─────

    private static ViewportSpec[] AlignedChain()
    {
        return
        [
            new(["Child 01", "Child 02", "Child 03", "Child 04"],
                Rows("Child 01", "Child 02", "Child 03", "Child 04")),
            new(["Child 03", "Child 04", "Child 05", "Child 06", "Child 07"],
                Rows("Child 03", "Child 04", "Child 05", "Child 06", "Child 07")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"],
                Rows("Child 05", "Child 06", "Child 07", "Child 08")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"],
                Rows("Child 05", "Child 06", "Child 07", "Child 08")),
        ];
    }

    // ── ACCEPT-1: explicit provenance + OCR present -> PASS ─────────────────

    [Fact]
    public async Task ACCEPT1_ExplicitProvenance_OcrPresent_Passes()
    {
        var world = new ScriptedWorld(AlignedChain());

        var run = await RunAsync(world, DefaultInventory, ExploreWhileNew, "ow-accept-1");

        // Acceptance passed (explicit grounding validated); the dispatch and the
        // full child round trip (with the bounded revisit) complete — this fake's
        // children carry no structured navigation candidates and no unresolved
        // Unknowns, so their leaf completeness passes.
        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("grounding rejected", run.Reason);
        Assert.DoesNotContain("does not reference accepted source evidence", run.Reason);
    }

    // ── ACCEPT-2: explicit provenance valid + OCR MISSED the row -> PASS ────

    [Fact]
    public async Task ACCEPT2_ExplicitProvenanceValid_OcrMissed_Passes()
    {
        // The capstone failure: O3's structured elements carry Child 06/07, but
        // the OCR channel dropped them ("Child 06" grounded to seq=3). The
        // provenance is valid; the legacy OCR text check must not reject it.
        var world = new ScriptedWorld(
        [
            new(["Child 01", "Child 02", "Child 03", "Child 04"],
                Rows("Child 01", "Child 02", "Child 03", "Child 04")),
            new(["Child 03", "Child 04", "Child 05"],          // OCR dropped Child 06/07
                Rows("Child 03", "Child 04", "Child 05", "Child 06", "Child 07")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"],
                Rows("Child 05", "Child 06", "Child 07", "Child 08")),
            new(["Child 05", "Child 06", "Child 07", "Child 08"],
                Rows("Child 05", "Child 06", "Child 07", "Child 08")),
        ]);

        var run = await RunAsync(world, DefaultInventory, ExploreWhileNew, "ow-accept-2");

        Assert.Equal(RunState.Completed, run.State);
        Assert.DoesNotContain("does not reference accepted source evidence seq=3", run.Reason);
        Assert.DoesNotContain("grounding rejected", run.Reason);
    }

    // ── ACCEPT-3: BranchIdentity != source title, provenance valid -> PASS ──

    [Fact]
    public async Task ACCEPT3_BranchLabelDiffersFromSourceTitle_ProvenanceValid_Passes()
    {
        var world = new ScriptedWorld(AlignedChain());
        // Branch label "Sixth child" is grounded to occurrence nav:5 of O3 whose
        // structured title is "Child 06" — the label is NOT the source identity.
        var inventory = InventoryOf(("Sixth child", 4, "nav:5"));  // scroll-1 stability-confirmed frame

        var run = await RunAsync(
            world,
            (observations, depth) => depth >= 1
                ? new BranchInventoryEvidence(
                    ImmutableDictionary<string, long>.Empty,
                    "no child is required inside depth <= 1",
                    ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty)
                : inventory,
            ExploreWhileNew,
            "ow-accept-3");

        // The single "Sixth child" branch (label != source title) is grounded,
        // dispatched and returned; only the incidental goal (this world expects 8
        // visits) remains unsatisfied — the acceptance/grounding/return all PASS.
        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Verified bounded traversal completion but fresh GoalEvidence remains unsatisfied", run.Reason);
        Assert.DoesNotContain("grounding rejected", run.Reason);
        Assert.DoesNotContain("does not reference accepted source evidence", run.Reason);
    }

    // ── ACCEPT-4: nonexistent occurrence -> FAIL ────────────────────────────

    [Fact]
    public async Task ACCEPT4_NonexistentOccurrence_Rejected()
    {
        var world = new ScriptedWorld([new(["Child 01", "Child 02", "Child 03", "Child 04"],
            Rows("Child 01", "Child 02", "Child 03", "Child 04"))]);
        var inventory = InventoryOf(("Child 01", 2, "nav:99"));

        var run = await RunAsync(world, (observations, depth) => inventory, explore: null, "ow-accept-4");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("grounding rejected", run.Reason);
        Assert.Contains("nav:99", run.Reason);
    }

    // ── ACCEPT-5: stale/foreign occurrence -> FAIL ──────────────────────────

    [Fact]
    public async Task ACCEPT5_StaleForeignOccurrence_Rejected()
    {
        var world = new ScriptedWorld([new(["Child 01", "Child 02", "Child 03", "Child 04"],
            Rows("Child 01", "Child 02", "Child 03", "Child 04"))]);
        // Required-map source sequence is a valid accepted observation (2), but
        // the EXPLICIT GROUNDING references a foreign observation (999) — the
        // provenance-driven validator must reject it.
        var required = ImmutableDictionary<string, long>.Empty.Add("Child 01", 2);
        var grounding = ImmutableDictionary<string, NavigationSourceOccurrenceReference>.Empty
            .Add("Child 01", new NavigationSourceOccurrenceReference(999, "nav:1"));
        var inventory = new BranchInventoryEvidence(required, "injected inventory", grounding);

        var run = await RunAsync(world, (observations, depth) => inventory, explore: null, "ow-accept-5");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("grounding rejected", run.Reason);
        Assert.Contains("not an accepted viewport observation", run.Reason);
    }

    // ── ACCEPT-6: LOCAL_CONTROL / UNKNOWN element -> FAIL ───────────────────

    [Theory]
    [InlineData("switch")]
    [InlineData("unknown")]
    public async Task ACCEPT6_LocalControlOrUnknown_HasNoOccurrence_Rejected(string mode)
    {
        StructuredElementEvidence[] structured = mode == "switch"
            ? [Row("Child 01", 0), SwitchRow("Local", 1), Row("Child 02", 2)]
            : [Row("Child 01", 0), ClickableTextlessRow(1), Row("Child 02", 2)];
        var world = new ScriptedWorld([new(["Child 01", "Local", "Child 02"], structured)]);
        // A LOCAL_CONTROL/UNKNOWN element produces no NAVIGATION_CANDIDATE
        // occurrence (only nav:1, nav:2 exist), so a grounding targeting the
        // would-be third position cannot exist -> fail closed.
        var inventory = InventoryOf(("Local", 2, "nav:3"));

        var run = await RunAsync(world, (observations, depth) => inventory, explore: null, "ow-accept-6-" + mode);

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("grounding rejected", run.Reason);
        Assert.Contains("does not exist as a NAVIGATION_CANDIDATE", run.Reason);
    }

    // ── ACCEPT-7: ambiguous equivalence -> FAIL (Unresolved -> rejected) ─────

    [Fact]
    public void ACCEPT7_AmbiguousEquivalence_UnresolvedAndRejected()
    {
        // Duplicate complete signatures -> normalization unresolved -> the
        // acceptance mechanism (SourceGroundingValidator) rejects. On the full
        // open-world path this evidence is already blocked earlier by the
        // completeness gate (same fail-closed Normalize), which is unchanged.
        var obs = new Observation([], App, 1)
        {
            StructuredElements = ImmutableArray.Create(
                Row("Shared", 0), Row("Item A", 1), Row("Shared", 2), Row("Item B", 3)),
        };
        var accepted = ImmutableArray.Create(obs);
        var normalization = SourceEquivalenceNormalizer.Normalize(accepted);
        Assert.False(normalization.IsResolved);

        var result = SourceGroundingValidator.Validate(
            accepted,
            new BranchSourceGroundingEvidence("Shared", new NavigationSourceOccurrenceReference(1, "nav:1")),
            normalization);
        Assert.Equal(SourceGroundingValidator.SourceGroundingStatus.Unresolved, result.Status);
    }

    // ── ACCEPT-8: duplicate branches -> same logical source -> FAIL ─────────

    [Fact]
    public async Task ACCEPT8_DuplicateBranchesSameLogicalSource_Rejected()
    {
        var world = new ScriptedWorld(AlignedChain());
        // "Child 05" in O3 (nav:3) and O4 (nav:1) are the SAME world source
        // (unique ordered overlap). Two branches claiming it must be rejected.
        var inventory = InventoryOf(("Child 05", 4, "nav:3"), ("Child 05 dup", 6, "nav:1"));  // scroll-1/2 stability-confirmed frames

        var run = await RunAsync(world, (observations, depth) => inventory, ExploreWhileNew, "ow-accept-8");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("grounding rejected", run.Reason);
        Assert.Contains("already claimed by another branch", run.Reason);
    }

    // ── ACCEPT-9: legacy (no grounding, Elements-only) -> old behavior ──────

    [Fact]
    public async Task ACCEPT9_LegacyNoGrounding_ElementsOnly_UnchangedAndCompletes()
    {
        // Elements-only world (no structured occurrences). Inventory acceptance
        // is provenance-driven through the canonical navigation occurrences; the
        // full 4-child round trip completes.
        var world = new ScriptedWorld([new(["Child 01", "Child 02", "Child 03", "Child 04"], [])]);

        var run = await RunAsync(world, DefaultInventory, explore: null, "ow-accept-9");

        Assert.Equal(RunState.Completed, run.State);
        Assert.Equal(8, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.False(run.Reason?.Contains("grounding rejected", StringComparison.Ordinal) ?? false);
        Assert.False(run.Reason?.Contains("has no explicit source provenance grounding", StringComparison.Ordinal) ?? false);
    }

    // ── ACCEPT-10: historical O2/O3/O4 groundings valid while current = O5 ──

    [Fact]
    public async Task ACCEPT10_HistoricalGroundingsAccepted_WhileCurrentIsO5()
    {
        var world = new ScriptedWorld(AlignedChain());

        var run = await RunAsync(world, DefaultInventory, ExploreWhileNew, "ow-accept-10");

        Assert.Equal(RunState.Completed, run.State);
        // The accepted progress evidence (recorded by TryAcceptBranchInventory)
        // retains the HISTORICAL source anchors — O2/O3/O4 — while the loop's
        // current was the fresh O5.
        var approved = run.Agent.BranchProgress[RootPage].ApprovedSiblingEvidence;
        Assert.Equal(8, approved.Count);
        Assert.Equal(2, approved["Child 01"]);
        Assert.Equal(4, approved["Child 05"]);  // scroll-1 stability-confirmed frame
        Assert.Equal(4, approved["Child 06"]);
        Assert.Equal(6, approved["Child 08"]);  // scroll-2 stability-confirmed frame
    }

    // ── REVISIT_COMPLETENESS_FRESHNESS_PRESSURE evidence (contract boundary) ─

    [Fact]
    public void CompletenessNormalization_IsForwardMonotonic_BackwardEvidenceUnresolved()
    {
        // The accepted-set normalizer requires a unique ORDERED suffix-prefix
        // overlap between ADJACENT viewports (forward-scroll shape). A backward
        // or parent-returned observation (e.g. the top viewport re-appearing
        // after the terminal bottom viewport) has NO ordered overlap with the
        // forward terminal viewport, so the existing completeness contract
        // yields Unresolved — it cannot express non-monotonic revisit evidence
        // (this is the REVISIT_COMPLETENESS_FRESHNESS_PRESSURE finding).
        Observation View(long seq, params string[] titles)
            => new([], App, seq)
            {
                StructuredElements = titles.Select((t, i) => Row(t, i)).ToImmutableArray(),
            };

        var top = View(2, "Child 01", "Child 02", "Child 03", "Child 04");
        var mid = View(3, "Child 03", "Child 04", "Child 05", "Child 06", "Child 07");
        var bottom = View(4, "Child 05", "Child 06", "Child 07", "Child 08");
        var terminal = View(5, "Child 05", "Child 06", "Child 07", "Child 08"); // exhaustion duplicate

        // Forward chain resolves.
        Assert.True(SourceEquivalenceNormalizer
            .Normalize(ImmutableArray.Create(top, mid, bottom, terminal)).IsResolved);

        // A non-monotonic observation (the top viewport again) appended after the
        // terminal viewport breaks the ordered-overlap chain -> Unresolved.
        var returnedTop = View(6, "Child 01", "Child 02", "Child 03", "Child 04");
        Assert.False(SourceEquivalenceNormalizer
            .Normalize(ImmutableArray.Create(top, mid, bottom, terminal, returnedTop)).IsResolved);
    }
}
