using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Adapters.Operator;

/// <summary>
/// Stateless translation boundary: already-authorized Runtime DeviceAction
/// → physical execution primitives.
///
/// The Operator receives ONLY lowered/authorized execution input.
/// It performs NO semantic capability selection, target identity
/// resolution, or goal completion adjudication.
///
/// Dispatch receipt (ActionResult) is a mechanism outcome only.
/// Dispatch != World Effect.
/// </summary>
public static class DeviceActionTranslator
{
    /// <summary>
    /// Translates a Runtime DeviceAction into an ADB execution descriptor.
    /// Returns null if the action cannot be translated (invalid target,
    /// unsupported action type, or invalid bounds).
    /// </summary>
    public static AdbOperation? Translate(
        DeviceAction action,
        int displayWidth,
        int displayHeight)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action switch
        {
            DeviceAction.LaunchApp launch => TranslateLaunch(launch),
            DeviceAction.Tap tap => TranslateTap(tap, displayWidth, displayHeight),
            DeviceAction.SetSwitch setSwitch => TranslateSetSwitch(setSwitch, displayWidth, displayHeight),
            DeviceAction.ScrollForward scroll => TranslateScroll(displayWidth, displayHeight),
            _ => null,
        };
    }

    private static AdbOperation? TranslateLaunch(DeviceAction.LaunchApp launch)
    {
        if (string.IsNullOrWhiteSpace(launch.ApplicationId))
            return null;

        return new AdbOperation.Launch(launch.ApplicationId, launch.LaunchIntentAction);
    }

    private static AdbOperation? TranslateTap(
        DeviceAction.Tap tap, int displayWidth, int displayHeight)
    {
        if (tap.TargetBounds is { IsValid: true } bounds)
        {
            var pixel = CoordinateMapper.ToPixelCenter(bounds, displayWidth, displayHeight);
            if (pixel is null) return null;
            return new AdbOperation.Tap(pixel.Value.X, pixel.Value.Y);
        }
        // Index-only tap (legacy compat): cannot translate without bounds
        return null;
    }

    private static AdbOperation? TranslateSetSwitch(
        DeviceAction.SetSwitch setSwitch, int displayWidth, int displayHeight)
    {
        // SetSwitch on a physical device is a tap at the switch location.
        // The DesiredValue semantic is handled by the Runtime (idempotent).
        // The Operator only needs to tap the switch.
        if (setSwitch.TargetBounds is { IsValid: true } bounds)
        {
            var pixel = CoordinateMapper.ToPixelCenter(bounds, displayWidth, displayHeight);
            if (pixel is null) return null;
            return new AdbOperation.Tap(pixel.Value.X, pixel.Value.Y);
        }
        return null;
    }

    private static AdbOperation? TranslateScroll(
        int displayWidth, int displayHeight)
    {
        // ScrollForward: swipe up from 70% to 30% of screen height
        int centerX = displayWidth / 2;
        int startY = (int)(displayHeight * 0.7);
        int endY = (int)(displayHeight * 0.3);

        return new AdbOperation.Swipe(centerX, startY, centerX, endY);
    }
}

/// <summary>
/// Execution descriptor — the physical operation the Operator must perform.
/// This is an adapter-internal type. It does NOT cross the Runtime boundary.
/// </summary>
public abstract record AdbOperation
{
    private AdbOperation() { }

    /// <summary>启动应用。</summary>
    /// <param name="PackageName">目标应用包名。</param>
    /// <param name="LaunchIntentAction">可选公开 intent action（机制级；null = 默认启动方式）。</param>
    public sealed record Launch(string PackageName, string? LaunchIntentAction = null) : AdbOperation;
    public sealed record Tap(int X, int Y) : AdbOperation;
    public sealed record Swipe(int X1, int Y1, int X2, int Y2) : AdbOperation;
    public sealed record KeyEvent(string KeyCode) : AdbOperation;
}
