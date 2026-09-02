using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Stage B1 capability coverage: occurrence-to-region spatial association is
/// max-overlap dominance with fail-closed ambiguity (REGION_BINDING !=
/// OWNERSHIP, REGION_BINDING != OCCURRENCE_IDENTITY).
/// </summary>
public sealed class OccurrenceRegionBindingTests
{
    private static readonly SpatialRegion Primary = new(
        "primary", SpatialRegionKind.ScrollableContent,
        new ElementBounds(0f, 0.1f, 1f, 0.9f), true, true, true);

    private static readonly SpatialRegion Chrome = new(
        "chrome", SpatialRegionKind.FixedChrome,
        new ElementBounds(0f, 0f, 1f, 0.1f), false, false, true);

    [Fact]
    public void DominantOverlapBindsPrimaryRegion()
    {
        var assessment = SpatialRegionBinding.Assess(
            new ElementBounds(0.1f, 0.5f, 0.9f, 0.7f),
            [Primary, Chrome]);

        Assert.NotNull(assessment.PrimarySpatialRegionRef);
        Assert.Equal(Primary.RegionRef, assessment.PrimarySpatialRegionRef);
        Assert.False(assessment.Ambiguous);
        Assert.True(assessment.OverlapRatio > 0.99d);
    }

    [Fact]
    public void BelowThresholdOverlapYieldsAmbiguousBindingWithoutPrimary()
    {
        // ~80% of the occurrence lies in the primary region, but the demanded
        // dominance threshold is 0.9: no dominant region exists.
        var assessment = SpatialRegionBinding.Assess(
            new ElementBounds(0.1f, 0.06f, 0.9f, 0.26f),
            [Primary, Chrome],
            dominantOverlapThreshold: 0.9f);

        Assert.Null(assessment.PrimarySpatialRegionRef);
        Assert.True(assessment.Ambiguous);
        // Best overlap ratio is retained as evidence even when ambiguous.
        Assert.True(assessment.OverlapRatio > 0.7d);
    }

    [Fact]
    public void EqualBestOverlapForTwoRegionsIsNotDominant()
    {
        var left = new SpatialRegion("left", SpatialRegionKind.Panel,
            new ElementBounds(0f, 0f, 0.5f, 1f), false, true, true);
        var right = new SpatialRegion("right", SpatialRegionKind.Panel,
            new ElementBounds(0.5f, 0f, 1f, 1f), false, true, true);

        var assessment = SpatialRegionBinding.Assess(
            new ElementBounds(0.25f, 0.1f, 0.75f, 0.9f),
            [left, right]);

        Assert.Null(assessment.PrimarySpatialRegionRef);
        Assert.True(assessment.Ambiguous);
        Assert.True(assessment.OverlapRatio > 0.49d);
    }

    [Fact]
    public void DisjointOccurrenceIsAmbiguous()
    {
        var assessment = SpatialRegionBinding.Assess(
            new ElementBounds(0f, 0.95f, 0.5f, 1f),
            [Primary, Chrome]);

        Assert.True(assessment.Ambiguous);
        Assert.Null(assessment.PrimarySpatialRegionRef);
    }

    [Fact]
    public void ZeroAreaOccurrenceIsAmbiguous()
    {
        var assessment = SpatialRegionBinding.Assess(
            new ElementBounds(0.5f, 0.5f, 0.5f, 0.5f),
            [Primary]);

        Assert.True(assessment.Ambiguous);
        Assert.Equal(0d, assessment.OverlapRatio);
    }

    [Fact]
    public void NoRegionsYieldsAmbiguousBinding()
    {
        var assessment = SpatialRegionBinding.Assess(
            new ElementBounds(0.1f, 0.2f, 0.8f, 0.8f),
            []);

        Assert.True(assessment.Ambiguous);
        Assert.Null(assessment.PrimarySpatialRegionRef);
    }

    [Fact]
    public void AmbiguousBindingRecordCarriesNoPrimaryRegion()
    {
        var binding = new OccurrenceRegionBinding(
            new ViewportOccurrenceRef("O1"),
            PrimarySpatialRegionRef: null,
            OverlapRatio: 0.4d,
            Ambiguous: true);

        Assert.True(binding.IsValid);
    }

    [Fact]
    public void AmbiguousRecordWithPrimaryRegionViolatesContract()
    {
        var binding = new OccurrenceRegionBinding(
            new ViewportOccurrenceRef("O1"),
            Primary.RegionRef,
            OverlapRatio: 0.4d,
            Ambiguous: true);

        Assert.False(binding.IsValid);
    }

    [Fact]
    public void OutOfRangeOverlapRatioViolatesContract()
    {
        var binding = new OccurrenceRegionBinding(
            new ViewportOccurrenceRef("O1"),
            Primary.RegionRef,
            OverlapRatio: 1.5d,
            Ambiguous: false);

        Assert.False(binding.IsValid);
    }
}
