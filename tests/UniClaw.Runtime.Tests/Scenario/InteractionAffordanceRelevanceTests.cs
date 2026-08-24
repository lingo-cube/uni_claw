using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// INTERACTION_AFFORDANCE_RELEVANCE_REPAIR — REL-1..REL-10.
///
/// StructuredElementEvidence -> interaction relevance gate:
///   NON_INTERACTIVE (no clickable/checkable/focusable/switch signals) -> never a
///     blocking Unknown;
///   INTERACTION_RELEVANT -> NAVIGATION_CANDIDATE / LOCAL_CONTROL / UNKNOWN,
///     where UNKNOWN (genuinely interactive but ambiguous) still blocks completeness.
/// </summary>
public sealed class InteractionAffordanceRelevanceTests
{
    private static StructuredElementEvidence El(
        string @class,
        bool? clickable = null,
        bool? checkable = null,
        bool? focusable = null,
        string? title = null,
        string? summary = null,
        bool hasSwitchChild = false,
        string resourceId = "")
        => new(Class: @class, ResourceId: resourceId, Clickable: clickable,
            Checkable: checkable, Checked: false, Enabled: true, Focusable: focusable,
            Bounds: new ElementBounds(0, 0, 1, 0.1f), RawText: title,
            ContentDescription: summary);

    private static InteractionAffordanceEvidence First(Observation o, int index = 0)
        => InteractionAffordanceAnalyzer.Analyze(o)[index];

    private static Observation Obs(params StructuredElementEvidence[] elements)
        => new([], "com.uniclaw.fixture", 1) { StructuredElements = elements.ToImmutableArray() };

    // ── REL-1: non-interactive title TextView -> no blocking Unknown ────────

    [Fact]
    public void REL1_NonInteractiveTitleTextView_IsNonInteractive()
    {
        var title = El("android.widget.TextView", clickable: false, focusable: false, title: "Fixture Root");
        var evidence = First(Obs(title));
        Assert.Equal(InteractionAffordanceKind.NonInteractive, evidence.Classification);
    }

    // ── REL-2: non-interactive status text -> no blocking Unknown ───────────

    [Fact]
    public void REL2_StatusText_IsNonInteractive()
    {
        var status = El("android.widget.TextView", clickable: false, focusable: false, title: "Visited 0/8");
        var evidence = First(Obs(status));
        Assert.Equal(InteractionAffordanceKind.NonInteractive, evidence.Classification);
    }

    // ── REL-3: navigation row -> NAVIGATION_CANDIDATE ───────────────────────

