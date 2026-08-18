using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_ROOT_SEARCH_AFFORDANCE — SEARCH-1..SEARCH-10.
///
/// Generic role-based search-control resolution: an interactive element with a
/// STABLE search-role structured token (SearchView / SearchBar view family, or
/// the standard "search_action_bar" resource-id leaf) is LOCAL_CONTROL — it is
/// interactive and resolved but never a navigation source, never a
/// child-inventory entry, never a recursive obligation. TitleText / package /
/// page are never used; generic clickable ViewGroups stay UNKNOWN.
/// </summary>
public sealed class SearchAffordanceRoleTests
{
    private const string App = "com.uniclaw.fixture";

    private static StructuredElementEvidence SearchBar(string title, string resourceId = "com.android.settings:id/search_action_bar", string @class = "android.view.ViewGroup")
        => new(@class, resourceId, true, false, false, true, true, new ElementBounds(0, 0.05f, 1, 0.12f),
            title, null, false, null, null);

    private static StructuredElementEvidence NavRow(string title)
        => new("android.widget.LinearLayout", "com.uniclaw.fixture:id/row_title", true, false, false, true, true,
            new ElementBounds(0, 0.2f, 1, 0.32f), title, null, false, null, null);

    private static StructuredElementEvidence GenericViewGroup()
        => new("android.view.ViewGroup", null, true, false, false, true, true,
            new ElementBounds(0, 0.2f, 1, 0.32f), "Generic panel", null, false, null, null);

    private static StructuredElementEvidence SwitchRow()
        => new("android.widget.LinearLayout", "com.uniclaw.fixture:id/local_switch", true, true, false, true, true,
            new ElementBounds(0, 0.2f, 1, 0.32f), "Local", null, true, null, null);

    private static InteractionAffordanceKind Classify(StructuredElementEvidence raw)
    {
        var observation = new Observation([], App, 1) { StructuredElements = ImmutableArray.Create(raw) };
        return InteractionAffordanceAnalyzer.Analyze(observation)[0].Classification;
    }

    private static Observation Obs(long seq, params StructuredElementEvidence[] rows)
        => new([], App, seq) { StructuredElements = rows.ToImmutableArray() };

    // ── SEARCH-1: clickable ViewGroup + rid leaf search_action_bar -> LOCAL_CONTROL ──

    [Fact]
    public void SEARCH1_SearchActionBarResourceId_LocalControl()
    {
        Assert.Equal(InteractionAffordanceKind.LocalControl,
            Classify(SearchBar("Search settings")));
    }

    // ── SEARCH-2: localized / changing title does not change classification ──

    [Fact]
    public void SEARCH2_LocalizedTitle_ClassificationUnchanged()
    {
        Assert.Equal(InteractionAffordanceKind.LocalControl,
            Classify(SearchBar("搜索设置"))); // localized title, same role token
        Assert.Equal(InteractionAffordanceKind.LocalControl,
            Classify(SearchBar("")));        // even empty title, same role token
    }

    // ── SEARCH-3: title alone (no role token) must NOT classify ─────────────

    [Fact]
    public void SEARCH3_TitleAlone_NotClassified()
    {
        var raw = SearchBar("Search settings", resourceId: "", @class: "android.view.ViewGroup");
        Assert.Equal(InteractionAffordanceKind.Unknown, Classify(raw));
    }

    // ── SEARCH-4: generic clickable ViewGroup -> UNKNOWN ────────────────────

    [Fact]
    public void SEARCH4_GenericClickableViewGroup_Unknown()
    {
        Assert.Equal(InteractionAffordanceKind.Unknown, Classify(GenericViewGroup()));
    }

    // ── SEARCH-5: navigation LinearLayout row unchanged ─────────────────────

    [Fact]
    public void SEARCH5_NavigationRow_Unchanged()
    {
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, Classify(NavRow("Network & internet")));
    }

    // ── SEARCH-6: Switch/checkable -> LOCAL_CONTROL unchanged ───────────────

    [Fact]
    public void SEARCH6_Switch_Unchanged()
    {
        Assert.Equal(InteractionAffordanceKind.LocalControl, Classify(SwitchRow()));
    }

    // ── SEARCH-7: search control produces no NavigationSourceOccurrence ─────

    [Fact]
    public void SEARCH7_SearchControl_NoOccurrence()
    {
        var obs = Obs(1, SearchBar("Search settings"), NavRow("Network & internet"), NavRow("Apps"));
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(obs);

        Assert.Equal(2, occurrences.Length); // only the two nav rows
        Assert.DoesNotContain(occurrences, o => o.StructuredSignature.StartsWith("Search settings|", StringComparison.Ordinal));
    }

    // ── SEARCH-8: search control does not add logical-source cardinality ────

    [Fact]
    public void SEARCH8_SearchControl_CardinalityUnaffected()
    {
        var v1 = Obs(1, SearchBar("Search settings"), NavRow("A"), NavRow("B"));
        var v2 = Obs(2, SearchBar("Search settings"), NavRow("B"), NavRow("C"));
        var normalization = SourceEquivalenceNormalizer.Normalize(ImmutableArray.Create(v1, v2));

        Assert.True(normalization.IsResolved);
        Assert.Equal(3, normalization.UniqueSourceSignatures.Length); // A, B, C — search never a source
    }

    // ── SEARCH-9: real Settings-root-style evidence -> Unknown 1 -> 0 ───────

    [Fact]
    public void SEARCH9_RealSettingsRootStyle_UnknownCountZero()
    {
        var obs = Obs(1,
            SearchBar("Search settings"),
            NavRow("Network & internet"),
            NavRow("Connected devices"),
            NavRow("Apps"));
        var affordances = InteractionAffordanceAnalyzer.Analyze(obs);

        Assert.DoesNotContain(affordances, a => a.Classification == InteractionAffordanceKind.Unknown);
        Assert.Contains(affordances, a =>
            a.Classification == InteractionAffordanceKind.LocalControl
            && a.SourceResourceId == "com.android.settings:id/search_action_bar");
        Assert.Equal(3, affordances.Count(a => a.Classification == InteractionAffordanceKind.NavigationCandidate));
    }

    // ── SEARCH-10: no regression in COMPOSE-05 / PROV / SIG / RVT / AFF / SET ──
    // Covered by the full deterministic suite (see the gate result).
}
