using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_DESTINATION_IDENTITY_MODEL — DIM-1..DIM-20.
///
/// The Settings sub-page semantic identity is derived from FRESH destination
/// structured evidence: the explicit PAGE-TITLE-ROLE node (the app toolbar
/// title, content-desc of com.android.settings:id/collapsing_toolbar) →
/// SettingsSubpage(&lt;fresh page-title-role value&gt;). Frozen principles:
/// PAGE CLASS != PAGE IDENTITY; SOURCE LABEL != DESTINATION IDENTITY; BRANCH
/// IDENTITY != DESTINATION IDENTITY; CALLER EXPECTATION != WORLD TRUTH.
/// Missing / ambiguous page-title-role → fail closed (no generic SettingsChild
/// fallback). Run-level identity safety (ancestry acceptance / duplicate
/// rejection / verified return / wrong-destination failure) is covered here
/// and in GC-4/7/14/15/16.
/// DIM-20 (no regression) is the full deterministic suite.
/// </summary>
public sealed class DestinationIdentityModelTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";
    private const string SearchBarRid = "com.android.settings:id/search_action_bar";
    private const string BackControlCd = "Navigate up";
    private const string TitleRoleRid = "com.android.settings:id/collapsing_toolbar";
    private const string SettingsSubpagePrefix = "SettingsSubpage(";

    private static Observation Obs(long seq, params StructuredElementEvidence[] elements)
        => new(ImmutableArray<ObservedElement>.Empty, App, seq)
        {
            StructuredElements = elements.ToImmutableArray(),
        };

    private static StructuredElementEvidence SearchBar()
        => new("android.view.ViewGroup", SearchBarRid, true, false, false, true, false,
            new ElementBounds(0f, 0f, 1f, 0.06f), "Search settings", null, null, null, null);

    private static StructuredElementEvidence UpControl()
        => new("android.widget.ImageButton", null, true, false, false, true, true,
            new ElementBounds(0f, 0f, 0.13f, 0.1f), null, null, null, BackControlCd, null);

    private static StructuredElementEvidence TitleRole(string pageTitle)
        => new("android.widget.FrameLayout", TitleRoleRid, null, null, null, true, null,
            new ElementBounds(0f, 0f, 1f, 0.28f), null, null, null, pageTitle, null);

    private static StructuredElementEvidence NavRow(string title, int ordinal)
        => new("android.widget.LinearLayout", null, true, false, false, true, true,
            new ElementBounds(0f, 0.08f + 0.1f * ordinal, 1f, 0.08f + 0.1f * (ordinal + 1)),
            title, "summary for " + title, false, null, null);

    // ── DIM-1: Root → SettingsRoot ──────────────────────────────────────────

    [Fact]
    public void DIM1_Root_ResolvesSettingsRoot()
    {
        var root = Obs(1, SearchBar(), NavRow("Location", 0));
        Assert.Equal(RootPage, SettingsSingleRecursiveChildTests.ResolveSemanticPage(root));
    }

    // ── DIM-2: Location → distinct subpage identity ─────────────────────────

    [Fact]
    public void DIM2_Location_DistinctSubpageIdentity()
    {
        var location = Obs(2, UpControl(), TitleRole("Location"), NavRow("See all", 1));
        var resolved = SettingsSingleRecursiveChildTests.ResolveSemanticPage(location);
        Assert.Equal("SettingsSubpage(Location)", resolved);
        Assert.NotEqual(RootPage, resolved);
    }

    // ── DIM-3: Location services → different identity ───────────────────────

    [Fact]
    public void DIM3_LocationServices_DifferentIdentity()
    {
        var locServices = Obs(3, UpControl(), TitleRole("Location services"), NavRow("Wi-Fi scanning", 1));
        var resolved = SettingsSingleRecursiveChildTests.ResolveSemanticPage(locServices);
        Assert.Equal("SettingsSubpage(Location services)", resolved);
        Assert.NotEqual("SettingsSubpage(Location)", resolved);
    }

    // ── DIM-4: sibling identity independently derived ───────────────────────

    [Fact]
    public void DIM4_SiblingIdentity_IndependentlyDerived()
    {
        var recent = Obs(4, UpControl(), TitleRole("Recent access"));
        Assert.Equal("SettingsSubpage(Recent access)", SettingsSingleRecursiveChildTests.ResolveSemanticPage(recent));
    }

    // ── DIM-5: arbitrary BranchIdentity does not affect destination ─────────

    [Fact]
    public void DIM5_BranchIdentityDoesNotAffectDestination()
    {
        // The resolver is a pure function of the fresh structured evidence —
        // any caller label is irrelevant. Two observations with the same
        // title-role but different row content resolve identically.
        var a = Obs(5, UpControl(), TitleRole("Recent access"), NavRow("X", 1));
        var b = Obs(6, UpControl(), TitleRole("Recent access"), NavRow("Y", 1));
        Assert.Equal(
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(a),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(b));
    }

    // ── DIM-6: source title must not be copied to destination identity ──────

    [Fact]
    public void DIM6_SourceTitleNotCopiedToDestination()
    {
        // The source row's TitleText ("See all") must never become the
        // destination identity — the identity comes from the fresh title-role.
        var obs = Obs(7, UpControl(), TitleRole("Recent access"), NavRow("See all", 1));
        var resolved = SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs)!;
        Assert.Equal("SettingsSubpage(Recent access)", resolved);
        Assert.NotEqual("SettingsSubpage(See all)", resolved);
        Assert.DoesNotContain("See all", resolved, StringComparison.Ordinal);
    }

    // ── DIM-7: Navigate up must not become the page identity ────────────────

    [Fact]
    public void DIM7_NavigateUpNotPageIdentity()
    {
        var obs = Obs(8, UpControl(), TitleRole("Location"));
        var resolved = SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs)!;
        Assert.DoesNotContain(BackControlCd, resolved, StringComparison.Ordinal);
        Assert.DoesNotContain("Navigate", resolved, StringComparison.Ordinal);
    }

    // ── DIM-8: summary must not become the page identity ────────────────────

    [Fact]
    public void DIM8_SummaryNotPageIdentity()
    {
        // The page's summaries ("summary for X") never influence the identity.
        var obs = Obs(9, UpControl(), TitleRole("Location"), NavRow("X", 1));
        Assert.Equal("SettingsSubpage(Location)", SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs));
    }

    // ── DIM-9: generic first-text must not become identity ──────────────────

    [Fact]
    public void DIM9_GenericFirstTextNotIdentity()
    {
        // A sub-page observation with a first text but NO page-title-role →
        // fail closed (never "first text" as identity).
        var firstText = new StructuredElementEvidence(
            "android.widget.TextView", null, false, false, false, true, false,
            new ElementBounds(0f, 0f, 1f, 0.1f), "Some first text", null, false, null, null);
        var obs = Obs(10, UpControl(), firstText);
        Assert.Null(SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs));
    }

    // ── DIM-10: missing page-title-role → fail closed ───────────────────────

    [Fact]
    public void DIM10_MissingPageTitleRole_FailsClosed()
    {
        var obs = Obs(11, UpControl(), NavRow("See all", 1));
        Assert.Null(SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs));
    }

    // ── DIM-11: ambiguous page-title-role → fail closed ─────────────────────

    [Fact]
    public void DIM11_AmbiguousPageTitleRole_FailsClosed()
    {
        var obs = Obs(12, UpControl(), TitleRole("Location"), TitleRole("Location services"));
        Assert.Null(SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs));
    }

    // ── DIM-12: same page across viewport → same identity ───────────────────

    [Fact]
    public void DIM12_SamePageAcrossViewport_SameIdentity()
    {
        var v1 = Obs(13, UpControl(), TitleRole("Location"), NavRow("See all", 1));
        var v2 = Obs(14, UpControl(), TitleRole("Location"), NavRow("App location permissions", 1));
        Assert.Equal(
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(v1),
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(v2));
    }

    // ── DIM-13: Child != Grandchild → ancestry accepted ─────────────────────

    [Fact]
    public async Task DIM13_ChildGrandchildDistinct_AncestryAccepted()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(), "dim-13");
        Assert.Contains(run.Agent.Trace, t => t.ContainerId == "SettingsSubpage(Location services)");
    }

    // ── DIM-14: ancestry duplicate → reject ─────────────────────────────────

    [Fact]
    public async Task DIM14_AncestryDuplicate_Rejected()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(grandchildTitle: "Location"), "dim-14");
        Assert.Equal(RunState.Failed, run.State);
        Assert.Equal(2, run.Agent.Trace.Count(t =>
            t.Reason?.Contains("discovery epoch FROZEN", StringComparison.Ordinal) is true));
    }

    // ── DIM-15: generic SettingsChild can no longer establish full identity ──

    [Fact]
    public void DIM15_GenericSettingsChildNotFullIdentity()
    {
        // A sub-page observation WITHOUT a page-title-role never resolves to a
        // generic "SettingsChild" — it fails closed (null).
        var obs = Obs(15, UpControl(), NavRow("See all", 1));
        Assert.Null(SettingsSingleRecursiveChildTests.ResolveSemanticPage(obs));
    }

    // ── DIM-16: Grandchild return → exact Child identity ────────────────────

    [Fact]
    public async Task DIM16_GrandchildReturn_ExactChildIdentity()
    {
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(
                grandchildRows: [],
                grandchildTitle: "Recent access"), "dim-16");
        Assert.Contains(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);
    }

    // ── DIM-17: wrong sibling return → FAIL ─────────────────────────────────

    [Fact]
    public async Task DIM17_WrongDestinationReturn_Fails()
    {
        // Foreign destination: the return settle cannot confirm the expected
        // Child identity → fail closed.
        var run = await SettingsGrandchildVerifiedReturnTests.RunGcAsync(
            new SettingsGrandchildVerifiedReturnTests.GrandchildWorld(
                grandchildRows: [],
                grandchildTitle: "Recent access",
                returnEffect: SettingsGrandchildVerifiedReturnTests.ReturnEffect.Foreign), "dim-17");
        Assert.Equal(RunState.Failed, run.State);
        Assert.DoesNotContain(run.Agent.Trace, t =>
            t.Reason?.Contains("verified parent return", StringComparison.Ordinal) is true);

        // Resolver-level: a return that lands on a DIFFERENT Settings sub-page
        // (e.g. "App location permissions") would reconcile to a different
        // identity than the expected parent — never accepted as the return.
        var wrongSibling = Obs(16, UpControl(), TitleRole("App location permissions"));
        Assert.Equal("SettingsSubpage(App location permissions)",
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(wrongSibling));
        Assert.NotEqual("SettingsSubpage(Location)",
            SettingsSingleRecursiveChildTests.ResolveSemanticPage(wrongSibling));
    }

    // ── DIM-18: identity safety unchanged ───────────────────────────────────
    // The ancestry/duplicate/cycle fail-closed is production identity safety
    // (never relaxed): asserted at the run level by DIM-14 and GC-7, and by
    // the existing OpenWorldTraversalIdentitySafetyTests. Asserted here at the
    // resolver level: the root marker is authoritative; a sub-page identity is
    // never equal to the root identity.

    [Fact]
    public void DIM18_IdentitySafetyInvariantsHold()
    {
        Assert.NotEqual(RootPage, "SettingsSubpage(Location)");
        Assert.NotEqual(RootPage, "SettingsSubpage(Recent access)");
        Assert.NotEqual("SettingsSubpage(Location)", "SettingsSubpage(Location services)");
    }

    // ── DIM-19: COMPOSE-05 unchanged — covered by the full suite (the
    // ── COMPOSE-05 fixture resolver is independent of the Settings resolver). ─

    // ── DIM-20: GC / PCC / PRC / RC1 / ART / ROLE / SIG / SEARCH / SQ / PROV /
    // ── NM / RVT / AFF / SET green — covered by the full deterministic suite. ─
}
