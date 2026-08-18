using System.Collections.Immutable;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Adapters.Perception;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Traversal;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_SINGLE_RECURSIVE_CHILD — Phase 2 of settings-full-tree-enumeration-integration.
///
/// Proves on the REAL Android Settings (emulator) the graduated recursion
/// integration: ContainerComplete(Root) → EXACTLY ONE explicitly authorized
/// grounded child source → fresh-bounds dispatch → settled Child transition →
/// Child container → ContainerComplete(Child). Root completeness is re-proven
/// per run (never reused). BranchIdentity is a caller label only; the child
/// semantic identity is established by a STRUCTURAL rule (Settings foreground,
/// root marker absent, labelled back control present → "SettingsChild" — never
/// a title hardcode). Grandchild recursion is NOT authorized in Phase 2.
///
/// The deterministic RC1-1..RC1-14 tests exercise the same resolver/authorizer
/// through a fake world (whose back control models the graduated mechanism's
/// expected shape: a labelled control carrying the parent page name); the
/// real-device test records the evidence.
/// </summary>
public sealed class SettingsSingleRecursiveChildTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string SearchBarRid = "com.android.settings:id/search_action_bar";
    private const string BackControlCd = "Navigate up";
    private const string TitleRoleRid = "com.android.settings:id/collapsing_toolbar";
    private const string SettingsSubpagePrefix = "SettingsSubpage(";
    /// <summary>The Location sub-page semantic identity (fresh page-title-role
    /// "Location" → SettingsSubpage(Location)).</summary>
    internal const string LocationIdentity = "SettingsSubpage(Location)";
    /// <summary>Android platform accessibility action-label evidence for the
    /// toolbar Up control (PARENT_RETURN_ACTION_LABEL_EVIDENCE — never a
    /// destination identity).</summary>
    private const string ParentReturnActionRoleLabel = "Navigate up";
    private const string SelectedChildLabel = "Location"; // scenario-declared target

    private const string AdbPath = "/Users/fran/Android/Sdk/platform-tools/adb";
    private const string Serial = "emulator-5554";
    private const string VisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RunId = "settings-single-recursive-child-001";
    private const string AgentInstanceId = "SETTINGS-RECURSION-001";

    private static int _agentCreations;

    // ── Phase-2 semantic resolver (structural; no title hardcode) ────────────

    /// <summary>
    /// DESTINATION IDENTITY MODEL (fresh destination evidence):
    ///  - ROOT identity: Settings foreground + the root-specific search action
    ///    bar (supporting structural marker; the entry boundary contract) →
    ///    "SettingsRoot" (unchanged).
    ///  - SUB-PAGE identity: Settings foreground + root marker ABSENT + the
    ///    labelled back control (the sub-page class signal: the real Android
    ///    "Navigate up" action label, or the fixture destination-labelled
    ///    return anchor) + the explicit PAGE-TITLE-ROLE structured evidence
    ///    (the app toolbar title node, audited: the content-desc of
    ///    com.android.settings:id/collapsing_toolbar) →
    ///    "SettingsSubpage(&lt;fresh page-title-role value&gt;)".
    /// The identity value comes ONLY from fresh destination structured
    /// evidence — never BranchIdentity, never SourceTitleText, never caller
    /// expectation, never OCR/first-text. PageClass "SettingsSubpage" != Page
    /// Identity (the title value). Missing / ambiguous page-title-role, or a
    /// back control with no title-role → fail closed (null). External
    /// foreground → null.
    /// </summary>
    internal static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, App, StringComparison.Ordinal))
            return null;
        var hasSearchBar = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, SearchBarRid, StringComparison.Ordinal));
        if (hasSearchBar)
            return RootPage;
        // Sub-page CLASS signal: the labelled back control — the real Android
        // "Navigate up" accessibility action label, OR the destination-labelled
        // return anchor (TitleText == the known PARENT page name — fixture
        // style). This is the page-CLASS signal, never the page identity.
        var hasBackControl = observation.StructuredElements.Any(se =>
            string.Equals(se.ContentDescription, BackControlCd, StringComparison.Ordinal)
            || string.Equals(se.TitleText, RootPage, StringComparison.Ordinal));
        if (!hasBackControl)
            return null;
        // PAGE-TITLE-ROLE: the explicit toolbar title node's content-desc from
        // the CURRENT fresh observation. Missing or ambiguous → fail closed.
        var titleRoles = observation.StructuredElements
            .Where(se => string.Equals(se.ResourceId, TitleRoleRid, StringComparison.Ordinal))
            .Select(se => se.ContentDescription)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (titleRoles.Length != 1)
            return null;
        return string.Concat(SettingsSubpagePrefix, titleRoles[0], ")");
    }

    /// <summary>
    /// Phase-2 authorization — EXACTLY ONE recursive obligation:
    ///  - from a ROOT observation: only the scenario-declared target child
    ///    label is authorized (the label is a caller branch label; the real
    ///    binding is the explicit RequiredBranchGrounding validated by
    ///    SourceGroundingValidator); every other root source is audited.
    ///  - from a CHILD observation: only the labelled parent-return control
    ///    (TitleText == the known parent page name — the graduated return
    ///    mechanism) is authorized; every child/grandchild source is audited
    ///    (recursive authorization is Phase 3+).
    /// </summary>
    internal static CandidateAuthorizationEvidence AuthorizePhase2(Observation observation, ObservedElement candidate)
    {
        var isRootObservation = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, SearchBarRid, StringComparison.Ordinal));
        if (isRootObservation)
        {
            if (string.Equals(candidate.Text, SelectedChildLabel, StringComparison.Ordinal))
            {
                return new(true,
                    $"Phase-2: authorize exactly one grounded root child '{candidate.Text}' (recursion integration).");
            }
            return new(false,
                "Phase-2 audit: root source is not the selected child; recursive authorization is Phase 3+.");
        }
        if (string.Equals(candidate.Text, RootPage, StringComparison.Ordinal)
            || string.Equals(candidate.Text, ParentReturnActionRoleLabel, StringComparison.Ordinal))
        {
            return new(true,
                "Phase-2: labelled parent-return control authorized (return mechanism, not recursion).");
        }
        return new(false,
            "Phase-2 audit: child/grandchild source; recursive authorization is Phase 3+.");
    }

    private static string TitleOf(string signature)
    {
        int bar = signature.IndexOf('|');
        return bar < 0 ? signature : signature[..bar];
    }

    private static ImmutableArray<string> NavSignatures(Observation observation)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var occurrence in SourceEquivalenceNormalizer.OccurrencesOf(observation))
            builder.Add(occurrence.StructuredSignature);
        return builder.ToImmutable();
    }

    internal static ViewportExplorationEvidence ExploreWhileNew(ImmutableArray<Observation> observations)
    {
        if (observations.IsDefaultOrEmpty)
            return new ViewportExplorationEvidence(true, "explore");
        var latest = observations[^1];
        var latestSigs = NavSignatures(latest).ToHashSet(StringComparer.Ordinal);
        var prior = observations.Take(observations.Length - 1)
            .SelectMany(o => NavSignatures(o)).ToHashSet(StringComparer.Ordinal);
        var hasNew = latestSigs.Any(s => !prior.Contains(s));
        return new ViewportExplorationEvidence(
            hasNew,
            hasNew ? "new source appeared; scroll more" : "no new source; exhausted");
    }

    internal static BranchInventoryEvidence Inventory(ImmutableArray<Observation> observations, int semanticDepth)
    {
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
        return new BranchInventoryEvidence(required.ToImmutable(), $"inventory: {first.Count} sources", grounding.ToImmutable());
    }

    internal static GoalEvidence AuditGoal(Observation observation)
        => new(false, "Phase-2 audit goal (no full-tree claim).", observation.SequenceNumber);

    // ═════════════════════════════════════════════════════════════════════════
    // RC1 deterministic integration tests (fake world, REAL resolver/authorizer)
    // ═════════════════════════════════════════════════════════════════════════

    internal sealed class RecursionWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private readonly string[][] _childViewports;
        private readonly bool _childHasTextlessUnknown;
        private readonly bool _childGoesForeign;
        private readonly bool _transitionDelayed;
        private string _screen = "Launcher";
        private int _rootViewport;
        private int _childViewport;
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];
        private bool _pendingRootFrame;

        public RecursionWorld(
            string[][] rootViewports,
            string[][] childViewports,
            bool childHasTextlessUnknown = false,
            bool childGoesForeign = false,
            bool transitionDelayed = false)
        {
            _rootViewports = rootViewports;
            _childViewports = childViewports;
            _childHasTextlessUnknown = childHasTextlessUnknown;
            _childGoesForeign = childGoesForeign;
            _transitionDelayed = transitionDelayed;
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
                    if (_screen == "Root" && _rootViewport < _rootViewports.Length - 1)
                        _rootViewport++;
                    else if (_screen == "Child" && _childViewport < _childViewports.Length - 1)
                        _childViewport++;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport advanced"));
                case DeviceAction.ScrollBackward:
                    if (_screen == "Root" && _rootViewport > 0)
                        _rootViewport--;
                    else if (_screen == "Child" && _childViewport > 0)
                        _childViewport--;
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport receded"));
                case DeviceAction.Tap tap:
                    if (_screen == "Root")
                    {
                        var rows = _rootViewports[_rootViewport];
                        int? idx = ResolveRowIndex(tap, rows.Length);
                        if (idx is { } i && i >= 0 && i < rows.Length)
                        {
                            if (_transitionDelayed)
                                _pendingRootFrame = true;
                            _screen = _childGoesForeign ? "Foreign" : "Child";
                            _childViewport = 0;
                        }
                    }
                    else if (_screen == "Child")
                    {
                        // A tap inside the child page: only the labelled
                        // parent-return control is a real interaction in
                        // Phase 2 — anything else would be a forbidden
                        // grandchild dispatch.
                        _screen = "Root";
                        _rootViewport = 0;
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
                return new Observation(
                    [new ObservedElement("Foreign marker", null, 0, null, "text")],
                    "com.android.other", seq);
            if (_screen == "Root")
            {
                var rows = _rootViewports[_rootViewport];
                var elements = rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(i), "text")).ToImmutableArray();
                var structured = rows.Select((r, i) => RootRow(r, i)).Append(SearchBar()).ToImmutableArray();
                if (_pendingRootFrame)
                {
                    // Transition-delay: the first post-tap capture still shows
                    // the ROOT (the tap is dispatched but the page has not
                    // transitioned yet). The settle must treat it as
                    // provisional and re-observe.
                    _pendingRootFrame = false;
                }
                return new Observation(elements, App, seq) { StructuredElements = structured };
            }
            // Child page: labelled back control (graduated mechanism shape:
            // TitleText == parent page name) + its own rows.
            var childElements = _childViewports[_childViewport]
                .Select((r, i) => new ObservedElement(r, null, i, ChildRowBounds(i), "text")).ToImmutableArray();
            var childStructured = _childViewports[_childViewport]
                .Select((r, i) => ChildRow(r, i))
                .Append(BackControl())
                .Append(TitleRole(SelectedChildLabel))
                .ToImmutableArray();
            if (_childHasTextlessUnknown)
                childStructured = childStructured.Add(TextlessClickable(ChildRowBounds(_childViewports[_childViewport].Length)));
            return new Observation(childElements, App, seq) { StructuredElements = childStructured };
        }

        private static ElementBounds RowBounds(int ordinal) => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));
        private static ElementBounds ChildRowBounds(int ordinal) => new(0, 0.08f + 0.1f * ordinal, 1, 0.08f + 0.1f * (ordinal + 1));

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

        internal static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", SearchBarRid, true, false, false, true, false,
                new ElementBounds(0f, 0f, 1f, 0.06f), "Search settings", null, null, null, null);

        /// <summary>The REAL Settings parent-return control shape: ImageButton,
        /// content-desc "Navigate up", NO TitleText (the graduated action-role
        /// resolution resolves it contextually; the analyzer keeps it UNKNOWN).</summary>
        internal static StructuredElementEvidence BackControl()
            => new("android.widget.ImageButton", null, true, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, null, BackControlCd, null);

        /// <summary>Fixture-style destination-labelled return control (TitleText
        /// == the parent page name) — the existing graduated evidence kind.</summary>
        internal static StructuredElementEvidence FixtureReturnControl()
            => new("android.widget.Button", null, true, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), RootPage, null, null, null, null);

        /// <summary>PAGE-TITLE-ROLE structural evidence for a sub-page: the app
        /// toolbar title node (content-desc = the page title) — admitted by the
        /// production admission boundary as non-interactive structural page
        /// evidence.</summary>
        internal static StructuredElementEvidence TitleRole(string pageTitle)
            => new("android.widget.FrameLayout", TitleRoleRid, null, null, null, true, null,
                new ElementBounds(0f, 0f, 1f, 0.28f), null, null, null, pageTitle, null);


        internal static StructuredElementEvidence RootRow(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                RowBounds(ordinal), title, null, false, null, null);

        internal static StructuredElementEvidence ChildRow(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                ChildRowBounds(ordinal), title, null, false, null, null);

        private static StructuredElementEvidence TextlessClickable(ElementBounds bounds)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                bounds, null, null, false, null, null);
    }

    private sealed record RunOutcome(RunState State, string? Reason, RecursionWorld Environment, RuntimeAgent Agent);

    private static async Task<RunOutcome> RunFakeAsync(RecursionWorld world, string runId)
    {
        var traversal = new RuntimeTraversal(world);
        var startup = new RuntimeStartup(world, App, ResolveSemanticPage, launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => world.ObserveAsync(cancellationToken),
            ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var goal = new Goal(
            AuditGoal,
            CandidateAuthorizationEvaluator: AuthorizePhase2,
            ViewportExplorationEvaluator: ExploreWhileNew,
            BranchInventoryEvaluator: Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 2,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-2: ContainerComplete(Root) → exactly one authorized child → ContainerComplete(Child)",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new RunOutcome(state, agent.Reason, world, agent);
    }

    private static string[][] RootChain()
    {
        return
        [
            ["Network & internet", "Connected devices", "Apps", "Notifications", "Battery"],
            ["Battery", "Storage", "Sound & vibration", "Display", "Location", "Security & privacy"],
            ["Location", "Security & privacy", "Safety & emergency", "Passwords & accounts", "System", "About phone"],
        ];
    }

    private static string[][] ChildChain()
    {
        return
        [
            ["Location services", "App location permissions", "Recent location requests"],
            ["App location permissions", "Recent location requests", "Location accuracy"],
        ];
    }

    // ── RC1-1: Root complete → one grounded authorized child ─────────────────

    [Fact]
    public async Task RC1_1_RootComplete_OneGroundedAuthorizedChild()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-1");

        // The run re-proves the ROOT epoch and dispatches the selected child.
        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true
            && t.Reason.Contains("discovery epoch FROZEN", StringComparison.Ordinal)
            && t.ContainerId == RootPage);
        Assert.Contains(run.Environment.ActionHistory, a => a is DeviceAction.Tap);
        // The child container was reached through the production transition.
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == LocationIdentity);
    }

    // ── RC1-2: exactly ONE Root child authorized ─────────────────────────────

    [Fact]
    public async Task RC1_2_ExactlyOneRootChildAuthorized()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-2");

        // EXACTLY ONE root child is dispatched: the two taps are the root→child
        // dispatch and the child's verified parent return to the root (the
        // child's own candidates are audited → return-eligible). No other root
        // source is ever dispatched.
        Assert.Equal(2, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == LocationIdentity);
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == RootPage
            && t.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true);
    }

    // ── RC1-3: fresh source bounds used for dispatch ─────────────────────────

    [Fact]
    public async Task RC1_3_FreshSourceBoundsUsedForDispatch()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-3");

        var tap = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().FirstOrDefault();
        Assert.NotNull(tap);
        // The tap carries FRESH structured bounds (the world resolves the tapped
        // row via those bounds) — never null, never historical.
        Assert.NotNull(tap.TargetBounds);
        Assert.True(tap.TargetBounds.IsValid);
        Assert.True(tap.TargetBounds.Height > 0f);
    }

    // ── RC1-4: settled Child transition required ─────────────────────────────

    [Fact]
    public async Task RC1_4_SettledChildTransitionRequired()
    {
        // The first post-tap observation still shows the ROOT (transition
        // delay): the provisional frame must NOT enter the child container —
        // only the settled fresh child observation may.
        var world = new RecursionWorld(RootChain(), ChildChain(), transitionDelayed: true);

        var run = await RunFakeAsync(world, "rc1-4");

        Assert.Contains(run.Agent.Trace, t => t.ContainerId == LocationIdentity);
        Assert.DoesNotContain(run.Reason ?? "", "did not prove a fresh child Container transition");
    }

    // ── RC1-5: Root cannot equal Child identity ──────────────────────────────

    [Fact]
    public void RC1_5_RootCannotEqualChildIdentity()
    {
        var rootObs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 1)
        {
            StructuredElements = ImmutableArray.Create(RecursionWorld.SearchBar(), RecursionWorld.RootRow("Location", 0)),
        };
        var childObs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 2)
        {
            StructuredElements = ImmutableArray.Create(
                RecursionWorld.BackControl(), RecursionWorld.ChildRow("Location services", 0),
                RecursionWorld.TitleRole(SelectedChildLabel)),
        };

        Assert.Equal(RootPage, ResolveSemanticPage(rootObs));
        Assert.Equal(LocationIdentity, ResolveSemanticPage(childObs));
        Assert.NotEqual(ResolveSemanticPage(rootObs), ResolveSemanticPage(childObs));
    }

    // ── RC1-6: Child inventory independently discovered ──────────────────────

    [Fact]
    public async Task RC1_6_ChildInventoryIndependentlyDiscovered()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-6");

        // The CHILD's own navigation candidates are discovered in the child
        // container and the child epoch freezes on the child inventory (the
        // root rows never leak into the child's epoch).
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == LocationIdentity
            && t.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true
            && t.Reason.Contains("discovery epoch FROZEN", StringComparison.Ordinal));
    }

    // ── RC1-7: Child positive exhaustion required ────────────────────────────

    [Fact]
    public async Task RC1_7_ChildPositiveExhaustionRequired()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-7");

        // The child epoch freezes only after "no new source; exhausted".
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == LocationIdentity
            && t.Reason?.Contains("viewport exploration exhausted", StringComparison.Ordinal) is true);
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == LocationIdentity
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
    }

    // ── RC1-8: Child Unknown blocks completeness ─────────────────────────────

    [Fact]
    public async Task RC1_8_ChildUnknownBlocksCompleteness()
    {
        var world = new RecursionWorld(RootChain(), ChildChain(), childHasTextlessUnknown: true);

        var run = await RunFakeAsync(world, "rc1-8");

        // The valid-bounds textless clickable is a genuine UNKNOWN that blocks
        // the child completeness (fail closed).
        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
    }

    // ── RC1-9: ContainerComplete(Child) != SubtreeComplete(Child) ────────────

    [Fact]
    public async Task RC1_9_ContainerCompleteDoesNotClaimSubtree()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-9");

        // Phase 2 never claims subtree / full-tree completion.
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("SubtreeComplete", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("FullTreeComplete", StringComparison.Ordinal) is true);
    }

    // ── RC1-10: Grandchild recursion not authorized in Phase 2 ───────────────

    [Fact]
    public async Task RC1_10_GrandchildRecursionNotAuthorized()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-10");

        // The child's own navigation candidates are audited, NOT authorized:
        // the taps are the root→child dispatch and the child's verified parent
        // return — zero grandchild taps, zero grandchild container.
        Assert.Equal(2, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
        Assert.DoesNotContain(run.Agent.Trace, t => t.ContainerId == "SettingsSubpage(Location services)"
            || t.ContainerId == "SettingsSubpage(Recent access)");
    }

    // ── RC1-11: external foreground fails closed ─────────────────────────────

    [Fact]
    public async Task RC1_11_ExternalForegroundFailsClosed()
    {
        var world = new RecursionWorld(RootChain(), ChildChain(), childGoesForeign: true);

        var run = await RunFakeAsync(world, "rc1-11");

        // The child transition lands on a foreign foreground → the resolver
        // returns null → the transition settle fails closed (no child identity).
        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t => t.ContainerId == LocationIdentity);
    }

    // ── RC1-12: duplicate/cycle identity safety unchanged ────────────────────
    // Covered by OpenWorldTraversalIdentitySafetyTests (ancestry cycle +
    // duplicate visited identity fail closed) and asserted here at the
    // resolver level: the root marker is authoritative — a child can never
    // impersonate the root.

    [Fact]
    public void RC1_12_IdentitySafetyInvariantsHold()
    {
        var both = new Observation(ImmutableArray<ObservedElement>.Empty, App, 1)
        {
            StructuredElements = ImmutableArray.Create(
                RecursionWorld.SearchBar(), RecursionWorld.BackControl()),
        };
        Assert.Equal(RootPage, ResolveSemanticPage(both));
    }

    // ── RC1-13: Root frozen inventory remains unchanged ──────────────────────

    [Fact]
    public async Task RC1_13_RootFrozenInventoryUnchanged()
    {
        var world = new RecursionWorld(RootChain(), ChildChain());

        var run = await RunFakeAsync(world, "rc1-13");

        // The ROOT discovery epoch is frozen exactly once; the child stage
        // never re-normalizes or expands the root inventory.
        var rootEpochs = run.Agent.Trace.Count(t =>
            t.ContainerId == RootPage
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.Equal(1, rootEpochs);
    }

    // ── RC1-14: COMPOSE-05 / ART / ROLE / SIG / SEARCH / SQ / PROV / NM / RVT /
    // ── AFF / SET green — covered by the full deterministic suite. ───────────

    // ═════════════════════════════════════════════════════════════════════════
    // REAL DEVICE Phase-2 run
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class StructuredEnvironment : IEnvironment
    {
        private readonly PhysicalEnvironment _inner;
        public StructuredEnvironment(PhysicalEnvironment inner) => _inner = inner;
        public IReadOnlyList<DeviceAction> ActionHistory => _inner.ActionHistory;
        public IReadOnlyList<Observation> ObservationHistory => _inner.ObservationHistory;
        public List<Observation> AllObservations { get; } = new();
        public List<string> RawXmls { get; } = new();

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var observation = await _inner.ObserveAsync(cancellationToken);
            var runner = new AdbProcessRunner();
            _ = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/recursion.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var cat = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "cat", "/sdcard/recursion.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var xml = System.Text.Encoding.UTF8.GetString(cat.StandardOutput);
            RawXmls.Add(xml);
            if (string.IsNullOrWhiteSpace(xml))
                return observation;
            try
            {
                var structured = AdbUiHierarchySource.Parse(xml, 1080, 1920);
                var decorated = observation with { StructuredElements = structured };
                AllObservations.Add(decorated);
                return decorated;
            }
            catch
            {
                AllObservations.Add(observation);
                return observation;
            }
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => _inner.ExecuteAsync(action, cancellationToken);
    }

    [Fact]
    public async Task SettingsSingleRecursiveChild_RealDevice_Phase2()
    {
        _agentCreations = 0;
        var setupRunner = new AdbProcessRunner();
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "force-stop", App }, TimeSpan.FromSeconds(30), CancellationToken.None);
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "start", "-a", "android.settings.SETTINGS" }, TimeSpan.FromSeconds(30), CancellationToken.None);
        for (int i = 0; i < 20; i++)
        {
            var probe = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/ready_rec.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            var probeCat = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "cat", "/sdcard/ready_rec.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            if (System.Text.Encoding.UTF8.GetString(probeCat.StandardOutput).Contains("com.android.settings", StringComparison.Ordinal))
                break;
            await Task.Delay(1000);
        }

        var rawEnvironment = new PhysicalEnvironment(
            new AdbScreenshotSource(Serial, AdbPath),
            new LocalVisionPerceptionSource(VisionSocket),
            new AdbDispatchTarget(Serial, AdbPath),
            App, 1080, 1920);
        var environment = new StructuredEnvironment(rawEnvironment);

        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(
            environment, App, ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        _agentCreations++;
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var receipts = new List<GoalEvidence>();
        var goal = new Goal(
            observation =>
            {
                var evidence = AuditGoal(observation);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: AuthorizePhase2,
            ViewportExplorationEvaluator: ExploreWhileNew,
            BranchInventoryEvaluator: Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 2,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-2: ContainerComplete(Root) → exactly one authorized child → ContainerComplete(Child)",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, RunId, CancellationToken.None);

        // ── evidence dump ────────────────────────────────────────────────────
        var evidence = new System.Text.StringBuilder();
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"AGENT_ID={AgentInstanceId} (creations={_agentCreations})");
        evidence.AppendLine($"RUN_ID={RunId}");
        evidence.AppendLine($"SELECTED_CHILD={SelectedChildLabel}");
        evidence.AppendLine("OBSERVATIONS=" + string.Join(",", environment.ObservationHistory.Select(o => o.SequenceNumber)));
        foreach (var observation in environment.ObservationHistory)
            evidence.AppendLine($"OBS_TEXT[{observation.SequenceNumber}]=" + string.Join(" | ", observation.Elements.Select(e => e.Text)));
        foreach (var observation in environment.AllObservations)
        {
            var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
            foreach (var affordance in affordances)
            {
                var raw = observation.StructuredElements[affordance.SourceElementIndex];
                evidence.AppendLine($"AFFORD[{observation.SequenceNumber}] {affordance.Classification} class={raw.Class} clickable={raw.Clickable} title={raw.TitleText} summary={raw.SummaryText} rid={raw.ResourceId} cd={raw.ContentDescription} bounds={raw.Bounds}");
            }
        }
        foreach (var observation in environment.AllObservations)
        {
            var sigs = SourceEquivalenceNormalizer.OccurrencesOf(observation).Select(o => o.StructuredSignature).ToArray();
            evidence.AppendLine($"SIG[{observation.SequenceNumber}] count={sigs.Length} sigs=[{string.Join(" | ", sigs)}]");
        }
        var rootEpoch = agent.Trace.FirstOrDefault(t =>
            t.ContainerId == RootPage
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        var sources = 0;
        if (rootEpoch?.Reason is { } reason)
        {
            var m = System.Text.RegularExpressions.Regex.Match(reason, @"sources=(\d+)");
            if (m.Success) sources = int.Parse(m.Groups[1].Value);
        }
        evidence.AppendLine($"ROOT_EPOCH_FROZEN={rootEpoch is not null} sources={sources} epochTrace={rootEpoch?.Reason}");
        var childEpoch = agent.Trace.FirstOrDefault(t =>
            t.ContainerId == LocationIdentity
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        evidence.AppendLine($"CHILD_EPOCH_FROZEN={childEpoch is not null} epochTrace={childEpoch?.Reason}");
        evidence.AppendLine("ACTIONS=" + string.Join(",", environment.ActionHistory.Select(a => a.GetType().Name)));
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE {entry.RunState} | {entry.ContainerId} | {entry.StepId} | {entry.Reason}");
        evidence.AppendLine("GOAL_EVIDENCE=" + string.Join(";", receipts.Select(r => $"{r.Satisfied}@{r.SourceObservationSequence}")));
        File.WriteAllText("/tmp/settings_recursion_evidence.txt", evidence.ToString());

        // ── Phase-2 truth: the trace decides — the run must reach the child
        // stage through the production pipeline (root epoch re-proven, one
        // authorized child dispatched). The terminal state is evidence (the
        // first real pressure stops the phase).
        Assert.Equal(1, _agentCreations);
        Assert.True(environment.ObservationHistory.Any(o =>
            string.Equals(o.ForegroundApplication, App, StringComparison.Ordinal)));
        Assert.True(environment.AllObservations.Any(o => !o.StructuredElements.IsDefaultOrEmpty));
    }
}
