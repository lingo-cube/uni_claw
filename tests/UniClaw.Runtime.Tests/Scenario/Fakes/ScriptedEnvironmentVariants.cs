using System.Collections.Immutable;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>
/// Scenario 的 Initial World 数据变体工厂（scenarios/catalog.md「Initial World」段）：
/// happy / startup-fg-fail / switch-stuck / missing-target / same-text / launcher-drift（C1）/
/// flicker-target（C2）/ unrecoverable（C3）/ uncertain-action-effect-applied / uncertain-action-effect-absent（SC-P3-001）/
/// popup-dismiss-continuous / popup-dismiss-rejected / popup-dismiss-page-changed（SC-P3-002 Task 1.1）/
/// viewport-continuous / viewport-stale / viewport-page-changed（SC-P3-003 Task 1.1）/
/// viewport-exploration-*（SC-P3-CAND-007 Task 1.1）。
/// 测试注入数据，可含 "WiFi" 等场景字符串（生产 Runtime 不硬编码场景字符串 — 裁决 11）。
/// 每个工厂返回全新实例（fake 是单次 run 状态 owner；确定性 = 相同动作序列产生相同观察序列）。
/// </summary>
public static class ScriptedEnvironmentVariants
{
    /// <summary>SC-P1-001 happy：Settings Main → Network Settings → WiFi Settings → SetSwitch(ON) → 开关 true。</summary>
    public static ScriptedEnvironment Happy() => new(
        "Launcher", "SettingsMain",
        [Launcher(), SettingsMain(), NetworkSettings(), WiFiSettings(), WiFiSettingsOn()]);

    /// <summary>CP-06 initial-goal-satisfied：LaunchApp 后首屏即 WiFiSettingsOn（WiFi 开关已 ON）；
    /// 初始 Observation 已满足 Goal——空 Plan 时无需 dispatch 任何 Plan 步即可完成。</summary>
    public static ScriptedEnvironment InitialGoalSatisfied() => new(
        "Launcher", "WiFiSettingsOn",
        [Launcher(), WiFiSettingsOn()]);

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

    /// <summary>
    /// SC-P3-001 正向：Tap(Network &amp; Internet) 应用 SettingsMain → NetworkSettings 世界转场，
    /// 但 transport outcome 返回 TimedOut；后续 Observe 可见目标世界。
    /// </summary>
    public static ScriptedEnvironment UncertainActionEffectApplied() => new(
        "Launcher", "SettingsMain",
        [Launcher(), UncertainSettingsMain("NetworkSettings"), NetworkSettings()]);

    /// <summary>
    /// SC-P3-001 负向：同一 Tap 返回 TimedOut，但转场自环到 SettingsMain，世界效果未发生；
    /// 后续 Observe 仍显示原世界。
    /// </summary>
    public static ScriptedEnvironment UncertainActionEffectAbsent() => new(
        "Launcher", "SettingsMain",
        [Launcher(), UncertainSettingsMain("SettingsMain"), NetworkSettings()]);

    /// <summary>
    /// SC-P3-002 正向 Fixture：seq1 显示底层 NetworkSettings；seq2 外部 Popup 自发出现；
    /// Tap(Dismiss) Dispatched 并回到底层页；seq3 fresh Observation 再次支持 NetworkSettings。
    /// </summary>
    public static ScriptedEnvironment PopupDismissContinuous() => PopupObstruction(
        dismissTarget: "PopupUnderlyingNetworkSettings",
        dispatchOutcome: ActionResultOutcome.Dispatched);

    /// <summary>
    /// SC-P3-002 dismiss failure Fixture：Popup 出现后 Tap(Dismiss) 返回 Rejected，世界自环保持 Popup；
    /// subsequent Observation 不伪造底层页恢复。
    /// </summary>
    public static ScriptedEnvironment PopupDismissRejected() => PopupObstruction(
        dismissTarget: "PopupObstruction",
        dispatchOutcome: ActionResultOutcome.Rejected);

    /// <summary>
    /// SC-P3-002 continuity-failure Fixture：dismiss 动作 Dispatched，但 subsequent Observation 显示
    /// SettingsMain（不同 semantic Container），因此不能证明原 NetworkSettings Container 连续。
    /// </summary>
    public static ScriptedEnvironment PopupDismissPageChanged() => PopupObstruction(
        dismissTarget: "PopupChangedSettingsMain",
        dispatchOutcome: ActionResultOutcome.Dispatched);

    /// <summary>SC-P3-002 Task 3.1 正向 Runtime 组合：Startup/initial observe 后，首个 local step 的 post-observe 出现 Popup。</summary>
    public static ScriptedEnvironment PopupRuntimeContinuous() => PopupObstruction(
        dismissTarget: "PopupUnderlyingNetworkSettings",
        dispatchOutcome: ActionResultOutcome.Dispatched,
        popupObservationSequence: 3);

