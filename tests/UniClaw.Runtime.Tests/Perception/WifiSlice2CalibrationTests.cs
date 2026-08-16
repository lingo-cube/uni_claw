using SkiaSharp;
using UniClaw.Runtime.Adapters.Perception.Vision;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Replay;
using UniClaw.Runtime.Tests.Scenario;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// SLICE 2 — 5.1 录制校准资产的确定性状态读取（Tier 3：真实分类器 + 录制帧）。
///
/// 资产: wifi-slice2-calibration/frames/{wifi-off,wifi-on}-emulator-5554.png
///       provenance.json (schema uniclaw.calibration.wifiOffToOnPair.v1, recordedAt 2026-08-14)
/// 设备: emulator-5554, AVD uniclaw-lite-api35, Android 15 API 35, 1080×1920@420dpi
/// 独立验证: adb shell settings get global wifi_on — OFF=0, ON=1（只读验证，非状态注入）。
/// 感知候选（vision server 输出，type=switch → toggle 规范化）:
///   OFF: bounds (0.834722,0.407031)-(0.958333,0.450781), center (968,824)
///   ON:  bounds (0.831944,0.407031)-(0.959722,0.451562), center (968,824)
///
/// 期望: OFF → ImageSwitchStateProvider.ReadAsync == false；ON → == true；确定性。
/// 资产为 test-side fixture data（无生产场景注入）。
/// </summary>
public sealed class WifiSlice2CalibrationTests
{
    private const int FullWidth = 1080;
    private const int FullHeight = 1920;

    private static readonly string CalibrationDir = Path.Combine(
        TestRepositoryPaths.RepoPath("tests", "UniClaw.Runtime.Tests", "Perception", "Assets"),
        "wifi-slice2-calibration");

    private static readonly string FramesDir = Path.Combine(CalibrationDir, "frames");
    private static readonly string PerceptionDir = Path.Combine(CalibrationDir, "perception");

    /// <summary>录制 OFF 帧感知候选 bounds（vision server yolo det_1 / candidate_8）。</summary>
    private static readonly ElementBounds RecordedOffSwitchBounds = new(0.834722f, 0.407031f, 0.958333f, 0.450781f);

    /// <summary>录制 ON 帧感知候选 bounds（vision server yolo det_1 / candidate_8）。</summary>
    private static readonly ElementBounds RecordedOnSwitchBounds = new(0.831944f, 0.407031f, 0.959722f, 0.451562f);

    private static async Task<bool?> ReadStateAsync(string frameFile, ElementBounds bounds)
    {
        var path = Path.Combine(FramesDir, frameFile);
        Assert.True(File.Exists(path), $"frame not found: {path}");
        using var bitmap = SKBitmap.Decode(path);
        Assert.Equal(FullWidth, bitmap.Width);
        Assert.Equal(FullHeight, bitmap.Height);
        var provider = new ImageSwitchStateProvider(bitmap, FullWidth, FullHeight);
        return await provider.ReadAsync(bounds);
    }

    // ── Tier3: 确定性 OFF→false / ON→true ────────────────────────────────

    [Fact]
    public async Task Tier3_RecordedOffFrame_SwitchStateIsFalse()
    {
        var state = await ReadStateAsync("wifi-off-emulator-5554.png", RecordedOffSwitchBounds);
        Assert.False(state, $"OFF 帧在录制 bounds 下应读得 false，实际 {state?.ToString() ?? "null"}");
    }

    [Fact]
    public async Task Tier3_RecordedOnFrame_SwitchStateIsTrue()
    {
        var state = await ReadStateAsync("wifi-on-emulator-5554.png", RecordedOnSwitchBounds);
        Assert.True(state, $"ON 帧在录制 bounds 下应读得 true，实际 {state?.ToString() ?? "null"}");
    }

