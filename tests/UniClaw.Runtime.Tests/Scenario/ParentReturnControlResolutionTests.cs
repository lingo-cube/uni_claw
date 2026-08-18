using System.Collections.Immutable;
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
/// SETTINGS_PARENT_RETURN_CONTROL_RESOLUTION — PRC-1..PRC-14.
///
/// The Agent-owned contextual parent-return resolution supports two evidence
/// kinds:
///   A. destination-labelled return: TitleText == the expected parent page
///      (fixture-style "Return" control — unchanged);
///   B. action-role return: the stable Android toolbar Up control
///      (content-desc "Navigate up", ImageButton, NO TitleText) — resolved
///      contextually as PARENT_RETURN_CONTROL when a known parent exists, the
///      candidate is unique/interactive/fresh-actionable and authorization
///      PASSES. ContentDescription is PARENT_RETURN_ACTION_LABEL_EVIDENCE only
///      — never PageIdentity / SourceIdentity / DestinationIdentity.
/// The InteractionAffordanceAnalyzer is FROZEN (an ImageButton remains UNKNOWN
/// context-free); the resolved control never enters the child inventory /
/// occurrences / RequiredChildren / unresolved-Unknown count. Ordinary UNKNOWNs
/// keep blocking completeness. Tap receipt alone is never parent-return truth:
/// the return is verified only by the fresh post-action reconciliation.
/// PRC-14 (no regression) is the full deterministic suite.
/// </summary>
public sealed class ParentReturnControlResolutionTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string ChildPage = SettingsSingleRecursiveChildTests.LocationIdentity;
    private const string SelectedChildLabel = "Location";

    private enum BackKind { None, RealUp, FixtureLabel, MoreOptions }
    private enum ReturnEffect { BackToRoot, NoEffect, Foreign }

    // ── PRC world ────────────────────────────────────────────────────────────

    private sealed class PrcWorld : IEnvironment
    {
        private readonly string[][] _rootViewports;
        private readonly string[] _childRows;
        private readonly BackKind _backKind;
        private readonly bool _backInvalidBounds;
        private readonly int _backCount;
        private readonly bool _rootHasUpControl;
        private readonly bool _childHasTextlessUnknown;
        private readonly ReturnEffect _returnEffect;
        private string _screen = "Launcher";
        private int _rootViewport;
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public PrcWorld(
            string[][] rootViewports,
            string[] childRows,
            BackKind backKind = BackKind.RealUp,
            bool backInvalidBounds = false,
            int backCount = 1,
            bool rootHasUpControl = false,
            bool childHasTextlessUnknown = false,
            ReturnEffect returnEffect = ReturnEffect.BackToRoot)
        {
            _rootViewports = rootViewports;
            _childRows = childRows;
            _backKind = backKind;
            _backInvalidBounds = backInvalidBounds;
            _backCount = backCount;
            _rootHasUpControl = rootHasUpControl;
            _childHasTextlessUnknown = childHasTextlessUnknown;
            _returnEffect = returnEffect;
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
                case DeviceAction.ScrollBackward:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport unchanged"));
                case DeviceAction.Tap:
                    if (_screen == "Root")
                        _screen = "Child";
                    else if (_screen == "Child")
                    {
                        _screen = _returnEffect switch
                        {
                            ReturnEffect.BackToRoot => "Root",
                            ReturnEffect.Foreign => "Foreign",
                            _ => "Child", // NoEffect: the tap is dispatched but the page does NOT leave
                        };
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
                var structured = rows.Select((r, i) => Row(r, i)).Append(SearchBar()).ToImmutableArray();
                if (_rootHasUpControl)
                    structured = structured.Add(BackControl(BackKind.RealUp, RowBounds(rows.Length), _backInvalidBounds));
                return new Observation(elements, App, seq) { StructuredElements = structured };
            }
            // Child page.
            var childElements = _childRows
                .Select((r, i) => new ObservedElement(r, null, i, ChildRowBounds(i), "text")).ToImmutableArray();
            var childStructured = _childRows
                .Select((r, i) => ChildRow(r, i))
                .ToList();
            // The REAL Settings child page always carries the toolbar Up
            // control (the page-class marker); the variant under test is
            // modeled on top of it — except the fixture-style child, which
            // carries ONLY the destination-labelled return anchor.
            switch (_backKind)
            {
                case BackKind.RealUp:
                    for (int i = 0; i < _backCount; i++)
                        childStructured.Add(BackControl(BackKind.RealUp, ChildRowBounds(_childRows.Length), _backInvalidBounds));
                    break;
                case BackKind.MoreOptions:
                    childStructured.Add(BackControl(BackKind.RealUp, ChildRowBounds(_childRows.Length), false));
                    childStructured.Add(BackControl(BackKind.MoreOptions, ChildRowBounds(_childRows.Length + 1), false));
                    break;
                case BackKind.FixtureLabel:
                    childStructured.Add(BackControl(BackKind.FixtureLabel, ChildRowBounds(_childRows.Length), false));
                    break;
            }
            // PAGE-TITLE-ROLE: the child sub-page carries its toolbar title.
            childStructured.Add(SettingsSingleRecursiveChildTests.RecursionWorld.TitleRole("Location"));
            if (_childHasTextlessUnknown)
                childStructured.Add(TextlessClickable(ChildRowBounds(_childRows.Length + 2)));
            return new Observation(childElements, App, seq)
            {
                StructuredElements = childStructured.ToImmutableArray(),
            };
        }

        private static StructuredElementEvidence BackControl(BackKind kind, ElementBounds bounds, bool invalidBounds)
        {
            var actualBounds = invalidBounds ? null : bounds;
            switch (kind)
            {
                case BackKind.FixtureLabel:
                    return new("android.widget.Button", null, true, false, false, true, true,
                        actualBounds, RootPage, null, false, null, null);
                case BackKind.MoreOptions:
                    return new("android.widget.ImageButton", null, true, false, false, true, true,
                        actualBounds, null, null, false, "More options", null);
                default: // RealUp
                    return new("android.widget.ImageButton", null, true, false, false, true, true,
                        actualBounds, null, null, false, "Navigate up", null);
            }
        }

        private static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", "com.android.settings:id/search_action_bar",
                true, false, false, true, false, new ElementBounds(0f, 0f, 1f, 0.06f),
                "Search settings", null, null, null, null);

        private static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                RowBounds(ordinal), title, null, false, null, null);

        private static StructuredElementEvidence ChildRow(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                ChildRowBounds(ordinal), title, null, false, null, null);

        private static StructuredElementEvidence TextlessClickable(ElementBounds bounds)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                bounds, null, null, false, null, null);

        private static ElementBounds RowBounds(int ordinal) => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));
        private static ElementBounds ChildRowBounds(int ordinal) => new(0, 0.08f + 0.1f * ordinal, 1, 0.08f + 0.1f * (ordinal + 1));
    }

    private sealed record RunOutcome(RunState State, string? Reason, PrcWorld Environment, RuntimeAgent Agent);

    private static async Task<RunOutcome> RunFakeAsync(PrcWorld world, string runId)
    {
        var traversal = new RuntimeTraversal(world);
        var startup = new RuntimeStartup(world, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(world, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => world.ObserveAsync(cancellationToken),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var goal = new Goal(
            SettingsSingleRecursiveChildTests.AuditGoal,
            CandidateAuthorizationEvaluator: SettingsSingleRecursiveChildTests.AuthorizePhase2,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 2,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "PRC: contextual parent-return resolution",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new RunOutcome(state, agent.Reason, world, agent);
    }

    private static string[][] SingleViewportRoot() => [["Network & internet", "Connected devices", "Apps", SelectedChildLabel, "Battery"]];

    private static string[] ChildRows() => ["Location services", "App location permissions", "Recent location requests"];

    private static bool HasChildEpoch(RunOutcome run)
        => run.Agent.Trace.Any(t =>
            t.ContainerId == ChildPage
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);

    private static int ChildEpochSources(RunOutcome run)
    {
        var epoch = run.Agent.Trace.FirstOrDefault(t =>
            t.ContainerId == ChildPage
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        if (epoch?.Reason is not { } reason)
            return -1;
        var m = System.Text.RegularExpressions.Regex.Match(reason, @"sources=(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }

    // ── PRC-1: known parent + unique ImageButton cd="Navigate up" → resolved ─

    [Fact]
    public async Task PRC1_RealUpControl_ResolvedParentReturnControl()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows()), "prc-1");

        Assert.True(HasChildEpoch(run), $"child epoch should freeze; reason={run.Reason}");
    }

    // ── PRC-2: same element, no known parent → UNKNOWN / fail closed ─────────

    [Fact]
    public async Task PRC2_NoKnownParent_UpControlStaysUnknown_FailsClosed()
    {
        // The ROOT container has no parent: an Up control there cannot be a
        // parent-return control — it stays a genuine UNKNOWN and blocks the
        // root completeness.
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows(), rootHasUpControl: true), "prc-2");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
        Assert.False(HasChildEpoch(run));
    }

    // ── PRC-3: generic ImageButton cd="More options" → UNKNOWN ───────────────

    [Fact]
    public async Task PRC3_GenericImageButton_StaysUnknown()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows(), backKind: BackKind.MoreOptions), "prc-3");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
        Assert.False(HasChildEpoch(run));
    }

    // ── PRC-4: two valid parent-return candidates → ambiguous / fail closed ──

    [Fact]
    public async Task PRC4_AmbiguousCandidates_FailClosed()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows(), backCount: 2), "prc-4");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
        Assert.False(HasChildEpoch(run));
    }

    // ── PRC-5: Navigate-up candidate with invalid bounds → not actionable ────

    [Fact]
    public async Task PRC5_InvalidBounds_NotActionable_FailsClosed()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows(), backInvalidBounds: true), "prc-5");

        // The invalid-bounds Up control is interaction-relevant with no
        // actionable bounds: the post-scroll evidence-quality settle treats the
        // capture as provisional and fail-closes (Unresolved) before
        // completeness — the candidate is NOT actionable, the child never
        // completes. (In the real pipeline the eligibility admission would
        // exclude such a node from the structured channel entirely.)
        Assert.True(run.State == RunState.Failed, $"state={run.State} reason={run.Reason}");
        Assert.True(run.Reason?.Contains("did not prove positive exhaustion") is true, $"reason={run.Reason}");
        Assert.False(HasChildEpoch(run));
    }

    // ── PRC-6: resolved Up control excluded from child inventory ─────────────

    [Fact]
    public async Task PRC6_ResolvedUpControl_ExcludedFromChildInventory()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows()), "prc-6");

        // The child epoch contains ONLY the 3 navigation rows — the resolved
        // Up control produces no NavigationSourceOccurrence and never enters
        // the logical-source inventory.
        Assert.Equal(3, ChildEpochSources(run));
    }

    // ── PRC-7: ordinary UNKNOWN still blocks completeness ────────────────────

    [Fact]
    public async Task PRC7_OrdinaryUnknown_StillBlocksCompleteness()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows(), childHasTextlessUnknown: true), "prc-7");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
        Assert.False(HasChildEpoch(run));
    }

    // ── PRC-8: fixture TitleText==parent resolution remains supported ────────

    [Fact]
    public async Task PRC8_FixtureDestinationLabelledReturn_StillSupported()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), ChildRows(), backKind: BackKind.FixtureLabel), "prc-8");

        Assert.True(HasChildEpoch(run), $"fixture return should still resolve; reason={run.Reason}");
    }

    // ── PRC-9: analyzer unchanged — ImageButton stays context-free UNKNOWN ───

    [Fact]
    public void PRC9_AnalyzerUnchanged_ImageButtonContextFreeUnknown()
    {
        var upElement = PrcBackControlElement();
        var obs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 1)
        {
            StructuredElements = ImmutableArray.Create(upElement),
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(obs);
        Assert.Single(affordances);
        Assert.Equal(InteractionAffordanceKind.Unknown, affordances[0].Classification);
    }

    private static StructuredElementEvidence PrcBackControlElement()
        => new("android.widget.ImageButton", null, true, false, false, true, true,
            new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, false, "Navigate up", null);

    // ── PRC-10: fresh structured bounds required ─────────────────────────────
    // The resolution rejects non-actionable bounds (PRC-5) and the return Tap
    // is formed from the CURRENT fresh observation's bounds (asserted in
    // PRC-12 via the world's tap record — never historical/null).

    [Fact]
    public async Task PRC10_FreshBoundsRequired_ReturnTapUsesFreshBounds()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), [], returnEffect: ReturnEffect.BackToRoot), "prc-10");

        // Leaf child → the flow performs the verified parent return; the return
        // Tap carries valid bounds from the current fresh observation.
        var returnTap = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().LastOrDefault();
        Assert.NotNull(returnTap);
        Assert.True(returnTap.TargetBounds is { IsValid: true } && returnTap.TargetBounds.Height > 0f);
    }

    // ── PRC-11: Tap receipt alone is not parent-return truth ─────────────────

    [Fact]
    public async Task PRC11_TapReceiptAlone_NotReturnTruth()
    {
        // The return Tap is dispatched, but the world does NOT leave the child
        // (NoEffect): the receipt is not truth — the return settle cannot
        // confirm the expected parent and the run fails closed.
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), [], returnEffect: ReturnEffect.NoEffect), "prc-11");

        Assert.True(run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count() >= 2,
            "the root→child tap and the return tap must both be dispatched");
        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── PRC-12: fresh expected-parent evidence → verified return PASS ────────

    [Fact]
    public async Task PRC12_VerifiedParentReturn_Pass()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), [], returnEffect: ReturnEffect.BackToRoot), "prc-12");

        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── PRC-13: fresh foreign page → return FAIL ─────────────────────────────

    [Fact]
    public async Task PRC13_ForeignPage_ReturnFails()
    {
        var run = await RunFakeAsync(new PrcWorld(SingleViewportRoot(), [], returnEffect: ReturnEffect.Foreign), "prc-13");

        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── PRC-14: Root inventory / ART / ROLE / SIG / SEARCH / SQ / PROV / NM /
    // ── RVT / AFF / SET unchanged — covered by the full deterministic suite. ─
}
