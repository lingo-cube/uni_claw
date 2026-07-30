using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Runner;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

public sealed class EnumerateScenarioRunnerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-enum-{Guid.NewGuid():N}");

    private static readonly ScenarioSnapshot DefaultSnapshot =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "enumerate-settings-safely.v1.json"));

    /// <summary>
    /// Test 1: a single safe entry, end-of-list verified. The runner clicks in,
    /// backs out, then the planner reports the entry sampled and, with the
    /// end-of-list verified, completes success.
    /// </summary>
    [Fact]
    public async Task SingleSafeEntry_EndOfList_ReachesSuccess()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Home("home", [Menu("About phone")], isEnd: true),
                Home("home", [Menu("About phone")], isEnd: true),
                Child("about", "About phone"),
                Child("about", "About phone"),
                Home("home", [Menu("About phone")], isEnd: true),
                Home("home", [Menu("About phone")], isEnd: true),
            ],
            ["home", "about", "home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("enumerated_all_first_level", outcome.CompletionReason);
        Assert.Equal(["click", "back"], harness.Actions.Calls);
        Assert.Equal(2, outcome.SafetyAllowed);
        Assert.Equal(0, outcome.SafetyDenied);
    }

    /// <summary>
    /// Test 2: four entries across two screens. A scroll is required to reveal
    /// the second screen's entries before the end-of-list is verified.
    /// </summary>
    [Fact]
    public async Task MultiScreen_ScrollsAndSamplesAllEntries()
    {
        ImmutableArray<MenuInfo> screen1 = [Menu("About phone"), Menu("Battery")];
        ImmutableArray<MenuInfo> screen2 = [Menu("About phone"), Menu("Battery"), Menu("Apps"), Menu("Storage")];
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                // reset
                Home("s0", screen1, hasScroll: true),
                // step1: click About phone (pre s0, post about)
                Home("s0", screen1, hasScroll: true),
                Child("about", "About phone"),
                // step2: back (pre about, post s0)
                Child("about", "About phone"),
                Home("s0", screen1, hasScroll: true),
                // step3: click Battery (pre s0, post battery)
                Home("s0", screen1, hasScroll: true),
                Child("battery", "Battery"),
                // step4: back (pre battery, post s0)
                Child("battery", "Battery"),
                Home("s0", screen1, hasScroll: true),
                // step5: scroll (pre s0, post s1)
                Home("s0", screen1, hasScroll: true),
                Home("s1", screen2, hasScroll: true),
                // step6: click Apps (pre s1, post apps)
                Home("s1", screen2, hasScroll: true),
                Child("apps", "Apps"),
                // step7: back (pre apps, post s1)
                Child("apps", "Apps"),
                Home("s1", screen2, hasScroll: true),
                // step8: click Storage (pre s1, post storage)
                Home("s1", screen2, hasScroll: true),
                Child("storage", "Storage"),
                // step9: back (pre storage, post s1)
                Child("storage", "Storage"),
                Home("s1", screen2, hasScroll: true),
                // step10: scroll (pre s1, post s1 end-of-list)
                Home("s1", screen2, hasScroll: true),
                Home("s1", screen2, isEnd: true),
            ],
            // one fingerprint per action step (10 actions)
            ["s0", "about", "s0", "battery", "s0", "s1", "apps", "s1", "storage", "s1"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("enumerated_all_first_level", outcome.CompletionReason);
        // 4 clicks + 4 backs + 2 scrolls = 10 actions, all allowed.
        Assert.Equal(10, outcome.SafetyAllowed);
        Assert.Equal(0, outcome.SafetyDenied);
        Assert.Equal(10, harness.Actions.Calls.Count);
        Assert.Contains("click", harness.Actions.Calls);
        Assert.Contains("back", harness.Actions.Calls);
        Assert.Contains("scroll", harness.Actions.Calls);
    }

    /// <summary>
    /// Test 3: an entry reappears after a scroll. Dedup by normalized name
    /// ensures the reappearing entry is clicked only once.
    /// </summary>
    [Fact]
    public async Task DuplicateRowsAfterScroll_ClickedOnceByDedup()
    {
        ImmutableArray<MenuInfo> screen1 = [Menu("About phone")];
        // After scroll, About phone reappears (duplicate) but is already sampled.
        ImmutableArray<MenuInfo> screen2 = [Menu("About phone")];
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                // reset
                Home("s0", screen1, hasScroll: true),
                // step1: click About phone (pre s0, post about)
                Home("s0", screen1, hasScroll: true),
                Child("about", "About phone"),
                // step2: back (pre about, post s0)
                Child("about", "About phone"),
                Home("s0", screen1, hasScroll: true),
                // step3: all visible processed; scroll (pre s0, post s1 — About reappears, end-of-list)
                Home("s0", screen1, hasScroll: true),
                Home("s1", screen2, isEnd: true),
                // step4: pre s1 — About already sampled, IsEndOfList && all processed -> complete
                Home("s1", screen2, isEnd: true),
            ],
            // 3 action steps: click, back, scroll
            ["s0", "about", "s0"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("enumerated_all_first_level", outcome.CompletionReason);
        // About phone clicked exactly once despite reappearing.
        Assert.Equal(1, harness.Actions.Calls.Count(c => c == "click"));
        Assert.Contains("back", harness.Actions.Calls);
    }

    /// <summary>
    /// Test 4 (exploratory): a dangerous entry is denied by the safety gate.
    /// The runner writes a per-step "skipped" verification, marks the entry
    /// skipped, and continues. The run finishes success once the end-of-list is
    /// verified. "Reset options" is never clicked. (Decision 3.)
    /// </summary>
    [Fact]
    public async Task DangerousEntry_SkippedAndContinuedToEndOfList()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Home("home", [Menu("Reset options")], isEnd: true),
                Home("home", [Menu("Reset options")], isEnd: true),
                Home("home", [Menu("Reset options")], isEnd: true),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("enumerated_all_first_level", outcome.CompletionReason);
        Assert.Equal(0, outcome.SafetyAllowed);
        Assert.Equal(1, outcome.SafetyDenied);
        // Reset options is never clicked — the gate denies before execution.
        Assert.DoesNotContain("click", harness.Actions.Calls);
        Assert.DoesNotContain("back", harness.Actions.Calls);
    }

    /// <summary>
    /// Test 5: a back from a sampled child page lands off Settings. The back
    /// verification fails and the run finishes failure.
    /// </summary>
    [Fact]
    public async Task ReturnLandsOffSettings_FailsReturnVerification()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Home("home", [Menu("About phone")], isEnd: true),
                Home("home", [Menu("About phone")], isEnd: true),
                Child("about", "About phone"),
                Child("about", "About phone"),
                // Back lands off-Settings (unknown page, outside allowed pages
                // would throw a boundary exception; instead land on a page that
                // starts with "Settings" but is not home — but the planner only
                // checks Normalize(PageIdentity) == "settings". To make the back
                // verification fail without a boundary throw, the after page must
                // NOT equal "Settings". Use a child page that is still within the
                // allowed prefix but a different identity.)
                OffSettings("off", "Reset options"),
                OffSettings("off", "Reset options"),
            ],
            ["home", "about", "off"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.Equal("return_verification_failed", outcome.CompletionReason);
        Assert.Contains("click", harness.Actions.Calls);
        Assert.Contains("back", harness.Actions.Calls);
    }

    /// <summary>
    /// Test 6: the scroll budget is exhausted before the end-of-list is
    /// verified. The run finishes incomplete with <c>end_of_list_unproven</c>.
    /// </summary>
    [Fact]
    public async Task ScrollBudgetExhausted_NoEndOfListProof_IsIncomplete()
    {
        ImmutableArray<MenuInfo> entries = [Menu("About phone")];
        // Reduce the scroll budget so the test is short. After sampling About
        // phone, the planner scrolls until remainingScrolls hits 0, then reports
        // incomplete end_of_list_unproven (no end-of-list proof).
        var snapshot = DefaultSnapshot with
        {
            Scenario = DefaultSnapshot.Scenario with
            {
                Boundaries = DefaultSnapshot.Scenario.Boundaries with
                {
                    MaxSteps = 10,
                    MaxScrolls = 2,
                },
            },
        };
        var harness = await Harness.CreateAsync(
            _root,
            snapshot,
            [
                // reset
                Home("s0", entries, hasScroll: true),
                // step1: click About phone (pre s0, post about)
                Home("s0", entries, hasScroll: true),
                Child("about", "About phone"),
                // step2: back (pre about, post s0)
                Child("about", "About phone"),
                Home("s0", entries, hasScroll: true),
                // step3: scroll (pre s0, post s1)
                Home("s0", entries, hasScroll: true),
                Home("s1", entries, hasScroll: true),
                // step4: scroll (pre s1, post s2)
                Home("s1", entries, hasScroll: true),
                Home("s2", entries, hasScroll: true),
                // step5: pre s2, all visible processed, !IsEndOfList, remainingScrolls=0 -> incomplete
                Home("s2", entries, hasScroll: true),
            ],
            // 4 action steps: click, back, scroll, scroll
            ["s0", "about", "s0", "s1"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("incomplete", outcome.Status);
        Assert.Equal("end_of_list_unproven", outcome.CompletionReason);
    }

    /// <summary>
    /// Test 7: scroll is stuck — no new entries appear and no end-of-list is
    /// verified. The scroll verifier reports failure (page fingerprint did not
    /// change, not end-of-list). The run never reaches the end; the loop
    /// continues until the step budget or the run terminates via the stale-plan
    /// / no-progress path. We assert the run does not succeed and never claims
    /// the end-of-list.
    /// </summary>
    [Fact]
    public async Task ScrollStuck_NoNewEntries_NoEnd_NeverReachesEnd()
    {
        ImmutableArray<MenuInfo> entries = [Menu("About phone")];
        // After About phone is sampled, the planner scrolls. The scroll produces
        // the SAME fingerprint (stuck). The scroll verifier reports failure
        // (no change, not end). The base loop's OnScrollEndOfList does NOT fire
        // (IsEndOfList is false), so it loops. The next plan: still home, all
        // visible processed, !IsEndOfList, hasScroll, remainingScrolls>0 ->
        // scroll again. To avoid an infinite loop in the test, cap via a small
        // maxSteps by using a snapshot with reduced budgets.
        var snapshot = DefaultSnapshot with
        {
            Scenario = DefaultSnapshot.Scenario with
            {
                Boundaries = DefaultSnapshot.Scenario.Boundaries with
                {
                    MaxSteps = 6,
                    MaxScrolls = 3,
                },
            },
        };
        var harness = await Harness.CreateAsync(
            _root,
            snapshot,
            [
                Home("s0", entries, hasScroll: true),
                Home("s0", entries, hasScroll: true),
                Child("about", "About phone"),
                Child("about", "About phone"),
                Home("s0", entries, hasScroll: true),
                // scroll 1: stuck (same fp)
                Home("s0", entries, hasScroll: true),
                Home("s0", entries, hasScroll: true),
                // scroll 2: stuck
                Home("s0", entries, hasScroll: true),
                Home("s0", entries, hasScroll: true),
                // scroll 3: stuck
                Home("s0", entries, hasScroll: true),
                Home("s0", entries, hasScroll: true),
                // next plan: remainingScrolls=0 -> incomplete end_of_list_unproven
                Home("s0", entries, hasScroll: true),
            ],
            ["s0", "about", "s0", "s0", "s0", "s0"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.NotEqual("success", outcome.Status);
        Assert.DoesNotContain("enumerated_all_first_level", outcome.CompletionReason);
    }

    /// <summary>
    /// Test 8 (exploratory): all entries are dangerous and denied by the gate.
    /// The end-of-list is verified. With sampled=0 but every discovered entry
    /// skipped, the run finishes success (Decision 4 — all-dangerous + verified
    /// end-of-list = success).
    /// </summary>
    [Fact]
    public async Task AllDangerousAndVerifiedEndOfList_IsSuccess()
    {
        ImmutableArray<MenuInfo> dangerous = [Menu("Reset options"), Menu("Factory reset")];
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                // reset
                Home("home", dangerous, isEnd: true),
                // step1: plan click Reset options -> denied -> skipped -> continue
                Home("home", dangerous, isEnd: true),
                // step2: plan click Factory reset -> denied -> skipped -> continue
                Home("home", dangerous, isEnd: true),
                // step3: all discovered processed, IsEndOfList -> complete success
                Home("home", dangerous, isEnd: true),
            ],
            // one fingerprint per action step (2 denied clicks)
            ["home", "home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("enumerated_all_first_level", outcome.CompletionReason);
        Assert.Equal(0, outcome.SafetyAllowed);
        Assert.Equal(2, outcome.SafetyDenied);
        Assert.DoesNotContain("click", harness.Actions.Calls);
        Assert.Empty(harness.Actions.Calls);
    }

    private static ImmutableArray<MenuInfo> Menus(params string[] names) =>
        names.Select(n => Menu(n)).ToImmutableArray();

    private static ScenarioObservation Home(
        string fingerprint,
        ImmutableArray<MenuInfo> level1Menus,
        bool hasScroll = false,
        bool isEnd = false) =>
        RunnerTestHarness.Observation(
            fingerprint,
            "Settings",
            level1Menus: level1Menus,
            hasScroll: hasScroll,
            isEnd: isEnd);

    private static ScenarioObservation Child(
        string fingerprint,
        string page) =>
        RunnerTestHarness.Observation(
            fingerprint,
            page,
            screenshot: new byte[40]);

    private static ScenarioObservation OffSettings(
        string fingerprint,
        string page) =>
        RunnerTestHarness.Observation(
            fingerprint,
            page,
            screenshot: new byte[40]);

    private static MenuInfo Menu(string name) =>
        RunnerTestHarness.Menu(name);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class Harness
    {
        private Harness(EnumerateScenarioRunner runner, RunnerHarness inner)
        {
            Runner = runner;
            Actions = inner.Actions;
            Assets = inner.Assets;
        }

        public EnumerateScenarioRunner Runner { get; }

        public FakeActionExecutor Actions { get; }

        public RunAssetSession Assets { get; }

        public static async Task<Harness> CreateAsync(
            string root,
            ScenarioSnapshot snapshot,
            IEnumerable<object> observations,
            IEnumerable<string> fingerprints)
        {
            var inner = await RunnerTestHarness.CreateAsync(
                root,
                snapshot,
                observations,
                fingerprints,
                (s, p, svc, src) => new EnumerateScenarioRunner(s, p, svc, src));
            return new Harness(
                (EnumerateScenarioRunner)inner.Runner,
                inner);
        }
    }
}