    [Fact]
    public async Task Tier3_RecordedPair_DeterministicAcrossRepeatedReads()
    {
        var off1 = await ReadStateAsync("wifi-off-emulator-5554.png", RecordedOffSwitchBounds);
        var off2 = await ReadStateAsync("wifi-off-emulator-5554.png", RecordedOffSwitchBounds);
        var on1 = await ReadStateAsync("wifi-on-emulator-5554.png", RecordedOnSwitchBounds);
        var on2 = await ReadStateAsync("wifi-on-emulator-5554.png", RecordedOnSwitchBounds);

        Assert.Equal(off1, off2);
        Assert.Equal(on1, on2);
        Assert.False(off1);
        Assert.True(on1);
    }

    /// <summary>两个录制帧的 switch 中心同为 (968,824)（0.895833,0.428906）— 状态变化不改变几何。</summary>
    [Fact]
    public void Tier3_RecordedPair_SwitchGeometryStableAcrossStates()
    {
        Assert.Equal(RecordedOffSwitchBounds.X2 - RecordedOffSwitchBounds.X1, RecordedOnSwitchBounds.X2 - RecordedOnSwitchBounds.X1, precision: 1);
        Assert.Equal(RecordedOffSwitchBounds.Y1, RecordedOnSwitchBounds.Y1, precision: 2);
        Assert.Equal(RecordedOffSwitchBounds.Y2, RecordedOnSwitchBounds.Y2, precision: 2);
    }

    // ── 5.1 资产完备性（provenance 必须存在且为录制对 schema）────────────

    [Fact]
    public void Calibration_Provenance_ExistsWithRecordedPairSchema()
    {
        var provenancePath = Path.Combine(CalibrationDir, "provenance.json");
        Assert.True(File.Exists(provenancePath), $"provenance.json not found: {provenancePath}");

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(provenancePath));
        var root = doc.RootElement;
        Assert.Equal("uniclaw.calibration.wifiOffToOnPair.v1", root.GetProperty("schema").GetString());

        var profile = root.GetProperty("device").GetProperty("profile");
        Assert.Equal("Android", profile.GetProperty("platform").GetString());
        Assert.Equal("Emulator", profile.GetProperty("type").GetString());
        Assert.Equal(1080, profile.GetProperty("width").GetInt32());
        Assert.Equal(1920, profile.GetProperty("height").GetInt32());
        Assert.Equal(35, profile.GetProperty("apiLevel").GetInt32());

        // 序列: OFF →(物理 tap 开关中心 968,824)→ ON，每步只读 wifi_on 独立验证
        var sequence = root.GetProperty("sequence");
        Assert.Equal(2, sequence.GetArrayLength());
        Assert.Equal("OFF", sequence[0].GetProperty("state").GetString());
        Assert.Equal("0", sequence[0].GetProperty("verification").GetProperty("observed").GetString());
        Assert.Equal("ON", sequence[1].GetProperty("state").GetString());
        Assert.Equal("1", sequence[1].GetProperty("verification").GetProperty("observed").GetString());
        Assert.Contains("physical tap", sequence[1].GetProperty("action").GetString());

        // 感知证据映射: OFF→false, ON→true
        Assert.False(root.GetProperty("evidenceMapping").GetProperty("OFF").GetProperty("expectedBelief").GetBoolean());
        Assert.True(root.GetProperty("evidenceMapping").GetProperty("ON").GetProperty("expectedBelief").GetBoolean());

