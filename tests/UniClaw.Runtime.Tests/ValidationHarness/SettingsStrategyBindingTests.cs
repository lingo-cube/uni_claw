using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.ValidationHarness.SettingsBinding;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-P26-F capability tests for <see cref="SettingsStrategyBinding"/>: the
/// binding is a pure adapter over the production SettingsSemanticCapability's
/// ADMITTED PRIMARY EVIDENCE (spec "SettingsStrategyBinding adapts without
/// inventing" + design D6). Observations come from deterministic
/// Settings-shaped worlds wrapped with the production capability through
/// <see cref="SettingsSemanticCapabilityTestEnvironment"/> (source metadata
/// stamped by the test-only fake; the harness composition
/// <see cref="SettingsBindingComposition.Wrap"/> targets physical environments
/// that already carry source metadata). Structured row evidence mirrors the
/// graduated TreeWorld shapes (SearchBar/UpControl/TitleRole/Row builders
/// copied locally — no other test project internals).
/// </summary>
public sealed class SettingsStrategyBindingTests
{
    private const string App = SettingsStrategyBinding.ApplicationIdentity;
    private const string SearchBarResourceId = "com.android.settings:id/search_action_bar";
    private const string TitleRoleResourceId = "com.android.settings:id/collapsing_toolbar";
    private const string ParentReturnAccessibilityLabel = "Navigate up";

    private static readonly ElementBounds SearchBounds = new(0f, 0f, 1f, 0.06f);
    private static readonly ElementBounds TitleBounds = new(0f, 0f, 1f, 0.28f);
    private static readonly ElementBounds BackBounds = new(0f, 0f, 0.13f, 0.1f);

    // ── deterministic Settings-shaped worlds (TreeWorld-style builders) ────────

    private sealed class DeterministicSettingsWorld : IEnvironment
    {
        private readonly IReadOnlyList<Func<long, Observation>> _frames;
        private readonly List<DeviceAction> _actions = [];
        private long _sequence;
        private int _cursor;

        public DeterministicSettingsWorld(IEnumerable<Func<long, Observation>> frames)
        {
            _frames = frames.ToArray();
            Assert.NotEmpty(_frames);
        }

