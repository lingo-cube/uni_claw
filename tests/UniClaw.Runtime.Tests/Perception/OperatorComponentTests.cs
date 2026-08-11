using UniClaw.Runtime.Adapters.Operator;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// Operator component proofs O1-O8.
///
/// Proves: DeviceAction → AdbOperation translation, coordinate mapping,
/// invalid target fail-closed, no semantic capability selection,
/// dispatch ≠ effect, and IEnvironment sufficiency.
/// </summary>
public sealed class OperatorComponentTests
{
    private const int DisplayWidth = 1080;
    private const int DisplayHeight = 1920;

    // ── O1: TAP DISPATCH ─────────────────────────────────────────────────

    [Fact]
    public void O1_TapDispatch_TranslatesBoundsToPixelTap()
    {
        var bounds = new ElementBounds(0.5f, 0.5f, 0.6f, 0.6f);
        var action = new DeviceAction.Tap(0, bounds);

        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);

        var tap = Assert.IsType<AdbOperation.Tap>(op);
        // Center of bounds: (0.55 * 1080, 0.55 * 1920) = (594, 1056)
        Assert.True(tap.X > 500 && tap.X < 700);
        Assert.True(tap.Y > 1000 && tap.Y < 1200);
    }

    // ── O2: SETSWITCH TRANSLATION ────────────────────────────────────────

    [Fact]
    public void O2_SetSwitch_TranslatesToTapAtSwitchLocation()
    {
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);
        var action = new DeviceAction.SetSwitch(1, true, bounds);

        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);

        // SetSwitch → Tap (not Toggle — idempotent semantic handled by Runtime)
        var tap = Assert.IsType<AdbOperation.Tap>(op);
        Assert.True(tap.X > 800 && tap.X < 1000);
        Assert.True(tap.Y > 350 && tap.Y < 600);
    }

    // ── O3: COORDINATE TRANSLATION ───────────────────────────────────────

    [Fact]
    public void O3_CoordinateTranslation_B1GoldenMatch()
    {
        // B1 PKJ110: 1440×3168, switch at [1160,1251,1314,1346]
        var bounds = new ElementBounds(
            1160f / 1440f, 1251f / 3168f,
            1314f / 1440f, 1346f / 3168f);

        var pixel = CoordinateMapper.ToPixelCenter(bounds, 1440, 3168);
        Assert.NotNull(pixel);

        // Center should be near (1237, 1298)
        Assert.True(pixel.Value.X > 1200 && pixel.Value.X < 1280,
            $"X={pixel.Value.X} not in expected range");
        Assert.True(pixel.Value.Y > 1250 && pixel.Value.Y < 1350,
            $"Y={pixel.Value.Y} not in expected range");
    }

    [Fact]
    public void O3_CoordinateTranslation_ClampsToDisplay()
    {
        // Bounds outside normalized range
        var bounds = new ElementBounds(1.5f, 1.5f, 2.0f, 2.0f);
        Assert.False(bounds.IsValid); // X1 > 1 → invalid

        // Valid bounds at edge
        var edgeBounds = new ElementBounds(0.99f, 0.99f, 1.0f, 1.0f);
        Assert.True(edgeBounds.IsValid);

        var pixel = CoordinateMapper.ToPixelCenter(edgeBounds, 100, 100);
        Assert.NotNull(pixel);
        Assert.True(pixel.Value.X <= 99);
        Assert.True(pixel.Value.Y <= 99);
    }

    // ── O4: INVALID TARGET FAIL CLOSED ───────────────────────────────────

    [Fact]
    public void O4_InvalidBounds_FailClosed_NoTranslation()
    {
        var invalid = new ElementBounds(0.9f, 0.2f, 0.1f, 0.3f); // X1 > X2
        var action = new DeviceAction.Tap(0, invalid);

        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);
        Assert.Null(op); // fail closed — no dispatch
    }

    [Fact]
    public void O4_TapWithoutBounds_FailClosed()
    {
        // Legacy index-only tap (no spatial evidence)
        var action = new DeviceAction.Tap(0, TargetBounds: null);

        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);
        Assert.Null(op); // cannot translate without spatial evidence
    }

    [Fact]
    public void O4_LaunchWithoutAppId_FailClosed()
    {
        var action = new DeviceAction.LaunchApp(null);

        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);
        Assert.Null(op);
    }

    // ── O5: NO SEMANTIC DECISION ─────────────────────────────────────────

    [Fact]
    public void O5_Translator_HasNoSemanticObjectReference()
    {
        // DeviceActionTranslator must not reference SemanticObject, Capability,
        // or any business-level type.
        var translatorType = typeof(DeviceActionTranslator);
        var methods = translatorType.GetMethods();

        foreach (var method in methods)
        {
            var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToHashSet();
            Assert.DoesNotContain(typeof(SemanticObject), paramTypes);
            Assert.DoesNotContain(typeof(Capability), paramTypes);
        }
    }

    [Fact]
    public void O5_Translator_DoesNotSelectCapability()
    {
        // The translator only sees DeviceAction — not SemanticAction or Capability
        var translateMethod = typeof(DeviceActionTranslator).GetMethod("Translate")!;
        var paramTypes = translateMethod.GetParameters().Select(p => p.ParameterType).ToArray();

        Assert.Contains(typeof(DeviceAction), paramTypes);
        Assert.DoesNotContain(typeof(SemanticAction), paramTypes);
        Assert.DoesNotContain(typeof(Capability), paramTypes);
    }

    // ── O6: IENVIRONMENT SUFFICIENCY ─────────────────────────────────────

    [Fact]
    public void O6_IEnvironment_IsSufficient_NoNewPortNeeded()
    {
        // IEnvironment.ExecuteAsync(DeviceAction, ct) → Task<ActionResult>
        // already provides the complete Operator boundary.
        // The Operator is a mechanism domain behind IEnvironment.

        var executeMethod = typeof(UniClaw.Runtime.Environment.IEnvironment)
            .GetMethod("ExecuteAsync")!;

        var paramTypes = executeMethod.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(DeviceAction), paramTypes);
        Assert.Contains(typeof(CancellationToken), paramTypes);
        Assert.Equal(typeof(Task<ActionResult>), executeMethod.ReturnType);

        // No new Runtime port required — IEnvironment is the boundary
    }

    // ── O7: DISPATCH ≠ EFFECT ────────────────────────────────────────────

    [Fact]
    public void O7_ActionResult_DoesNotCarryWorldState()
    {
        // ActionResult only has Outcome + ActionDescription + Info
        // No world state, no SwitchState, no Observation
        var props = typeof(ActionResult).GetProperties()
            .Select(p => p.Name).ToHashSet();

        Assert.Contains("Outcome", props);
        Assert.Contains("ActionDescription", props);
        Assert.Contains("Info", props);
        Assert.Equal(3, props.Count); // no hidden world state fields
    }

    // ── O8: LAUNCH AND SCROLL TRANSLATION ─────────────────────────────────

    [Fact]
    public void O8_LaunchApp_TranslatesCorrectly()
    {
        var action = new DeviceAction.LaunchApp("com.android.settings");
        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);

        var launch = Assert.IsType<AdbOperation.Launch>(op);
        Assert.Equal("com.android.settings", launch.PackageName);
    }

    [Fact]
    public void O8_ScrollForward_TranslatesToSwipe()
    {
        var action = new DeviceAction.ScrollForward();
        var op = DeviceActionTranslator.Translate(action, DisplayWidth, DisplayHeight);

        var swipe = Assert.IsType<AdbOperation.Swipe>(op);
        Assert.Equal(DisplayWidth / 2, swipe.X1);
        Assert.Equal(DisplayWidth / 2, swipe.X2);
        Assert.True(swipe.Y1 > swipe.Y2, "Scroll up: start Y > end Y");
    }
}
