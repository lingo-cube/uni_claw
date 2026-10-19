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
    public void MultipleRememberedPages_AnchorNewHierarchyToCurrentPage()
    {
        var evidence = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        void Accept(ObservationProposal proposal)
        {
            var (admission, record) = evidence.Admit(proposal);
            Assert.Equal(AdmissionDecision.Accepted, admission.Decision);
            world.Reconcile(record!, world.JudgeRelevance(record!));
        }
        var time = DateTimeOffset.Parse("2026-10-18T00:00:00Z");
        Accept(new ObservationProposal(new ObservationClaim("ui.screen", "root"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("host.live.settings", time, "scope:root", new[] { "test" })));
        Accept(new ObservationProposal(new ObservationClaim("ui.screen", "root/child"),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("host.live.settings", time.AddSeconds(1), "scope:child", new[] { "test" })));
        const string xml = """
            <hierarchy><node index="0" text="" content-desc="Navigate up" resource-id="android:id/up"
            class="android.widget.ImageButton" package="com.android.settings" clickable="true" enabled="true"
            checkable="false" scrollable="false" bounds="[0,0][120,120]"/></hierarchy>
            """;
        var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
            new UiAutomatorDump.UiHierarchyParseContext("multi-page", time.AddSeconds(2), "device", "run", 35,
                ObservationCycleId: "cycle-2", Capabilities: new HierarchyCapabilities(
                    HierarchyCapability.SemanticText | HierarchyCapability.ContentDescription)));
        foreach (var proposal in TypedHierarchyProposalProjector.Project(parsed.Observation!, ObservationContext.External))
            Accept(proposal);
        Assert.Equal(2, world.Current!.Containers!.Count);
        var active = world.Current.WorldState[WorldModel.CurrentContainerSubject].Value;
        var occurrence = Assert.Single(world.Current.Occurrences!);
        Assert.Equal(active, occurrence.OwningContainerId);
        Assert.Single(world.DeriveSlice(active).Occurrences);
    }

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
