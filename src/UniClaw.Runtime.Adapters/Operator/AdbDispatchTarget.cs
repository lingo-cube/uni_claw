using UniClaw.Runtime.Adapters.Device;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Operator;

/// <summary>Dispatches already-lowered ADB operations; a success is never world-effect evidence.</summary>
public sealed class AdbDispatchTarget : IAdbDispatchTarget
{
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(10);
    private readonly IAdbProcessRunner _runner;
    private readonly string _adbExecutable;
    private readonly string _serial;

    public AdbDispatchTarget(string serial, string adbExecutable = "adb")
        : this(new AdbProcessRunner(), serial, adbExecutable) { }

    internal AdbDispatchTarget(IAdbProcessRunner runner, string serial, string adbExecutable)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _serial = string.IsNullOrWhiteSpace(serial) ? throw new ArgumentException("Resolved device serial is required.", nameof(serial)) : serial;
        _adbExecutable = string.IsNullOrWhiteSpace(adbExecutable) ? throw new ArgumentException("ADB executable is required.", nameof(adbExecutable)) : adbExecutable;
    }

    public async Task<ActionResult> ExecuteAsync(AdbOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var command = BuildCommand(operation);
        if (command is null)
            return new(ActionResultOutcome.Rejected, operation.GetType().Name, "Unsupported or invalid ADB operation.");

        var result = await _runner.RunAsync(_adbExecutable, ["-s", _serial, .. command], DispatchTimeout, cancellationToken);
        if (result.TimedOut)
            return new(ActionResultOutcome.TimedOut, Describe(operation), "ADB dispatch timed out; world effect is unknown.");
        if (!result.Started || result.ExitCode != 0)
            return new(ActionResultOutcome.Rejected, Describe(operation), result.FailureReason ?? result.StandardError);
        return new(ActionResultOutcome.Dispatched, Describe(operation), "ADB command was issued; world effect is unverified.");
    }

    internal static IReadOnlyList<string>? BuildCommand(AdbOperation operation) => operation switch
    {
        // Launch intent action (public Settings deep link, e.g. android.settings.WIFI_SETTINGS):
        // deterministic landing page for the semantic loop. Mechanism-level only — the provider
        // never interprets WiFi semantics/goals/success (dispatch receipt ≠ world effect).
        AdbOperation.Launch launch when !string.IsNullOrWhiteSpace(launch.LaunchIntentAction)
            => ["shell", "am", "start", "-a", launch.LaunchIntentAction],
        AdbOperation.Launch launch when !string.IsNullOrWhiteSpace(launch.PackageName) => ["shell", "monkey", "-p", launch.PackageName, "1"],
        AdbOperation.Tap tap when tap.X >= 0 && tap.Y >= 0 => ["shell", "input", "tap", tap.X.ToString(System.Globalization.CultureInfo.InvariantCulture), tap.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)],
        AdbOperation.Swipe swipe when swipe.X1 >= 0 && swipe.Y1 >= 0 && swipe.X2 >= 0 && swipe.Y2 >= 0 => ["shell", "input", "swipe", swipe.X1.ToString(System.Globalization.CultureInfo.InvariantCulture), swipe.Y1.ToString(System.Globalization.CultureInfo.InvariantCulture), swipe.X2.ToString(System.Globalization.CultureInfo.InvariantCulture), swipe.Y2.ToString(System.Globalization.CultureInfo.InvariantCulture)],
        // KEY EVENT: RESTRICTIVE allow-list — only the Android SYSTEM BACK key
        // (keycode 4) is authorized (the EBD SystemBack primitive). Any other
        // key code (e.g. "HOME") is NOT translated → rejected at the adapter
        // boundary before any process execution.
        AdbOperation.KeyEvent keyEvent when keyEvent.KeyCode == "4" => ["shell", "input", "keyevent", keyEvent.KeyCode],
        _ => null,
    };

    private static string Describe(AdbOperation operation) => operation switch
    {
        AdbOperation.Launch launch => "launch " + launch.PackageName + (string.IsNullOrWhiteSpace(launch.LaunchIntentAction) ? "" : $" (-a {launch.LaunchIntentAction})"),
        AdbOperation.Tap tap => $"tap {tap.X},{tap.Y}",
        AdbOperation.Swipe swipe => $"swipe {swipe.X1},{swipe.Y1}→{swipe.X2},{swipe.Y2}",
        AdbOperation.KeyEvent keyEvent => $"keyevent {keyEvent.KeyCode}",
        _ => operation.GetType().Name,
    };
}
