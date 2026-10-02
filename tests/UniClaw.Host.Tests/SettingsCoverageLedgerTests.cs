using UniClaw.Host.SettingsCoverage;
using Xunit;

namespace UniClaw.Host.Tests;

[Collection("SettingsCoverageConfigSerial")]
public sealed class SettingsCoverageLedgerTests
{
    private const string RootRoute = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private const string ScrollDescriptor = "com.android.settings:id/main_content_scrollable_container";
    private const string BackDescriptor = "Navigate up";

    private static readonly string[] TargetPages =
    [
        "Network & internet",
        "Connected devices",
        "Apps",
        "Notifications",
        "Battery",
        "Storage",
        "Sound & vibration",
    ];

    private static SettingsCoverageConfig Config() => SettingsCoverageConfig.LoadDefault();

    private static SettingsCoverageConfig ConfigWith(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-coverage-ledger-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        try
        {
            return SettingsCoverageConfig.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Observe(SettingsCoverageLedger ledger, string route, params string[] descriptors) =>
        ledger.RecordObservation(route,
            descriptors.Select(descriptor => ("ui.element", (string?)descriptor)).ToList());

    private static void EnterStep(SettingsCoverageLedger ledger, string descriptor, string routeAfter,
        bool verified = true, string? receiptId = null, string? failureReason = null, string? directive = null) =>
        ledger.RecordStep(new CoverageStepRecord(
            Index: 0,
            DecisionId: $"decision-{descriptor}-{routeAfter}-{Guid.NewGuid():N}",
            DshSessionId: "dsh-session-1",
            TargetRole: "ui.element",
            TargetDescriptor: descriptor,
            EffectClass: "tap",
            DesiredState: null,
            PreActionTargetUnique: true,
            ReceiptId: receiptId ?? $"receipt-{Guid.NewGuid():N}",
            ReceiptOutcome: verified ? "ok" : "mismatch",
            ReceiptCommand: "tap",
            RouteBefore: RootRoute,
            RouteAfter: routeAfter,
            PostCaptureId: $"capture-{Guid.NewGuid():N}",
            Verified: verified,
            FailureReason: failureReason,
            Directive: directive));

    private static void BackStep(SettingsCoverageLedger ledger, string routeBefore, bool verified = true) =>
        ledger.RecordStep(new CoverageStepRecord(
            Index: 0,
            DecisionId: $"decision-back-{Guid.NewGuid():N}",
            DshSessionId: "dsh-session-1",
            TargetRole: "ui.element",
            TargetDescriptor: BackDescriptor,
            EffectClass: "tap",
            DesiredState: null,
            PreActionTargetUnique: true,
            ReceiptId: $"receipt-{Guid.NewGuid():N}",
            ReceiptOutcome: verified ? "ok" : "mismatch",
            ReceiptCommand: "tap",
            RouteBefore: routeBefore,
            RouteAfter: verified ? RootRoute : routeBefore,
            PostCaptureId: $"capture-{Guid.NewGuid():N}",
            Verified: verified,
            FailureReason: verified ? null : "back-not-verified",
            Directive: null));

    private static void ScrollStep(SettingsCoverageLedger ledger, bool verified) =>
        ledger.RecordStep(new CoverageStepRecord(
            Index: 0,
            DecisionId: $"decision-scroll-{Guid.NewGuid():N}",
            DshSessionId: "dsh-session-1",
            TargetRole: "ui.element",
            TargetDescriptor: ScrollDescriptor,
            EffectClass: "swipe-up",
            DesiredState: "scroll-down",
            PreActionTargetUnique: true,
            ReceiptId: $"receipt-{Guid.NewGuid():N}",
            ReceiptOutcome: verified ? "ok" : "mismatch",
            ReceiptCommand: "swipe-up",
            RouteBefore: RootRoute,
            RouteAfter: RootRoute,
            PostCaptureId: $"capture-{Guid.NewGuid():N}",
            Verified: verified,
            FailureReason: verified ? null : "post-action-target-unique",
            Directive: null));

    private static void EnterAllTargetPages(SettingsCoverageLedger ledger)
    {
        foreach (var page in TargetPages)
            EnterStep(ledger, page, $"android.settings|route:{page}");
    }

    private static CoverageItem Item(SettingsCoverageLedger ledger, string requirement)
    {
        var report = ledger.Report(Config());
        return report.Items.Single(item => item.Requirement == requirement);
    }

    [Fact]
    public void RootObservation_CoversRootPage()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages[0], TargetPages[1]);

        var item = Item(ledger, "root-page");
        Assert.True(item.Covered);

        var snapshot = ledger.Snapshot(Config());
        Assert.Equal(RootRoute, snapshot.CurrentRoute);
        Assert.False(snapshot.CoverageComplete);
    }

