using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// SCROLL EXECUTION PROFILE — deterministic proofs.
///
/// The physical swipe duration is derived from the actual distance so the
/// velocity stays capped (~900 px/s) — high-speed flings blur the frame and
/// degrade OCR. The SEMANTIC StepFraction is unchanged (DeviceAction is pure);
/// only the Adapter-side execution timing is profile-derived. Pure translator
/// tests: no device, no ADB process, no Settings vocabulary.
/// </summary>
public sealed class ScrollExecutionProfileTests
{
    private const int Width = 1080;
    private const int Height = 1920;
    private const float VelocityCapPxPerMs = 0.9f; // ~900 px/s

    [Fact]
    public void DifferentStepFractions_ProduceDifferentDurations()
    {
        var (d1, dur1, _) = DeviceActionTranslator.ScrollProfile(0.4f, Height);
        var (d2, dur2, _) = DeviceActionTranslator.ScrollProfile(0.8f, Height);

        // Larger semantic step -> larger physical distance -> longer duration
        // (velocity stays bounded; duration is NOT constant).
        Assert.True(d2 > d1, $"distance {d1} -> {d2}");
        Assert.True(dur2 > dur1, $"duration {dur1} -> {dur2}");
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.4f)]
    [InlineData(0.6f)]
    [InlineData(0.8f)]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    public void Velocity_NeverExceedsCap(float stepFraction)
    {
        var (distance, duration, velocity) = DeviceActionTranslator.ScrollProfile(stepFraction, Height);
        Assert.True(duration >= 200, $"duration floor violated: {duration}ms");
        Assert.True(velocity <= VelocityCapPxPerMs + 0.0001f,
            $"velocity {velocity * 1000f:0}px/s exceeds cap for fraction {stepFraction} (distance={distance}px, duration={duration}ms)");
    }

    [Fact]
    public void DeviceActionSemantics_Unchanged()
    {
        // ScrollForward/ScrollBackward remain pure semantic actions: only
        // StepFraction, no physical parameters leaked into the model.
        var forward = new DeviceAction.ScrollForward(0.6f);
        var backward = new DeviceAction.ScrollBackward(0.6f);
        Assert.Equal(0.6f, forward.StepFraction);
        Assert.Equal(0.6f, backward.StepFraction);
        // Non-scroll actions translate to duration-free operations (default compat).
        var tap = DeviceActionTranslator.Translate(new DeviceAction.Tap(null, new ElementBounds(0, 0.5f, 1, 0.6f)), Width, Height);
        Assert.IsType<AdbOperation.Tap>(tap);
        Assert.IsType<AdbOperation.KeyEvent>(
            DeviceActionTranslator.Translate(new DeviceAction.SystemBack(), Width, Height));
    }

    [Fact]
    public void Translate_CarriesDurationIntoCommand()
    {
        var op = DeviceActionTranslator.Translate(new DeviceAction.ScrollForward(0.6f), Width, Height);
        var swipe = Assert.IsType<AdbOperation.Swipe>(op);
        Assert.NotNull(swipe.Duration);

        var cmd = AdbDispatchTarget.BuildCommand(swipe);
        Assert.NotNull(cmd);
        Assert.Equal("shell", cmd[0]);
        Assert.Equal("input", cmd[1]);
        Assert.Equal("swipe", cmd[2]);
        // The explicit duration is the LAST token (adb `input swipe x1 y1 x2 y2 duration`).
        Assert.Equal(swipe.Duration.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), cmd[^1]);
    }

    [Fact]
    public void NoSettingsVocabulary_VisionOnly_NoAdbProcess()
    {
        var op = DeviceActionTranslator.Translate(new DeviceAction.ScrollForward(0.4f), Width, Height);
        var cmd = AdbDispatchTarget.BuildCommand(op!);
        Assert.DoesNotContain("Settings", string.Join(" ", cmd!), StringComparison.Ordinal);
        Assert.DoesNotContain("WiFi", string.Join(" ", cmd!), StringComparison.Ordinal);
        Assert.DoesNotContain("Android", string.Join(" ", cmd!), StringComparison.Ordinal);
        // Vision-only / ADB-independent: this proof runs without any device or
        // adb process — translation is pure arithmetic on StepFraction/dimensions.
        var (distance, _, _) = DeviceActionTranslator.ScrollProfile(0.4f, Height);
        Assert.True(distance > 0f);
    }
}
