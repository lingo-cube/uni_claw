using UniClaw.Host.HostServices;
using Xunit;

namespace UniClaw.Host.Tests.HostServices;

/// <summary>
/// Task 7.5 — RunMachineInfoCollector sanity tests. Zero external dependencies
/// (RuntimeInformation + Environment.MachineName), so values are available on any
/// host without ADB.
/// </summary>
public sealed class RunInfoCollectorsTests
{
    [Fact]
    public void MachineInfoCollector_ReturnsPopulatedRecord()
    {
        var info = RunMachineInfoCollector.Collect();

        Assert.False(string.IsNullOrWhiteSpace(info.Os), "Os must be populated");
        Assert.False(string.IsNullOrWhiteSpace(info.Arch), "Arch must be populated");
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime), "Runtime must be populated");
        Assert.False(string.IsNullOrWhiteSpace(info.Hostname), "Hostname must be populated");
    }

    [Fact]
    public void MachineInfoCollector_OsContainsDarwinOrLinuxOrWindows()
    {
        var info = RunMachineInfoCollector.Collect();

        Assert.True(
            info.Os.Contains("Darwin", StringComparison.OrdinalIgnoreCase)
            || info.Os.Contains("macOS", StringComparison.OrdinalIgnoreCase)
            || info.Os.Contains("Linux", StringComparison.OrdinalIgnoreCase)
            || info.Os.Contains("Windows", StringComparison.OrdinalIgnoreCase),
            $"Unexpected OS description: {info.Os}");
    }
}
