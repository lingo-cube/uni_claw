using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsCoverageProgressConsistencyTests
{
    private const string Root = "android.settings|route:Settings";
    private const string Network = "android.settings|route:Network";

    private static SettingsCoverageConfig Config() => new(
        "1", new("settings", "workspace", true, false), new(20, 20, 4, 3, 1),
        new(true, "configured", 0, 0, false, 0), ["Network"],
        new(true, true, true, true), Root, "scroll-container", "Navigate up",
        DynamicNestedEntries: new(true, 3, 4));

    [Fact]
    public void VerifiedRootEntry_IsConsistentWithReport_WithoutCompletingItsUnvisitedChild()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new[] { ("ui.element", (string?)"Network") });
        ledger.RecordStep(Entry());
        ledger.RecordObservation(Network, new[] { ("ui.element", (string?)"Details") });

        var report = ledger.Report(Config());
        Assert.True(Assert.Single(report.Items, item => item.Requirement == "first-level-all-visible").Covered);
        Assert.Empty(ledger.TargetMarks);

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());
        Assert.Contains(projection.KnownItems, item => item.StartsWith("[verified][configured-target] Network", StringComparison.Ordinal));
        Assert.DoesNotContain("root:Network", projection.ActionableItems!.Keys);
        Assert.Contains("verified:configured-root:Network", projection.ProgressFactKeys!);
        var verification = Assert.Single(ledger.Events, item => item.Type == CoverageEventType.PostActionVerified);
        Assert.Contains($"coverage-event:{verification.Sequence}", projection.SourceFactRefs);
        Assert.Contains(projection.ActionableItems.Keys, key => key.Contains("Network>Details", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(Root, Network, false, "tap", "blocked")]
    [InlineData(Root, Root, true, "tap", "pending")]
    [InlineData("android.settings|route:Apps", Network, true, "tap", "pending")]
    [InlineData(Root, Network, true, "swipe-up", "pending")]
    [InlineData(Root, null, true, "tap", "pending")]
    [InlineData("android.settings", Network, true, "click", "verified")]
    public void EntryStatus_FollowsOwnerRootArrivalEvidence(
        string before, string? after, bool verified, string effect, string status)
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new[] { ("ui.element", (string?)"Network") });
        ledger.RecordStep(Entry() with
        {
            RouteBefore = before, RouteAfter = after, Verified = verified, EffectClass = effect,
        });

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());
        Assert.Contains(projection.KnownItems, item => item.StartsWith($"[{status}][configured-target] Network", StringComparison.Ordinal));
        Assert.Equal(status != "verified", projection.ActionableItems!.ContainsKey("root:Network"));
        Assert.Equal(status == "verified", Assert.Single(ledger.Report(Config()).Items,
            item => item.Requirement == "first-level-all-visible").Covered);
    }

    [Fact]
    public void ObservationAlone_DoesNotProveEntry()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new[] { ("ui.element", (string?)"Network") });

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());
        Assert.Contains(projection.KnownItems, item => item.StartsWith("[pending][configured-target] Network", StringComparison.Ordinal));
        Assert.Contains("root:Network", projection.ActionableItems!.Keys);
    }

    [Fact]
    public void ExplicitSkip_RemainsSkipRatherThanVerifiedEntry()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new[] { ("ui.element", (string?)"Network") });
        Assert.True(ledger.TryMarkTargetSkipped("Network", "decision-skip", "outside task scope"));

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());
        Assert.Contains(projection.KnownItems, item => item.StartsWith("[skipped][configured-target] Network", StringComparison.Ordinal));
        Assert.DoesNotContain("verified:configured-root:Network", projection.ProgressFactKeys!);
        Assert.DoesNotContain("root:Network", projection.ActionableItems!.Keys);
    }

    [Fact]
    public void FailedReentry_DoesNotEraseEarlierArrival_AndRemainsASeparateBlocker()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new[] { ("ui.element", (string?)"Network") });
        ledger.RecordStep(Entry());
        var successfulVerification = Assert.Single(ledger.Events, item => item.Type == CoverageEventType.PostActionVerified);
        ledger.RecordStep(Entry() with
        {
            Index = 2, ReceiptId = "receipt-reentry", Verified = false, FailureReason = "reentry failed",
        });

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());
        Assert.Contains(projection.KnownItems, item => item.StartsWith("[verified][configured-target] Network", StringComparison.Ordinal));
        Assert.Contains($"coverage-event:{successfulVerification.Sequence}", projection.SourceFactRefs);
        Assert.Contains(projection.Blockers, item => item.Contains("reentry failed", StringComparison.Ordinal));
    }

    private static CoverageStepRecord Entry() => new(
        1, "decision-enter", null, "ui.element", "Network", "tap", null, true,
        "receipt-enter", "success", "tap", Root, Network, "capture-network", true, null, null);
}
