using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

[Collection("SettingsCoverageConfigSerial")]
public sealed class SettingsNavigationScopeTests
{
    private const string Root = "android.settings|rk1:Settings|src=homepage_title|up=0";
    private const string Child = "android.settings|rk1:Network & internet|src=title|up=1";
    private const string Scroll = "com.android.settings:id/main_content_scrollable_container";

    private static SettingsCoverageConfig Config(SettingsNavigationScope? scope) =>
        SettingsCoverageConfig.LoadDefault() with { NavigationScope = scope };

    private static AgentDecisionContext Context(string route, string popup = "absent", bool conflict = false,
        params ElementSummary[] elements)
    {
        var claims = new Dictionary<string, ClaimSummary>(StringComparer.Ordinal)
        {
            [UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.ScreenRouteSubject] =
                new(route, "observed", conflict),
            [UniClaw.Kernel.World.UiRealization.ProductAssociationStrategy.PopupSubject] =
                new(popup, "observed", false),
        };
        return new AgentDecisionContext("d-1", "run-1", "v0", "settings",
            new HashSet<string>(StringComparer.Ordinal) { "tap", "swipe-up" }, claims,
            Array.Empty<AgentObligationView>(), AgentDecisionPhase.StepVerified, Elements: elements);
    }

    private static AgentActionStep Step(string role, string descriptor, string effect = "tap", string? desired = null) =>
        new(role, descriptor, effect, desired);