        public IReadOnlyList<DeviceAction> ActionHistory => _actions;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = _frames[Math.Min(_cursor, _frames.Count - 1)];
            _cursor++;
            return Task.FromResult(frame(++_sequence));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _actions.Add(action);
            return Task.FromResult(new ActionResult(
                ActionResultOutcome.Rejected, action.GetType().Name, "deterministic world executes no actions"));
        }
    }

    /// <summary>Root observation: Settings title + search bar + rows + one
    /// evidence-less marker (banner perception, no structured support).</summary>
    private static Observation RootFrame(long seq)
    {
        var elements = ImmutableArray.Create(
            new ObservedElement("Settings", null, 0, new ElementBounds(0f, 0f, 1f, 0.08f), "text"),
            new ObservedElement("Search settings", null, 1, SearchBounds, "menu_item"),
            RowElement("Network & internet", 2),
            RowElement("Connected devices", 3),
            RowElement("Apps", 4),
            new ObservedElement("Unknown marker", null, 5, null, "banner"));
        var structured = ImmutableArray.Create(
            Row("Network & internet", 2),
            Row("Connected devices", 3),
            Row("Apps", 4),
            SearchBar());
        return new Observation(elements, App, seq) { StructuredElements = structured };
    }

    /// <summary>Child observation: labelled back control + title role + rows.</summary>
    private static Observation ChildFrame(long seq, string pageTitle, params string[] rows)
    {
        var elements = rows.Select((row, i) => RowElement(row, i))
            .Append(new ObservedElement(ParentReturnAccessibilityLabel, null, rows.Length, BackBounds, "menu_item"))
            .ToImmutableArray();
        var structured = rows.Select((row, i) => Row(row, i))
            .Append(UpControl())
            .Append(TitleRole(pageTitle))
            .ToImmutableArray();
        return new Observation(elements, App, seq) { StructuredElements = structured };
    }

    /// <summary>Plain scroll frame: only preference rows (viewport tests).</summary>
    private static Observation RowsFrame(long seq, params string[] titles)
        => new(titles.Select((title, i) => RowElement(title, i)).ToImmutableArray(), App, seq)
        {
            StructuredElements = titles.Select((title, i) => Row(title, i)).ToImmutableArray(),
        };

    private static ObservedElement RowElement(string title, int index)
        => new(title, null, index, RowBounds(index), "menu_item");

    private static ElementBounds RowBounds(int ordinal) => new(0f, 0.1f * ordinal, 1f, 0.1f * (ordinal + 1));

    private static StructuredElementEvidence Row(string title, int ordinal)
        => new("android.widget.LinearLayout", null, true, false, false, true, true,
            RowBounds(ordinal), null, null, title, null);

    private static StructuredElementEvidence SearchBar()
        => new("android.view.ViewGroup", SearchBarResourceId, true, false, false, true, false,
            SearchBounds, null, null, "Search settings", null);

    private static StructuredElementEvidence UpControl()
        => new("android.widget.ImageButton", null, true, false, false, true, true,
            BackBounds, ParentReturnAccessibilityLabel, null, null);

    private static StructuredElementEvidence TitleRole(string pageTitle)
        => new("android.widget.FrameLayout", TitleRoleResourceId, null, null, null, true, null,
            TitleBounds, pageTitle, null, null);

    /// <summary>Vision-tier OCR text block (WI-P26-R1 structural fallback).</summary>
    private static ObservedElement VisionText(string text, int index, ElementBounds bounds)
        => new(text, null, index, bounds, "text");

    /// <summary>Structured clickable content row with EXPLICIT bounds
    /// (fallback tests need rows fully below the title band).</summary>
    private static StructuredElementEvidence ClickableRow(string text, ElementBounds bounds)
        => new("android.widget.LinearLayout", null, true, false, false, true, true,
            bounds, null, null, text, null);

    // ── directive builder (mirrors DirectiveFixtureCatalog.BuildLegalDirective) ─

    private static StrategyDirective SettingsDirective(int maximumDepth = 2, string? strategyId = null)
        => new(
            strategyId ?? "settings-binding-test-1",
            StrategyContractCompiler.SupportedContractVersion,
            new StrategyObjective(StrategyObjectiveKind.ExploreScope),
            new StrategyScope(SettingsStrategyBinding.ApplicationIdentity, SettingsStrategyBinding.RootIdentity, maximumDepth),
            ExplorationIntent.ExhaustiveWithinScope,
            new StrategyConstraintSet(
                ImmutableHashSet.Create(TypeLevelElementCategory.NavigableContainer),
                ImmutableHashSet.Create(
                    StrategyProhibitedEffect.StateMutation,
                    StrategyProhibitedEffect.ExternalBoundaryCrossing)),
            new StrategyCompletionCriteria(StrategyCompletionKind.ExhaustiveCoverageWithinScope),
            new StrategyAdaptationBoundary(
                ImmutableHashSet.Create(
                    StrategyAdaptationKind.ReconcileBelief,
                    StrategyAdaptationKind.ReviseExecutionHypothesis)));

    // ── tests ───────────────────────────────────────────────────────────────────

    // ════════════════════════════════════════════════════════════════════════
    // VISION BACK INDICATOR (runK transition-settle): the structured tier is
    // momentarily empty in immediate post-tap child frames. A unique top-LEFT
    // back ICON + a top-band title TEXT in vision must NOT flip the page
    // identity back to the ROOT (which broke two-consecutive settle). Root
    // pages keep the scrolled-root fallback unchanged.
    // ════════════════════════════════════════════════════════════════════════

    private static ElementBounds TopBandBounds(double left, double right) => new((float)left, 0.06f, (float)right, 0.12f);

    [Fact]
    public void VisionOnlyChildToolbar_DoesNotFlipToRoot()
    {
        // runK seq21 replica: child toolbar as pure vision (back icon + title
        // text), structured tier empty → must NOT resolve to the ROOT.
        var vision = new Observation(
            ImmutableArray.Create(
                new ObservedElement("", null, 0, TopBandBounds(0.048611, 0.090278), "icon"),
                new ObservedElement("Display", null, 1, TopBandBounds(0.066667, 0.340278), "text_block")),
            App, 21);

        var page = SettingsStrategyBinding.ResolveSemanticPage(vision);

        Assert.NotEqual(SettingsStrategyBinding.RootIdentity, page);
        Assert.True(page is null || page == "SettingsSubpage(Display)",
            $"expected child or null, got '{page}'");
    }

    [Fact]
    public void RootLikeFrame_RightAlignedIcon_StaysRoot()
    {
        // Root-shaped frame with only a RIGHT-aligned top icon (avatar/menu) and
        // no back arrow: the vision indicator must NOT fire → root fallback.
        var vision = new Observation(
            ImmutableArray.Create(
                new ObservedElement("Settings", null, 0, new ElementBounds(0.05f, 0.05f, 0.4f, 0.12f), "text"),
                new ObservedElement("", null, 1, new ElementBounds(0.72f, 0.05f, 0.95f, 0.12f), "icon")),
            App, 21);

        Assert.Equal(SettingsStrategyBinding.RootIdentity, SettingsStrategyBinding.ResolveSemanticPage(vision));
    }

    [Fact]
    public void BackIconWithoutTopTitleBand_StaysRoot()
    {
        // A top-left icon with NO top-band title text is not a child toolbar.
        var vision = new Observation(
            ImmutableArray.Create(
                new ObservedElement("", null, 0, TopBandBounds(0.048611, 0.090278), "icon")),
            App, 21);

        Assert.Equal(SettingsStrategyBinding.RootIdentity, SettingsStrategyBinding.ResolveSemanticPage(vision));
    }

    [Fact]
    public async Task RootObservation_ResolvesToRoot_AndEvaluatorsConsumeAdmittedEvidence()
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => RootFrame(seq)]));
        var observation = await environment.ObserveAsync(CancellationToken.None);

        // Page identity: root marker anchor → Settings root.
        Assert.Equal(SettingsStrategyBinding.RootIdentity, SettingsStrategyBinding.ResolveSemanticPage(observation));

        var binding = new SettingsStrategyBinding();
        var goal = binding.CreateGoal(SettingsDirective());

        // Goal evidence: honest root-identity evidence at the root.
        var evidence = goal.EvidenceEvaluator(observation);
        Assert.True(evidence.Satisfied);
        Assert.Contains("semantic root", evidence.Reason, StringComparison.Ordinal);

        // Inventory: every admitted NavigationCandidate row anchor, grounded.
        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 0);
        Assert.NotNull(inventory.RequiredBranchEvidence);
        Assert.NotNull(inventory.RequiredBranchGrounding);
        Assert.Contains("Network & internet", inventory.RequiredBranchEvidence!.Keys);
        Assert.Contains("Connected devices", inventory.RequiredBranchEvidence.Keys);
        Assert.Contains("Apps", inventory.RequiredBranchEvidence.Keys);
        Assert.All(inventory.RequiredBranchEvidence.Keys,
            anchor => Assert.True(inventory.RequiredBranchGrounding!.ContainsKey(anchor),
                $"branch '{anchor}' must carry an explicit occurrence grounding"));
        // Non-navigation anchors never become branches (title / search / marker).
        Assert.DoesNotContain("Settings", inventory.RequiredBranchEvidence.Keys);
        Assert.DoesNotContain("Search settings", inventory.RequiredBranchEvidence.Keys);
        Assert.DoesNotContain("Unknown marker", inventory.RequiredBranchEvidence.Keys);

        // Authorization: NavigationCandidate row authorized; the search bar is
        // LocalControl evidence and the plain title ContainerIdentity evidence —
        // both positively rejected with an audit reason naming the class.
        var row = observation.Elements.Single(e => e.Text == "Network & internet");
        Assert.True(goal.CandidateAuthorizationEvaluator!(observation, row).Authorized!.Value);
        var search = observation.Elements.Single(e => e.Text == "Search settings");
        Assert.False(goal.CandidateAuthorizationEvaluator(observation, search).Authorized!.Value);
        Assert.Contains("LocalControl", goal.CandidateAuthorizationEvaluator(observation, search).Reason, StringComparison.Ordinal);
        var title = observation.Elements.Single(e => e.Text == "Settings");
        var titleAuth = goal.CandidateAuthorizationEvaluator(observation, title);
        Assert.False(titleAuth.Authorized!.Value);
        Assert.Contains("NonInteractive", titleAuth.Reason, StringComparison.Ordinal);
        var marker = observation.Elements.Single(e => e.Text == "Unknown marker");
        var markerAuth = goal.CandidateAuthorizationEvaluator(observation, marker);
        Assert.False(markerAuth.Authorized!.Value);
        Assert.Contains("no element-level admitted primary evidence", markerAuth.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auxiliary_only_navigation_row_never_becomes_DFS_inventory()
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => new Observation(
                [RowElement("Vision row", 0)], App, seq)
            {
                StructuredElements = [Row("Vision row", 0), Row("Auxiliary only", 1), SearchBar()],
            }]));
        var observation = await environment.ObserveAsync(CancellationToken.None);
        var goal = new SettingsStrategyBinding().CreateGoal(SettingsDirective());

        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 0);

        Assert.NotNull(inventory.RequiredBranchEvidence);
        Assert.Contains("Vision row", inventory.RequiredBranchEvidence!.Keys);
        Assert.DoesNotContain("Auxiliary only", inventory.RequiredBranchEvidence.Keys);
        Assert.All(inventory.RequiredBranchGrounding!.Values, reference =>
        {
            var source = SourceEquivalenceNormalizer.OccurrencesOf(observation)
                .Single(occurrence => occurrence.OccurrenceIdentity == reference.OccurrenceLocalIdentity);
            Assert.True(source.EligibleForAuthorization);
        });
    }

    [Fact]
    public async Task ChildObservation_ResolvesToSubpage_ChildInventoryNonEmpty_RootEvidenceNotSatisfied()
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => ChildFrame(seq, "Location", "Location services", "Recent location requests")]));
        var observation = await environment.ObserveAsync(CancellationToken.None);

        // Page identity: labelled back control + exactly-one title role.
        Assert.Equal("SettingsSubpage(Location)", SettingsStrategyBinding.ResolveSemanticPage(observation));

        var binding = new SettingsStrategyBinding();
        var goal = binding.CreateGoal(SettingsDirective());

        // Root goal evidence is NOT satisfied on a child page.
        Assert.False(goal.EvidenceEvaluator(observation).Satisfied);

        // The child inventories ITS OWN navigation rows (recursion) — never empty.
        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 1);
        Assert.NotNull(inventory.RequiredBranchEvidence);
        Assert.Contains("Location services", inventory.RequiredBranchEvidence!.Keys);
        Assert.Contains("Recent location requests", inventory.RequiredBranchEvidence.Keys);
        Assert.All(inventory.RequiredBranchEvidence.Keys,
            anchor => Assert.True(inventory.RequiredBranchGrounding!.ContainsKey(anchor)));

        // The labelled parent-return control is authorized through the admitted
        // parent-return relation class (it never becomes a branch).
        var back = observation.Elements.Single(e => e.Text == ParentReturnAccessibilityLabel);
        var backAuth = goal.CandidateAuthorizationEvaluator!(observation, back);
        Assert.True(backAuth.Authorized!.Value);
        Assert.Contains("parent-return", backAuth.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LeafChildObservation_EmptyInventoryIsBoundedLeaf()
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => ChildFrame(seq, "Location services")]));
        var observation = await environment.ObserveAsync(CancellationToken.None);

        Assert.Equal("SettingsSubpage(Location services)", SettingsStrategyBinding.ResolveSemanticPage(observation));

        var goal = new SettingsStrategyBinding().CreateGoal(SettingsDirective());
        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 2);
        Assert.NotNull(inventory.RequiredBranchEvidence);
        Assert.Empty(inventory.RequiredBranchEvidence!);
        Assert.Contains("bounded leaf", inventory.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Viewport_NewAdmittedOccurrenceContinues_IdenticalFrameExhausted()
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld(
            [
                seq => RowsFrame(seq, "Network & internet", "Connected devices", "Apps"),
                seq => RowsFrame(seq, "Apps", "Display", "Battery"),
            ]));
        var first = await environment.ObserveAsync(CancellationToken.None);
        var second = await environment.ObserveAsync(CancellationToken.None);

        var goal = new SettingsStrategyBinding().CreateGoal(SettingsDirective());
        var viewport = goal.ViewportExplorationEvaluator!(ImmutableArray.Create(first, second));
        Assert.True(viewport.ContinueExploration!.Value);
        Assert.Contains("new admitted navigation occurrence", viewport.Reason, StringComparison.Ordinal);

        // A repeated identical frame adds no new admitted occurrence → exhausted.
        var repeated = await environment.ObserveAsync(CancellationToken.None);
        var exhausted = goal.ViewportExplorationEvaluator!(ImmutableArray.Create(second, repeated));
        Assert.False(exhausted.ContinueExploration!.Value);
        Assert.Contains("exhausted", exhausted.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_AcceptsLegalSettingsDirective()
    {
        var result = new StrategyContractCompiler([new SettingsStrategyBinding()])
            .Compile(SettingsDirective(maximumDepth: 2));

        var accepted = Assert.IsType<StrategyCompilationResult.Accepted>(result);
        Assert.Equal("settings-binding-test-1", accepted.Intent.Strategy.StrategyId);
        Assert.Equal(SettingsStrategyBinding.ApplicationIdentity, accepted.Intent.Strategy.Scope.ApplicationIdentity);
        Assert.Equal(SettingsStrategyBinding.RootIdentity, accepted.Intent.Strategy.Scope.SemanticRoot);
        Assert.Equal(2, accepted.Intent.Strategy.Scope.MaximumDepth);
        Assert.Equal(ExplorationIntent.ExhaustiveWithinScope, accepted.Intent.Strategy.Exploration);
    }

    // ── WI-P26-R1: structural child-title fallback (no collapsing_toolbar) ─────

    [Fact]
    public void StructuralChild_NoToolbarTitle_FallsBackToLeftMarginTitleBand()
    {
        // Frozen re-entry frame (seq 22, 'Wallpaper & style' child): no
        // collapsing_toolbar. Title 'Choose wallpaper' is the page's leftmost
        // text, above the topmost clickable content row 'Gallery'; caption
        // 'from' sits in the same column overlapping the title band and is part
        // of it (ignored as a separate candidate).
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var live = new ElementBounds(0.1986f, 0.3875f, 0.5375f, 0.4088f);
        var title = new ElementBounds(0.0639f, 0.1825f, 0.7667f, 0.2650f);
        var caption = new ElementBounds(0.0611f, 0.2325f, 0.2431f, 0.2656f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Choose wallpaper", 1, title),
                VisionText("from", 2, caption),
                VisionText("Gallery", 3, gallery),
                VisionText("Live Wallpapers", 4, live)),
            App, 22)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", gallery),
                ClickableRow("Live Wallpapers", live)),
        };

        Assert.Equal(
            "SettingsSubpage(Choose wallpaper)",
            SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void StructuralChild_TwoSeparateLeftMarginBands_TopmostBandWins()
    {
        // Two independent left-margin labels above the first clickable row, in
        // separate (non-overlapping) vertical bands. R3 RULE (2026-08-29): the
        // TOPMOST band is the title; the lower band is a section header and
        // does NOT veto. Resolves to SettingsSubpage(First label).
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var first = new ElementBounds(0.0600f, 0.1800f, 0.6000f, 0.2200f);
        var second = new ElementBounds(0.0600f, 0.2500f, 0.6000f, 0.2900f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("First label", 1, first),
                VisionText("Second label", 2, second),
                VisionText("Gallery", 3, gallery)),
            App, 23)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", gallery)),
        };

        var page = SettingsStrategyBinding.ResolveSemanticPage(observation);
        Assert.Equal("SettingsSubpage(First label)", page);
    }

    [Fact]
    public void R3_DisplayChildPage_DisplayTitlePlusBrightnessSection_ResolvesDisplay()
    {
        // FROZEN REAL FIXTURE (seq 18-24 of the Phase 2.6 reentry campaign):
        // the Display child page has a stable "Display" title band and a
        // separate "Brightness" SECTION HEADER band below it, both above the
        // first clickable row ("Brightness level"). R3: the topmost band
        // ("Display") is the page title; "Brightness" does NOT veto.
        var brightnessLevel = new ElementBounds(0.06f, 0.38f, 0.90f, 0.44f);
        var displayTitle = new ElementBounds(0.06f, 0.15f, 0.40f, 0.19f);
        var brightnessHeader = new ElementBounds(0.06f, 0.28f, 0.30f, 0.32f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Display", 1, displayTitle),
                VisionText("Brightness", 2, brightnessHeader),
                VisionText("Brightness level", 3, brightnessLevel)),
            App, 25)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Brightness level", brightnessLevel)),
        };

        Assert.Equal("SettingsSubpage(Display)", SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void R3_TopBandNestedCaption_Subordinate_TitleWins()
    {
        // Wallpaper shape: caption's Y1 is INSIDE the title's Y-range → nested
        // subordinate, not a peer competitor → title wins (R1 behavior preserved).
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var title = new ElementBounds(0.0600f, 0.1825f, 0.7667f, 0.2650f);
        var caption = new ElementBounds(0.0611f, 0.2325f, 0.4000f, 0.2656f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Choose wallpaper", 1, title),
                VisionText("from", 2, caption),
                VisionText("Gallery", 3, gallery)),
            App, 26)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", gallery)),
        };

        Assert.Equal("SettingsSubpage(Choose wallpaper)", SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void R3_TopBandPeerCompetitor_ReturnsNull()
    {
        // Two DIFFERENT texts in the same top band, the second's Y1 BELOW the
        // title's Y2 (peer at the same level, not nested) → unresolvable → null.
        var row = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var alpha = new ElementBounds(0.0600f, 0.1500f, 0.6000f, 0.1900f);
        var beta = new ElementBounds(0.0600f, 0.2050f, 0.6000f, 0.2450f); // Y1 > alpha.Y2, gap 0.015 ≤ 0.02
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Alpha", 1, alpha),
                VisionText("Beta", 2, beta),
                VisionText("Gallery", 3, row)),
            App, 27)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", row)),
        };

        Assert.Null(SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void R3_SameTextDuplicateOCR_MergesAndStabilizes()
    {
        // The same title appears as text_block AND menu_item AND another
        // text_block (OCR duplicate), all in the same band → merge → stable title.
        var row = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var title1 = new ElementBounds(0.0600f, 0.1500f, 0.6000f, 0.1900f);
        var title2 = new ElementBounds(0.0605f, 0.1520f, 0.5900f, 0.1880f); // OCR duplicate
        var observation = new Observation(
            ImmutableArray.Create(
                new ObservedElement("Network & internet", null, 1, title1, "menu_item"),
                new ObservedElement("Network & internet", null, 2, title2, "text_block"),
                VisionText("Gallery", 3, row)),
            App, 28)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", row)),
        };

        Assert.Equal("SettingsSubpage(Network & internet)", SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void R3_MultipleLowerBands_AllIgnored_TopmostWins()
    {
        // Title at top, then multiple section headers in separate lower bands
        // → ALL ignored, the topmost title wins.
        var row = new ElementBounds(0.1972f, 0.5225f, 0.3458f, 0.5450f);
        var title = new ElementBounds(0.0600f, 0.1500f, 0.6000f, 0.1900f);
        var section1 = new ElementBounds(0.0600f, 0.2500f, 0.4000f, 0.2900f);
        var section2 = new ElementBounds(0.0600f, 0.3500f, 0.4000f, 0.3900f);
        var section3 = new ElementBounds(0.0600f, 0.4500f, 0.4000f, 0.4900f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Page Title", 1, title),
                VisionText("Section 1", 2, section1),
                VisionText("Section 2", 3, section2),
                VisionText("Section 3", 4, section3),
                VisionText("Content", 5, row)),
            App, 29)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Content", row)),
        };

        Assert.Equal("SettingsSubpage(Page Title)", SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void R3_NoNavigateUp_SkipsStructuralFallback_RootFallbackFires()
    {
        // Acceptance (e): no "Navigate up" back control → the structural fallback
        // is NOT active. With no search bar either, the scrolled-root fallback
        // fires → root identity (never a subpage identity from the band).
        var row = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var title = new ElementBounds(0.0600f, 0.1800f, 0.7667f, 0.2650f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Some title", 1, title),
                VisionText("Gallery", 2, row)),
            App, 30)
        {
            // No UpControl, no SearchBar → neither root marker nor back control.
            StructuredElements = ImmutableArray.Create(ClickableRow("Gallery", row)),
        };

        // Scrolled-root fallback: Settings foreground + no search bar + no back
        // control → root identity (the structural band never decides here).
        Assert.Equal(SettingsStrategyBinding.RootIdentity, SettingsStrategyBinding.ResolveSemanticPage(observation));
        // Diagnostic confirms the structural fallback is inactive.
        Assert.Contains("inactive", SettingsStrategyBinding.DescribeStructuralTitleResolution(observation)!, StringComparison.Ordinal);
    }

    [Fact]
    public void R3_NoTitleTextAboveFirstClickableRow_ReturnsNull()
    {
        // Acceptance (f): Navigate up + clickable rows but NO title text above
        // the topmost clickable content row → no candidates → null (fail closed).
        var row = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var observation = new Observation(
            ImmutableArray.Create(VisionText("Some row", 1, row)),
            App, 31)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Some row", row)),
        };

        // The only vision text sits AT the clickable row (centerY == topClickableY
        // → not strictly above it) → zero candidates → null.
        Assert.Null(SettingsStrategyBinding.ResolveSemanticPage(observation));
        Assert.Contains("null", SettingsStrategyBinding.DescribeStructuralTitleResolution(observation)!, StringComparison.Ordinal);
    }

    [Fact]
    public void R3_SameTextPeerPositionDuplicate_MergesBeforeNestingCheck_TitleWins()
    {
        // Leader ruling corollary: "Same-text duplicates still merge first
        // (before the nesting check)." A duplicate of the SAME text at a PEER
        // position (Y1 below title.Y2) must merge → NOT a conflict → title wins.
        // (This guards against the merge-order bug where the nesting check ran
        // over the raw band instead of the merged unique texts.)
        var row = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var title = new ElementBounds(0.0600f, 0.1500f, 0.6000f, 0.1900f);
        var dup = new ElementBounds(0.0600f, 0.2050f, 0.6000f, 0.2450f); // Y1 > title.Y2, gap 0.015 ≤ 0.02 → same band
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Display", 1, title),
                VisionText("Display", 2, dup), // SAME text, peer position
                VisionText("Gallery", 3, row)),
            App, 32)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", row)),
        };

        Assert.Equal("SettingsSubpage(Display)", SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void StructuralChild_NoClickableContentRows_IsNull()
    {
        // Without a content anchor there is nothing to order the title against
        // → fail closed even when a plausible left-margin title is present.
        var title = new ElementBounds(0.0600f, 0.1800f, 0.7667f, 0.2650f);
        var observation = new Observation(
            ImmutableArray.Create(VisionText("Choose wallpaper", 1, title)), App, 24)
        {
            StructuredElements = ImmutableArray.Create(UpControl()),
        };

        Assert.Null(SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void StructuralChild_LeftMarginTextBelowFirstClickableRow_IsNotTitle_IsNull()
    {
        // A left-margin caption sitting BELOW the topmost clickable content row
        // is not a title candidate (the title lives above the content anchor).
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var caption = new ElementBounds(0.0600f, 0.3600f, 0.2431f, 0.3900f);
        var observation = new Observation(
            ImmutableArray.Create(
                VisionText("Some caption", 1, caption),
                VisionText("Gallery", 2, gallery)),
            App, 25)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", gallery)),
        };

        Assert.Null(SettingsStrategyBinding.ResolveSemanticPage(observation));
    }

    [Fact]
    public void ResolveSemanticPage_RootAndToolbarTitlePathsUnchanged()
    {
        // Root: the search_action_bar anchor still wins (never reaches the
        // structural fallback).
        Assert.Equal(
            SettingsStrategyBinding.RootIdentity,
            SettingsStrategyBinding.ResolveSemanticPage(RootFrame(26)));
        // Toolbar child: the collapsing_toolbar title role still wins over the
        // fallback, even with no content rows (fallback applies only when the
        // title-role count is exactly zero).
        Assert.Equal(
            "SettingsSubpage(Location)",
            SettingsStrategyBinding.ResolveSemanticPage(ChildFrame(27, "Location")));
    }

    // ── WI-P26-R2: structural page title is identity, not a destination ────────

    /// <summary>Structural child frame (R1 shape) whose title band is ALSO
    /// admitted as a NavigationCandidate occurrence: the frozen capability has
    /// no NonInteractive admission path, so the child page title reaches the
    /// binding as a preference-row candidate (the re-entry shape R2 repairs).
    /// Uses element indices equal to their array positions (the correlation the
    /// production projector relies on).</summary>
    private static Observation StructuralTitleAdmittedChildFrame(long seq)
    {
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var live = new ElementBounds(0.1986f, 0.3875f, 0.5375f, 0.4088f);
        var title = new ElementBounds(0.0639f, 0.1825f, 0.7667f, 0.2650f);
        var caption = new ElementBounds(0.0611f, 0.2325f, 0.2431f, 0.2656f);
        return new Observation(
            ImmutableArray.Create(
                new ObservedElement("Choose wallpaper", null, 1, title, "menu_item"),
                new ObservedElement("from", null, 2, caption, "text"),
                new ObservedElement("Gallery", null, 3, gallery, "menu_item"),
                new ObservedElement("Live Wallpapers", null, 4, live, "menu_item")),
            App, seq)
        {
            StructuredElements = ImmutableArray.Create(
                UpControl(),
                ClickableRow("Gallery", gallery),
                ClickableRow("Live Wallpapers", live)),
        };
    }

    [Fact]
    public async Task StructuralChildTitle_AdmittedAsNavigationCandidate_ExcludedFromInventory_AndAuthorizationRejected()
    {
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => StructuralTitleAdmittedChildFrame(seq)]));
        var observation = await environment.ObserveAsync(CancellationToken.None);

        // R1 identity: the leftmost-margin title band still decides the child page.
        Assert.Equal(
            "SettingsSubpage(Choose wallpaper)",
            SettingsStrategyBinding.ResolveSemanticPage(observation));

        var goal = new SettingsStrategyBinding().CreateGoal(SettingsDirective());

        // The title occurrence REMAINS an admitted NavigationCandidate occurrence
        // (completeness never sees an Unknown); it just never becomes a branch.
        var eligible = SourceEquivalenceNormalizer.OccurrencesOf(observation)
            .Where(o => o.EligibleForAuthorization)
            .ToArray();
        Assert.Equal(3, eligible.Length);
        Assert.Contains(
            eligible,
            o => o.StructuredSignature.StartsWith("Choose wallpaper|", StringComparison.Ordinal));

        // Inventory: the title anchor is excluded from required AND grounding;
        // the navigation rows remain; the reason records the exclusion honestly.
        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 1);
        Assert.NotNull(inventory.RequiredBranchEvidence);
        Assert.Contains("Gallery", inventory.RequiredBranchEvidence!.Keys);
        Assert.Contains("Live Wallpapers", inventory.RequiredBranchEvidence.Keys);
        Assert.DoesNotContain("Choose wallpaper", inventory.RequiredBranchEvidence.Keys);
        Assert.All(inventory.RequiredBranchGrounding!.Keys,
            anchor => Assert.DoesNotContain("Choose wallpaper", anchor));
        Assert.Contains(
            "title-excluded: Choose wallpaper (page identity, not a destination)",
            inventory.Reason, StringComparison.Ordinal);

        // Authorization: the title candidate is positively rejected with the
        // page-identity reason even though the capability admitted it as a
        // preference-row NavigationCandidate; the rows stay authorized.
        var titleElement = observation.Elements.Single(e => e.Text == "Choose wallpaper");
        var titleAuth = goal.CandidateAuthorizationEvaluator!(observation, titleElement);
        Assert.False(titleAuth.Authorized!.Value);
        Assert.Equal("page title is the page identity, not a navigation destination", titleAuth.Reason);
        var galleryElement = observation.Elements.Single(e => e.Text == "Gallery");
        Assert.True(goal.CandidateAuthorizationEvaluator(observation, galleryElement).Authorized!.Value);
    }

    [Fact]
    public async Task RootPage_TitleBandShape_IsNotExcluded_StaysInInventory()
    {
        // Root page (search marker present): even an ADMITTED band-shaped
        // element above the clickable rows is NOT the page title there — the
        // page identity is the scope root, so the structural-title exclusion
        // never fires (root pages are unaffected by R2).
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var band = new ElementBounds(0.0600f, 0.1800f, 0.7667f, 0.2650f);
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => new Observation(
                ImmutableArray.Create(
                    new ObservedElement("Upgrade banner", null, 0, band, "menu_item"),
                    new ObservedElement("Gallery", null, 1, gallery, "menu_item")),
                App, seq)
            {
                StructuredElements = ImmutableArray.Create(
                    SearchBar(),
                    ClickableRow("Gallery", gallery)),
            }]));
        var observation = await environment.ObserveAsync(CancellationToken.None);

        Assert.Equal(SettingsStrategyBinding.RootIdentity, SettingsStrategyBinding.ResolveSemanticPage(observation));

        var goal = new SettingsStrategyBinding().CreateGoal(SettingsDirective());
        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 0);
        Assert.NotNull(inventory.RequiredBranchEvidence);
        // The band element is kept honestly as a branch on the root page.
        Assert.Contains("Upgrade banner", inventory.RequiredBranchEvidence!.Keys);
        Assert.Contains("Gallery", inventory.RequiredBranchEvidence.Keys);
        Assert.DoesNotContain("title-excluded", inventory.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguousChild_TwoLeftMarginBands_TopmostTitleExcluded_LowerIsSection()
    {
        // Two separate left-margin labels above the first clickable row. R3 RULE
        // (2026-08-29): the TOPMOST band wins ("First label" is the page title);
        // the lower band ("Second label") is a section header — it stays in the
        // required inventory (it's a clickable candidate), but the title is
        // excluded from navigation per the structural title exclusion.
        var gallery = new ElementBounds(0.1972f, 0.3225f, 0.3458f, 0.3450f);
        var first = new ElementBounds(0.0600f, 0.1800f, 0.6000f, 0.2200f);
        var second = new ElementBounds(0.0600f, 0.2500f, 0.6000f, 0.2900f);
        var environment = new SettingsSemanticCapabilityTestEnvironment(
            new DeterministicSettingsWorld([seq => new Observation(
                ImmutableArray.Create(
                    new ObservedElement("First label", null, 1, first, "menu_item"),
                    new ObservedElement("Second label", null, 2, second, "menu_item"),
                    new ObservedElement("Gallery", null, 3, gallery, "menu_item")),
                App, seq)
            {
                StructuredElements = ImmutableArray.Create(
                    UpControl(),
                    ClickableRow("Gallery", gallery)),
            }]));
        var observation = await environment.ObserveAsync(CancellationToken.None);

        // R3: topmost band is the title → identity resolves to the topmost label.
        Assert.Equal("SettingsSubpage(First label)", SettingsStrategyBinding.ResolveSemanticPage(observation));

        // The lower-band label and gallery stay as honest candidates; the
        // structural title ("First label") is excluded from navigation.
        var goal = new SettingsStrategyBinding().CreateGoal(SettingsDirective());
        var inventory = goal.BranchInventoryEvaluator!(ImmutableArray.Create(observation), 1);
        Assert.NotNull(inventory.RequiredBranchEvidence);
        Assert.Contains("Second label", inventory.RequiredBranchEvidence!.Keys);
        Assert.Contains("Gallery", inventory.RequiredBranchEvidence.Keys);
    }

    [Fact]
    public void BindingSource_IsPureAdapter_NoCoordinatesPathsSelectorsOrTruthInjection()
    {
        var path = BindingSourcePath();
        Assert.True(File.Exists(path), $"binding source not found at '{path}' (guard failure is a test-environment problem).");
        var source = File.ReadAllText(path);

        // Frozen purity rules (spec "SettingsStrategyBinding adapts without
        // inventing" / design D6) — each assertion names the rule it enforces.

        // (1) No coordinate vocabulary: the binding has no spatial understanding.
        Assert.DoesNotContain("Coordinate", source, StringComparison.Ordinal);

        // (2) No tap/click vocabulary: the binding never scripts navigation.
        Assert.DoesNotContain("Tap(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("click(", source, StringComparison.Ordinal);

        // (3) No selector/XPath vocabulary: no fixed node selection.
        Assert.DoesNotContain("//*[@", source, StringComparison.Ordinal);

        // (4) No fixed page paths: the application identity constant is a scope
        // identity only — any "com.android.settings/" suffix would be a path.
        Assert.DoesNotContain("com.android.settings/", source, StringComparison.Ordinal);

        // (5) No hardcoded page-title truth: page identity resolution may only
        // use the graduated resource anchors + production evidence, never
        // hardcoded UI page-name literals.
        foreach (var pageTitle in new[]
                 {
                     "Network & internet", "Connected devices", "Display",
                     "Battery", "Location", "Notifications", "Storage",
                     "Security & privacy", "Passwords & accounts",
                 })
        {
            Assert.DoesNotContain(pageTitle, source, StringComparison.Ordinal);
        }

        // (6) No knowledge/fixture/campaign access: the binding consumes only
        // production capability output; zero reads of knowledge or fixture
        // content and zero references into sibling harness domains.
        foreach (var forbiddenNamespace in new[]
                 {
                     "UniClaw.Runtime.ValidationHarness.Knowledge",
                     "UniClaw.Runtime.ValidationHarness.Campaign",
                     "UniClaw.Runtime.ValidationHarness.PlanDelta",
                     "UniClaw.Runtime.ValidationHarness.Fixtures",
                     "ScenarioKnowledge",
                 })
        {
            Assert.DoesNotContain(forbiddenNamespace, source, StringComparison.Ordinal);
        }
    }

    private static string BindingSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "src", "UniClaw.Runtime.sln")))
            {
                return Path.Combine(
                    directory.FullName,
                    "src", "UniClaw.Runtime.ValidationHarness", "SettingsBinding", "SettingsStrategyBinding.cs");
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException(
            "Unable to locate the repository root from the test output directory.");
    }
}
