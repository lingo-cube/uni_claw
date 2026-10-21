using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldViewportTransitionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VerifiedScroll_RecordsOperationAndMeasuredOffset_PreservesHistory()
    {
        var (world, observation) = Scenario();
        var before = world.Current!;
        Assert.True(world.RecordObservedViewportTransition(observation));
        var edge = Assert.Single(world.Current!.ViewportTransitions);
        Assert.Equal(.2, edge.OffsetY!.Value, 9);
        Assert.Equal(0, edge.OffsetX!.Value, 9);
        Assert.Equal("Aligned", edge.AlignmentStatus);
        Assert.Equal(observation.OperationReceiptId, edge.OperationReceiptId);
        Assert.Equal(observation.SourceRevisionId, edge.SourceRevisionId);
        Assert.Equal(observation.TargetRevisionId, edge.TargetRevisionId);
        Assert.Equal(2, edge.Correspondences.Count(c => !c.Stationary));
        Assert.Empty(before.ViewportTransitions);
        var revision = world.Current.RevisionId;
        Assert.True(world.RecordObservedViewportTransition(observation));
        Assert.Equal(revision, world.Current.RevisionId);
        Assert.Single(world.DeriveGraphProjection(revision, "run:viewport").ViewportTransitions);
        Assert.Empty(world.DeriveGraphProjection(revision, "other").ViewportTransitions);
    }

    [Theory]
    [InlineData("no-overlap", "InsufficientOverlap")]
    [InlineData("duplicate", "AmbiguousAnchors")]
    [InlineData("reflow", "NotPureTranslation")]
    [InlineData("clipped", "NotPureTranslation")]
    [InlineData("dimensions", "CoordinateSpaceMismatch")]
    [InlineData("rotation", "CoordinateSpaceMismatch")]
    [InlineData("frame", "FrameMismatch")]
    [InlineData("down", "DirectionMismatch")]
    [InlineData("nested", "OverlappingScrollRegions")]
    public void VerifiedScroll_WithInsufficientSpatialEvidence_PreservesRelationButNoOffset(string variant, string expected)
    {
        var (world, observation) = Scenario(variant);
        Assert.True(world.RecordObservedViewportTransition(observation));
        var edge = Assert.Single(world.Current!.ViewportTransitions);
        Assert.Equal(expected, edge.AlignmentStatus);
        Assert.Null(edge.OffsetX); Assert.Null(edge.OffsetY);
        Assert.Empty(edge.Correspondences);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("container")]
    [InlineData("target")]
    [InlineData("receipt")]
    [InlineData("verification")]
    [InlineData("time")]
    public void InvalidOperationCorrelation_DoesNotCreateRelation(string mutation)
    {
        var (world, input) = Scenario();
        input = mutation switch
        {
            "revision" => input with { SourceRevisionId = "missing" },
            "container" => input with { ContainerId = "other" },
            "target" => input with { SourceTargetOccurrenceId = "other" },
            "receipt" => input with { OperationReceiptId = "" },
            "verification" => input with { VerificationSourceRef = "" },
            _ => input with { DispatchedAt = T0.AddSeconds(4) },
        };
        var current = world.Current;
        Assert.False(world.RecordObservedViewportTransition(input));
        Assert.Same(current, world.Current);
        Assert.Empty(world.Current!.ViewportTransitions);
    }

    [Fact]
    public void DiagonalObservedTranslation_PreservesBothCoordinateOffsets()
    {
        var (world, observation) = Scenario("diagonal");
        Assert.True(world.RecordObservedViewportTransition(observation));
        var transition = Assert.Single(world.Current!.ViewportTransitions);
        Assert.Equal("Aligned", transition.AlignmentStatus);
        Assert.Equal(.1, transition.OffsetX!.Value, 9);
        Assert.Equal(.2, transition.OffsetY!.Value, 9);
    }

    [Fact]
    public void NonFiniteSpatialCoordinates_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new SpatialLocator(double.NaN, 0, .5, .5, "device-viewport"));
        Assert.Throws<ArgumentException>(() => new SpatialLocator(0, double.NaN, .5, .5, "device-viewport"));
    }

    [Fact]
    public void TwoScrollRegions_UseTheDispatchedRegionRatherThanWholeViewport()
    {
        var (leftWorld, left) = Scenario("regions");
        Assert.True(leftWorld.RecordObservedViewportTransition(left));
        Assert.Equal(.2, Assert.Single(leftWorld.Current!.ViewportTransitions).OffsetY!.Value, 9);
        var (rightWorld, right) = Scenario("regions");
        var source = rightWorld.RevisionHistory.Single(r => r.RevisionId == right.SourceRevisionId);
        right = right with
        {
            SourceTargetOccurrenceId = source.Occurrences!.Single(o => o.SemanticDescriptor == "other-list").OccurrenceId,
            EffectClass = "swipe-down",
        };
        Assert.True(rightWorld.RecordObservedViewportTransition(right));
        Assert.Equal(-.1, Assert.Single(rightWorld.Current!.ViewportTransitions).OffsetY!.Value, 9);
    }

    private static (WorldModel World, ObservedViewportTransition Observation) Scenario(string variant = "after")
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen", "test.viewport" },
            new ProductAssociationStrategy(), new ObservationStrategy());
        void Observe(string subject, string value, int second)
        {
            var (_, record) = ledger.Admit(new ObservationProposal(new(subject, value),
                IngressKind.Observation, ObservationContext.External,
                new Provenance("viewport-fixture", T0.AddSeconds(second), "capture:" + second, new[] { "worked-example" })));
            world.Reconcile(record!, world.JudgeRelevance(record!));
        }
        Observe("ui.screen", "page", 0);
        Observe("test.viewport", variant == "regions" ? "regions-before" : "before", 1);
        var source = world.Current!;
        var target = source.Occurrences!.Single(o => o.Role == "scrollable" && o.SemanticDescriptor == "list");
        Observe("test.viewport", variant, 3);
        var current = world.Current!;
        return (world, new(source.RevisionId, current.RevisionId, target.OwningContainerId!,
            "receipt:1", "binding:1", target.OccurrenceId, variant == "down" ? "swipe-down" : "swipe-up", T0.AddSeconds(2),
            "run:viewport", "verification:1"));
    }

    private sealed class ObservationStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (record.Claim.Subject != "test.viewport") return [];
            var owner = previous!.WorldState[WorldModel.CurrentContainerSubject].Value;
            var variant = record.Claim.Value;
            var offset = variant is "before" or "regions-before" ? 0 : .2;
            var regions = variant.StartsWith("regions", StringComparison.Ordinal);
            var space = CoordinateSpace.DeviceViewport(1000, variant == "dimensions" ? 2000 : 1000,
                variant == "rotation" ? ScreenRotation.Rot90 : ScreenRotation.None);
            var frame = variant == "frame" ? "other-frame" : "device-viewport";
            var result = new List<ProposedOccurrence>
            {
                new(owner, "scrollable", "list", Locator: new(0, .1, regions ? .5 : 1, .95, frame), Space: space),
                new(owner, "ui.element", "Sticky", Locator: new(.1, .12, regions ? .4 : .8, .15, frame), Space: space),
                new(owner, "ui.element", variant == "no-overlap" ? "NewA" : "Alpha", Locator: new(.1, .4-offset, regions ? .4 : .8, .45-offset + (variant == "clipped" ? .02 : 0), frame), Space: space),
                new(owner, "ui.element", variant == "no-overlap" ? "NewB" : "Beta", Locator: new(.1, .6-offset + (variant == "reflow" ? .03 : 0), regions ? .4 : .8, .65-offset + (variant == "reflow" ? .03 : 0), frame), Space: space),
            };
            if (regions)
            {
                var down = variant == "regions-before" ? 0 : .1;
                result.Add(new(owner, "scrollable", "other-list", Locator: new(.5, .1, 1, .95, frame), Space: space));
                result.Add(new(owner, "ui.element", "RightA", Locator: new(.6, .3+down, .9, .35+down, frame), Space: space));
                result.Add(new(owner, "ui.element", "RightB", Locator: new(.6, .5+down, .9, .55+down, frame), Space: space));
            }
            if (variant == "duplicate") result.Add(result[2]);
            if (variant == "diagonal")
                for (var i = 2; i <= 3; i++)
                {
                    var bounds = result[i].Locator!;
                    result[i] = result[i] with { Locator = new(bounds.X1 - .1, bounds.Y1, bounds.X2 - .1, bounds.Y2, frame) };
                }
            if (variant == "nested")
                result.Add(new(owner, "scrollable", "nested-list", Locator: new(.05, .15, .95, .9, frame), Space: space));
            return result;
        }
    }
}