    [Fact]
    public void Scope_AllowsOnlyCurrentRouteWhitelistedUiElementTap()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
            [Child] = new[] { "SIMs" },
        });
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step("ui.element", "Network & internet"),
            Context(Root, elements: new ElementSummary("ui.element", "Network & internet", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Allow, result.Verdict);
    }

    [Fact]
    public void Scope_RejectsSameNamedTargetOnWrongRoute()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        });
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step("ui.element", "Network & internet"),
            Context(Child, elements: new ElementSummary("ui.element", "Network & internet", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Reject, result.Verdict);
        Assert.Equal("task-navigation-not-authorized", result.Reason);
    }

    [Theory]
    [InlineData("switch", "Wi-Fi", "tap", null)]
    [InlineData("ui.element", "Network & internet", "tap", "checked")]
    [InlineData("ui.element", "Network & internet", "swipe-up", null)]
    public void Scope_RejectsStateMutationSwitchAndNonRootScroll(string role, string target, string effect, string? desired)
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        });
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step(role, target, effect, desired),
            Context(Child, elements: new ElementSummary(role, target, null, true, role == "switch", true,
                ElementEpistemic.Observed, "unchecked")), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Reject, result.Verdict);
    }

    [Fact]
    public void Scope_RejectsMissingOrConflictingRouteAndNonAbsentPopup()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        });
        var missing = SettingsActionGuard.EvaluateCurrentStep(
            Step("ui.element", "Network & internet"),
            Context(Root, popup: "present", elements: new ElementSummary("ui.element", "Network & internet", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        var conflict = SettingsActionGuard.EvaluateCurrentStep(
            Step("ui.element", "Network & internet"),
            Context(Root, conflict: true, elements: new ElementSummary("ui.element", "Network & internet", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Reject, missing.Verdict);
        Assert.Equal(PlanStepGuardVerdict.Reject, conflict.Verdict);
    }

    [Fact]
    public void Scope_AllowsRootScrollOnlyWhenConfigured()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        }, AllowRootScroll: true);
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step("scrollable", Scroll, "swipe-up"),
            Context(Root, elements: new ElementSummary("scrollable", Scroll, null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Allow, result.Verdict);
    }

    [Fact]
    public void ProjectTraversal_UsesCurrentRouteScope_AndBlocksSameNamedWrongRoute()
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.overlay.popup", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        var kernel = new UniKernel(new EvidenceLedger(), world,
            RunTraceFactory.BeginDisabled(new RunCorrelation("scope-projection")).Trace, new RunModel());
        var config = Config(new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Child] = new[] { "Network & internet" },
        }));
        Assert.True(kernel.AdmitContract(new ExecutionContract("v1", "settings", new HashSet<string>
        { "ui.screen", "ui.screen.route", "ui.overlay.popup", "ui.node.*" }, new HashSet<string> { "tap" },
            new HashSet<string>(), new[] { "settings" },
            TaskInstanceId: "task-settings-scope", ProductSessionId: "session-settings-scope",
            ContractRef: "contract-settings-scope")).Accepted);
        var now = DateTimeOffset.UtcNow;
        kernel.Process(new ObservationProposal(new ObservationClaim(ProductAssociationStrategy.ScreenIdentitySubject, "screen"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("test", now, "scope:screen", new[] { "test" })));
        kernel.Process(new ObservationProposal(new ObservationClaim(ProductAssociationStrategy.ScreenRouteSubject, Child),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("test", now, "scope:route", new[] { "test" })));
        var popupResult = kernel.Process(new ObservationProposal(new ObservationClaim(ProductAssociationStrategy.PopupSubject, "absent"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("test", now, "scope:popup", new[] { "test" })));
        Assert.Equal(AdmissionDecision.Accepted, popupResult.Admission.Decision);
        Assert.Equal("absent", kernel.CurrentBelief!.WorldState[ProductAssociationStrategy.PopupSubject].Value);
        var xml = "<hierarchy><node text=\"Network &amp; internet\" class=\"android.widget.Button\" clickable=\"true\" enabled=\"true\" bounds=\"[0,0][100,100]\"/></hierarchy>";
        var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
            new UiAutomatorDump.UiHierarchyParseContext("scope-capture", now, "device", kernel.RunId, 35,
                ObservationCycleId: "scope-cycle", Capabilities: new HierarchyCapabilities(HierarchyCapability.SemanticText)));
        foreach (var proposal in TypedHierarchyProposalProjector.Project(parsed.Observation!, ObservationContext.External))
            kernel.Process(proposal);
        var projection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel, config)!;
        var candidate = Assert.Single(projection.Candidates);
        Assert.Equal(PlanCandidateDisposition.Ready, candidate.Target.Disposition);
        Assert.Equal(PolicyTruth.Satisfied, candidate.Conditions[SettingsTraversalProjection.NavigationCondition]);

        kernel.Process(new ObservationProposal(new ObservationClaim(ProductAssociationStrategy.PopupSubject, "present"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("test", now.AddSeconds(1), "scope:popup-present", new[] { "test" })));
        var unsafeProjection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel, config)!;
        var unsafeCandidate = Assert.Single(unsafeProjection.Candidates);
        Assert.Equal(PlanCandidateDisposition.Blocked, unsafeCandidate.Target.Disposition);
        Assert.Equal(PolicyTruth.Violated, unsafeCandidate.Conditions[SettingsTraversalProjection.NavigationCondition]);

        var wrongRoute = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel,
            Config(new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
            {
                [Root] = new[] { "Network & internet" },
            })))!;
        var blocked = Assert.Single(wrongRoute.Candidates);
        Assert.Equal(PlanCandidateDisposition.Blocked, blocked.Target.Disposition);
    }

    [Fact]
    public void Scope_RejectsBackOnNonSettingsRoute()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        });
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step("ui.element", "Navigate up"),
            Context("com.example|route:Other|up=1", elements: new ElementSummary("ui.element", "Navigate up", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Reject, result.Verdict);
        Assert.Equal("route-unknown-or-conflicted", result.Reason);
    }

    [Fact]
    public void Scope_RejectsBackOnRootRoute()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        });
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step("ui.element", "Navigate up"),
            Context(Root, elements: new ElementSummary("ui.element", "Navigate up", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Reject, result.Verdict);
        Assert.Equal("task-navigation-not-authorized", result.Reason);
    }

    [Fact]
    public void Scope_RejectsWhitelistedSameNamedTapWithNonUiElementRole()
    {
        var scope = new SettingsNavigationScope(new Dictionary<string, IReadOnlyList<string>>
        {
            [Root] = new[] { "Network & internet" },
        });
        var result = SettingsActionGuard.EvaluateCurrentStep(
            Step("button", "Network & internet"),
            Context(Root, elements: new ElementSummary("button", "Network & internet", null, true, false, true,
                ElementEpistemic.Observed)), Config(scope));
        Assert.Equal(PlanStepGuardVerdict.Reject, result.Verdict);
        Assert.Equal("task-navigation-not-authorized", result.Reason);
    }
}
