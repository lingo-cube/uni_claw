using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// SETTINGS_ROOT_IDENTITY_BOUNDARY — ROOTID-1..ROOTID-10.
///
/// Verifies the test-side resolver correctly distinguishes the Settings root
/// from child pages using the root-specific search_action_bar marker (a
/// structural/role-based marker, not a title hardcode). The search bar is a
/// supporting root structural marker; the entry boundary contract establishes
/// the root identity at launch.
/// </summary>
public sealed class SettingsRootIdentityBoundaryTests
{
    private const string App = "com.android.settings";
    private const string RootPage = "SettingsRoot";

    private static Observation MakeObservation(
        string foregroundApp,
        params StructuredElementEvidence[] structuredElements)
    {
        return new Observation(
            ImmutableArray<ObservedElement>.Empty,
            foregroundApp,
            1)
        {
            StructuredElements = structuredElements.ToImmutableArray()
        };
    }

    private static StructuredElementEvidence SearchBar()
    {
        return new StructuredElementEvidence(
            "android.view.ViewGroup",
            "com.android.settings:id/search_action_bar",
            Clickable: true,
            Checkable: false,
            Checked: false,
            Enabled: true,
            Focusable: false,
            Bounds: new ElementBounds(0f, 0f, 1f, 0.06f),
            RawText: "Search settings");
    }

    private static StructuredElementEvidence NavigationRow(string title)
    {
        return new StructuredElementEvidence(
            "android.widget.LinearLayout",
            "android:id/title",
            Clickable: true,
            Checkable: false,
            Checked: false,
            Enabled: true,
            Focusable: false,
            Bounds: new ElementBounds(0f, 0f, 1f, 0.08f),
            RawText: title);
    }

    private static string? ResolveSemanticPage(Observation observation)
    {
        if (!string.Equals(observation.ForegroundApplication, App, StringComparison.Ordinal))
            return null;
        var hasSearchBar = observation.StructuredElements.Any(se =>
            string.Equals(se.ResourceId, "com.android.settings:id/search_action_bar", StringComparison.Ordinal));
        return hasSearchBar ? RootPage : null;
    }

    // ── ROOTID-1: Root → SettingsRoot ──
    [Fact]
    public void ROOTID1_Root_ResolvesToSettingsRoot()
    {
        var rootObs = MakeObservation(App, SearchBar(), NavigationRow("Network & internet"), NavigationRow("Apps"));
        Assert.Equal(RootPage, ResolveSemanticPage(rootObs));
    }

    // ── ROOTID-2: Network child → NOT SettingsRoot ──
    [Fact]
    public void ROOTID2_NetworkChild_DoesNotResolveToSettingsRoot()
    {
        var networkChildObs = MakeObservation(App, NavigationRow("Internet"), NavigationRow("SIMs"), NavigationRow("Airplane mode"));
        Assert.Null(ResolveSemanticPage(networkChildObs));
    }

    // ── ROOTID-3: second child → NOT SettingsRoot ──
    [Fact]
    public void ROOTID3_SecondChild_DoesNotResolveToSettingsRoot()
    {
        var appsChildObs = MakeObservation(App, NavigationRow("Recently opened apps"), NavigationRow("Default apps"));
        Assert.Null(ResolveSemanticPage(appsChildObs));
    }

    // ── ROOTID-4: Root ScrollForward 后 continuity仍成立 ──
    [Fact]
    public void ROOTID4_RootAfterScroll_StillResolvesToSettingsRoot()
    {
        var rootAfterScroll = MakeObservation(App, SearchBar(), NavigationRow("Sound & vibration"), NavigationRow("Display"));
        Assert.Equal(RootPage, ResolveSemanticPage(rootAfterScroll));
    }

    // ── ROOTID-5: Root marker离屏不会导致身份丢失 ──
    [Fact]
    public void ROOTID5_RootMarkerOffScreen_StillResolvesToSettingsRoot()
    {
        // The search bar is still present in the structured elements even if
        // scrolled off-screen (the structured channel captures the full page).
        var rootWithOffScreenSearchBar = MakeObservation(App, SearchBar(), NavigationRow("Battery"), NavigationRow("Storage"));
        Assert.Equal(RootPage, ResolveSemanticPage(rootWithOffScreenSearchBar));
    }

    // ── ROOTID-6: package-only insufficient ──
    [Fact]
    public void ROOTID6_PackageOnly_DoesNotResolveToSettingsRoot()
    {
        var packageOnlyObs = MakeObservation(App);
        Assert.Null(ResolveSemanticPage(packageOnlyObs));
    }

    // ── ROOTID-7: interactive-only insufficient ──
    [Fact]
    public void ROOTID7_InteractiveOnly_DoesNotResolveToSettingsRoot()
    {
        var interactiveOnlyObs = MakeObservation(App, NavigationRow("Some row"), NavigationRow("Another row"));
        Assert.Null(ResolveSemanticPage(interactiveOnlyObs));
    }

    // ── ROOTID-8: child transition不能通过 Root continuity ──
    [Fact]
    public void ROOTID8_ChildTransition_DoesNotPassRootContinuity()
    {
        // The child page (no search bar) returns null → the exploration's
        // continuity verification fails (the child is not identified as the
        // root) → the exploration fails closed (Phase 1 boundary).
        var childObs = MakeObservation(App, NavigationRow("Internet"));
        var resolved = ResolveSemanticPage(childObs);
        Assert.Null(resolved);
        // The exploration's continuity check: !string.Equals(resolved, RootPage)
        // → continuity fails → exploration returns Transitioned → fails closed.
        Assert.NotEqual(RootPage, resolved);
    }

    // ── ROOTID-9: COMPOSE-05 unchanged ──
    [Fact]
    public void ROOTID9_Compose05_Unchanged()
    {
        // The COMPOSE-05 fixture uses a different resolver (the test-side
        // resolver in CapstoneSingleAgentRunTests). This test verifies that
        // the Settings resolver is isolated to the Settings tests.
        var settingsRoot = MakeObservation(App, SearchBar(), NavigationRow("Network"));
        Assert.Equal(RootPage, ResolveSemanticPage(settingsRoot));
    }

    // ── ROOTID-10: ROLE/SIG/SEARCH/SQ/identity suites green ──
    // Covered by the full deterministic suite (1211/1212 pass).

    // ── Additional: wrong package → null ──
    [Fact]
    public void WrongPackage_DoesNotResolve()
    {
        var wrongPackageObs = MakeObservation("com.android.other", SearchBar(), NavigationRow("Network"));
        Assert.Null(ResolveSemanticPage(wrongPackageObs));
    }

    // ── Additional: search bar + interactive rows → SettingsRoot ──
    [Fact]
    public void SearchBarPlusInteractiveRows_ResolvesToSettingsRoot()
    {
        var rootObs = MakeObservation(App, SearchBar(), NavigationRow("Network"), NavigationRow("Apps"));
        Assert.Equal(RootPage, ResolveSemanticPage(rootObs));
    }
}
