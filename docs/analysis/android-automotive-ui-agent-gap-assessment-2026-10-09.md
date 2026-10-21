# Android 手机/平板与车机 Android UI 自动化 Agent Gap 调研

日期：2026-10-09  
范围：只审查 Android 手机/平板、AAOS/Android Auto/head unit 相关能力；删除通用 Web、iOS、Windows 内容。本文只读，不修改产品代码。
> Status: DRAFT（研究输入，未授权实现）
> Authority: NONE

## 结论

当前仓库已经有 Android 手机/平板 Settings 的真实闭环基础：ADB 截屏、UiAutomator XML、设备预检、可替换的 Effect Boundary、Settings traversal ledger、Wi-Fi 场景和 TRX/scenario 覆盖治理。

车机方向目前没有实现级能力。仓库中唯一直接的车机证据是设计推演文档中的 A1-A6 场景表；该文档明确写着“车机/机械臂尚无 realization”（[decision-granularity-scenarios-v0.1.md:28](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:28)、[decision-granularity-scenarios-v0.1.md:130](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:130)）。对仓库源码、工具、测试集和配置检索 `AAOS`、`Android Auto`、`VHAL`、`Vehicle HAL`、`CarService`、`head unit`、`车机` 等关键词，未发现专用实现、环境启动器、VHAL adapter、Android Auto testset 或车机 scenario。

## 现有 Android 手机/平板能力

| 垂直 | 当前证据 | 结论 |
|---|---|---|
| 设备环境 | `docs/agents/test-emulator.md` 注册 `p26_pixel`，ADB、截图、UiAutomator、Settings probe；`tools/android/start-test-device.sh` 负责 supervisor/preflight | 有单一 Android 手机形态的可复现模拟器基线 |
| 结构化观察 | `src/UniClaw.Host/UiAutomatorDump.cs` 与 `tests/UniClaw.Host.Tests/UiHierarchyTypedParseTests.cs` | 能解析 Android accessibility/UI hierarchy、bounds、checked/clickable 等字段 |
| 真实动作 | `AdbLiveEffectDriver`、`SettingsTraversalLiveTests`、`AdbEnvironmentTests` | 有真实 ADB tap/swipe 与重新 dump 验证；范围主要是 Settings/Wi-Fi |
| 安全策略 | `testsets/android-settings/decoy-target-policy.json`、`product/policy/android-settings-forbidden-actions.json` | Settings 有 forbidden-action 和 wrong-target fail-closed 测试资产 |
| 场景治理 | `scenarios/SCN-WIFI-*`、`tools/scenario-coverage.py` | 场景映射测试并由真实 TRX outcome 派生状态 |

## P0：车机没有真实 realization，且 Android 设备模型仍是单屏手机假设

### 证据

- 车机场景仅存在设计表：全菜单遍历、空调温度、导航回家、物理按键、双屏主题、驾驶模式限制（[decision-granularity-scenarios-v0.1.md:28](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:28)）。文档把它们标为场景推演，且明确没有 realization（[decision-granularity-scenarios-v0.1.md:128](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:128)）。
- 设备环境只注册 `p26_pixel`、1080×2400 AVD 和手机 Settings 入口（[test-emulator.md:8](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:8)、[test-emulator.md:12](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:12)、[test-emulator.md:17](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:17)）。
- DSE-002 仍把 viewport 语义写成单设备全屏；“多屏/车机 frame”被列为未来 buyer 的 residual risk（[DSE-002 state.md:99](/Users/fran/Documents/Code/spacex/uni_claw/changes/DSE-002/state.md:99)）。

### 缺口与影响

没有 AAOS emulator/device profile、cluster/副屏模型、head-unit display identity、driver distraction state、parked/driving restriction、CarService 或 VHAL 读写路径。手机上的单一 `device-viewport`、单窗口前台 app、Android Settings route 不能证明中控屏、仪表屏、后排屏或旋钮/方向盘输入的正确性。

### 补齐建议

先建立 AAOS 专用 capability/环境 contract，而不是把车机字段塞进手机路径：

1. `AutomotiveEnvironmentProfile`：AAOS API/build、head-unit identity、display/cluster 列表、DPI/分辨率、驾驶/驻车状态、locale、network、power。
2. `VehicleStateProvider` 与 `VehicleEffectDriver` seam：对 VHAL/CarService 只保留显式 adapter，区分观察事实、模拟注入和真实车辆信号；未知或不安全状态必须 fail-closed。
3. 第一条真 buyer 用 AAOS emulator + mock VHAL 验证：温度调整、音量物理输入、驾驶态禁用项、双屏一致性；每一步都要求动作前状态和动作后新观察。

## P0：Android Auto/head unit 交互入口完全缺失

当前仓库没有 Android Auto projection/session、head-unit transport、Car App Library、rotary controller、steering-wheel key、voice/assistant、projection disconnect/reconnect 或 host validation 的实现和测试。`tools/android/device-capabilities.yaml` 只有 adb、screenshot、uiautomator 等通用 Android 能力，没有车机/投屏/车辆信号字段。

影响是：即使把手机 Settings 测试扩展到更大屏幕，也不能测试 Android Auto 的 projected surface、连接生命周期、宿主限制和车机输入模型。

建议把 Android Auto 与 AAOS native UI 分成两个受控 adapter：

- AAOS native：设备 UI + CarService/VHAL + 多 display。
- Android Auto：手机端 projection session + head-unit host + disconnect/reconnect + host policy。

两者共用观察/断言/证据协议，但不共用未经验证的 locator 或 effect 语义。

## P1：Android 动作词汇不足以覆盖手机/车机测试

