using UniClaw.Host.Capability;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

[Collection("SettingsCoverageConfigSerial")]
public sealed class TraversalOwnerBindingTests
{
    private static (UniKernel Kernel, EvidenceLedger Evidence) Kernel(string label = "中文", string? hierarchy = null, string? route = null)
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        var scope = RunTraceFactory.BeginDisabled(new RunCorrelation("traversal-binding"));
        var evidence = new EvidenceLedger();
        var kernel = new UniKernel(evidence, world, scope.Trace, new RunModel());
        Assert.True(kernel.AdmitContract(new ExecutionContract("v1", "inspect labels",
            new HashSet<string> { "ui.screen", "ui.screen.route", "ui.node.*" }, new HashSet<string> { "tap" },
            new HashSet<string>(), new[] { "read-only" },
            TaskInstanceId: "task-traversal-binding", ProductSessionId: "session-traversal-binding",
            ContractRef: "contract-traversal-binding")).Accepted);
        var time = DateTimeOffset.Parse("2026-10-19T00:00:00Z");
        kernel.Process(new ObservationProposal(new ObservationClaim("ui.screen", "root"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("host.live.settings", time, "scope:root", new[] { "fixture" })));
        if (route is not null)
            kernel.Process(new ObservationProposal(new ObservationClaim("ui.screen.route", route),
                IngressKind.Observation, ObservationContext.External,
                new Provenance("host.live.settings", time, "scope:route", new[] { "fixture" })));
        var xml = hierarchy ?? $"""
            <hierarchy><node index="0" text="{label}" resource-id="id/switch"
            class="android.widget.Switch" package="com.android.settings" clickable="true" enabled="true"
            checkable="true" checked="false" scrollable="false" bounds="[0,0][120,120]"/></hierarchy>
            """;
        var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
            new UiAutomatorDump.UiHierarchyParseContext("binding-capture", time.AddSeconds(1), "device", kernel.RunId, 35,
                ObservationCycleId: "cycle-binding", Capabilities: new HierarchyCapabilities(HierarchyCapability.SemanticText)));
        foreach (var proposal in TypedHierarchyProposalProjector.Project(parsed.Observation!, ObservationContext.External))
            kernel.Process(proposal);
        return (kernel, evidence);
    }

    [Fact]
    public async Task TypedAsyncAdapter_PreservesFinding_AndHonorsPreInvocationCancellation()
    {
        var (kernel, evidence) = Kernel();
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
        var binding = LanguageInspectionBindingFactory.Create(registry, kernel.RunId,
            new LanguageInspectionTaskRequest(true, "en", FixedCapabilityId: LanguageInspectionProtocol.CapabilityId));
        var adapter = new TraversalLanguageInspection(kernel, evidence, () => binding);
        var handler = new PlanLanguageHandler("label", LanguageInspectionProtocol.CapabilityId, "1.0", "en");
        var observation = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel,
            SettingsCoverageConfig.LoadDefault())!;
        var input = new PlanHandlerInput(kernel.RunId, "async-label", "Before",
            observation.Graph.SourceRevisionId, observation.Graph.CurrentContainerId!,
            Assert.Single(observation.Candidates).Target);

        var result = await adapter.InspectAsync(handler, input, CancellationToken.None);
        Assert.Equal(FindingDisposition.Violation, result!.Disposition);
        Assert.Equal(CapabilityStatus.Completed, result.Status);
        Assert.Equal("async-label", result.Correlation.OperationId);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await adapter.InspectAsync(handler, input, cancelled.Token));
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void RegisteredTypedInspector_ReceivesCurrentTarget_AndReturnsViolationWithoutEffect()
    {
        var (kernel, evidence) = Kernel();
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
        var binding = LanguageInspectionBindingFactory.Create(registry, kernel.RunId,
            new LanguageInspectionTaskRequest(true, "en", FixedCapabilityId: LanguageInspectionProtocol.CapabilityId));
        var adapter = new TraversalLanguageInspection(kernel, evidence, () => binding);
        var handler = new PlanLanguageHandler("label", LanguageInspectionProtocol.CapabilityId, "1.0", "en");
        Assert.True(adapter.CanBind(handler));
        var projection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel,
            SettingsCoverageConfig.LoadDefault())!;
        var target = Assert.Single(projection.Candidates).Target;
        var input = new PlanHandlerInput(kernel.RunId, "read-label", "before",
            projection.Graph.SourceRevisionId, projection.Graph.CurrentContainerId!, target);
        var result = adapter.Inspect(handler, input)!;
        Assert.Equal(CapabilityStatus.Completed, result.Status);
        Assert.Equal(FindingDisposition.Violation, result.Disposition);
        Assert.Equal("read-label", result.Correlation.OperationId);
        Assert.Empty(kernel.EffectReceipts);
        Assert.False(projection.DiscoveryComplete);
        Assert.Equal(PolicyTruth.Satisfied, projection.Candidates[0].Conditions["readOnly"]);
    }

    [Fact]
    public void WrongBindingOrStaleTarget_CannotInspectAnAlternateTarget()
    {
        var (kernel, evidence) = Kernel("Wi-Fi");
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
        var binding = LanguageInspectionBindingFactory.Create(registry, kernel.RunId,
            new LanguageInspectionTaskRequest(true, "en", FixedCapabilityId: LanguageInspectionProtocol.CapabilityId));
        var adapter = new TraversalLanguageInspection(kernel, evidence, () => binding);
        var handler = new PlanLanguageHandler("label", LanguageInspectionProtocol.CapabilityId, "1.0", "en");
        Assert.False(adapter.CanBind(handler with { ProtocolVersion = "2.0" }));
        Assert.False(adapter.CanBind(handler with { ExpectedLanguage = "zh" }));
        var projection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel,
            SettingsCoverageConfig.LoadDefault())!;
        var target = Assert.Single(projection.Candidates).Target;
        var input = new PlanHandlerInput(kernel.RunId, "read-label", "before", "stale",
            projection.Graph.CurrentContainerId!, target);
        Assert.Equal(CapabilityStatus.Unknown, adapter.Inspect(handler, input)!.Status);
        var disappeared = input with { SourceRevisionId = projection.Graph.SourceRevisionId,
            Target = target with { TargetDescriptor = "no-longer-present" } };
        Assert.Equal(FindingDisposition.Unknown, adapter.Inspect(handler, disappeared)!.Disposition);
        Assert.Equal(FindingDisposition.Unknown, adapter.Inspect(handler,
            input with { SourceRevisionId = projection.Graph.SourceRevisionId,
                TargetContainerId = "other-bound-page" })!.Disposition);
        Assert.Empty(kernel.EffectReceipts);
    }
    [Fact]
    public void ResourceIdFallback_IsNotCertifiedAsVisibleLanguage()
    {
        var (kernel, evidence) = Kernel("");
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
        var binding = LanguageInspectionBindingFactory.Create(registry, kernel.RunId,
            new LanguageInspectionTaskRequest(true, "en", FixedCapabilityId: LanguageInspectionProtocol.CapabilityId));
        var adapter = new TraversalLanguageInspection(kernel, evidence, () => binding);
        var handler = new PlanLanguageHandler("label", LanguageInspectionProtocol.CapabilityId, "1.0", "en");
        var observation = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel,
            SettingsCoverageConfig.LoadDefault())!;
        var target = Assert.Single(observation.Candidates).Target;
        Assert.Equal("id/switch", target.TargetDescriptor);
        var result = adapter.Inspect(handler, new PlanHandlerInput(kernel.RunId, "read-resource-label", "Before",
            observation.Graph.SourceRevisionId, observation.Graph.CurrentContainerId!, target));
        Assert.Equal(FindingDisposition.Unknown, result!.Disposition);
        Assert.Empty(kernel.EffectReceipts);
    }

    private const string ScopeHierarchy = """
        <hierarchy>
        <node index="0" text="Heading" class="android.widget.TextView" clickable="false" enabled="true" bounds="[0,0][120,40]"/>
        <node index="1" text="Network &amp; internet" class="android.widget.Button" clickable="true" enabled="true" bounds="[0,40][120,80]"/>
        <node index="2" text="Connection details" class="android.widget.Button" clickable="true" enabled="true" bounds="[0,80][120,120]"/>
        <node index="3" text="Unknown action" class="android.widget.Button" clickable="true" enabled="true" bounds="[0,120][120,160]"/>
        <node index="4" text="Airplane mode" class="android.widget.Switch" clickable="true" enabled="true" checkable="true" checked="false" bounds="[0,160][120,200]"/>
        </hierarchy>
        """;

    [Fact]
    public void ConfiguredRootScope_BlocksOtherActionsWithoutTreatingThemAsNavigation()
    {
        var (kernel, _) = Kernel(hierarchy: ScopeHierarchy, route: "root");
        var original = SettingsCoverageConfig.LoadDefault();
        var config = original with { RootRoute = "root", TargetPages = new[] { "Network & internet" },
            DynamicRootEntries = new DynamicRootEntriesConfig(), DynamicNestedEntries = new DynamicNestedEntriesConfig() };
        var projection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel, config)!;
        Assert.DoesNotContain(projection.Candidates, c => c.Target.TargetDescriptor == "Heading");
        var navigation = Assert.Single(projection.Candidates.Where(c => c.Target.TargetDescriptor == "Network & internet"));
        Assert.Equal(PolicyTruth.Satisfied, navigation.Conditions["navigation"]);
        var excluded = Assert.Single(projection.Candidates.Where(c => c.Target.TargetDescriptor == "Unknown action"));
        Assert.Equal(PlanCandidateDisposition.Blocked, excluded.Target.Disposition);
        Assert.Equal("outside-task-navigation-scope", excluded.Target.Reason);
        var readOnly = Assert.Single(projection.Candidates.Where(c => c.Target.TargetRole == "switch"));
        Assert.Equal(PlanCandidateDisposition.Ready, readOnly.Target.Disposition);
        Assert.Equal(PolicyTruth.Satisfied, readOnly.Conditions["readOnly"]);
        Assert.False(projection.DiscoveryComplete);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Fact]
    public void ChildPage_DoesNotInheritRootTargetClassification_AndDisabledNestedScopeIsBlocked()
    {
        var (kernel, _) = Kernel(hierarchy: ScopeHierarchy, route: "child");
        var config = SettingsCoverageConfig.LoadDefault() with { RootRoute = "root",
            TargetPages = new[] { "Network & internet" }, DeepTraversal = new DeepTraversalConfig(),
            DynamicRootEntries = new DynamicRootEntriesConfig(), DynamicNestedEntries = new DynamicNestedEntriesConfig() };
        var projection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel, config)!;
        Assert.All(projection.Candidates.Where(c => c.Target.TargetRole != "switch"), candidate =>
        {
            Assert.Equal(PlanCandidateDisposition.Blocked, candidate.Target.Disposition);
            Assert.Equal(PolicyTruth.Violated, candidate.Conditions["navigation"]);
        });
        Assert.Equal(PolicyTruth.Satisfied, Assert.Single(projection.Candidates.Where(c =>
            c.Target.TargetRole == "switch")).Conditions["readOnly"]);
        Assert.Empty(kernel.EffectReceipts);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("child")]
    [InlineData(null)]
    public void DynamicOrUnobservedScope_DoesNotCertifyUnclassifiedAction(string? route)
    {
        var (kernel, _) = Kernel(hierarchy: ScopeHierarchy, route: route);
        var config = SettingsCoverageConfig.LoadDefault() with { RootRoute = "root",
            TargetPages = new[] { "Network & internet" },
            DynamicRootEntries = new DynamicRootEntriesConfig(true, "all-visible"),
            DynamicNestedEntries = new DynamicNestedEntriesConfig(true, 1, 1) };
        var projection = SettingsTraversalProjection.Project(SettingsTraversalProjection.Source, kernel, config)!;
        var unknown = Assert.Single(projection.Candidates.Where(c => c.Target.TargetDescriptor == "Unknown action"));
        Assert.Equal(PlanCandidateDisposition.Ready, unknown.Target.Disposition);
        Assert.Equal(PolicyTruth.Unknown, unknown.Conditions["navigation"]);
        if (route != "root")
            Assert.Equal(PolicyTruth.Unknown, Assert.Single(projection.Candidates.Where(c =>
                c.Target.TargetDescriptor == "Network & internet")).Conditions["navigation"]);
        Assert.Empty(kernel.EffectReceipts);
    }

}
