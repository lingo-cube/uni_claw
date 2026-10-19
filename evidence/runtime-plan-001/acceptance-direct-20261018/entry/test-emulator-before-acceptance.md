# 测试常用模拟器（注册环境）

> 本仓 Product Runtime 一切 ENVIRONMENT 级验收（真实设备目标）的**注册
> 标准模拟器**。ENVIRONMENT 类测试不得另起设备目标；变更本文件登记的
> 任何事实须同步 `tests/UniClaw.Kernel.Tests/AdbEnvironmentTests.cs` 并
> 在对应 change 留痕。

## 注册事实（provenance：legacy uni-agent 考古 + ADB-002 实测）

| 项 | 值 |
|---|---|
| AVD | `p26_pixel`（本机 `~/.android/avd/`，1080×2400 @ 420dpi） |
| serial | legacy 默认 `emulator-5554`；`tools/android/start-test-device.sh` 从 `emulator-5556` 起选择空闲端口，并通过 `UNICLAW_ANDROID_DEVICE` 注入 |
| emulator 二进制 | `/opt/homebrew/share/android-commandlinetools/emulator/emulator` |
| adb | Homebrew `adb`（37.0.1 实测） |
| boot 命令 | `/opt/homebrew/share/android-commandlinetools/emulator/emulator -avd p26_pixel`（legacy HANDOFF 同款；boot 实测 ~10s） |
| 环境入口（ADB-002） | `adb -s emulator-5554 shell am start -a android.settings.WIFI_SETTINGS`（Wi-Fi Settings 页，含 checkable Switch） |
| golden-run device-profile | 1080×1920 @ 420dpi（corpus 归一化假设的设备事实来源） |
| 备用 AVD | `scroll-test`（在场，未注册——无 buyer） |

## 使用约定

- **显式启用**：ENVIRONMENT 级测试以 `DSH_TEST_ADB_ENV=1` 开关启用；
  默认显式跳过（零影响全量套件——V2 断言面）。
- **fail-closed**：启用后前置缺失（模拟器离线 / adb 缺席 / 页面未打开）
  直接 FAIL，不静默跳过（legacy Tier-2 约定）。
- **生命周期由调用方显式管理**：测试不负责 boot/关机；调用方使用启动器
  完成环境预检并持有 run lease，正式流程结束后显式回收。
推荐使用 `tools/android/start-test-device.sh`，它会克隆 `p26_pixel`、选择
空闲 serial，并输出 `UNICLAW_ANDROID_DEVICE` 与 `UNICLAW_ANDROID_RUN_DIR`。

启动脚本通过独立的 emulator supervisor 托管实例：启动脚本退出不代表设备
已经可用，只有输出 `android_api`、`wm_size`、截图、uiautomator 和 Settings
探针全部通过后才会报告 READY。正式测试应在同一个 shell 会话中保留
`UNICLAW_ANDROID_RUN_DIR`，结束时调用 `tools/android/stop-test-device.sh`；
`.state` 保存 supervisor PID、serial 和 clone 目录，设备消失时应先看
`emulator.log`，再重新执行环境预检。
- 测试和 Host live selector 都读取这个 serial，不再假定固定端口。
- **快照锁恢复（ADB-003）**：启动报 `snapshot operation ... pending` FATAL 时，
  先确认无 emulator 进程，再清 `~/.android/avd/<avd>/*.lock` 并将
  `snapshots/` 改名备份后冷启动（`-no-snapshot` 路径）；禁止 ad-hoc 手起，
  修复后仍走本脚本。
- **DSH 专线重载（ADB-003）**：握手报 `schema-hash-mismatch` 时，比对
  `dsh/uniclaw-decision-channel/schema/schema-hash.txt` 与产品侧哈希——不一致
  即专线实例（3081）加载的是旧插件：重启该实例（dk-harness checkout 内
  `dsh web --port 3081 --no-open`）重载；3080 非注册线路，不得接入。
