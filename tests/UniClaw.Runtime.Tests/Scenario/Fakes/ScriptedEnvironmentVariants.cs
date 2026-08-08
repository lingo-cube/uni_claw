namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// 5 个 Scenario 的 Initial World 数据变体工厂（scenarios/catalog.md「Initial World」段）：
/// happy / startup-fg-fail / switch-stuck / missing-target / same-text。
/// 测试注入数据，可含 "WiFi" 等场景字符串（生产 Runtime 不硬编码场景字符串 — 裁决 11）。
/// 每个工厂返回全新实例（fake 是单次 run 状态 owner；确定性 = 相同动作序列产生相同观察序列）。
/// </summary>
public static class ScriptedEnvironmentVariants
{
    /// <summary>SC-P1-001 happy：Settings Main → Network Settings → WiFi Settings → SetSwitch(ON) → 开关 true。</summary>
    public static ScriptedEnvironment Happy() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), NetworkSettings(), WiFiSettings(), WiFiSettingsOn()]);

    /// <summary>SC-P1-002 startup-fg-fail：LaunchApp 后前台仍为 "Launcher"（≠ 目标应用，ForegroundApplication 验证失败）。</summary>
    public static ScriptedEnvironment StartupForegroundFail() => new(
        "Launcher", null,
        [Launcher()]);

    /// <summary>SC-P1-003 负向 switch-stuck：SetSwitch(ON) 不改变开关状态（开关物理卡住，世界不变）。</summary>
    public static ScriptedEnvironment SwitchStuck() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), NetworkSettings(), WiFiSettingsStuck()]);

    /// <summary>SC-P1-004 missing-target：Network Settings 只含 "Bluetooth"（无 "WiFi" 候选 → Traversal.Select 失败）。</summary>
    public static ScriptedEnvironment MissingTarget() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), NetworkSettingsBluetoothOnly()]);

    /// <summary>SC-P1-005 same-text：WiFi Settings 含标题与开关两个 "WiFi"（Index 稳定可区分）。世界数据与 happy 相同（catalog）。</summary>
    public static ScriptedEnvironment SameText() => Happy();

    private static ScreenConfig Launcher() => new("Launcher", "Launcher", []);

    private static ScreenConfig SettingsMain() => new(
        "SettingsMain", "Settings",
        [new ElementConfig("Network & Internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkSettings"))]);

    private static ScreenConfig NetworkSettings() => new(
        "NetworkSettings", "Settings",
        [new ElementConfig("WiFi", null, new TransitionConfig(ScreenTransitionAction.Tap, "WiFiSettings"))]);

    private static ScreenConfig NetworkSettingsBluetoothOnly() => new(
        "NetworkSettings", "Settings",
        [new ElementConfig("Bluetooth", null, null)]);

    private static ScreenConfig WiFiSettings() => new(
        "WiFiSettings", "Settings",
        [
            new ElementConfig("WiFi", null, null),
            new ElementConfig("WiFi", false, new TransitionConfig(ScreenTransitionAction.SetSwitch, "WiFiSettingsOn", true)),
        ]);

    private static ScreenConfig WiFiSettingsStuck() => new(
        "WiFiSettings", "Settings",
        [
            new ElementConfig("WiFi", null, null),
            new ElementConfig("WiFi", false, null),
        ]);

    private static ScreenConfig WiFiSettingsOn() => new(
        "WiFiSettingsOn", "Settings",
        [
            new ElementConfig("WiFi", null, null),
            new ElementConfig("WiFi", true, null),
        ]);
}
