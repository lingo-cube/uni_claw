using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldCaptureIsolationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateCaptureField_CannotReplaceCurrentViewport(bool samePage)
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy());
        var time = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        void Accept(ObservationProposal proposal)
        {
            var (_, record) = ledger.Admit(proposal);
            world.Reconcile(record!, world.JudgeRelevance(record!));
        }
        void Page(string page, int step) => Accept(new(new("ui.screen", page), IngressKind.Observation,
            ObservationContext.External, new("capture-test", time.AddSeconds(step), "scope:" + step, new[] { "fixture" })));
        IReadOnlyList<ObservationProposal> Capture(string id, string label, int step)
        {
            var xml = $"<hierarchy><node text=\"{label}\" class=\"android.widget.Button\" package=\"fixture\" clickable=\"true\" enabled=\"true\" checkable=\"false\" scrollable=\"false\" selected=\"false\" bounds=\"[100,100][300,200]\"/></hierarchy>";
            var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
                new(id, time.AddSeconds(step), "device", "session", 35,
                    Space: CoordinateSpace.DeviceViewport(1000, 1000)));
            return TypedHierarchyProposalProjector.Project(parsed.Observation!, ObservationContext.External);
        }
        Page("A", 0);
        var first = Capture("A", "Alpha", 1);
        var late = first.Single(p => p.Provenance.Hierarchy!.Field == "selected");
        foreach (var p in first.Where(p => p != late)) Accept(p);
        Page(samePage ? "A" : "B", 2);
        foreach (var p in Capture("B", "Beta", 3)) Accept(p);
        var source = world.Current!;
        var sourceOccurrence = Assert.Single(source.Occurrences!);
        Accept(late);
        var current = Assert.Single(world.Current!.Occurrences!);
        Assert.Equal("Beta", current.SemanticDescriptor);
        Assert.Equal(sourceOccurrence.OwningContainerId, current.OwningContainerId);
        Assert.Equal(sourceOccurrence.Locator, current.Locator);
        Assert.Equal(sourceOccurrence.EvidenceBasis, current.EvidenceBasis);
    }
}
