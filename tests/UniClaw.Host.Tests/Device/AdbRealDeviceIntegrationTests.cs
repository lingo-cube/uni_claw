using System.Text.RegularExpressions;
using System.Xml.Linq;
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
/// 每个场景以 Settings 首页为固定起点，证据写入 artifacts/runs/integration/adb/。
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
    public async Task LaunchSettings_ScreenStateParses()
    {
        var context = await AdbTestContext.CreateAsync("adb-launch");

        await context.Actions.LaunchPackageAsync("com.android.settings");
        await context.Actions.WaitAsync(2000);

        var state = await context.ScreenState.RefreshAsync();
        Assert.True(state.Succeeded, $"ScreenState 解析失败: {state.Failure?.Kind}");
        await context.WriteArtifactAsync("settings-home.xml", state.HierarchyXml ?? "");
    }

    [Trait("IntegrationScope", IntegrationTestScopes.AdbAction)]
    [IntegrationFact(IntegrationTestScopes.AdbAction)]
    public async Task LocateSafeNavigationRow_TapNavigates_BackRestores()
    {
        var context = await AdbTestContext.CreateAsync("adb-wifi-navigate");

        await context.Actions.LaunchPackageAsync("com.android.settings");
        await context.Actions.WaitAsync(2000);
        var home = await context.ScreenState.RefreshAsync();
        Assert.True(home.Succeeded, $"Settings 首页解析失败: {home.Failure?.Kind}");

        string[] safeRows = [
            "Network & internet",
            "Connected devices",
            "Wi-Fi",
            "WiFi",
        ];
        var center = FindTextCenter(home.HierarchyXml ?? "", safeRows);
        Assert.True(
            center is not null,
            "UIAutomator 中未找到约定的安全导航行（检查设备语言/版本，见 docs/testing/integration-tests.md）");

        var (x, y) = center!.Value;
        var tapped = await context.Actions.TapAsync(x, y);
        Assert.True(tapped, "tap 动作执行失败");
        await context.Actions.WaitAsync(1500);

        var afterTap = await context.ScreenState.RefreshAsync();
        Assert.True(afterTap.Succeeded, $"点击后页面解析失败: {afterTap.Failure?.Kind}");
        Assert.NotEqual(home.HierarchyFingerprint, afterTap.HierarchyFingerprint);
        await context.WriteArtifactAsync("after-tap.xml", afterTap.HierarchyXml ?? "");

        var back = await context.Actions.PressBackAsync();
        Assert.True(back, "back 动作执行失败");
        await context.Actions.WaitAsync(1500);

        var restored = await context.ScreenState.RefreshAsync();
        Assert.True(restored.Succeeded, $"返回后页面解析失败: {restored.Failure?.Kind}");
        Assert.NotEqual(afterTap.HierarchyFingerprint, restored.HierarchyFingerprint);
        Assert.True(
            FindTextCenter(restored.HierarchyXml ?? "", safeRows) is not null,
            "返回后 Settings 首页未恢复（安全导航行缺失）");
        await context.WriteArtifactAsync("restored.xml", restored.HierarchyXml ?? "");
    }

    /// <summary>在 UIAutomator XML 中按 text/content-desc 找目标行中心（归一化坐标）。</summary>
    private static (double X, double Y)? FindTextCenter(string xml, string[] names)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        var root = document.Root;
        if (root is null) return null;

        // UIAutomator's <hierarchy> root has no bounds. Use the largest valid
        // node rectangle as the physical screen instead of assuming a root attr.
        var screen = document.Descendants("node")
            .Select(node => ParseBounds((string?)node.Attribute("bounds")))
            .Where(bounds => bounds is not null)
            .Select(bounds => bounds!.Value)
            .Where(bounds => bounds.X2 > bounds.X1 && bounds.Y2 > bounds.Y1)
            .OrderByDescending(bounds =>
                (long)(bounds.X2 - bounds.X1) * (bounds.Y2 - bounds.Y1))
            .FirstOrDefault();
        if (screen.X2 <= 0 || screen.Y2 <= 0) return null;

        var target = document.Descendants("node")
            .FirstOrDefault(node => names.Any(name =>
                TextMatches((string?)node.Attribute("text"), name)
                || TextMatches((string?)node.Attribute("content-desc"), name)));
        if (target is null) return null;

        var bounds = ParseBounds((string?)target.Attribute("bounds"));
        if (bounds is null) return null;

        return (
            (bounds.Value.X1 + bounds.Value.X2) / 2.0 / screen.X2,
            (bounds.Value.Y1 + bounds.Value.Y2) / 2.0 / screen.Y2);
    }

    private static bool TextMatches(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;
        var normalized = string.Concat(actual.Where(char.IsLetterOrDigit))
            .ToLowerInvariant();
        var expectedNormalized = string.Concat(expected.Where(char.IsLetterOrDigit))
            .ToLowerInvariant();
        return normalized.Contains(expectedNormalized, StringComparison.Ordinal)
               || expectedNormalized.Contains(normalized, StringComparison.Ordinal);
    }

    private static (int X1, int Y1, int X2, int Y2)? ParseBounds(string? bounds)
    {
        if (string.IsNullOrWhiteSpace(bounds)) return null;
        var match = Regex.Match(
            bounds,
            @"\[\s*(\d+)\s*,\s*(\d+)\s*\]\[\s*(\d+)\s*,\s*(\d+)\s*\]");
        if (!match.Success) return null;
        return (
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value),
            int.Parse(match.Groups[4].Value));
    }
}
