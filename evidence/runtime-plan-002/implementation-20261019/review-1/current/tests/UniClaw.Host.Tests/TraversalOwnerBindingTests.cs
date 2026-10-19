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
    private static UniKernel Kernel(string label = "中文")
    {
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        var scope = RunTraceFactory.BeginDisabled(new RunCorrelation("traversal-binding"));
        var kernel = new UniKernel(new EvidenceLedger(), world, scope.Trace, new RunModel());
        Assert.True(kernel.AdmitContract(new ExecutionContract("v1", "inspect labels",
            new HashSet<string> { "ui.screen", "ui.node.*" }, new HashSet<string> { "tap" },
            new HashSet<string>(), new[] { "read-only" },
            TaskInstanceId: "task-traversal-binding", ProductSessionId: "session-traversal-binding",
            ContractRef: "contract-traversal-binding")).Accepted);
        var time = DateTimeOffset.Parse("2026-10-19T00:00:00Z");
        kernel.Process(new ObservationProposal(new ObservationClaim("ui.screen", "root"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("host.live.settings", time, "scope:root", new[] { "fixture" })));
        var xml = $"""
            <hierarchy><node index="0" text="{label}" resource-id="id/switch"
            class="android.widget.Switch" package="com.android.settings" clickable="true" enabled="true"
            checkable="true" checked="false" scrollable="false" bounds="[0,0][120,120]"/></hierarchy>
            """;
        var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
            new UiAutomatorDump.UiHierarchyParseContext("binding-capture", time.AddSeconds(1), "device", kernel.RunId, 35,
                ObservationCycleId: "cycle-binding", Capabilities: new HierarchyCapabilities(HierarchyCapability.SemanticText)));
        foreach (var proposal in TypedHierarchyProposalProjector.Project(parsed.Observation!, ObservationContext.External))
            kernel.Process(proposal);
        return kernel;
    }

    [Fact]
    public void RegisteredTypedInspector_ReceivesCurrentTarget_AndReturnsViolationWithoutEffect()
    {
        var kernel = Kernel();
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
        var binding = LanguageInspectionBindingFactory.Create(registry, kernel.RunId,
            new LanguageInspectionTaskRequest(true, "en", FixedCapabilityId: LanguageInspectionProtocol.CapabilityId));
        var adapter = new TraversalLanguageInspection(kernel, () => binding);
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
        var kernel = Kernel("Wi-Fi");
        var registry = RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
        var binding = LanguageInspectionBindingFactory.Create(registry, kernel.RunId,
            new LanguageInspectionTaskRequest(true, "en", FixedCapabilityId: LanguageInspectionProtocol.CapabilityId));
        var adapter = new TraversalLanguageInspection(kernel, () => binding);
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
        Assert.Empty(kernel.EffectReceipts);
    }
}
