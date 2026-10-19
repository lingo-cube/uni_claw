using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsCoverageOverviewProjectorTests
{
    private const string Root = "android.settings|route:Settings";

    private static SettingsCoverageConfig Config() => new(
        ConfigVersion: "1",
        Session: new CoverageSessionConfig("settings", "workspace", true, false),
        Bounds: new CoverageBounds(20, 20, 4, 3, 1),
        Coverage: new CoverageRequirements(true, "all-visible", 1, 1, false, 0),
        TargetPages: new[] { "Configured seed", "Configured verified" },
        Termination: new CoverageTermination(true, true, true, true),
        RootRoute: Root,
        ScrollContainerDescriptor: "scroll-container",
        BackDescriptor: "Navigate up",
        DynamicRootEntries: new DynamicRootEntriesConfig(true, "all-visible", new[] { "Configured seed" }));

    [Fact]
    public void Projection_KeepsUnobservedSeedPending_AndSeparatesObservedDynamicCandidate()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new (string, string?, string?)[]
        {
            ("ui.element", "Configured verified", "rid/configured"),
            ("ui.element", "Dynamic entry", "rid/dynamic"),
        });
        Assert.True(ledger.TryMarkTargetReached(
            "Configured verified", TargetCoverageMarkKind.RouteEntered,
            "decision-1", "android.settings|route:configured", new[] { "capture:configured" }));

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());

        Assert.Contains(projection.KnownItems, item => item.Contains("[pending][configured-target] Configured seed", StringComparison.Ordinal)
            && item.Contains("configured seed; not yet observed", StringComparison.Ordinal));
        Assert.Contains(projection.KnownItems, item => item.Contains("[verified][configured-target] Configured verified", StringComparison.Ordinal));
        Assert.Contains(projection.KnownItems, item => item.Contains("[pending][dynamic-observation] Dynamic entry", StringComparison.Ordinal));
        Assert.DoesNotContain(projection.KnownItems, item => item.Contains("[skipped]", StringComparison.Ordinal)
            && item.Contains("Configured seed", StringComparison.Ordinal));
        Assert.Contains("coverage-config:targetPages", projection.SourceFactRefs);
        Assert.Contains(projection.SourceFactRefs, item => item.StartsWith("coverage-event:", StringComparison.Ordinal));
        Assert.Equal($"coverage-ledger:{ledger.LastEventSequence}", projection.ProjectionRevision);
        Assert.Contains("root:Configured seed", projection.ActionableItems!.Keys);
        Assert.DoesNotContain("root:Configured verified", projection.ActionableItems.Keys);
        Assert.All(projection.ActionableItems.Values.SelectMany(item => item.SourceFactRefs),
            reference => Assert.Contains(reference, projection.SourceFactRefs));
    }

    [Fact]
    public void Projection_RevisitingKnownPagesDoesNotCreateProgressFacts()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, Array.Empty<(string, string?)>());
        var root = SettingsCoverageOverviewProjector.Project(ledger, Config());
        ledger.RecordObservation("android.settings|route:Known page", Array.Empty<(string, string?)>());
        var other = SettingsCoverageOverviewProjector.Project(ledger, Config());
        ledger.RecordObservation(Root, Array.Empty<(string, string?)>());
        var returned = SettingsCoverageOverviewProjector.Project(ledger, Config());

        Assert.NotEqual(root.CurrentBranch, other.CurrentBranch);
        Assert.Equal(root.ProgressFactKeys, other.ProgressFactKeys);
        Assert.Equal(root.ProgressFactKeys, returned.ProgressFactKeys);
    }

    [Fact]
    public void Projection_PreservesUnknownPageAndUnresolvedFailureAsSeparateFacts()
    {
        var ledger = new SettingsCoverageLedger();
        ledger.RecordObservation(Root, new (string, string?, string?)[]
        {
            ("ui.element", "Dynamic entry", "rid/dynamic"),
        });
        ledger.RecordObservation(SettingsCoverageDirector.UnknownRouteIdentity, Array.Empty<(string, string?)>());
        ledger.RecordStep(new CoverageStepRecord(
            Index: 1, DecisionId: "decision-failed", DshSessionId: null,
            TargetRole: "ui.element", TargetDescriptor: "Dynamic entry", EffectClass: "tap",
            DesiredState: null, PreActionTargetUnique: true, ReceiptId: "receipt-failed",
            ReceiptOutcome: "mismatch", ReceiptCommand: "tap", RouteBefore: Root,
            RouteAfter: SettingsCoverageDirector.UnknownRouteIdentity, PostCaptureId: "capture-failed",
            Verified: false, FailureReason: "arrival was not verified", Directive: "enter"));

        var projection = SettingsCoverageOverviewProjector.Project(ledger, Config());

        Assert.NotEmpty(projection.Unknowns);
        Assert.Contains(projection.Blockers, item => item.Contains("Dynamic entry", StringComparison.Ordinal));
        Assert.Contains(projection.KnownItems, item => item.Contains("[blocked][dynamic-observation] Dynamic entry", StringComparison.Ordinal));
        Assert.Contains("coverage-event:1", projection.SourceFactRefs);
    }
}
