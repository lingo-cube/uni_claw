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
/// SETTINGS_POST_COMPLETENESS_CONSISTENCY — PCC-1..PCC-15.
///
/// The post-completeness consistency Validator consumes the Agent's explicit,
/// occurrence-scoped contextual dispositions (the Agent contextually resolved
/// the occurrence as PARENT_RETURN_CONTROL in the CURRENT fresh Observation)
/// instead of re-treating the resolved control as a raw UNKNOWN. The Validator
/// MUST NOT learn parent-return semantics itself; the InteractionAffordanceAnalyzer
/// stays a context-free classifier. Occurrence-scoped: only dispositions whose
/// ObservationSequence equals the fresh Observation's sequence apply — a stale
/// disposition can never whitelist a later observation's occurrence.
/// PCC-15 (no regression) is the full deterministic suite.
/// </summary>
public sealed class PostCompletenessConsistencyTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string ChildPage = SettingsSingleRecursiveChildTests.LocationIdentity;

    private static readonly string[] FrozenChildSignatures =
    [
        "Location services|android.widget.LinearLayout||",
        "App location permissions|android.widget.LinearLayout||",
        "Recent location requests|android.widget.LinearLayout||",
    ];

    // ── fixture builders ─────────────────────────────────────────────────────

    private static ContainerInventoryCompletenessEvidence FrozenChildEvidence()
    {
        var sources = FrozenChildSignatures
            .Select(sig => new ProvenLogicalSource(sig, ImmutableArray<ProvenSourceOccurrence>.Empty))
            .ToImmutableArray();
        return new ContainerInventoryCompletenessEvidence(
            ChildPage,
            ImmutableArray.Create(1L, 2L),
            FrozenChildSignatures.ToImmutableArray(),
            ExplorationExhausted: true,
            UnresolvedCandidateCount: 0,
            "frozen child epoch",
            sources);
    }

    private static Observation FreshObservation(long seq, params StructuredElementEvidence[] elements)
        => new(ImmutableArray<ObservedElement>.Empty, App, seq)
        {
            StructuredElements = elements.ToImmutableArray(),
        };

    private static StructuredElementEvidence RealUpControl(int ordinal)
        => new("android.widget.ImageButton", null, true, false, false, true, true,
            new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, false, "Navigate up", null);

    private static StructuredElementEvidence MoreOptionsButton(int ordinal)
        => new("android.widget.ImageButton", null, true, false, false, true, true,
            new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, false, "More options", null);

    private static StructuredElementEvidence NavRow(string title, int ordinal)
        => new("android.widget.LinearLayout", null, true, false, false, true, true,
            new ElementBounds(0f, 0.08f + 0.1f * ordinal, 1f, 0.08f + 0.1f * (ordinal + 1)),
            title, null, false, null, null);

    private static StructuredElementEvidence TextlessClickable(int ordinal)
        => new("android.widget.LinearLayout", null, true, false, false, true, true,
            new ElementBounds(0f, 0.08f + 0.1f * ordinal, 1f, 0.08f + 0.1f * (ordinal + 1)),
            null, null, false, null, null);

    private static ContextualInteractionDisposition ParentReturnDisposition(long seq, int elementIndex)
        => new(seq, elementIndex, ContextualInteractionDispositionKind.ParentReturnControl);

    private static PostCompletenessConsistencyValidator.ConsistencyResult Validate(
        Observation fresh,
        ImmutableArray<ContextualInteractionDisposition> dispositions)
        => PostCompletenessConsistencyValidator.Validate(
            fresh, FrozenChildEvidence(), continuityVerified: true, dispositions);

    // ── PCC-1: frozen child + fresh NavigateUp + Agent-resolved → CONSISTENT ─

    [Fact]
    public void PCC1_ResolvedNavigateUp_Consistent()
    {
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Location services", 1));
        var disposition = ImmutableArray.Create(ParentReturnDisposition(9, 0));

        var result = Validate(fresh, disposition);

        Assert.True(result.Consistent, result.Reason);
    }

    // ── PCC-2: same raw NavigateUp but no contextual resolution → INVALIDATED ─

    [Fact]
    public void PCC2_UnresolvedNavigateUp_Invalidated()
    {
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Location services", 1));

        var result = Validate(fresh, []);

        Assert.False(result.Consistent);
        Assert.Contains("UNRESOLVED interactive UNKNOWN", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-3: generic MoreOptions ImageButton → INVALIDATED ────────────────

    [Fact]
    public void PCC3_GenericMoreOptions_Invalidated()
    {
        var fresh = FreshObservation(9, MoreOptionsButton(0), NavRow("Location services", 1));

        var result = Validate(fresh, []);

        Assert.False(result.Consistent);
        Assert.Contains("UNRESOLVED interactive UNKNOWN", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-4: two NavigateUp candidates → resolution fails → INVALIDATED ────

    [Fact]
    public void PCC4_AmbiguousCandidates_Invalidated()
    {
        var fresh = FreshObservation(9, RealUpControl(0), RealUpControl(1));
        // The Agent's resolution FAILS CLOSED on ambiguity → NO disposition.
        var result = Validate(fresh, []);

        Assert.False(result.Consistent);
        Assert.Contains("UNRESOLVED interactive UNKNOWN", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-5: resolved occurrence excluded from Unknown accounting ─────────

    [Fact]
    public void PCC5_ResolvedExcluded_OrdinaryUnknownStillInvalidates()
    {
        var fresh = FreshObservation(9, RealUpControl(0), TextlessClickable(1));
        // Only the Up control is Agent-resolved; the textless row stays UNKNOWN.
        var result = Validate(fresh, ImmutableArray.Create(ParentReturnDisposition(9, 0)));

        Assert.False(result.Consistent);
        Assert.Contains("UNRESOLVED interactive UNKNOWN", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-6: resolved occurrence excluded from frozen-source mapping ──────

    [Fact]
    public void PCC6_ResolvedOccurrence_NotMappedToFrozenSource()
    {
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Location services", 1));

        // The resolved Up control produces NO NavigationSourceOccurrence.
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(fresh);
        Assert.DoesNotContain(occurrences, o => o.StructuredSignature.StartsWith("|android.widget.ImageButton", StringComparison.Ordinal));
        Assert.Equal(1, occurrences.Length);
        // And the validator does not require it to map to any frozen class.
        var result = Validate(fresh, ImmutableArray.Create(ParentReturnDisposition(9, 0)));
        Assert.True(result.Consistent, result.Reason);
    }

    // ── PCC-7: resolved occurrence does not alter frozen cardinality ────────

    [Fact]
    public void PCC7_FrozenCardinalityUnchanged()
    {
        var evidence = FrozenChildEvidence();
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Location services", 1));

        var result = Validate(fresh, ImmutableArray.Create(ParentReturnDisposition(9, 0)));

        Assert.True(result.Consistent, result.Reason);
        Assert.Equal(3, evidence.UniqueNavigationSourceIdentities.Length);
        Assert.Equal(3, evidence.ProvenLogicalSources.Length);
    }

    // ── PCC-8: fresh genuine nav candidate matching a frozen source → CONSISTENT ──

    [Fact]
    public void PCC8_MatchingFrozenSource_Consistent()
    {
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("App location permissions", 1));

        var result = Validate(fresh, ImmutableArray.Create(ParentReturnDisposition(9, 0)));

        Assert.True(result.Consistent, result.Reason);
    }

    // ── PCC-9: fresh genuine NEW navigation candidate → INVALIDATED ─────────

    [Fact]
    public void PCC9_NewNavigationSource_Invalidated()
    {
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Brand new section", 1));

        var result = Validate(fresh, ImmutableArray.Create(ParentReturnDisposition(9, 0)));

        Assert.False(result.Consistent);
        Assert.Contains("does not resolve to any proven frozen logical source", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-10: ambiguous frozen-source mapping → INVALIDATED ────────────────

    [Fact]
    public void PCC10_AmbiguousFrozenMapping_Invalidated()
    {
        // Two frozen classes with the SAME signature -> the fresh occurrence
        // maps ambiguously -> invalidated (no signature guessing).
        var ambiguousSources = ImmutableArray.Create(
            new ProvenLogicalSource("Duplicate|android.widget.LinearLayout||", ImmutableArray<ProvenSourceOccurrence>.Empty),
            new ProvenLogicalSource("Duplicate|android.widget.LinearLayout||", ImmutableArray<ProvenSourceOccurrence>.Empty));
        var evidence = FrozenChildEvidence() with { ProvenLogicalSources = ambiguousSources };
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Duplicate", 1));

        var result = PostCompletenessConsistencyValidator.Validate(
            fresh, evidence, continuityVerified: true, ImmutableArray.Create(ParentReturnDisposition(9, 0)));

        Assert.False(result.Consistent);
        Assert.Contains("maps ambiguously", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-11: disposition cannot cross ObservationSequence ────────────────

    [Fact]
    public void PCC11_DispositionCannotCrossObservationSequence()
    {
        var fresh = FreshObservation(9, RealUpControl(0), NavRow("Location services", 1));
        // The disposition references a DIFFERENT observation sequence: it must
        // NOT apply to this fresh observation's occurrence.
        var stale = ImmutableArray.Create(ParentReturnDisposition(8, 0));

        var result = Validate(fresh, stale);

        Assert.False(result.Consistent);
        Assert.Contains("UNRESOLVED interactive UNKNOWN", result.Reason, StringComparison.Ordinal);
    }

    // ── PCC-12: stale disposition cannot whitelist a fresh occurrence ───────

    [Fact]
    public void PCC12_StaleDisposition_NoWhitelist()
    {
        // Case F: a resolved occurrence from a PREVIOUS observation must NOT
        // whitelist the same-looking occurrence in the next observation.
        var previous = FreshObservation(8, RealUpControl(0), NavRow("Location services", 1));
        var next = FreshObservation(9, RealUpControl(0), NavRow("Location services", 1));

        var previousResult = Validate(previous, ImmutableArray.Create(ParentReturnDisposition(8, 0)));
        Assert.True(previousResult.Consistent, previousResult.Reason);

        // The NEXT observation has NO fresh disposition (the Agent re-resolves
        // per observation; nothing is cached): the same-looking Up control is
        // an UNRESOLVED UNKNOWN -> invalidated.
        var nextResult = Validate(next, []);
        Assert.False(nextResult.Consistent);
        Assert.Contains("UNRESOLVED interactive UNKNOWN", nextResult.Reason, StringComparison.Ordinal);
    }

    // ── PCC-13: fixture destination-labelled parent-return still works ──────

    [Fact]
    public void PCC13_FixtureDestinationLabelled_Consistent()
    {
        // A fixture-style return control (Button, TitleText == parent page) is
        // classified UNKNOWN context-free; the Agent's destination-labelled
        // resolution produces the disposition.
        var fixtureReturn = new StructuredElementEvidence(
            "android.widget.Button", null, true, false, false, true, true,
            new ElementBounds(0f, 0f, 0.13f, 0.1f), RootPage, null, false, null, null);
        var fresh = FreshObservation(9, fixtureReturn, NavRow("Location services", 1));

        var result = Validate(fresh, ImmutableArray.Create(ParentReturnDisposition(9, 0)));

        Assert.True(result.Consistent, result.Reason);
    }

    // ── PCC-14: analyzer unchanged ───────────────────────────────────────────

    [Fact]
    public void PCC14_AnalyzerUnchanged_UpControlContextFreeUnknown()
    {
        var obs = FreshObservation(9, RealUpControl(0));
        var affordances = InteractionAffordanceAnalyzer.Analyze(obs);
        Assert.Single(affordances);
        Assert.Equal(InteractionAffordanceKind.Unknown, affordances[0].Classification);
    }

    // ── PCC-15: PRC / RC1 / ART / ROLE / SIG / SEARCH / SQ / PROV / NM / RVT /
    // ── AFF / SET / COMPOSE-05 green — covered by the full deterministic
    // ── suite (and the RC1/PRC suites re-run below at the run level). ────────

    // ── End-to-end: Agent → disposition → Validator in a full run ───────────

    private sealed class PccWorld : IEnvironment
    {
        private string _screen = "Launcher";
        private long _seq;
        private readonly List<DeviceAction> _actions = [];
        private readonly List<Observation> _history = [];

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
                    _screen = _screen == "Root" ? "Child" : "Root";
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "tap", "tap dispatched"));
                default:
                    return Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "scroll", "viewport unchanged"));
            }
        }

        private Observation Build(long seq)
        {
            if (_screen == "Launcher")
                return new Observation([new ObservedElement("Launcher", null, 0, null, null)], App, seq);
            if (_screen == "Root")
            {
                var rows = new[] { "Network & internet", "Connected devices", "Apps", "Location", "Battery" };
                return new Observation(
                    rows.Select((r, i) => new ObservedElement(r, null, i, RowBounds(i), "text")).ToImmutableArray(),
                    App, seq)
                {
                    StructuredElements = rows.Select((r, i) => Row(r, i))
                        .Append(SearchBar())
                        .ToImmutableArray(),
                };
            }
            var childRows = new[] { "Location services", "App location permissions", "Recent location requests" };
            return new Observation(
                childRows.Select((r, i) => new ObservedElement(r, null, i, ChildRowBounds(i), "text")).ToImmutableArray(),
                App, seq)
            {
                StructuredElements = childRows.Select((r, i) => ChildRow(r, i))
                    .Append(RealUpControl(3))
                    .Append(SettingsSingleRecursiveChildTests.RecursionWorld.TitleRole("Location"))
                    .ToImmutableArray(),
            };
        }

        private static ElementBounds RowBounds(int ordinal) => new(0, 0.1f * ordinal, 1, 0.1f * (ordinal + 1));
        private static ElementBounds ChildRowBounds(int ordinal) => new(0, 0.08f + 0.1f * ordinal, 1, 0.08f + 0.1f * (ordinal + 1));

        internal static StructuredElementEvidence SearchBar()
            => new("android.view.ViewGroup", "com.android.settings:id/search_action_bar",
                true, false, false, true, false, new ElementBounds(0f, 0f, 1f, 0.06f),
                "Search settings", null, null, null, null);

        internal static StructuredElementEvidence Row(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                RowBounds(ordinal), title, null, false, null, null);

        internal static StructuredElementEvidence ChildRow(string title, int ordinal)
            => new("android.widget.LinearLayout", null, true, false, false, true, true,
                ChildRowBounds(ordinal), title, null, false, null, null);
    }

    private sealed record PccRunOutcome(RunState State, string? Reason, RuntimeAgent Agent);

    private static async Task<PccRunOutcome> RunPccAsync(PccWorld world, string runId)
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
            "PCC: post-completeness consistency with Agent contextual disposition",
            goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(specification));
        var state = await IntentExecution.RunOpenWorldAsync(agent, envelope, runId, CancellationToken.None);
        return new PccRunOutcome(state, agent.Reason, agent);
    }

    [Fact]
    public async Task PCC1_EndToEnd_RevisitConsistencyPasses()
    {
        var run = await RunPccAsync(new PccWorld(), "pcc-e2e-1");

        // Child completeness proven (epoch frozen) AND the post-completeness
        // revisit is CONSISTENT (no INVALIDATED trace) — the Agent-built
        // disposition flows into the Validator.
        Assert.Contains(run.Agent.Trace, t =>
            t.ContainerId == ChildPage
            && t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true);
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("Post-completeness fresh evidence INVALIDATED", StringComparison.Ordinal) is true);
        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }
}