    [Fact]
    public void REL3_NavigationRow_IsNavigationCandidate()
    {
        var row = El("android.widget.LinearLayout", clickable: true, focusable: true, title: "Child 01");
        var evidence = First(Obs(row));
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, evidence.Classification);
    }

    // ── REL-4: Switch/checkable -> LOCAL_CONTROL ────────────────────────────

    [Fact]
    public void REL4_SwitchAndCheckable_AreLocalControl()
    {
        var sw = El("android.widget.Switch", clickable: true, focusable: true, title: "Local Switch");
        var cb = El("android.widget.LinearLayout", clickable: true, checkable: true, focusable: true, title: "Local Checkbox");
        var analyzed = InteractionAffordanceAnalyzer.Analyze(Obs(sw, cb));
        Assert.All(analyzed, a => Assert.Equal(InteractionAffordanceKind.LocalControl, a.Classification));
    }

    // ── REL-5: clickable ambiguous row -> UNKNOWN, still blocks ─────────────

    [Fact]
    public void REL5_ClickableAmbiguousRow_IsUnknown()
    {
        // Clickable but not a Settings-row shape and not a local control:
        // genuine interaction with insufficient evidence -> UNKNOWN (blocks).
        var ambiguous = El("android.widget.FrameLayout", clickable: true, focusable: true);
        var evidence = First(Obs(ambiguous));
        Assert.Equal(InteractionAffordanceKind.Unknown, evidence.Classification);
    }

    // ── REL-6: ambiguous Button -> UNKNOWN ──────────────────────────────────

    [Fact]
    public void REL6_AmbiguousButton_IsUnknown()
    {
        var button = El("android.widget.Button", clickable: true, focusable: true, title: "RESET SCENARIO");
        var evidence = First(Obs(button));
        Assert.Equal(InteractionAffordanceKind.Unknown, evidence.Classification);
    }

    // ── REL-7: title child ignored, clickable parent still found ────────────

    [Fact]
    public void REL7_TitleChildNonInteractive_ClickableParentNavigation()
    {
        var child = El("android.widget.TextView", clickable: false, focusable: false, title: "Child 01");
        var parent = El("android.widget.LinearLayout", clickable: true, focusable: true, title: "Child 01");
        var analyzed = InteractionAffordanceAnalyzer.Analyze(Obs(child, parent));
        Assert.Equal(InteractionAffordanceKind.NonInteractive, analyzed[0].Classification);
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, analyzed[1].Classification);
    }

    // ── REL-8: SCROLL-03 mixed controls don't weaken Unknown safety ─────────

    [Fact]
    public void REL8_MixedControls_UnknownSafetyPreserved()
    {
        var nav = El("android.widget.LinearLayout", clickable: true, focusable: true, title: "Navigation Row 1");
        var sw = El("android.widget.Switch", clickable: true, focusable: true, title: "Local Switch");
        var btn = El("android.widget.Button", clickable: true, focusable: true, title: "Press");
        var analyzed = InteractionAffordanceAnalyzer.Analyze(Obs(nav, sw, btn));
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, analyzed[0].Classification);
        Assert.Equal(InteractionAffordanceKind.LocalControl, analyzed[1].Classification);
        Assert.Equal(InteractionAffordanceKind.Unknown, analyzed[2].Classification);
    }

    // ── REL-9: real root frame decorations don't block; only interactive Unknown ─

    [Fact]
    public void REL9_RootFrame_OnlyInteractiveUnknownRemains()
    {
        // Capstone root frame structure: title + status TextView (non-interactive),
        // 4 clickable child rows (navigation), one RESET Button (interactive,
        // ambiguous -> UNKNOWN). Completeness must not fail on the decorations.
        var analyzed = InteractionAffordanceAnalyzer.Analyze(Obs(
            El("android.widget.TextView", clickable: false, focusable: false, title: "Fixture Root"),
            El("android.widget.TextView", clickable: false, focusable: false, title: "Visited 0/8"),
            El("android.widget.LinearLayout", clickable: true, focusable: true, title: "Child 01"),
            El("android.widget.LinearLayout", clickable: true, focusable: true, title: "Child 02"),
            El("android.widget.Button", clickable: true, focusable: true, title: "RESET SCENARIO")));

        Assert.Equal(5, analyzed.Length);
        Assert.Equal(InteractionAffordanceKind.NonInteractive, analyzed[0].Classification);
        Assert.Equal(InteractionAffordanceKind.NonInteractive, analyzed[1].Classification);
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, analyzed[2].Classification);
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, analyzed[3].Classification);
        Assert.Equal(InteractionAffordanceKind.Unknown, analyzed[4].Classification);
        Assert.Equal(1, analyzed.Count(a => a.Classification == InteractionAffordanceKind.Unknown));
    }

    // ── REL-10: empty/unavailable structured evidence still cannot prove ────

    [Fact]
    public void REL10_EmptyStructuredEvidence_CannotProveCompleteness()
    {
        // No structured navigation candidates -> normalization unresolved ->
        // completeness/leaf cannot be proven from empty evidence.
        var empty = Obs();
        var normalization = SourceEquivalenceNormalizer.Normalize(
            ImmutableArray.Create(empty));
        Assert.False(normalization.IsResolved);

        var noOccurrences = SourceEquivalenceNormalizer.OccurrencesOf(empty);
        Assert.True(noOccurrences.IsDefaultOrEmpty || noOccurrences.IsEmpty);
    }
}