    /// <summary>SC-P3-002 Task 3.1 Rejected Runtime 组合。</summary>
    public static ScriptedEnvironment PopupRuntimeDismissRejected() => PopupObstruction(
        dismissTarget: "PopupObstruction",
        dispatchOutcome: ActionResultOutcome.Rejected,
        popupObservationSequence: 3);

    /// <summary>SC-P3-002 Task 3.1 continuity-failure Runtime 组合。</summary>
    public static ScriptedEnvironment PopupRuntimePageChanged() => PopupObstruction(
        dismissTarget: "PopupChangedSettingsMain",
        dispatchOutcome: ActionResultOutcome.Dispatched,
        popupObservationSequence: 3);

    /// <summary>SC-P3-003 正向：A/B/C → one ScrollForward → fresh D/E/F，同一测试语义页。</summary>
    public static ScriptedEnvironment ViewportContinuous() => ViewportMovement("ViewportBottom");

    /// <summary>SC-P3-003 stale evidence：世界转场到 D/E/F，但第二次 Observe 返回与 pre-action 相同序号。</summary>
    public static ScriptedEnvironment ViewportStale() => ViewportMovement(
        "ViewportBottom",
        observeSequenceOverrides: new Dictionary<long, long> { [2] = 1 });

    /// <summary>SC-P3-003 identity conflict：fresh post-action evidence 显示不同测试语义页。</summary>
    public static ScriptedEnvironment ViewportPageChanged() => ViewportMovement("ViewportOtherPage");

    /// <summary>SC-P3-003 Runtime 组合 stale：Startup/initial/Tap 后的 viewport post-observe 不推进序号。</summary>
    public static ScriptedEnvironment ViewportRuntimeStale() => ViewportMovement(
        "ViewportBottom",
        observeSequenceOverrides: new Dictionary<long, long> { [4] = 2 });

    /// <summary>SC-P3-003 Rejected：targetless action 被环境拒绝；Traversal 不应 Observe 或重复派发。</summary>
    public static ScriptedEnvironment ViewportRejected() => ViewportMovement(
        "ViewportBottom",
        dispatchOutcome: ActionResultOutcome.Rejected);

    /// <summary>SC-P3-CAND-007 positive：V1 → V2 → V3(end)，两次独立 bounded movement。</summary>
    public static ScriptedEnvironment ViewportExplorationPositive() => ViewportExploration(
        firstTarget: "ViewportExploreMiddle");

    /// <summary>SC-P3-CAND-007 ambiguous：fresh evidence 与 V1 相同，Fake 不宣称 exhaustion。</summary>
    public static ScriptedEnvironment ViewportExplorationAmbiguousSame() => ViewportExploration(
        firstTarget: "ViewportExploreSame");

    /// <summary>SC-P3-CAND-007 rejected：dispatch 被拒绝；world/semantic exhaustion 仍未获证明。</summary>
    public static ScriptedEnvironment ViewportExplorationRejected() => ViewportExploration(
        firstTarget: "ViewportExploreMiddle",
        dispatchOutcome: ActionResultOutcome.Rejected);

    /// <summary>SC-P3-CAND-007 stale：world 转到 V2，但 fresh-sequence proof 缺失。</summary>
    public static ScriptedEnvironment ViewportExplorationStale() => ViewportExploration(
        firstTarget: "ViewportExploreMiddle",
        observeSequenceOverrides: new Dictionary<long, long> { [2] = 1 });

    /// <summary>SC-P3-CAND-007 Runtime 组合 stale：Startup/initial 后的 first viewport post-observe 不推进序号。</summary>
    public static ScriptedEnvironment ViewportExplorationRuntimeStale() => ViewportExploration(
        firstTarget: "ViewportExploreMiddle",
        observeSequenceOverrides: new Dictionary<long, long> { [3] = 2 });

    /// <summary>SC-P3-CAND-007 formal 组合 stale：Startup/initial/progress-Tap 后的 viewport evidence 不推进。</summary>
    public static ScriptedEnvironment ViewportExplorationFormalStale() => ViewportExploration(
        firstTarget: "ViewportExploreMiddle",
        observeSequenceOverrides: new Dictionary<long, long> { [4] = 2 });

    /// <summary>SC-P3-CAND-007 continuity conflict：movement 后 evidence 属于另一 semantic page。</summary>
    public static ScriptedEnvironment ViewportExplorationPageChanged() => ViewportExploration(
        firstTarget: "ViewportExploreOtherPage");

    private static ScreenConfig Launcher() => new("Launcher", "Launcher", []);

