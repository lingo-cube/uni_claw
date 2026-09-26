using System.Text.Json;
using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// CSC-001 Slice B：frame claim 携带 capture 实测尺寸——
/// ScreenFrameOccurrenceStrategy 解析 w/h → occurrence.Space；缺 w/h 的
/// legacy claim → Space null（不伪造）。Simulation 孪生
/// （DevV0Runtime.FrameOccurrenceStrategy）逐行同步（C1 保真度）。
/// </summary>
public sealed class ScreenFrameSpaceTests
{
    private static ProposedOccurrence? Derive(string frameJson)
    {
        var record = new EvidenceRecord(
            "ev-frame-test", new ObservationClaim("screen.frame", frameJson),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("host.test", DateTimeOffset.Now, "scope:screen.frame", new[] { "test" }));
        return new ScreenFrameOccurrenceStrategy().Derive(record, previous: null)
            .FirstOrDefault();
    }

    [Fact]
    public void FrameClaim_WithMeasuredDims_CarriesDeviceViewportSpace()
    {
        var occurrence = Derive(
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.83,0.40,0.96,0.45],\"w\":1080,\"h\":1920,\"f\":\"device-viewport\"}");

        Assert.NotNull(occurrence);
        Assert.NotNull(occurrence!.Space);
        Assert.Equal("device-viewport:1080x1920@0", occurrence.Space!.CoordinateSpaceId);
        Assert.Equal(1080, occurrence.Space.PixelWidth);
        Assert.Equal(1920, occurrence.Space.PixelHeight);
    }

    [Fact]
    public void FrameClaim_WrongConfiguredDims_AreJustDifferentSpace()
    {
        // E4 形态：capture 实测 1080×1920；若另一空间声称 1080×2400 → 不匹配
        var occurrence = Derive(
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.9,0.4,0.96,0.45],\"w\":1080,\"h\":1920,\"f\":\"device-viewport\"}");

        var configured = CoordinateSpace.DeviceViewport(1080, 2400);
        Assert.False(occurrence!.Space!.Matches(configured));
        Assert.True(occurrence.Space.Matches(CoordinateSpace.DeviceViewport(1080, 1920)));
    }

    [Fact]
    public void LegacyFrameClaim_WithoutDims_SpaceIsNull_NotFabricated()
    {
        var occurrence = Derive(
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.83,0.40,0.96,0.45],\"f\":\"device-viewport\"}");

        Assert.NotNull(occurrence);
        Assert.Null(occurrence!.Space); // 不伪造尺寸；消费端（Slice C/D）fail-closed
        Assert.NotNull(occurrence.Locator);
    }

    [Fact]
    public void LandscapeCapture_DistinctSpace()
    {
        var occurrence = Derive(
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.4,0.83,0.45,0.96],\"w\":1920,\"h\":1080,\"f\":\"device-viewport\"}");

        Assert.Equal("device-viewport:1920x1080@0", occurrence!.Space!.CoordinateSpaceId);
        Assert.False(occurrence.Space.Matches(CoordinateSpace.DeviceViewport(1080, 1920)));
    }
}
