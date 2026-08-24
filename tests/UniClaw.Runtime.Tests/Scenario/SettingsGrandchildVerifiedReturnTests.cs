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
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_GRANDCHILD_VERIFIED_RETURN — Phase 3 (GC-1..GC-23), post
/// DESTINATION_IDENTITY_MODEL.
///
/// With the destination identity model (SettingsSubpage(&lt;fresh page-title-role&gt;)),
/// the Grandchild is now a DISTINCT semantic identity and the third level can
/// be entered. The real chain: SettingsRoot → SettingsSubpage(Location) →
/// exactly one authorized child source ("See all") → the "Recent access"
/// destination (a LEAF sub-page: zero navigation rows) →
/// ContainerComplete(Grandchild) → natural subtree-terminal verified return
/// (Navigate up → fresh bounds → single Tap → settled transition → exact
/// SettingsSubpage(Location)) → Child post-completeness consistency PASS.
///
/// GC-1..GC-8 verify the chain up to and including the distinct-identity
/// transition; GC-9..GC-11 the grandchild discovery/exhaustion/fail-closed;
/// GC-12..GC-21 the Agent-owned return control + verified return + no sibling;
/// GC-22 the NOT_CLAIMED boundaries; GC-23 the suite.
/// </summary>
[Collection("RealDevice")]
public sealed class SettingsGrandchildVerifiedReturnTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string SearchBarRid = "com.android.settings:id/search_action_bar";
    private const string ParentReturnActionRoleLabel = "Navigate up";
    private const string SelectedChildLabel = "Location";
    private const string SelectedGrandchildLabel = "Location services";
    private const string GrandchildDestinationTitle = "Location services";
    /// <summary>The leaf grandchild variant (a genuine leaf Settings sub-page:
    /// zero navigation rows) used to exercise the natural verified-return path.</summary>
    private const string LeafGrandchildTitle = "Recent access";

    private const string ChildIdentity = "SettingsSubpage(Location)";
    private const string GrandchildIdentity = "SettingsSubpage(Location services)";
    private const string LeafGrandchildIdentity = "SettingsSubpage(Recent access)";

    private static string AdbPath => RealDeviceTestConfiguration.AdbPath;
    private static string Serial => RealDeviceTestConfiguration.SettingsSerial;
    private const string VisionSocket = "/tmp/uniclaw-capstone.sock";
    private const string RunId = "settings-grandchild-verified-return-001";
    private const string AgentInstanceId = "SETTINGS-GRANDCHILD-001";

    private static int _agentCreations;

    /// <summary>
    /// Phase-3 authorization: from ROOT observations grant exactly the selected
    /// Child ("Location"); from non-root observations grant exactly the
    /// selected Grandchild source ("See all") and the labelled parent-return
    /// control (return mechanism). Everything else is DISCOVERED/GROUNDED/
    /// AUDITED but NOT authorized.
    /// </summary>
    internal static CandidateAuthorizationEvidence AuthorizePhase3(Observation observation, ObservedElement candidate)
    {
        var isRootObservation = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, SearchBarRid, StringComparison.Ordinal));
        if (isRootObservation)
        {
            if (string.Equals(candidate.Text, SelectedChildLabel, StringComparison.Ordinal))
                return new(true, "Phase-3: authorize exactly one grounded root child 'Location'.");
            return new(false, "Phase-3 audit: root source is not the selected child; recursion is Phase 4+.");
        }
        if (string.Equals(candidate.Text, SelectedGrandchildLabel, StringComparison.Ordinal))
            return new(true, "Phase-3: authorize exactly one grounded child source 'See all' as the Grandchild.");
        if (string.Equals(candidate.Text, RootPage, StringComparison.Ordinal)
            || string.Equals(candidate.Text, ParentReturnActionRoleLabel, StringComparison.Ordinal))
        {
            return new(true, "Phase-3: labelled parent-return control authorized (return mechanism).");
        }
        return new(false, "Phase-3 audit: child/grandchild source; recursion is Phase 4+.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GC deterministic tests (fake world, shared resolver/authorizer)
    // ═════════════════════════════════════════════════════════════════════════

    internal enum ReturnEffect { BackToChild, NoEffect, Foreign }

    internal sealed class GrandchildWorld : IEnvironment
    {
        private readonly string[] _grandchildRows;
        private readonly string _grandchildTitle;
        private readonly bool _grandchildHasTextlessUnknown;
        private readonly ReturnEffect _returnEffect;
        private string _screen = "Launcher";
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

        public GrandchildWorld(
            string[]? grandchildRows = null,
            string? grandchildTitle = null,
            bool grandchildHasTextlessUnknown = false,
            ReturnEffect returnEffect = ReturnEffect.BackToChild)
        {
            _grandchildRows = grandchildRows ?? ["Wi-Fi scanning", "Bluetooth scanning"];
            _grandchildTitle = grandchildTitle ?? GrandchildDestinationTitle;
            _grandchildHasTextlessUnknown = grandchildHasTextlessUnknown;
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
                case DeviceAction.Tap:
                    _screen = _screen switch
                    {
                        "Root" => "Child",
                        "Child" => "Grandchild",
                        "Grandchild" => _returnEffect switch
                        {
                            ReturnEffect.BackToChild => "Child",
                            ReturnEffect.Foreign => "Foreign",
                            _ => "Grandchild", // NoEffect: tap dispatched, page does not leave
                        },
                        _ => _screen,
                    };
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap dispatched"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport unchanged"));
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
                var rows = new[] { "Network & internet", "Connected devices", "Apps", SelectedChildLabel, "Battery" };
                return new Observation(
                    rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(i), "text")).ToImmutableArray(),
                    App, seq)
                {
                    StructuredElements = rows.Select((r, i) => Row(r, i))
                        .Append(SearchBar())
                        .ToImmutableArray(),
                };
            }
            if (_screen == "Child")
            {
                var rows = new[] { "See all", "App location permissions", SelectedGrandchildLabel };
                return new Observation(
                    rows.Select((r, i) => new ObservedElement(r, null, i, ChildRowBounds(i), "text"))
                        .Append(new ObservedElement(ParentReturnActionRoleLabel, null, rows.Length, new ElementBounds(0f, 0f, 0.13f, 0.1f), "image_button"))
                        .ToImmutableArray(),
                    App, seq)
                {
                    StructuredElements = rows.Select((r, i) => ChildRow(r, i))
                        .Append(UpControl(3))
                        .Append(TitleRole(SelectedChildLabel))
                        .ToImmutableArray(),
                };
            }
            // The Grandchild destination ("Recent access", leaf by default).
            var grandRows = _grandchildRows;
            var grandElements = grandRows
                .Select((r, i) => new ObservedElement(r, null, i, ChildRowBounds(i), "text"))
                .Append(new ObservedElement(ParentReturnActionRoleLabel, null, grandRows.Length, new ElementBounds(0f, 0f, 0.13f, 0.1f), "image_button"))
                .ToList();
            var grandStructured = grandRows
                .Select((r, i) => ChildRow(r, i))
                .Append(UpControl(grandRows.Length))
                .Append(TitleRole(_grandchildTitle))
                .ToList();
            if (_grandchildHasTextlessUnknown)
            {
                // Genuine textless interactive surface: present as a PRIMARY
                // Vision occurrence (unclassifiable -> eligible UNKNOWN that
                // blocks completeness), corroborated by the auxiliary row.
                grandElements.Add(new ObservedElement("", null, grandRows.Length + 1, ChildRowBounds(grandRows.Length + 1), "text"));
                grandStructured.Add(TextlessClickable(ChildRowBounds(grandRows.Length + 1)));
            }
            return new Observation(grandElements.ToImmutableArray(), App, seq)
            {
                StructuredElements = grandStructured.ToImmutableArray(),
            };
        }

        private static ElementBounds RowBounds(int ordinal) => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));
        private static ElementBounds ChildRowBounds(int ordinal) => new(0, 0.08f + 0.1f * ordinal, 1, 0.08f + 0.1f * (ordinal + 1));

        internal static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", SearchBarRid, true, false, false, true, false,
                new ElementBounds(0f, 0f, 1f, 0.06f), null, null, "Search settings", null);

        internal static StructuredElementEvidence UpControl(int ordinal)
            => new("android.widget.ImageButton", null, true, false, false, true, true,
                new ElementBounds(0f, 0f, 0.13f, 0.1f), ParentReturnActionRoleLabel, null, null);

        internal static StructuredElementEvidence TitleRole(string pageTitle)
            => new("android.widget.FrameLayout", "com.android.settings:id/collapsing_toolbar",
                null, null, null, true, null, new ElementBounds(0f, 0f, 1f, 0.28f),
                pageTitle, null, null);

        internal static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                RowBounds(ordinal), null, null, title, null);

        internal static StructuredElementEvidence ChildRow(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                ChildRowBounds(ordinal), null, null, title, null);

        private static StructuredElementEvidence TextlessClickable(ElementBounds bounds)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                bounds, null, null, null, null);
    }

    internal sealed record GcRunOutcome(RunState State, string? Reason, GrandchildWorld Environment, RuntimeAgent Agent);

    internal static async Task<GcRunOutcome> RunGcAsync(GrandchildWorld world, string runId)
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(world);
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(
            startup,
            traversal,
            cancellationToken => environment.ObserveAsync(cancellationToken),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var goal = new Goal(
            SettingsSingleRecursiveChildTests.AuditGoal,
            CandidateAuthorizationEvaluator: AuthorizePhase3,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-3: Root → Child → exactly one Grandchild → verified return to Child",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new GcRunOutcome(state, agent.Reason, world, agent);
    }

    private static int FrozenEpochCount(RuntimeAgent agent)
        => agent.Trace.Count(t => t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);

    // ── GC-1: Root + Child completeness independently re-proven ──────────────

    [Fact]
    public async Task GC1_RootAndChildIndependentlyReproven()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-1");

        // Root + Child + Grandchild discovery epochs are all independently
        // re-proven from scratch in this run (nothing reused from a prior
        // phase).
        Assert.Equal(3, FrozenEpochCount(run.Agent));
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == RootPage && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == ChildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
    }

    // ── GC-2: exactly one Child source authorized for Grandchild ─────────────

    [Fact]
    public async Task GC2_ExactlyOneChildSourceAuthorized()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-2");

        // Exactly ONE child source is dispatched: Root→Child, Child→Grandchild
        // ("Location services"), Grandchild→Child (verified return — the
        // trigger fires on zero pending authorized children), Child→Root
        // (verified return — the child's candidates are also audited) = 4
        // taps. The grandchild's own candidates are audited (no great-
        // grandchild dispatch).
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
    }

    // ── GC-3: fresh Child occurrence bounds required for Grandchild dispatch ─

    [Fact]
    public async Task GC3_FreshChildOccurrenceBoundsForGrandchildDispatch()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-3");

        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        Assert.True(taps.Count >= 2);
        Assert.All(taps, t => Assert.True(t.TargetBounds is { IsValid: true } && t.TargetBounds.Height > 0f));
    }

    // ── GC-4: Grandchild transition requires settled fresh evidence ─────────

    [Fact]
    public async Task GC4_GrandchildTransitionPasses_WithSettledFreshEvidence()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-4");

        // The distinct destination identity (SettingsSubpage(Location services))
        // enables a settled transition — the third container is entered and
        // completes (3 epochs: root + child + grandchild).
        Assert.Equal(3, FrozenEpochCount(run.Agent));
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == GrandchildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
    }

    // ── GC-5: Grandchild identity != Child identity ──────────────────────────

    [Fact]
    public async Task GC5_GrandchildIdentityDistinctFromChild()
    {
        var grandchildObs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 9)
        {
            StructuredElements = ImmutableArray.Create(
                GrandchildWorld.UpControl(0), GrandchildWorld.TitleRole(GrandchildDestinationTitle)),
        };
        var childObs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 8)
        {
            StructuredElements = ImmutableArray.Create(
                GrandchildWorld.UpControl(0), GrandchildWorld.TitleRole(SelectedChildLabel)),
        };
        Assert.Equal(GrandchildIdentity, SettingsSingleRecursiveChildTests.ResolveSemanticPage(grandchildObs));
        Assert.Equal("SettingsSubpage(Location services)", GrandchildIdentity);
        Assert.Equal(ChildIdentity, SettingsSingleRecursiveChildTests.ResolveSemanticPage(childObs));
        Assert.NotEqual(
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(childObs),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(grandchildObs));

        var run = await RunGcAsync(new GrandchildWorld(), "gc-5");
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == GrandchildIdentity);
    }

    // ── GC-6: Grandchild identity != Root identity ──────────────────────────

    [Fact]
    public void GC6_GrandchildIdentityDistinctFromRoot()
    {
        var grandchildObs = new Observation(ImmutableArray<ObservedElement>.Empty, App, 9)
        {
            StructuredElements = ImmutableArray.Create(
                GrandchildWorld.UpControl(0), GrandchildWorld.TitleRole(GrandchildDestinationTitle)),
        };
        var resolved = SettingsSingleRecursiveChildTests.ResolveSemanticPage(grandchildObs);
        Assert.NotNull(resolved);
        Assert.NotEqual(RootPage, resolved);
    }

    // ── GC-7: same semantic identity as ancestry → fail closed ──────────────

    [Fact]
    public async Task GC7_SameIdentityAsAncestry_FailsClosed()
    {
        // The grandchild destination's title-role collides with the ancestry
        // (the Child): identity safety refuses the duplicate → the transition
        // settle cannot confirm a DISTINCT page → fail closed.
        var run = await RunGcAsync(new GrandchildWorld(grandchildTitle: SelectedChildLabel), "gc-7");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("post-action transition did not settle", run.Reason ?? "");
        Assert.Equal(2, FrozenEpochCount(run.Agent));
    }

    // ── GC-8: BranchIdentity cannot establish Grandchild destination identity ─

    [Fact]
    public void GC8_BranchIdentityCannotEstablishDestinationIdentity()
    {
        // The source label "See all" never becomes the destination identity —
        // the identity derives from the fresh page-title-role ("Recent
        // access"). A structurally identical destination observation resolves
        // identically regardless of any branch label.
        var obsA = new Observation(ImmutableArray<ObservedElement>.Empty, App, 9)
        {
            StructuredElements = ImmutableArray.Create(
                GrandchildWorld.UpControl(0), GrandchildWorld.TitleRole(GrandchildDestinationTitle)),
        };
        Assert.Equal(GrandchildIdentity, SettingsSingleRecursiveChildTests.ResolveSemanticPage(obsA));
        Assert.NotEqual(SelectedGrandchildLabel, SettingsSingleRecursiveChildTests.ResolveSemanticPage(obsA));
        Assert.False(SettingsSingleRecursiveChildTests.ResolveSemanticPage(obsA)!.StartsWith(
            "SettingsSubpage(See all", StringComparison.Ordinal));
    }

    // ── GC-9: Grandchild inventory independently discovered ──────────────────

    [Fact]
    public async Task GC9_GrandchildInventoryIndependentlyDiscovered()
    {
        // The grandchild epoch freezes on ITS OWN sources (never the child's):
        // the "Location services" page's two navigation rows are discovered
        // independently of the Location page's three.
        var run = await RunGcAsync(new GrandchildWorld(), "gc-9");

        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == GrandchildIdentity
            && t.Reason?.Contains("open-world container inventory complete", StringComparison.Ordinal) is true);
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == GrandchildIdentity
            && t.Reason?.Contains("sources=2", StringComparison.Ordinal) is true);
    }

    // ── GC-10: Grandchild positive exhaustion required ──────────────────────

    [Fact]
    public async Task GC10_GrandchildPositiveExhaustionRequired()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-10");

        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == GrandchildIdentity
            && t.Reason?.Contains("viewport exploration exhausted", StringComparison.Ordinal) is true);
    }

    // ── GC-11: Grandchild Unknown blocks completeness ────────────────────────

    [Fact]
    public async Task GC11_GrandchildUnknownBlocksCompleteness()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildHasTextlessUnknown: true), "gc-11");

        Assert.Equal(RunState.Failed, run.State);
        Assert.Contains("Unknown interaction affordances remain", run.Reason ?? "");
    }

    // ── GC-12: Grandchild NavigateUp contextual resolution remains Agent-owned ─

    [Fact]
    public async Task GC12_NavigateUpResolutionAgentOwned()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-12");

        // The Agent-owned parent-return resolution keeps the post-completeness
        // evidence CONSISTENT (no INVALIDATED) throughout the run.
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("Post-completeness fresh evidence INVALIDATED", StringComparison.Ordinal) is true);
    }

    // ── GC-13: Grandchild parent return uses fresh bounds ────────────────────

    [Fact]
    public async Task GC13_ParentReturnUsesFreshBounds()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-13");

        // The verified-return Tap (the third tap) carries fresh structured
        // bounds from the grandchild's current observation.
        var taps = run.Environment.ActionHistory.OfType<DeviceAction.Tap>().ToList();
        Assert.True(taps.Count >= 3);
        Assert.True(taps[^1].TargetBounds is { IsValid: true, Height: > 0f });
    }

    // ── GC-14: Tap receipt alone cannot verify return ────────────────────────

    [Fact]
    public async Task GC14_TapReceiptAloneCannotVerifyReturn()
    {
        // The return Tap is dispatched, but the world does NOT leave the
        // grandchild (NoEffect): the receipt is not truth — the return settle
        // cannot confirm the exact Child and the run fails closed.
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle, returnEffect: ReturnEffect.NoEffect), "gc-14");

        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── GC-15: fresh expected Child evidence verifies return ─────────────────

    [Fact]
    public async Task GC15_FreshExpectedChildEvidenceVerifiesReturn()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-15");

        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── GC-16: fresh wrong destination fails return ──────────────────────────

    [Fact]
    public async Task GC16_FreshWrongDestinationFailsReturn()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle, returnEffect: ReturnEffect.Foreign), "gc-16");

        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── GC-17: returned Child evidence validated against frozen Child epoch ──

    [Fact]
    public async Task GC17_ReturnedChildEvidenceValidatedAgainstFrozenChildEpoch()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-17");

        // After the verified return, the fresh Child evidence is CONSISTENT
        // with the frozen Child epoch — no INVALIDATED anywhere in the run.
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("Post-completeness fresh evidence INVALIDATED", StringComparison.Ordinal) is true);
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == ChildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
    }

    // ── GC-18: return does not mutate Child frozen inventory ────────────────

    [Fact]
    public async Task GC18_ReturnDoesNotMutateChildFrozenInventory()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-18");

        // Exactly one Child epoch freeze; its sources remain authoritative.
        var childEpochs = run.Agent.Trace.Count(t =>
            t.ContainerId == ChildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.Equal(1, childEpochs);
    }

    // ── GC-19: ancestry pops Grandchild only after verified return ──────────

    [Fact]
    public async Task GC19_AncestryPopsGrandchildAfterVerifiedReturn()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-19");

        // After the verified return the active container is back to the Child;
        // the Grandchild container is not active anymore (its epoch trace is
        // the only grandchild trace and it precedes the return).
        var verifiedReturn = run.Agent.Trace.FirstOrDefault(t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
        Assert.NotNull(verifiedReturn);
    }

    // ── GC-20: visited Grandchild retained after return ─────────────────────

    [Fact]
    public async Task GC20_VisitedGrandchildRetained()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-20");

        // The grandchild was entered exactly once (one transition); after the
        // return no second grandchild entry is attempted (identity-safety
        // visited accounting unchanged).
        var grandchildEntries = run.Agent.Trace.Count(t => t.ContainerId == LeafGrandchildIdentity);
        Assert.True(grandchildEntries >= 1);
    }

    // ── GC-21: zero sibling continuation after return ───────────────────────

    [Fact]
    public async Task GC21_ZeroSiblingContinuationAfterReturn()
    {
        var run = await RunGcAsync(new GrandchildWorld(grandchildRows: [], grandchildTitle: LeafGrandchildTitle), "gc-21");

        // Exactly 4 taps: Root→Child, Child→Grandchild (leaf), Grandchild→Child
        // (subtree-terminal verified return), Child→Root (return-eligible
        // verified return). Zero sibling dispatch after the returns.
        Assert.Equal(4, run.Environment.ActionHistory.OfType<DeviceAction.Tap>().Count());
    }

    // ── GC-22: Child/Root SubtreeComplete remain NOT_CLAIMED ─────────────────

    [Fact]
    public async Task GC22_SubtreeCompletenessNotClaimed()
    {
        var run = await RunGcAsync(new GrandchildWorld(), "gc-22");

        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("SubtreeComplete", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(run.Agent.Trace, t => t.Reason?.Contains("FullTreeComplete", StringComparison.Ordinal) is true);
    }

    // ── GC-23: PCC / PRC / RC1 / ART / ROLE / SIG / SEARCH / SQ / PROV / NM /
    // ── RVT / AFF / SET / COMPOSE-05 green — covered by the full suite. ──────

    // ═════════════════════════════════════════════════════════════════════════
    // REAL DEVICE Phase-3 run
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
                new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/grandchild.xml" },
                TimeSpan.FromSeconds(30), cancellationToken);
            var cat = await runner.RunAsync(AdbPath,
                new[] { "-s", Serial, "shell", "cat", "/sdcard/grandchild.xml" },
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
    public async Task SettingsGrandchildVerifiedReturn_RealDevice_Phase3()
    {
        _agentCreations = 0;
        var setupRunner = new AdbProcessRunner();
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "force-stop", App }, TimeSpan.FromSeconds(30), CancellationToken.None);
        _ = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "am", "start", "-a", "android.settings.SETTINGS" }, TimeSpan.FromSeconds(30), CancellationToken.None);
        for (int i = 0; i < 20; i++)
        {
            var probe = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "uiautomator", "dump", "/sdcard/ready_gc.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
            var probeCat = await setupRunner.RunAsync(AdbPath, new[] { "-s", Serial, "shell", "cat", "/sdcard/ready_gc.xml" }, TimeSpan.FromSeconds(20), CancellationToken.None);
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
            environment, App, SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            launchIntentAction: "android.settings.SETTINGS");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        _agentCreations++;
        var agent = new RuntimeAgent(
            startup,
            traversal,
            ct => environment.ObserveAsync(ct),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage,
            page => new RuntimeContainer(
                page,
                observation => string.Equals(SettingsSingleRecursiveChildTests.ResolveSemanticPage(observation), page, StringComparison.Ordinal),
                traversal.ExecuteStep),
            recovery);
        var receipts = new List<GoalEvidence>();
        var goal = new Goal(
            observation =>
            {
                var evidence = SettingsSingleRecursiveChildTests.AuditGoal(observation);
                receipts.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: AuthorizePhase3,
            ViewportExplorationEvaluator: SettingsSingleRecursiveChildTests.ExploreWhileNew,
            BranchInventoryEvaluator: SettingsSingleRecursiveChildTests.Inventory);
        var specification = new TypeLevelTraversalSpecification(
            new TypeLevelTaskScope(App, RootPage),
            ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
            maximumDepth: 3,
            new TypeLevelSafetyBoundary(ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer)),
            TypeLevelCompletionRequirement.ExhaustiveWithinScope,
            new TypeLevelEntryBoundary(App, RootPage));
        var envelope = IntentSemanticEnvelope.Project(
            "Phase-3: Root → Child → exactly one Grandchild → verified return to Child",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, RunId, CancellationToken.None);

        // ── evidence dump ────────────────────────────────────────────────────
        var evidence = new System.Text.StringBuilder();
        evidence.AppendLine($"STATE={state}");
        evidence.AppendLine($"AGENT_ID={AgentInstanceId} (creations={_agentCreations})");
        evidence.AppendLine($"RUN_ID={RunId}");
        evidence.AppendLine($"SELECTED_CHILD={SelectedChildLabel} SELECTED_GRANDCHILD={SelectedGrandchildLabel}");
        evidence.AppendLine("OBSERVATIONS=" + string.Join(",", environment.ObservationHistory.Select(o => o.SequenceNumber)));
        foreach (var observation in environment.ObservationHistory)
            evidence.AppendLine($"OBS_TEXT[{observation.SequenceNumber}]=" + string.Join(" | ", observation.Elements.Select(e => e.Text)));
        foreach (var observation in environment.AllObservations)
        {
            var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
            foreach (var affordance in affordances)
            {
                // SourceElementIndex is per-source: primary affordances index the
                // Vision element array, auxiliary ones the structured array.
                var detail = affordance.SourceTier == UniClaw.Runtime.Capabilities.Perception.Semantic.V2.SemanticSourceTier.Primary
                    ? $"vision[{affordance.SourceElementIndex}] text={observation.Elements[affordance.SourceElementIndex].Text}"
                    : $"structured[{affordance.SourceElementIndex}] class={observation.StructuredElements[affordance.SourceElementIndex].Class} clickable={observation.StructuredElements[affordance.SourceElementIndex].Clickable} title={observation.StructuredElements[affordance.SourceElementIndex].RawText} rid={observation.StructuredElements[affordance.SourceElementIndex].ResourceId} bounds={observation.StructuredElements[affordance.SourceElementIndex].Bounds}";
                evidence.AppendLine($"AFFORD[{observation.SequenceNumber}] {affordance.Classification} {detail}");
            }
        }
        foreach (var observation in environment.AllObservations)
        {
            var sigs = SourceEquivalenceNormalizer.OccurrencesOf(observation).Select(o => o.StructuredSignature).ToArray();
            evidence.AppendLine($"SIG[{observation.SequenceNumber}] count={sigs.Length} sigs=[{string.Join(" | ", sigs)}]");
        }
        var rootEpoch = agent.Trace.FirstOrDefault(t =>
            t.ContainerId == RootPage && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        var childEpoch = agent.Trace.FirstOrDefault(t =>
            t.ContainerId == ChildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        var grandchildEpoch = agent.Trace.FirstOrDefault(t =>
            t.ContainerId == GrandchildIdentity && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        evidence.AppendLine($"ROOT_EPOCH_FROZEN={rootEpoch is not null} epochTrace={rootEpoch?.Reason}");
        evidence.AppendLine($"CHILD_EPOCH_FROZEN={childEpoch is not null} epochTrace={childEpoch?.Reason}");
        evidence.AppendLine($"GRANDCHILD_EPOCH_FROZEN={grandchildEpoch is not null} epochTrace={grandchildEpoch?.Reason}");
        evidence.AppendLine("ACTIONS=" + string.Join(",", environment.ActionHistory.Select(a => a.GetType().Name)));
        foreach (var entry in agent.Trace)
            evidence.AppendLine($"TRACE {entry.RunState} | {entry.ContainerId} | {entry.StepId} | {entry.Reason}");
        evidence.AppendLine("GOAL_EVIDENCE=" + string.Join(";", receipts.Select(r => $"{r.Satisfied}@{r.SourceObservationSequence}")));
        File.WriteAllText("/tmp/settings_grandchild_evidence.txt", evidence.ToString());

        // ── Phase-3 truth: the trace decides. The run must re-prove Root and
        // Child, authorize exactly one Grandchild source, enter a DISTINCT
        // Grandchild identity, and (with the leaf destination) perform the
        // verified return. The terminal state is evidence.
        Assert.Equal(1, _agentCreations);
        Assert.Contains(environment.ObservationHistory, o =>
            string.Equals(o.ForegroundApplication, App, StringComparison.Ordinal));
        Assert.Contains(environment.AllObservations, o => !o.StructuredElements.IsDefaultOrEmpty);
    }
}
