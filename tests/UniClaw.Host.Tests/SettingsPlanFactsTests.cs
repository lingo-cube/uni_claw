using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

[Collection("SettingsCoverageConfigSerial")]
public sealed class SettingsPlanFactsTests
{
    [Fact]
    public void RootCandidates_UseLatestFactsAndKeepExcludedEntryEvidence()
    {
        var original = SettingsCoverageConfig.LoadDefault();
        var config = original with
        {
            TargetPages = new[] { "Network & internet", "Developer options" },
            DynamicRootEntries = new DynamicRootEntriesConfig(true, "all-visible"),
        };
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(config.RootRoute,
            new (string, string?)[] { ("ui.element", "Network & internet"), ("ui.element", "Developer options") });
        var first = SettingsPlanFacts.Project(SettingsPlanFacts.RootNavigationSource, ledger, config)!;
        Assert.Equal(PlanCandidateDisposition.Ready,
            Assert.Single(first.Candidates.Where(item => item.TargetDescriptor == "Network & internet")).Disposition);
        var blocked = Assert.Single(first.Candidates.Where(item => item.TargetDescriptor == "Developer options"));
        Assert.Equal(PlanCandidateDisposition.Blocked, blocked.Disposition);
        Assert.NotEmpty(blocked.EvidenceRefs);
        Assert.False(string.IsNullOrWhiteSpace(blocked.Reason));
        ledger.RecordObservation(config.RootRoute,
            new (string, string?)[] { ("ui.element", "Network & internet"), ("ui.element", "Connected devices") });
        var updated = SettingsPlanFacts.Project(SettingsPlanFacts.RootNavigationSource, ledger, config)!;
        Assert.Equal(first.ScopeKey, updated.ScopeKey);
        Assert.NotEqual(first.Revision, updated.Revision);
        Assert.Contains(updated.Candidates, item => item.TargetDescriptor == "Connected devices"
            && item.Disposition == PlanCandidateDisposition.Ready);
    }
    [Fact]
    public void CurrentPageCandidates_DiscoverNestedEntryKeepSwitchRecordOnlyAndRememberVerifiedReturn()
    {
        var config = SettingsCoverageConfig.LoadDefault() with
        {
            DynamicNestedEntries = new DynamicNestedEntriesConfig(true, 1, 4),
        };
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(config.RootRoute, new (string, string?)[] { ("ui.element", "Network & internet") });
        const string child = "android.settings|rk1:Network|src=title|up=1";
        const string grandchild = "android.settings|rk1:Connection details|src=title|up=1";
        ledger.RecordStep(new CoverageStepRecord(1, "d1", null, "ui.element", "Network & internet", "tap", null,
            true, "receipt-1", "DeliveryCompleted", "tap", config.RootRoute, child, "c1", true, null, "enter"));
        ledger.RecordObservation(child, new (string, string?)[]
        {
            ("ui.element", "Navigate up"), ("ui.element", "Connection details"), ("switch", "Auto sync"),
        });
        var candidates = SettingsPlanFacts.Project("settings.current-navigation", ledger, config);
        Assert.NotNull(candidates);
        Assert.Equal(child, candidates!.ScopeKey);
        Assert.Equal("Connection details", Assert.Single(candidates.Candidates).TargetDescriptor);
        Assert.DoesNotContain(candidates.Candidates, c => c.TargetDescriptor == "Auto sync");
        ledger.RecordStep(new CoverageStepRecord(2, "d1", null, "ui.element", "Connection details", "tap", null,
            true, "receipt-2", "DeliveryCompleted", "tap", child, grandchild, "c2", true, null, "nested-enter"));
        ledger.RecordObservation(grandchild, new (string, string?)[] { ("ui.element", "Navigate up"), ("switch", "Auto sync"), ("ui.element", "Outside configured nested depth") });
        Assert.DoesNotContain($"{grandchild}>Outside configured nested depth", ledger.DynamicNestedFrontier(config));
        Assert.Empty(SettingsPlanFacts.Project("settings.current-navigation", ledger, config)!.Candidates);
        ledger.RecordStep(new CoverageStepRecord(3, "d1", null, "ui.element", "Navigate up", "tap", null,
            true, "receipt-3", "DeliveryCompleted", "tap", grandchild, child, "c3", true, null, "back"));
        ledger.RecordObservation(child, new (string, string?)[] { ("ui.element", "Navigate up"), ("ui.element", "Connection details") });
        Assert.Empty(SettingsPlanFacts.Project("settings.current-navigation", ledger, config)!.Candidates);
    }

}
