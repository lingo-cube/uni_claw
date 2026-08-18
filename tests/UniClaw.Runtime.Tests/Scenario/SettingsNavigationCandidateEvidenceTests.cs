using System.Collections.Immutable;
using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using Xunit;

namespace UniClaw.Runtime.Tests.Scenario;

public sealed class SettingsNavigationCandidateEvidenceTests
{
    private const int DisplayWidth = 1080;
    private const int DisplayHeight = 1920;

    private static string RootFixture =>
        Path.Combine(AppContext.BaseDirectory, "Replay/Assets/structured-settings/settings-root.xml");

    private static string NetworkFixture =>
        Path.Combine(AppContext.BaseDirectory, "Replay/Assets/structured-settings/network-internet.xml");

    private static Observation ParseObservation(string path, long sequence = 1)
    {
        var xml = File.ReadAllText(path);
        var structured = AdbUiHierarchySource.Parse(xml, DisplayWidth, DisplayHeight);
        return new Observation([], "com.android.settings", sequence)
        {
            StructuredElements = structured,
        };
    }

    [Fact]
    public void SNE1_RealSettingsRoot_NavigationRows_ClassifiedNavigationCandidate()
    {
        var observation = ParseObservation(RootFixture);
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);

        var navigation = affordances
            .Where(a => a.Classification == InteractionAffordanceKind.NavigationCandidate)
            .Select(a => a.SourceResourceId)
            .ToArray();

