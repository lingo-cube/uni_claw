using Xunit;

namespace UniClaw.Host.Tests;

public sealed class LiveDeviceSelectorTests
{
    private static LiveDeviceSelector.Result Ready(string serial) =>
        new(serial, "READY", "fixture", 35);

    [Fact]
    public void SingleEligibleDevice_IsSelected()
    {
        var result = LiveDeviceSelector.ResolveEligible(["fixture-1"], Ready);
        Assert.Equal("READY", result.Status);
        Assert.Equal("fixture-1", result.Serial);
    }

    [Fact]
    public void ZeroEligibleDevices_IsEnvironmentUnavailable()
    {
        var result = LiveDeviceSelector.ResolveEligible([], _ =>
            new LiveDeviceSelector.Result(null, "ENVIRONMENT_UNAVAILABLE", "fixture"));
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", result.Status);
    }

    [Fact]
    public void MultipleEligibleDevices_AreAmbiguous()
    {
        var result = LiveDeviceSelector.ResolveEligible(["fixture-1", "fixture-2"], Ready);
        Assert.Equal("AMBIGUOUS_DEVICE", result.Status);
        Assert.Null(result.Serial);
    }
}
