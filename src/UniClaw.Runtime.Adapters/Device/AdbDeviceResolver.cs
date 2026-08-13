using System.Text;

namespace UniClaw.Runtime.Adapters.Device;

/// <summary>Minimum deterministic device selection for one ADB mechanism consumer.</summary>
public sealed class AdbDeviceResolver
{
    private static readonly TimeSpan DevicesTimeout = TimeSpan.FromSeconds(5);
    private readonly IAdbProcessRunner _runner;
    private readonly string _adbExecutable;
    private readonly string? _configuredSerial;

    public AdbDeviceResolver(string adbExecutable = "adb", string? configuredSerial = null)
        : this(new AdbProcessRunner(), adbExecutable, configuredSerial) { }

    internal AdbDeviceResolver(IAdbProcessRunner runner, string adbExecutable, string? configuredSerial)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? throw new ArgumentException("ADB executable is required.", nameof(adbExecutable)) : adbExecutable;
        _configuredSerial = string.IsNullOrWhiteSpace(configuredSerial) ? null : configuredSerial;
    }

    internal IAdbProcessRunner Runner => _runner;
    internal string AdbExecutable => _adbExecutable;

    public async Task<AdbDeviceResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(_adbExecutable, ["devices"], DevicesTimeout, cancellationToken);
        if (!result.Started)
            return AdbDeviceResolution.Failed("ADB executable is unavailable: " + result.FailureReason);
        if (result.TimedOut)
            return AdbDeviceResolution.Failed("ADB device listing timed out.");
        if (result.ExitCode != 0)
            return AdbDeviceResolution.Failed("ADB device listing failed: " + result.StandardError);

        var parsed = ParseDevices(result.StandardOutput);
        if (parsed is null)
            return AdbDeviceResolution.Failed("ADB device listing was malformed.");

        if (_configuredSerial is not null)
        {
            var configured = parsed.Where(device => device.Serial == _configuredSerial).ToArray();
            return configured.Length == 1 && configured[0].State == AdbDeviceState.Device
                ? AdbDeviceResolution.Selected(configured[0].Serial)
                : AdbDeviceResolution.Failed("Configured ADB device is missing or not online: " + _configuredSerial);
        }

        var eligible = parsed.Where(device => device.State == AdbDeviceState.Device).ToArray();
        return eligible.Length switch
        {
            1 => AdbDeviceResolution.Selected(eligible[0].Serial),
            0 => AdbDeviceResolution.Failed("No eligible online ADB device was found."),
            _ => AdbDeviceResolution.Failed("Multiple eligible ADB devices found; explicit serial is required."),
        };
    }

    internal static IReadOnlyList<AdbDevice>? ParseDevices(byte[] output)
    {
        var text = Encoding.UTF8.GetString(output).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || lines[0] != "List of devices attached")
            return null;

        var devices = new List<AdbDevice>();
        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split('\t', StringSplitOptions.None);
            if (fields.Length != 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
                return null;
            var state = fields[1] switch
            {
                "device" => AdbDeviceState.Device,
                "offline" => AdbDeviceState.Offline,
                "unauthorized" => AdbDeviceState.Unauthorized,
                _ => AdbDeviceState.Other,
            };
            devices.Add(new(fields[0], state));
        }
        return devices;
    }
}

public sealed record AdbDeviceResolution(string? Serial, string? FailureReason)
{
    public bool IsResolved => Serial is not null;
    public static AdbDeviceResolution Selected(string serial) => new(serial, null);
    public static AdbDeviceResolution Failed(string reason) => new(null, reason);
}

internal sealed record AdbDevice(string Serial, AdbDeviceState State);
internal enum AdbDeviceState { Device, Offline, Unauthorized, Other }
