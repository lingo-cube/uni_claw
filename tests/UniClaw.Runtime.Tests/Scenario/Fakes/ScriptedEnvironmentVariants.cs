using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// Scenario 的 Initial World 数据变体工厂（scenarios/catalog.md「Initial World」段）：
/// happy / startup-fg-fail / switch-stuck / missing-target / same-text / launcher-drift（C1）/
/// flicker-target（C2）/ unrecoverable（C3）。
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

    /// <summary>
    /// C1 launcher-drift（SC-P2-001 数据变体）：Step-2 post-action（seq=4）注入一次性观测掩码 —
    /// Foreground="Launcher" + 不可解析元素（"Phone"/"Messages" — ScenarioIdentity 无法判别语义页面
    /// → SemanticPage=null → Agent-scope drift 触发）。空元素列表会解析为 "Launcher"（压掉 drift），
    /// 故掩码必须用不可解析元素（drift 前置条件）。mask 不改变当前屏幕、不进 ActionHistory；
    /// Relaunch 后回到 SettingsMain（seq=5）。
    /// C4 扩展：屏幕集含完整恢复链（SettingsMain → NetworkSettings → WiFiSettings → WiFiSettingsOn）—
    /// SC-P2-001 恢复后续跑（位置恢复 → Step-2/3）需要开关屏幕完成 SetSwitch(ON) → Completed；
    /// 掩码不变，C1 观测序列（seq1-5）字节级不变。
    /// </summary>
    public static ScriptedEnvironment LauncherDrift() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), NetworkSettings(), WiFiSettings(), WiFiSettingsOn()],
        observeOverrides: new Dictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>
        {
            [4] = ("Launcher", ImmutableArray.Create(
                new ObservedElement("Phone", null, 0),
                new ObservedElement("Messages", null, 1))),
        });

    /// <summary>
    /// C2 flicker-target（SC-P2-002 数据变体）：观测侧 flicker — 世界（屏幕）含 "Bluetooth" + "WiFi"
    /// （FlickerNetworkSettings），但 Step-1 post-action 首次观测（seq3，掩码）只"看到" "Bluetooth"
    /// （观测是不完整证据 — I-4）；重试 re-observe（seq4，掩码）看到 "Bluetooth" + "WiFi" →
    /// Traversal Step-scope retry re-resolve 命中（WiFi Index=1）。与 C1 同机制：一次性观测掩码 —
    /// 仅替换观测，不改变当前屏幕（世界无 flicker，观测有 flicker）。
    /// C5 扩展：屏幕集含完整完成链（SettingsMain → NetworkSettings → WiFiSettings → WiFiSettingsOn）—
    /// SC-P2-002 Then（重试成功后 Step-3 SetSwitch(ON) → Completed）需要开关屏幕；
    /// 掩码不变，C2 观测序列（seq1-5）字节级不变。
    /// </summary>
    public static ScriptedEnvironment FlickerTarget() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), FlickerNetworkSettings(), WiFiSettings(), WiFiSettingsOn()],
        observeOverrides: new Dictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>
        {
            [3] = ("Settings", ImmutableArray.Create(new ObservedElement("Bluetooth", null, 0))),
            [4] = ("Settings", ImmutableArray.Create(
                new ObservedElement("Bluetooth", null, 0),
                new ObservedElement("WiFi", null, 1))),
        });

    /// <summary>Network Settings 真实世界（flicker 恢复态）："Bluetooth" + "WiFi"（WiFi 是列表项，非开关 — SwitchState=null；
    /// 转场目标 WiFiSettings 在本变体屏幕集中（C5 — 重试成功后进入开关屏幕完成 Step-3 SetSwitch(ON) → Completed）。</summary>
    private static ScreenConfig FlickerNetworkSettings() => new(
        "NetworkSettings", "Settings",
        [
            new ElementConfig("Bluetooth", null, null),
            new ElementConfig("WiFi", null, new TransitionConfig(ScreenTransitionAction.Tap, "WiFiSettings")),
        ]);

    /// <summary>
    /// C3 unrecoverable（SC-P2-003 数据变体）：同 launcher-drift（seq4 = Launcher 前台 + 不可解析元素 → drift），
    /// 但 Relaunch 后恢复观测（seq5 掩码）前台仍为 "Launcher"（恢复动作无效 — 世界不配合）。
    /// LaunchApp 动作照常 Dispatched（fake 切换屏幕状态），但掩码使观测显示未恢复状态 —
    /// dispatch outcome ≠ world success（裁决 10 / I-9 在恢复语境延续）→ Recovery.Verify 判据失败。
    /// </summary>
    public static ScriptedEnvironment Unrecoverable() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), NetworkSettings()],
        observeOverrides: new Dictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>
        {
            [4] = ("Launcher", ImmutableArray.Create(
                new ObservedElement("Phone", null, 0),
                new ObservedElement("Messages", null, 1))),
            [5] = ("Launcher", ImmutableArray.Create(
                new ObservedElement("Phone", null, 0),
                new ObservedElement("Messages", null, 1))),
        });

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