当前 ADB 驱动支持集是 tap/click/set-switch/swipe，测试还明确把超出支持集的动作 fail-closed。缺少文本输入、长按、拖拽、多指、旋钮/方向盘按键、back/home/app switch、IME/权限对话框、媒体键和 vehicle property effect。

手机端影响是表单、登录、通知、权限和复杂滚动无法形成通用测试；车机端影响更大：空调温度、音量、物理键、旋钮、驾驶模式限制均无法执行或验证。

建议按 effect domain 分层冻结支持集：`android.touch`、`android.input`、`android.system`、`automotive.rotary`、`automotive.key`、`automotive.vehicle-property`。每种 effect 都要带设备/display/vehicle-state 约束和真实 receipt，禁止通过坐标或旧 handle 猜测替代。

## P1：观察模型缺少车机的多屏、车辆状态和动态限制

现有 UiAutomator 观察适合单设备 UI hierarchy；没有 display/cluster/zone/occupant context，也没有车速、驻车、驾驶员注意力、档位、HVAC/音量/媒体等车辆状态的 provenance。设计中的 A5 双屏一致性和 A6 驾驶模式禁用项只是推演场景，不是 executable scenario（[decision-granularity-scenarios-v0.1.md:35](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:35)、[decision-granularity-scenarios-v0.1.md:37](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:37)）。

建议将 `DisplayContext`、`VehicleContext`、`DrivingRestriction` 作为同一 capture cycle 的 typed evidence；把 UI 观察与车辆信号按 correlation id 关联，明确实时状态、模拟 fixture、历史 memory 不能互相替代。测试 oracle 要能分别断言“控件不可用”“effect 被拒绝”“副屏一致”“车辆信号变化”。

## P1：测试环境和 testset 只有手机 Settings，缺 AAOS/VHAL fixture 生命周期

现有环境要求通过 `start-test-device.sh`、supervisor、截图、UiAutomator 和 Settings probe 后才 READY（[test-emulator.md:32](/Users/fran/Documents/Code/spacex/uni_claw/docs/agents/test-emulator.md:32)）。`testsets/android-settings/manifest.json` 的 fixtures 也都是 Settings root、Wi-Fi、非 checkable target 和 forbidden-action policy；没有 AAOS、VHAL、CarService、双屏或 Android Auto 资产。

缺口包括：AAOS emulator/VHAL mock 启停、vehicle property reset、display/seat/zone fixture、driving/parked 状态 lease、head-unit session lease、投屏断线恢复和并发隔离。建议新增 `testsets/android-automotive/`，把环境声明、VHAL fixture、display fixture、车辆状态、输入设备和 acceptance refs 分开，运行结束必须有 teardown receipt；环境不可用只产 `ENVIRONMENT_UNAVAILABLE`/`pending-env`，不能变成产品 PASS。

## P1：车机安全策略与危险动作没有产品化

手机 Settings 已有 Android forbidden-action policy，但没有车机对应策略。A6 驾驶模式限制目前只是文档场景：车速下禁用项不可点，点成功应记录缺陷；还没有 policy schema、VHAL 状态注入、人工 gate 或 evidence 绑定。

建议定义 automotive action policy：按 driving state、vehicle property、display/occupant、可逆性和外部副作用分类。驾驶中导航输入、视频/键盘、设置修改、HVAC/媒体控制应有明确 allowed/blocked/unknown 规则；未知车辆状态必须停止，不能默认 parked 或允许 effect。

## P2：车机专用策略、记忆和报告面尚未形成

现有 coverage director 是 Android Settings 专用，场景推演提出 L2 泛化策略（例如全菜单遍历、温度循环），但 L2 尚未成为可执行产品能力（[decision-granularity-scenarios-v0.1.md:17](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:17)、[decision-granularity-scenarios-v0.1.md:119](/Users/fran/Documents/Code/spacex/uni_claw/docs/design/decision-granularity-scenarios-v0.1.md:119)）。车机场景还缺测试 oracle、异常降级、跨版本策略和车厂配置 provenance。

建议在完成 P0/P1 buyer 后再做：车机页面 graph、车辆配置 profile、版本/车型/cluster 差异策略、L2 traversal/temperature policy、跨 run memory 和 report projection。记忆只能提出策略或解释差异，不能替代当前 AAOS/车辆 evidence，也不能授权 effect。

## 优先级总表

| 优先级 | 必须补齐 |
|---|---|
| P0 | AAOS 专用环境与观察/执行 contract；真实 AAOS emulator + mock VHAL buyer；多屏/head-unit identity；Android Auto projection/session adapter；动作前安全门与动作后重新观察 |
| P1 | 车机 effect vocabulary（旋钮/方向盘/vehicle property/多屏）；VehicleContext/DrivingRestriction typed evidence；AAOS/VHAL/CarService fixture、lease、reset、teardown；驾驶态危险动作 policy；手机端输入/权限/表单动作扩展 |
| P2 | L2 车机遍历和温度循环策略；车型/版本/配置 memory；跨 run report、性能/响应时间 oracle、车厂差异管理和 flaky quarantine |

## 核查结果与边界

- 已核查关键词：AAOS、Android Auto、VHAL、Vehicle HAL、CarService、head unit、车机、车载、汽车；未发现专用源码目录、环境脚本、VHAL adapter、AAOS/Auto testset 或 executable scenario。
- `docs/design/decision-granularity-scenarios-v0.1.md` 的“车机测试域”是设计推演，文档明确车机尚无 realization，不能当作现有能力证据。
- 当前报告保留手机/平板 Android 的真实能力和与车机直接相关的共性 gap；不把通用 Web、iOS、Windows 缺口带入本专项结论。
