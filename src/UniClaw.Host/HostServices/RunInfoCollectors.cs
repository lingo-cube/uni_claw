using System.Runtime.InteropServices;
using UniClaw.Device;
using UniClaw.Host.Artifacts;

namespace UniClaw.Host.HostServices;

public static class RunMachineInfoCollector
{
    public static RunMachineInfo Collect()
    {
        return new RunMachineInfo(
            Os: RuntimeInformation.OSDescription,
            Arch: RuntimeInformation.OSArchitecture.ToString(),
            Runtime: RuntimeInformation.FrameworkDescription,
            Hostname: Environment.MachineName);
    }
}

public static class AdbSystemInfoCollector
{
    /// <summary>
    /// Collect Android system info via ADB getprop. Returns null on any failure
    /// (device offline, ADB unavailable, etc.) — the run proceeds normally.
    /// </summary>
    public static async Task<RunSystemInfo?> CollectAsync(
        IAdbSession adb,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sdkLevel = await GetPropAsync(adb, "ro.build.version.sdk", cancellationToken);
            var releaseVersion = await GetPropAsync(adb, "ro.build.version.release", cancellationToken);
            var buildFingerprint = await GetPropAsync(adb, "ro.build.fingerprint", cancellationToken);
            var codename = await GetPropAsync(adb, "ro.build.version.codename", cancellationToken);
            var arch = await GetPropAsync(adb, "ro.product.cpu.abi", cancellationToken);

            // If we got at least one value, consider it a valid collection
            if (sdkLevel is null && releaseVersion is null && buildFingerprint is null
                && codename is null && arch is null)
                return null;

            return new RunSystemInfo(sdkLevel, releaseVersion, buildFingerprint, codename, arch);
        }
        catch
        {
            return null; // ADB failure → null, run proceeds normally
        }
    }

    private static async Task<string?> GetPropAsync(
        IAdbSession adb,
        string prop,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await adb.ExecuteShellAsync(
                $"getprop {prop}",
                cancellationToken);
            if (!result.Success)
                return null;
            var value = result.StandardOutput?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
