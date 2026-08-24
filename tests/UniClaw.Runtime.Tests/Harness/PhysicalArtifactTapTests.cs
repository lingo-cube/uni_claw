using System.Collections.Immutable;
using SkiaSharp;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Harness;

public sealed class PhysicalArtifactTapTests
{
    [Fact]
    public async Task Tap_receives_same_generation_frame_candidates_png_and_observation()
    {
        using var bitmap = new SKBitmap(8, 6);
        var candidate = new PerceptionCandidate("Wi-Fi", "menuItem", null);
        PhysicalArtifactTap? report = null;
        var environment = CreateEnvironment(bitmap, [candidate], tap => report = tap);

        var observation = await environment.ObserveAsync(CancellationToken.None);

        Assert.NotNull(report);
        Assert.Same(observation, report!.Observation);
        Assert.Equal(observation.SequenceNumber, report.SequenceNumber);
        Assert.Single(report.Candidates);
        Assert.Equal(candidate, report.Candidates[0]);
        Assert.Equal(8, report.Width);
        Assert.Equal(6, report.Height);
        Assert.NotEmpty(report.PngBytes);
        using var decoded = SKBitmap.Decode(report.PngBytes);
        Assert.NotNull(decoded);
        Assert.Equal(8, decoded!.Width);
        Assert.Equal(6, decoded.Height);
    }

    [Fact]
    public async Task Tap_fault_is_isolated_from_observation_and_dispatch()
    {
        using var bitmap = new SKBitmap(8, 6);
        var environment = CreateEnvironment(bitmap, [], _ => throw new InvalidOperationException("tap fault"));

        var observation = await environment.ObserveAsync(CancellationToken.None);
        var result = await environment.ExecuteAsync(new DeviceAction.SystemBack(), CancellationToken.None);

        Assert.Equal(1, observation.SequenceNumber);
        Assert.Equal(ActionResultOutcome.Dispatched, result.Outcome);
        Assert.Single(environment.ObservationHistory);
        Assert.Single(environment.ActionHistory);
    }

    [Fact]
    public async Task Without_tap_observe_behavior_is_unchanged()
    {
        using var bitmap = new SKBitmap(8, 6);
        var environment = CreateEnvironment(bitmap, [new PerceptionCandidate("A", "menuItem", null)]);

        var observation = await environment.ObserveAsync(CancellationToken.None);

        Assert.Equal(1, observation.SequenceNumber);
        Assert.Single(observation.Elements);
        Assert.Equal("A", observation.Elements[0].Text);
    }

    private static PhysicalEnvironment CreateEnvironment(
        SKBitmap bitmap,
        ImmutableArray<PerceptionCandidate> candidates,
        Action<PhysicalArtifactTap>? tap = null)
        => new(
            new ScreenshotSource(bitmap),
            new PerceptionSource(candidates),
            new DispatchTarget(),
            "com.android.settings",
            bitmap.Width,
            bitmap.Height,
            artifactTap: tap);

    private sealed class ScreenshotSource(SKBitmap bitmap) : IScreenshotSource
    {
        public Task<ScreenshotCapture> CaptureAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ScreenshotCapture(bitmap, bitmap.Width, bitmap.Height));
    }

    private sealed class PerceptionSource(ImmutableArray<PerceptionCandidate> candidates) : IPerceptionSource
    {
        public Task<ImmutableArray<PerceptionCandidate>> AnalyzeAsync(
            SKBitmap screenshot, int width, int height, CancellationToken cancellationToken)
            => Task.FromResult(candidates);
    }

    private sealed class DispatchTarget : IAdbDispatchTarget
    {
        public Task<ActionResult> ExecuteAsync(AdbOperation operation, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "stub", "stub"));
    }
}