- **确定性纪律**：ENVIRONMENT 测试只验证「真实物理映射 + 世界变化经
  再观察证实」，不做性能断言、不依赖时序精度（轮询间隔为宽松常量）。

## 本地环境基准规则（汇总；2026-10-06 所有者指令固化）

一切 ENVIRONMENT 级（真实设备/真实模型）工作遵守：

1. **设备**：只用 `tools/android/start-test-device.sh` / `stop-test-device.sh`
   启停（supervisor 托管、READY 探针全过才可用）；禁止 ad-hoc 手起模拟器
   或绕过 `.state` 生命周期记录。
2. **模型线路**：真实咨询走专用 DSH 实例（见下节）；禁止临时改代理或
   绕过家目录代理配置直连 provider。
3. **预检**：正式流程前 environment-preflight 必须通过并落盘
   （`environment-preflight.json`）；预热与正式流程分段计时，不混记。
4. **诚实停止**：环境不可用一律 `ENVIRONMENT_UNAVAILABLE` 如实停止，
   不静默降级、不把环境失败改写为行为结论。
5. **证据归属**：preflight/trace/run 目录是运行证据，不回写产品配置、
   不提交生成运行文件（除评审决定保留的脱敏件）。

本节与「注册事实」同权：变更须在对应 change 留痕。

## DSH/模型线路基准（AGT-014 起）

| 项 | 值 / 约定 |
|---|---|
| 承载 | 专用 DSH 实例（本地 3081），绑定 settings 链路真实咨询（`zai-coding-cn/glm-5.3-flash`） |
| provider 访问 | DSH **家目录**配置 `HTTP/HTTPS proxy=127.0.0.1:7890`（一次性、持久化；重启 DSH 生效）。无代理时 provider 直连超时（实测 `UND_ERR_CONNECT_TIMEOUT` ≈10.5s）；配置后真实咨询通过（实测单次 ~11.2s） |
| 预检证据 | 正式流程前写 `environment-preflight.json`（含视觉服务预热与 `formalFlowStartedAfterPreflight` 标志）；预热耗时（实测 ~63.5s 视觉服务冷启动）与正式流程分段计时分开记录 |
| 诊断入口 | provider 不通先看专用实例日志与代理线路（排查顺序见 `evidence/agt-014/dsh-provider-timeout-diagnosis-2026-10-06.md`），不先怀疑上下文压力 |

## 运行速查

```bash
# 0) boot（若未在线；保持启动终端存活）
tools/android/start-test-device.sh
# 1) 把脚本输出的 serial 注入当前 shell 后确认在线（动态端口）
export UNICLAW_ANDROID_DEVICE="${UNICLAW_ANDROID_DEVICE:-$(adb devices | awk 'NR > 1 && $2 == "device" { print $1; exit }')}"
adb devices | grep "$UNICLAW_ANDROID_DEVICE"
# 2) 运行 ENVIRONMENT 验收
DSH_TEST_ADB_ENV=1 UNICLAW_ANDROID_DEVICE="$UNICLAW_ANDROID_DEVICE" dotnet test --filter AdbEnvironmentTests
# 3) 全量回归（默认跳过环境测试）
dotnet test
```

### 运维注意（实测）

- **快照锁残留**：上一次模拟器进程被硬杀后再 boot 报
  `FATAL | A snapshot operation ... pending and timeout has expired`。
  处置：确认无 emulator 进程（`pgrep -fl qemu-system`）后
  `rm ~/.android/avd/p26_pixel.avd/*.lock`，再正常 boot。
- boot 到 `sys.boot_completed=1` 实测 ~30s；`adb devices` 出现
  `device` 状态即可用。

## 相关 change

- ADB-001（L3-lite：真实 adb 二进制 × 假 serial，失败通道）
- ADB-002（V1：locator → tap → dump 证实世界翻转；本环境注册的建立者）
