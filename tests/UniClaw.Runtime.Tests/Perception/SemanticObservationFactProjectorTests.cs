using System.Collections.Immutable;
using UniClaw.Runtime.Capabilities.Perception.Semantic.V2;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

public sealed class SemanticObservationFactProjectorTests
{
    private static ObservationSourceMetadata Vision(bool available = true, long sequence = 7, string frame = "frame-7") =>
        new(ObservationSourceTier.PrimaryVision, available, sequence, frame, 100, 100, "vision-capture", "vision");

    private static ObservationSourceMetadata Structured(bool available = true, long sequence = 7, string frame = "frame-7") =>
        new(ObservationSourceTier.AuxiliaryStructured, available, sequence, frame, 100, 100, "structured-capture", "structured");

    [Fact]
    public void ProjectsVisionAsPrimaryAndStructuredAsAuxiliary()
    {
        var observation = new Observation(
            ImmutableArray.Create(new ObservedElement("Alpha", true, 0, new ElementBounds(.1f, .2f, .4f, .6f), "toggle")), null, 7)
        {
            StructuredElements = ImmutableArray.Create(new StructuredElementEvidence(
                Class: "Widget", ResourceId: "id", Clickable: true, Checkable: false, Checked: null,
                Enabled: true, Focusable: false, Bounds: new ElementBounds(.2f, .3f, .5f, .7f), RawText: "Raw")),
            Sources = ImmutableArray.Create(Vision(), Structured())
        };

        var context = SemanticObservationFactProjector.Project(observation);

        Assert.Contains(context.Facts, f => f.SourceTier == SemanticSourceTier.Primary && f.RawText == "Alpha");
        Assert.Contains(context.Facts, f => f.SourceTier == SemanticSourceTier.Auxiliary && f.RawText == "Raw");
        Assert.Equal(2, context.Sources.Length);
        Assert.All(context.Facts, f => Assert.Equal(7, f.ObservationSequence));
    }

    [Fact]
    public void UsesCanonicalBoundsAndStableOccurrence()
    {
        var element = new ObservedElement("Alpha", null, 0, new ElementBounds(.1f, .2f, .4f, .6f), "text");
        var observation = new Observation(ImmutableArray.Create(element), null, 7) { Sources = ImmutableArray.Create(Vision()) };
        var first = SemanticObservationFactProjector.Project(observation);
        var second = SemanticObservationFactProjector.Project(observation);
        var geometry = Assert.Single(first.Facts.Where(f => f.Kind == SemanticObservationFactKind.Geometry));
        Assert.Equal(.1, geometry.Bounds!.Left, 5);
        Assert.Equal(.2, geometry.Bounds.Top, 5);
        Assert.Equal(.3, geometry.Bounds.Width, 5);
        Assert.Equal(.4, geometry.Bounds.Height, 5);
        Assert.Equal(first.Facts.Select(f => f.OccurrenceId), second.Facts.Select(f => f.OccurrenceId));
    }

    [Fact]
    public void UnavailableAuxiliaryDoesNotBlockPrimary()
    {
        var observation = new Observation(ImmutableArray.Create(new ObservedElement("Alpha", null, 0)), null, 7)
        {
            Sources = ImmutableArray.Create(Vision(), Structured(false))
        };
        var context = SemanticObservationFactProjector.Project(observation);
        Assert.NotEmpty(context.Facts);
        Assert.DoesNotContain(context.Facts, f => f.SourceTier == SemanticSourceTier.Auxiliary);
    }

    [Fact]
    public void MissingOrMismatchedPrimaryFailsClosed()
    {
        var noSource = new Observation(ImmutableArray<ObservedElement>.Empty, null, 7);
        Assert.Throws<InvalidOperationException>(() => SemanticObservationFactProjector.Project(noSource));
        var mismatch = new Observation(ImmutableArray<ObservedElement>.Empty, null, 7) { Sources = ImmutableArray.Create(Vision(sequence: 8)) };
        Assert.Throws<InvalidOperationException>(() => SemanticObservationFactProjector.Project(mismatch));
    }

    [Fact]
    public void ResolvesOnlyCurrentStructuredOccurrence()
    {
        var observation = new Observation(ImmutableArray.Create(new ObservedElement("Alpha", null, 0)), null, 7)
        {
            StructuredElements = ImmutableArray.Create(
                new StructuredElementEvidence(Class: "Widget", ResourceId: null, Clickable: true, Checkable: false,
                    Checked: null, Enabled: true, Focusable: false, Bounds: null, SourceNodeIdentity: "node/0")),
            Sources = ImmutableArray.Create(Vision(), Structured())
        };
        var occurrence = SemanticObservationFactProjector.CreateOccurrenceId("structured", "node/0");
        Assert.True(SemanticObservationFactProjector.TryResolveStructuredIndex(observation, occurrence, out var index));
        Assert.Equal(0, index);
        Assert.False(SemanticObservationFactProjector.TryResolveStructuredIndex(observation, "foreign-occurrence", out _));
    }

    [Fact]
    public void ProjectsSourceCorrelatedParentOccurrence()
    {
        var observation = new Observation(ImmutableArray.Create(new ObservedElement("Alpha", null, 0)), null, 7)
        {
            StructuredElements = ImmutableArray.Create(new StructuredElementEvidence(Class: "Widget", ResourceId: null,
                Clickable: true, Checkable: false, Checked: null, Enabled: true, Focusable: false, Bounds: null,
                SourceNodeIdentity: "child", ParentSourceNodeIdentity: "parent")),
            Sources = ImmutableArray.Create(Vision(), Structured())
        };
        var expected = SemanticObservationFactProjector.CreateOccurrenceId("structured", "parent");
        Assert.All(SemanticObservationFactProjector.Project(observation).Facts.Where(f => f.SourceTier == SemanticSourceTier.Auxiliary),
            fact => Assert.Equal(expected, fact.ParentOccurrenceId));
    }
}
