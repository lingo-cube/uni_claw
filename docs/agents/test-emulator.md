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

### DeepSeek出口例外（2026-10-18，Owner配置与ADR-0053验收）

Owner已在DSH家目录`.env`的`NO_PROXY`与`no_proxy`加入`api.deepseek.com,.deepseek.com`。DeepSeek官方请求按该持久配置直连，其他provider继续沿用原代理规则；不得把本说明解释为临时绕过家目录配置。环境快照在专线启动时加载，修改后需重载拥有的3081实例。正式模型以当前`.dsh/product/uniagent-prod-bindings.yaml`及trial实际model selection为准，上表GLM耗时属于历史基准。

本次只更新DeepSeek实现线路事实，未改变设备AVD、尺寸、lease或AdbEnvironmentTests设备断言。实际服务PID、家目录配置指纹、模型调用时段的直连socket正向见证及采样边界，见[evidence/runtime-plan-001/acceptance-direct-20261018/results.md](../../evidence/runtime-plan-001/acceptance-direct-20261018/results.md)；新旧性能不得混算。

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

### RUNTIME-PLAN-004 当前生成验收参数（2026-10-20）

RUNTIME-PLAN-004历史对照使用`deepseek-official/deepseek-flash`，未采用Pro；当时宿主配置为`thinking: enabled`、`reasoningEffort: low`，两个配置入口（default model与DeepSeek provider）一致。该参数在实际请求头和reasoning block中已取证。Profile25/Prompt24/Skill4及236项源、固定输入与门槛不变的对照中，off批首稿18/21、最终19/21；low批首稿20/21、最终21/21，21份模型输出全部经正式codec交给真实Kernel仿真消费通过。low批平均单次正式咨询4.07秒、中位3.27秒；此为有限样本记录，不是未来正确性、设备性能或网络延迟归因保证。

参数属于DSH宿主adapter，未进入host-neutral Profile；代理配置保持Owner已设置的持久规则。完整对照、当前服务指纹、失败批次与私有配置恢复路径索引见`evidence/runtime-plan-004/generation/thinking-low-config-change.json`、`thinking-low-controlled-comparison.json`及`epoch-007/leader-evidence-audit.json`。私有配置备份保留在索引指定的本机临时目录，不包含进仓证据的凭据正文。

### 2026-10-20 CommandCode Flash正式绑定（所有者授权）
当前专用3081及`.dsh/product/uniagent-prod-bindings.yaml`选择`commandcode/deepseek/deepseek-v4.1-flash`，复用宿主既有pi-ai provider和`COMMANDCODE_API_KEY`凭据引用；无需把provider或凭据写入产品Profile/Prompt/Skill。原官方Flash choice保留。宿主agent默认effort值为low，但CommandCode真实request/header没有显式reasoningEffort，验收登记为null/provider-default，不宣称与官方enabled/low相同。代理与凭据未改。
专用3081已移除无关Jev MCP sidecar，确保正式Agent系统上下文只有既有产品Prompt与UI测试Skill两段；CommandCode模型provider独立保留。变更及私有恢复位置见`evidence/runtime-plan-005/commandcode-binding-change.json`与`commandcode-context-isolation.json`；固定输入和验收门槛见`preregistration-epoch007.json`，结果以相应TRX及`live-summary.json`为准。官方402余额不足与旧malformed批次保留，不能计入新通道通过率。


### WORLD-OBSERVATION-001 慢感知请求强度（2026-10-21所有者补充）
无XML身份恢复成功验收必须包含慢模型请求high。仍使用当前DSH绑定的provider/model；高档通过`/slow`隔离session的请求waterfall施加，不更改主Agent默认强度。专用uniclaw-product剖面的CommandCode既有模型补充high可选档位及reasoning_effort兼容声明，凭据/代理不变；回执未见high或供应方拒绝均不算该路径通过。部署备份和指纹见`changes/WORLD-OBSERVATION-001/evidence/slow-high-deployment.json`；实际验收结果以该Change最终验证记录为准，不能以配置声明代替请求证据。
