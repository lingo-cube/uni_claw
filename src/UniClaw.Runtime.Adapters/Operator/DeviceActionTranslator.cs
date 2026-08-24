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
            DeviceAction.ScrollForward scroll => TranslateScroll(scroll.StepFraction, displayWidth, displayHeight),
            DeviceAction.ScrollBackward scroll => TranslateScrollBackward(scroll.StepFraction, displayWidth, displayHeight),
            DeviceAction.SystemBack => new AdbOperation.KeyEvent("4"),
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
        float stepFraction, int displayWidth, int displayHeight)
    {
        // ScrollForward: swipe up from 70% to 30% of screen height (full step).
        // StepFraction scales the swipe DISTANCE around the screen center so a
        // smaller fraction yields a shorter scroll while staying centered —
        // pure mechanism scaling, no semantic/page/scenario knowledge.
        float fraction = NormalizeStepFraction(stepFraction);
        int centerX = displayWidth / 2;
        float centerY = displayHeight * 0.5f;
        float halfDistance = displayHeight * 0.2f * fraction; // full step = 70%→30% (±20%)
        int startY = (int)(centerY + halfDistance);
        int endY = (int)(centerY - halfDistance);
        float distance = displayHeight * 0.4f * fraction; // full swipe length

        // ── SCROLL EXECUTION PROFILE ──
        // StepFraction remains the SEMANTIC scroll amount; the physical DURATION
        // is derived from the actual distance so the swipe VELOCITY stays capped
        // (high-speed flings blur the frame and degrade OCR — real-device
        // evidence). distance ∝ duration keeps the velocity bounded for every
        // step; a floor keeps tiny steps from becoming sub-reliable flicks.
        int duration = ComputeScrollDurationMs(distance);
        return new AdbOperation.Swipe(centerX, startY, centerX, endY, duration);
    }

    private static AdbOperation? TranslateScrollBackward(
        float stepFraction, int displayWidth, int displayHeight)
    {
        // ScrollBackward: swipe DOWN from 30% to 70% of screen height — the exact
        // mirror of ScrollForward (content moves back toward earlier sources),
        // scaled by StepFraction around the screen center.
        float fraction = NormalizeStepFraction(stepFraction);
        int centerX = displayWidth / 2;
        float centerY = displayHeight * 0.5f;
        float halfDistance = displayHeight * 0.2f * fraction;
        int startY = (int)(centerY - halfDistance);
        int endY = (int)(centerY + halfDistance);
        float distance = displayHeight * 0.4f * fraction;

        int duration = ComputeScrollDurationMs(distance);
        return new AdbOperation.Swipe(centerX, startY, centerX, endY, duration);
    }

    /// <summary>
    /// SCROLL EXECUTION PROFILE — distance-proportional swipe duration with a
    /// capped velocity (~900 px/s) and a minimum duration floor (tiny steps stay
    /// reliable physical flicks, never sub-200ms jabs). Mechanism-only: the
    /// semantic StepFraction is unchanged; only the physical execution timing
    /// is derived. Diagnostic fields (distance/duration/velocity) are exposed
    /// for observability only — they never participate in decisions.
    /// </summary>
    private const float ScrollVelocityCapPxPerMs = 0.9f; // ~900 px/s upper bound
    private const int MinScrollDurationMs = 200;

    internal static int ComputeScrollDurationMs(float distancePx)
        => Math.Max(MinScrollDurationMs, (int)Math.Ceiling(distancePx / ScrollVelocityCapPxPerMs));

    internal static (float DistancePx, int DurationMs, float VelocityPxPerMs) ScrollProfile(
        float stepFraction, int displayHeight)
    {
        float fraction = NormalizeStepFraction(stepFraction);
        float distance = displayHeight * 0.4f * fraction;
        int duration = ComputeScrollDurationMs(distance);
        return (distance, duration, distance / duration);
    }

    /// <summary>Bounded step-fraction normalization: (0,∞) input, clamped to a
    /// sane physical range so a degenerate fraction can never produce a
    /// zero-length or inverted swipe. Mechanism-only; no semantic meaning.</summary>
    private static float NormalizeStepFraction(float stepFraction)
        => Math.Clamp(stepFraction, 0.1f, 2.0f);
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

    /// <summary>Swipe with an optional explicit DURATION (ms). Null keeps the
    /// historical adb default (300ms); a value expresses the Scroll Execution
    /// Profile's distance-proportional duration (velocity-capped).</summary>
    public sealed record Swipe(int X1, int Y1, int X2, int Y2, int? Duration = null) : AdbOperation;
    public sealed record KeyEvent(string KeyCode) : AdbOperation;
}
