using System.Diagnostics;
using System.Text.Json;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Tests;

/// <summary>New device evidence for ADR-0052 wiring. The decision source is a
/// deterministic double; this test makes no claim about real-model judgment.</summary>
[Collection("LiveDevice")]
public sealed class RuntimeOverviewLiveTests(ITestOutputHelper output)
{
    [Fact]
    public void RealSettingsEnterReturn_PreservesGraphAndMeteredReview()
    {
        if (Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_LIVE") != "1")
        {
            output.WriteLine("未启用真实设备；不作为ENVIRONMENT验收证据。");
            return;
        }
        var root = FindRoot();
        var device = LiveDeviceSelector.Resolve();
        Assert.True(device.IsUsable, device.Detail);
        using (var reset = Process.Start(new ProcessStartInfo("adb")
        {
            ArgumentList = { "-s", device.Serial!, "shell", "am", "start", "-S", "-a", "android.settings.SETTINGS" },
            RedirectStandardOutput = true,
        }))
        {
            Assert.NotNull(reset);
            reset.WaitForExit();
            Assert.Equal(0, reset.ExitCode);
        }
        Thread.Sleep(2000);
        var original = SettingsCoverageConfig.LoadDefault();
        var config = original with
        {
            Bounds = original.Bounds with
            {
                MaxSteps = 4, MaxConsultRounds = 8,
                RenewalThresholdSteps = 3, RenewalThresholdRounds = 2,
                GoalDirectedContinuationEnabled = true,
                AuthorizedTotalSteps = 12, AuthorizedTotalConsultRounds = 16,
            },
            Coverage = new CoverageRequirements(true, "all-visible", 0, 1, true, 0),
            TargetPages = new[] { "Network & internet" },
            DynamicRootEntries = new DynamicRootEntriesConfig(),
            DynamicNestedEntries = new DynamicNestedEntriesConfig(),
            DeepTraversal = new DeepTraversalConfig(),
            LanguageInspection = null,
            Slow = new SlowTriggerConfig(),
        };
        var reviews = 0;
        var contexts = new List<AgentDecisionContext>();
        var runRoot = Path.Combine(root, "evidence", "runtime-overview-001", "device-deterministic");
        var result = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
            device.Serial!,
            new LivePerception.LiveAssets(device.Serial!, "wifi-settings",
                Path.Combine(root, "platforms", "perception"),
                Path.Combine(root, ".perception", "venv", "bin", "python"),
                Path.Combine(root, ".perception", "cache")),
            config,
            context =>
            {
                contexts.Add(context);
                if (context.BudgetReview is { } review)
                {
                    reviews++;
                    var overview = context.ExecutionOverview!;
                    var assessment = new AgentProgressAssessment(
                        overview.OverviewId, "inspect then return", "return to observed root",
                        "fresh root observation after Navigate up",
                        new[] { "root route observed and back independently verified" },
                        overview.Projection!.SourceFactRefs,
                        new[] { "none" }, "new resource threshold or blocker",
                        Rationale: "One verified entry; return serves the remaining scoped obligation.",
                        NextItemKey: "requirement:back-navigation");
                    return new AgentDecision.Defer(context.DecisionId, new ObserveSpec(null, 1),
                        ReviewResponse: new BudgetReviewResponse(review.ReviewId,
                            BudgetReviewOutcome.RequestRenewal, assessment,
                            new BudgetRenewalRequest(2, 3, "one observed return and final scoped evaluation")));
                }
                if (context.Progress?.StepsDispatched >= 2)
                    return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId,
                        "The explicitly scoped entry and return are observed; unobserved branches remain unknown."));
                var descriptor = context.Progress?.StepsDispatched == 1 ? "Navigate up" : "Network & internet";
                Assert.Contains(context.Elements!, element => element.Text == descriptor);
                return new AgentDecision.Act(new AgentActionProposal(context.DecisionId,
                    new[] { new AgentActionStep("ui.element", descriptor, "tap", null) }, "device wiring probe"));
            }, ViewportWidth: 1080, ViewportHeight: 1920, UseCoverageDirector: false));
        output.WriteLine($"runDir={result.RunDir}; status={result.Status}; reason={result.Reason}");
        Assert.Equal(2, result.Steps.Count(step => step.Verified));
        Assert.Equal(1, reviews);
        Assert.All(contexts, context => Assert.NotNull(context.ExecutionOverview));
        Assert.Contains(contexts, context => context.ExecutionOverview?.Projection?.ActionableItems?
            .GetValueOrDefault("explore:unobserved-scope")?.ProgressFactKeys
            .Any(key => key.StartsWith("navigation:", StringComparison.Ordinal)) == true);
        using var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "world-navigation.json")));
        Assert.Equal(2, graph.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.Equal(2, graph.RootElement.GetProperty("edges").GetArrayLength());
        Assert.NotEmpty(graph.RootElement.GetProperty("unknown").EnumerateArray());
        using var facts = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "facts.json")));
        var grant = Assert.Single(facts.RootElement.GetProperty("budgetRenewals").EnumerateArray());
        Assert.True(grant.GetProperty("granted").GetBoolean());
        Assert.Equal(2, grant.GetProperty("additionalSteps").GetInt32());
        Assert.True(File.Exists(Path.Combine(result.RunDir, "environment-preflight.json")));
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        throw new InvalidOperationException("repository root unavailable");
    }
}
