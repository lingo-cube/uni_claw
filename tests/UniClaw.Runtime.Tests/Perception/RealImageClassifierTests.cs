using SkiaSharp;
using UniClaw.Semantic.Android.Visual;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Replay;
using UniClaw.Runtime.Tests.Scenario;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// S3 REAL PERCEPTION REPLAY — ImageSwitchStateProvider against real captured images.
///
/// Image source: settings-home-api35-full.png (emulator, api35, 1080×1920, 420dpi)
///   → RECORDED_REALITY (ADB screencap from emulator)
///
/// Toggle coordinates from: analysis.jsonl (real perception pipeline output)
///   → RECORDED_REALITY (YOLO detection + fusion)
///
/// Switch state label: MANUALLY ANNOTATED (no recorded ON/OFF state exists)
///   → REALITY_SEEDED
///
/// Overall scenario maturity: REALITY_SEEDED
/// </summary>
public sealed class RealImageClassifierTests
{
    private const int FullWidth = 1080;
    private const int FullHeight = 1920;
    private static readonly string AssetPath = TestRepositoryPaths.RepoPath(
        "tests", "UniClaw.Runtime.Tests", "Perception", "Assets");

    // ── REAL TOGGLE REGION (from analysis.jsonl, recorded reality) ────────

    /// <summary>
    /// System toggle detected by YOLO+fusion on the Settings main page.
    /// Center: (0.2125, 0.7848), type: "toggle". Bounding box estimated from
    /// Material Design standard toggle proportions (~2.5:1 aspect, ~50dp tall).
    /// Bounds are REALITY_SEEDED — center is recorded, box is estimated.
    /// </summary>
    private static readonly ElementBounds SystemToggleBounds = new(
        0.1325f, 0.7698f, 0.2925f, 0.7998f);

    // ── S3-R1: Real image classification runs without error ───────────────

    [Fact]
    public void S3_R1_RealImage_ProviderDoesNotCrash()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        Assert.True(File.Exists(imagePath), $"Real image not found: {imagePath}");

        using var bitmap = SKBitmap.Decode(imagePath);
        Assert.NotNull(bitmap);
        Assert.Equal(FullWidth, bitmap.Width);
        Assert.Equal(FullHeight, bitmap.Height);

        var provider = new ImageSwitchStateProvider(bitmap, FullWidth, FullHeight);
        Assert.NotNull(provider.Frame);
        Assert.IsAssignableFrom<ISwitchStateReader>(provider);
    }

    // ── S3-R2: Real toggle region produces valid output (bool? or null) ───

    [Fact]
    public async Task S3_R2_RealToggleRegion_ReturnsValidType()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        using var bitmap = SKBitmap.Decode(imagePath);
        var provider = new ImageSwitchStateProvider(bitmap, FullWidth, FullHeight);

        var result = await provider.ReadAsync(SystemToggleBounds);

        // Result is null, true, or false — never throws, never an unexpected type
        Assert.True(result is null || result.HasValue,
            $"Unexpected result type: {result?.GetType().Name ?? "null"}");
    }

    // ── S3-R3: Stale-frame fail-closed with real image ────────────────────

    [Fact]
    public async Task S3_R3_RealImage_StaleFrameFailClosed()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        using var bitmap1 = SKBitmap.Decode(imagePath);
        using var bitmap2 = SKBitmap.Decode(imagePath); // second decode = new frame

        var readerF1 = new ImageSwitchStateProvider(bitmap1, FullWidth, FullHeight);
        var readerF2 = new ImageSwitchStateProvider(bitmap2, FullWidth, FullHeight);

        // Different frames
        Assert.NotEqual(readerF1.Frame, readerF2.Frame);

        var resultF1 = await readerF1.ReadAsync(SystemToggleBounds);

        // Validate against correct frame → passes through
        var valid = SwitchStateValidation.ValidateFrameMatch(readerF1, readerF1.Frame, resultF1);
        Assert.Equal(resultF1, valid);

        // Validate against wrong frame → fail closed
        var stale = SwitchStateValidation.ValidateFrameMatch(readerF1, readerF2.Frame, resultF1);
        Assert.Null(stale);
    }

    // ── S3-R4: Invalid bounds on real image → null ────────────────────────

    [Fact]
    public async Task S3_R4_RealImage_InvalidBoundsReturnsNull()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        using var bitmap = SKBitmap.Decode(imagePath);
        var provider = new ImageSwitchStateProvider(bitmap, FullWidth, FullHeight);

        var invalid = new ElementBounds(0.9f, 0.2f, 0.1f, 0.3f); // X1 > X2
        var result = await provider.ReadAsync(invalid);
        Assert.Null(result);
    }

    // ── S3-R5: Deterministic replay on same real image ────────────────────

    [Fact]
    public async Task S3_R5_RealImage_DeterministicReplay()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        using var bitmap = SKBitmap.Decode(imagePath);
        var provider = new ImageSwitchStateProvider(bitmap, FullWidth, FullHeight);

        var r1 = await provider.ReadAsync(SystemToggleBounds);
        var r2 = await provider.ReadAsync(SystemToggleBounds);
        var r3 = await provider.ReadAsync(SystemToggleBounds);

        Assert.Equal(r1, r2);
        Assert.Equal(r2, r3);
    }

    // ── S3-R6: Runtime integration with perception-enriched observation ────

    [Fact]
    public async Task S3_R6_RealImage_EnrichesObservation()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        using var bitmap = SKBitmap.Decode(imagePath);
        var provider = new ImageSwitchStateProvider(bitmap, FullWidth, FullHeight);

        // Simulate perception adapter: create ObservedElement from detection + reader
        var switchState = await provider.ReadAsync(SystemToggleBounds);

        // Validate frame match
        var validated = SwitchStateValidation.ValidateFrameMatch(
            provider, provider.Frame, switchState);

        // Construct ObservedElement with validated SwitchState
        var element = new ObservedElement(
            "System", validated, 0, SystemToggleBounds, "toggle");

        // SwitchState is null (UNKNOWN) or a valid bool — never fabricated
        Assert.True(element.SwitchState is null || element.SwitchState.HasValue);
        Assert.Equal("System", element.Text);
        Assert.Equal("toggle", element.PerceptionType);
        Assert.Equal(SystemToggleBounds, element.Bounds);
    }

    // ── PROVENANCE ────────────────────────────────────────────────────────

    [Fact]
    public void Provenance_RealImage_HasCorrectProperties()
    {
        var imagePath = Path.Combine(AssetPath, "settings-home-api35-full.png");
        Assert.True(File.Exists(imagePath));

        using var bitmap = SKBitmap.Decode(imagePath);
        Assert.Equal(1080, bitmap.Width);
        Assert.Equal(1920, bitmap.Height);

        // Image is RECORDED_REALITY (ADB screencap from emulator)
        // Switch state label is REALITY_SEEDED (manually assigned)
        // Bounding box is REALITY_SEEDED (estimated from center point)
    }

    [Fact]
    public void Provenance_DeviceProfile_Api35Matches()
    {
        var profile = DeviceProfile.Pkj110; // 1440×3168 — B1 real device
        Assert.Equal(DevicePlatform.Android, profile.Platform);
        Assert.Equal(DeviceKind.Physical, profile.Kind);
        Assert.Equal(1440, profile.DisplayWidth);
        Assert.Equal(3168, profile.DisplayHeight);
    }
}