    private static ScreenConfig SettingsMain() => new(
        "SettingsMain", "Settings",
        [new ElementConfig("Network & Internet", null, new TransitionConfig(ScreenTransitionAction.Tap, "NetworkSettings"))]);

    private static ScreenConfig NetworkSettings() => new(
        "NetworkSettings", "Settings",
        [new ElementConfig("WiFi", null, new TransitionConfig(ScreenTransitionAction.Tap, "WiFiSettings"))]);

    private static ScreenConfig UncertainSettingsMain(string transitionTarget) => new(
        "SettingsMain", "Settings",
        [
            new ElementConfig(
                "Network & Internet",
                null,
                new TransitionConfig(
                    ScreenTransitionAction.Tap,
                    transitionTarget,
                    DispatchOutcome: ActionResultOutcome.TimedOut)),
        ]);

    private static ScriptedEnvironment PopupObstruction(
        string dismissTarget,
        ActionResultOutcome dispatchOutcome,
        long popupObservationSequence = 2) => new(
        "PopupUnderlyingNetworkSettings",
        launchNextScreenName: null,
        [
            new ScreenConfig(
                "PopupUnderlyingNetworkSettings",
                "Settings",
                [new ElementConfig("WiFi", null, null)]),
            new ScreenConfig(
                "PopupObstruction",
                "Settings",
                [
                    new ElementConfig(
                        "Dismiss",
                        null,
                        new TransitionConfig(
                            ScreenTransitionAction.Tap,
                            dismissTarget,
                            DispatchOutcome: dispatchOutcome)),
                ]),
            new ScreenConfig(
                "PopupChangedSettingsMain",
                "Settings",
                [new ElementConfig("Network & Internet", null, null)]),
        ],
        observeScreenTransitions: new Dictionary<long, string>
        {
            [popupObservationSequence] = "PopupObstruction",
        });

    private static ScriptedEnvironment ViewportMovement(
        string viewportTarget,
        ActionResultOutcome dispatchOutcome = ActionResultOutcome.Dispatched,
        IReadOnlyDictionary<long, long>? observeSequenceOverrides = null) => new(
        "ViewportTop",
        launchNextScreenName: null,
        [
            new ScreenConfig(
                "ViewportTop",
                "Settings",
                [
                    new ElementConfig("A", null, null),
                    new ElementConfig("B", null, null),
                    new ElementConfig("C", null, null),
                ],
                new ViewportTransitionConfig(viewportTarget, dispatchOutcome)),
            new ScreenConfig(
                "ViewportBottom",
                "Settings",
                [
                    new ElementConfig("D", null, null),
                    new ElementConfig("E", null, null),
                    new ElementConfig("F", null, null),
                ]),
            new ScreenConfig(
                "ViewportOtherPage",
                "Settings",
                [new ElementConfig("Other semantic page", null, null)]),
        ],
        observeSequenceOverrides: observeSequenceOverrides);

    private static ScriptedEnvironment ViewportExploration(
        string firstTarget,
        ActionResultOutcome dispatchOutcome = ActionResultOutcome.Dispatched,
        IReadOnlyDictionary<long, long>? observeSequenceOverrides = null) => new(
        "ViewportExploreStart",
        launchNextScreenName: null,
        [
            new ScreenConfig(
                "ViewportExploreStart",
                "Settings",
                [
                    new ElementConfig("A", null, null),
                    new ElementConfig("B", null, null),
                    new ElementConfig("C", null, null),
                    new ElementConfig("More content", null, null),
                ],
                new ViewportTransitionConfig(firstTarget, dispatchOutcome)),
            new ScreenConfig(
                "ViewportExploreMiddle",
                "Settings",
                [
                    new ElementConfig("B", null, null),
                    new ElementConfig("C", null, null),
                    new ElementConfig("D", null, null),
                    new ElementConfig("More content", null, null),
                ],
                new ViewportTransitionConfig("ViewportExploreEnd")),
            new ScreenConfig(
                "ViewportExploreEnd",
                "Settings",
                [
                    new ElementConfig("C", null, null),
                    new ElementConfig("D", null, null),
                    new ElementConfig("E", null, null),
                    new ElementConfig("End of list", null, null),
                ]),
            new ScreenConfig(
                "ViewportExploreSame",
                "Settings",
                [
                    new ElementConfig("A", null, null),
                    new ElementConfig("B", null, null),
                    new ElementConfig("C", null, null),
                    new ElementConfig("More content", null, null),
                ]),
            new ScreenConfig(
                "ViewportExploreOtherPage",
                "Settings",
                [new ElementConfig("Other semantic page", null, null)]),
        ],
        observeSequenceOverrides: observeSequenceOverrides);

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