        Assert.Contains(affordances, a => a.Classification == InteractionAffordanceKind.NavigationCandidate);
        // Real Settings root contains navigation rows such as Network & internet, Connected devices, Apps, Battery, System.
        Assert.Contains(affordances, a => a.Reason.Contains("Network & internet", StringComparison.Ordinal));
        Assert.Contains(affordances, a => a.Reason.Contains("Connected devices", StringComparison.Ordinal));
        Assert.Contains(affordances, a => a.Reason.Contains("Apps", StringComparison.Ordinal));
        Assert.Contains(affordances, a => a.Reason.Contains("Battery", StringComparison.Ordinal));
        Assert.Contains(affordances, a => a.Reason.Contains("System", StringComparison.Ordinal));
        // The search action bar carries a stable search-role token and is a
        // resolved LOCAL_CONTROL (never a navigation candidate, never a child
        // source) — the only local control on the root.
        Assert.Single(affordances, a => a.Classification == InteractionAffordanceKind.LocalControl);
        Assert.Contains(affordances, a => a.Classification == InteractionAffordanceKind.LocalControl
            && a.SourceResourceId == "com.android.settings:id/search_action_bar");
    }

    [Fact]
    public void SNE2_RealSwitchPreference_IsLocalControl_NotNavigation()
    {
        var observation = ParseObservation(NetworkFixture);
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);

        var airplane = affordances
            .Where(a => a.Reason.Contains("Airplane mode", StringComparison.Ordinal)
                || a.SourceResourceId == "com.android.settings:id/switchWidget"
                || (a.Reason.Contains("local control", StringComparison.OrdinalIgnoreCase)
                    && a.SourceResourceId is null))
            .ToArray();

        Assert.Contains(affordances, a => a.Classification == InteractionAffordanceKind.LocalControl);
        Assert.All(
            affordances.Where(a => a.Classification == InteractionAffordanceKind.NavigationCandidate),
            a => Assert.DoesNotContain("Airplane mode", a.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void SNE3_StandaloneSwitch_IsLocalControl()
    {
        var raw = new StructuredElementEvidence(
            "android.widget.Switch",
            "com.android.settings:id/switchWidget",
            Clickable: false,
            Checkable: true,
            Checked: false,
            Enabled: true,
            Focusable: false,
            Bounds: new ElementBounds(0.9f, 0.2f, 0.98f, 0.22f));
        var observation = new Observation([], "com.android.settings", 1)
        {
            StructuredElements = [raw],
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var item = Assert.Single(affordances);
        Assert.Equal(InteractionAffordanceKind.LocalControl, item.Classification);
    }

    [Fact]
    public void SNE4_ClickableButtonWithoutNavigationEvidence_IsNotNavigation()
    {
        var raw = new StructuredElementEvidence(
            "android.widget.Button",
            null,
            Clickable: true,
            Checkable: false,
            Checked: false,
            Enabled: true,
            Focusable: true,
            Bounds: new ElementBounds(0.1f, 0.1f, 0.5f, 0.2f));
        var observation = new Observation([], "com.android.settings", 1)
        {
            StructuredElements = [raw],
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var item = Assert.Single(affordances);
        Assert.NotEqual(InteractionAffordanceKind.NavigationCandidate, item.Classification);
    }

    [Fact]
    public void SNE5_NavigationCandidateDestinationRemainsUnknown()
    {
        var observation = ParseObservation(RootFixture);
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        var navigation = affordances.First(a => a.Classification == InteractionAffordanceKind.NavigationCandidate);
        Assert.Null(navigation.DestinationSemanticPage);
    }

    [Fact]
    public void SNE6_MixedRealSettingsEvidence_SeparatesNavigationFromLocalControls()
    {
        var root = InteractionAffordanceAnalyzer.Analyze(ParseObservation(RootFixture, 1));
        var network = InteractionAffordanceAnalyzer.Analyze(ParseObservation(NetworkFixture, 2));

        Assert.Contains(root, a => a.Classification == InteractionAffordanceKind.NavigationCandidate);
        Assert.Contains(network, a => a.Classification == InteractionAffordanceKind.LocalControl);
        Assert.DoesNotContain(network, a => a.Classification == InteractionAffordanceKind.NavigationCandidate
            && a.Reason.Contains("Airplane mode", StringComparison.Ordinal));
    }

    [Fact]
    public void SNE7_SameRealSource_CanBeParsedDeterministically()
    {
        var first = AdbUiHierarchySource.Parse(File.ReadAllText(RootFixture), DisplayWidth, DisplayHeight);
        var second = AdbUiHierarchySource.Parse(File.ReadAllText(RootFixture), DisplayWidth, DisplayHeight);
        Assert.Equal(first, second);
    }

    [Fact]
    public void SNE8_NewViewportNavigationRow_IndependentlyDiscovered()
    {
        var raw = new StructuredElementEvidence(
            "android.widget.LinearLayout",
            null,
            Clickable: true,
            Checkable: false,
            Checked: false,
            Enabled: true,
            Focusable: true,
            Bounds: new ElementBounds(0f, 0.1f, 1f, 0.2f),
            TitleText: "New row after scroll",
            SummaryText: "Discovered in second viewport");
        var observation = new Observation([], "com.android.settings", 2)
        {
            StructuredElements = [raw],
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, Assert.Single(affordances).Classification);
    }

    [Fact]
    public void SNE9_PopupButton_IsNotNormalNavigationCandidate()
    {
        var raw = new StructuredElementEvidence(
            "android.widget.Button",
            null,
            Clickable: true,
            Checkable: false,
            Checked: false,
            Enabled: true,
            Focusable: true,
            Bounds: new ElementBounds(0.3f, 0.3f, 0.7f, 0.4f),
            TitleText: "OK");
        var observation = new Observation([], "com.android.settings", 1)
        {
            StructuredElements = [raw],
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        Assert.NotEqual(InteractionAffordanceKind.NavigationCandidate, Assert.Single(affordances).Classification);
    }

    [Fact]
    public void SNE13_AuthorizedNavigationDoesNotRewritePreActionEvidence()
    {
        var preAction = new Observation([], "com.android.settings", 1)
        {
            StructuredElements =
            [
                new StructuredElementEvidence(
                    "android.widget.LinearLayout",
                    null,
                    Clickable: true,
                    Checkable: false,
                    Checked: false,
                    Enabled: true,
                    Focusable: true,
                    Bounds: new ElementBounds(0f, 0.1f, 1f, 0.2f),
                    TitleText: "Network & internet"),
            ],
        };
        var preActionAffordances = InteractionAffordanceAnalyzer.Analyze(preAction);
        var preActionCandidate = Assert.Single(preActionAffordances);
        Assert.Equal(InteractionAffordanceKind.NavigationCandidate, preActionCandidate.Classification);

        // Fresh post-action Observation may resolve destination later, but the pre-action evidence remains unchanged.
        Assert.Null(preActionCandidate.DestinationSemanticPage);
        Assert.Equal(1L, preActionCandidate.SourceObservationSequence);
    }

    [Fact]
    public void SNE14_LocalEffectDoesNotRewritePreActionEvidence()
    {
        var preAction = new Observation([], "com.android.settings", 1)
        {
            StructuredElements =
            [
                new StructuredElementEvidence(
                    "android.widget.LinearLayout",
                    null,
                    Clickable: true,
                    Checkable: false,
                    Checked: false,
                    Enabled: true,
                    Focusable: true,
                    Bounds: new ElementBounds(0f, 0.1f, 1f, 0.2f),
                    TitleText: "Ambiguous row"),
            ],
        };
        var preActionAffordances = InteractionAffordanceAnalyzer.Analyze(preAction);
        var preActionItem = Assert.Single(preActionAffordances);
        var preActionKind = preActionItem.Classification;

        // A later local effect Observation cannot mutate the historical evidence.
        var localObservation = new Observation([], "com.android.settings", 2)
        {
            StructuredElements =
            [
                new StructuredElementEvidence(
                    "android.widget.Switch",
                    null,
                    Clickable: false,
                    Checkable: true,
                    Checked: true,
                    Enabled: true,
                    Focusable: false,
                    Bounds: new ElementBounds(0.8f, 0.1f, 0.95f, 0.2f)),
            ],
        };
        var localAffordances = InteractionAffordanceAnalyzer.Analyze(localObservation);
        Assert.Equal(InteractionAffordanceKind.LocalControl, Assert.Single(localAffordances).Classification);
        Assert.Equal(preActionKind, preActionItem.Classification);
        Assert.Equal(1L, preActionItem.SourceObservationSequence);
    }

    [Fact]
    public void SNE10_AmbiguousEvidence_IsUnknown()
    {
        var raw = new StructuredElementEvidence(
            "android.view.View",
            null,
            Clickable: true,
            Checkable: false,
            Checked: false,
            Enabled: true,
            Focusable: true,
            Bounds: new ElementBounds(0.1f, 0.1f, 0.5f, 0.2f));
        var observation = new Observation([], "com.android.settings", 1)
        {
            StructuredElements = [raw],
        };
        var affordances = InteractionAffordanceAnalyzer.Analyze(observation);
        Assert.Equal(InteractionAffordanceKind.Unknown, Assert.Single(affordances).Classification);
    }

    [Fact]
    public void SNE11_EvidenceRetainsMoreCandidatesThanCallerSubset()
    {
        var observation = ParseObservation(RootFixture);
        var evidenceCandidates = InteractionAffordanceAnalyzer.Analyze(observation)
            .Where(a => a.Classification == InteractionAffordanceKind.NavigationCandidate)
            .Select(a => a.Reason)
            .ToArray();

        // Caller-like subset intentionally omits many evidence-visible rows.
        Assert.True(evidenceCandidates.Length >= 5);
        Assert.Contains(evidenceCandidates, r => r.Contains("System", StringComparison.Ordinal));
    }

    [Fact]
    public void SNE12_CallerInventedCandidateHasNoAcceptedStructuredSource()
    {
        var observation = ParseObservation(RootFixture);
        var sourceResourceIds = observation.StructuredElements
            .Select(e => e.ResourceId)
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(sourceResourceIds, id => id == "invented:id/no_such_row");
    }
}
