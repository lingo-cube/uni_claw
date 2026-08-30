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

    // ── SEMANTIC_PROJECTION_BOUNDS_REPAIR_GATE ────────────────────────────────
    // Root cause: ElementBounds stores float32. Normalize computed X2−X1 in float32
    // (1.0f − 0.002778f rounds UP to 0.9972220063209534f), then widened to double;
    // SemanticNormalizedBounds' "left+width ≤ 1" check then saw 1.0000000063 > 1
    // and falsely rejected a VALID full-width element (IsValid == true, X2 == 1.0f).
    // Fix: widen to double BEFORE subtracting so left+width reconstructs X2 exactly.

    [Fact]
    public void FullWidthVisionElementAtFrameEdgeProjectsWithoutFloatReconstructionException()
    {
        // Real Display toolbar title: fused {x1:0.002778, y1:0.0625, x2:1.0, y2:0.120625}.
        var element = new ObservedElement("Display", null, 0, new ElementBounds(0.002778f, 0.0625f, 1f, 0.120625f), "menu_item");
        var observation = new Observation(ImmutableArray.Create(element), null, 24)
        {
            Sources = ImmutableArray.Create(Vision(sequence: 24, frame: "frame-24"))
        };

        var context = SemanticObservationFactProjector.Project(observation); // must NOT throw

        var geometry = Assert.Single(context.Facts.Where(f => f.Kind == SemanticObservationFactKind.Geometry));
        // Widen-first subtraction: width = (double)1.0f − (double)0.002778f = 0.99722200003452599,
        // and left + width reconstructs X2 == 1.0 exactly (the float32-rounded 0.99722200632
        // is exactly what the repair removes).
        Assert.Equal(0.0027779999654740095, geometry.Bounds!.Left, 12);
        Assert.Equal(0.9972220000345260, geometry.Bounds.Width, 12);
        Assert.Equal(1.0, geometry.Bounds.Left + geometry.Bounds.Width, 12);
    }

    [Fact]
    public void FullWidthStructuredElementAtFrameEdgeProjectsWithoutFloatReconstructionException()
    {
        var observation = new Observation(ImmutableArray.Create(new ObservedElement("Alpha", null, 0)), null, 24)
        {
            StructuredElements = ImmutableArray.Create(new StructuredElementEvidence(
                Class: "Toolbar", ResourceId: "id", Clickable: false, Checkable: false, Checked: null,
                Enabled: true, Focusable: false, Bounds: new ElementBounds(0.002778f, 0.0625f, 1f, 0.120625f),
                RawText: "Display")),
            Sources = ImmutableArray.Create(Vision(sequence: 24, frame: "frame-24"), Structured(sequence: 24, frame: "frame-24"))
        };

        var context = SemanticObservationFactProjector.Project(observation); // must NOT throw

        var geometry = Assert.Single(context.Facts.Where(f =>
            f.SourceTier == SemanticSourceTier.Auxiliary && f.Kind == SemanticObservationFactKind.Geometry));
        Assert.Equal(0.9972220000345260, geometry.Bounds!.Width, 12);
        Assert.Equal(1.0, geometry.Bounds.Left + geometry.Bounds.Width, 12);
    }

    [Fact]
    public void FullWidthElementWithZeroLeftEdgeStillProjects()
    {
        // x1_px = 0 case from the diagnostic baseline: exactly 1.0, no rounding.
        var element = new ObservedElement("Display", null, 0, new ElementBounds(0f, 0.0625f, 1f, 0.120625f), "menu_item");
        var observation = new Observation(ImmutableArray.Create(element), null, 24)
        {
            Sources = ImmutableArray.Create(Vision(sequence: 24, frame: "frame-24"))
        };
        var context = SemanticObservationFactProjector.Project(observation);
        var geometry = Assert.Single(context.Facts.Where(f => f.Kind == SemanticObservationFactKind.Geometry));
        Assert.Equal(1.0, geometry.Bounds.Width, 12);
    }

    [Fact]
    public void OutOfFrameRightEdgeStillFailsClosed()
    {
        // IsValid precertifies X2 ≤ 1: a >1 right edge drops the Geometry fact fail-closed.
        var element = new ObservedElement("Oob", null, 0, new ElementBounds(0.05f, 0.05f, 1.05f, 0.2f), "text");
        var observation = new Observation(ImmutableArray.Create(element), null, 7)
        {
            Sources = ImmutableArray.Create(Vision())
        };
        var context = SemanticObservationFactProjector.Project(observation);
        Assert.DoesNotContain(context.Facts, f => f.Kind == SemanticObservationFactKind.Geometry);
        // And the invariant holder itself rejects the same shape outright.
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticNormalizedBounds(0.05, 0.05, 1.0, 0.15));
    }

    [Fact]
    public void InvertedAndNegativeDimensionsStillFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticNormalizedBounds(0.5, 0.1, -0.3, 0.2));   // width < 0
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticNormalizedBounds(0.1, 0.5, 0.3, -0.2));   // height < 0
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticNormalizedBounds(-0.1, 0.1, 0.3, 0.2));   // left < 0
    }
}
