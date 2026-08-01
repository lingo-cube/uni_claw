using UniClaw.Core.Domain.Models.Content;
using UniClaw.Host.Commands;
using Xunit;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// 视觉 + ADB 的最小闭环：真实模型从模拟器当前截图定位一个明确的安全导航行，
/// ADB 点击后验证页面变化，再 Back 恢复。它位于原始 ADB 边界与完整场景之间。
/// </summary>
[Trait("Category", "Integration")]
public sealed class AdbVisionActionIntegrationTests
{
    [Trait("IntegrationScope", IntegrationTestScopes.AdbVisionAction)]
    [IntegrationFact(IntegrationTestScopes.AdbVisionAction)]
    public async Task VisionLocatesSafeSettingsRow_AdbNavigatesAndRestores()
    {
        var context = await AdbTestContext.CreateAsync("adb-vision-action");
        await context.Actions.LaunchPackageAsync("com.android.settings");
        await context.Actions.WaitAsync(2000);

        var before = await context.ScreenState.RefreshAsync();
        Assert.True(before.Succeeded, $"Settings 首页解析失败: {before.Failure?.Kind}");

        var provider = Environment.GetEnvironmentVariable(
                           "UNICLAW_INTEGRATION_PROVIDER")
                       ?? "sensenova";
        var model = Environment.GetEnvironmentVariable(
                        "UNICLAW_INTEGRATION_MODEL")
                    ?? "sensenova-6.7-flash-lite";
        var report = await new HostCompositionFactory()
            .CreateAnalyzer(new HostCommandOptions(
                "analyze",
                context.Serial,
                context.ArtifactRoot,
                provider,
                model,
                "direct"))
            .AnalyzeAsync();

        Assert.Equal(0, report.DeviceActionsSent);
        var target = FindSafeNavigation(report.Analysis.Items);
        Assert.NotNull(target);
        Assert.Equal(ExpectedAction.Navigate, target!.ExpectedAction);

        var tapped = await context.Actions.TapAsync(
            target.Coordinate.X,
            target.Coordinate.Y);
        Assert.True(tapped, $"ADB 未能点击视觉目标 '{target.Name}'");
        await context.Actions.WaitAsync(1500);

        var after = await context.ScreenState.RefreshAsync();
        Assert.True(after.Succeeded, $"点击后页面解析失败: {after.Failure?.Kind}");
        Assert.NotEqual(before.HierarchyFingerprint, after.HierarchyFingerprint);
        await context.WriteArtifactAsync("after-vision-tap.xml", after.HierarchyXml ?? "");

        Assert.True(await context.Actions.PressBackAsync(), "ADB back 动作失败");
        await context.Actions.WaitAsync(1500);
        var restored = await context.ScreenState.RefreshAsync();
        Assert.True(restored.Succeeded, $"返回后页面解析失败: {restored.Failure?.Kind}");
        Assert.NotEqual(after.HierarchyFingerprint, restored.HierarchyFingerprint);
        await context.WriteArtifactAsync("restored.xml", restored.HierarchyXml ?? "");
    }

    private static MenuItem? FindSafeNavigation(
        IEnumerable<MenuItem> items)
    {
        string[] aliases = ["Wi-Fi", "WiFi", "WLAN", "Network & internet"];
        return items.FirstOrDefault(item =>
            item.ExpectedAction == ExpectedAction.Navigate
            && aliases.Any(alias =>
                Normalize(item.Name).Contains(
                    Normalize(alias),
                    StringComparison.Ordinal)));
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
