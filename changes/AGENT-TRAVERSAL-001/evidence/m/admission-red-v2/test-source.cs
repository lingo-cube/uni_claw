using System.Text.Json;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed partial class SettingsCoverageScenarioTests
{
    [Fact]
    public void LocalAct_ScrollThenEnterUsesFreshSameContainerFacts_AndEndsAtDestination()
    {
        var world = new SimulatedSettingsWorld();
        var contexts = new List<AgentDecisionContext>();
        var root = Path.Combine(Path.GetTempPath(), "local-act-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = SettingsCoverageConfig.LoadDefault() with
            {
                Bounds = new(8, 8, 4, 3, 0),
                Coverage = new(true, "configured", 0, 0, false, 0),
                TargetPages = ["Accessibility"],
                DynamicRootEntries = new(false), DynamicNestedEntries = new(false),
                LanguageInspection = null, NavigationScope = null,
                RootRoute = RootRoute, ScrollContainerDescriptor = ScrollContainer, BackDescriptor = "Navigate up",
            };
            var result = SettingsCoverageRunner.Run(root, new SettingsCoverageRunner.Options(
                "emulator-sim", new("emulator-sim", "unused", "/unused", "/unused", "/unused"),
                config, context =>
                {
                    contexts.Add(context);
                    return contexts.Count == 1
                        ? new AgentDecision.Act(new(context.DecisionId,
                            [new("scrollable", ScrollContainer, "swipe-up", null),
                             new("ui.element", "Accessibility", "tap", null)], "local scroll then final entry"))
                        : new AgentDecision.Blocked(context.DecisionId, "fixture segment ended");
                }, ViewportWidth: Width, ViewportHeight: Height, FeedNext: world.Next,
                CurrentCaptureId: () => world.CurrentCaptureId, EffectDriver: new SimulatedEffectDriver(world),
                UseCoverageDirector: false));

            Assert.Equal(2, result.Steps.Count);
            Assert.All(result.Steps, step => Assert.True(step.Verified, step.FailureReason));
            Assert.Equal("android.settings|route:Accessibility", world.Route);
            Assert.DoesNotContain(contexts[0].Elements!, element => element.Text == "Accessibility");
            Assert.Equal(AgentDecisionPhase.StepVerified, contexts[1].Phase);
            Assert.Equal(2, result.Steps.Select(step => step.ReceiptId).Distinct().Count());
            Assert.All(result.Steps, step => Assert.Equal(contexts[0].DecisionId, step.DecisionId));
            using var guards = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "current-step-guards.json")));
            var screens = guards.RootElement.EnumerateArray().Select(row => row.GetProperty("screen").GetProperty("containerId").GetString()).ToArray();
            Assert.Equal(2, screens.Length);
            Assert.Equal(screens[0], screens[1]);
        }
        finally { Directory.Delete(root, true); }
    }
}
