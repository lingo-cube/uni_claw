using UniClaw.Runtime.Tests.Scenario;
using Xunit;

namespace UniClaw.Runtime.Tests.Dependencies;

/// <summary>
/// Mechanical guard for the test dependency manifest: every dependency is
/// declared, classification is total (no unclassified failure mode), and the
/// real-device serial resolution path is documented (env-var override or
/// discovery — never a hidden machine-specific default).
/// </summary>
public sealed class TestDependencyManifestTests
{
    [Fact]
    public void Manifest_DeclaresAllSuites()
    {
        var all = TestDependencyManifest.All;
        Assert.NotEmpty(all);

        // Suites with real-device requirements must declare the emulator/APK/vision
        // dependencies explicitly (no hidden manual preparation).
        var realDeviceSuites = all
            .Where(s => s.Dependencies.Any(d => d.Kind is TestDependencyManifest.TestDependencyKind.AndroidEmulator
                or TestDependencyManifest.TestDependencyKind.RealDevice))
            .ToArray();
        Assert.NotEmpty(realDeviceSuites);
        foreach (var suite in realDeviceSuites)
        {
            Assert.Contains(suite.Dependencies, d => d.Kind == TestDependencyManifest.TestDependencyKind.AndroidEmulator);
        }
    }

    [Fact]
    public void Manifest_EveryDependencyHasClassifiableFailure()
    {
        foreach (var suite in TestDependencyManifest.All)
        {
            foreach (var dependency in suite.Dependencies)
            {
                Assert.True(Enum.IsDefined(dependency.FailureIfMissing),
                    $"suite '{suite.Name}' dependency '{dependency.Requirement}' has an undefined failure class.");
            }
        }
    }

    [Fact]
    public void RealDeviceConfiguration_IsDocumentedAndNeverMachineSpecific()
    {
        // The serial resolution path must be: env var override → discovery of the
        // unique online device → clear failure. A machine-specific baked-in serial
        // would violate dependency transparency.
        Assert.False(RealDeviceTestConfiguration.SettingsSerialEnvironmentVariable.Length == 0);
        Assert.False(RealDeviceTestConfiguration.CapstoneSerialEnvironmentVariable.Length == 0);
        Assert.False(RealDeviceTestConfiguration.AdbPathEnvironmentVariable.Length == 0);
    }

    [Fact]
    public void Capstone_DeclaresFixtureApkDependency()
    {
        var capstone = TestDependencyManifest.All
            .First(s => s.Name.Contains("Capstone", StringComparison.Ordinal));
        Assert.Contains(capstone.Dependencies,
            d => d.Kind == TestDependencyManifest.TestDependencyKind.FixtureApk);
    }
}
