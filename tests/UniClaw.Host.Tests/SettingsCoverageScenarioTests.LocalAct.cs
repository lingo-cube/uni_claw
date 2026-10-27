using System.Text.Json;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed partial class SettingsCoverageScenarioTests
{
    [Fact]
    public void LocalAct_ContainerChangeRejectsTail_AndReturnsExactFailureStep()
    {
        var outcome = RunLocalActCase("scope-tail",
            [new("ui.element", "Network & internet", "tap", null),
             new("ui.element", "Navigate up", "tap", null)]);

        Assert.Single(outcome.Result.Steps);
        Assert.Equal("android.settings|route:Network & internet", outcome.Route);
        var feedback = outcome.Contexts[1];
        Assert.Equal(AgentDecisionPhase.StepRejected, feedback.Phase);
        Assert.Equal(1, feedback.FailedStepIndex);
        Assert.StartsWith("act-scope-changed", feedback.FailureReason);
        Assert.Contains(outcome.Contexts[0].DecisionId, feedback.FailureReason);
        Assert.Contains(outcome.Result.Steps[0].ReceiptId!, feedback.FailureReason);
        Assert.Contains("Navigate up", feedback.FailureReason);
    }

    [Theory]
    [InlineData("switch", "Accessibility", "switch target requires typed desiredState", false)]
    [InlineData("ui.element", "Missing entry", "current target is missing or ambiguous", false)]
    [InlineData("ui.element", "Factory reset", "target matches forbidden pattern", false)]
    [InlineData("ui.element", "Missing entry", "current target is missing or ambiguous", true)]
    public void LocalAct_LaterRejectedOrUnknownStepHasExactIndexAndReason_WithoutDelivery(
        string role, string target, string reason, bool asPlan)
    {
        var outcome = RunLocalActCase("guard-" + role + "-" + target + "-" + asPlan,
            [new("scrollable", ScrollContainer, "swipe-up", null), new(role, target, "tap", null)], asPlan: asPlan);

        Assert.Single(outcome.Result.Steps);
        Assert.Equal(1, outcome.Deliveries);
        Assert.Equal(AgentDecisionPhase.StepRejected, outcome.Contexts[1].Phase);
        Assert.Equal(1, outcome.Contexts[1].FailedStepIndex);
        Assert.Contains(reason, outcome.Contexts[1].FailureReason);
        Assert.DoesNotContain("no-response", outcome.Contexts[1].FailureReason);
    }

    [Fact]
    public void LocalAct_PlanMayCrossContainers_WhileActCanReplanFromNewContainer()
    {
        AgentActionStep[] steps = [new("ui.element", "Network & internet", "tap", null),
            new("ui.element", "Navigate up", "tap", null)];
        var plan = RunLocalActCase("plan-crossing", steps, asPlan: true);
        Assert.Equal(2, plan.Result.Steps.Count);
        Assert.All(plan.Result.Steps, step => Assert.True(step.Verified));
        Assert.Equal(RootRoute, plan.Route);

        var revised = RunLocalActCase("act-revised", steps, reviseAfterScopeFailure: true);
        Assert.Equal(2, revised.Result.Steps.Count);
        Assert.All(revised.Result.Steps, step => Assert.True(step.Verified));
        Assert.Equal(RootRoute, revised.Route);
        Assert.Equal(2, revised.Result.Steps.Select(step => step.DecisionId).Distinct().Count());
        Assert.Equal(2, revised.Result.Steps.Select(step => step.ReceiptId).Distinct().Count());
        Assert.Equal(AgentDecisionPhase.StepRejected, revised.Contexts[1].Phase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LocalAct_GoalDirectedAndDirectCompositionUseTheSameScopeRule(bool goalDirected)
    {
        var allowed = RunLocalActCase("same-container-" + goalDirected,
            [new("scrollable", ScrollContainer, "swipe-up", null),
             new("ui.element", "Accessibility", "tap", null)], goalDirected: goalDirected);
        Assert.Equal(2, allowed.Result.Steps.Count);
        Assert.All(allowed.Result.Steps, step => Assert.True(step.Verified));
        var stopped = RunLocalActCase("cross-container-" + goalDirected,
            [new("ui.element", "Network & internet", "tap", null),
             new("ui.element", "Navigate up", "tap", null)], goalDirected: goalDirected);
        Assert.Single(stopped.Result.Steps);
        Assert.StartsWith("act-scope-changed", stopped.Contexts[1].FailureReason);
    }

    [Fact]
    public void LocalAct_FailedFirstVerificationDoesNotDispatchTheNextStep()
    {
        var outcome = RunLocalActCase("first-verification-failed",
            [new("scrollable", ScrollContainer, "swipe-up", null),
             new("ui.element", "Network & internet", "tap", null)], noScrollProgress: true);
        Assert.Single(outcome.Result.Steps);
        Assert.Equal(1, outcome.Deliveries);
        Assert.False(outcome.Result.Steps[0].Verified);
        Assert.Equal(AgentDecisionPhase.VerificationFailed, outcome.Contexts[1].Phase);
        Assert.Equal(0, outcome.Contexts[1].FailedStepIndex);
    }

    [Theory]
    [InlineData(17, 32, "too-many-steps")]
    [InlineData(2, 1, "budget-exceeded:steps")]
    public void LocalAct_ProposalAndStepBudgetRemainEnforced(int count, int budget, string reason)
    {
        var outcome = RunLocalActCase("budget-" + count,
            Enumerable.Repeat(new AgentActionStep("scrollable", ScrollContainer, "swipe-up", null), count).ToArray(),
            maxSteps: budget);
        Assert.Equal(0, outcome.Deliveries);
        Assert.Equal(RunDriveStatus.AgentDecisionFailed, outcome.Result.Status);
        Assert.Contains(reason, outcome.Result.Reason);
    }

    [Fact]
    public void LocalAct_SingleForbiddenActionRetainsPolicyReasonInsteadOfNoResponse()
    {
        var outcome = RunLocalActCase("single-rejected", [new("ui.element", "Factory reset", "tap", null)]);
        Assert.Equal(0, outcome.Deliveries);
        Assert.Equal(AgentDecisionPhase.StepRejected, outcome.Contexts[1].Phase);
        Assert.Contains("target matches forbidden pattern", outcome.Contexts[1].FailureReason);
        Assert.DoesNotContain("no-response", outcome.Contexts[1].FailureReason);
    }

    [Fact]
    public void LocalAct_ScrollThenEnterUsesFreshSameContainerFacts_AndEndsAtDestination()
    {
        var world = new SimulatedSettingsWorld { UseConfirmedPageObservations = true };
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

    private sealed record LocalActCaseOutcome(
        SettingsCoverageRunner.RunResult Result,
        IReadOnlyList<AgentDecisionContext> Contexts,
        string Route,
        int Deliveries);

    private static LocalActCaseOutcome RunLocalActCase(
        string name, AgentActionStep[] steps, bool asPlan = false,
        int maxSteps = 8, bool goalDirected = false,
        bool noScrollProgress = false, bool reviseAfterScopeFailure = false)
    {
        var world = new SimulatedSettingsWorld { UseConfirmedPageObservations = true };
        if (noScrollProgress) world.MakeAlreadyAtBottom();
        var effects = new SimulatedEffectDriver(world);
        var contexts = new List<AgentDecisionContext>();
        var root = Path.Combine(Path.GetTempPath(), "local-act-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = SettingsCoverageConfig.LoadDefault() with
            {
                Bounds = new(maxSteps, 8, 4, 3, 0, GoalDirectedContinuationEnabled: goalDirected),
                Coverage = new(true, "configured", 0, 0, false, 0),
                TargetPages = ["Network & internet"],
                DynamicRootEntries = new(false), DynamicNestedEntries = new(false),
                LanguageInspection = null, NavigationScope = null,
                RootRoute = RootRoute, ScrollContainerDescriptor = ScrollContainer, BackDescriptor = "Navigate up",
            };
            var result = SettingsCoverageRunner.Run(root, new SettingsCoverageRunner.Options(
                "emulator-sim", new("emulator-sim", "unused", "/unused", "/unused", "/unused"),
                config, context =>
                {
                    contexts.Add(context);
                    if (contexts.Count == 2 && reviseAfterScopeFailure)
                        return new AgentDecision.Act(new(context.DecisionId,
                            [new("ui.element", "Navigate up", "tap", null)], "explicit fresh-context revision"));
                    if (contexts.Count > 1)
                        return new AgentDecision.Blocked(context.DecisionId, "fixture segment ended");
                    return asPlan
                        ? new AgentDecision.Plan(context.DecisionId, new(
                            steps.Select(step => (PlanItem)new PlanItem.ActItem(step.TargetRole,
                                step.TargetDescriptor, step.EffectClass, step.DesiredState)).ToArray(),
                            "explicit cross-container plan", "local-act-plan"))
                        : new AgentDecision.Act(new(context.DecisionId, steps, "local Act fixture"));
                }, ViewportWidth: Width, ViewportHeight: Height, FeedNext: world.Next,
                CurrentCaptureId: () => world.CurrentCaptureId, EffectDriver: effects,
                UseCoverageDirector: goalDirected));
            if (Environment.GetEnvironmentVariable("UNICLAW_LOCAL_ACT_EVIDENCE") is { Length: > 0 } output)
            {
                var target = Path.Combine(output, name);
                Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(result.RunDir))
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
            return new(result, contexts, world.Route, effects.Deliveries);
        }
        finally { Directory.Delete(root, true); }
    }
}
