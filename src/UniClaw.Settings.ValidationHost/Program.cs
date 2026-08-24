using UniClaw.Runtime.PhysicalHost;

namespace UniClaw.Settings.ValidationHost;

/// <summary>
/// Scenario validation composition root. This executable is deliberately outside
/// the production physical host and is never a DriverHost or run.start entry point.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Any(static argument => argument == "--execute"))
            return await LegacyProofRunner.RunAsync(args);

        var mode = ValidationOptions.Parse(args);
        if (mode is null) return 64;

        Console.WriteLine($"VALIDATION scenario={mode.Scenario} app={mode.Application} intent={mode.LaunchIntent}");
        if (mode.DryRun) return 0;

        var options = new PhysicalHostOptions(
            mode.Adb, mode.Serial, mode.Application, mode.VisionSocket,
            PhysicalHostOptions.DefaultDisplayWidth, PhysicalHostOptions.DefaultDisplayHeight,
            mode.LaunchIntent);
        if (!mode.DryRun)
            return await ValidationRunner.RunAsync(mode.Scenario, options, CancellationToken.None);
        var resolution = await PhysicalHostComposition.ResolveDeviceAsync(options, CancellationToken.None);
        if (!resolution.IsResolved)
        {
            Console.Error.WriteLine($"VALIDATION_NOT_READY {resolution.FailureReason ?? "device unavailable"}");
            return 2;
        }
        Console.WriteLine($"VALIDATION_DEVICE {resolution.Serial}");
        return 0;
    }

    private sealed record ValidationOptions(
        string Scenario, string Application, string LaunchIntent, string Adb,
        string? Serial, string? VisionSocket, bool DryRun)
    {
        public static ValidationOptions? Parse(string[] args)
        {
            var scenario = "settings-tree";
            var app = "com.android.settings";
            var intent = "android.settings.SETTINGS";
            var adb = "adb";
            string? serial = null;
            string? socket = null;
            var dryRun = true;
            for (var i = 0; i < args.Length; i++)
            {
                if (i + 1 >= args.Length && args[i] is "--scenario" or "--app" or "--launch-intent" or "--adb" or "--serial" or "--vision-socket")
                    return null;
                switch (args[i])
                {
                    case "--scenario": scenario = args[++i]; break;
                    case "--app": app = args[++i]; break;
                    case "--launch-intent": intent = args[++i]; break;
                    case "--adb": adb = args[++i]; break;
                    case "--serial": serial = args[++i]; break;
                    case "--vision-socket": socket = args[++i]; break;
                    case "--execute": dryRun = false; break;
                    case "--slice2": scenario = "settings-toggle"; intent = "android.settings.WIFI_SETTINGS"; break;
                    case "--multilevel": scenario = "settings-tree"; break;
                    case "--scroll": scenario = "developer-options-scroll"; intent = "com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS"; break;
                    case "--corpus": scenario = "settings-corpus"; break;
                    default: return null;
                }
            }
            return new ValidationOptions(scenario, app, intent, adb, serial, socket, dryRun);
        }
    }
}
