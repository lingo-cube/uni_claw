using UniClaw.Runtime.Adapters.Device;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// Single source of truth for REAL-device test configuration.
///
/// Replaces the previously hardcoded machine-specific values
/// (e.g. `"/Users/fran/Android/Sdk/platform-tools/adb"`,
/// `"emulator-5554"`, `"emulator-5556"`) with:
///
///   1. explicit environment variables (CI/other machines), else
///   2. discovery of the unique online ADB device via the production
///      <see cref="AdbDeviceResolver"/> (exactly one eligible device),
///      else
///   3. a clear failure explaining what is required — never a silent
///      machine-specific default.
///
/// Resolution is lazy: deterministic tests in the same class as a
/// RealDevice test never touch this configuration, so they are
/// unaffected by an absent device/adb.
/// </summary>
public static class RealDeviceTestConfiguration
{
    /// <summary>ADB executable path. Env: UNICLAW_ADB_PATH; else "adb" on PATH.</summary>
    public const string AdbPathEnvironmentVariable = "UNICLAW_ADB_PATH";

    /// <summary>Primary Settings real-device serial. Env: UNICLAW_SETTINGS_SERIAL.</summary>
    public const string SettingsSerialEnvironmentVariable = "UNICLAW_SETTINGS_SERIAL";

    /// <summary>Capstone real-emulator serial. Env: UNICLAW_CAPSTONE_SERIAL.</summary>
    public const string CapstoneSerialEnvironmentVariable = "UNICLAW_CAPSTONE_SERIAL";

    private static readonly Lazy<string> AdbPathLazy = new(ResolveAdbPath);
    private static readonly Lazy<string> SettingsSerialLazy = new(
        () => ResolveSerial(SettingsSerialEnvironmentVariable));
    private static readonly Lazy<string> CapstoneSerialLazy = new(
        () => ResolveSerial(CapstoneSerialEnvironmentVariable));

    /// <summary>ADB executable path (env override, else "adb" resolved from PATH).</summary>
    public static string AdbPath => AdbPathLazy.Value;

    /// <summary>Serial for Settings real-device suites (env override, else unique online device).</summary>
    public static string SettingsSerial => SettingsSerialLazy.Value;

    /// <summary>Serial for the capstone real-emulator suite (env override, else unique online device).</summary>
    public static string CapstoneSerial => CapstoneSerialLazy.Value;

    private static string ResolveAdbPath()
    {
        var fromEnv = System.Environment.GetEnvironmentVariable(AdbPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        // "adb" on PATH is the portable default; a clear failure below (when
        // invoked) explains if it is missing — no machine-specific path baked in.
        return "adb";
    }

    private static string ResolveSerial(string environmentVariable)
    {
        var fromEnv = System.Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        // Fall back to discovering the unique online ADB device. The production
        // resolver fails clearly for zero / multiple eligible devices instead of
        // guessing — the test failure message states exactly what is required.
        var resolution = new AdbDeviceResolver(AdbPath).ResolveAsync().GetAwaiter().GetResult();
        if (resolution.IsResolved)
            return resolution.Serial!;

        throw new InvalidOperationException(
            $"RealDevice test requires an ADB device serial. Set {environmentVariable} to the " +
            $"serial of an online device, or connect exactly one online device so it can be " +
            $"discovered automatically. Resolver: {resolution.FailureReason}");
    }
}