    [Fact]
    public void EnterAllTargetsAndBack_CoversFirstLevelSecondLevelAndBack()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        EnterAllTargetPages(ledger);
        BackStep(ledger, $"android.settings|route:{TargetPages[^1]}");

        Assert.True(Item(ledger, "first-level-all-visible").Covered);
        Assert.True(Item(ledger, "second-level-pages").Covered);
        Assert.True(Item(ledger, "back-navigation").Covered);
    }

    [Fact]
    public void ScrollDiscoveredEntry_RequiresVerifiedScrollBeforeObservation()
    {
        var ledger = new SettingsCoverageLedger();
        // Root observation before any scroll: "Accessibility" must NOT become a candidate.
        Observe(ledger, RootRoute, TargetPages);
        ScrollStep(ledger, verified: true);
        // Root observation after the verified scroll: "Accessibility" is discovered.
        Observe(ledger, RootRoute, "Accessibility");
        EnterStep(ledger, "Accessibility", "android.settings|route:Accessibility");

        var item = Item(ledger, "scroll-discovered-entry");
        Assert.True(item.Covered);
        Assert.NotNull(item.Evidence);
        Assert.Contains("Accessibility", item.Evidence);
    }

    [Fact]
    public void UnverifiedScroll_DoesNotCountScrollsOrProduceCandidates()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        ScrollStep(ledger, verified: false);
        Observe(ledger, RootRoute, "Accessibility");
        EnterStep(ledger, "Accessibility", "android.settings|route:Accessibility");

        var report = ledger.Report(Config());
        Assert.Equal(1, report.ScrollAttempts);
        Assert.Equal(0, report.ScrollsUsed);
        Assert.False(report.Items.Single(item => item.Requirement == "scroll-discovered-entry").Covered);
    }

    [Fact]
    public void RepeatedEntry_DifferentReceipts_SatisfiesRepeatedEntry()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        EnterStep(ledger, "Battery", "android.settings|route:Battery", receiptId: "receipt-1");
        EnterStep(ledger, "Battery", "android.settings|route:Battery-again", receiptId: "receipt-2");

        Assert.True(Item(ledger, "repeated-entry").Covered);

        var report = ledger.Report(Config());
        Assert.Equal(report.StepsExecuted, report.StepsVerified);
        Assert.Null(report.FirstDivergence);
    }

    [Fact]
    public void DuplicateDispatchedStepsWithoutVerifiedBetween_FlagsFirstDivergence()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        EnterStep(ledger, "Apps", "android.settings|route:Apps", verified: false,
            failureReason: "receipt-no-route-change", receiptId: "receipt-1");
        EnterStep(ledger, "Apps", "android.settings|route:Apps", verified: false,
            failureReason: "receipt-no-route-change", receiptId: "receipt-2");

        var divergence = ledger.Report(Config()).FirstDivergence;
        Assert.NotNull(divergence);
        Assert.Contains("duplicate-effect-no-interleaving-verified-step", divergence);
    }

    [Fact]
    public void DuplicateDispatchedStepsWithVerifiedBetween_NotFlagged()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        EnterStep(ledger, "Apps", "android.settings|route:Apps", verified: false,
            failureReason: "receipt-no-route-change", receiptId: "receipt-1");
        EnterStep(ledger, "Notifications", "android.settings|route:Notifications", verified: true,
            receiptId: "receipt-2");
        EnterStep(ledger, "Apps", "android.settings|route:Apps", verified: false,
            failureReason: "receipt-no-route-change", receiptId: "receipt-3");

        var divergence = ledger.Report(Config()).FirstDivergence;
        Assert.NotNull(divergence);
        Assert.DoesNotContain("duplicate-effect", divergence);
    }

    [Fact]
    public void ConsecutiveFailures_TriggerMaxConsecutiveFailuresReached()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            EnterStep(ledger, "Apps", RootRoute, verified: false,
                failureReason: "post-action-target-unique");
        }

        var snapshot = ledger.Snapshot(Config());
        Assert.True(snapshot.MaxConsecutiveFailuresReached);
        Assert.Equal(3, ledger.Report(Config()).StepsExecuted);
        Assert.Equal(0, ledger.Report(Config()).StepsVerified);
    }

    [Fact]
    public void MaxStepsReached_AfterExecutingBoundSteps()
    {
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        for (var attempt = 0; attempt < 24; attempt++)
        {
            EnterStep(ledger, "Apps", RootRoute, verified: false,
                failureReason: "post-action-target-unique");
        }

        Assert.True(ledger.Snapshot(Config()).MaxStepsReached);
    }

    [Fact]
    public void NextDirective_FollowsPrioritySequence()
    {
        var ledger = new SettingsCoverageLedger();

        // Initial: first not-entered target page.
        Observe(ledger, RootRoute, TargetPages);
        var initial = ledger.Snapshot(Config());
        Assert.Equal("enter", initial.NextDirectiveKind);
        Assert.Contains($"<{TargetPages[0]}>", initial.NextDirective);

        // All targets entered, currently inside a second-level page → back.
        EnterAllTargetPages(ledger);
        Observe(ledger, $"android.settings|route:{TargetPages[^1]}");
        var insideSecondLevel = ledger.Snapshot(Config());
        Assert.Equal("back", insideSecondLevel.NextDirectiveKind);
        Assert.Contains($"<{BackDescriptor}>", insideSecondLevel.NextDirective);

        // Back at the root → scroll.
        Observe(ledger, RootRoute);
        var backAtRoot = ledger.Snapshot(Config());
        Assert.Equal("scroll", backAtRoot.NextDirectiveKind);
        Assert.Contains($"<{ScrollDescriptor}>", backAtRoot.NextDirective);

        // Verified scroll discovers a new entry; enter it → re-enter directive.
        //（滚动后的根页观察同时含既有目标与新条目——可见性约束下
        // re-enter 只指挥当前可见的已进入目标。）
        ScrollStep(ledger, verified: true);
        Observe(ledger, RootRoute, TargetPages.Append("Accessibility").ToArray());
        EnterStep(ledger, "Accessibility", "android.settings|route:Accessibility");
        var afterScrollEntry = ledger.Snapshot(Config());
        Assert.Equal("re-enter", afterScrollEntry.NextDirectiveKind);
        Assert.Contains($"<{TargetPages[0]}>", afterScrollEntry.NextDirective);

        // Complete the coverage (re-enter first target, verify back, stay at root) → null.
        EnterStep(ledger, TargetPages[0], "android.settings|route:Network-Reentry");
        BackStep(ledger, "android.settings|route:Network-Reentry");
        Observe(ledger, RootRoute, TargetPages);
        var complete = ledger.Snapshot(Config());
        Assert.True(complete.CoverageComplete);
        Assert.Null(complete.NextDirective);
        Assert.Null(complete.NextDirectiveKind);
    }

    [Fact]
    public void FailedEntryCandidate_IsSkipped_NotRetried()
    {
        // AGT-007：候选进入验证失败（如路由指纹碰撞页）后不得重试。
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        EnterAllTargetPages(ledger);
        ScrollStep(ledger, verified: true);
        Observe(ledger, RootRoute, TargetPages.Append("Accessibility").Append("System").ToArray());
        // Accessibility 进入失败（Verified=false，路由未变）。
        ledger.RecordStep(new CoverageStepRecord(
            99, "decision-x", null, "ui.element", "Accessibility", "tap", null,
            true, "receipt-x", "DeliveryCompleted", "tap",
            RootRoute, RootRoute, null, false, "post-action-target-unique", null));

        var snapshot = ledger.Snapshot(ConfigWith("""
            configVersion: "1"
            session:
              taskTitle: fail-skip-test
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 24
              maxConsultRounds: 24
              maxScrolls: 4
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 2
              secondLevelPages: 2
              backNavigation: false
              repeatedEntries: 0
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            rootRoute: android.settings|rk1:Settings|src=homepage_title|up=0
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """));
        // 失败的 Accessibility 被跳过；下一个候选是 System。
        Assert.Equal("enter", snapshot.NextDirectiveKind);
        Assert.Contains("<System>", snapshot.NextDirective);
        Assert.DoesNotContain("<Accessibility>", snapshot.NextDirective);
    }

    [Fact]
    public void BackDescriptor_IsNeverAScrollCandidate()
    {
        // AGT-006 补：标题与根页相同的子页会被误判为根页，其 "Navigate up"
        // 不应成为滚动候选目标（真机 c4 step 21 实证）。
        var ledger = new SettingsCoverageLedger();
        EnterAllTargetPages(ledger);
        ScrollStep(ledger, verified: true);
        Observe(ledger, RootRoute, TargetPages.Append(BackDescriptor).ToArray());

        var snapshot = ledger.Snapshot(Config());
        if (snapshot.NextDirectiveKind == "enter")
            Assert.DoesNotContain($"<{BackDescriptor}>", snapshot.NextDirective);
    }

    [Fact]
    public void EnteredScrollCandidate_IsNotReEntered_NextDirectiveAdvances()
    {
        // AGT-006 回归：候选不在 TargetPages 内，曾因 entered 跟踪只算
        // TargetPages 而被反复重进（30 enters/1 swipe 的真机循环）。
        var ledger = new SettingsCoverageLedger();
        Observe(ledger, RootRoute, TargetPages);
        EnterAllTargetPages(ledger);
        ScrollStep(ledger, verified: true);
        Observe(ledger, RootRoute, TargetPages.Append("Accessibility").Append("System").ToArray());
        EnterStep(ledger, "Accessibility", "android.settings|route:Accessibility");
        BackStep(ledger, "android.settings|route:Accessibility");
        Observe(ledger, RootRoute, TargetPages.Append("Accessibility").Append("System").ToArray());

        var snapshot = ledger.Snapshot(ConfigWith("""
            configVersion: "1"
            session:
              taskTitle: candidate-test
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 24
              maxConsultRounds: 24
              maxScrolls: 4
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 2
              secondLevelPages: 2
              backNavigation: false
              repeatedEntries: 0
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            rootRoute: android.settings|rk1:Settings|src=homepage_title|up=0
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """));
        // 已进入的 Accessibility 不再被指挥；下一个候选是 System。
        Assert.Equal("enter", snapshot.NextDirectiveKind);
        Assert.Contains("<System>", snapshot.NextDirective);
        Assert.DoesNotContain("<Accessibility>", snapshot.NextDirective);
    }

    [Fact]
    public void Report_ComputesCoverageAndStepSuccessRates()
    {
        var ledger = new SettingsCoverageLedger();
        var config = ConfigWith("""
            configVersion: "1"
            session:
              taskTitle: rate-test
              workspace: UniClaw_Product_Tasks
              workspaceReuse: true
              autoCloseTurn: false
            bounds:
              maxSteps: 24
              maxConsultRounds: 24
              maxScrolls: 4
              maxConsecutiveFailures: 3
              maxDirectiveRetries: 1
            coverage:
              rootPage: true
              firstLevelMode: all-visible
              scrollDiscoveredEntries: 1
              secondLevelPages: 2
              backNavigation: false
              repeatedEntries: 0
            targetPages:
              - Network & internet
            termination:
              onCoverageComplete: true
              onMaxSteps: true
              onMaxScrolls: true
              onConsecutiveFailures: true
            rootRoute: android.settings|rk1:Settings|src=homepage_title|up=0
            scrollContainerDescriptor: com.android.settings:id/main_content_scrollable_container
            backDescriptor: Navigate up
            """);
        Observe(ledger, RootRoute, "Network & internet");
        EnterStep(ledger, "Network & internet", "android.settings|route:Network");
        EnterStep(ledger, "Connected devices", "android.settings|route:Devices");
        EnterStep(ledger, "Apps", "android.settings|route:Apps");
        EnterStep(ledger, "Notifications", "android.settings|route:Notifications",
            verified: false, failureReason: "post-action-target-unique");

        var report = ledger.Report(config);
        Assert.Equal(5.0 / 6.0, report.CoverageRate, precision: 6);   // scroll-discovered-entry uncovered
        Assert.Equal(3.0 / 4.0, report.StepSuccessRate, precision: 6); // 3 verified of 4 executed
    }

    [Fact]
    public void Report_StatusReflectsCoverageCompletionOrBoundedStop()
    {
        var complete = new SettingsCoverageLedger();
        Observe(complete, RootRoute, TargetPages);
        EnterAllTargetPages(complete);
        // scroll-discovered + repeated-entry + back still needed for full coverage.
        ScrollStep(complete, verified: true);
        Observe(complete, RootRoute, "Accessibility");
        EnterStep(complete, "Accessibility", "android.settings|route:Accessibility");
        EnterStep(complete, TargetPages[0], "android.settings|route:Network-Reentry");
        BackStep(complete, "android.settings|route:Network-Reentry");

        var completeReport = complete.Report(Config());
        Assert.Equal("CoverageComplete", completeReport.Status);
        Assert.Empty(completeReport.UncoveredItems);
        Assert.Equal(1.0, completeReport.CoverageRate, precision: 6);

        var truncated = new SettingsCoverageLedger();
        Observe(truncated, RootRoute, TargetPages);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            EnterStep(truncated, "Apps", RootRoute, verified: false,
                failureReason: "post-action-target-unique");
        }

        var truncatedReport = truncated.Report(Config());
        Assert.Equal("BoundedStop", truncatedReport.Status);
        Assert.NotEmpty(truncatedReport.UncoveredItems);
        Assert.Contains("first-level-all-visible", truncatedReport.UncoveredItems);
    }
}
