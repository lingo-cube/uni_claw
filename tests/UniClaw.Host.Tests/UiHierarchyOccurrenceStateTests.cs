using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class UiHierarchyOccurrenceStateTests
{
    [Fact]
    public void ExactUncheckedSwitch_RemainsAvailableToControlPolicy()
    {
        const string xml = """
            <hierarchy>
              <node index="0" text="Wi-Fi" resource-id="id/wifi-switch"
                    class="android.widget.Switch" package="com.android.settings"
                    checkable="true" checked="false" clickable="true"
                    enabled="true" focusable="true" focused="false"
                    scrollable="false" selected="false" password="false"
                    bounds="[900,300][1040,400]"/>
            </hierarchy>
            """;
        var proof = new CheckedExactProof(
            "contract:fixture/two-state", "capability:fixture/exact", "fixture:two-state");
        var parsed = UiAutomatorDump.ParseHierarchyObservation(
            xml,
            new UiAutomatorDump.UiHierarchyParseContext(
                "occ-state-capture", DateTimeOffset.UtcNow, "emulator-test", "run-test", 35,
                ObservationCycleId: "cycle-1",
                Capabilities: new HierarchyCapabilities(
                    HierarchyCapability.SemanticText | HierarchyCapability.CheckedBooleanExact),
                ExactProof: proof));

        var ledger = new EvidenceLedger();
        var world = new WorldModel(
            new HashSet<string> { "ui.node.*" },
            new ProductAssociationStrategy(),
            new UiHierarchyOccurrenceStrategy());
        foreach (var proposal in TypedHierarchyProposalProjector.Project(
                     parsed.Observation!, ObservationContext.External))
        {
            var (admission, record) = ledger.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
            var relevance = world.JudgeRelevance(record!);
            Assert.True(relevance.IsRelevant, relevance.Reason);
            world.Reconcile(record!, relevance);
        }

        var occurrence = Assert.Single(world.Current!.Occurrences!);
        Assert.Equal("switch", occurrence.Role);
        Assert.Equal("unchecked", occurrence.State);
    }
}
