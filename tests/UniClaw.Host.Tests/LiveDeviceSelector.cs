using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UniClaw.Host.Tests;

/// Resolves one capable live-test device. It never chooses the first device.
internal static class LiveDeviceSelector
{
    internal sealed record Result(string? Serial, string Status, string Detail, int? ApiLevel = null)
    {
        internal bool IsUsable => Serial is not null && Status == "READY";
    }

    internal static Result Resolve()
    {
        var explicitSerial = Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE")
            ?? Environment.GetEnvironmentVariable("DSH_TEST_PERCEPTION_DEVICE");
        if (!string.IsNullOrWhiteSpace(explicitSerial))
            return Probe(explicitSerial.Trim());

        var devices = Run("adb", "devices").Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(line => line.Split('\t', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && parts[1].Trim() == "device")
            .Select(parts => parts[0].Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return ResolveEligible(devices, Probe);
    }

    internal static Result ResolveEligible(IEnumerable<string> devices, Func<string, Result> probe)
    {
        var capable = devices.Distinct(StringComparer.Ordinal).Select(probe).Where(r => r.IsUsable).ToArray();
        if (capable.Length == 0)
            return new Result(null, "ENVIRONMENT_UNAVAILABLE", "no device satisfies capability manifest");
        if (capable.Length > 1)
            return new Result(null, "AMBIGUOUS_DEVICE", $"{capable.Length} devices satisfy capability manifest: {string.Join(", ", capable.Select(c => c.Serial))}");
        return capable[0];
    }

    internal static string? WaitForSwitchHierarchy(string serial, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(12));
        string? previous = null;
        while (DateTime.UtcNow < deadline)
        {
            var (xml, _) = UiAutomatorDump.TryDumpToDevice(serial);
            if (xml is not null && Regex.IsMatch(
                    xml,
                    "class=\"android\\.widget\\.Switch\"[^>]*checkable=\"true\"[^>]*checked=\"(?:true|false)\"",
                    RegexOptions.CultureInvariant))
            {
                if (previous is not null && previous.Contains("class=\"android.widget.Switch\"", StringComparison.Ordinal))
                    return xml;
                previous = xml;
            }
            else
            {
                previous = null;
            }
            Thread.Sleep(500);
        }
        return null;
    }

    private static Result Probe(string serial)
    {
        var apiRaw = Run("adb", "-s", serial, "shell", "getprop", "ro.build.version.sdk");
        if (!apiRaw.Success || !int.TryParse(apiRaw.Output.Trim(), out var api) || api < 35)
            return new Result(serial, "ENVIRONMENT_UNAVAILABLE", $"API level is unavailable or below 35 ({apiRaw.Output.Trim()})");
        var wm = Run("adb", "-s", serial, "shell", "wm", "size");
        if (!wm.Success || string.IsNullOrWhiteSpace(wm.Output)) return new Result(serial, "ENVIRONMENT_UNAVAILABLE", "wm size unavailable", api);
        if (!Retry(() => Run("adb", "-s", serial, "exec-out", "screencap", "-p").Success)) return new Result(serial, "ENVIRONMENT_UNAVAILABLE", "screenshot unavailable", api);
        var uiautomator = Run("adb", "-s", serial, "shell", "uiautomator", "help");
        if (!uiautomator.Success && !Retry(() => Run("adb", "-s", serial, "shell", "uiautomator", "help").Success)) return new Result(serial, "ENVIRONMENT_UNAVAILABLE", $"uiautomator unavailable: {uiautomator.Output.Trim()}", api);
        var settings = Run("adb", "-s", serial, "shell", "settings", "get", "global", "wifi_on");
        if (!settings.Success && !Retry(() => Run("adb", "-s", serial, "shell", "settings", "get", "global", "wifi_on").Success)) return new Result(serial, "ENVIRONMENT_UNAVAILABLE", $"settings fixture unavailable: {settings.Output.Trim()}", api);
        return new Result(serial, "READY", wm.Output.Trim(), api);
    }

    private static bool Retry(Func<bool> probe)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (probe()) return true;
            Thread.Sleep(250);
        }
        return false;
    }

    private static (bool Success, string Output) Run(string fileName, params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo(fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in args) info.ArgumentList.Add(arg);
            using var process = Process.Start(info);
            if (process is null) return (false, "process unavailable");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);
            return (process.ExitCode == 0, string.IsNullOrWhiteSpace(output) ? error : output);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return (false, ex.Message);
        }
    }
}
