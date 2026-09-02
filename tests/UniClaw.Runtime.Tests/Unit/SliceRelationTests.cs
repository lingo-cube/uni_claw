using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Stage B1 capability coverage: SliceRelation is region-bound pairwise
/// spatial evidence; gap evidence is derivable and never becomes coverage
/// authority (SLICE_ALIGNMENT != ITEM_IDENTITY).
/// </summary>
public sealed class SliceRelationTests
{
    private static RegionRelation Relation(
        float overlap,
        RegionContinuity continuity,
        SpatialRelationConfidenceBand band = SpatialRelationConfidenceBand.High)
        => new(
            new SpatialRegionRef("primary"),
            new SpatialRegionRef("primary"),
            new SpatialTranslation(0f, -0.8f),
            new SpatialRelationUncertainty(0.02f, band),
            overlap,
            continuity,
            ImmutableArray.Create(SpatialEvidenceChannel.OccurrenceAnchorMatching, SpatialEvidenceChannel.PixelRegistration));

    [Fact]
    public void V1ShapeWithSinglePrimaryRegionRelationIsValid()
    {
        var relation = new SliceRelation(
            new ContainerSliceRef("S1"),
            new ContainerSliceRef("S2"),
            [Relation(0.2f, RegionContinuity.Continuous)]);

        Assert.True(relation.IsValid);
        Assert.Single(relation.Regions);
    }

    [Fact]
    public void LargeDisplacementWithLowOverlapCarriesGapEvidence()
    {
        // Fast-scroll buyer: dy ≈ 1.8 viewports with 0.05 overlap — an
        // uncovered interval exists and the evidence MUST expose it.
        var relation = new SliceRelation(
            new ContainerSliceRef("S1"),
            new ContainerSliceRef("S2"),
            [new RegionRelation(
                new SpatialRegionRef("primary"),
                new SpatialRegionRef("primary"),
                new SpatialTranslation(0f, -1.8f),
                new SpatialRelationUncertainty(0.1f, SpatialRelationConfidenceBand.Medium),
                0.05f,
                RegionContinuity.Gap,
                ImmutableArray.Create(SpatialEvidenceChannel.RobustConsensus))]);

        Assert.True(relation.IndicatesUncoveredGap);
    }

    [Fact]
    public void ContinuousOverlapDoesNotIndicateGap()
    {
        var relation = new SliceRelation(
            new ContainerSliceRef("S1"),
            new ContainerSliceRef("S2"),
            [Relation(0.25f, RegionContinuity.Continuous)]);

        Assert.False(relation.IndicatesUncoveredGap);
    }

    [Fact]
    public void MultiRegionShapePreservesPerRegionEvidence()
    {
        // IVI shape: media scrolls, sidebar/climate static — same pairwise
        // relation carries per-region translations without domain change.
        var media = Relation(0.2f, RegionContinuity.Continuous) with { Translation = new SpatialTranslation(0f, -380f / 1000f) };
        var sidebar = Relation(1f, RegionContinuity.Continuous) with
        {
            FromSpatialRegionRef = new SpatialRegionRef("sidebar"),
            ToSpatialRegionRef = new SpatialRegionRef("sidebar"),
            Translation = new SpatialTranslation(0f, 0f),
        };

        var relation = new SliceRelation(
            new ContainerSliceRef("S31"),
            new ContainerSliceRef("S32"),
            [media, sidebar]);

        Assert.True(relation.IsValid);
        Assert.Equal(2, relation.Regions.Length);
        Assert.All(relation.Regions, r => Assert.False(r.Continuity == RegionContinuity.Gap));
    }

    [Fact]
    public void EmptyRegionRelationsAreInvalid()
    {
        var relation = new SliceRelation(
            new ContainerSliceRef("S1"),
            new ContainerSliceRef("S2"));

        Assert.False(relation.IsValid);
    }

    [Fact]
    public void OutOfRangeOverlapMakesRelationInvalid()
    {
        Assert.False(Relation(1.2f, RegionContinuity.Continuous).IsValid);
    }
}
