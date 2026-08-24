using UniClaw.Runtime.PhysicalHost;

namespace UniClaw.Settings.ValidationHost;

/// <summary>Legacy scenario-proof options owned by the external validation host.</summary>
internal sealed record ScenarioValidationOptions(
    string AdbExecutable,
    string? Serial,
    string TargetApplication,
    string? VisionSocketPath,
    int DisplayWidth,
    int DisplayHeight,
    string? LaunchIntentAction,
    bool Slice2Proof = false,
    bool MultilevelProof = false,
    bool ScrollProof = false,
    bool CorpusProof = false)
{
    public const string SettingsRootLaunchIntentAction = "android.settings.SETTINGS";
    public const string DeveloperOptionsLaunchIntentAction = "com.android.settings.APPLICATION_DEVELOPMENT_SETTINGS";
    public const string DefaultWifiLaunchIntentAction = "android.settings.WIFI_SETTINGS";

    public static ScenarioValidationOptions Parse(string[] args)
    {
        var adb = "adb"; string? serial = null; var app = "com.android.settings";
        string? socket = null; var width = PhysicalHostOptions.DefaultDisplayWidth;
        var height = PhysicalHostOptions.DefaultDisplayHeight; string? intent = null;
        var slice2 = false; var multi = false; var scroll = false; var corpus = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--adb" when ++i < args.Length: adb = args[i]; break;
                case "--serial" when ++i < args.Length: serial = args[i]; break;
                case "--app" when ++i < args.Length: app = args[i]; break;
                case "--vision-socket" when ++i < args.Length: socket = args[i]; break;
                case "--width" when ++i < args.Length: width = int.Parse(args[i]); break;
                case "--height" when ++i < args.Length: height = int.Parse(args[i]); break;
                case "--launch-intent" when ++i < args.Length: intent = args[i]; break;
                case "--slice2": slice2 = true; break;
                case "--multilevel": multi = true; break;
                case "--scroll": scroll = true; break;
                case "--corpus": corpus = true; break;
                case "--execute": break;
                default: throw new FormatException($"Unknown validation option '{args[i]}'.");
            }
        }
        return new(adb, serial, app, socket, width, height, intent, slice2, multi, scroll, corpus);
    }

    public static implicit operator PhysicalHostOptions(ScenarioValidationOptions value) =>
        new(value.AdbExecutable, value.Serial, value.TargetApplication, value.VisionSocketPath,
            value.DisplayWidth, value.DisplayHeight, value.LaunchIntentAction);
}
