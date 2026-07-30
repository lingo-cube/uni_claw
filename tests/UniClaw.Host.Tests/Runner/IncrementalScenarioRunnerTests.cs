using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Artifacts;
using UniClaw.Host.Commands;
using UniClaw.Host.Runner;
using UniClaw.Host.Safety;
using UniClaw.Host.Scenarios;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

public sealed class IncrementalScenarioRunnerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"uniclaw-runner-{Guid.NewGuid():N}");

    private static readonly ScenarioSnapshot DefaultSnapshot =
        new ScenarioCatalog().LoadSnapshot(
            Path.Combine(
                AppContext.BaseDirectory,
                "Scenarios",
                "locate-one-item.v1.json"));

    [Fact]
    public async Task Compiler_IsDeterministicAndPlanIsPersistedBeforeExecution()
    {
        var compiler = new ScenarioPlanCompiler();
        var first = compiler.Compile(DefaultSnapshot);
        var second = compiler.Compile(DefaultSnapshot);
        var assets = await new RunAssetStore().CreateAsync(
            _root,
            DefaultSnapshot,
            first,
            Manifest("plan-run"));

        Assert.Equal(first.ToJson(), second.ToJson());
        Assert.Equal("target_only", first.IntentSlots?.Scope);
        Assert.Equal("menu_only", first.IntentSlots?.ElementHandling);
        Assert.Equal(DefaultSnapshot.ScenarioHash, first.Meta?["scenarioHash"]);
        Assert.True(File.Exists(Path.Combine(assets.RunDirectory, "plan.json")));
    }

    [Fact]
    public async Task TargetVisible_ExecutesOneAllowedClickAndVerifiesPage()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("about", "About phone"),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("target_page_identity_verified", outcome.CompletionReason);
        Assert.Equal(["click"], harness.Actions.Calls);
        Assert.Equal(1, outcome.SafetyAllowed);
        Assert.Equal(0, outcome.SafetyDenied);
    }

    [Fact]
    public async Task TargetAfterScroll_UsesBoundedScrollThenOneClick()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home-0", "Settings", hasScroll: true),
                Observation("home-0", "Settings", hasScroll: true),
                Observation("home-1", "Settings", Item("About phone"), hasScroll: true),
                Observation("home-1", "Settings", Item("About phone"), hasScroll: true),
                Observation("about", "About phone"),
            ],
            ["home-0", "home-1"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal(["scroll", "click"], harness.Actions.Calls);
        Assert.Equal(1, outcome.Scrolls);
        Assert.Equal(2, outcome.Steps);
    }

    [Fact]
    public async Task TargetAbsent_ReportsIncompleteWithoutOverstatement()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", isEnd: true),
                Observation("home", "Settings", isEnd: true),
            ],
            []);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("incomplete", outcome.Status);
        Assert.Equal("target_absent_at_verified_end", outcome.CompletionReason);
        Assert.Empty(harness.Actions.Calls);
        Assert.False(await ResultSucceededAsync(harness.Assets));
    }

    [Fact]
    public async Task StalePlan_IsRejectedBeforeSafetyOrDevice()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
            ],
            ["changed"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.Equal("stale_plan", outcome.CompletionReason);
        Assert.Empty(harness.Actions.Calls);
        Assert.Single(outcome.IssueFingerprints);
    }

    [Fact]
    public async Task DangerousTarget_IsDeniedWithZeroDeviceCalls()
    {
        var dangerous = DefaultSnapshot with
        {
            Scenario = DefaultSnapshot.Scenario with
            {
                Target = new ScenarioTarget("Reset options", []),
            },
        };
        var harness = await Harness.CreateAsync(
            _root,
            dangerous,
            [
                Observation("home", "Settings", Item("Reset options"), hasScroll: true),
                Observation("home", "Settings", Item("Reset options"), hasScroll: true),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("blocked", outcome.Status);
        Assert.Equal("deny.dangerous.text", outcome.CompletionReason);
        Assert.Empty(harness.Actions.Calls);
        Assert.Equal(1, outcome.SafetyDenied);
    }

    [Fact]
    public async Task VerificationMismatch_IsOperationalFailure()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("home", "Settings", Item("About phone"), hasScroll: true),
                Observation("settings-other", "Settings"),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.StartsWith("verification_mismatch", outcome.CompletionReason);
        Assert.Equal(["click"], harness.Actions.Calls);
    }

    [Fact]
    public async Task VerificationAcceptsLargeVisualTransitionWhenHierarchyIsStale()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings", Item("About phone"), hasScroll: true,
                    screenshot: new byte[100]),
                Observation("home", "Settings", Item("About phone"), hasScroll: true,
                    screenshot: new byte[100]),
                Observation("stale-home", "Settings", screenshot: new byte[10]),
            ],
            ["home"]);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("success", outcome.Status);
        Assert.Equal("target_page_visual_transition_verified", outcome.CompletionReason);
        Assert.Equal(["click"], harness.Actions.Calls);
    }

    [Theory]
    [InlineData("device_offline")]
    [InlineData("provider_timeout")]
    public async Task ObservationFailures_AreClassifiedAndRetainResult(string kind)
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings"),
                new ScenarioObservationException(kind, "injected failure"),
            ],
            []);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("failure", outcome.Status);
        Assert.Equal(kind, outcome.CompletionReason);
        Assert.Single(outcome.IssueFingerprints);
        Assert.True(File.Exists(Path.Combine(harness.Assets.RunDirectory, "result.json")));
    }

    [Fact]
    public async Task Cancellation_FinalizesCancelledResult()
    {
        var harness = await Harness.CreateAsync(
            _root,
            DefaultSnapshot,
            [
                Observation("home", "Settings"),
                new OperationCanceledException(),
            ],
            []);

        var outcome = await harness.Runner.RunAsync();

        Assert.Equal("cancelled", outcome.Status);
        Assert.Equal("cancelled", outcome.CompletionReason);
        Assert.False(await ResultSucceededAsync(harness.Assets));
    }

    private static async Task<bool> ResultSucceededAsync(RunAssetSession assets)
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(assets.RunDirectory, "result.json"));
        return json.Contains(
            "\"successCriteriaSatisfied\": true",
            StringComparison.Ordinal);
    }

    private static MenuItem Item(string name) => RunnerTestHarness.Item(name);

    private static ScenarioObservation Observation(
        string fingerprint,
        string page,
        MenuItem? item = null,
        bool hasScroll = false,
        bool isEnd = false,
        byte[]? screenshot = null) =>
        RunnerTestHarness.Observation(
            fingerprint,
            page,
            item,
            hasScroll,
            isEnd,
            screenshot);

    private static RunManifestInput Manifest(string runId) =>
        RunnerTestHarness.Manifest(runId);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class Harness
    {
        private Harness(IncrementalScenarioRunner runner, RunnerHarness inner)
        {
            Runner = runner;
            Actions = inner.Actions;
            Assets = inner.Assets;
        }

        public IncrementalScenarioRunner Runner { get; }

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
                (s, p, svc, src) => new IncrementalScenarioRunner(s, p, svc, src));
            return new Harness(
                (IncrementalScenarioRunner)inner.Runner,
                inner);
        }
    }
}
