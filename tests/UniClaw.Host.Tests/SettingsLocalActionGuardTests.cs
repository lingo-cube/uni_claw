using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class SettingsLocalActionGuardTests
{
    [Theory]
    [InlineData("present", "CANCEL", PlanStepGuardVerdict.Allow)]
    [InlineData("present", "Navigate up", PlanStepGuardVerdict.Allow)]
    [InlineData("present", "Network", PlanStepGuardVerdict.Reject)]
    [InlineData("present", "ALLOW", PlanStepGuardVerdict.Reject)]
    [InlineData("unknown", "CANCEL", PlanStepGuardVerdict.Unknown)]
    [InlineData("conflict", "CANCEL", PlanStepGuardVerdict.Unknown)]
    public void CurrentStepGuard_PopupRecoveryKeepsItsDeclaredSafeChoices(
        string state, string target, PlanStepGuardVerdict expected)
    {
        var config = SettingsCoverageConfig.LoadDefault() with { NavigationScope = null };
        var context = RecoveryContext(target, popup: state, route: "android.settings");
        var result = SettingsActionGuard.EvaluateCurrentStep(new("ui.element", target, "tap", null), context, config);
        Assert.Equal(expected, result.Verdict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentStepGuard_UnknownRouteAllowsVisibleBackOrScroll(bool scroll)
    {
        var config = SettingsCoverageConfig.LoadDefault() with { NavigationScope = null };
        var target = scroll ? config.ScrollContainerDescriptor : config.BackDescriptor!;
        var role = scroll ? "scrollable" : "ui.element";
        var context = RecoveryContext(target, role, route: "android.settings");
        Assert.Equal(PlanStepGuardVerdict.Allow,
            SettingsActionGuard.EvaluateCurrentStep(new(role, target, scroll ? "swipe-up" : "tap", null), context, config).Verdict);
        if (!scroll)
        {
            Assert.Equal(PlanStepGuardVerdict.Unknown,
                SettingsActionGuard.EvaluateCurrentStep(new(role, target, "tap", null), context,
                    config with { BackDescriptor = null }).Verdict);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultiActAdmissionRequiresTheMandatoryCurrentStepGuard(bool installed)
    {
        var config = SettingsCoverageConfig.LoadDefault();
        var context = RecoveryContext("Network");
        var act = new AgentDecision.Act(new(context.DecisionId,
            [new("ui.element", "Network", "tap", null), new("ui.element", "Navigate up", "tap", null)], "admission only"));
        var result = SettingsActionGuard.GuardDecision(act, context, config.ActionPolicy!, out var guard, installed);
        if (installed)
        {
            Assert.Same(act, result);
            Assert.Null(guard);
        }
        else
        {
            Assert.Null(result);
            Assert.Equal(SettingsActionGuardVerdict.Reject, guard!.Verdict);
        }
    }

    private static AgentDecisionContext RecoveryContext(
        string target, string role = "ui.element", string? popup = null, string? route = null)
    {
        var claims = new Dictionary<string, ClaimSummary>();
        if (popup is not null) claims[ProductAssociationStrategy.PopupSubject] = new(popup, "established", false);
        if (route is not null) claims[ProductAssociationStrategy.ScreenRouteSubject] = new(route, "established", false);
        return new("decision-recovery", "run-recovery", "v1", "traverse settings", new HashSet<string> { "tap", "swipe-up" },
            claims, [], AgentDecisionPhase.InitialPlanning,
            Elements: [new(role, target, null, true, false, true, ElementEpistemic.Observed)]);
    }

    [Fact]
    public void UnknownRouteCannotUseAnOrdinaryNavigationAsRecovery()
    {
        var config = SettingsCoverageConfig.LoadDefault() with { NavigationScope = null };
        var context = new AgentDecisionContext("decision-unknown", "run-unknown", "v1", "traverse settings",
            new HashSet<string> { "tap" },
            new Dictionary<string, ClaimSummary> { [ProductAssociationStrategy.ScreenRouteSubject] = new("android.settings", "established", false) },
            [], AgentDecisionPhase.InitialPlanning,
            Elements: [new("ui.element", "Network", null, true, false, true, ElementEpistemic.Observed)]);

        var result = SettingsActionGuard.EvaluateCurrentStep(new("ui.element", "Network", "tap", null), context, config);
        Assert.Equal(PlanStepGuardVerdict.Unknown, result.Verdict);
        Assert.Contains("unknown-route", result.Reason);
    }

    [Fact]
    public void PopupConfirmationIsRejectedByTheMandatoryCurrentStepGuard()
    {
        var config = SettingsCoverageConfig.LoadDefault() with { NavigationScope = null };
        var context = new AgentDecisionContext("decision-popup", "run-popup", "v1", "traverse settings",
            new HashSet<string> { "tap" },
            new Dictionary<string, ClaimSummary> { [ProductAssociationStrategy.PopupSubject] = new("present", "established", false) },
            [], AgentDecisionPhase.InitialPlanning,
            Elements: [new("ui.element", "ALLOW", null, true, false, true, ElementEpistemic.Observed)]);

        var result = SettingsActionGuard.EvaluateCurrentStep(new("ui.element", "ALLOW", "tap", null), context, config);
        Assert.Equal(PlanStepGuardVerdict.Reject, result.Verdict);
        Assert.Contains("popup-confirm", result.Reason);
    }
}
