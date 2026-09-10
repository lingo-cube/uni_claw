using System.Diagnostics;
using System.Xml.Linq;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.World;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ADB-002 ENVIRONMENT 验收 —— 真实模拟器端到端：SpatialLocator（真实
/// bounds）→ AdbLiveEffectDriver → 真实 input tap → 世界变化经再观察
/// （uiautomator dump）证实。默认显式跳过（DSH_TEST_ADB_ENV 未启用 =
/// 本测试体不执行，零 adb 调用）；<c>DSH_TEST_ADB_ENV=1</c> 启用，启用后
/// 按 legacy Tier-2 约定 fail-closed（前置缺失 = FAIL，不静默）。
/// 环境事实见 docs/agents/test-emulator.md（注册测试常用模拟器）：
/// AVD p26_pixel、serial emulator-5554、Wi-Fi Settings 入口。
/// I-3 活教材：Wi-Fi toggle 非幂等——checked=true 时盲 tap 即破坏为 false
/// （CDS-001 satisfaction 闸的物理必然性，本测试第一段翻转就是它的展示）。
/// </summary>
public sealed class AdbEnvironmentTests
{
    private const string Serial = "emulator-5554";

    private static bool Enabled =>
        Environment.GetEnvironmentVariable("DSH_TEST_ADB_ENV") == "1";

    private readonly ITestOutputHelper _output;

    public AdbEnvironmentTests(ITestOutputHelper output) => _output = output;

    private static string Adb(params string[] args)
    {
        var info = new ProcessStartInfo("adb", string.Join(' ', args))
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(15000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"adb {string.Join(' ', args)} 失败：{process.StandardError.ReadToEnd()}");
        return stdout.Trim();
    }

    private static (bool Checked, (int X1, int Y1, int X2, int Y2) Bounds) DumpWifiSwitch()
    {
        Adb("-s", Serial, "shell", "uiautomator", "dump", "/sdcard/wifi.xml");
        var xml = Adb("-s", Serial, "shell", "cat", "/sdcard/wifi.xml");
        var doc = XDocument.Parse(xml);
        var sw = doc.Descendants("node")
            .FirstOrDefault(n => n.Attribute("checkable")?.Value == "true")
            ?? throw new InvalidOperationException("dump 中未找到 checkable 节点（Wi-Fi 页未打开？）");
        var b = sw.Attribute("bounds")!.Value; // "[901,834][1038,960]"
        var nums = b.Split(new[] { '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse).ToArray();
        return (sw.Attribute("checked")!.Value == "true", (nums[0], nums[1], nums[2], nums[3]));
    }

    private static (int W, int H) Viewport()
    {
        var size = Adb("-s", Serial, "shell", "wm", "size"); // "Physical size: 1080x2400"
        var m = System.Text.RegularExpressions.Regex.Match(size, @"(\d+)x(\d+)");
        return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    [Fact]
    public void Env_RealTapFlipsWorldState_ObservedByRedump()
    {
        // V2（REVIEW 修复）：未启用 = 测试体不执行（零 adb 调用、零环境
        // 依赖）；启用 = fail-closed（adb 前置缺失直接 FAIL，不静默）
        if (!Enabled)
        {
            _output.WriteLine("跳过：DSH_TEST_ADB_ENV 未启用（注册环境见 docs/agents/test-emulator.md）");
            return;
        }
        Adb("-s", Serial, "shell", "am", "start", "-a", "android.settings.WIFI_SETTINGS");
        Thread.Sleep(2000);

        var (w, h) = Viewport();
        var (checked0, bounds) = DumpWifiSwitch();
        var cx = (bounds.Item1 + bounds.Item3) / 2;
        var cy = (bounds.Item2 + bounds.Item4) / 2;

        // 真实 locator：归一化 bounds + device-viewport（frame 规则 A）
        var locator = new SpatialLocator(
            bounds.Item1 / (double)w, bounds.Item2 / (double)h,
            bounds.Item3 / (double)w, bounds.Item4 / (double)h,
            AdbEffectDriver.SupportedFrame);
        var request = new DispatchRequest(
            new DeliveryTarget("occ-wifi-switch", locator), "set-switch", "true", "rev-env");

        var driver = new AdbLiveEffectDriver(Serial, w, h, clock: () => DateTimeOffset.UnixEpoch);

        // 第一段：tap → DeliveryCompleted（命令像素精确）→ 世界翻转经 dump 证实
        var r1 = driver.Deliver(request);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, r1.Outcome);
        Assert.Equal($"adb -s {Serial} shell input tap {cx} {cy}", r1.Report);
        Thread.Sleep(1200);
        var (checked1, _) = DumpWifiSwitch();
        Assert.Equal(!checked0, checked1);   // attempt≠effect：翻转由观察证实

        // 第二段（对称 + I-3 活教材回正）：再 tap → 翻回原状态
        var r2 = driver.Deliver(request);
        Assert.Equal(DispatchOutcome.DeliveryCompleted, r2.Outcome);
        Thread.Sleep(1200);
        var (checked2, _) = DumpWifiSwitch();
        Assert.Equal(checked0, checked2);
    }
}
