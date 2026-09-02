using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Stage B1 capability coverage: SpatialRegion participation flags are
/// independent contract dimensions (container-runtime-v2-evidence-model,
/// spec: evidence-foundation "SpatialRegion 与 OccurrenceRegionBinding").
/// </summary>
public sealed class SpatialRegionTests
{
    [Fact]
    public void FixedChromeBackControlDoesNotScrollOrCoverButRemainsGroundable()
    {
        var region = new SpatialRegion(
            "chrome-back",
            SpatialRegionKind.FixedChrome,
            new ElementBounds(0.00f, 0.00f, 0.20f, 0.08f),
            ParticipatesInScroll: false,
            ParticipatesInCoverage: false,
            ParticipatesInGrounding: true);

        Assert.False(region.ParticipatesInScroll);
        Assert.False(region.ParticipatesInCoverage);
        Assert.True(region.ParticipatesInGrounding);
        Assert.True(region.IsValid);
    }

    [Fact]
    public void ScrollableContentParticipatesInAllThreeDimensions()
    {
        var region = new SpatialRegion(
            "primary",
            SpatialRegionKind.ScrollableContent,
            new ElementBounds(0f, 0.08f, 1f, 0.92f),
            ParticipatesInScroll: true,
            ParticipatesInCoverage: true,
            ParticipatesInGrounding: true);

        Assert.True(region.ParticipatesInScroll);
        Assert.True(region.ParticipatesInCoverage);
        Assert.True(region.ParticipatesInGrounding);
    }

    [Fact]
    public void HorizontalPagerCoversWithoutVerticalScrollParticipation()
    {
        var region = new SpatialRegion(
            "pager",
            SpatialRegionKind.Panel,
            new ElementBounds(0f, 0.4f, 1f, 0.6f),
            ParticipatesInScroll: false,
            ParticipatesInCoverage: true,
            ParticipatesInGrounding: true);

        Assert.False(region.ParticipatesInScroll);
        Assert.True(region.ParticipatesInCoverage);
        Assert.True(region.ParticipatesInGrounding);
    }

    [Fact]
    public void WhitespaceReferenceIsRejectedAtConstruction()
    {
        // Ref types are validated at construction (repo pattern): an invalid
        // reference cannot enter the evidence model silently.
        Assert.Throws<ArgumentException>(() => new SpatialRegionRef(" "));
    }

    [Fact]
    public void MalformedBoundsAreInvalid()
    {
        var region = new SpatialRegion(
            "primary",
            SpatialRegionKind.ScrollableContent,
            new ElementBounds(0.9f, 0.0f, 0.1f, 1.0f),
            ParticipatesInScroll: true,
            ParticipatesInCoverage: true,
            ParticipatesInGrounding: true);

        Assert.False(region.IsValid);
    }

    [Fact]
    public void UndefinedKindIsInvalid()
    {
        var region = new SpatialRegion(
            "primary",
            (SpatialRegionKind)(-1),
            new ElementBounds(0f, 0f, 1f, 1f),
            ParticipatesInScroll: true,
            ParticipatesInCoverage: true,
            ParticipatesInGrounding: true);

        Assert.False(region.IsValid);
    }
}
