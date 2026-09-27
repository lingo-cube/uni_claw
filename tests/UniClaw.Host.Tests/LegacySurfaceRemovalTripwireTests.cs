using System.Reflection;
using UniClaw.Host;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class LegacySurfaceRemovalTripwireTests
{
    [Fact]
    public void ProductHostOptions_HasNoLegacyEgressSwitch()
    {
        Assert.Null(typeof(HostRunner.HostOptions).GetProperty("LegacyStateEgress"));
        Assert.DoesNotContain(typeof(HostRunner.HostOptions).GetProperties(),
            p => p.Name.Contains("Legacy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductHostAssembly_HasNoLegacyStateRouteSymbols()
    {
        var names = typeof(HostRunner).Assembly.GetTypes().Select(t => t.FullName ?? "").ToArray();
        Assert.DoesNotContain(names, n => n.Contains("LegacyState", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("MapTargetStateClaim", StringComparison.Ordinal));
    }
}
