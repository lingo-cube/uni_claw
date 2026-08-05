using UniClaw.Core.UniBrain;
using UniClaw.Host.Tests.Integration;
using Xunit;

namespace UniClaw.Host.Tests.Device;

/// <summary>
/// 最小粒度 ADB 真机/模拟器操作集成测试。默认跳过（IntegrationFact）。
/// 运行前启动设备：scripts/android-emulator.sh start；可设 UNICLAW_ADB_SERIAL 指定串行。
/// 运行：
/// <code>
/// UNICLAW_INTEGRATION_SCOPES=adb-read dotnet test tests/UniClaw.Host.Tests --filter "IntegrationScope=adb-read"
/// </code>
/// UIA 层级相关的 ScreenState 场景已随 UIA 移除 (delete-uia)；
/// 保留截图与 raw 帧缓冲往返验证。
/// </summary>
[Trait("Category", "Integration")]
public sealed class AdbRealDeviceIntegrationTests
{
    [Trait("IntegrationScope", IntegrationTestScopes.AdbConnectivity)]
    [IntegrationFact(IntegrationTestScopes.AdbConnectivity)]
    public async Task Devices_ResolvesOnlineSerial()
    {
        var serial = await AdbTestContext.ResolveSerialAsync();

        Assert.False(string.IsNullOrWhiteSpace(serial));
        // 用 runner 再确认串行可达（devices 解析本身已校验在线状态）。
        var context = await AdbTestContext.CreateAsync("adb-devices");
        Assert.Equal(serial, context.Serial);
        await context.WriteArtifactAsync("serial.txt", serial);
    }

    [Trait("IntegrationScope", IntegrationTestScopes.AdbReadOnly)]
    [IntegrationFact(IntegrationTestScopes.AdbReadOnly)]
    public async Task Screencap_ReturnsDecodablePng()
    {
        var context = await AdbTestContext.CreateAsync("adb-screencap");

        var bytes = await context.Capture.CaptureAsync();

        // PNG 魔数 + 非空尺寸，证明 exec-out screencap 往返可用。
        Assert.True(bytes.Length > 10_000, $"截图过小: {bytes.Length} bytes");
        Assert.Equal(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            bytes.Take(8).ToArray());
        await context.WriteArtifactAsync("screenshot.png", bytes);
    }

    [Trait("IntegrationScope", IntegrationTestScopes.AdbReadOnly)]
    [IntegrationFact(IntegrationTestScopes.AdbReadOnly)]
    public async Task ScreencapRaw_ReturnsValidRgbaBuffer()
    {
        var context = await AdbTestContext.CreateAsync("adb-screencap-raw");

        var raw = await context.Capture.CaptureRawAsync();

        Assert.Equal(1, raw.PixelFormat); // RGBA_8888
        Assert.True(raw.Width > 0, $"Width <= 0: {raw.Width}");
        Assert.True(raw.Height > 0, $"Height <= 0: {raw.Height}");
        Assert.Equal(raw.Width * raw.Height * 4, raw.Pixels.Length);

        // Verify we can round-trip: raw → JPEG → decode → valid image
        var jpeg = ImageResizer.ProcessRaw(raw);
        Assert.True(jpeg.Length > 1000, $"JPEG too small: {jpeg.Length} bytes");
        // JPEG magic bytes
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);

        await context.WriteArtifactAsync("screenshot-raw.jpg", jpeg);
    }
}