        // 资产仅测试侧（无生产场景注入声明）
        Assert.Contains("Test-side calibration asset only", root.GetProperty("source").GetString());
    }

    /// <summary>感知 JSON 必须与帧共存（vision server 输出即录制证据链的感知侧）。</summary>
    [Fact]
    public void Calibration_PerceptionOutputs_CoexistWithFrames()
    {
        foreach (var name in new[] { "wifi-off-emulator-5554", "wifi-on-emulator-5554" })
        {
            Assert.True(File.Exists(Path.Combine(FramesDir, name + ".png")), $"{name}.png missing");
            Assert.True(File.Exists(Path.Combine(PerceptionDir, name + ".json")), $"{name}.json missing");
        }
    }

    /// <summary>fixture 使用的录制开关 bounds 与校准感知候选一致（四舍五入容差）。</summary>
    [Fact]
    public void Calibration_FixtureBounds_ConsistentWithRecordedCandidates()
    {
        var fixture = RealitySeededSettingsFixture.RecordedWifiSwitchBounds;
        Assert.Equal(RecordedOnSwitchBounds.X1, fixture.X1, precision: 3);
        Assert.Equal(RecordedOnSwitchBounds.Y1, fixture.Y1, precision: 3);
        Assert.Equal(RecordedOnSwitchBounds.X2, fixture.X2, precision: 3);
        Assert.Equal(RecordedOnSwitchBounds.Y2, fixture.Y2, precision: 3);
    }

    // ── 合成主题覆盖（保护分类器启发式：白 knob 亮主题 + 旧暗 knob 主题）──────

    private const int SyntheticW = 100;
    private const int SyntheticH = 40;

    /// <summary>构造合成开关位图：track 填充 trackLum，knob（lum）占据左/右侧 40% 宽度。</summary>
    private static SKBitmap BuildSyntheticSwitch(byte trackR, byte trackG, byte trackB, byte knobR, byte knobG, byte knobB, bool knobRight)
    {
        var bitmap = new SKBitmap(SyntheticW, SyntheticH);
        int knobStart = knobRight ? (int)(SyntheticW * 0.6) : 0;
        int knobEnd = knobRight ? SyntheticW : (int)(SyntheticW * 0.4);
        for (int y = 0; y < SyntheticH; y++)
        {
            for (int x = 0; x < SyntheticW; x++)
            {
                var (r, g, b) = x >= knobStart && x < knobEnd ? (knobR, knobG, knobB) : (trackR, trackG, trackB);
                bitmap.SetPixel(x, y, new SKColor(r, g, b));
            }
        }
        return bitmap;
    }

    [Fact]
    public async Task Synthetic_Android15LightTheme_WhiteKnobLeftOnGrayTrack_IsFalse()
    {
        // 5.1 录制现实 OFF 帧配色: 灰 track (228,227,233) + 深灰 knob (117,119,128) 于左侧
        using var bitmap = BuildSyntheticSwitch(228, 227, 233, 117, 119, 128, knobRight: false);
        var provider = new ImageSwitchStateProvider(bitmap, SyntheticW, SyntheticH);
        Assert.False(await provider.ReadAsync(new ElementBounds(0f, 0f, 1f, 1f)));
    }

    [Fact]
    public async Task Synthetic_Android15LightTheme_WhiteKnobRightOnTealTrack_IsTrue()
    {
        using var bitmap = BuildSyntheticSwitch(73, 93, 146, 255, 255, 255, knobRight: true);
        var provider = new ImageSwitchStateProvider(bitmap, SyntheticW, SyntheticH);
        Assert.True(await provider.ReadAsync(new ElementBounds(0f, 0f, 1f, 1f)));
    }

    [Fact]
    public async Task Synthetic_DarkTheme_DarkKnobLeftOnLightTrack_IsFalse()
    {
        using var bitmap = BuildSyntheticSwitch(210, 210, 210, 40, 40, 40, knobRight: false);
        var provider = new ImageSwitchStateProvider(bitmap, SyntheticW, SyntheticH);
        Assert.False(await provider.ReadAsync(new ElementBounds(0f, 0f, 1f, 1f)));
    }

    [Fact]
    public async Task Synthetic_DarkTheme_DarkKnobRightOnLightTrack_IsTrue()
    {
        using var bitmap = BuildSyntheticSwitch(210, 210, 210, 40, 40, 40, knobRight: true);
        var provider = new ImageSwitchStateProvider(bitmap, SyntheticW, SyntheticH);
        Assert.True(await provider.ReadAsync(new ElementBounds(0f, 0f, 1f, 1f)));
    }

    [Fact]
    public async Task Synthetic_UniformTrack_NoKnob_FailsClosedNull()
    {
        using var bitmap = BuildSyntheticSwitch(228, 227, 233, 228, 227, 233, knobRight: false);
        var provider = new ImageSwitchStateProvider(bitmap, SyntheticW, SyntheticH);
        Assert.Null(await provider.ReadAsync(new ElementBounds(0f, 0f, 1f, 1f)));
    }
}
